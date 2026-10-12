using System.Net;
using System.Net.Http.Json;
using Evolith.Ums.Application.IGA.DTOs;
using Evolith.Ums.Infrastructure.Persistence.Seeders;
using Evolith.Ums.Presentation.IntegrationTest.Infrastructure;

namespace Evolith.Ums.Presentation.IntegrationTest.Iga;

/// <summary>
/// Pruebas de los endpoints REST del contexto acotado IGA (ADR-UMS-093, G-052). Cubren la máquina de
/// estados de promoción de rol de punta a punta —Draft → PendingEligibilityCheck → PendingManagerApproval
/// → (PendingSecurityReview) → Approved → Executed → Verified—, la confirmación de elegibilidad
/// <em>fail-closed</em> (feliz y borde no elegible), la lectura acotada por inquilino y la segregación de
/// funciones (SoD, INV-RPR3).
///
/// El happy-path se apoya en el <see cref="IgaDevDataSeeder"/>: siembra un <c>RoleMaturityStatus</c>
/// ELEGIBLE para <c>RansaAdminUserId</c> en <c>DemoAdminRoleId</c> y uno NO ELEGIBLE (borde) para
/// <c>RansaAnalystUserId</c> en <c>DemoOperatorRoleId</c>. La segregación de funciones se ejerce con
/// actores distintos por transición mediante el encabezado de prueba <c>X-Test-Actor-Id</c>.
/// </summary>
public sealed class RolePromotionRequestRestEndpointTests : IClassFixture<UmsApiWebApplicationFactory>
{
    private static readonly Guid SeededTenantId = Guid.Parse(CoreDevDataSeeder.RansaTenantId);

    // El actor por omisión: el administrador SEMBRADO, que existe y tiene grafo. Antes era
    // `…0111`, que no corresponde a ninguna cuenta ([[G-286]]).
    private static readonly Guid AuthenticatedRequesterId = Guid.Parse(TestAuthHandler.AdministradorSembrado);

    // Usuario objetivo ELEGIBLE (con RoleMaturityStatus sembrado) y sus roles (ambos sembrados en RANSA).
    private static readonly Guid TargetUserId = Guid.Parse(CoreDevDataSeeder.RansaAdminUserId);
    private static readonly Guid CurrentRoleId = Guid.Parse(CoreDevDataSeeder.DemoAdminRoleId);
    private static readonly Guid TargetRoleId = Guid.Parse(CoreDevDataSeeder.DemoOperatorRoleId);

    // Usuario objetivo NO ELEGIBLE (borde): tiempo insuficiente en nivel ⇒ elegibilidad rechazada.
    private static readonly Guid IneligibleTargetUserId = Guid.Parse(CoreDevDataSeeder.RansaAnalystUserId);
    private static readonly Guid IneligibleCurrentRoleId = Guid.Parse(CoreDevDataSeeder.DemoOperatorRoleId);
    private static readonly Guid IneligibleTargetRoleId = Guid.Parse(CoreDevDataSeeder.DemoAdminRoleId);

    // Actores distintos para respetar la segregación de funciones a lo largo del flujo.
    //
    // Eran GUIDs INVENTADOS —`a0000000-…`, `b0000000-…`— y con el atajo de Admin Root apagado eso ya
    // no se sostiene: no corresponden a ninguna cuenta, no tienen grafo y no tienen un solo scope,
    // así que la petición muere en el guardia y la prueba falla por su andamiaje y no por lo que mide
    // ([[G-286]]). Ahora se PROVISIONAN: cinco cuentas reales, distintas, con perfil de administrador.
    // Lo que la prueba necesita de ellos es que sean distintos, y siguen siéndolo.
    private string _requesterActor = string.Empty;
    private string _approverActor = string.Empty;
    private string _securityActor = string.Empty;
    private string _executorActor = string.Empty;
    private string _verifierActor = string.Empty;

    // UNA sola vez por clase, no una por prueba. xUnit instancia la clase de nuevo en cada prueba,
    // así que aprovisionar en el constructor —o en un flag de instancia— repetiría veinte altas por
    // corrida y el limitador de peticiones responde 429 (G-248), que es lo que se midió.
    private static Task<string[]>? _actores;
    private static readonly SemaphoreSlim Cerrojo = new(1, 1);

