using Evolith.Ums.Infrastructure.Persistence.Seeders;
using Evolith.Ums.Presentation.IntegrationTest.Infrastructure;

namespace Evolith.Ums.Presentation.IntegrationTest.Authorization;

public sealed class ProfileRestEndpointTests : IClassFixture<UmsApiWebApplicationFactory>
{
    private readonly UmsApiWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public ProfileRestEndpointTests(UmsApiWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
        });
        _client.DefaultRequestHeaders.Add("X-User-Id", TestAuthHandler.AdministradorSembrado);
        _client.DefaultRequestHeaders.Add("X-User-Name", "Integration Tester");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", CoreDevDataSeeder.InternalAdminTenantId);
        // ADR-0071 / FS-26: INTERNAL_ADMIN ya no es management owner; las escrituras acotadas
        // (crear usuario, sucursal, perfil) exigen contexto internal-admin explícito o devuelven
        // AUTH_015 → 400. Se declara el rol de operador de gestión de forma explícita.
        _client.DefaultRequestHeaders.Add("X-Is-Internal-Admin", "true");
    }

    /// <summary>
    /// G-215 — un perfil por (usuario, rol, sucursal) activo.
    ///
    /// <para>Nada lo impedía: cada llamada con los mismos datos creaba OTRO perfil. El
    /// aprovisionamiento del Tablero SDLC, reejecutado, dejó a ocho usuarios con perfiles
    /// duplicados —uno con concesiones y otro vacío, porque la plantilla se asigna a uno solo— y el
    /// selector de perfil se los ofrecía indistinguibles. Elegir el vacío es entrar sin un solo
    /// permiso, con todos los HTTP en 2xx.</para>
    /// </summary>
    [Fact]
    public async Task CreateProfile_Duplicado_Del_Mismo_Rol_Y_Sucursal_Se_Rechaza()
    {
        var ct = TestContext.Current.CancellationToken;
        var tenantId = Guid.Parse(CoreDevDataSeeder.InternalAdminTenantId);
        var userId = await CreateUserAsync(tenantId, ct);
        var systemSuiteId = await GetManagementSystemSuiteIdAsync(tenantId, ct);
        var roleId = await CreateRoleAsync(systemSuiteId, ct);
        var branchId = await CreateBranchAsync(tenantId, ct);

        var primero = await _client.PostAsJsonAsync("/api/v1/profiles",
            new { tenantId, userId, roleId, branchId }, ct);
        primero.StatusCode.Should().Be(HttpStatusCode.Created);

        var segundo = await _client.PostAsJsonAsync("/api/v1/profiles",
            new { tenantId, userId, roleId, branchId }, ct);

        segundo.StatusCode.Should().NotBe(HttpStatusCode.Created,
            because: "el segundo perfil idéntico quedaría sin plantilla y el usuario podría elegirlo");
        var cuerpo = await segundo.Content.ReadAsStringAsync(ct);
        cuerpo.Should().Contain("profile_already_exists_for_role");
    }

    /// <summary>
    /// El mismo rol en OTRA sucursal sí es un perfil distinto: la guarda acota por
    /// (usuario, rol, sucursal), no por (usuario, rol). Sin esta prueba, endurecer la guarda de más
    /// —y romper el multi-sucursal, que es un caso real del negocio— pasaría inadvertido.
    /// </summary>
    [Fact]
    public async Task CreateProfile_Mismo_Rol_En_Otra_Sucursal_Se_Admite()
    {
        var ct = TestContext.Current.CancellationToken;
        var tenantId = Guid.Parse(CoreDevDataSeeder.InternalAdminTenantId);
        var userId = await CreateUserAsync(tenantId, ct);
        var systemSuiteId = await GetManagementSystemSuiteIdAsync(tenantId, ct);
        var roleId = await CreateRoleAsync(systemSuiteId, ct);
        var sucursalA = await CreateBranchAsync(tenantId, ct);
        var sucursalB = await CreateBranchAsync(tenantId, ct);

        var a = await _client.PostAsJsonAsync("/api/v1/profiles",
            new { tenantId, userId, roleId, branchId = sucursalA }, ct);
        a.StatusCode.Should().Be(HttpStatusCode.Created);

        var b = await _client.PostAsJsonAsync("/api/v1/profiles",
            new { tenantId, userId, roleId, branchId = sucursalB }, ct);
        b.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task CreateProfile_Deactivate_Activate_ShouldSucceed()
    {
        var tenantId = Guid.Parse(CoreDevDataSeeder.InternalAdminTenantId);
        var userId = await CreateUserAsync(tenantId, TestContext.Current.CancellationToken);
        var systemSuiteId = await GetManagementSystemSuiteIdAsync(tenantId, TestContext.Current.CancellationToken);
        var roleId = await CreateRoleAsync(systemSuiteId, TestContext.Current.CancellationToken);
        var branchId = await CreateBranchAsync(tenantId, TestContext.Current.CancellationToken);

        var createResponse = await _client.PostAsJsonAsync("/api/v1/profiles", new
        {
            tenantId,
            userId,
            roleId,
            branchId,
        }, TestContext.Current.CancellationToken);

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        using var createdPayload = JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var profileId = createdPayload.RootElement.GetProperty("profileId").GetGuid();

        var getCreatedResponse = await _client.GetAsync($"/api/v1/profiles/{profileId}", TestContext.Current.CancellationToken);
        getCreatedResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        using var createdProfilePayload = JsonDocument.Parse(await getCreatedResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        createdProfilePayload.RootElement.GetProperty("profileId").GetGuid().Should().Be(profileId);
        createdProfilePayload.RootElement.GetProperty("scope").GetString().Should().Be("BranchScoped");
        createdProfilePayload.RootElement.GetProperty("isActive").GetBoolean().Should().BeTrue();

        var deactivateResponse = await _client.PostAsync($"/api/v1/profiles/{profileId}/deactivate", null, TestContext.Current.CancellationToken);
        deactivateResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var getDeactivatedResponse = await _client.GetAsync($"/api/v1/profiles/{profileId}", TestContext.Current.CancellationToken);
        using var deactivatedPayload = JsonDocument.Parse(await getDeactivatedResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        deactivatedPayload.RootElement.GetProperty("isActive").GetBoolean().Should().BeFalse();

        var activateResponse = await _client.PostAsync($"/api/v1/profiles/{profileId}/activate", null, TestContext.Current.CancellationToken);
        activateResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var getActivatedResponse = await _client.GetAsync($"/api/v1/profiles/{profileId}", TestContext.Current.CancellationToken);
        using var activatedPayload = JsonDocument.Parse(await getActivatedResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        activatedPayload.RootElement.GetProperty("isActive").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task GetProfileById_WhenMissing_ShouldReturnNotFound()
    {
        var response = await _client.GetAsync($"/api/v1/profiles/{Guid.NewGuid()}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetAuthGraphPreview_WithValidProfile_ShouldReturn200WithPreviewPayload()
    {
        var tenantId = Guid.Parse(CoreDevDataSeeder.InternalAdminTenantId);
        var userId = await CreateUserAsync(tenantId, TestContext.Current.CancellationToken);
        var systemSuiteId = await GetManagementSystemSuiteIdAsync(tenantId, TestContext.Current.CancellationToken);
        var roleId = await CreateRoleAsync(systemSuiteId, TestContext.Current.CancellationToken);
        var branchId = await CreateBranchAsync(tenantId, TestContext.Current.CancellationToken);

        var createResponse = await _client.PostAsJsonAsync("/api/v1/profiles", new
        {
            tenantId,
            userId,
            roleId,
            branchId,
        }, TestContext.Current.CancellationToken);

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        using var createdPayload = JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var profileId = createdPayload.RootElement.GetProperty("profileId").GetGuid();

        var previewResponse = await _client.GetAsync(
            $"/api/v1/profiles/{profileId}/auth-graph/preview",
            TestContext.Current.CancellationToken);

        previewResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        previewResponse.Headers.Should().ContainKey("X-Preview-Mode");
        previewResponse.Headers.GetValues("X-Preview-Mode").Should().ContainSingle("internal-preview");
        previewResponse.Headers.Contains("X-Request-Id").Should().BeTrue();

        using var payload = JsonDocument.Parse(await previewResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        payload.RootElement.GetProperty("previewMode").GetString().Should().Be("internal-preview");
        payload.RootElement.GetProperty("profileId").GetGuid().Should().Be(profileId);
        payload.RootElement.GetProperty("tenantId").GetGuid().Should().Be(tenantId);
        payload.RootElement.GetProperty("requestId").GetString().Should().NotBeNullOrWhiteSpace();
        payload.RootElement.GetProperty("format").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task GetAuthGraphPreview_WhenProfileIsMissing_ShouldReturnNotFound()
    {
        var response = await _client.GetAsync(
            $"/api/v1/profiles/{Guid.NewGuid()}/auth-graph/preview?format=JSON",
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        payload.RootElement.GetProperty("error").GetString().Should().Be("Profile not found.");
        payload.RootElement.GetProperty("errorId").GetGuid().Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetAuthGraphPreview_WithoutAuthentication_ShouldReturn401()
    {
        var unauthClient = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
        });
        unauthClient.DefaultRequestHeaders.Add("X-Disable-Dev-Auth", "true");

        var response = await unauthClient.GetAsync(
            $"/api/v1/profiles/{Guid.NewGuid()}/auth-graph/preview",
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private async Task<Guid> GetManagementSystemSuiteIdAsync(Guid tenantId, CancellationToken ct)
    {
        var response = await _client.GetAsync($"/api/v1/system-suites?page=1&pageSize=20&tenantId={tenantId}", ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var items = payload.RootElement.GetProperty("items");

        foreach (var item in items.EnumerateArray())
        {
            if (string.Equals(item.GetProperty("code").GetString(), "UMS", StringComparison.OrdinalIgnoreCase))
            {
                return item.GetProperty("systemSuiteId").GetGuid();
            }
        }

        throw new InvalidOperationException("Seeded UMS system suite was not found for the internal admin tenant.");
    }

    private async Task<Guid> CreateRoleAsync(Guid systemSuiteId, CancellationToken ct)
    {
        var roleCode = $"INTEGRATION_ADMIN_{Guid.NewGuid():N}"[..24];
        var response = await _client.PostAsJsonAsync($"/api/v1/system-suites/{systemSuiteId}/roles", new
        {
            code = roleCode,
            value = "Integration Admin",
            description = "Temporary role used by the profile REST integration test.",
            parentRoleId = (Guid?)null,
            hierarchyLevel = 0,
            promotionOrder = 0,
        }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        return payload.RootElement.GetProperty("roleId").GetGuid();
    }

    private async Task<Guid> CreateBranchAsync(Guid tenantId, CancellationToken ct)
    {
        var branchCode = $"INT_{Guid.NewGuid():N}"[..12];
        var response = await _client.PostAsJsonAsync($"/api/v1/tenants/{tenantId}/branches", new
        {
            code = branchCode,
            name = "Integration Branch",
            geofencingMetadata = (string?)null,
        }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        return payload.RootElement.GetProperty("branchId").GetGuid();
    }

    private async Task<Guid> CreateUserAsync(Guid tenantId, CancellationToken ct)
    {
        var email = $"preview.user.{Guid.NewGuid():N}@ums.local";
        var response = await _client.PostAsJsonAsync("/api/v1/user-accounts", new
        {
            tenantId,
            branchId = (Guid?)null,
            email,
            category = "Internal",
            identityReference = $"EMP-{Guid.NewGuid():N}"[..10],
            identityReferenceType = "HrId",
        }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var userId = payload.RootElement.GetProperty("userAccountId").GetGuid();

        var activateResponse = await _client.PostAsync($"/api/v1/user-accounts/{userId}/activate", null, ct);
        activateResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        return userId;
    }

    /// <summary>
    /// [[G-272]] / ADR-UMS-106 — publicar una versión nueva y asignarla NO suma permisos.
    ///
    /// <para>Medido en UAT el 2026-08-17: los perfiles pasaron de 94→188, 124→248 y 407→528, la suma
    /// exacta de las dos versiones. El modelo de permisos dejaba de ser el declarado y, en el caso
    /// peor, la cookie —un claim `scope` por permiso— rebasaba los 4 096 caracteres y el login
    /// respondía 200 sin dejar entrar.</para>
    /// </summary>
    [Fact]
    public async Task AsignarLaVersionSiguiente_NoAcumulaLosPermisosDeLaAnterior()
    {
        var ct = TestContext.Current.CancellationToken;
        var tenantId = Guid.Parse(CoreDevDataSeeder.InternalAdminTenantId);

        var suiteId = await CrearSuiteConNodoAsync(ct);
        var roleId = await CrearRolAsync(suiteId, ct);
        var userId = await CrearUsuarioAsync(tenantId, ct);

        var v1 = await CrearPlantillaPublicadaAsync(tenantId, roleId, suiteId, ct);
        var v2 = await CrearPlantillaPublicadaAsync(tenantId, roleId, suiteId, ct);
        Assert.NotEqual(v1.TemplateId, v2.TemplateId);

        var perfil = await _client.PostAsJsonAsync("/api/v1/profiles",
            new { tenantId, userId, roleId, branchId = (Guid?)null }, ct);
        perfil.StatusCode.Should().Be(HttpStatusCode.Created);
        var profileId = (await perfil.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("profileId").GetGuid();

        (await _client.PostAsync($"/api/v1/profiles/{profileId}/templates/{v1.TemplateId}", null, ct))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        var trasV1 = await ContarPermisosVivosAsync(profileId, ct);
        trasV1.Should().Be(v1.Items);

        (await _client.PostAsync($"/api/v1/profiles/{profileId}/templates/{v2.TemplateId}", null, ct))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        // La versión nueva SUPERSEDE: el perfil queda con los de v2, no con la suma.
        (await ContarPermisosVivosAsync(profileId, ct)).Should().Be(v2.Items);
    }

    /// <summary>ADR-UMS-106 D4: desligar retira lo que la plantilla concedía.</summary>
    [Fact]
    public async Task DesligarLaPlantilla_DejaElPerfilSinSusPermisos()
    {
        var ct = TestContext.Current.CancellationToken;
        var tenantId = Guid.Parse(CoreDevDataSeeder.InternalAdminTenantId);

        var suiteId = await CrearSuiteConNodoAsync(ct);
        var roleId = await CrearRolAsync(suiteId, ct);
        var userId = await CrearUsuarioAsync(tenantId, ct);
        var plantilla = await CrearPlantillaPublicadaAsync(tenantId, roleId, suiteId, ct);

        var perfil = await _client.PostAsJsonAsync("/api/v1/profiles",
            new { tenantId, userId, roleId, branchId = (Guid?)null }, ct);
        var profileId = (await perfil.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("profileId").GetGuid();

        (await _client.PostAsync($"/api/v1/profiles/{profileId}/templates/{plantilla.TemplateId}", null, ct))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ContarPermisosVivosAsync(profileId, ct)).Should().Be(plantilla.Items);

        var desligar = await _client.DeleteAsync($"/api/v1/profiles/{profileId}/templates/{plantilla.TemplateId}", ct);
        desligar.StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await ContarPermisosVivosAsync(profileId, ct)).Should().Be(0);
    }

    private async Task<int> ContarPermisosVivosAsync(Guid profileId, CancellationToken ct)
    {
        var respuesta = await _client.GetAsync($"/api/v1/profiles/{profileId}", ct);
        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await respuesta.Content.ReadFromJsonAsync<JsonElement>(ct);
        return dto.GetProperty("permissions").EnumerateArray().Count(p => p.GetProperty("isActive").GetBoolean());
    }

    private async Task<Guid> CrearSuiteConNodoAsync(CancellationToken ct)
    {
        var sufijo = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        var suite = await _client.PostAsJsonAsync("/api/v1/system-suites", new
        {
            tenantId = Guid.Parse(CoreDevDataSeeder.InternalAdminTenantId),
            code = $"SUP_{sufijo}",
            name = "Suite de superseder",
            description = "aprovisionada por la prueba",
        }, ct);
        suite.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await suite.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("systemSuiteId").GetGuid();
    }

    private async Task<Guid> CrearRolAsync(Guid suiteId, CancellationToken ct)
    {
        var sufijo = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        var rol = await _client.PostAsJsonAsync($"/api/v1/system-suites/{suiteId}/roles", new
        {
            systemSuiteId = suiteId,
            code = $"ROL_{sufijo}",
            value = "Rol de superseder",
            description = "aprovisionado por la prueba",
            parentRoleId = (Guid?)null,
            hierarchyLevel = 0,
            promotionOrder = 0,
        }, ct);
        rol.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await rol.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("roleId").GetGuid();
    }

    private async Task<Guid> CrearUsuarioAsync(Guid tenantId, CancellationToken ct)
    {
        var cuenta = await _client.PostAsJsonAsync("/api/v1/user-accounts", new
        {
            tenantId,
            branchId = (Guid?)null,
            email = $"superseder.{Guid.NewGuid():N}@evolith.example",
            category = "Internal",
            identityReference = (string?)null,
            identityReferenceType = (string?)null,
        }, ct);
        cuenta.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await cuenta.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("userAccountId").GetGuid();
    }

    /// <summary>
    /// Crea y publica una plantilla de la línea (inquilino, rol, suite) con un ítem por acción nueva.
    /// La segunda llamada produce la VERSIÓN SIGUIENTE de la misma línea, que es el caso del gap.
    /// </summary>
    private async Task<(Guid TemplateId, int Items)> CrearPlantillaPublicadaAsync(
        Guid tenantId, Guid roleId, Guid suiteId, CancellationToken ct)
    {
        var plantilla = await _client.PostAsJsonAsync("/api/v1/permission-templates",
            new { tenantId, roleId, systemSuiteId = suiteId }, ct);
        plantilla.StatusCode.Should().Be(HttpStatusCode.Created);
        var templateId = (await plantilla.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("templateId").GetGuid();

        var sufijo = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        var modulo = await _client.PostAsJsonAsync($"/api/v1/system-suites/{suiteId}/modules", new
        {
            systemSuiteId = suiteId, code = $"MOD_{sufijo}", name = "Módulo", description = "x", sortOrder = 1,
        }, ct);
        modulo.StatusCode.Should().Be(HttpStatusCode.Created);
        var moduleId = (await modulo.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("moduleId").GetGuid();
        (await _client.PostAsync($"/api/v1/system-suites/{suiteId}/modules/{moduleId}/activate", null, ct))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var nodo = await _client.PostAsJsonAsync($"/api/v1/system-suites/{suiteId}/modules/{moduleId}/nodes", new
        {
            systemSuiteId = suiteId, moduleId, parentNodeId = (Guid?)null, kind = "Option",
            code = $"NODO_{sufijo}", label = "Nodo", description = "x", sortOrder = 1,
        }, ct);
        nodo.StatusCode.Should().Be(HttpStatusCode.Created);
        var nodeId = (await nodo.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("nodeId").GetGuid();

        var accion = await _client.PostAsJsonAsync($"/api/v1/system-suites/{suiteId}/actions",
            new { systemSuiteId = suiteId, code = $"ACC_{sufijo}", name = "Acción" }, ct);
        accion.StatusCode.Should().Be(HttpStatusCode.Created);
        var actionId = (await accion.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("actionId").GetGuid();

        (await _client.PostAsJsonAsync($"/api/v1/system-suites/{suiteId}/modules/{moduleId}/nodes/{nodeId}/actions",
            new { systemSuiteId = suiteId, moduleId, nodeId, actionCode = $"ACC_{sufijo}" }, ct))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await _client.PostAsJsonAsync($"/api/v1/permission-templates/{templateId}/items", new
        {
            templateId, targetType = "Option", targetId = nodeId, actionId, isAllowed = true, isDenied = false,
        }, ct)).StatusCode.Should().Be(HttpStatusCode.Created);

        (await _client.PostAsync($"/api/v1/permission-templates/{templateId}/publish", null, ct))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        return (templateId, 1);
    }
}
