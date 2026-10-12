using Microsoft.EntityFrameworkCore;
using Evolith.Ums.Presentation.IntegrationTest.Infrastructure;

namespace Evolith.Ums.Presentation.IntegrationTest.Authorization;

/// <summary>
/// Política del propietario: SOLO existe borrado lógico. Estas pruebas fijan sobre PostgreSQL real
/// (Testcontainers) las cuatro afirmaciones que la sostienen para <c>PermissionTemplate</c>:
///
///   a. El contrato HTTP no cambia: DELETE sigue devolviendo 204.
///   b. La FILA SIGUE EN LA BASE tras el borrado —con el estado terminal Deleted y sus ítems—. Es la
///      prueba que fija la política: sin ella el cambio no está demostrado.
///   c. El GET posterior devuelve 404 y la plantilla no aparece en el listado.
///   d. Con una referencia VIVA (un perfil activo que la usa) el DELETE devuelve 409 estructurado.
///   e. Con esa referencia ya eliminada lógicamente (perfil desactivado), el borrado sí procede.
///
/// Antes de este cambio el repositorio hacía <c>dbContext.PermissionTemplates.Remove(record)</c>, que
/// además arrastraba por cascada los ítems de la plantilla: se perdía sin remedio el rastro de qué
/// concesiones había otorgado, justo lo que el negocio consulta hacia atrás.
/// </summary>
[Collection("PostgreSql")]
public sealed class PermissionTemplateSoftDeleteTests : IntegrationTestBase
{
    /// <summary>Id de <c>TemplateStatus.Deleted</c> tal y como se persiste en la columna StatusId.</summary>
    private const int DeletedStatusId = 4;

    public PermissionTemplateSoftDeleteTests(PostgreSqlContainerFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Delete_PlantillaSinReferencias_Responde204_DejaLaFilaEnLaBase_YOcultaLasLecturas()
    {
        if (!Fixture.IsAvailable) Assert.Skip("Docker es necesario para las pruebas de integración PostgreSQL.");
        var ct = TestContext.Current.CancellationToken;

        var contexto = await ProvisionarSuiteYRolAsync(ct);
        var templateId = await CrearPlantillaConItemAsync(contexto, ct);

        // (a) El contrato no cambia: 204 No Content, igual que con el borrado físico.
        var deleteResponse = await Client.DeleteAsync($"/api/v1/permission-templates/{templateId}", ct);
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent,
            await deleteResponse.Content.ReadAsStringAsync(ct));

        // (b) LA FILA SIGUE EN LA BASE. Se consulta con un contexto propio, no por el API, porque el
        // API es precisamente lo que ahora la oculta: la evidencia tiene que venir del almacenamiento.
        await using var db = CrearContextoDirecto();
        var fila = await db.PermissionTemplates
            .Include(x => x.Items)
            .SingleOrDefaultAsync(x => x.Id == templateId, ct);

        fila.Should().NotBeNull("el borrado es LÓGICO: la fila no puede desaparecer de la tabla");
        fila!.StatusId.Should().Be(DeletedStatusId, "la eliminación se registra como estado terminal Deleted");
        fila.Items.Should().NotBeEmpty("los ítems son el rastro de las concesiones otorgadas y sobreviven al borrado");
        fila.UpdatedBy.Should().NotBeNullOrWhiteSpace("la auditoría debe registrar quién eliminó la plantilla");

        // (c) Las lecturas la ocultan: por id da 404 y no aparece en el listado.
        var getResponse = await Client.GetAsync($"/api/v1/permission-templates/{templateId}", ct);
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var listado = await Client.GetAsync(
            $"/api/v1/permission-templates?page=1&pageSize=200&tenantId={contexto.TenantId}&systemSuiteId={contexto.SystemSuiteId}", ct);
        listado.StatusCode.Should().Be(HttpStatusCode.OK);

        using var payload = JsonDocument.Parse(await listado.Content.ReadAsStringAsync(ct));
        payload.RootElement.GetProperty("items").EnumerateArray()
            .Should().NotContain(i => i.GetProperty("templateId").GetGuid() == templateId,
                "una plantilla eliminada lógicamente no puede seguir apareciendo en el catálogo");
    }

