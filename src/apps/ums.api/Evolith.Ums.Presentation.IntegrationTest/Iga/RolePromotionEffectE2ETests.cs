using Microsoft.AspNetCore.Mvc.Testing;
using Evolith.Ums.Application.IGA.DTOs;
using Evolith.Ums.Domain.IGA;
using Evolith.Ums.Presentation.IntegrationTest.Infrastructure;
using ProfileAggregate = Evolith.Ums.Domain.Authorization.Profile.Profile;
using RoleAggregate = Evolith.Ums.Domain.Authorization.Role.Role;
using RoleMaturityStatusAggregate = Evolith.Ums.Domain.IGA.RoleMaturityStatus.RoleMaturityStatus;
using SystemSuiteAggregate = Evolith.Ums.Domain.Authorization.SystemSuite.SystemSuite;

namespace Evolith.Ums.Presentation.IntegrationTest.Iga;

/// <summary>
/// ADR-UMS-096 (Brecha 2 / G-093) endurecido por G-094: E2E del EFECTO real de la promoción de rol IGA
/// sobre un contenedor PostgreSQL real (Testcontainers). El efecto ya NO se aplica por un manejador
/// in-process best-effort, sino por el Transactional Outbox de MassTransit:
/// <c>ExecuteRolePromotionCommandHandler</c> publica
/// <see cref="Evolith.Ums.Domain.Events.RolePromotionExecutedIntegrationEvent"/> dentro de la
/// transacción que confirma <c>Execute</c>, y <c>RolePromotionRoleAssignmentConsumer</c> reasigna el
/// rol del <c>Profile</c> del usuario objetivo en su propia transacción (D-016).
///
/// En este host el bus es en memoria (sin outbox EF): el publish entrega el mensaje al consumidor en
/// un task de fondo, de modo que la reasignación es ASÍNCRONA respecto a la respuesta HTTP de
/// <c>execute</c>. Por eso la aserción del efecto relee el perfil con reintentos deterministas (no
/// sleeps arbitrarios) hasta observar el cambio.
///
/// El test conduce el happy-path completo (Create → Submit → ConfirmEligibility → ManagerApprove →
/// [SecurityApprove] → Execute) con actores DISTINTOS por transición (segregación de funciones,
/// INV-RPR3 endurecida por ADR-UMS-096) y, tras Execute, asevera que <c>Profile.RoleId</c> del usuario
/// objetivo pasó del rol origen al rol destino.
/// </summary>
[Collection("PostgreSql")]
public sealed class RolePromotionEffectE2ETests
{
    private readonly PostgreSqlContainerFixture _fixture;
    private readonly PostgreSqlWebApplicationFactory? _factory;

    private static readonly Guid SeedTenantId = Guid.NewGuid();
    private static readonly Guid SeedTargetUserId = Guid.NewGuid();

    // Actores distintos entre sí y del objetivo, para respetar la SoD a lo largo del flujo.
    private static readonly Guid RequesterActor = Guid.NewGuid();
    private static readonly Guid ApproverActor = Guid.NewGuid();
    private static readonly Guid SecurityActor = Guid.NewGuid();
    private static readonly Guid ExecutorActor = Guid.NewGuid();

    public RolePromotionEffectE2ETests(PostgreSqlContainerFixture fixture)
    {
        _fixture = fixture;
        if (fixture.IsAvailable)
        {
            _factory = new PostgreSqlWebApplicationFactory(fixture.ConnectionString);
        }
    }

