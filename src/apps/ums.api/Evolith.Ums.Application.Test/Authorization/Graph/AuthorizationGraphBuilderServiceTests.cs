namespace Evolith.Ums.Application.Test.Authorization.Graph;

#pragma warning disable S125

using Evolith.Ums.Application.Authorization.Graph;
using Evolith.Ums.Application.Authorization.Template.Commands;
using Evolith.Ums.Application.Common.Interfaces;
using Evolith.Ums.Application.Configuration.Services;
using Evolith.Ums.Domain.Authorization;
using Evolith.Ums.Domain.Authorization.Graph;
using Evolith.Ums.Domain.Authorization.Profile;
using Evolith.Ums.Domain.Authorization.SystemSuite.DomainResource;
using Evolith.Ums.Domain.Authorization.Template;
using Evolith.Ums.Domain.Configuration;
using Evolith.Ums.Domain.Configuration.FeatureFlag;
using Evolith.Ums.Domain.Identity;
using Evolith.Ums.Domain.Identity.Auth;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using FeatureFlagAggregate = Evolith.Ums.Domain.Configuration.FeatureFlag.FeatureFlag;
using ProfileAggregate     = Evolith.Ums.Domain.Authorization.Profile.Profile;
using RoleAggregate        = Evolith.Ums.Domain.Authorization.Role.Role;
using SystemSuiteAggregate = Evolith.Ums.Domain.Authorization.SystemSuite.SystemSuite;
using UserAccountAggregate = Evolith.Ums.Domain.Identity.UserAccount.UserAccount;

/// <summary>
/// Unit tests for AuthorizationGraphBuilderService.
/// All dependencies are mocked — no DB access.
/// </summary>
public class AuthorizationGraphBuilderServiceTests
{
    private readonly Mock<IProfileRepository>            _profileRepo    = new();
    private readonly Mock<IRoleRepository>               _roleRepo       = new();
    private readonly Mock<ISystemSuiteRepository>        _suiteRepo      = new();
    private readonly Mock<ITenantRepository>             _tenantRepo     = new();
    private readonly Mock<IFeatureFlagRepository>        _flagRepo       = new();
    private readonly Mock<IFeatureFlagEvaluator>         _flagEvaluator  = new();
    private readonly Mock<IConfigurationProvider>        _configProvider = new();

    private AuthorizationGraphBuilderService CreateSut() => new(
        _profileRepo.Object, _roleRepo.Object, _suiteRepo.Object,
        _tenantRepo.Object, _flagRepo.Object,
        _flagEvaluator.Object, _configProvider.Object,
        NullLogger<AuthorizationGraphBuilderService>.Instance);

    private static readonly Guid TenantGuid = Guid.NewGuid();
    private static readonly Guid UserGuid   = Guid.NewGuid();

