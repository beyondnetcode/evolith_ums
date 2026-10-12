namespace Evolith.Ums.Application.Test.Identity.Auth;

using Evolith.Ums.Application.Authorization.Graph;
using Evolith.Ums.Application.Authorization.Graph.Serializers;
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
/// Tests unitarios de <see cref="RefreshAuthenticationCommandHandler"/> (ADR-UMS-091 / FR-015/016).
/// Dan cobertura verificable (SD-04/SD-05) al residual «falta E2E dedicado» de G-050 y G-034:
///
///   G-050 — auditoría y revocación:
///     · cada fallo de renovación (incl. <b>detección de reuso</b>) se audita;
///     · los códigos de fallo se distinguen (el reuso NO colapsa con la expiración);
///
///   G-034 — refresh configurable + revocación:
///     · fail-closed cuando la política del inquilino está apagada;
///     · detección de reuso ⇒ invalidación de la familia;
///     · revocación efectiva ante inquilino/usuario inactivos (bloqueo/suspensión);
///     · <b>regeneración COMPLETA del grafo</b> al renovar;
///     · rotación del token (y su ausencia cuando la política no rota).
///
/// Todas las dependencias están mockeadas — sin BD ni infraestructura.
/// </summary>
public sealed class RefreshAuthenticationCommandHandlerTests
{
    private readonly Mock<IRefreshTokenStore>            _store          = new();
    private readonly Mock<IRefreshTokenPolicyProvider>   _policyProvider = new();
    private readonly Mock<ITenantRepository>             _tenantRepo     = new();
    private readonly Mock<IUserAccountRepository>        _userRepo       = new();
    private readonly Mock<IAuthMethodResolver>           _methodResolver = new();
    private readonly Mock<IAuthorizationGraphBuilder>    _graphBuilder   = new();
    private readonly Mock<IAuthGraphFormatProvider>      _formatProvider = new();
    private readonly Mock<IAuthorizationGraphSerializer> _serializer     = new();
    private readonly Mock<IAuthAuditService>             _auditService   = new();

    private static readonly Guid TenantGuid = Guid.NewGuid();
    private static readonly Guid UserGuid   = Guid.NewGuid();
    private static readonly Guid FamilyGuid = Guid.NewGuid();

    private RefreshAuthenticationCommandHandler CreateSut() => new(
        _store.Object, _policyProvider.Object, _tenantRepo.Object, _userRepo.Object,
        _methodResolver.Object, _graphBuilder.Object, _formatProvider.Object,
        _serializer.Object, _auditService.Object);

    private static RefreshAuthenticationCommand Command(string token = "opaque-refresh")
        => new(token, ClientIp: "10.0.0.1");