    [Fact]
    public async Task FullPromotionFlow_UntilExecute_ReassignsTargetProfileRole()
    {
        if (!_fixture.IsAvailable || _factory is null)
        {
            Assert.Skip("Docker required.");
            return;
        }

        var ct = TestContext.Current.CancellationToken;

        // Fuerza la construcción del host (que resetea la BD y siembra la línea base) ANTES de sembrar
        // nuestros propios agregados, para que el TRUNCATE de arranque no borre lo que sembramos.
        using var warmup = _factory.CreateClient();

        var (currentRoleId, targetRoleId, profileId) = await SeedRolesProfileAndEligibilityAsync(ct);

        var requester = CreateActorClient(RequesterActor);
        var approver = CreateActorClient(ApproverActor);
        var security = CreateActorClient(SecurityActor);
        var executor = CreateActorClient(ExecutorActor);

        // 1. Create (Draft) — el solicitante no es el objetivo.
        var createResponse = await requester.PostAsJsonAsync(
            "/api/v1/role-promotion-requests",
            new
            {
                TenantId = SeedTenantId,
                TargetUserId = SeedTargetUserId,
                CurrentRoleId = currentRoleId,
                TargetRoleId = targetRoleId,
            },
            ct);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResponse.Content.ReadFromJsonAsync<CreateRolePromotionRequestResponse>(ct);
        created.Should().NotBeNull();
        var baseUrl = $"/api/v1/role-promotion-requests/{created!.RolePromotionRequestId}";

        // 2. Submit → PendingEligibilityCheck (congela el RiskScore; el cálculo lee los roles sembrados).
        (await requester.PostAsync($"{baseUrl}/submit", null, ct)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        // 3. ConfirmEligibility → PendingManagerApproval (elegible por el RoleMaturityStatus sembrado).
        (await requester.PostAsync($"{baseUrl}/confirm-eligibility", null, ct)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await GetAsync(requester, baseUrl, ct)).Status.Should().Be("PendingManagerApproval");

        // 4. ManagerApprove → Approved o PendingSecurityReview según el RiskScore.
        (await approver.PostAsync($"{baseUrl}/manager-approve", null, ct)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        var afterManager = await GetAsync(approver, baseUrl, ct);
        afterManager.Status.Should().BeOneOf("Approved", "PendingSecurityReview");

        // 4b. Si escaló a revisión de seguridad, un revisor distinto la aprueba.
        if (afterManager.Status == "PendingSecurityReview")
        {
            (await security.PostAsync($"{baseUrl}/security-approve", null, ct)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        (await GetAsync(security, baseUrl, ct)).Status.Should().Be("Approved");

        // 5. Execute → Executed. El efecto (G-094) se publica al outbox y lo aplica el consumidor.
        (await executor.PostAsync($"{baseUrl}/execute", null, ct)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await GetAsync(executor, baseUrl, ct)).Status.Should().Be("Executed");

        // 6. Aserción del EFECTO: el Profile del usuario objetivo quedó reasignado al rol destino.
        //    La reasignación llega por el bus (RolePromotionRoleAssignmentConsumer) de forma asíncrona,
        //    así que se relee con reintentos deterministas —creando un scope/DbContext nuevo por intento
        //    para no leer del mapa de identidad— hasta observar el rol destino. No hay sleeps ciegos:
        //    la espera termina en cuanto el efecto es visible o se agotan los intentos.
        ProfileAggregate? profile = null;
        for (var attempt = 0; attempt < 50; attempt++)
        {
            using var scope = _factory.Services.CreateScope();
            var profileRepository = scope.ServiceProvider.GetRequiredService<IProfileRepository>();
            profile = await profileRepository.GetByIdAsync(profileId, ct);

            if (profile is not null && profile.RoleId.GetValue() == targetRoleId)
            {
                break;
            }

            await Task.Delay(100, ct);
        }

        profile.Should().NotBeNull();
        profile!.RoleId.GetValue().Should().Be(targetRoleId, "la promoción ejecutada debe reasignar el rol del perfil objetivo (ADR-UMS-096 / G-094, vía outbox + consumidor)");
        profile.IsActive.Should().BeTrue();
    }

    /// <summary>
    /// Siembra, sobre el contenedor real: un <c>SystemSuite</c> y dos <c>Role</c> raíz (origen/destino)
    /// —que el cálculo de RiskScore necesita resolver por id—; un <c>RoleMaturityStatus</c> ELEGIBLE
    /// para (inquilino, objetivo, rol origen) —Junior, ingreso &gt; 2 años, desempeño 4.5, sin
    /// incidencias— para que la confirmación de elegibilidad avance; y un <c>Profile</c> ACTIVO del
    /// usuario objetivo con el rol origen, que es el que la promoción debe reasignar. Devuelve los ids
    /// de rol origen/destino y del perfil.
    /// </summary>
    private async Task<(Guid CurrentRoleId, Guid TargetRoleId, Guid ProfileId)> SeedRolesProfileAndEligibilityAsync(CancellationToken ct)
    {
        using var scope = _factory!.Services.CreateScope();
        var actor = ActorId.Create(RequesterActor.ToString());
        var tenantId = TenantId.Load(SeedTenantId);
        var targetUser = UserId.Load(SeedTargetUserId);
        var suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

        // SystemSuite (contenedor de los roles; el RoleRecord tiene FK a SystemSuiteRecord).
        var suiteRepository = scope.ServiceProvider.GetRequiredService<ISystemSuiteRepository>();
        var suite = SystemSuiteAggregate.Create(
            tenantId,
            Code.Create($"SS{suffix}"),
            Name.Create($"IGA Effect Suite {suffix}"),
            Description.Create("Suite sembrada por RolePromotionEffectE2ETests (ADR-UMS-096)."),
            actor).Value;
        await suiteRepository.AddAsync(suite, ct);
        await suiteRepository.UnitOfWork.SaveEntitiesAsync(ct);

        // Dos roles raíz: origen (orden 0) y destino (orden 1). RiskScore resultante < umbral ⇒ Approved.
        var roleRepository = scope.ServiceProvider.GetRequiredService<IRoleRepository>();
        var currentRole = RoleAggregate.Create(
            tenantId, suite.GetId(), Code.Create($"RLCUR{suffix}"), Name.Create($"Current {suffix}"),
            Description.Create("Rol origen (ADR-UMS-096)."), parentRoleId: null, hierarchyLevel: 0, promotionOrder: 0, actor).Value;
        var targetRole = RoleAggregate.Create(
            tenantId, suite.GetId(), Code.Create($"RLTGT{suffix}"), Name.Create($"Target {suffix}"),
            Description.Create("Rol destino (ADR-UMS-096)."), parentRoleId: null, hierarchyLevel: 0, promotionOrder: 1, actor).Value;
        await roleRepository.AddAsync(currentRole, ct);
        await roleRepository.AddAsync(targetRole, ct);
        await roleRepository.UnitOfWork.SaveEntitiesAsync(ct);

        var currentRoleId = currentRole.GetId();

        // RoleMaturityStatus ELEGIBLE del objetivo en el rol origen.
        var maturityRepository = scope.ServiceProvider.GetRequiredService<IRoleMaturityStatusRepository>();
        var eligible = RoleMaturityStatusAggregate.Create(
            tenantId, targetUser, currentRoleId, RoleMaturityLevel.Junior, DateTime.UtcNow.AddYears(-2), actor).Value;
        eligible.UpdatePerformanceScore(4.5m, actor);
        await maturityRepository.AddAsync(eligible, ct);
        await maturityRepository.UnitOfWork.SaveEntitiesAsync(ct);

        // Profile ACTIVO del objetivo con el rol origen: el sujeto del efecto.
        var profileRepository = scope.ServiceProvider.GetRequiredService<IProfileRepository>();
        var profile = ProfileAggregate.Create(tenantId, targetUser, currentRoleId, branchId: null, actor).Value;
        await profileRepository.AddAsync(profile, ct);
        await profileRepository.UnitOfWork.SaveEntitiesAsync(ct);

        return (currentRole.GetId().GetValue(), targetRole.GetId().GetValue(), profile.GetId().GetValue());
    }

    private HttpClient CreateActorClient(Guid actorId)
    {
        var client = _factory!.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
        });
        client.DefaultRequestHeaders.Add("X-Tenant-Id", SeedTenantId.ToString());
        client.DefaultRequestHeaders.Add("X-Test-Actor-Id", actorId.ToString());
        return client;
    }

    private static async Task<RolePromotionRequestDto> GetAsync(HttpClient client, string baseUrl, CancellationToken ct)
    {
        var dto = await client.GetFromJsonAsync<RolePromotionRequestDto>(baseUrl, ct);
        dto.Should().NotBeNull();
        return dto!;
    }
}
