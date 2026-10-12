namespace Evolith.Ums.Application.Test.Identity.Auth;

using Evolith.Ums.Application.Authorization.Graph;
using Evolith.Ums.Application.Common.Interfaces;
using Evolith.Ums.Application.Identity.Auth;
using Evolith.Ums.Application.Identity.Auth.Commands;
using Evolith.Ums.Domain.Authorization.Graph;
using Evolith.Ums.Domain.Identity;
using Evolith.Ums.Domain.Identity.Auth;
using Moq;
using Xunit;
using TenantAggregate      = Evolith.Ums.Domain.Identity.Tenant.Tenant;
using UserAccountAggregate = Evolith.Ums.Domain.Identity.UserAccount.UserAccount;

/// <summary>
/// Tests unitarios de <see cref="RefreshSessionCommandHandler"/> (D-019 / ADR-UMS-091).
///
/// El refresh deslizante por cookie debe ESPEJAR el login: regenera el grafo COMPLETO desde el
/// estado vigente (no re-firma claims estancados). Se verifica:
///   · happy-path ⇒ el grafo se regenera (IAuthorizationGraphBuilder) y el evento se audita;
///   · el resultado surfacea el grafo VIGENTE del builder (un cambio de permisos desde el login
///     se refleja, porque el grafo se reconstruye, no se cachea);
///   · corte en caliente ⇒ inquilino/usuario inactivo no renueva (y no se reconstruye el grafo).
///
/// Todas las dependencias están mockeadas — sin BD ni infraestructura.
/// </summary>
public sealed class RefreshSessionCommandHandlerTests
{
    private readonly Mock<ITenantRepository>          _tenantRepo     = new();
    private readonly Mock<IUserAccountRepository>     _userRepo       = new();
    private readonly Mock<IAuthMethodResolver>        _methodResolver = new();
    private readonly Mock<IAuthorizationGraphBuilder> _graphBuilder   = new();
    private readonly Mock<IAuthGraphFormatProvider>   _formatProvider = new();
    private readonly Mock<IAuthAuditService>          _auditService   = new();

    private static readonly Guid TenantGuid = Guid.NewGuid();
    private static readonly Guid UserGuid   = Guid.NewGuid();

    private RefreshSessionCommandHandler CreateSut() => new(
        _tenantRepo.Object, _userRepo.Object, _methodResolver.Object,
        _graphBuilder.Object, _formatProvider.Object, _auditService.Object);

    private static RefreshSessionCommand Command(string? sistemaPedido = null)
        => new(UserGuid, TenantGuid, ClientIp: "10.0.0.1", SystemCode: sistemaPedido);

    // ── Happy-path: regeneración COMPLETA del grafo + auditoría ───────────────────