    private async Task ProvisionarActoresAsync(CancellationToken ct)
    {
        await Cerrojo.WaitAsync(ct);
        try
        {
            _actores ??= CrearActoresAsync(ct);
        }
        finally
        {
            Cerrojo.Release();
        }

        var actores = await _actores;
        _requesterActor = actores[0];
        _approverActor = actores[1];
        _securityActor = actores[2];
        _executorActor = actores[3];
        _verifierActor = actores[4];
    }

    private async Task<string[]> CrearActoresAsync(CancellationToken ct)
    {
        var actores = new string[5];
        for (var i = 0; i < actores.Length; i++)
        {
            actores[i] = (await IdentidadesDePrueba.CrearAdministradorAsync(_client, ct)).ToString();
        }
        return actores;
    }

    private readonly UmsApiWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public RolePromotionRequestRestEndpointTests(UmsApiWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Add("X-User-Id", TestAuthHandler.AdministradorSembrado);
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", SeededTenantId.ToString());
        // La identidad de estas pruebas es DELIBERADAMENTE arbitraria —varios actores distintos para
        // ejercer la segregacion de funciones—, asi que no puede resolverse contra el catalogo: son
        // cuentas que no existen. Mientras el atajo de Admin Root siga encendido en este anfitrion,
        // decir que el actor es administrador interno es lo que las deja atravesar el guardia; el
        // dia que se apague necesitaran cuentas reales, como el resto (G-280).
        _client.DefaultRequestHeaders.Add("X-Is-Internal-Admin", "true");
    }

