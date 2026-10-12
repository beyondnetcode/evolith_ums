namespace Evolith.Ums.Presentation.IntegrationTest.Identity;

using Microsoft.EntityFrameworkCore;
using Moq;
using Evolith.Ums.Application.Authorization.Graph;
using Evolith.Ums.Application.Authorization.Graph.Serializers;
using Evolith.Ums.Application.Identity.Auth;
using Evolith.Ums.Application.Identity.Auth.Commands;
using Evolith.Ums.Domain.Authorization.Graph;
using Evolith.Ums.Domain.Identity.Auth;
using Evolith.Ums.Domain.Kernel;
using Evolith.Ums.Infrastructure.Persistence.Identity.Entities;
using TenantAggregate      = Evolith.Ums.Domain.Identity.Tenant.Tenant;
using UserAccountAggregate = Evolith.Ums.Domain.Identity.UserAccount.UserAccount;

/// <summary>
/// Cobertura de persistencia (SD-04/SD-05) de la revocación/rotación de refresh tokens
/// (ADR-UMS-091 / FR-015/016, G-050). Los tests de handler de la capa Application mockean el
/// <see cref="IRefreshTokenStore"/>; aquí se ejerce el <b>store real</b> (RefreshTokenStore
/// sobre EF InMemory, igual patrón que <c>PostgreSqlUserAccountRepositoryTests</c>) para probar
/// que la revocación es <b>efectiva en el almacén</b> y no solo en el mock:
///
///   · logout (<see cref="RefreshTokenStore.RevokeAllForUserAsync"/>) marca Revoked todas las
///     familias vivas del usuario ⇒ un token revocado <b>ya no renueva</b> (round-trip
///     store + <see cref="RefreshAuthenticationCommandHandler"/> real);
///   · la revocación de familia (reuso / max-renewals / principal inactivo) invalida la cadena
///     entera y es idempotente;
///   · la rotación deja el token anterior Rotated (detectable como reuso) y crea el nuevo Active
///     con el contador incrementado;
///   · aislamiento estricto multi-inquilino: revocar (usuario, inquilino) no toca otros
///     usuarios ni otros inquilinos (invariante de ADR-UMS-091).
///
/// El mecanismo opaco sigue apagado en el piloto (fail-closed); estos tests NO lo encienden a
/// nivel de configuración: ejercen directamente la lógica de store/handler, que debe estar
/// completa y probada aunque el flag esté OFF.
/// </summary>
public sealed class RefreshTokenStoreTests
{
    private static readonly Guid TenantGuid = Guid.NewGuid();
    private static readonly Guid UserGuid   = Guid.NewGuid();

    // ── Store: emisión + búsqueda por hash ───────────────────────────────────────

    [Fact]
    public async Task IssueAsync_ThenFindByHash_ReturnsActiveSnapshot()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var ctx = CreateContext();
        var store = new RefreshTokenStore(ctx);

        var plaintext = RefreshTokenGenerator.Generate();
        var hash = RefreshTokenHasher.Hash(plaintext);
        var now = DateTime.UtcNow;

        await store.IssueAsync(TenantGuid, UserGuid, Guid.NewGuid(), hash, now, now.AddMinutes(60), sessionId: null, systemCode: null, ct);

        var found = await store.FindByHashAsync(hash, ct);