    [Fact]
    public async Task Delete_SegundoIntento_Responde404_PorqueLasLecturasOcultanLoEliminado()
    {
        if (!Fixture.IsAvailable) Assert.Skip("Docker es necesario para las pruebas de integración PostgreSQL.");
        var ct = TestContext.Current.CancellationToken;

        var contexto = await ProvisionarSuiteYRolAsync(ct);
        var templateId = await CrearPlantillaConItemAsync(contexto, ct);

        (await Client.DeleteAsync($"/api/v1/permission-templates/{templateId}", ct))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var segundo = await Client.DeleteAsync($"/api/v1/permission-templates/{templateId}", ct);

        segundo.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "el borrado no es idempotente en silencio: lo ya eliminado simplemente no existe para el API");
    }

    [Fact]
    public async Task Delete_ConPerfilVivo_Responde409_YTrasDesactivarlo_Permite()
    {
        if (!Fixture.IsAvailable) Assert.Skip("Docker es necesario para las pruebas de integración PostgreSQL.");
        var ct = TestContext.Current.CancellationToken;

        var contexto = await ProvisionarSuiteYRolAsync(ct);
        var templateId = await CrearPlantillaConItemAsync(contexto, ct);

        // Solo una plantilla PUBLICADA puede asignarse a un perfil (Profile.AssignTemplate).
        (await Client.PostAsync($"/api/v1/permission-templates/{templateId}/publish", null, ct))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var profileId = await CrearPerfilActivoAsync(contexto, ct);
        (await Client.PostAsync($"/api/v1/profiles/{profileId}/templates/{templateId}", null, ct))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        // (d) Referencia VIVA → 409 con las dependencias que bloquean, no un 500 ni un borrado a
        // traición que dejaría al perfil apuntando a la nada.
        var bloqueado = await Client.DeleteAsync($"/api/v1/permission-templates/{templateId}", ct);
        bloqueado.StatusCode.Should().Be(HttpStatusCode.Conflict,
            await bloqueado.Content.ReadAsStringAsync(ct));

        using var error = JsonDocument.Parse(await bloqueado.Content.ReadAsStringAsync(ct));
        error.RootElement.GetProperty("errorCode").GetString().Should().Be("TEMPLATE_HAS_ACTIVE_PROFILES");
        var deps = error.RootElement.GetProperty("blockingDependencies");
        deps.GetArrayLength().Should().BeGreaterThan(0);
        deps[0].GetProperty("entityType").GetString().Should().Be("Profile");
        deps[0].GetProperty("count").GetInt32().Should().BeGreaterThan(0);

        // La plantilla sigue viva: el rechazo es previo a cualquier escritura.
        (await Client.GetAsync($"/api/v1/permission-templates/{templateId}", ct))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        // (e) Se elimina la referencia (perfil desactivado) y ahora el borrado sí procede. La plantilla
        // se deprecia primero porque una PUBLICADA está en uso por definición: deprecar es la retirada
        // explícita de la oferta y es lo que habilita el borrado.
        (await Client.PostAsync($"/api/v1/profiles/{profileId}/deactivate", null, ct))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await Client.PostAsync($"/api/v1/permission-templates/{templateId}/deprecate", null, ct))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var permitido = await Client.DeleteAsync($"/api/v1/permission-templates/{templateId}", ct);
        permitido.StatusCode.Should().Be(HttpStatusCode.NoContent,
            await permitido.Content.ReadAsStringAsync(ct));

        // Y de nuevo: la fila sigue en la base, ahora en estado terminal.
        await using var db = CrearContextoDirecto();
        var fila = await db.PermissionTemplates.SingleOrDefaultAsync(x => x.Id == templateId, ct);
        fila.Should().NotBeNull();
        fila!.StatusId.Should().Be(DeletedStatusId);
    }

    [Fact]
    public async Task Delete_NoLiberaLaVersionParaUnAltaPosterior()
    {
        if (!Fixture.IsAvailable) Assert.Skip("Docker es necesario para las pruebas de integración PostgreSQL.");
        var ct = TestContext.Current.CancellationToken;

        // G-140 revisitado bajo borrado lógico: la fila eliminada SIGUE ocupando su versión en el
        // índice único IX_PermissionTemplates_TenantId_RoleId_SystemSuiteId_Version. Si el cálculo de
        // la versión siguiente ignorase las eliminadas, el alta posterior reutilizaría 0.1.0 y chocaría
        // con 23505. Por eso GetByTenantRoleSuiteAsync es la ÚNICA lectura que no filtra.
        var contexto = await ProvisionarSuiteYRolAsync(ct);
        var primera = await CrearPlantillaConItemAsync(contexto, ct);

        (await Client.DeleteAsync($"/api/v1/permission-templates/{primera}", ct))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var segunda = await Client.PostAsJsonAsync("/api/v1/permission-templates", new
        {
            tenantId = contexto.TenantId,
            roleId = contexto.RoleId,
            systemSuiteId = contexto.SystemSuiteId,
        }, ct);

        segunda.StatusCode.Should().Be(HttpStatusCode.Created, await segunda.Content.ReadAsStringAsync(ct));

        using var payload = JsonDocument.Parse(await segunda.Content.ReadAsStringAsync(ct));
        var segundaId = payload.RootElement.GetProperty("templateId").GetGuid();

        var get = await Client.GetAsync($"/api/v1/permission-templates/{segundaId}", ct);
        get.StatusCode.Should().Be(HttpStatusCode.OK);
        using var detalle = JsonDocument.Parse(await get.Content.ReadAsStringAsync(ct));
        detalle.RootElement.GetProperty("version").GetString().Should().Be("0.2.0",
            "la versión de la plantilla eliminada sigue ocupada en el índice único");
    }

    [Fact]
    public async Task CargadorDelTablero_BorraLasPlantillasDuplicadas_YEnLaReejecucionYaNoLasVe()
    {
        if (!Fixture.IsAvailable) Assert.Skip("Docker es necesario para las pruebas de integración PostgreSQL.");
        var ct = TestContext.Current.CancellationToken;

        // Reproduce la secuencia EXACTA de src/provisioning/cargar-en-ums.mjs (paso 7):
        //   1. lee el catálogo con GET /permission-templates?...&systemSuiteId=…
        //   2. agrupa por rol, se queda con la canónica (menor versión) y borra las sobrantes con
        //      DELETE /permission-templates/{id}, contando como FALLO cualquier respuesta no-ok
        //   3. en la siguiente pasada vuelve a leer el catálogo
        // El borrado lógico tiene que bastarle: la plantilla deja de estorbar porque desaparece del
        // catálogo. Si siguiera apareciendo, el cargador la trataría otra vez como sobrante, el
        // segundo DELETE daría 404 y sumaría un fallo en cada reejecución.
        var contexto = await ProvisionarSuiteYRolAsync(ct);
        var canonica = await CrearPlantillaConItemAsync(contexto, ct);
        var duplicada = await CrearPlantillaConItemAsync(contexto, ct);

        var primeraLectura = await LeerCatalogoDelCargadorAsync(contexto, ct);
        primeraLectura.Should().Contain(canonica).And.Contain(duplicada,
            "el alta repetida genera una versión más, que es justo el duplicado que el cargador limpia");

        var borrado = await Client.DeleteAsync($"/api/v1/permission-templates/{duplicada}", ct);
        borrado.IsSuccessStatusCode.Should().BeTrue(
            "el cargador solo mira `r.ok`; cualquier otra cosa cuenta como fallo de carga");

        var segundaLectura = await LeerCatalogoDelCargadorAsync(contexto, ct);
        segundaLectura.Should().Contain(canonica);
        segundaLectura.Should().NotContain(duplicada,
            "en la reejecución la duplicada ya no aparece, así que el cargador no vuelve a intentar borrarla");
    }

    /// <summary>Misma consulta que hace el cargador del Tablero para construir su catálogo.</summary>
    private async Task<List<Guid>> LeerCatalogoDelCargadorAsync(ContextoDePrueba contexto, CancellationToken ct)
    {
        var response = await Client.GetAsync(
            $"/api/v1/permission-templates?page=1&pageSize=200&tenantId={contexto.TenantId}&systemSuiteId={contexto.SystemSuiteId}", ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        return payload.RootElement.GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("templateId").GetGuid())
            .ToList();
    }

    // ── Utilidades de aprovisionamiento ─────────────────────────────────────

    private sealed record ContextoDePrueba(Guid TenantId, Guid SystemSuiteId, Guid RoleId);

    private async Task<ContextoDePrueba> ProvisionarSuiteYRolAsync(CancellationToken ct)
    {
        // El host PostgreSQL corre con SeedDevData=false: no hay ningún inquilino sembrado, así que
        // cada prueba levanta el suyo (mismo patrón que PostgreSqlAuthorizationPersistenceTests).
        var code = $"PTSD{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        var tenantResponse = await Client.PostAsJsonAsync("/api/v1/tenants", new
        {
            code,
            name = $"Inquilino de borrado lógico {code}",
            type = "CLIENT",
            isManagementOwner = false,
        }, ct);
        tenantResponse.StatusCode.Should().Be(HttpStatusCode.Created, await tenantResponse.Content.ReadAsStringAsync(ct));
        var tenantId = Guid.Parse(tenantResponse.Headers.Location!.ToString().Split('/')[^1]);

        // ADR-0077: aprovisionar recursos de un inquilino CLIENT es una operación ON-BEHALF que solo
        // ejerce el internal-admin; sin la cabecera, TenantScopePolicy devuelve AUTH_015 → 400.
        Client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        Client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());
        Client.DefaultRequestHeaders.Remove("X-Is-Internal-Admin");
        Client.DefaultRequestHeaders.Add("X-Is-Internal-Admin", "true");

        var suiteResponse = await Client.PostAsJsonAsync("/api/v1/system-suites", new
        {
            tenantId,
            code = $"SS{Guid.NewGuid():N}"[..10],
            name = "Consola de operaciones",
            description = "Suite para las pruebas de borrado lógico de plantillas.",
        }, ct);
        suiteResponse.StatusCode.Should().Be(HttpStatusCode.Created, await suiteResponse.Content.ReadAsStringAsync(ct));
        using var suitePayload = JsonDocument.Parse(await suiteResponse.Content.ReadAsStringAsync(ct));
        var systemSuiteId = suitePayload.RootElement.GetProperty("systemSuiteId").GetGuid();

        var roleResponse = await Client.PostAsJsonAsync($"/api/v1/system-suites/{systemSuiteId}/roles", new
        {
            code = $"ROLE{Guid.NewGuid():N}"[..12].ToUpperInvariant(),
            value = "Coordinador de operaciones",
            description = "Rol para las pruebas de borrado lógico de plantillas.",
            parentRoleId = (Guid?)null,
            hierarchyLevel = 0,
            promotionOrder = 0,
        }, ct);
        roleResponse.StatusCode.Should().Be(HttpStatusCode.Created, await roleResponse.Content.ReadAsStringAsync(ct));
        using var rolePayload = JsonDocument.Parse(await roleResponse.Content.ReadAsStringAsync(ct));
        var roleId = rolePayload.RootElement.GetProperty("roleId").GetGuid();

        return new ContextoDePrueba(tenantId, systemSuiteId, roleId);
    }

    private async Task<Guid> CrearPlantillaConItemAsync(ContextoDePrueba contexto, CancellationToken ct)
    {
        var response = await Client.PostAsJsonAsync("/api/v1/permission-templates", new
        {
            tenantId = contexto.TenantId,
            roleId = contexto.RoleId,
            systemSuiteId = contexto.SystemSuiteId,
        }, ct);
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(ct));

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var templateId = payload.RootElement.GetProperty("templateId").GetGuid();

        // Publicar exige al menos un ítem; además el ítem es la evidencia de que el borrado lógico no
        // arrastra por cascada el detalle de las concesiones.
        var item = await Client.PostAsJsonAsync($"/api/v1/permission-templates/{templateId}/items", new
        {
            targetType = "SystemSuite",
            targetId = contexto.SystemSuiteId,
            actionId = Guid.NewGuid(),
            isAllowed = true,
            isDenied = false,
        }, ct);
        item.StatusCode.Should().Be(HttpStatusCode.Created, await item.Content.ReadAsStringAsync(ct));

        return templateId;
    }

    private async Task<Guid> CrearPerfilActivoAsync(ContextoDePrueba contexto, CancellationToken ct)
    {
        var userResponse = await Client.PostAsJsonAsync("/api/v1/user-accounts", new
        {
            tenantId = contexto.TenantId,
            branchId = (Guid?)null,
            email = $"operador.{Guid.NewGuid():N}"[..24] + "@evolith.local",
            category = "Internal",
            identityReference = $"EMP-{Guid.NewGuid():N}"[..10],
            identityReferenceType = "HrId",
        }, ct);
        userResponse.StatusCode.Should().Be(HttpStatusCode.Created, await userResponse.Content.ReadAsStringAsync(ct));
        using var userPayload = JsonDocument.Parse(await userResponse.Content.ReadAsStringAsync(ct));
        var userId = userPayload.RootElement.GetProperty("userAccountId").GetGuid();

        var profileResponse = await Client.PostAsJsonAsync("/api/v1/profiles", new
        {
            tenantId = contexto.TenantId,
            userId,
            roleId = contexto.RoleId,
            branchId = (Guid?)null,
        }, ct);
        profileResponse.StatusCode.Should().Be(HttpStatusCode.Created, await profileResponse.Content.ReadAsStringAsync(ct));
        using var profilePayload = JsonDocument.Parse(await profileResponse.Content.ReadAsStringAsync(ct));
        return profilePayload.RootElement.GetProperty("profileId").GetGuid();
    }

    /// <summary>
    /// Contexto EF conectado al MISMO contenedor que el API, con inquilino nulo para que ningún filtro
    /// global recorte la vista. Es la ventana al almacenamiento real: lo que el API oculta, aquí se ve.
    /// </summary>
    private UmsPlatformDbContext CrearContextoDirecto()
    {
        var options = new DbContextOptionsBuilder<UmsPlatformDbContext>()
            .UseNpgsql(Fixture.ConnectionString)
            .Options;

        return new UmsPlatformDbContext(
            options,
            new ContextoDeInquilinoDelSistema(),
            new Moq.Mock<MediatR.IPublisher>().Object,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<UmsPlatformDbContext>.Instance);
    }

    private sealed class ContextoDeInquilinoDelSistema : ITenantContext
    {
        public Guid? OrganizationId => null;
        public Guid? OriginalTenantId => null;
        public bool IsInternalAdmin => true;
        public void Initialize(Guid userTenantId, bool isInternalAdmin) { }
        public void SetOrganizationId(Guid organizationId) { }
        public void EnableCrossTenantAccess() { }
        public void DisableCrossTenantAccess() { }
    }
}