    public AuthorizationGraphBuilderServiceTests()
    {
        _configProvider.Setup(c => c.GetValueAs<int>(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<int>()))
                       .Returns<string, Guid?, int>((_, __, def) => def);
        _configProvider.Setup(c => c.GetValueAs<bool>(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<bool>()))
                       .Returns<string, Guid?, bool>((_, __, def) => def);
        _configProvider.Setup(c => c.GetValue(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<string?>()))
                       .Returns((string _, Guid? __, string? defaultValue) => defaultValue ?? string.Empty);

        // El constructor carga SIEMPRE los perfiles del usuario para el bloque `profiles`, también
        // en la vía que recibe un perfil explícito. Sin este stub, el mock devuelve null.
        _profileRepo.Setup(r => r.GetActiveByUserAndTenantAsync(
                         It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync([]);

        // Bloque `profiles` (G-177): sin estos dos stubs el constructor recibe null de los mocks.
        // Devuelven vacío a propósito — estas pruebas verifican la resolución de permisos, no el
        // selector de perfiles, que tiene las suyas.
        _roleRepo.Setup(r => r.GetByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync([]);
        _suiteRepo.Setup(r => r.GetSummariesByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync([]);

        _flagRepo.Setup(r => r.GetBySystemSuiteIdForEvaluationAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new List<FeatureFlagAggregate>());

    }

    // ── Error paths ────────────────────────────────────────────────────────────

    [Fact]
    public async Task BuildAsync_TenantNotFound_ReturnsFailure()
    {
        _tenantRepo.Setup(r => r.GetByIdAsync(TenantGuid, It.IsAny<CancellationToken>()))
                   .ReturnsAsync((Evolith.Ums.Domain.Identity.Tenant.Tenant?)null);

        var result = await CreateSut().BuildAsync(MakeUser(), TenantGuid, AuthMethod.Local());

        Assert.True(result.IsFailure);
        Assert.Contains("Tenant", result.Error);
    }

    [Fact]
    public async Task BuildAsync_NoActiveProfileForUser_ReturnsLobbyGraph()
    {
        // G-043: un usuario autenticado y aprobado pero SIN perfil activo ya no falla el login;
        // recibe un GRAFO LOBBY (OnboardingPending=true, sin suite/rol/perfil, sin menús) para que
        // el cliente muestre el onboarding en vez de un error opaco.
        SetupValidTenant();
        _profileRepo.Setup(r => r.GetActiveByUserAndTenantAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new List<ProfileAggregate>());

        var result = await CreateSut().BuildAsync(MakeUser(), TenantGuid, AuthMethod.Local());

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.OnboardingPending);
        Assert.Null(result.Value.Context.SystemSuite);
        Assert.Null(result.Value.Context.Role);
        Assert.Null(result.Value.Context.Profile);
        Assert.Empty(result.Value.MenuAccess);
        // El contexto de usuario e inquilino sí es real.
        Assert.NotNull(result.Value.Context.User);
        Assert.Equal(TenantGuid, result.Value.Context.Tenant.Id);
    }

    [Fact]
    public async Task BuildAsync_RoleNotFound_ReturnsFailure()
    {
        SetupValidTenant();
        _profileRepo.Setup(r => r.GetActiveByUserAndTenantAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new List<ProfileAggregate> { MakeProfile() });
        _roleRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync((RoleAggregate?)null);

        var result = await CreateSut().BuildAsync(MakeUser(), TenantGuid, AuthMethod.Local());

        Assert.True(result.IsFailure);
        Assert.Contains("Role", result.Error);
    }

    // ── Success paths ─────────────────────────────────────────────────────────

    [Fact]
    public async Task BuildAsync_UserWithNoPermissions_ReturnsGraphWithEmptyScopes()
    {
        SetupFullChain();

        var result = await CreateSut().BuildAsync(MakeUser(), TenantGuid, AuthMethod.Local());

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value.Scopes);
    }

    [Fact]
    public async Task BuildAsync_ReturnsGraphWithCorrectTenantContext()
    {
        SetupFullChain();

        var result = await CreateSut().BuildAsync(MakeUser(), TenantGuid, AuthMethod.Local());

        Assert.True(result.IsSuccess);
        Assert.Equal(TenantGuid, result.Value.Context.Tenant.Id);
        Assert.Equal("Local", result.Value.Authentication.Method);
        Assert.Null(result.Value.Authentication.Provider);
    }

    [Fact]
    public async Task BuildAsync_ValidUntil_IsAfterGeneratedAt()
    {
        SetupFullChain();

        var result = await CreateSut().BuildAsync(MakeUser(), TenantGuid, AuthMethod.Local());

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.ValidUntil > result.Value.GeneratedAt);
    }

    [Fact]
    public async Task BuildAsync_FeatureFlags_EvaluatedWithUserContext()
    {
        SetupFullChain();
        var suiteId = Guid.NewGuid();

        var flag = FeatureFlagAggregate.Create(
            IdValueObject.Load(suiteId), null, "TEST_FLAG",
            FlagType.Boolean, "*", null, null, null,
            ActorId.Create("test")).Value;
        flag.Activate(ActorId.Create("test"));
        flag.DomainEvents.MarkChangesAsCommitted();

        _flagRepo.Setup(r => r.GetBySystemSuiteIdForEvaluationAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new List<FeatureFlagAggregate> { flag });
        _flagEvaluator.Setup(e => e.Evaluate(It.IsAny<FeatureFlagAggregate>(), It.IsAny<EvaluationContext>()))
                      .Returns(new FlagEvaluationResult(true, null, "no criteria"));

        var result = await CreateSut().BuildAsync(MakeUser(), TenantGuid, AuthMethod.Local());

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value.FeatureFlags);
        Assert.True(result.Value.FeatureFlags[0].IsEnabled);
        Assert.Equal("TEST_FLAG", result.Value.FeatureFlags[0].FlagCode);
    }

    [Fact]
    public async Task BuildAsync_EffectiveConfig_ReadsFromConfigProvider()
    {
        SetupFullChain();
        _configProvider.Setup(c => c.GetValueAs<int>("SESSION_TIMEOUT_MINUTES", TenantGuid, 30))
                       .Returns(45);

        var result = await CreateSut().BuildAsync(MakeUser(), TenantGuid, AuthMethod.Local());

        Assert.True(result.IsSuccess);
        Assert.Equal(45, result.Value.EffectiveConfig.SessionTimeoutMinutes);
    }

    [Fact]
    public async Task BuildAsync_Actions_IncludeAllSuiteActions()
    {
        SetupFullChain();

        var result = await CreateSut().BuildAsync(MakeUser(), TenantGuid, AuthMethod.Local());

        Assert.True(result.IsSuccess);
        Assert.NotEmpty(result.Value.Actions);
        Assert.Contains(result.Value.Actions, a => a.Code == "VIEW");
    }