    [Fact]
    public async Task Handle_HappyPath_RegeneratesGraph_AndAuditsSuccess()
    {
        SetupActiveTenant();
        SetupActiveUser();
        SetupGraphPipeline(BuildGraph(accessTokenDurationMs: 900_000));

        var result = await CreateSut().Handle(Command(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        // Regeneración COMPLETA (decisión 1 de ADR-UMS-091): el builder se invoca.
        _graphBuilder.Verify(g => g.BuildAsync(
            It.IsAny<UserAccountAggregate>(), TenantGuid, It.IsAny<AuthMethod>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Once);
        // ExpiresIn se deriva del grafo vigente, no de un valor hardcodeado.
        Assert.Equal(900, result.Value.ExpiresIn);
        _auditService.Verify(a => a.RecordAuthEventAsync(
            It.Is<AuthAuditEvent>(e => e.EventType == "Auth.Refresh.Success" && e.Succeeded),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_SurfacesCurrentGraph_NotALoginSnapshot()
    {
        // El handler NO tiene acceso a los permisos/claims del login; su única fuente es el
        // builder. Un cambio de permisos aplicado tras el login se refleja porque el grafo se
        // reconstruye desde cero: el resultado ES exactamente el grafo recién construido.
        SetupActiveTenant();
        SetupActiveUser();
        var freshGraph = BuildGraph(accessTokenDurationMs: 1_800_000);
        SetupGraphPipeline(freshGraph);

        var result = await CreateSut().Handle(Command(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Same(freshGraph, result.Value.Graph);
        Assert.Equal(1800, result.Value.ExpiresIn);
    }

    // ── G-297: renovar no puede devolver la sesión a otro sistema ────────────────

    /// <summary>
    /// El sistema que el ingreso pidió llega HASTA el constructor del grafo.
    ///
    /// <para>Aquí se pasaba <c>systemCode: null</c> fijo, así que el grafo se rearmaba por
    /// desempate: para un sujeto con perfil en dos sistemas —`admin@evolith.example` los tiene en SIL
    /// y en UMS— la primera renovación automática podía devolver el token con el `sys_suite` del
    /// sistema que nadie pidió, mientras la SPA seguía pintando el menú del que sí. La prueba mira
    /// el argumento, no el resultado, porque el desempate lo decide el builder: lo que este handler
    /// puede garantizar es que la pregunta llegue completa.</para>
    /// </summary>
    [Fact]
    public async Task Handle_PropagaElSistemaPedido_AlConstruirElGrafo()
    {
        SetupActiveTenant();
        SetupActiveUser();
        SetupGraphPipeline(BuildGraph(accessTokenDurationMs: 900_000));

        var result = await CreateSut().Handle(Command(sistemaPedido: "UMS"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        _graphBuilder.Verify(g => g.BuildAsync(
            It.IsAny<UserAccountAggregate>(), TenantGuid, It.IsAny<AuthMethod>(),
            "UMS", It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// La otra mitad, y no es adorno: sin ella bastaría con acotar SIEMPRE por el sistema resuelto
    /// para poner verde la prueba anterior. Un ingreso que no pidió sistema —el portal
    /// multiproducto— debe seguir renovando sin acotar, o su bloque `profiles` perdería los
    /// sombreros de los demás sistemas y el usuario dejaría de poder cambiarse a ellos.
    /// </summary>
    [Fact]
    public async Task Handle_SinSistemaPedido_SigueRenovandoSinAcotar()
    {
        SetupActiveTenant();
        SetupActiveUser();
        SetupGraphPipeline(BuildGraph(accessTokenDurationMs: 900_000));

        var result = await CreateSut().Handle(Command(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        _graphBuilder.Verify(g => g.BuildAsync(
            It.IsAny<UserAccountAggregate>(), TenantGuid, It.IsAny<AuthMethod>(),
            null, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Corte en caliente: principal inactivo no renueva ─────────────────────────

    [Fact]
    public async Task Handle_WhenTenantInactiveOrMissing_Fails_WithoutRebuildingGraph()
    {
        _tenantRepo.Setup(r => r.GetByIdAsync(TenantGuid, It.IsAny<CancellationToken>()))
                   .ReturnsAsync((TenantAggregate?)null);

        var result = await CreateSut().Handle(Command(), CancellationToken.None);

        Assert.True(result.IsFailure);
        _graphBuilder.Verify(g => g.BuildAsync(
            It.IsAny<UserAccountAggregate>(), It.IsAny<Guid>(), It.IsAny<AuthMethod>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
        VerifyFailureAudited();
    }

    [Fact]
    public async Task Handle_WhenUserBlocked_Fails_WithoutRebuildingGraph()
    {
        SetupActiveTenant();
        _userRepo.Setup(r => r.GetByIdAsync(UserGuid, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(BuildBlockedUser());

        var result = await CreateSut().Handle(Command(), CancellationToken.None);

        Assert.True(result.IsFailure);
        _graphBuilder.Verify(g => g.BuildAsync(
            It.IsAny<UserAccountAggregate>(), It.IsAny<Guid>(), It.IsAny<AuthMethod>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
        VerifyFailureAudited();
    }

    // ── Setup helpers ────────────────────────────────────────────────────────────

    private void SetupActiveTenant()
        => _tenantRepo.Setup(r => r.GetByIdAsync(TenantGuid, It.IsAny<CancellationToken>()))
                      .ReturnsAsync(BuildActiveTenant());

    private void SetupActiveUser()
        => _userRepo.Setup(r => r.GetByIdAsync(UserGuid, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(BuildActiveUser());

    private void SetupGraphPipeline(AuthorizationGraph graph)
    {
        _methodResolver.Setup(m => m.ResolveAsync(TenantGuid, It.IsAny<AuthAccessScope>(), It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                       .ReturnsAsync(Result<AuthMethod>.Success(AuthMethod.Local()));
        _graphBuilder.Setup(g => g.BuildAsync(It.IsAny<UserAccountAggregate>(), TenantGuid, It.IsAny<AuthMethod>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(Result<AuthorizationGraph>.Success(graph));
        _formatProvider.Setup(f => f.GetDefaultFormatAsync(TenantGuid, It.IsAny<CancellationToken>()))
                       .ReturnsAsync("JSON");
    }

    private static TenantAggregate BuildActiveTenant()
    {
        var tenant = TenantAggregate.Create(
            Code.Create("TEST"),
            Name.Create("Test Tenant"),
            Evolith.Ums.Domain.Enums.OrganizationType.INTERNAL,
            ActorId.Create("test"),
            Evolith.Ums.Domain.Enums.IdpStrategy.InternalBcrypt,
            tenantId: TenantId.Load(TenantGuid)).Value;   // Create ⇒ Status Active
        tenant.DomainEvents.MarkChangesAsCommitted();
        return tenant;
    }

    private static UserAccountAggregate BuildActiveUser()
    {
        var user = BuildUser();
        user.Activate(ActorId.Create("test"));
        user.DomainEvents.MarkChangesAsCommitted();
        return user;
    }

    private static UserAccountAggregate BuildBlockedUser()
    {
        var user = BuildUser();
        user.Activate(ActorId.Create("test"));
        user.Block(Reason.Create("suspendido"), ActorId.Create("test"));
        user.DomainEvents.MarkChangesAsCommitted();
        return user;
    }

    private static UserAccountAggregate BuildUser()
        => UserAccountAggregate.Create(
            TenantId.Load(TenantGuid),
            Email.Create("user@test.com"),
            Evolith.Ums.Domain.Enums.UserCategory.Internal,
            null, null,
            ActorId.Create("test"),
            null,
            UserAccountId.Load(UserGuid)).Value;

    private static AuthorizationGraph BuildGraph(int accessTokenDurationMs)
    {
        var context = new GraphContext(
            new GraphUser(UserGuid, "user@test.com", "user", "User", "Active"),
            new GraphTenant(TenantGuid, "TEST", "Test Tenant", "Active", false),
            SystemSuite: null, Role: null, Profile: null, Branch: null);

        var authentication = new GraphAuthentication(
            "Local", Provider: null, MfaRequired: false,
            IssuedAt: DateTime.UtcNow, SessionExpiresAt: DateTime.UtcNow.AddMinutes(30));

        var effectiveConfig = new GraphEffectiveConfig(
            SessionTimeoutMinutes: 30, MaxLoginAttempts: 5, MinPasswordLength: 8,
            MfaRequiredForAdmin: false, MfaAllowedMethods: Array.Empty<string>(),
            AccessTokenDurationMs: accessTokenDurationMs, AuthUseExternalIdp: false);

        return AuthorizationGraph.Build(
            context, authentication,
            Array.Empty<GraphAction>(),
            Array.Empty<GraphMenuModule>(),
            Array.Empty<GraphDomainPermission>(),
            Array.Empty<GraphFeatureFlag>(),
            effectiveConfig,
            Array.Empty<string>(),
            DateTime.UtcNow);
    }

    private void VerifyFailureAudited()
        => _auditService.Verify(a => a.RecordAuthEventAsync(
            It.Is<AuthAuditEvent>(e => e.EventType == "Auth.Refresh.Failure" && !e.Succeeded),
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);
}