    private HttpClient CreateActorClient(string actorId)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", SeededTenantId.ToString());
        // G-057: el host InMemory (UmsApiWebApplicationFactory) autentica con DevAuthMiddleware, que lee
        // X-User-Id (→ NameIdentifier) e IGNORA X-Test-Actor-Id; sin X-User-Id el actor quedaba en
        // "dev-user" (no-GUID) y el create fallaba con 400 «identificador no válido». Se envía el actor
        // también como X-User-Id para que el host InMemory lo honre (el TestAuthHandler del host PostgreSQL
        // sigue leyendo X-Test-Actor-Id); ambos resuelven el mismo actorId → segregación de funciones intacta.
        client.DefaultRequestHeaders.Add("X-User-Id", actorId);
        // La identidad de estas pruebas es DELIBERADAMENTE arbitraria —varios actores distintos para
        // ejercer la segregacion de funciones—, asi que no puede resolverse contra el catalogo: son
        // cuentas que no existen. Mientras el atajo de Admin Root siga encendido en este anfitrion,
        // decir que el actor es administrador interno es lo que las deja atravesar el guardia; el
        // dia que se apague necesitaran cuentas reales, como el resto (G-280).
        client.DefaultRequestHeaders.Add("X-Is-Internal-Admin", "true");
        client.DefaultRequestHeaders.Add("X-Test-Actor-Id", actorId);
        return client;
    }

    [Fact]
    public async Task Create_ThenSubmit_ShouldAdvanceThroughStateMachine()
    {
        await ProvisionarActoresAsync(TestContext.Current.CancellationToken);
        var cancellationToken = TestContext.Current.CancellationToken;
        var command = new
        {
            TenantId = SeededTenantId,
            TargetUserId,
            CurrentRoleId,
            TargetRoleId,
        };

        var createResponse = await _client.PostAsJsonAsync("/api/v1/role-promotion-requests", command, cancellationToken);
        createResponse.EnsureSuccessStatusCode();

        var created = await createResponse.Content.ReadFromJsonAsync<CreateRolePromotionRequestResponse>(cancellationToken);
        created.Should().NotBeNull();

        var getResponse = await _client.GetAsync($"/api/v1/role-promotion-requests/{created!.RolePromotionRequestId}", cancellationToken);
        getResponse.EnsureSuccessStatusCode();

        var draft = await getResponse.Content.ReadFromJsonAsync<RolePromotionRequestDto>(cancellationToken);
        draft.Should().NotBeNull();
        draft!.Status.Should().Be("Draft");
        draft.TargetUserId.Should().Be(TargetUserId);
        draft.RequesterId.Should().Be(AuthenticatedRequesterId);

        var submitResponse = await _client.PostAsync(
            $"/api/v1/role-promotion-requests/{created.RolePromotionRequestId}/submit", content: null, cancellationToken);
        submitResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var afterSubmit = await _client.GetFromJsonAsync<RolePromotionRequestDto>(
            $"/api/v1/role-promotion-requests/{created.RolePromotionRequestId}", cancellationToken);
        afterSubmit.Should().NotBeNull();
        afterSubmit!.Status.Should().Be("PendingEligibilityCheck");
        afterSubmit.RiskScore.Should().NotBeNull();
    }

    /// <summary>
    /// Happy-path completo (G-052): con el <c>RoleMaturityStatus</c> sembrado ELEGIBLE, la solicitud
    /// atraviesa toda la máquina de estados hasta Verified. Cada transición la ejecuta un actor distinto
    /// para respetar la segregación de funciones (INV-RPR3).
    /// </summary>
    [Fact]
    public async Task FullPromotionFlow_WithEligibleMaturity_ShouldReachVerified()
    {
        await ProvisionarActoresAsync(TestContext.Current.CancellationToken);
        var cancellationToken = TestContext.Current.CancellationToken;

        var requester = CreateActorClient(_requesterActor);
        var approver  = CreateActorClient(_approverActor);
        var security  = CreateActorClient(_securityActor);
        var executor  = CreateActorClient(_executorActor);
        var verifier  = CreateActorClient(_verifierActor);

        // 1. Crear (Draft) — el solicitante no es el objetivo.
        var createResponse = await requester.PostAsJsonAsync(
            "/api/v1/role-promotion-requests",
            new { TenantId = SeededTenantId, TargetUserId, CurrentRoleId, TargetRoleId },
            cancellationToken);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResponse.Content.ReadFromJsonAsync<CreateRolePromotionRequestResponse>(cancellationToken);
        created.Should().NotBeNull();
        var id = created!.RolePromotionRequestId;
        var baseUrl = $"/api/v1/role-promotion-requests/{id}";

        // 2. Submit → PendingEligibilityCheck (congela el RiskScore).
        (await requester.PostAsync($"{baseUrl}/submit", null, cancellationToken))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await GetAsync(requester, baseUrl, cancellationToken)).Status.Should().Be("PendingEligibilityCheck");

        // 3. ConfirmEligibility → PendingManagerApproval (ELEGIBLE gracias al seed; ya no rechaza).
        (await requester.PostAsync($"{baseUrl}/confirm-eligibility", null, cancellationToken))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await GetAsync(requester, baseUrl, cancellationToken)).Status.Should().Be("PendingManagerApproval");

        // 4. ManagerApprove → Approved o PendingSecurityReview según el RiskScore.
        (await approver.PostAsync($"{baseUrl}/manager-approve", null, cancellationToken))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var afterManager = await GetAsync(approver, baseUrl, cancellationToken);
        afterManager.Status.Should().BeOneOf("Approved", "PendingSecurityReview");

        // 4b. Si el riesgo escaló a revisión de seguridad, un revisor distinto la aprueba.
        if (afterManager.Status == "PendingSecurityReview")
        {
            (await security.PostAsync($"{baseUrl}/security-approve", null, cancellationToken))
                .StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        (await GetAsync(security, baseUrl, cancellationToken)).Status.Should().Be("Approved");

        // 5. Execute → Executed (ejecutor distinto del objetivo).
        (await executor.PostAsync($"{baseUrl}/execute", null, cancellationToken))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await GetAsync(executor, baseUrl, cancellationToken)).Status.Should().Be("Executed");

        // 6. Verify → Verified (verificador distinto del ejecutor, del aprobador y del objetivo).
        (await verifier.PostAsync($"{baseUrl}/verify", null, cancellationToken))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var verified = await GetAsync(verifier, baseUrl, cancellationToken);
        verified.Status.Should().Be("Verified");
        verified.ApproverId.Should().NotBeNull();
        verified.ExecutorId.Should().NotBeNull();
        verified.VerifierId.Should().NotBeNull();
    }

    /// <summary>
    /// Borde fail-closed (G-052): el objetivo tiene un <c>RoleMaturityStatus</c> sembrado pero con tiempo
    /// insuficiente en nivel, de modo que la confirmación de elegibilidad RECHAZA y la promoción nunca
    /// avanza (INV-RPR4). Cubre el corte sin depender de la ausencia del dato de madurez.
    /// </summary>
    [Fact]
    public async Task ConfirmEligibility_WithIneligibleMaturity_ShouldRejectAndHalt()
    {
        await ProvisionarActoresAsync(TestContext.Current.CancellationToken);
        var cancellationToken = TestContext.Current.CancellationToken;
        var requester = CreateActorClient(_requesterActor);

        var createResponse = await requester.PostAsJsonAsync(
            "/api/v1/role-promotion-requests",
            new
            {
                TenantId = SeededTenantId,
                TargetUserId = IneligibleTargetUserId,
                CurrentRoleId = IneligibleCurrentRoleId,
                TargetRoleId = IneligibleTargetRoleId,
            },
            cancellationToken);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResponse.Content.ReadFromJsonAsync<CreateRolePromotionRequestResponse>(cancellationToken);
        created.Should().NotBeNull();
        var baseUrl = $"/api/v1/role-promotion-requests/{created!.RolePromotionRequestId}";

        (await requester.PostAsync($"{baseUrl}/submit", null, cancellationToken))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await requester.PostAsync($"{baseUrl}/confirm-eligibility", null, cancellationToken))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var rejected = await GetAsync(requester, baseUrl, cancellationToken);
        rejected.Status.Should().Be("Rejected");
        rejected.DecisionReason.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Create_WhenRequesterIsTargetUser_ShouldFailSegregationOfDuties()
    {
        await ProvisionarActoresAsync(TestContext.Current.CancellationToken);
        var cancellationToken = TestContext.Current.CancellationToken;
        var command = new
        {
            TenantId = SeededTenantId,
            TargetUserId = AuthenticatedRequesterId, // viola SoD: el solicitante no puede promoverse a sí mismo.
            CurrentRoleId,
            TargetRoleId,
        };

        var response = await _client.PostAsJsonAsync("/api/v1/role-promotion-requests", command, cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ManagerApprove_WhenApproverIsRequester_ShouldFailSegregationOfDuties()
    {
        await ProvisionarActoresAsync(TestContext.Current.CancellationToken);
        var cancellationToken = TestContext.Current.CancellationToken;
        var requester = CreateActorClient(_requesterActor);

        var createResponse = await requester.PostAsJsonAsync(
            "/api/v1/role-promotion-requests",
            new { TenantId = SeededTenantId, TargetUserId, CurrentRoleId, TargetRoleId },
            cancellationToken);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResponse.Content.ReadFromJsonAsync<CreateRolePromotionRequestResponse>(cancellationToken);
        var baseUrl = $"/api/v1/role-promotion-requests/{created!.RolePromotionRequestId}";

        (await requester.PostAsync($"{baseUrl}/submit", null, cancellationToken))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await requester.PostAsync($"{baseUrl}/confirm-eligibility", null, cancellationToken))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        // El solicitante intenta aprobar su propia solicitud → violación de SoD (INV-RPR3).
        var approveResponse = await requester.PostAsync($"{baseUrl}/manager-approve", null, cancellationToken);
        approveResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task List_ShouldReturnCreatedRequestScopedByTenant()
    {
        await ProvisionarActoresAsync(TestContext.Current.CancellationToken);
        var cancellationToken = TestContext.Current.CancellationToken;
        var command = new
        {
            TenantId = SeededTenantId,
            TargetUserId,
            CurrentRoleId,
            TargetRoleId,
        };

        var createResponse = await _client.PostAsJsonAsync("/api/v1/role-promotion-requests", command, cancellationToken);
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<CreateRolePromotionRequestResponse>(cancellationToken);
        created.Should().NotBeNull();

        var list = await _client.GetFromJsonAsync<IReadOnlyList<RolePromotionRequestDto>>(
            $"/api/v1/role-promotion-requests?tenantId={SeededTenantId}", cancellationToken);

        list.Should().NotBeNull();
        list!.Should().Contain(r => r.Id == created!.RolePromotionRequestId);
    }

    /// <summary>
    /// G-100: un GET por id inexistente debe resolver 404. El handler devuelve el error de dominio de
    /// «no encontrado» en español (código estable <c>iga.role_promotion_request_not_found</c>); antes el
    /// <c>DomainErrorStatusMapper</c> sólo reconocía el substring en inglés «not found» y lo colapsaba a 400.
    /// Esta prueba recorre el endpoint real y el mapeador real, con lo que verifica la clasificación por
    /// código con independencia del idioma del mensaje.
    /// </summary>
    [Fact]
    public async Task GetById_WhenRequestDoesNotExist_ShouldReturn404()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var missingId = Guid.NewGuid();

        var response = await _client.GetAsync($"/api/v1/role-promotion-requests/{missingId}", cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private static async Task<RolePromotionRequestDto> GetAsync(HttpClient client, string baseUrl, CancellationToken cancellationToken)
    {
        var dto = await client.GetFromJsonAsync<RolePromotionRequestDto>(baseUrl, cancellationToken);
        dto.Should().NotBeNull();
        return dto!;
    }
}
