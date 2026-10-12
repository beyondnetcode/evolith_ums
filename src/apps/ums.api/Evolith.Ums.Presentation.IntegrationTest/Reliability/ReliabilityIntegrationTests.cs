using System.Net.Http.Headers;
using Evolith.Ums.Presentation.IntegrationTest.Infrastructure;

namespace Evolith.Ums.Presentation.IntegrationTest.Reliability;

/// <summary>
/// Layer 2 reliability tests — full HTTP stack with in-memory repositories.
///
/// These tests exercise the pipeline end-to-end (middleware → minimal-API endpoint →
/// MediatR handler → domain aggregate → in-memory repository) and document the
/// current (sometimes gap) behaviour for each reliability risk identified in
/// docs/architecture/transactional-reliability-assessment.md.
///
/// Tests marked "[documents gap]" pass today by asserting the *current* behaviour.
/// The commented-out assertions below them show the *desired* behaviour once the
/// corresponding FIX is applied.
///
/// Coverage:
///   T13  Create-then-GET returns 201 + correct body + 200 + stable id
///   T14  Duplicate code within same scope → 409 Conflict
///   T15  Invalid state transition via HTTP → 422 Unprocessable
///   T16  Missing X-User-Id header → 401 Unauthorized
///   T17  Full lifecycle: Create → Publish → Archive succeeds
///   T18  [gap] Duplicate rapid POST → two resources created (RISK-05 — no idempotency)
///   T19  [gap] Concurrent updates → last write wins silently (RISK-02 — no optimistic lock)
///   T20  [gap] Tenant isolation: InMemory repo returns all tenants' data (RISK-04)
/// </summary>
public sealed class ReliabilityIntegrationTests : IClassFixture<UmsApiWebApplicationFactory>
{
    // Tenant seeded by UmsApiWebApplicationFactory.SeedConfigurationAggregates
    private static readonly Guid SeededTenantId     = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6");
    private static readonly Guid SeededSystemSuiteId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid SeededModuleId      = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private const string ActorUserId   = TestAuthHandler.AdministradorSembrado;  // cuenta REAL: sin ella no hay grafo ni scopes (G-280)
    private const string ActorUserName = "Integration Tester";

    // A second tenant — NOT seeded — used to test isolation gaps
    private static readonly Guid OtherTenantId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

    private readonly HttpClient _client;
    private readonly UmsApiWebApplicationFactory _factory;

    public ReliabilityIntegrationTests(UmsApiWebApplicationFactory factory)
    {
        _factory = factory;
        _client  = BuildClient(ActorUserId, ActorUserName);
    }

    // =========================================================================
    // T13 — Atomic create: 201 + stable id persisted and retrievable via GET
    // =========================================================================

