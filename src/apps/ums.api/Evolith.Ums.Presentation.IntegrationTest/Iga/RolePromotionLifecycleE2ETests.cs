using Evolith.Ums.Application.IGA.DTOs;
using Evolith.Ums.Domain.Authorization.SystemSuite.MenuNode;
using Evolith.Ums.Domain.IGA;
using Evolith.Ums.Presentation.IntegrationTest.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using ProfileAggregate = Evolith.Ums.Domain.Authorization.Profile.Profile;
using RoleAggregate = Evolith.Ums.Domain.Authorization.Role.Role;
using RoleMaturityStatusAggregate = Evolith.Ums.Domain.IGA.RoleMaturityStatus.RoleMaturityStatus;
using RolePromotionRequestAggregate = Evolith.Ums.Domain.IGA.RolePromotionRequest.RolePromotionRequest;
using SystemSuiteAggregate = Evolith.Ums.Domain.Authorization.SystemSuite.SystemSuite;
using TemplateAggregate = Evolith.Ums.Domain.Authorization.Template.PermissionTemplate;

namespace Evolith.Ums.Presentation.IntegrationTest.Iga;

/// <summary>
/// G-087 (ADR-UMS-093 / ADR-UMS-096, D-014 / D-020): E2E de integración del <em>ciclo de vida completo</em>
/// de la promoción de rol IGA sobre un contenedor PostgreSQL real (Testcontainers), complementando el
/// <see cref="RolePromotionEffectE2ETests"/> —que sólo llega a <c>Execute</c> y verifica el efecto— y las
/// pruebas InMemory de <c>RolePromotionRequestRestEndpointTests</c>. Aquí se ejercitan, de forma
/// DETERMINISTA (los datos de siembra fuerzan cada rama, sin <c>BeOneOf</c>):
///
///   1. Ciclo feliz de BAJO riesgo de punta a punta —Create → Submit → ConfirmEligibility →
///      ManagerApprove(→Approved) → Execute → Verify— aseverando cada transición, que el <c>RiskScore</c>
///      congelado queda por debajo del umbral, y el EFECTO real (reasignación del rol del perfil objetivo).
///   2. Ruta de ALTO riesgo (RiskScore ≥ umbral) que enruta a <c>PendingSecurityReview</c> y sólo tras la
///      aprobación de seguridad llega a <c>Approved</c> y <c>Executed</c>.
///   3. Rechazo <em>fail-closed</em> de elegibilidad (sin <c>RoleMaturityStatus</c> sembrado) y verificación
///      de que la promoción no puede avanzar tras el corte (INV-RPR4).
///   4. Violación de segregación de funciones (ADR-UMS-096): el aprobador NO puede ejecutar su propia decisión
///      (ejecutor ≠ aprobador); tras el rechazo, un ejecutor distinto sí puede.
///   5. Consulta de <c>RoleMaturityStatus</c> por usuario, aislada por inquilino (la madurez no cruza
///      fronteras de inquilino).
///
/// Cada transición la ejecuta un actor DISTINTO mediante el encabezado <c>X-Test-Actor-Id</c> que honra el
/// <see cref="TestAuthHandler"/> del host PostgreSQL, respetando la segregación de funciones (INV-RPR3).
/// El bus es en memoria (sin outbox EF): el efecto de <c>Execute</c> se aplica de forma ASÍNCRONA, por lo
/// que se relee el perfil con reintentos deterministas (no <c>sleep</c> ciegos).
/// </summary>
[Collection("PostgreSql")]
public sealed class RolePromotionLifecycleE2ETests
{
    private readonly PostgreSqlContainerFixture _fixture;
    private readonly PostgreSqlWebApplicationFactory? _factory;

    public RolePromotionLifecycleE2ETests(PostgreSqlContainerFixture fixture)
    {
        _fixture = fixture;
        if (fixture.IsAvailable)
        {
            _factory = new PostgreSqlWebApplicationFactory(fixture.ConnectionString);
        }
    }

