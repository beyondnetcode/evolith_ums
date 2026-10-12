namespace Evolith.Ums.Application.Test.Identity.Auth;

using Evolith.Ums.Application.Authorization.Graph;
using Evolith.Ums.Application.Authorization.Graph.Serializers;
using Evolith.Ums.Application.Common.Interfaces;
using Evolith.Ums.Application.Configuration.Services;
using Evolith.Ums.Application.Identity.Auth.Commands;
using Evolith.Ums.Domain.Authorization.Graph;
using Evolith.Ums.Domain.Configuration.AppConfiguration;
using Evolith.Ums.Domain.Identity;
using Evolith.Ums.Domain.Identity.Auth;
using TenantAggregate      = Evolith.Ums.Domain.Identity.Tenant.Tenant;
using UserAccountAggregate = Evolith.Ums.Domain.Identity.UserAccount.UserAccount;

/// <summary>
/// Pruebas unitarias de <see cref="AuthenticateUserCommandHandler"/> centradas en el bloqueo
/// temporal de cuenta por intentos fallidos (ADR-UMS-095). Todas las dependencias están mockeadas
/// —sin BD ni infraestructura— y el instante determinista se controla desde el arrange del dominio.
///
/// Cumplimiento (ADR-UMS-095, sección test-first):
///   · Tras MaxLoginAttempts fallos, el intento N+1 con credencial CORRECTA devuelve «cuenta
///     bloqueada» (AUTH_017), no un login exitoso, y NO se llega a construir el grafo.
///   · Tras expirar LockedUntil, el login correcto vuelve a funcionar y resetea el contador.
/// </summary>
public sealed class AuthenticateUserCommandHandlerTests
{
    private readonly Mock<ITenantRepository>              _tenantRepo     = new();
    private readonly Mock<IUserAccountRepository>         _userRepo       = new();
    private readonly Mock<IAuthMethodResolver>            _methodResolver = new();
    private readonly Mock<ILocalAuthStrategy>             _localStrategy  = new();
    private readonly Mock<IIdpChainAuthenticator>         _chainAuth      = new();
    private readonly Mock<IAuthorizationGraphBuilder>     _graphBuilder   = new();
    private readonly Mock<IAuthorizationGraphSerializer>  _serializer     = new();
    private readonly Mock<IAuthAuditService>              _auditService   = new();
    private readonly Mock<IConfigurationProvider>         _configProvider = new();
    private readonly Mock<IUnitOfWork>                    _unitOfWork     = new();

    private const int MaxAttempts    = 3;
    private const int LockoutMinutes = 15;
    private const string CorrectPassword = "correct-horse";
    private const string WrongPassword   = "wrong-password";
    private const string TenantCode      = "TEST";
    private const string UserEmail        = "user@test.com";

    private static readonly Guid TenantGuid = Guid.NewGuid();
    private static readonly Guid UserGuid   = Guid.NewGuid();

    private AuthenticateUserCommandHandler CreateSut() => new(
        _tenantRepo.Object, _userRepo.Object, _methodResolver.Object,
        _localStrategy.Object, _chainAuth.Object, _graphBuilder.Object,
        _serializer.Object, _auditService.Object,
        _configProvider.Object);

    private static AuthenticateUserCommand Command(string password)
        => new(TenantCode, UserEmail, password, ClientIp: "10.0.0.1", AuthAccessScope.ExternalApi);

    // ── Test-first ADR-UMS-095: intento N+1 con credencial correcta ⇒ AUTH_017 ─────────

    [Fact]
    public async Task AuthenticateLocal_AfterMaxFailedAttempts_ReturnsAccountLocked()
    {
        var user = BuildActiveUser();
        SetupLocalPipeline(user);
        var sut = CreateSut();

        // MaxLoginAttempts fallos consecutivos con credencial incorrecta.
        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            var failed = await sut.Handle(Command(WrongPassword), CancellationToken.None);
            Assert.True(failed.IsFailure);
            Assert.Contains("AUTH_006", failed.Error);
        }

        // Intento N+1 con credencial CORRECTA: debe rechazar por bloqueo, NO iniciar sesión.
        var locked = await sut.Handle(Command(CorrectPassword), CancellationToken.None);