    [Fact]
    public async Task BuildForProfileAsync_WhenProfileIsInactive_ReturnsFailure()
    {
        SetupValidTenant();
        var profile = MakeProfile();
        profile.Deactivate(ActorId.Create("test"));
        _profileRepo.Setup(r => r.GetByIdAsync(profile.GetId().GetValue(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);

        var result = await CreateSut().BuildForProfileAsync(
            MakeUser(),
            TenantGuid,
            profile.GetId().GetValue(),
            AuthMethod.Local());

        Assert.True(result.IsFailure);
        Assert.Contains("inactive", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BuildForProfileAsync_UsesRequestedProfile()
    {
        SetupValidTenant();

        var suiteId = Guid.NewGuid();
        var requestedRoleId = Guid.NewGuid();
        var requestedProfile = ProfileAggregate.Create(
            TenantId.Load(TenantGuid), UserId.Load(UserGuid),
            RoleId.Load(requestedRoleId), null, ActorId.Create("test")).Value;
        requestedProfile.DomainEvents.MarkChangesAsCommitted();

        _profileRepo.Setup(r => r.GetByIdAsync(requestedProfile.GetId().GetValue(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(requestedProfile);

        var role = RoleAggregate.Create(
            TenantId.Load(TenantGuid),
            SystemSuiteId.Load(suiteId),
            Code.Create("ADMIN"),
            Name.Create("Administrator"),
            Description.Create(""),
            null, 0, 1,
            ActorId.Create("test")).Value;
        role.DomainEvents.MarkChangesAsCommitted();
        _roleRepo.Setup(r => r.GetByIdAsync(requestedRoleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(role);

        var suite = BuildMinimalSuite();
        _suiteRepo.Setup(r => r.GetByIdAsync(suiteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(suite);

        var result = await CreateSut().BuildForProfileAsync(
            MakeUser(),
            TenantGuid,
            requestedProfile.GetId().GetValue(),
            AuthMethod.Local());

        Assert.True(result.IsSuccess);
        Assert.Equal(requestedProfile.GetId().GetValue(), result.Value.Context.Profile.Id);
    }

    [Fact]
    public async Task BuildAsync_OrgWide_NoBranchInContext()
    {
        SetupFullChain();

        var result = await CreateSut().BuildAsync(MakeUser(), TenantGuid, AuthMethod.Local());

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.Context.Branch);
        Assert.Equal("OrgWide", result.Value.Context.Profile.Scope);
    }

    // ── Adversariales: deny-wins multi-fuente / fail-closed / scaffolding G-016 / ciclo (G-085) ──
    //
    // El mapa efectivo (AuthorizationGraphBuilderService.BuildPermissionMap) compone varias fuentes
    // — perfil + plantilla + override — para el mismo (TargetId, ActionId). Estas pruebas ejercen la
    // resolución REAL: deny-wins gana siempre e independiente del orden; Override gana sobre Template
    // sólo para Allow; la ausencia de entrada resuelve NotGranted (fail-closed, ADR-UMS-088/G-039); una
    // plantilla Published que nunca se asignó NO contribuye al mapa (scaffolding reservado, G-016); y
    // el constructor proyecta el ParentRoleId inmediato sin recorrer la ascendencia (ciclo-seguro).
    //
    // El efecto resuelto se observa en DomainPermissions (BuildDomainPermissions): se registra un
    // DomainResource "USERS" (Aggregate) y una Action "VIEW", y los permisos de perfil se materializan
    // vía AssignTemplate contra ese (TargetId=USERS.Id, ActionId=VIEW.Id).

    [Fact]
    public async Task BuildForProfileAsync_DenyAndAllowFromTwoTemplates_ResolvesDeny()
    {
        // Dos plantillas Published para el mismo (target, action): una Allow y otra Deny.
        // Ambas materializan ProfilePermission distintos ⇒ dos fuentes en conflicto ⇒ gana Deny.
        var scenario = SetupProfileScenario();
        var allow = MakePublishedTemplate(scenario.ResourceId, scenario.ActionId, isAllowed: true,  isDenied: false);
        var deny  = MakePublishedTemplate(scenario.ResourceId, scenario.ActionId, isAllowed: false, isDenied: true);

        Assert.True(scenario.Profile.AssignTemplate(allow, ActorId.Create("test")).IsSuccess);
        Assert.True(scenario.Profile.AssignTemplate(deny,  ActorId.Create("test")).IsSuccess);

        var result = await CreateSut().BuildForProfileAsync(
            MakeUser(), TenantGuid, scenario.Profile.GetId().GetValue(), AuthMethod.Local());

        Assert.True(result.IsSuccess);
        var action = ResolveDomainAction(result.Value, "USERS", "VIEW");
        Assert.Equal(AccessEffect.Deny, action.Effect);
        Assert.Equal(PermissionSource.Template, action.Source);
    }

    [Fact]
    public async Task BuildForProfileAsync_DenyWins_IsOrderIndependent()
    {
        // Mismo conflicto que arriba pero asignando primero la Deny y luego la Allow:
        // deny-wins no depende del orden de iteración de profile.Permissions.
        var scenario = SetupProfileScenario();
        var deny  = MakePublishedTemplate(scenario.ResourceId, scenario.ActionId, isAllowed: false, isDenied: true);
        var allow = MakePublishedTemplate(scenario.ResourceId, scenario.ActionId, isAllowed: true,  isDenied: false);

        Assert.True(scenario.Profile.AssignTemplate(deny,  ActorId.Create("test")).IsSuccess);
        Assert.True(scenario.Profile.AssignTemplate(allow, ActorId.Create("test")).IsSuccess);

        var result = await CreateSut().BuildForProfileAsync(
            MakeUser(), TenantGuid, scenario.Profile.GetId().GetValue(), AuthMethod.Local());

        Assert.True(result.IsSuccess);
        Assert.Equal(AccessEffect.Deny, ResolveDomainAction(result.Value, "USERS", "VIEW").Effect);
    }

    [Fact]
    public async Task BuildForProfileAsync_OverrideAllow_WinsOverTemplateAllow_SourceIsOverride()
    {
        // Dos fuentes Allow para el mismo (target, action): una Template y una Override.
        // Override gana sobre Template para Allow ⇒ el efecto sigue Allow pero la fuente es Override.
        var scenario = SetupProfileScenario();
        var t1 = MakePublishedTemplate(scenario.ResourceId, scenario.ActionId, isAllowed: true, isDenied: false);
        var t2 = MakePublishedTemplate(scenario.ResourceId, scenario.ActionId, isAllowed: true, isDenied: false);

        Assert.True(scenario.Profile.AssignTemplate(t1, ActorId.Create("test")).IsSuccess);
        Assert.True(scenario.Profile.AssignTemplate(t2, ActorId.Create("test")).IsSuccess);

        // Convierte la última entrada materializada (fuente Template) en un Override Allow. FindPermission
        // resuelve por la identidad de entidad viva (ProfilePermission.Id), no por Props.Id: en un agregado
        // en memoria ambas difieren (Props.Id sólo se alinea tras rehidratar desde BD, ver SystemSuite.FindModule).
        var overridden = scenario.Profile.Permissions.Last();
        Assert.True(scenario.Profile.OverridePermissionAllow(overridden.GetId(), ActorId.Create("test")).IsSuccess);

        var result = await CreateSut().BuildForProfileAsync(
            MakeUser(), TenantGuid, scenario.Profile.GetId().GetValue(), AuthMethod.Local());

        Assert.True(result.IsSuccess);
        var action = ResolveDomainAction(result.Value, "USERS", "VIEW");
        Assert.Equal(AccessEffect.Allow, action.Effect);
        Assert.Equal(PermissionSource.Override, action.Source);
    }

    [Fact]
    public async Task BuildForProfileAsync_Deny_WinsOverTemplateAllowAndOverrideAllow()
    {
        // Tres fuentes para el mismo (target, action): Template Allow + Override Allow + Template Deny.
        // deny-wins se evalúa ANTES que "Override gana sobre Template", así que Deny gana sobre ambos.
        var scenario = SetupProfileScenario();
        var templateAllow = MakePublishedTemplate(scenario.ResourceId, scenario.ActionId, isAllowed: true,  isDenied: false);
        var willOverride  = MakePublishedTemplate(scenario.ResourceId, scenario.ActionId, isAllowed: true,  isDenied: false);
        var templateDeny  = MakePublishedTemplate(scenario.ResourceId, scenario.ActionId, isAllowed: false, isDenied: true);

        Assert.True(scenario.Profile.AssignTemplate(templateAllow, ActorId.Create("test")).IsSuccess);
        Assert.True(scenario.Profile.AssignTemplate(willOverride,  ActorId.Create("test")).IsSuccess);
        var overridden = scenario.Profile.Permissions.Last();
        Assert.True(scenario.Profile.OverridePermissionAllow(overridden.GetId(), ActorId.Create("test")).IsSuccess);
        Assert.True(scenario.Profile.AssignTemplate(templateDeny, ActorId.Create("test")).IsSuccess);

        var result = await CreateSut().BuildForProfileAsync(
            MakeUser(), TenantGuid, scenario.Profile.GetId().GetValue(), AuthMethod.Local());

        Assert.True(result.IsSuccess);
        Assert.Equal(AccessEffect.Deny, ResolveDomainAction(result.Value, "USERS", "VIEW").Effect);
    }

    [Fact]
    public async Task BuildForProfileAsync_NoPermissionEntry_ResolvesNotGranted_FailClosed()
    {
        // Fail-closed (G-039, ADR-UMS-088): sin ProfilePermission para (target, action), el efecto es
        // NotGranted, NO Allow. Nota adversarial: AccessEffect.Allow es el valor 0 del enum, así que
        // un default de diccionario dejaría Allow (fail-open). Esta prueba fija la denegación implícita.
        var scenario = SetupProfileScenario();   // sin AssignTemplate: perfil sin permisos materializados

        var result = await CreateSut().BuildForProfileAsync(
            MakeUser(), TenantGuid, scenario.Profile.GetId().GetValue(), AuthMethod.Local());

        Assert.True(result.IsSuccess);
        var action = ResolveDomainAction(result.Value, "USERS", "VIEW");
        Assert.Equal(AccessEffect.NotGranted, action.Effect);
        Assert.Empty(result.Value.Scopes);
    }

    [Fact]
    public async Task BuildForProfileAsync_PublishedTemplateNeverAssigned_DoesNotContributeToEffectiveMap()
    {
        // El mapa efectivo deriva SÓLO de los ProfilePermission materializados: un perfil sin
        // AssignTemplate sale con permisos vacíos aunque exista una plantilla Published que le
        // aplicaría. Desde G-174 el constructor ni siquiera consulta las plantillas —antes las
        // cargaba todas y descartaba el resultado—, así que esta prueba fija que la plantilla
        // no contribuye por ninguna vía, ni directa ni accidental.
        var scenario = SetupProfileScenario();

        // Plantilla Published para el MISMO rol+inquilino: el candidato más plausible a colarse.
        MakePublishedTemplate(
            scenario.ResourceId, scenario.ActionId, isAllowed: true, isDenied: false,
            roleId: scenario.Role.GetId().GetValue());

        var result = await CreateSut().BuildForProfileAsync(
            MakeUser(), TenantGuid, scenario.Profile.GetId().GetValue(), AuthMethod.Local());

        Assert.True(result.IsSuccess);
        // …y aun así el permiso efectivo es NotGranted y no hay scopes: la plantilla no contribuye.
        Assert.Equal(AccessEffect.NotGranted, ResolveDomainAction(result.Value, "USERS", "VIEW").Effect);
        Assert.Empty(result.Value.Scopes);
    }

    [Fact]
    public async Task BuildForProfileAsync_ItemRetiradoDeLaPlantilla_NoConcede()
    {
        // ADR-0164 — la comprobación que decide si el borrado lógico es seguro o es una brecha.
        //
        // Al dejar de borrar físicamente el ítem, la fila permanece en la plantilla. Esta prueba
        // recorre la cadena entera —ítem retirado → AssignTemplate → permMap → grafo resuelto— y
        // fija que la retirada NO concede. Con `Profile.AssignTemplate` leyendo `Items` en vez de
        // `ActiveItems`, aquí saldría Allow: sería el permiso vivo de una concesión que el operador
        // ve apagada en la plantilla.
        var scenario = SetupProfileScenario();
        var actor    = ActorId.Create("test");

        var template = PermissionTemplate.Create(
            TenantId.Load(TenantGuid), RoleId.Load(Guid.NewGuid()), SystemSuiteId.Load(Guid.NewGuid()), actor).Value;

        // Dos concesiones: la que se retira sobre (USERS, VIEW) —la observable— y otra cualquiera que
        // sostiene la publicación, porque una plantilla sin ítems vigentes ya no se puede publicar.
        Assert.True(template.AddItem(
            ExclusiveArcTarget.Aggregate, IdValueObject.Load(scenario.ResourceId), ActionId.Load(scenario.ActionId),
            isAllowed: true, isDenied: false, actor).IsSuccess);
        Assert.True(template.AddItem(
            ExclusiveArcTarget.Aggregate, IdValueObject.Create(), ActionId.Load(Guid.NewGuid()),
            isAllowed: true, isDenied: false, actor).IsSuccess);

        var retirado = template.Items.Single(i => i.TargetId.GetValue() == scenario.ResourceId);
        Assert.True(template.DeactivateItem(retirado.GetId(), actor).IsSuccess);
        Assert.True(template.Publish(actor).IsSuccess);
        template.DomainEvents.MarkChangesAsCommitted();

        Assert.True(scenario.Profile.AssignTemplate(template, actor).IsSuccess);

        var result = await CreateSut().BuildForProfileAsync(
            MakeUser(), TenantGuid, scenario.Profile.GetId().GetValue(), AuthMethod.Local());

        Assert.True(result.IsSuccess);
        Assert.Equal(AccessEffect.NotGranted, ResolveDomainAction(result.Value, "USERS", "VIEW").Effect);
        Assert.DoesNotContain("users.view", result.Value.Scopes);

        // Y la fila sigue en la plantilla: retirar no es borrar.
        Assert.Equal(2, template.Items.Count);
    }

    [Fact]
    public async Task BuildForProfileAsync_ItemReactivadoTrasRetirarlo_VuelveAConceder()
    {
        // Contraprueba obligatoria de la anterior: si el ítem retirado no concediera por cualquier
        // otra razón —una plantilla que nunca se asignó, un recurso que no resuelve— la prueba de
        // arriba se satisfaría por accidente. Reactivar tiene que devolver el permiso.
        var scenario = SetupProfileScenario();
        var actor    = ActorId.Create("test");

        var template = PermissionTemplate.Create(
            TenantId.Load(TenantGuid), RoleId.Load(Guid.NewGuid()), SystemSuiteId.Load(Guid.NewGuid()), actor).Value;

        Assert.True(template.AddItem(
            ExclusiveArcTarget.Aggregate, IdValueObject.Load(scenario.ResourceId), ActionId.Load(scenario.ActionId),
            isAllowed: true, isDenied: false, actor).IsSuccess);

        var itemId = template.Items.Single().GetId();
        Assert.True(template.DeactivateItem(itemId, actor).IsSuccess);
        Assert.True(template.ActivateItem(itemId, actor).IsSuccess);
        Assert.True(template.Publish(actor).IsSuccess);
        template.DomainEvents.MarkChangesAsCommitted();

        Assert.True(scenario.Profile.AssignTemplate(template, actor).IsSuccess);

        var result = await CreateSut().BuildForProfileAsync(
            MakeUser(), TenantGuid, scenario.Profile.GetId().GetValue(), AuthMethod.Local());

        Assert.True(result.IsSuccess);
        Assert.Equal(AccessEffect.Allow, ResolveDomainAction(result.Value, "USERS", "VIEW").Effect);
    }

    /// <summary>
    /// G-192 — recorrido completo de una concesión sobre un OBJETO DE DOMINIO por la vía de la API:
    /// el validador la admite, el manejador la da de alta en la plantilla, <c>AssignTemplate</c> la
    /// materializa en el perfil y el constructor la proyecta en <c>domainPermissions</c>.
    ///
    /// Es la prueba que faltaba: el tramo del grafo ya estaba cubierto, pero nadie fijaba que el
    /// alta por comando llegase hasta él. Mientras el validador copió a mano los cuatro destinos de
    /// navegación, este camino devolvía 400 y `domainPermissions` salía vacío en todo perfil
    /// provisionado por API, aunque la suite hubiese declarado su catálogo de objetos de dominio.
    /// </summary>
    [Theory]
    [InlineData("Aggregate")]
    [InlineData("Entity")]
    public async Task BuildForProfileAsync_ConcesionSobreObjetoDeDominioAltaPorComando_LlegaAlGrafo(string targetType)
    {
        var scenario = SetupProfileScenario();
        var actor    = ActorId.Create("test");

        var template = PermissionTemplate.Create(
            TenantId.Load(TenantGuid), RoleId.Load(scenario.Role.GetId().GetValue()),
            SystemSuiteId.Load(Guid.NewGuid()), actor).Value;

        var comando = new AddTemplateItemCommand(
            TemplateId: template.GetId().GetValue(),
            TargetType: targetType,
            TargetId:   scenario.ResourceId,
            ActionId:   scenario.ActionId,
            IsAllowed:  true,
            IsDenied:   false);

        // 1) La puerta de la API: el validador de FluentValidation es quien devolvía el 400.
        Assert.True(new AddTemplateItemCommandValidator().Validate(comando).IsValid);

        // 2) El manejador: resuelve el destino contra el enumerado del dominio y lo añade.
        var templateRepo = new Mock<IPermissionTemplateRepository>();
        var uow          = new Mock<IUnitOfWork>();
        var userCtx      = new Mock<IUserContext>();
        templateRepo.Setup(r => r.GetByIdAsync(template.GetId().GetValue(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(template);
        templateRepo.Setup(r => r.UnitOfWork).Returns(uow.Object);
        uow.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        userCtx.Setup(u => u.UserId).Returns("test");

        var alta = await new AddTemplateItemCommandHandler(templateRepo.Object, userCtx.Object)
            .Handle(comando, CancellationToken.None);

        Assert.True(alta.IsSuccess, alta.IsFailure ? alta.Error : string.Empty);
        Assert.Equal(targetType, template.Items.Single().TargetType.Name);

        // 3) Publicación y materialización en el perfil.
        Assert.True(template.Publish(actor).IsSuccess);
        template.DomainEvents.MarkChangesAsCommitted();
        Assert.True(scenario.Profile.AssignTemplate(template, actor).IsSuccess);

        // 4) Proyección: el objeto de dominio viaja en el grafo con su acción concedida.
        var result = await CreateSut().BuildForProfileAsync(
            MakeUser(), TenantGuid, scenario.Profile.GetId().GetValue(), AuthMethod.Local());

        Assert.True(result.IsSuccess);
        var recurso = Assert.Single(result.Value.DomainPermissions, p => p.ResourceCode == "USERS");
        Assert.Equal(AccessEffect.Allow, ResolveDomainAction(result.Value, "USERS", "VIEW").Effect);
        Assert.Equal(scenario.ResourceId, recurso.ResourceId);
    }

    [Fact]
    public async Task BuildForProfileAsync_CyclicRoleHierarchy_ProjectsImmediateParentWithoutTraversing()
    {
        // Ciclo en el grafo efectivo, más allá del ciclo de roles ya cubierto en
        // RoleCommandHandlerTests.Update_WhenParentIsDescendant_RejectsCycle (que lo RECHAZA al persistir).
        // Aquí el dato ya contiene un ciclo R1 → R2 → R1; el constructor del grafo lo RESUELVE proyectando
        // sólo el ParentRoleId inmediato como escalar (AuthorizationGraphBuilderService, nodo Context.Role)
        // y NUNCA recorre la ascendencia — por eso una jerarquía cíclica no puede colgar la construcción.
        SetupValidTenant();

        var suiteId = Guid.NewGuid();
        var r1Key   = Guid.NewGuid();
        var r2Key   = Guid.NewGuid();

        var suite = BuildMinimalSuite();
        _suiteRepo.Setup(r => r.GetByIdAsync(suiteId, It.IsAny<CancellationToken>())).ReturnsAsync(suite);

        var r1 = RoleAggregate.Create(
            TenantId.Load(TenantGuid), SystemSuiteId.Load(suiteId),
            Code.Create("R1"), Name.Create("Role 1"), Description.Create(""),
            RoleId.Load(r2Key), 1, 1, ActorId.Create("test")).Value;
        r1.DomainEvents.MarkChangesAsCommitted();

        var r2 = RoleAggregate.Create(
            TenantId.Load(TenantGuid), SystemSuiteId.Load(suiteId),
            Code.Create("R2"), Name.Create("Role 2"), Description.Create(""),
            RoleId.Load(r1Key), 1, 1, ActorId.Create("test")).Value;
        r2.DomainEvents.MarkChangesAsCommitted();

        _roleRepo.Setup(r => r.GetByIdAsync(r1Key, It.IsAny<CancellationToken>())).ReturnsAsync(r1);
        _roleRepo.Setup(r => r.GetByIdAsync(r2Key, It.IsAny<CancellationToken>())).ReturnsAsync(r2);

        var profile = ProfileAggregate.Create(
            TenantId.Load(TenantGuid), UserId.Load(UserGuid),
            RoleId.Load(r1Key), null, ActorId.Create("test")).Value;
        profile.DomainEvents.MarkChangesAsCommitted();
        _profileRepo.Setup(r => r.GetByIdAsync(profile.GetId().GetValue(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(profile);

        var result = await CreateSut().BuildForProfileAsync(
            MakeUser(), TenantGuid, profile.GetId().GetValue(), AuthMethod.Local());

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value.Context.Role);
        Assert.Equal(r2Key, result.Value.Context.Role!.ParentRoleId);   // padre inmediato proyectado como escalar
        // El constructor no resolvió el padre ⇒ no existe recorrido de ascendencia que pueda ciclar.
        _roleRepo.Verify(r => r.GetByIdAsync(r2Key, It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static UserAccountAggregate MakeUser()
    {
        var actor = ActorId.Create("test");
        var user  = UserAccountAggregate.Create(
            TenantId.Load(TenantGuid),
            Email.Create("user@test.com"),
            Evolith.Ums.Domain.Enums.UserCategory.Internal,
            null, null,
            actor,
            null,
            UserAccountId.Load(UserGuid)).Value;
        user.DomainEvents.MarkChangesAsCommitted();
        return user;
    }

    private void SetupValidTenant()
    {
        var actor  = ActorId.Create("test");
        var tenant = Evolith.Ums.Domain.Identity.Tenant.Tenant.Create(
            Code.Create("TEST"),
            Name.Create("Test Tenant"),
            Evolith.Ums.Domain.Enums.OrganizationType.INTERNAL,
            actor,
            Evolith.Ums.Domain.Enums.IdpStrategy.AzureAd,
            tenantId: TenantId.Load(TenantGuid)).Value;
        tenant.DomainEvents.MarkChangesAsCommitted();
        _tenantRepo.Setup(r => r.GetByIdAsync(TenantGuid, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(tenant);
    }

    private static ProfileAggregate MakeProfile()
    {
        var profile = ProfileAggregate.Create(
            TenantId.Load(TenantGuid),
            UserId.Load(UserGuid),
            RoleId.Load(Guid.NewGuid()),
            null,
            ActorId.Create("test")).Value;
        profile.DomainEvents.MarkChangesAsCommitted();
        return profile;
    }

    private void SetupFullChain()
    {
        SetupValidTenant();

        var suiteId = Guid.NewGuid();
        var roleId  = Guid.NewGuid();

        var profile = ProfileAggregate.Create(
            TenantId.Load(TenantGuid), UserId.Load(UserGuid),
            RoleId.Load(roleId), null, ActorId.Create("test")).Value;
        profile.DomainEvents.MarkChangesAsCommitted();
        _profileRepo.Setup(r => r.GetActiveByUserAndTenantAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new List<ProfileAggregate> { profile });

        var role = RoleAggregate.Create(
            TenantId.Load(TenantGuid),
            SystemSuiteId.Load(suiteId),
            Code.Create("ADMIN"),
            Name.Create("Administrator"),
            Description.Create(""),
            null, 0, 1,
            ActorId.Create("test")).Value;
        role.DomainEvents.MarkChangesAsCommitted();
        _roleRepo.Setup(r => r.GetByIdAsync(roleId, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(role);

        var suite = BuildMinimalSuite();
        _suiteRepo.Setup(r => r.GetByIdAsync(suiteId, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(suite);
    }

    private static SystemSuiteAggregate BuildMinimalSuite()
    {
        var actor = ActorId.Create("test");
        var suite = SystemSuiteAggregate.Create(
            TenantId.Load(Guid.NewGuid()),
            Code.Create("CORE"),
            Name.Create("Core System"),
            Description.Create(""),
            actor).Value;

        // Register a VIEW action using ActionCode VO
        suite.RegisterAction(ActionCode.Create("VIEW"), Name.Create("View Records"), actor);
        suite.DomainEvents.MarkChangesAsCommitted();
        return suite;
    }

    // ── Helpers adversariales (G-085) ────────────────────────────────────────────

    /// <summary>
    /// Prepara la cadena Perfil → Rol → Suite para BuildForProfileAsync con una suite que
    /// contiene un DomainResource "USERS" (Aggregate) y una Action "VIEW", de modo que el
    /// efecto resuelto para (USERS.Id, VIEW.Id) sea observable en DomainPermissions.
    /// </summary>
    private (ProfileAggregate Profile, RoleAggregate Role, Guid ResourceId, Guid ActionId) SetupProfileScenario()
    {
        SetupValidTenant();

        var suiteId = Guid.NewGuid();
        var roleId  = Guid.NewGuid();

        var suite = BuildSuiteWithDomainResource(out var resourceId, out var actionId);
        _suiteRepo.Setup(r => r.GetByIdAsync(suiteId, It.IsAny<CancellationToken>())).ReturnsAsync(suite);

        var role = RoleAggregate.Create(
            TenantId.Load(TenantGuid), SystemSuiteId.Load(suiteId),
            Code.Create("ADMIN"), Name.Create("Administrator"), Description.Create(""),
            null, 0, 1, ActorId.Create("test")).Value;
        role.DomainEvents.MarkChangesAsCommitted();
        _roleRepo.Setup(r => r.GetByIdAsync(roleId, It.IsAny<CancellationToken>())).ReturnsAsync(role);

        var profile = ProfileAggregate.Create(
            TenantId.Load(TenantGuid), UserId.Load(UserGuid),
            RoleId.Load(roleId), null, ActorId.Create("test")).Value;
        profile.DomainEvents.MarkChangesAsCommitted();
        _profileRepo.Setup(r => r.GetByIdAsync(profile.GetId().GetValue(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(profile);
        _profileRepo.Setup(r => r.GetActiveByUserAndTenantAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new List<ProfileAggregate> { profile });

        return (profile, role, resourceId, actionId);
    }

    private static SystemSuiteAggregate BuildSuiteWithDomainResource(out Guid resourceId, out Guid actionId)
    {
        var actor = ActorId.Create("test");
        var suite = SystemSuiteAggregate.Create(
            TenantId.Load(Guid.NewGuid()),
            Code.Create("CORE"), Name.Create("Core System"), Description.Create(""),
            actor).Value;

        actionId   = suite.RegisterAction(ActionCode.Create("VIEW"), Name.Create("View Records"), actor).Value;
        resourceId = suite.AddDomainResource(
            null, null, DomainResourceType.Aggregate,
            Code.Create("USERS"), Name.Create("Users"), Description.Create(""), actor).Value;
        suite.DomainEvents.MarkChangesAsCommitted();
        return suite;
    }

    /// <summary>
    /// Crea y publica una plantilla del inquilino de prueba con un único item para
    /// (Aggregate, resourceId, actionId) con el efecto indicado. El RoleId es libre por
    /// defecto (AssignTemplate no lo valida); se puede fijar para que el filtro del paso 5
    /// del constructor la seleccione (prueba de scaffolding G-016).
    /// </summary>
    private static PermissionTemplate MakePublishedTemplate(
        Guid resourceId, Guid actionId, bool isAllowed, bool isDenied, Guid? roleId = null)
    {
        var actor = ActorId.Create("test");
        var template = PermissionTemplate.Create(
            TenantId.Load(TenantGuid),
            RoleId.Load(roleId ?? Guid.NewGuid()),
            SystemSuiteId.Load(Guid.NewGuid()),
            actor).Value;

        Assert.True(template.AddItem(
            ExclusiveArcTarget.Aggregate, IdValueObject.Load(resourceId), ActionId.Load(actionId),
            isAllowed, isDenied, actor).IsSuccess);
        Assert.True(template.Publish(actor).IsSuccess);
        template.DomainEvents.MarkChangesAsCommitted();
        return template;
    }

    /// <summary>
    /// Resolución efectiva de un par recurso-acción. Desde el contrato v2.0.0 las filas
    /// `NotGranted` NO viajan —la ausencia es la denegación—, así que «no encontrado» y
    /// «NotGranted» son lo mismo y este ayudante los unifica.
    /// </summary>
    private static GraphDomainAction ResolveDomainAction(
        AuthorizationGraph graph, string resourceCode, string actionCode)
        => graph.DomainPermissions
                .SingleOrDefault(p => p.ResourceCode == resourceCode)
                ?.Actions.SingleOrDefault(a => a.ActionCode == actionCode)
           ?? new GraphDomainAction(Guid.Empty, actionCode, actionCode,
                                    AccessEffect.NotGranted, PermissionSource.Template);
}
