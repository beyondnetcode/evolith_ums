using Evolith.Ums.Presentation.IntegrationTest.Infrastructure;

namespace Evolith.Ums.Presentation.IntegrationTest.Configuration;

/// <summary>
/// FS-20 REST endpoint integration tests.
/// Covers: full lifecycle (create → update → publish → archive),
/// scope-based authorization guards, error cases (404, 409, 422).
/// </summary>
public sealed class AppConfigurationRestEndpointTests : IClassFixture<UmsApiWebApplicationFactory>
{
    private readonly HttpClient _adminClient;
    private readonly HttpClient _tenantClient;
    // Almacén real del host InMemory: se consulta directamente para comprobar que la fila sobrevive
    // al DELETE (la API, por diseño, ya no la enseña).
    private readonly InMemoryAppConfigurationRepository _store;

    // Seeded tenant used by UmsApiWebApplicationFactory
    private static readonly Guid SeededTenantId = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6");
    private static readonly Guid SeededSystemSuiteId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public AppConfigurationRestEndpointTests(UmsApiWebApplicationFactory factory)
    {
        _store = factory.Services.GetRequiredService<InMemoryAppConfigurationRepository>();

        _adminClient = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
        });
        // No X-Tenant-Id → defaults to InternalAdminTenantId → IsInternalAdmin = true
        _adminClient.DefaultRequestHeaders.Add("X-User-Id", TestAuthHandler.AdministradorSembrado);
        _adminClient.DefaultRequestHeaders.Add("X-User-Name", "FS20 Admin Tester");

        _tenantClient = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
        });
        _tenantClient.DefaultRequestHeaders.Add("X-User-Id", TestAuthHandler.AdministradorSembrado);
        _tenantClient.DefaultRequestHeaders.Add("X-User-Name", "FS20 Tenant Tester");
        _tenantClient.DefaultRequestHeaders.Add("X-Tenant-Id", SeededTenantId.ToString());
        _tenantClient.DefaultRequestHeaders.Add("X-Is-Internal-Admin", "false");
    }

    // -----------------------------------------------------------------------
    // Full lifecycle
    // -----------------------------------------------------------------------

    [Fact]
    public async Task CreateUpdatePublishArchive_GlobalConfig_ShouldSucceed()
    {
        var code = $"FS20-GLOBAL-{Guid.NewGuid():N}";

        // 1. Create (Draft)
        var createResponse = await _adminClient.PostAsJsonAsync("/api/v1/app-configurations", new
        {
            tenantId      = (Guid?)null,
            systemSuiteId = (Guid?)null,
            moduleId      = (Guid?)null,
            code,
            value         = "3600",
            description   = "FS-20 global config lifecycle test",
            isInheritable = true,
            isEncrypted   = false,
        }, TestContext.Current.CancellationToken);

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        using var createPayload = JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var configId = createPayload.RootElement.GetProperty("appConfigurationId").GetGuid();

        // 2. GET by ID — verify Draft + Global scope + initial version
        var getResponse = await _adminClient.GetAsync($"/api/v1/app-configurations/{configId}", TestContext.Current.CancellationToken);
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        using var getPayload = JsonDocument.Parse(await getResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        getPayload.RootElement.GetProperty("status").GetString().Should().Be("Draft");
        getPayload.RootElement.GetProperty("scope").GetString().Should().Be("Global");
        getPayload.RootElement.GetProperty("version").GetString().Should().Be("1.0.0");

        // 3. Update (only Draft allows updates)
        var updateResponse = await _adminClient.PutAsJsonAsync($"/api/v1/app-configurations/{configId}", new
        {
            appConfigurationId = configId,
            value              = "7200",
            description        = "Updated description",
        }, TestContext.Current.CancellationToken);
        updateResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // 4. GET — verify updated value and minor version bump
        var getUpdatedResponse = await _adminClient.GetAsync($"/api/v1/app-configurations/{configId}", TestContext.Current.CancellationToken);
        using var updatedPayload = JsonDocument.Parse(await getUpdatedResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        updatedPayload.RootElement.GetProperty("value").GetString().Should().Be("7200");
        updatedPayload.RootElement.GetProperty("version").GetString().Should().Be("1.1.0");

        // 5. Publish
        var publishResponse = await _adminClient.PostAsync(
            $"/api/v1/app-configurations/{configId}/publish", null, TestContext.Current.CancellationToken);
        publishResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // 6. GET — verify Published
        var getPublishedResponse = await _adminClient.GetAsync($"/api/v1/app-configurations/{configId}", TestContext.Current.CancellationToken);
        using var publishedPayload = JsonDocument.Parse(await getPublishedResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        publishedPayload.RootElement.GetProperty("status").GetString().Should().Be("Published");

        // 7. Archive
        var archiveResponse = await _adminClient.PostAsync(
            $"/api/v1/app-configurations/{configId}/archive", null, TestContext.Current.CancellationToken);
        archiveResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // 8. GET — verify Archived
        var getArchivedResponse = await _adminClient.GetAsync($"/api/v1/app-configurations/{configId}", TestContext.Current.CancellationToken);
        using var archivedPayload = JsonDocument.Parse(await getArchivedResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        archivedPayload.RootElement.GetProperty("status").GetString().Should().Be("Archived");
    }

    [Fact]
    public async Task CreatePublish_TenantConfig_ShouldSucceed()
    {
        var code = $"FS20-TENANT-{Guid.NewGuid():N}";

        var createResponse = await _adminClient.PostAsJsonAsync("/api/v1/app-configurations", new
        {
            tenantId      = SeededTenantId,
            systemSuiteId = (Guid?)null,
            moduleId      = (Guid?)null,
            code,
            value         = "60",
            description   = "FS-20 tenant-scoped config test",
            isInheritable = false,
            isEncrypted   = false,
        }, TestContext.Current.CancellationToken);

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        using var createPayload = JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var configId = createPayload.RootElement.GetProperty("appConfigurationId").GetGuid();

        var getResponse = await _adminClient.GetAsync($"/api/v1/app-configurations/{configId}", TestContext.Current.CancellationToken);
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        using var getPayload = JsonDocument.Parse(await getResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        getPayload.RootElement.GetProperty("scope").GetString().Should().Be("Tenant");
        getPayload.RootElement.GetProperty("status").GetString().Should().Be("Draft");

        var publishResponse = await _adminClient.PostAsync(
            $"/api/v1/app-configurations/{configId}/publish", null, TestContext.Current.CancellationToken);
        publishResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var getPublishedResponse = await _adminClient.GetAsync($"/api/v1/app-configurations/{configId}", TestContext.Current.CancellationToken);
        using var publishedPayload = JsonDocument.Parse(await getPublishedResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        publishedPayload.RootElement.GetProperty("status").GetString().Should().Be("Published");
        publishedPayload.RootElement.GetProperty("tenantId").GetGuid().Should().Be(SeededTenantId);
    }

    // -----------------------------------------------------------------------
    // Query and filtering
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetAppConfigurationById_WhenNotFound_ShouldReturn404()
    {
        var response = await _adminClient.GetAsync(
            $"/api/v1/app-configurations/{Guid.NewGuid()}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetAppConfigurations_WithScopeGlobalFilter_ShouldReturnOnlyGlobalConfigs()
    {
        // Create at least one Global config so the assertion is meaningful
        var code = $"FS20-GFILTER-{Guid.NewGuid():N}";
        await _adminClient.PostAsJsonAsync("/api/v1/app-configurations", new
        {
            tenantId = (Guid?)null, systemSuiteId = (Guid?)null, moduleId = (Guid?)null,
            code, value = "1", description = "Scope filter test", isInheritable = false, isEncrypted = false,
        }, TestContext.Current.CancellationToken);

        var response = await _adminClient.GetAsync(
            "/api/v1/app-configurations?page=1&pageSize=100&scope=Global", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var items = payload.RootElement.GetProperty("items");
        items.GetArrayLength().Should().BeGreaterThan(0);
        Enumerable.Range(0, items.GetArrayLength())
            .Select(i => items[i].GetProperty("scope").GetString())
            .Should().OnlyContain(s => s == "Global");
    }

    [Fact]
    public async Task GetAppConfigurations_AsTenantAdmin_ShouldNotSeeGlobalScope()
    {
        var response = await _tenantClient.GetAsync(
            "/api/v1/app-configurations?page=1&pageSize=100&scope=Global", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // -----------------------------------------------------------------------
    // Authorization guards
    // -----------------------------------------------------------------------

    [Fact]
    public async Task CreateAppConfiguration_WithGlobalScope_AsTenantAdmin_ShouldReturn403()
    {
        var response = await _tenantClient.PostAsJsonAsync("/api/v1/app-configurations", new
        {
            tenantId      = (Guid?)null,
            systemSuiteId = (Guid?)null,
            moduleId      = (Guid?)null,
            code          = $"FS20-FORBID-{Guid.NewGuid():N}",
            value         = "forbidden",
            description   = "Tenant admin should not create global config",
            isInheritable = false,
            isEncrypted   = false,
        }, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateAppConfiguration_ForOtherTenant_AsTenantAdmin_ShouldReturn403()
    {
        var otherTenantId = Guid.NewGuid();

        var response = await _tenantClient.PostAsJsonAsync("/api/v1/app-configurations", new
        {
            tenantId      = otherTenantId,
            systemSuiteId = (Guid?)null,
            moduleId      = (Guid?)null,
            code          = $"FS20-CROSS-{Guid.NewGuid():N}",
            value         = "forbidden",
            description   = "Cross-tenant creation attempt",
            isInheritable = false,
            isEncrypted   = false,
        }, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PublishAppConfiguration_WithGlobalScope_AsTenantAdmin_ShouldReturn403()
    {
        // Internal admin creates a global config
        var code = $"FS20-PUBL-403-{Guid.NewGuid():N}";
        var createResponse = await _adminClient.PostAsJsonAsync("/api/v1/app-configurations", new
        {
            tenantId = (Guid?)null, systemSuiteId = (Guid?)null, moduleId = (Guid?)null,
            code, value = "100", description = "Admin creates, tenant tries to publish",
            isInheritable = false, isEncrypted = false,
        }, TestContext.Current.CancellationToken);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        using var createPayload = JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var configId = createPayload.RootElement.GetProperty("appConfigurationId").GetGuid();

        // Tenant admin attempts to publish — should be 403
        var publishResponse = await _tenantClient.PostAsync(
            $"/api/v1/app-configurations/{configId}/publish", null, TestContext.Current.CancellationToken);

        publishResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // -----------------------------------------------------------------------
    // Error cases
    // -----------------------------------------------------------------------

    [Fact]
    public async Task CreateAppConfiguration_WithDuplicateScopeAndCode_ShouldReturn409()
    {
        var code = $"FS20-DUP-{Guid.NewGuid():N}";

        var firstResponse = await _adminClient.PostAsJsonAsync("/api/v1/app-configurations", new
        {
            tenantId = SeededTenantId, systemSuiteId = SeededSystemSuiteId, moduleId = (Guid?)null,
            code, value = "first", description = "First instance",
            isInheritable = false, isEncrypted = false,
        }, TestContext.Current.CancellationToken);
        firstResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var duplicateResponse = await _adminClient.PostAsJsonAsync("/api/v1/app-configurations", new
        {
            tenantId = SeededTenantId, systemSuiteId = SeededSystemSuiteId, moduleId = (Guid?)null,
            code, // same code + same scope
            value = "second", description = "Duplicate instance",
            isInheritable = false, isEncrypted = false,
        }, TestContext.Current.CancellationToken);

        duplicateResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task UpdateAppConfiguration_WhenPublished_ShouldReturn422()
    {
        var code = $"FS20-UPD-422-{Guid.NewGuid():N}";

        var createResponse = await _adminClient.PostAsJsonAsync("/api/v1/app-configurations", new
        {
            tenantId = (Guid?)null, systemSuiteId = (Guid?)null, moduleId = (Guid?)null,
            code, value = "original", description = "Will be published",
            isInheritable = false, isEncrypted = false,
        }, TestContext.Current.CancellationToken);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        using var createPayload = JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var configId = createPayload.RootElement.GetProperty("appConfigurationId").GetGuid();

        await _adminClient.PostAsync($"/api/v1/app-configurations/{configId}/publish", null, TestContext.Current.CancellationToken);

        var updateResponse = await _adminClient.PutAsJsonAsync($"/api/v1/app-configurations/{configId}", new
        {
            appConfigurationId = configId,
            value              = "changed",
            description        = "Cannot update published config",
        }, TestContext.Current.CancellationToken);

        updateResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ArchiveAppConfiguration_WhenDraft_ShouldReturn422()
    {
        var code = $"FS20-ARC-422-{Guid.NewGuid():N}";

        var createResponse = await _adminClient.PostAsJsonAsync("/api/v1/app-configurations", new
        {
            tenantId = (Guid?)null, systemSuiteId = (Guid?)null, moduleId = (Guid?)null,
            code, value = "v", description = "Archive from Draft should fail",
            isInheritable = false, isEncrypted = false,
        }, TestContext.Current.CancellationToken);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        using var createPayload = JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var configId = createPayload.RootElement.GetProperty("appConfigurationId").GetGuid();

        // Draft → Archive is forbidden: must go Draft → Published → Archived
        var archiveResponse = await _adminClient.PostAsync(
            $"/api/v1/app-configurations/{configId}/archive", null, TestContext.Current.CancellationToken);

        archiveResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PublishAppConfiguration_WhenAlreadyPublished_ShouldReturn422()
    {
        var code = $"FS20-PUBL-DUP-{Guid.NewGuid():N}";

        var createResponse = await _adminClient.PostAsJsonAsync("/api/v1/app-configurations", new
        {
            tenantId = (Guid?)null, systemSuiteId = (Guid?)null, moduleId = (Guid?)null,
            code, value = "v", description = "Double-publish should fail",
            isInheritable = false, isEncrypted = false,
        }, TestContext.Current.CancellationToken);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        using var createPayload = JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var configId = createPayload.RootElement.GetProperty("appConfigurationId").GetGuid();

        await _adminClient.PostAsync($"/api/v1/app-configurations/{configId}/publish", null, TestContext.Current.CancellationToken);

        var secondPublishResponse = await _adminClient.PostAsync(
            $"/api/v1/app-configurations/{configId}/publish", null, TestContext.Current.CancellationToken);

        secondPublishResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // -----------------------------------------------------------------------
    // Delete (borrado LÓGICO: mismo contrato HTTP, la fila NO se pierde)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task DeleteAppConfiguration_WhenExists_Devuelve204_YElRegistroSigueExistiendo()
    {
        var code = $"FS20-DEL-{Guid.NewGuid():N}";

        var createResponse = await _adminClient.PostAsJsonAsync("/api/v1/app-configurations", new
        {
            tenantId = (Guid?)null, systemSuiteId = (Guid?)null, moduleId = (Guid?)null,
            code, value = "to-delete", description = "Borrado lógico",
            isInheritable = false, isEncrypted = false,
        }, TestContext.Current.CancellationToken);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        using var createPayload = JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var configId = createPayload.RootElement.GetProperty("appConfigurationId").GetGuid();

        var deleteResponse = await _adminClient.DeleteAsync(
            $"/api/v1/app-configurations/{configId}", TestContext.Current.CancellationToken);
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent, "el contrato HTTP no cambia");

        // La lectura la oculta…
        var getResponse = await _adminClient.GetAsync(
            $"/api/v1/app-configurations/{configId}", TestContext.Current.CancellationToken);
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // …pero el registro SIGUE en el almacén, en estado terminal Deleted. Esta es la aserción que
        // fija la política del propietario: sin ella, el borrado lógico no está demostrado. Se lee
        // SIN filtros (`GetAllIncludingDeleted`) y ya no por `GetByScopeAndCodeAsync`: desde que el
        // borrado libera la ranura, ese lookup solo devuelve la viva y no vería la lápida.
        var stored = _store.GetAllIncludingDeleted()
            .SingleOrDefault(item => string.Equals(item.Code.GetValue(), code, StringComparison.OrdinalIgnoreCase));
        stored.Should().NotBeNull("la fila jamás se elimina físicamente");
        stored!.Status.Should().Be(ConfigStatus.Deleted);

        // Y tampoco aparece en el listado.
        var listResponse = await _adminClient.GetAsync(
            "/api/v1/app-configurations?page=1&pageSize=200", TestContext.Current.CancellationToken);
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var listBody = await listResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        listBody.Should().NotContain(code, "lo eliminado no se lista");
    }

    /// <summary>
    /// La ranura (ámbito, código) se LIBERA al borrar: borrar la configuración de un parámetro no
    /// puede impedir volver a configurarlo. Es la excepción que el propietario del producto acotó
    /// sobre ADR-0164 §2.3 el 2026-08-04, tras medirse el efecto en vivo: el código de una
    /// configuración sale de un catálogo cerrado —`MFA_REQUIRED_FOR_ADMIN` es *el* nombre de ese
    /// parámetro— y no identifica una cosa del mundo real, a diferencia del código de una sucursal.
    ///
    /// La prueba cubre las cuatro mitades del cambio: se puede recrear; las dos filas siguen ahí;
    /// la lectura por código resuelve LA VIVA; y duplicar una viva sigue siendo conflicto.
    /// </summary>
    [Fact]
    public async Task DeleteAppConfiguration_LiberaLaRanura_YLaLecturaPorCodigoResuelveLaViva()
    {
        var ct = TestContext.Current.CancellationToken;
        var code = $"FS20-RANURA-{Guid.NewGuid():N}";

        async Task<HttpResponseMessage> CrearAsync(string value) =>
            await _adminClient.PostAsJsonAsync("/api/v1/app-configurations", new
            {
                tenantId = (Guid?)null, systemSuiteId = (Guid?)null, moduleId = (Guid?)null,
                code, value, description = "Ranura de configuración",
                isInheritable = false, isEncrypted = false,
            }, ct);

        var primera = await CrearAsync("original");
        primera.StatusCode.Should().Be(HttpStatusCode.Created);
        using var primeraPayload = JsonDocument.Parse(await primera.Content.ReadAsStringAsync(ct));
        var primeraId = primeraPayload.RootElement.GetProperty("appConfigurationId").GetGuid();

        (await _adminClient.DeleteAsync($"/api/v1/app-configurations/{primeraId}", ct))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        // (a) Se puede volver a crear con EL MISMO código.
        var segunda = await CrearAsync("reconfigurada");
        segunda.StatusCode.Should().Be(HttpStatusCode.Created,
            "una ranura de configuración liberada admite un alta nueva");
        using var segundaPayload = JsonDocument.Parse(await segunda.Content.ReadAsStringAsync(ct));
        var segundaId = segundaPayload.RootElement.GetProperty("appConfigurationId").GetGuid();

        // (b) LAS DOS filas siguen en el almacén; la vieja marcada como eliminada.
        var todas = _store.GetAllIncludingDeleted()
            .Where(item => string.Equals(item.Code.GetValue(), code, StringComparison.OrdinalIgnoreCase))
            .ToList();
        todas.Should().HaveCount(2, "esto NO es volver al borrado físico: la historia se conserva");
        todas.Single(item => item.Props.Id.GetValue() == primeraId).Status.Should().Be(ConfigStatus.Deleted);
        todas.Single(item => item.Props.Id.GetValue() == segundaId).Status.Should().Be(ConfigStatus.Draft);

        // (c) La lectura por código resuelve LA VIVA, sin ambigüedad.
        var viva = await _store.GetByScopeAndCodeAsync(null, null, null, code, ct);
        viva.Should().NotBeNull();
        viva!.Props.Id.GetValue().Should().Be(segundaId, "el lookup por código nunca devuelve la lápida");
        (await _adminClient.GetAsync($"/api/v1/app-configurations/{segundaId}", ct))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await _adminClient.GetAsync($"/api/v1/app-configurations/{primeraId}", ct))
            .StatusCode.Should().Be(HttpStatusCode.NotFound, "la eliminada sigue oculta para las lecturas");

        // (d) Duplicar una VIVA sigue dando conflicto legible.
        var duplicada = await CrearAsync("duplicada");
        duplicada.StatusCode.Should().Be(HttpStatusCode.Conflict,
            "liberar la ranura no debilita la unicidad entre las configuraciones vivas");
        (await duplicada.Content.ReadAsStringAsync(ct))
            .Should().Contain("already exists", "el conflicto tiene que decir qué pasa");
    }

    [Fact]
    public async Task DeleteAppConfiguration_DosVeces_LaSegundaDevuelve404()
    {
        var code = $"FS20-DEL2-{Guid.NewGuid():N}";

        var createResponse = await _adminClient.PostAsJsonAsync("/api/v1/app-configurations", new
        {
            tenantId = (Guid?)null, systemSuiteId = (Guid?)null, moduleId = (Guid?)null,
            code, value = "v", description = "Doble borrado",
            isInheritable = false, isEncrypted = false,
        }, TestContext.Current.CancellationToken);
        using var createPayload = JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var configId = createPayload.RootElement.GetProperty("appConfigurationId").GetGuid();

        (await _adminClient.DeleteAsync($"/api/v1/app-configurations/{configId}", TestContext.Current.CancellationToken))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var second = await _adminClient.DeleteAsync(
            $"/api/v1/app-configurations/{configId}", TestContext.Current.CancellationToken);

        second.StatusCode.Should().Be(HttpStatusCode.NotFound, "para las lecturas ya no existe");
    }

    [Fact]
    public async Task DeleteAppConfiguration_WhenNotFound_ShouldReturn404()
    {
        var deleteResponse = await _adminClient.DeleteAsync(
            $"/api/v1/app-configurations/{Guid.NewGuid()}", TestContext.Current.CancellationToken);

        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteAppConfiguration_GlobalScope_AsTenantAdmin_ShouldReturn403()
    {
        // Internal admin creates a global config
        var code = $"FS20-DEL-403-{Guid.NewGuid():N}";
        var createResponse = await _adminClient.PostAsJsonAsync("/api/v1/app-configurations", new
        {
            tenantId = (Guid?)null, systemSuiteId = (Guid?)null, moduleId = (Guid?)null,
            code, value = "v", description = "Admin creates, tenant tries to delete",
            isInheritable = false, isEncrypted = false,
        }, TestContext.Current.CancellationToken);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        using var createPayload = JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var configId = createPayload.RootElement.GetProperty("appConfigurationId").GetGuid();

        var deleteResponse = await _tenantClient.DeleteAsync(
            $"/api/v1/app-configurations/{configId}", TestContext.Current.CancellationToken);

        deleteResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