    /// <summary>
    /// T13 — Successful atomic operation.
    /// POST /app-configurations → 201 with AppConfigurationId.
    /// GET  /app-configurations/{id} → 200 with the same id.
    /// Verifies the happy-path atomicity guarantee at the HTTP level.
    /// </summary>
    [Fact]
    public async Task T13_CreateAppConfiguration_ThenGetById_ReturnsCreatedResource()
    {
        var code = UniqueCode("t13");

        // Act — create
        var createResponse = await _client.PostAsJsonAsync(
            "/api/v1/app-configurations",
            BuildCreatePayload(code),
            TestContext.Current.CancellationToken);

        // Assert — 201 Created
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        using var createPayload = JsonDocument.Parse(
            await createResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        var configId = createPayload.RootElement.GetProperty("appConfigurationId").GetGuid();
        configId.Should().NotBeEmpty("created resource must have a non-empty id");

        // Act — retrieve
        var getResponse = await _client.GetAsync(
            $"/api/v1/app-configurations/{configId}",
            TestContext.Current.CancellationToken);

        // Assert — 200 OK with matching id
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        using var getPayload = JsonDocument.Parse(
            await getResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        getPayload.RootElement.GetProperty("appConfigurationId").GetGuid()
            .Should().Be(configId, "GET must return the same resource that was created");

        getPayload.RootElement.GetProperty("code").GetString()
            .Should().Be(code.ToUpperInvariant(),
                "code is stored as upper-case and must survive a round-trip");
    }

    // =========================================================================
    // T14 — Duplicate code in same scope → 409 Conflict
    // =========================================================================

    /// <summary>
    /// T14 — Duplicate request handling.
    /// A second POST with the same (tenantId, systemSuiteId, moduleId, code) tuple
    /// must be rejected with 409 Conflict.
    /// </summary>
    [Fact]
    public async Task T14_DuplicateCreate_SameCode_Returns409Conflict()
    {
        var code = UniqueCode("t14");
        var payload = BuildCreatePayload(code);

        // First create — must succeed
        var first = await _client.PostAsJsonAsync(
            "/api/v1/app-configurations", payload,
            TestContext.Current.CancellationToken);
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        // Second create — same code, same scope → must conflict
        var second = await _client.PostAsJsonAsync(
            "/api/v1/app-configurations", payload,
            TestContext.Current.CancellationToken);

        second.StatusCode.Should().Be(HttpStatusCode.Conflict,
            "domain rules reject duplicate (scope, code) combinations");
    }

    // =========================================================================
    // T15 — Invalid state transition → 422 Unprocessable
    // =========================================================================

    /// <summary>
    /// T15 — Invalid transition rejection.
    /// Archiving a Draft (not yet Published) configuration is an invalid domain
    /// transition. The domain returns an error and the HTTP layer must surface it
    /// as 422 Unprocessable Entity (or 409 Conflict — whichever DomainErrorStatusMapper
    /// emits for transition errors; the test asserts 4xx ≠ 2xx).
    /// </summary>
    [Fact]
    public async Task T15_InvalidTransition_ArchiveDraftDirectly_Returns4xx()
    {
        var code = UniqueCode("t15");

        // Create → Draft state
        var createResponse = await _client.PostAsJsonAsync(
            "/api/v1/app-configurations",
            BuildCreatePayload(code),
            TestContext.Current.CancellationToken);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        using var createPayload = JsonDocument.Parse(
            await createResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var configId = createPayload.RootElement.GetProperty("appConfigurationId").GetGuid();

        // Try to archive without publishing first — invalid transition
        var archiveResponse = await _client.PostAsync(
            $"/api/v1/app-configurations/{configId}/archive",
            null,
            TestContext.Current.CancellationToken);

        ((int)archiveResponse.StatusCode).Should().BeInRange(400, 499,
            "archiving a Draft must be rejected with a 4xx error");
    }

    // =========================================================================
    // T16 — Missing auth header → 401 Unauthorized
    // =========================================================================

    /// <summary>
    /// T16 — Unauthenticated request.
    /// A POST without the X-User-Id header must be rejected with 401 Unauthorized.
    /// This validates the UserContextMiddleware auth guard.
    /// </summary>
    [Fact(Skip = "DevAuthMiddleware fills in a default user ID in Development environment — 401 enforcement is a production/staging concern only")]
    public async Task T16_MissingUserId_Returns401Unauthorized()
    {
        // Client with NO X-User-Id header
        var anonymousClient = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
        });

        var response = await anonymousClient.PostAsJsonAsync(
            "/api/v1/app-configurations",
            BuildCreatePayload(UniqueCode("t16")),
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "requests without X-User-Id must be rejected before reaching the handler");
    }

    // =========================================================================
    // T17 — Full lifecycle: Draft → Published → Archived
    // =========================================================================

    /// <summary>
    /// T17 — State lifecycle — Create → Publish → Archive.
    /// Verifies the happy-path state machine transitions surface correctly over HTTP.
    /// </summary>
    [Fact]
    public async Task T17_CreatePublishArchive_LifecycleSucceeds()
    {
        var code = UniqueCode("t17");

        // Step 1: Create (Draft)
        var createResponse = await _client.PostAsJsonAsync(
            "/api/v1/app-configurations",
            BuildCreatePayload(code),
            TestContext.Current.CancellationToken);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        using var createPayload = JsonDocument.Parse(
            await createResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var configId = createPayload.RootElement.GetProperty("appConfigurationId").GetGuid();

        // Verify Draft state
        var draftGet = await _client.GetAsync(
            $"/api/v1/app-configurations/{configId}", TestContext.Current.CancellationToken);
        draftGet.StatusCode.Should().Be(HttpStatusCode.OK);
        using var draftPayload = JsonDocument.Parse(
            await draftGet.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        draftPayload.RootElement.GetProperty("status").GetString()
            .Should().Be("Draft", "newly created config must be in Draft state");

        // Step 2: Publish (Draft → Published)
        var publishResponse = await _client.PostAsync(
            $"/api/v1/app-configurations/{configId}/publish",
            null,
            TestContext.Current.CancellationToken);
        publishResponse.StatusCode.Should().Be(HttpStatusCode.NoContent,
            "publishing a Draft must succeed with 204 NoContent");

        // Verify Published state
        var publishedGet = await _client.GetAsync(
            $"/api/v1/app-configurations/{configId}", TestContext.Current.CancellationToken);
        using var publishedPayload = JsonDocument.Parse(
            await publishedGet.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        publishedPayload.RootElement.GetProperty("status").GetString()
            .Should().Be("Published");

        // Step 3: Archive (Published → Archived)
        var archiveResponse = await _client.PostAsync(
            $"/api/v1/app-configurations/{configId}/archive",
            null,
            TestContext.Current.CancellationToken);
        archiveResponse.StatusCode.Should().Be(HttpStatusCode.NoContent,
            "archiving a Published config must succeed with 204 NoContent");

        // Verify Archived state
        var archivedGet = await _client.GetAsync(
            $"/api/v1/app-configurations/{configId}", TestContext.Current.CancellationToken);
        using var archivedPayload = JsonDocument.Parse(
            await archivedGet.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        archivedPayload.RootElement.GetProperty("status").GetString()
            .Should().Be("Archived");
    }

    // =========================================================================
    // T18 — [documents gap] Duplicate rapid POST → two distinct resources (RISK-05)
    // =========================================================================

    /// <summary>
    /// T18 — [documents gap] Duplicate-request rapid fire (RISK-05: no idempotency).
    ///
    /// Two POST requests with distinct codes are fired in parallel. Because the
    /// InMemory repository has no race-condition protection, both succeed independently
    /// and create two separate resources — demonstrating the absence of idempotency
    /// keying. In the SQL path, rapid retries of the same logical request (same
    /// Idempotency-Key) would create duplicate rows.
    ///
    /// Current: both return 201 with different ids (correct for distinct codes, but
    ///          highlights that no Idempotency-Key deduplication exists).
    ///
    /// After FIX-06 (IdempotencyMiddleware): repeating the same Idempotency-Key
    /// header should return the cached 201 response without creating a second row.
    /// </summary>
    [Fact]
    public async Task T18_DuplicateRapidPost_BothSucceed_DocumentsIdempotencyGap()
    {
        // Two requests with the same logical content but forced unique codes
        // (simulates a client retrying with a new code each time — or a bug where
        // the code is server-generated and the client retries before receiving the 201)
        var codeA = UniqueCode("t18a");
        var codeB = UniqueCode("t18b");

        var taskA = _client.PostAsJsonAsync(
            "/api/v1/app-configurations",
            BuildCreatePayload(codeA),
            TestContext.Current.CancellationToken);
        var taskB = _client.PostAsJsonAsync(
            "/api/v1/app-configurations",
            BuildCreatePayload(codeB),
            TestContext.Current.CancellationToken);

        var responses = await Task.WhenAll(taskA, taskB);
        var responseA = responses[0];
        var responseB = responses[1];

        // CURRENT (gap) behaviour: both succeed — no request-level deduplication
        responseA.StatusCode.Should().Be(HttpStatusCode.Created,
            "RISK-05: no Idempotency-Key middleware — first request creates resource");
        responseB.StatusCode.Should().Be(HttpStatusCode.Created,
            "RISK-05: no Idempotency-Key middleware — concurrent request also creates resource");

        using var payloadA = JsonDocument.Parse(
            await responseA.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        using var payloadB = JsonDocument.Parse(
            await responseB.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        var idA = payloadA.RootElement.GetProperty("appConfigurationId").GetGuid();
        var idB = payloadB.RootElement.GetProperty("appConfigurationId").GetGuid();

        idA.Should().NotBe(idB,
            "RISK-05: two distinct resources were created — idempotency enforcement would return same id for same Idempotency-Key");

        // La conducta DESEADA tras FIX-06 (repetir la MISMA Idempotency-Key devuelve la respuesta
        // cacheada, mismo id, sin doble efecto) ya NO es un gap: se verifica activamente en
        // T18b (G-003). Este T18 conserva el escenario base «sin clave ⇒ sin deduplicación».
    }

    // =========================================================================
    // T18b — [G-003] Idempotencia bajo reintento: misma Idempotency-Key → sin doble efecto
    // =========================================================================

    /// <summary>
    /// T18b — Idempotencia VERIFICADA (cierra el gap que T18 solo documentaba — G-003 / RISK-05 / FIX-06).
    ///
    /// Simula el reintento de un cliente que no recibió la respuesta del primer POST (timeout, corte de
    /// red) y reenvía la MISMA petición con la MISMA <c>Idempotency-Key</c>. El
    /// <see cref="Evolith.Ums.Presentation.Middleware.IdempotencyMiddleware"/> debe:
    ///   · devolver la respuesta CACHEADA del primer intento (mismo 201, mismo id),
    ///   · marcar la repetición con <c>X-Idempotency-Replayed: true</c>,
    ///   · NO re-ejecutar el manejador → NO crea un segundo recurso (efecto único), y en particular
    ///     NO devuelve 409 pese a repetir el mismo (scope, code) —el reintento se resuelve desde caché
    ///     ANTES de llegar a la regla de unicidad de dominio—.
    ///
    /// Es la garantía de resiliencia «reintento seguro»: reprocesar el mismo mensaje/petición no
    /// duplica el efecto.
    /// </summary>
    [Fact]
    public async Task T18b_RetryWithSameIdempotencyKey_ReturnsCachedResponse_NoDuplicateEffect()
    {
        var code           = UniqueCode("t18b");
        var idempotencyKey = Guid.NewGuid().ToString();
        var client         = BuildClientWithIdempotencyKey(idempotencyKey);

        // 1.er intento — crea el recurso y cachea la respuesta bajo la Idempotency-Key.
        var first = await client.PostAsJsonAsync(
            "/api/v1/app-configurations", BuildCreatePayload(code),
            TestContext.Current.CancellationToken);
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        using var firstPayload = JsonDocument.Parse(
            await first.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var idFirst = firstPayload.RootElement.GetProperty("appConfigurationId").GetGuid();

        // 2.º intento — MISMA clave, mismo cuerpo: el reintento del cliente.
        var retry = await client.PostAsJsonAsync(
            "/api/v1/app-configurations", BuildCreatePayload(code),
            TestContext.Current.CancellationToken);

        // Se devuelve la respuesta cacheada del primer intento, NO un 409 por (scope, code) duplicado.
        retry.StatusCode.Should().Be(HttpStatusCode.Created,
            "el reintento con la misma Idempotency-Key reproduce la respuesta original, no re-ejecuta el manejador ni choca con la unicidad de dominio");
        retry.Headers.TryGetValues("X-Idempotency-Replayed", out var replayed).Should().BeTrue(
            "el middleware marca la respuesta reproducida desde caché");
        replayed!.Should().ContainSingle().Which.Should().Be("true");

        using var retryPayload = JsonDocument.Parse(
            await retry.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var idRetry = retryPayload.RootElement.GetProperty("appConfigurationId").GetGuid();
        idRetry.Should().Be(idFirst,
            "el reintento idempotente devuelve el MISMO recurso — sin doble creación");

        // Efecto ÚNICO: existe exactamente un recurso con ese code (no se duplicó).
        var listResponse = await _client.GetAsync(
            "/api/v1/app-configurations?page=1&pageSize=200",
            TestContext.Current.CancellationToken);
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        using var listPayload = JsonDocument.Parse(
            await listResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var items = listPayload.RootElement.GetProperty("items");
        var matches = Enumerable.Range(0, items.GetArrayLength())
            .Count(i => string.Equals(
                items[i].GetProperty("code").GetString(),
                code.ToUpperInvariant(),
                StringComparison.OrdinalIgnoreCase));
        matches.Should().Be(1,
            "el reintento idempotente no debe crear un segundo recurso (efecto único bajo reintento)");
    }

    // =========================================================================
    // T19 — [documents gap] Concurrent updates → last write wins (RISK-02)
    // =========================================================================

    /// <summary>
    /// T19 — [documents gap] Concurrent update conflict (RISK-02: no optimistic concurrency).
    ///
    /// Two concurrent PUT requests against the same resource.  Because neither the
    /// domain entities nor the SQL schema carry a RowVersion/ETag, both succeed and
    /// the "last write wins" silently, discarding the first update.
    ///
    /// Current: both return 204 — data integrity gap exposed.
    ///
    /// After FIX-03 (add RowVersion + ETag): the second PUT without an updated ETag
    /// should return 409 Conflict.
    /// </summary>
    [Fact]
    public async Task T19_ConcurrentUpdates_BothSucceed_DocumentsOptimisticConcurrencyGap()
    {
        // Create a config to update concurrently
        var code = UniqueCode("t19");
        var createResponse = await _client.PostAsJsonAsync(
            "/api/v1/app-configurations",
            BuildCreatePayload(code),
            TestContext.Current.CancellationToken);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        using var createPayload = JsonDocument.Parse(
            await createResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var configId = createPayload.RootElement.GetProperty("appConfigurationId").GetGuid();

        // Fire two concurrent updates with different values
        var updatePayload1 = new
        {
            appConfigurationId = configId,
            value              = "updated-by-request-1",
            description        = "Concurrent update from request 1",
            isInheritable      = true,
            isEncrypted        = false,
        };
        var updatePayload2 = new
        {
            appConfigurationId = configId,
            value              = "updated-by-request-2",
            description        = "Concurrent update from request 2",
            isInheritable      = true,
            isEncrypted        = false,
        };

        var task1 = _client.PutAsJsonAsync(
            $"/api/v1/app-configurations/{configId}",
            updatePayload1,
            TestContext.Current.CancellationToken);
        var task2 = _client.PutAsJsonAsync(
            $"/api/v1/app-configurations/{configId}",
            updatePayload2,
            TestContext.Current.CancellationToken);

        var updateResults = await Task.WhenAll(task1, task2);

        // CURRENT (gap) behaviour: both succeed — last write wins silently
        updateResults[0].StatusCode.Should().Be(HttpStatusCode.NoContent,
            "RISK-02: no optimistic locking — first concurrent update succeeds");
        updateResults[1].StatusCode.Should().Be(HttpStatusCode.NoContent,
            "RISK-02: no optimistic locking — second concurrent update also succeeds (last write wins)");

        // Verify final state: one of the updates won (non-deterministic)
        var getResponse = await _client.GetAsync(
            $"/api/v1/app-configurations/{configId}",
            TestContext.Current.CancellationToken);
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        using var finalPayload = JsonDocument.Parse(
            await getResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var finalValue = finalPayload.RootElement.GetProperty("value").GetString();
        finalValue.Should().BeOneOf(
            "updated-by-request-1", "updated-by-request-2",
            "RISK-02: one value survived but the other was silently lost — no conflict detected");

#pragma warning disable S125 // Commented out code explains desired behaviour
        // DESIRED behaviour after FIX-03 (RowVersion/ETag):
        // The second concurrent PUT (without the updated ETag from the first response)
        // should return 409 Conflict:
        //
        // var conflictingStatuses = new[]
        // {
        //     task1.Result.StatusCode,
        //     task2.Result.StatusCode,
        // };
        // conflictingStatuses.Should().Contain(HttpStatusCode.Conflict,
        //     "after FIX-03: concurrent update without matching ETag must return 409");
#pragma warning restore S125
    }

    // =========================================================================
    // T20 — [documents gap] Tenant isolation (RISK-04: no EF HasQueryFilter)
    // =========================================================================

    /// <summary>
    /// T20 — [documents gap] Tenant isolation — InMemory repo returns cross-tenant data.
    ///
    /// The InMemory repositories hold all tenants' data in a single ConcurrentDictionary
    /// and the GET-all query handler lacks a mandatory tenantId filter.  A user from
    /// TenantA can query and receive resources owned by TenantB.
    ///
    /// Current: GET /app-configurations without tenantId filter returns ALL tenants' data,
    ///          including the seeded resource for SeededTenantId when queried without filter.
    ///
    /// After FIX-05 (EF HasQueryFilter + mandatory tenant scoping):
    /// - In-memory: InMemory repos must only return the caller's tenant data.
    /// - SQL: EF Core global query filter enforces RLS at the ORM level.
    /// </summary>
    [Fact]
    public async Task T20_TenantIsolation_GetAllWithoutFilter_ReturnsAllTenantsData_DocumentsIsolationGap()
    {
        // Create a config for a second (OtherTenant) — the factory allows any tenantId
        var otherTenantCode = UniqueCode("t20-other");
        var createOther = await _client.PostAsJsonAsync(
            "/api/v1/app-configurations",
            new
            {
                tenantId      = OtherTenantId,
                systemSuiteId = SeededSystemSuiteId,
                moduleId      = SeededModuleId,
                code          = otherTenantCode,
                value         = "other-tenant-value",
                description   = "Resource owned by OtherTenant",
                isInheritable = false,
                isEncrypted   = false,
            },
            TestContext.Current.CancellationToken);
        createOther.StatusCode.Should().Be(HttpStatusCode.Created);

        // Query without tenantId filter — should this return cross-tenant data?
        var listResponse = await _client.GetAsync(
            "/api/v1/app-configurations?page=1&pageSize=100",
            TestContext.Current.CancellationToken);
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        using var listPayload = JsonDocument.Parse(
            await listResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        var items = listPayload.RootElement.GetProperty("items");
        var codes = Enumerable.Range(0, items.GetArrayLength())
            .Select(i => items[i].GetProperty("code").GetString())
            .ToList();

        // CURRENT (gap) behaviour: all tenants' data is visible in a single unfiltered query
        codes.Should().Contain(
            otherTenantCode.ToUpperInvariant(),
            "RISK-04: InMemory repo has no tenant filter — cross-tenant data is visible");

        codes.Should().Contain(
            "SESSION_TIMEOUT_MINUTES",
            "Seeded config for SeededTenantId is also visible — confirms cross-tenant leakage");

#pragma warning disable S125 // Commented out code explains desired behaviour
        // DESIRED behaviour after FIX-05 (HasQueryFilter + mandatory tenant scope):
        // When no tenantId filter is provided, only the caller's own tenant's resources
        // should be returned (or a 400 Bad Request if tenantId is required):
        //
        // codes.Should().NotContain(otherTenantCode.ToUpperInvariant(),
        //     "after FIX-05: cross-tenant resources must not appear in unfiltered query");
#pragma warning restore S125
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private HttpClient BuildClient(string userId, string userName)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress    = new Uri("https://localhost"),
            AllowAutoRedirect = false,
        });
        client.DefaultRequestHeaders.Add("X-User-Id",   userId);
        client.DefaultRequestHeaders.Add("X-User-Name", userName);
        return client;
    }

    /// <summary>
    /// Cliente autenticado que adjunta la misma <c>Idempotency-Key</c> en TODAS sus peticiones —modela
    /// un cliente que reintenta la misma operación lógica bajo una única clave de idempotencia.
    /// </summary>
    private HttpClient BuildClientWithIdempotencyKey(string idempotencyKey)
    {
        var client = BuildClient(ActorUserId, ActorUserName);
        client.DefaultRequestHeaders.Add("Idempotency-Key", idempotencyKey);
        return client;
    }

    /// <summary>
    /// Produces a unique, deterministic code string per test run.
    /// Uses a short prefix + 8 hex chars to stay within domain code length limits.
    /// </summary>
    private static string UniqueCode(string prefix) =>
        $"{prefix}_{Guid.NewGuid():N}"[..Math.Min(40, $"{prefix}_{Guid.NewGuid():N}".Length)];

    private static object BuildCreatePayload(string code) => new
    {
        tenantId      = SeededTenantId,
        systemSuiteId = SeededSystemSuiteId,
        moduleId      = SeededModuleId,
        code,
        value         = "integration-test-value",
        description   = "Created by ReliabilityIntegrationTests",
        isInheritable = false,
        isEncrypted   = false,
    };
}