        found.Should().NotBeNull();
        found!.Status.Should().Be(RefreshTokenStatuses.Active);
        found.TenantId.Should().Be(TenantGuid);
        found.UserId.Should().Be(UserGuid);
        found.RenewalCount.Should().Be(0);
    }

    // ── Store: logout revoca ⇒ el token deja de ser renovable ────────────────────

    [Fact]
    public async Task RevokeAllForUserAsync_MarksLiveTokensRevoked_SoTheyNoLongerRenew()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var ctx = CreateContext();
        var store = new RefreshTokenStore(ctx);

        var hash = RefreshTokenHasher.Hash(RefreshTokenGenerator.Generate());
        var now = DateTime.UtcNow;
        await store.IssueAsync(TenantGuid, UserGuid, Guid.NewGuid(), hash, now, now.AddMinutes(60), sessionId: null, systemCode: null, ct);

        // Logout: no conoce el familyId, revoca por (inquilino, usuario).
        await store.RevokeAllForUserAsync(TenantGuid, UserGuid, "logout", now, ct);

        // El token sigue existiendo pero ya no está Active ⇒ el handler lo rechaza como Revoked.
        var found = await store.FindByHashAsync(hash, ct);
        found.Should().NotBeNull();
        found!.Status.Should().Be(RefreshTokenStatuses.Revoked);

        var record = await ctx.RefreshTokens.SingleAsync(r => r.TokenHash == hash, ct);
        record.RevokedReason.Should().Be("logout");
        record.RevokedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task RevokeAllForUserAsync_DoesNotCrossUsersOrTenants()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var ctx = CreateContext();
        var store = new RefreshTokenStore(ctx);
        var now = DateTime.UtcNow;

        var targetHash    = RefreshTokenHasher.Hash("target");
        var otherUserHash = RefreshTokenHasher.Hash("other-user");
        var otherTenantHash = RefreshTokenHasher.Hash("other-tenant");
        var otherUserId   = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();

        await store.IssueAsync(TenantGuid, UserGuid, Guid.NewGuid(), targetHash, now, now.AddMinutes(60), sessionId: null, systemCode: null, ct);
        await store.IssueAsync(TenantGuid, otherUserId, Guid.NewGuid(), otherUserHash, now, now.AddMinutes(60), sessionId: null, systemCode: null, ct);
        await store.IssueAsync(otherTenantId, UserGuid, Guid.NewGuid(), otherTenantHash, now, now.AddMinutes(60), sessionId: null, systemCode: null, ct);

        await store.RevokeAllForUserAsync(TenantGuid, UserGuid, "logout", now, ct);

        // Solo el token del (inquilino, usuario) objetivo se revoca.
        (await store.FindByHashAsync(targetHash, ct))!.Status.Should().Be(RefreshTokenStatuses.Revoked);
        // El mismo inquilino, otro usuario ⇒ intacto.
        (await store.FindByHashAsync(otherUserHash, ct))!.Status.Should().Be(RefreshTokenStatuses.Active);
        // El mismo usuario, otro inquilino ⇒ intacto (aislamiento estricto de ADR-UMS-091).
        (await store.FindByHashAsync(otherTenantHash, ct))!.Status.Should().Be(RefreshTokenStatuses.Active);
    }

    // ── Store: revocación de familia (reuso / max-renewals / principal inactivo) ──

    [Fact]
    public async Task RevokeFamilyAsync_RevokesWholeFamily_AndIsIdempotent()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var ctx = CreateContext();
        var store = new RefreshTokenStore(ctx);
        var now = DateTime.UtcNow;
        var familyId = Guid.NewGuid();

        var firstHash  = RefreshTokenHasher.Hash("family-1");
        var secondHash = RefreshTokenHasher.Hash("family-2");
        await store.IssueAsync(TenantGuid, UserGuid, familyId, firstHash, now, now.AddMinutes(60), sessionId: null, systemCode: null, ct);
        await store.IssueAsync(TenantGuid, UserGuid, familyId, secondHash, now, now.AddMinutes(60), sessionId: null, systemCode: null, ct);

        await store.RevokeFamilyAsync(familyId, "reuse_detected", now, ct);

        (await store.FindByHashAsync(firstHash, ct))!.Status.Should().Be(RefreshTokenStatuses.Revoked);
        (await store.FindByHashAsync(secondHash, ct))!.Status.Should().Be(RefreshTokenStatuses.Revoked);

        // Idempotente: repetir no arroja ni cambia nada.
        await store.RevokeFamilyAsync(familyId, "reuse_detected", now.AddSeconds(1), ct);
        var revokedCount = await ctx.RefreshTokens.CountAsync(r => r.FamilyId == familyId && r.Status == RefreshTokenStatuses.Revoked, ct);
        revokedCount.Should().Be(2);
    }

    // ── Store: rotación ⇒ el anterior queda reutilizable-detectable, el nuevo Active ─

    [Fact]
    public async Task RotateAsync_MarksOldRotated_AndAddsActiveWithIncrementedRenewalCount()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var ctx = CreateContext();
        var store = new RefreshTokenStore(ctx);
        var now = DateTime.UtcNow;
        var familyId = Guid.NewGuid();

        var oldHash = RefreshTokenHasher.Hash("old");
        await store.IssueAsync(TenantGuid, UserGuid, familyId, oldHash, now, now.AddMinutes(60), sessionId: null, systemCode: null, ct);
        var current = await store.FindByHashAsync(oldHash, ct);

        var newTokenId = Guid.NewGuid();
        var newHash = RefreshTokenHasher.Hash("new");
        await store.RotateAsync(current!, newTokenId, newHash, now, now.AddMinutes(60), ct);

        // El anterior queda Rotated (presentarlo de nuevo será reuso), apuntando al nuevo.
        var oldSnapshot = await store.FindByHashAsync(oldHash, ct);
        oldSnapshot!.Status.Should().Be(RefreshTokenStatuses.Rotated);
        var oldRecord = await ctx.RefreshTokens.SingleAsync(r => r.TokenHash == oldHash, ct);
        oldRecord.ReplacedByTokenId.Should().Be(newTokenId);

        // El nuevo entra Active en la misma familia con el contador incrementado.
        var newSnapshot = await store.FindByHashAsync(newHash, ct);
        newSnapshot!.Status.Should().Be(RefreshTokenStatuses.Active);
        newSnapshot.FamilyId.Should().Be(familyId);
        newSnapshot.RenewalCount.Should().Be(1);
    }

    /// <summary>
    /// G-295/G-297: la sesión y el sistema pedido son de la FAMILIA, y la rotación los hereda.
    ///
    /// <para>Sin esto el arreglo duraría exactamente una renovación: la primera devolvería el
    /// portador con la sesión correcta y la segunda —la que rota sobre el token ya rotado— saldría
    /// otra vez con una sesión inventada y con el grafo del desempate. Es el modo de fallo más caro,
    /// porque parece arreglado.</para>
    /// </summary>
    [Fact]
    public async Task RotateAsync_HeredaLaSesionYElSistemaDeLaFamilia()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var ctx = CreateContext();
        var store = new RefreshTokenStore(ctx);
        var now = DateTime.UtcNow;
        var familyId = Guid.NewGuid();
        const string sesionDelIngreso = "a1b2c3d4e5f60718293a4b5c6d7e8f90";

        var oldHash = RefreshTokenHasher.Hash("old-con-memoria");
        await store.IssueAsync(TenantGuid, UserGuid, familyId, oldHash, now, now.AddMinutes(60),
            sessionId: sesionDelIngreso, systemCode: "UMS", ct);

        var current = await store.FindByHashAsync(oldHash, ct);
        current!.SessionId.Should().Be(sesionDelIngreso, because: "la emisión debe persistirlo, no solo aceptarlo");
        current.SystemCode.Should().Be("UMS");

        var newHash = RefreshTokenHasher.Hash("new-con-memoria");
        await store.RotateAsync(current, Guid.NewGuid(), newHash, now, now.AddMinutes(60), ct);

        var rotado = await store.FindByHashAsync(newHash, ct);
        rotado!.SessionId.Should().Be(sesionDelIngreso,
            because: "rotar cambia la credencial, no la sesión");
        rotado.SystemCode.Should().Be("UMS",
            because: "rotar tampoco cambia de producto");
    }

    // ── Round-trip store + handler real: un token ACTIVO renueva ─────────────────

    [Fact]
    public async Task ActiveToken_RenewsThroughRealStore_AndRotationIsPersisted()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var ctx = CreateContext();
        var store = new RefreshTokenStore(ctx);

        var plaintext = RefreshTokenGenerator.Generate();
        var oldHash = RefreshTokenHasher.Hash(plaintext);
        var now = DateTime.UtcNow;
        await store.IssueAsync(TenantGuid, UserGuid, Guid.NewGuid(), oldHash, now, now.AddMinutes(60), sessionId: null, systemCode: null, ct);

        var handler = CreateHandler(store, out var audit);

        var result = await handler.Handle(new RefreshAuthenticationCommand(plaintext, "10.0.0.1"), ct);

        result.IsSuccess.Should().BeTrue();
        result.Value.NewRefreshToken.Should().NotBeNullOrWhiteSpace();
        // La rotación se persistió: el token presentado quedó Rotated en el store real.
        (await store.FindByHashAsync(oldHash, ct))!.Status.Should().Be(RefreshTokenStatuses.Rotated);
        audit.Verify(a => a.RecordAuthEventAsync(
            It.Is<AuthAuditEvent>(e => e.EventType == "Auth.Refresh.Success" && e.Succeeded),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Round-trip store + handler real: tras logout, el MISMO token ya no renueva ─

    [Fact]
    public async Task RevokedTokenNoLongerRenews_AfterLogout_EndToEnd()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var ctx = CreateContext();
        var store = new RefreshTokenStore(ctx);

        var plaintext = RefreshTokenGenerator.Generate();
        var hash = RefreshTokenHasher.Hash(plaintext);
        var now = DateTime.UtcNow;
        await store.IssueAsync(TenantGuid, UserGuid, Guid.NewGuid(), hash, now, now.AddMinutes(60), sessionId: null, systemCode: null, ct);

        // Logout real (ADR-UMS-091/FR-016): revoca todas las familias vivas del usuario.
        await store.RevokeAllForUserAsync(TenantGuid, UserGuid, "logout", now, ct);

        var handler = CreateHandler(store, out var audit);

        var result = await handler.Handle(new RefreshAuthenticationCommand(plaintext, "10.0.0.1"), ct);

        // Revocación efectiva: el token revocado ya NO renueva, y el fallo se audita.
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain(RefreshErrorCodes.Revoked);
        audit.Verify(a => a.RecordAuthEventAsync(
            It.Is<AuthAuditEvent>(e =>
                e.EventType == "Auth.Refresh.Failure" && !e.Succeeded &&
                e.FailureReason != null && e.FailureReason.Contains(RefreshErrorCodes.Revoked)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Round-trip store + handler real: el reuso de un token rotado revoca la familia ─

    [Fact]
    public async Task ReuseOfRotatedToken_RevokesFamilyInRealStore_AndAudits()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var ctx = CreateContext();
        var store = new RefreshTokenStore(ctx);

        var plaintext = RefreshTokenGenerator.Generate();
        var hash = RefreshTokenHasher.Hash(plaintext);
        var now = DateTime.UtcNow;
        var familyId = Guid.NewGuid();
        await store.IssueAsync(TenantGuid, UserGuid, familyId, hash, now, now.AddMinutes(60), sessionId: null, systemCode: null, ct);

        var handler = CreateHandler(store, out var audit);

        // Primera renovación: rota ⇒ el token presentado queda Rotated y nace uno nuevo Active.
        var first = await handler.Handle(new RefreshAuthenticationCommand(plaintext, "10.0.0.1"), ct);
        first.IsSuccess.Should().BeTrue();

        // Segunda presentación del MISMO (ya rotado) ⇒ reuso ⇒ familia revocada en el store real.
        var reuse = await handler.Handle(new RefreshAuthenticationCommand(plaintext, "10.0.0.1"), ct);

        reuse.IsFailure.Should().BeTrue();
        reuse.Error.Should().Contain(RefreshErrorCodes.ReuseDetected);
        // Toda la familia (incluido el token nuevo Active) queda Revoked: la sesión se corta.
        var live = await ctx.RefreshTokens.CountAsync(
            r => r.FamilyId == familyId && r.Status != RefreshTokenStatuses.Revoked, ct);
        live.Should().Be(0);
        audit.Verify(a => a.RecordAuthEventAsync(
            It.Is<AuthAuditEvent>(e =>
                e.EventType == "Auth.Refresh.Failure" && !e.Succeeded &&
                e.FailureReason != null && e.FailureReason.Contains(RefreshErrorCodes.ReuseDetected)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Infra de test ────────────────────────────────────────────────────────────

    private static UmsPlatformDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<UmsPlatformDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        // Contexto de sistema (OrganizationId null): RefreshTokenRecord no lleva filtro global
        // de inquilino — el aislamiento lo impone el store por predicado explícito, y estos
        // tests lo verifican.
        return new UmsPlatformDbContext(
            options,
            new SystemTenantContext(),
            new Mock<MediatR.IPublisher>().Object,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<UmsPlatformDbContext>.Instance);
    }

    /// <summary>
    /// Handler real cableado con el <paramref name="store"/> real; el resto de colaboradores
    /// se mockean con un principal (inquilino/usuario) activo y una política habilitada, para
    /// aislar el efecto de la revocación en el almacén.
    /// </summary>
    private static RefreshAuthenticationCommandHandler CreateHandler(
        IRefreshTokenStore store, out Mock<IAuthAuditService> audit)
    {
        var policyProvider = new Mock<IRefreshTokenPolicyProvider>();
        policyProvider.Setup(p => p.Resolve(It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<Guid?>()))
                      .Returns(new RefreshTokenPolicy(Enabled: true, LifetimeMinutes: 60, Rotate: true, DetectReuse: true, MaxRenewals: 0));

        var tenantRepo = new Mock<ITenantRepository>();
        tenantRepo.Setup(r => r.GetByIdAsync(TenantGuid, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(BuildActiveTenant());

        var userRepo = new Mock<IUserAccountRepository>();
        userRepo.Setup(r => r.GetByIdAsync(UserGuid, It.IsAny<CancellationToken>()))
                .ReturnsAsync(BuildActiveUser());

        var methodResolver = new Mock<IAuthMethodResolver>();
        methodResolver.Setup(m => m.ResolveAsync(TenantGuid, It.IsAny<AuthAccessScope>(), It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                      .ReturnsAsync(Result<AuthMethod>.Success(AuthMethod.Local()));

        var graphBuilder = new Mock<IAuthorizationGraphBuilder>();
        graphBuilder.Setup(g => g.BuildAsync(It.IsAny<UserAccountAggregate>(), TenantGuid, It.IsAny<AuthMethod>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(Result<AuthorizationGraph>.Success(BuildGraph()));

        var formatProvider = new Mock<IAuthGraphFormatProvider>();
        formatProvider.Setup(f => f.GetDefaultFormatAsync(TenantGuid, It.IsAny<CancellationToken>()))
                      .ReturnsAsync("JSON");

        var serializer = new Mock<IAuthorizationGraphSerializer>();
        serializer.Setup(s => s.Serialize(It.IsAny<AuthorizationGraph>(), It.IsAny<GraphSerializationOptions?>()))
                  .Returns("{}");

        audit = new Mock<IAuthAuditService>();

        return new RefreshAuthenticationCommandHandler(
            store, policyProvider.Object, tenantRepo.Object, userRepo.Object,
            methodResolver.Object, graphBuilder.Object, formatProvider.Object,
            serializer.Object, audit.Object);
    }

    private static TenantAggregate BuildActiveTenant()
    {
        var tenant = TenantAggregate.Create(
            Code.Create("TEST"),
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
            Email.Create("user@test.com"),
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

    private sealed class SystemTenantContext : ITenantContext
    {
        public Guid? OrganizationId   => null;
        public Guid? OriginalTenantId => null;
        public bool  IsInternalAdmin  => true;
        public void Initialize(Guid userTenantId, bool isInternalAdmin) { }
        public void SetOrganizationId(Guid organizationId) { }
        public void EnableCrossTenantAccess() { }
        public void DisableCrossTenantAccess() { }
    }
}