    /// <summary>
    /// (1) Ciclo feliz completo de BAJO riesgo: con ambos roles raíz (nivel 0) el RiskScore resultante
    /// queda &lt; umbral, de modo que <c>ManagerApprove</c> va directo a <c>Approved</c> (sin revisión de
    /// seguridad). La solicitud atraviesa toda la máquina hasta <c>Verified</c> y el perfil del objetivo
    /// queda reasignado al rol destino (efecto de ADR-UMS-096 / G-094 vía outbox + consumidor).
    /// </summary>
    [Fact]
    public async Task FullLowRiskLifecycle_ReachesVerified_AndReassignsTargetProfileRole()
    {
        if (!_fixture.IsAvailable || _factory is null)
        {
            Assert.Skip("Docker required.");
            return;
        }

        var ct = TestContext.Current.CancellationToken;
        using var warmup = _factory.CreateClient();

        var tenantId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();
        var requesterActor = Guid.NewGuid();
        var approverActor = Guid.NewGuid();
        var executorActor = Guid.NewGuid();
        var verifierActor = Guid.NewGuid();

        var (currentRoleId, targetRoleId, profileId) = await SeedScenarioAsync(
            tenantId, targetUserId, highRisk: false, seedEligibleMaturity: true, ct);

        var requester = CreateActorClient(tenantId, requesterActor);
        var approver = CreateActorClient(tenantId, approverActor);
        var executor = CreateActorClient(tenantId, executorActor);
        var verifier = CreateActorClient(tenantId, verifierActor);

        var baseUrl = await CreateAsync(requester, tenantId, targetUserId, currentRoleId, targetRoleId, ct);

        // Draft → PendingEligibilityCheck (congela el RiskScore).
        (await requester.PostAsync($"{baseUrl}/submit", null, ct)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        var afterSubmit = await GetAsync(requester, baseUrl, ct);
        afterSubmit.Status.Should().Be("PendingEligibilityCheck");
        afterSubmit.RiskScore.Should().NotBeNull();
        afterSubmit.RiskScore!.Value.Should().BeLessThan(
            RolePromotionRequestAggregate.DefaultHighRiskThreshold,
            "los roles raíz de nivel 0 producen un RiskScore de bajo riesgo (< umbral)");

        // PendingEligibilityCheck → PendingManagerApproval (elegible por el RoleMaturityStatus sembrado).
        (await requester.PostAsync($"{baseUrl}/confirm-eligibility", null, ct)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await GetAsync(requester, baseUrl, ct)).Status.Should().Be("PendingManagerApproval");

        // PendingManagerApproval → Approved DIRECTO (bajo riesgo ⇒ sin revisión de seguridad).
        (await approver.PostAsync($"{baseUrl}/manager-approve", null, ct)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        var afterManager = await GetAsync(approver, baseUrl, ct);
        afterManager.Status.Should().Be("Approved", "el bajo riesgo no debe enrutar a revisión de seguridad");
        afterManager.SecurityReviewerId.Should().BeNull();

        // Approved → Executed (ejecutor distinto del objetivo y del aprobador).
        (await executor.PostAsync($"{baseUrl}/execute", null, ct)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await GetAsync(executor, baseUrl, ct)).Status.Should().Be("Executed");

        // Executed → Verified (verificador distinto del ejecutor y del objetivo).
        (await verifier.PostAsync($"{baseUrl}/verify", null, ct)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        var verified = await GetAsync(verifier, baseUrl, ct);
        verified.Status.Should().Be("Verified");
        verified.ApproverId.Should().NotBeNull();
        verified.ExecutorId.Should().NotBeNull();
        verified.VerifierId.Should().NotBeNull();

        // Efecto: el rol del perfil objetivo quedó reasignado al rol destino (reentrega asíncrona por el bus).
        var profile = await WaitForProfileRoleAsync(profileId, targetRoleId, ct);
        profile.Should().NotBeNull();
        profile!.RoleId.GetValue().Should().Be(
            targetRoleId,
            "la promoción ejecutada debe reasignar el rol del perfil objetivo (ADR-UMS-096 / G-094)");
        profile.IsActive.Should().BeTrue();
    }

    /// <summary>
    /// (2) Ruta de ALTO riesgo: el rol destino es un rol hijo de nivel jerárquico 4, de modo que el
    /// RiskScore congelado alcanza/supera el umbral y <c>ManagerApprove</c> enruta a
    /// <c>PendingSecurityReview</c>. Sólo tras la aprobación de un revisor de seguridad distinto llega a
    /// <c>Approved</c>, y un ejecutor distinto la ejecuta.
    /// </summary>
    [Fact]
    public async Task HighRiskLifecycle_RoutesToSecurityReview_ThenApprovesAndExecutes()
    {
        if (!_fixture.IsAvailable || _factory is null)
        {
            Assert.Skip("Docker required.");
            return;
        }

        var ct = TestContext.Current.CancellationToken;
        using var warmup = _factory.CreateClient();

        var tenantId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();
        var requesterActor = Guid.NewGuid();
        var approverActor = Guid.NewGuid();
        var securityActor = Guid.NewGuid();
        var executorActor = Guid.NewGuid();

        var (currentRoleId, targetRoleId, _) = await SeedScenarioAsync(
            tenantId, targetUserId, highRisk: true, seedEligibleMaturity: true, ct);

        var requester = CreateActorClient(tenantId, requesterActor);
        var approver = CreateActorClient(tenantId, approverActor);
        var security = CreateActorClient(tenantId, securityActor);
        var executor = CreateActorClient(tenantId, executorActor);

        var baseUrl = await CreateAsync(requester, tenantId, targetUserId, currentRoleId, targetRoleId, ct);

        (await requester.PostAsync($"{baseUrl}/submit", null, ct)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        var afterSubmit = await GetAsync(requester, baseUrl, ct);
        afterSubmit.RiskScore.Should().NotBeNull();
        afterSubmit.RiskScore!.Value.Should().BeGreaterThanOrEqualTo(
            RolePromotionRequestAggregate.DefaultHighRiskThreshold,
            "ganar TODA la superficie de escritura de la suite produce alto riesgo (≥ umbral)");

        (await requester.PostAsync($"{baseUrl}/confirm-eligibility", null, ct)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await GetAsync(requester, baseUrl, ct)).Status.Should().Be("PendingManagerApproval");

        // PendingManagerApproval → PendingSecurityReview (DETERMINISTA por el alto RiskScore).
        (await approver.PostAsync($"{baseUrl}/manager-approve", null, ct)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await GetAsync(approver, baseUrl, ct)).Status.Should().Be(
            "PendingSecurityReview", "el alto riesgo debe enrutar a revisión de seguridad");

        // PendingSecurityReview → Approved (revisor distinto del objetivo y del aprobador).
        (await security.PostAsync($"{baseUrl}/security-approve", null, ct)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        var afterSecurity = await GetAsync(security, baseUrl, ct);
        afterSecurity.Status.Should().Be("Approved");
        afterSecurity.SecurityReviewerId.Should().NotBeNull("la ruta de alto riesgo registra al revisor de seguridad");

        // Approved → Executed (ejecutor distinto del objetivo, del aprobador y del revisor).
        (await executor.PostAsync($"{baseUrl}/execute", null, ct)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await GetAsync(executor, baseUrl, ct)).Status.Should().Be("Executed");
    }

    /// <summary>
    /// (3) Rechazo <em>fail-closed</em> de elegibilidad: sin un <c>RoleMaturityStatus</c> sembrado, la
    /// confirmación traduce la ausencia a «no elegible» y la solicitud pasa a <c>Rejected</c> (INV-RPR4).
    /// Se verifica además que NO puede avanzar tras el corte: un <c>ManagerApprove</c> posterior falla con
    /// transición de estado inválida.
    /// </summary>
    [Fact]
    public async Task ConfirmEligibility_WithoutSeededMaturity_RejectsFailClosed_AndHalts()
    {
        if (!_fixture.IsAvailable || _factory is null)
        {
            Assert.Skip("Docker required.");
            return;
        }

        var ct = TestContext.Current.CancellationToken;
        using var warmup = _factory.CreateClient();

        var tenantId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();
        var requesterActor = Guid.NewGuid();
        var approverActor = Guid.NewGuid();

        // Roles y perfil, pero SIN estado de madurez ⇒ la confirmación de elegibilidad rechaza (fail-closed).
        var (currentRoleId, targetRoleId, _) = await SeedScenarioAsync(
            tenantId, targetUserId, highRisk: false, seedEligibleMaturity: false, ct);

        var requester = CreateActorClient(tenantId, requesterActor);
        var approver = CreateActorClient(tenantId, approverActor);

        var baseUrl = await CreateAsync(requester, tenantId, targetUserId, currentRoleId, targetRoleId, ct);

        (await requester.PostAsync($"{baseUrl}/submit", null, ct)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await requester.PostAsync($"{baseUrl}/confirm-eligibility", null, ct)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var rejected = await GetAsync(requester, baseUrl, ct);
        rejected.Status.Should().Be("Rejected", "sin madurez sembrada la elegibilidad debe rechazar (fail-closed)");
        rejected.DecisionReason.Should().NotBeNullOrWhiteSpace();

        // La promoción rechazada no puede avanzar: aprobar un estado terminal falla (INV-RPR1).
        var approveAttempt = await approver.PostAsync($"{baseUrl}/manager-approve", null, ct);
        approveAttempt.StatusCode.Should().Be(HttpStatusCode.BadRequest, "una solicitud rechazada no puede aprobarse");
        (await GetAsync(requester, baseUrl, ct)).Status.Should().Be("Rejected", "el rechazo es terminal");
    }

    /// <summary>
    /// (4) Segregación de funciones endurecida (ADR-UMS-096, INV-RPR3): quien AUTORIZA no puede EJECUTAR su
    /// propia decisión. Se conduce la solicitud hasta <c>Approved</c> y el APROBADOR intenta ejecutarla:
    /// debe rechazarse (400) y el estado permanecer <c>Approved</c>. Un ejecutor distinto sí puede ejecutar,
    /// lo que confirma que el corte es la SoD y no un flujo roto.
    /// </summary>
    [Fact]
    public async Task Execute_WhenExecutorIsApprover_RejectsSegregationOfDuties()
    {
        if (!_fixture.IsAvailable || _factory is null)
        {
            Assert.Skip("Docker required.");
            return;
        }

        var ct = TestContext.Current.CancellationToken;
        using var warmup = _factory.CreateClient();

        var tenantId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();
        var requesterActor = Guid.NewGuid();
        var approverActor = Guid.NewGuid();
        var executorActor = Guid.NewGuid();

        var (currentRoleId, targetRoleId, _) = await SeedScenarioAsync(
            tenantId, targetUserId, highRisk: false, seedEligibleMaturity: true, ct);

        var requester = CreateActorClient(tenantId, requesterActor);
        var approver = CreateActorClient(tenantId, approverActor);
        var executor = CreateActorClient(tenantId, executorActor);

        var baseUrl = await CreateAsync(requester, tenantId, targetUserId, currentRoleId, targetRoleId, ct);

        (await requester.PostAsync($"{baseUrl}/submit", null, ct)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await requester.PostAsync($"{baseUrl}/confirm-eligibility", null, ct)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await approver.PostAsync($"{baseUrl}/manager-approve", null, ct)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await GetAsync(approver, baseUrl, ct)).Status.Should().Be("Approved");

        // El aprobador intenta ejecutar su propia decisión ⇒ violación de SoD (ejecutor ≠ aprobador).
        var sodViolation = await approver.PostAsync($"{baseUrl}/execute", null, ct);
        sodViolation.StatusCode.Should().Be(HttpStatusCode.BadRequest, "el aprobador no puede ejecutar su propia promoción (ADR-UMS-096)");
        (await GetAsync(approver, baseUrl, ct)).Status.Should().Be("Approved", "la ejecución rechazada por SoD no cambia el estado");

        // Un ejecutor distinto sí puede ejecutar: el corte era la SoD, no un flujo roto.
        (await executor.PostAsync($"{baseUrl}/execute", null, ct)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await GetAsync(executor, baseUrl, ct)).Status.Should().Be("Executed");
    }

    /// <summary>
    /// (5) Consulta de <c>RoleMaturityStatus</c> por usuario, aislada por inquilino: se siembra un estado
    /// para el MISMO usuario en dos inquilinos distintos. La consulta acotada a un inquilino devuelve
    /// exclusivamente su estado (nunca el del otro inquilino), verificando que la madurez no cruza
    /// fronteras de inquilino (FR-062, filtro global + parámetro <c>tenantId</c>).
    /// </summary>
    [Fact]
    public async Task GetRoleMaturityStatusByUser_IsScopedByTenant()
    {
        if (!_fixture.IsAvailable || _factory is null)
        {
            Assert.Skip("Docker required.");
            return;
        }

        var ct = TestContext.Current.CancellationToken;
        using var warmup = _factory.CreateClient();

        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var roleInA = Guid.NewGuid();
        var roleInB = Guid.NewGuid();

        await SeedMaturityAsync(tenantA, userId, roleInA, RoleMaturityLevel.Junior, 4.5m, ct);
        await SeedMaturityAsync(tenantB, userId, roleInB, RoleMaturityLevel.Senior, 3.2m, ct);

        // Inquilino A: sólo ve su propio estado (Junior en roleInA), nunca el de B.
        var clientA = CreateActorClient(tenantA, Guid.NewGuid());
        var listA = await clientA.GetFromJsonAsync<IReadOnlyList<RoleMaturityStatusDto>>(
            $"/api/v1/role-maturity-status/users/{userId}?tenantId={tenantA}", ct);
        listA.Should().NotBeNull();
        listA!.Should().ContainSingle();
        listA.Should().NotContain(s => s.TenantId == tenantB, "la madurez del inquilino B no puede filtrarse al inquilino A");
        var statusA = listA!.Single();
        statusA.TenantId.Should().Be(tenantA);
        statusA.RoleId.Should().Be(roleInA);
        statusA.CurrentMaturityLevel.Should().Be("Junior");

        // Inquilino B: simétricamente, sólo ve su propio estado (Senior en roleInB).
        var clientB = CreateActorClient(tenantB, Guid.NewGuid());
        var listB = await clientB.GetFromJsonAsync<IReadOnlyList<RoleMaturityStatusDto>>(
            $"/api/v1/role-maturity-status/users/{userId}?tenantId={tenantB}", ct);
        listB.Should().NotBeNull();
        listB!.Should().ContainSingle();
        listB.Should().NotContain(s => s.TenantId == tenantA, "la madurez del inquilino A no puede filtrarse al inquilino B");
        var statusB = listB!.Single();
        statusB.TenantId.Should().Be(tenantB);
        statusB.RoleId.Should().Be(roleInB);
        statusB.CurrentMaturityLevel.Should().Be("Senior");
    }

    // ── Siembra ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Siembra, sobre el contenedor real: un <c>SystemSuite</c> y dos <c>Role</c> (origen/destino) que el
    /// cálculo de RiskScore resuelve por id; opcionalmente un <c>RoleMaturityStatus</c> ELEGIBLE del objetivo
    /// en el rol origen; y un <c>Profile</c> ACTIVO del objetivo con el rol origen (sujeto del efecto).
    /// Con <paramref name="highRisk"/> el rol destino recibe una plantilla PUBLICADA que le concede TODA la
    /// superficie de escritura de la suite, que es lo que el modelo vigente mide (<c>iga-risk-graph-v2</c>):
    /// 10 de base + 60 por ganar el 100 % de las escrituras = 70, el umbral. En caso contrario el destino no
    /// tiene plantilla, no gana ninguna escritura y el riesgo queda en el mínimo ⇒ bajo riesgo.
    ///
    /// <para>Hasta el 2026-08-21 el alto riesgo se fabricaba con un rol hijo de nivel jerárquico 4, porque la
    /// heurística v1 puntuaba el salto en el organigrama. Ese proxy estaba invertido —promover a
    /// administrador puntuaba 15— y al sustituirlo esta siembra dejó de producir riesgo alguno. Se fabrica
    /// ahora con permisos reales: si el modelo vuelve a cambiar, esta prueba lo dice.</para>
    /// </summary>
    private async Task<(Guid CurrentRoleId, Guid TargetRoleId, Guid ProfileId)> SeedScenarioAsync(
        Guid tenantIdValue,
        Guid targetUserIdValue,
        bool highRisk,
        bool seedEligibleMaturity,
        CancellationToken ct)
    {
        using var scope = _factory!.Services.CreateScope();
        var actor = ActorId.Create(Guid.NewGuid().ToString());
        var tenantId = TenantId.Load(tenantIdValue);
        var targetUser = UserId.Load(targetUserIdValue);
        var suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

        var suiteRepository = scope.ServiceProvider.GetRequiredService<ISystemSuiteRepository>();
        var suite = SystemSuiteAggregate.Create(
            tenantId,
            Code.Create($"SS{suffix}"),
            Name.Create($"IGA Lifecycle Suite {suffix}"),
            Description.Create("Suite sembrada por RolePromotionLifecycleE2ETests (G-087)."),
            actor).Value;

        // El catálogo se monta ANTES de persistir la raíz: `UpdateAsync` sobre un agregado ya
        // guardado no arrastraba los módulos ni los nodos nuevos, y el cálculo veía una suite sin
        // superficie de escritura —riesgo 10, el mínimo— con la siembra en verde. Se vio porque la
        // prueba corrió; compilar decía que sí.
        if (highRisk) MontarCatalogoDeEscritura(suite, actor);

        await suiteRepository.AddAsync(suite, ct);
        await suiteRepository.UnitOfWork.SaveEntitiesAsync(ct);

        var roleRepository = scope.ServiceProvider.GetRequiredService<IRoleRepository>();

        // Rol origen: raíz (nivel 0, orden 0).
        var currentRole = RoleAggregate.Create(
            tenantId, suite.GetId(), Code.Create($"RLCUR{suffix}"), Name.Create($"Current {suffix}"),
            Description.Create("Rol origen (G-087)."), parentRoleId: null, hierarchyLevel: 0, promotionOrder: 0, actor).Value;

        // Rol destino: raíz también. El nivel jerárquico ya no fabrica riesgo —lo fabrica la plantilla—,
        // así que los dos roles son comparables y el único diferencial es el de permisos.
        var targetRole = RoleAggregate.Create(
            tenantId, suite.GetId(), Code.Create($"RLTGT{suffix}"), Name.Create($"Target {suffix}"),
            Description.Create($"Rol destino {(highRisk ? "de alto" : "de bajo")} riesgo (G-087)."),
            parentRoleId: null, hierarchyLevel: 0, promotionOrder: 1, actor).Value;

        await roleRepository.AddAsync(currentRole, ct);
        await roleRepository.AddAsync(targetRole, ct);
        await roleRepository.UnitOfWork.SaveEntitiesAsync(ct);

        if (highRisk)
        {
            await SembrarPlantillaDeEscrituraTotalAsync(scope, suite, targetRole, actor, ct);
        }

        var currentRoleId = currentRole.GetId();

        if (seedEligibleMaturity)
        {
            var maturityRepository = scope.ServiceProvider.GetRequiredService<IRoleMaturityStatusRepository>();
            var eligible = RoleMaturityStatusAggregate.Create(
                tenantId, targetUser, currentRoleId, RoleMaturityLevel.Junior, DateTime.UtcNow.AddYears(-2), actor).Value;
            eligible.UpdatePerformanceScore(4.5m, actor);
            await maturityRepository.AddAsync(eligible, ct);
            await maturityRepository.UnitOfWork.SaveEntitiesAsync(ct);
        }

        var profileRepository = scope.ServiceProvider.GetRequiredService<IProfileRepository>();
        var profile = ProfileAggregate.Create(tenantId, targetUser, currentRoleId, branchId: null, actor).Value;
        await profileRepository.AddAsync(profile, ct);
        await profileRepository.UnitOfWork.SaveEntitiesAsync(ct);

        return (currentRole.GetId().GetValue(), targetRole.GetId().GetValue(), profile.GetId().GetValue());
    }

    /// <summary>
    /// Da a la suite una superficie de escritura y concede TODA esa superficie al rol destino, en una
    /// plantilla publicada. Con el modelo vigente eso vale 60 de 60 en el factor de escrituras: el
    /// máximo que un diferencial de permisos puede pesar, y con la base, exactamente el umbral.
    /// </summary>
    /// <summary>
    /// Dos opciones que escriben: la superficie de escritura de esta suite son esos dos pares.
    /// Se monta sobre el agregado en memoria, antes de que la suite se persista por primera vez.
    /// </summary>
    private static void MontarCatalogoDeEscritura(SystemSuiteAggregate suite, ActorId actor)
    {
        suite.RegisterAction(ActionCode.Create("VIEW"), Name.Create("Ver"), actor);
        suite.RegisterAction(ActionCode.Create("UPDATE"), Name.Create("Actualizar"), actor);

        suite.AddModule(Code.Create("MOD"), Name.Create("Módulo"), Description.Create("Módulo sembrado (G-087)."), 1, actor);
        var module = suite.Modules.First();
        suite.ActivateModule(module.Props.Id, actor);

        foreach (var (codigo, orden) in new[] { ("OPT_A", 1), ("OPT_B", 2) })
        {
            suite.AddModuleRootNode(module.Props.Id, NodeKind.Option, Code.Create(codigo), Name.Create(codigo),
                Description.Create(codigo), orden, actor);
            var node = module.Nodes.First(n => n.Code.GetValue() == codigo);
            suite.LinkModuleNodeAction(module.Props.Id, node.GetId(), ActionCode.Create("VIEW"), actor);
            suite.LinkModuleNodeAction(module.Props.Id, node.GetId(), ActionCode.Create("UPDATE"), actor);
        }
    }

    private static async Task SembrarPlantillaDeEscrituraTotalAsync(
        IServiceScope scope,
        SystemSuiteAggregate suite,
        RoleAggregate targetRole,
        ActorId actor,
        CancellationToken ct)
    {
        var module = suite.Modules.First();
        var updateActionId = suite.Actions.First(a => a.Code.GetValue() == "UPDATE").GetId().GetValue();

        var templateRepository = scope.ServiceProvider.GetRequiredService<IPermissionTemplateRepository>();
        var template = TemplateAggregate.Create(
            TenantId.Load(suite.Props.TenantId.GetValue()), targetRole.GetId(), suite.GetId(), actor).Value;

        foreach (var node in module.Nodes)
        {
            template.AddItem(ExclusiveArcTarget.Option, IdValueObject.Load(node.GetId().GetValue()),
                ActionId.Load(updateActionId), isAllowed: true, isDenied: false, actor);
        }

        // Sin publicar no cuenta: el cálculo solo mira plantillas publicadas, igual que el resolutor.
        template.Publish(actor);

        await templateRepository.AddAsync(template, ct);
        await templateRepository.UnitOfWork.SaveEntitiesAsync(ct);
    }

    /// <summary>Siembra un <c>RoleMaturityStatus</c> aislado (sin roles/perfil) para la consulta por inquilino.</summary>
    private async Task SeedMaturityAsync(
        Guid tenantIdValue,
        Guid userIdValue,
        Guid roleIdValue,
        RoleMaturityLevel level,
        decimal performanceScore,
        CancellationToken ct)
    {
        using var scope = _factory!.Services.CreateScope();
        var actor = ActorId.Create(Guid.NewGuid().ToString());
        var maturityRepository = scope.ServiceProvider.GetRequiredService<IRoleMaturityStatusRepository>();

        var status = RoleMaturityStatusAggregate.Create(
            TenantId.Load(tenantIdValue),
            UserId.Load(userIdValue),
            RoleId.Load(roleIdValue),
            level,
            DateTime.UtcNow.AddYears(-1),
            actor).Value;
        status.UpdatePerformanceScore(performanceScore, actor);

        await maturityRepository.AddAsync(status, ct);
        await maturityRepository.UnitOfWork.SaveEntitiesAsync(ct);
    }

    // ── Utilidades HTTP ─────────────────────────────────────────────────────────

    private async Task<string> CreateAsync(
        HttpClient requester, Guid tenantId, Guid targetUserId, Guid currentRoleId, Guid targetRoleId, CancellationToken ct)
    {
        var createResponse = await requester.PostAsJsonAsync(
            "/api/v1/role-promotion-requests",
            new { TenantId = tenantId, TargetUserId = targetUserId, CurrentRoleId = currentRoleId, TargetRoleId = targetRoleId },
            ct);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResponse.Content.ReadFromJsonAsync<CreateRolePromotionRequestResponse>(ct);
        created.Should().NotBeNull();
        return $"/api/v1/role-promotion-requests/{created!.RolePromotionRequestId}";
    }

    private HttpClient CreateActorClient(Guid tenantId, Guid actorId)
    {
        var client = _factory!.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
        });
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());
        client.DefaultRequestHeaders.Add("X-Test-Actor-Id", actorId.ToString());
        return client;
    }

    private async Task<ProfileAggregate?> WaitForProfileRoleAsync(Guid profileId, Guid expectedRoleId, CancellationToken ct)
    {
        // La reasignación llega por el bus de forma asíncrona: se relee con reintentos deterministas
        // —scope/DbContext nuevo por intento para no leer del mapa de identidad— hasta observar el rol.
        ProfileAggregate? profile = null;
        for (var attempt = 0; attempt < 50; attempt++)
        {
            using var scope = _factory!.Services.CreateScope();
            var profileRepository = scope.ServiceProvider.GetRequiredService<IProfileRepository>();
            profile = await profileRepository.GetByIdAsync(profileId, ct);

            if (profile is not null && profile.RoleId.GetValue() == expectedRoleId)
            {
                return profile;
            }

            await Task.Delay(100, ct);
        }

        return profile;
    }

    private static async Task<RolePromotionRequestDto> GetAsync(HttpClient client, string baseUrl, CancellationToken ct)
    {
        var dto = await client.GetFromJsonAsync<RolePromotionRequestDto>(baseUrl, ct);
        dto.Should().NotBeNull();
        return dto!;
    }
}