    // ── Validación de entrada ────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenRefreshTokenEmpty_ReturnsInvalid_WithoutTouchingStore()
    {
        var result = await CreateSut().Handle(Command(" "), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains(RefreshErrorCodes.Invalid, result.Error);
        _store.Verify(s => s.FindByHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenTokenUnknown_ReturnsInvalid_AndAuditsFailure()
    {
        _store.Setup(s => s.FindByHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync((RefreshTokenSnapshot?)null);

        var result = await CreateSut().Handle(Command(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains(RefreshErrorCodes.Invalid, result.Error);
        VerifyFailureAudited(RefreshErrorCodes.Invalid);
    }

    // ── G-050: detección de reuso auditada + código distinto (no colapsa a expiración) ──

    [Fact]
    public async Task Handle_WhenRotatedTokenPresented_DetectsReuse_RevokesFamily_AndAudits()
    {
        SetupSnapshot(RefreshTokenStatuses.Rotated);
        _policyProvider.Setup(p => p.Resolve(It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<Guid?>()))
                       .Returns(EnabledPolicy(detectReuse: true));

        var result = await CreateSut().Handle(Command(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains(RefreshErrorCodes.ReuseDetected, result.Error);
        _store.Verify(s => s.RevokeFamilyAsync(FamilyGuid, "reuse_detected", It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
        VerifyFailureAudited(RefreshErrorCodes.ReuseDetected);
    }

    [Fact]
    public async Task Handle_WhenReuseDetectionDisabled_DoesNotRevokeFamily_ButStillReportsReuse()
    {
        SetupSnapshot(RefreshTokenStatuses.Used);
        _policyProvider.Setup(p => p.Resolve(It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<Guid?>()))
                       .Returns(EnabledPolicy(detectReuse: false));

        var result = await CreateSut().Handle(Command(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains(RefreshErrorCodes.ReuseDetected, result.Error);
        _store.Verify(s => s.RevokeFamilyAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
        VerifyFailureAudited(RefreshErrorCodes.ReuseDetected);
    }

    [Fact]
    public async Task Handle_WhenRevoked_ReturnsRevoked_DistinctFromReuse_AndAudits()
    {
        SetupSnapshot(RefreshTokenStatuses.Revoked);

        var result = await CreateSut().Handle(Command(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains(RefreshErrorCodes.Revoked, result.Error);
        Assert.DoesNotContain(RefreshErrorCodes.ReuseDetected, result.Error);
        VerifyFailureAudited(RefreshErrorCodes.Revoked);
    }

    [Fact]
    public async Task Handle_WhenExpired_ReturnsExpired_DistinctFromReuse_AndAudits()
    {
        // Un token Active pero vencido ⇒ AUTH_REFRESH_004, nunca el código de reuso:
        // el bug histórico (G-050) era que expiración y reuso colapsaban a AUTH_007.
        SetupSnapshot(RefreshTokenStatuses.Active, expiresAtUtc: DateTime.UtcNow.AddMinutes(-1));

        var result = await CreateSut().Handle(Command(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains(RefreshErrorCodes.Expired, result.Error);
        Assert.DoesNotContain(RefreshErrorCodes.ReuseDetected, result.Error);
        VerifyFailureAudited(RefreshErrorCodes.Expired);
    }

    // ── G-034: fail-closed cuando el inquilino no tiene la capacidad activa ───────

    [Fact]
    public async Task Handle_WhenPolicyDisabled_ReturnsDisabled_FailClosed()
    {
        SetupSnapshot(RefreshTokenStatuses.Active);
        _policyProvider.Setup(p => p.Resolve(It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<Guid?>()))
                       .Returns(RefreshTokenPolicy.Disabled);

        var result = await CreateSut().Handle(Command(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains(RefreshErrorCodes.Disabled, result.Error);
        VerifyFailureAudited(RefreshErrorCodes.Disabled);
        // Fail-closed: no se regenera grafo ni se rota nada.
        _graphBuilder.Verify(g => g.BuildAsync(It.IsAny<UserAccountAggregate>(), It.IsAny<Guid>(), It.IsAny<AuthMethod>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenMaxRenewalsReached_RevokesFamily_AndReturnsMaxRenewals()
    {
        SetupSnapshot(RefreshTokenStatuses.Active, renewalCount: 5);
        _policyProvider.Setup(p => p.Resolve(It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<Guid?>()))
                       .Returns(EnabledPolicy(maxRenewals: 5));

        var result = await CreateSut().Handle(Command(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains(RefreshErrorCodes.MaxRenewals, result.Error);
        _store.Verify(s => s.RevokeFamilyAsync(FamilyGuid, "max_renewals", It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
        VerifyFailureAudited(RefreshErrorCodes.MaxRenewals);
    }

    // ── G-034: revocación efectiva ante principal inactivo (bloqueo/suspensión) ───

    [Fact]
    public async Task Handle_WhenTenantInactive_RevokesFamily_ReturnsPrincipalGone()
    {
        SetupSnapshot(RefreshTokenStatuses.Active);
        SetupEnabledPolicy();
        _tenantRepo.Setup(r => r.GetByIdAsync(TenantGuid, It.IsAny<CancellationToken>()))
                   .ReturnsAsync((TenantAggregate?)null);

        var result = await CreateSut().Handle(Command(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains(RefreshErrorCodes.PrincipalGone, result.Error);
        _store.Verify(s => s.RevokeFamilyAsync(FamilyGuid, "tenant_inactive", It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
        VerifyFailureAudited(RefreshErrorCodes.PrincipalGone);
    }

    [Fact]
    public async Task Handle_WhenUserBlocked_RevokesFamily_ReturnsPrincipalGone()
    {
        // Un usuario bloqueado no puede renovar ⇒ la familia se revoca (revocación efectiva
        // por bloqueo/suspensión, G-034/D-012).
        SetupSnapshot(RefreshTokenStatuses.Active);
        SetupEnabledPolicy();
        SetupActiveTenant();
        _userRepo.Setup(r => r.GetByIdAsync(UserGuid, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(BuildBlockedUser());

        var result = await CreateSut().Handle(Command(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains(RefreshErrorCodes.PrincipalGone, result.Error);
        _store.Verify(s => s.RevokeFamilyAsync(FamilyGuid, "user_inactive", It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
        VerifyFailureAudited(RefreshErrorCodes.PrincipalGone);
    }

    // ── G-034: happy-path — regeneración COMPLETA del grafo + rotación + auditoría ─

    [Fact]
    public async Task Handle_HappyPath_RegeneratesGraph_RotatesToken_AndAuditsSuccess()
    {
        SetupSnapshot(RefreshTokenStatuses.Active);
        SetupEnabledPolicy(rotate: true);
        SetupActiveTenant();
        SetupActiveUser();
        SetupGraphPipeline();

        var result = await CreateSut().Handle(Command(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        // Regeneración COMPLETA del grafo (decisión 1 de ADR-UMS-091): el builder se invoca.
        _graphBuilder.Verify(g => g.BuildAsync(It.IsAny<UserAccountAggregate>(), TenantGuid, It.IsAny<AuthMethod>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
        // Rotación: el token vigente se canjea por uno nuevo de la misma familia.
        _store.Verify(s => s.RotateAsync(It.IsAny<RefreshTokenSnapshot>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.False(string.IsNullOrWhiteSpace(result.Value.NewRefreshToken));
        // Auditoría de éxito persistida (G-050).
        _auditService.Verify(a => a.RecordAuthEventAsync(
            It.Is<AuthAuditEvent>(e => e.EventType == "Auth.Refresh.Success" && e.Succeeded),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_HappyPath_WhenPolicyDoesNotRotate_KeepsClientToken()
    {
        SetupSnapshot(RefreshTokenStatuses.Active);
        SetupEnabledPolicy(rotate: false);
        SetupActiveTenant();
        SetupActiveUser();
        SetupGraphPipeline();

        var result = await CreateSut().Handle(Command(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        _store.Verify(s => s.RotateAsync(It.IsAny<RefreshTokenSnapshot>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Null(result.Value.NewRefreshToken);
        // Aun sin rotación, el grafo se regenera por completo.
        _graphBuilder.Verify(g => g.BuildAsync(It.IsAny<UserAccountAggregate>(), TenantGuid, It.IsAny<AuthMethod>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── G-295/G-297: la familia recuerda la sesión y el sistema del ingreso ───────

    /// <summary>
    /// Este canje es ANÓNIMO —el refresh token es la única credencial que llega—, así que si la
    /// familia no recordase la sesión y el sistema, nadie los sabría. Se comprobaba lo contrario por
    /// omisión: el handler pasaba <c>systemCode: null</c> fijo y no devolvía sesión alguna, de modo
    /// que el portador renovado salía con un `sid` recién inventado —invisible para el logout— y con
    /// el grafo del sistema que ganase el desempate.
    /// </summary>
    [Fact]
    public async Task Handle_PropagaLaSesionYElSistemaQueRecuerdaLaFamilia()
    {
        const string sesionDelIngreso = "0f9a1c2b3d4e5f60718293a4b5c6d7e8";
        SetupSnapshot(RefreshTokenStatuses.Active, sessionId: sesionDelIngreso, systemCode: "UMS");
        SetupEnabledPolicy(rotate: true);
        SetupActiveTenant();
        SetupActiveUser();
        SetupGraphPipeline();

        var result = await CreateSut().Handle(Command(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        // G-297: el grafo se rearma sobre el sistema PEDIDO, no por desempate.
        _graphBuilder.Verify(g => g.BuildAsync(
            It.IsAny<UserAccountAggregate>(), TenantGuid, It.IsAny<AuthMethod>(),
            "UMS", It.IsAny<CancellationToken>()), Times.Once);
        // G-295: la sesión sube a presentación, que es quien acuña el `sid` del portador nuevo.
        Assert.Equal(sesionDelIngreso, result.Value.SessionId);
    }

    /// <summary>
    /// Compatibilidad hacia atrás: una familia emitida ANTES de este cambio no recuerda nada, y
    /// entonces la renovación debe comportarse como siempre en vez de fallar. Una corrección de
    /// seguridad que cierra de golpe todas las sesiones vivas se revierte el mismo día.
    /// </summary>
    [Fact]
    public async Task Handle_ConFamiliaAnteriorAlCambio_RenuevaComoAntes()
    {
        SetupSnapshot(RefreshTokenStatuses.Active);   // sin sesión ni sistema
        SetupEnabledPolicy(rotate: true);
        SetupActiveTenant();
        SetupActiveUser();
        SetupGraphPipeline();

        var result = await CreateSut().Handle(Command(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.SessionId);
        _graphBuilder.Verify(g => g.BuildAsync(
            It.IsAny<UserAccountAggregate>(), TenantGuid, It.IsAny<AuthMethod>(),
            null, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Setup helpers ────────────────────────────────────────────────────────────

    private void SetupSnapshot(
        string status,
        DateTime? expiresAtUtc = null,
        int renewalCount = 0,
        string? sessionId = null,
        string? systemCode = null)
    {
        var snapshot = new RefreshTokenSnapshot(
            Id:           Guid.NewGuid(),
            TenantId:     TenantGuid,
            UserId:       UserGuid,
            FamilyId:     FamilyGuid,
            Status:       status,
            IssuedAtUtc:  DateTime.UtcNow.AddMinutes(-10),
            ExpiresAtUtc: expiresAtUtc ?? DateTime.UtcNow.AddDays(1),
            RenewalCount: renewalCount,
            SessionId:    sessionId,
            SystemCode:   systemCode);

        _store.Setup(s => s.FindByHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(snapshot);
    }

    private static RefreshTokenPolicy EnabledPolicy(
        bool rotate = true, bool detectReuse = true, int maxRenewals = 0)
        => new(Enabled: true, LifetimeMinutes: 60, Rotate: rotate, DetectReuse: detectReuse, MaxRenewals: maxRenewals);

    private void SetupEnabledPolicy(bool rotate = true)
        => _policyProvider.Setup(p => p.Resolve(It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<Guid?>()))
                          .Returns(EnabledPolicy(rotate: rotate));

    private void SetupActiveTenant()
        => _tenantRepo.Setup(r => r.GetByIdAsync(TenantGuid, It.IsAny<CancellationToken>()))
                      .ReturnsAsync(BuildActiveTenant());

    private void SetupActiveUser()
        => _userRepo.Setup(r => r.GetByIdAsync(UserGuid, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(BuildActiveUser());

    private void SetupGraphPipeline()
    {
        _methodResolver.Setup(m => m.ResolveAsync(TenantGuid, It.IsAny<AuthAccessScope>(), It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                       .ReturnsAsync(Result<AuthMethod>.Success(AuthMethod.Local()));
        _graphBuilder.Setup(g => g.BuildAsync(It.IsAny<UserAccountAggregate>(), TenantGuid, It.IsAny<AuthMethod>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(Result<AuthorizationGraph>.Success(BuildGraph()));
        _formatProvider.Setup(f => f.GetDefaultFormatAsync(TenantGuid, It.IsAny<CancellationToken>()))
                       .ReturnsAsync("JSON");
        _serializer.Setup(s => s.Serialize(It.IsAny<AuthorizationGraph>(), It.IsAny<GraphSerializationOptions?>()))
                   .Returns("{}");
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

    private static AuthorizationGraph BuildGraph()
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
            AccessTokenDurationMs: 900_000, AuthUseExternalIdp: false);

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

    private void VerifyFailureAudited(string code)
        => _auditService.Verify(a => a.RecordAuthEventAsync(
            It.Is<AuthAuditEvent>(e =>
                e.EventType == "Auth.Refresh.Failure" &&
                !e.Succeeded &&
                e.FailureReason != null && e.FailureReason.Contains(code)),
            It.IsAny<CancellationToken>()), Times.Once);
}