        Assert.True(locked.IsFailure);
        Assert.Contains("AUTH_017", locked.Error);
        Assert.True(user.IsLockedOut(DateTimeOffset.UtcNow));
        // No se llega a construir el grafo: el rechazo ocurre antes de validar credenciales.
        _graphBuilder.Verify(g => g.BuildAsync(
            It.IsAny<UserAccountAggregate>(), It.IsAny<Guid>(), It.IsAny<AuthMethod>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task AuthenticateLocal_BelowThreshold_ThenCorrect_Succeeds()
    {
        var user = BuildActiveUser();
        SetupLocalPipeline(user);
        var sut = CreateSut();

        // Un fallo (por debajo del umbral) no bloquea; el login correcto siguiente entra.
        var failed = await sut.Handle(Command(WrongPassword), CancellationToken.None);
        Assert.True(failed.IsFailure);

        var ok = await sut.Handle(Command(CorrectPassword), CancellationToken.None);

        Assert.True(ok.IsSuccess);
        Assert.Equal(0, user.FailedLoginAttempts);
        Assert.Null(user.LockedUntil);
    }

    // ── Test-first ADR-UMS-095: tras expirar el bloqueo, el login correcto reabre y resetea ──

    [Fact]
    public async Task AuthenticateLocal_AfterLockoutExpires_CorrectLoginSucceedsAndResets()
    {
        var user = BuildActiveUser();
        // Arrange determinista: bloqueo ya vencido. El instante se inyecta en el dominio (no reloj real):
        // un fallo con umbral 1 y duración 1 min, situado 30 min en el pasado ⇒ LockedUntil ~29 min atrás.
        var past = DateTimeOffset.UtcNow.AddMinutes(-30);
        user.RecordAuthenticationAttempt(false, past, maxAttempts: 1, lockoutMinutes: 1, "Invalid password", "10.0.0.1", ActorId.Create("test"));
        user.DomainEvents.MarkChangesAsCommitted();
        Assert.False(user.IsLockedOut(DateTimeOffset.UtcNow));

        SetupLocalPipeline(user);
        var sut = CreateSut();

        var ok = await sut.Handle(Command(CorrectPassword), CancellationToken.None);

        Assert.True(ok.IsSuccess);
        Assert.Equal(0, user.FailedLoginAttempts);
        Assert.Null(user.LockedUntil);
        Assert.False(user.IsLockedOut(DateTimeOffset.UtcNow));
    }

    // ── Setup helpers ────────────────────────────────────────────────────────────

    private void SetupLocalPipeline(UserAccountAggregate user)
    {
        _tenantRepo.Setup(r => r.GetByCodeAsync(TenantCode, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(BuildActiveTenant());

        _methodResolver.Setup(m => m.ResolveAsync(TenantGuid, It.IsAny<AuthAccessScope>(), It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                       .ReturnsAsync(Result<AuthMethod>.Success(AuthMethod.Local()));

        // G-168: el login busca acotado al inquilino. El stub global por email queda a
        // propósito devolviendo null: si alguien reintroduce la búsqueda sin inquilino, estas
        // pruebas fallan en vez de pasar por casualidad.
        _userRepo.Setup(r => r.GetByTenantAndEmailAsync(
                     TenantGuid, It.IsAny<Email>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(user);
        _userRepo.Setup(r => r.UpdateAsync(It.IsAny<UserAccountAggregate>(), It.IsAny<CancellationToken>()))
                 .Returns(Task.CompletedTask);
        _unitOfWork.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _userRepo.Setup(r => r.UnitOfWork).Returns(_unitOfWork.Object);
        _serializer.Setup(x => x.FileExtension).Returns("json");

        // Estrategia local: la contraseña correcta valida; cualquier otra falla (AUTH_006).
        _localStrategy.Setup(s => s.Authenticate(It.IsAny<UserAccountAggregate>(), CorrectPassword))
                      .Returns(Result.Success());
        _localStrategy.Setup(s => s.Authenticate(It.IsAny<UserAccountAggregate>(), It.Is<string>(p => p != CorrectPassword)))
                      .Returns(Result.Failure("AUTH_006: Invalid username or password."));

        // Config efectiva: umbral y duración del bloqueo (ADR-UMS-095). MfaRequiredForAdmin queda en false por defecto.
        _configProvider.Setup(p => p.GetValueAs<int>(AppConfigurationCodes.MaxLoginAttempts, It.IsAny<Guid?>(), It.IsAny<int>()))
                       .Returns(MaxAttempts);
        _configProvider.Setup(p => p.GetValueAs<int>(AppConfigurationCodes.AccountLockoutDurationMinutes, It.IsAny<Guid?>(), It.IsAny<int>()))
                       .Returns(LockoutMinutes);

        // Pipeline del grafo para el camino exitoso.
        _graphBuilder.Setup(g => g.BuildAsync(It.IsAny<UserAccountAggregate>(), TenantGuid, It.IsAny<AuthMethod>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(Result<AuthorizationGraph>.Success(BuildGraph()));
        _serializer.Setup(s => s.Serialize(It.IsAny<AuthorizationGraph>(), It.IsAny<GraphSerializationOptions?>()))
                   .Returns("{}");
    }

    private static TenantAggregate BuildActiveTenant()
    {
        var tenant = TenantAggregate.Create(
            Code.Create(TenantCode),
            Name.Create("Test Tenant"),
            OrganizationType.INTERNAL,
            ActorId.Create("test"),
            IdpStrategy.InternalBcrypt,
            tenantId: TenantId.Load(TenantGuid)).Value;   // Create ⇒ Status Active
        tenant.DomainEvents.MarkChangesAsCommitted();
        return tenant;
    }

    private static UserAccountAggregate BuildActiveUser()
    {
        var user = UserAccountAggregate.Create(
            TenantId.Load(TenantGuid),
            Email.Create(UserEmail),
            UserCategory.Internal,
            null, null,
            ActorId.Create("test"),
            null,
            UserAccountId.Load(UserGuid)).Value;
        user.Activate(ActorId.Create("test"));
        user.DomainEvents.MarkChangesAsCommitted();
        return user;
    }

    private static AuthorizationGraph BuildGraph()
    {
        var context = new GraphContext(
            new GraphUser(UserGuid, UserEmail, "user", "User", "Active"),
            new GraphTenant(TenantGuid, TenantCode, "Test Tenant", "Active", false),
            SystemSuite: null, Role: null, Profile: null, Branch: null);

        var authentication = new GraphAuthentication(
            "Local", Provider: null, MfaRequired: false,
            IssuedAt: DateTime.UtcNow, SessionExpiresAt: DateTime.UtcNow.AddMinutes(30));

        var effectiveConfig = new GraphEffectiveConfig(
            SessionTimeoutMinutes: 30, MaxLoginAttempts: MaxAttempts, MinPasswordLength: 8,
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
}
