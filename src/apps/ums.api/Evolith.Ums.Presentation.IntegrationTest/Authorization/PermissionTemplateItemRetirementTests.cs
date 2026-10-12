using Evolith.Ums.Presentation.IntegrationTest.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Evolith.Ums.Presentation.IntegrationTest.Authorization;

/// <summary>
/// ADR-0164 aplicado a los ÍTEMS de una plantilla de permisos. Un ítem es una concesión —«este rol
/// puede hacer esta acción sobre este recurso»—, es decir una DECISIÓN en el sentido del ADR-0162, y
/// por tanto no se borra: se retira.
///
/// Antes de este cambio <c>PermissionTemplate.RemoveItem</c> hacía <c>_items.Remove(...)</c> y
/// <c>EfChildCollectionReconciler</c> traducía esa ausencia en un DELETE físico. La fila desaparecía
/// y con ella la prueba de que alguien concedió ese permiso.
///
/// Estas pruebas fijan sobre PostgreSQL real (Testcontainers) las cuatro afirmaciones del ADR:
///
///   a. El contrato HTTP no cambia: DELETE del ítem sigue devolviendo 204.
///   b. La FILA SIGUE EN LA BASE, marcada como retirada. Se lee con un contexto propio, SIN los
///      filtros de lectura: preguntar a la API no distingue «oculto» de «borrado» (ADR-0164 §5).
///   c. El ciclo retirar/reactivar sigue funcionando y opera sobre la MISMA fila.
///   d. La clave natural del ítem no se libera: el alta con la misma terna responde 409 de dominio
///      legible, no una violación de índice.
/// </summary>
[Collection("PostgreSql")]
public sealed class PermissionTemplateItemRetirementTests : IntegrationTestBase
{
    public PermissionTemplateItemRetirementTests(PostgreSqlContainerFixture fixture) : base(fixture) { }

    [Fact]
    public async Task RetirarItem_Responde204_YLaFilaSigueEnLaBaseMarcadaComoRetirada()
    {
        if (!Fixture.IsAvailable) Assert.Skip("Docker es necesario para las pruebas de integración PostgreSQL.");
        var ct = TestContext.Current.CancellationToken;

        var contexto = await ProvisionarSuiteYRolAsync(ct);
        var templateId = await CrearPlantillaAsync(contexto, ct);
        var actionId = Guid.NewGuid();
        var itemId = await AnadirItemAsync(templateId, contexto.SystemSuiteId, actionId, ct);

        // (a) El contrato no cambia: 204 No Content, igual que con el borrado físico.
        var retirada = await Client.DeleteAsync($"/api/v1/permission-templates/{templateId}/items/{itemId}", ct);
        retirada.StatusCode.Should().Be(HttpStatusCode.NoContent, await retirada.Content.ReadAsStringAsync(ct));

        // (b) LA FILA SIGUE EN LA BASE. Es LA prueba: la evidencia viene del almacenamiento, no del
        // API, porque el API es precisamente lo que ahora la presenta como retirada.
        await using var db = CrearContextoDirecto();
        var fila = await db.PermissionTemplateItems.SingleOrDefaultAsync(x => x.Id == itemId, ct);

        fila.Should().NotBeNull("la retirada es LÓGICA: la fila del ítem no puede desaparecer de la tabla");
        fila!.IsActive.Should().BeFalse("la retirada se registra marcando el ítem, no quitándolo");
        fila.TargetId.Should().Be(contexto.SystemSuiteId, "el destino de la concesión sobrevive para poder auditarla");
        fila.ActionId.Should().Be(actionId, "la acción concedida sobrevive: es la mitad de la respuesta a «quién pudo hacer qué»");
        fila.UpdatedBy.Should().NotBeNullOrWhiteSpace("la auditoría debe registrar quién retiró la concesión");

        // La plantilla lo sigue mostrando en su detalle, apagado: el operador necesita verlo para
        // poder reactivarlo, y ocultarlo lo dejaría sin forma de recuperar una retirada por error.
        var detalle = await LeerItemsDelDetalleAsync(templateId, ct);
        detalle.Should().ContainSingle(i => i.ItemId == itemId && !i.IsActive);
    }

    [Fact]
    public async Task RetirarYReactivar_OperaSobreLaMismaFila_SinCrearOtra()
    {
        if (!Fixture.IsAvailable) Assert.Skip("Docker es necesario para las pruebas de integración PostgreSQL.");
        var ct = TestContext.Current.CancellationToken;

        var contexto = await ProvisionarSuiteYRolAsync(ct);
        var templateId = await CrearPlantillaAsync(contexto, ct);
        var itemId = await AnadirItemAsync(templateId, contexto.SystemSuiteId, Guid.NewGuid(), ct);

        (await Client.DeleteAsync($"/api/v1/permission-templates/{templateId}/items/{itemId}", ct))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var reactivada = await Client.PostAsync(
            $"/api/v1/permission-templates/{templateId}/items/{itemId}/activate", null, ct);
        reactivada.StatusCode.Should().Be(HttpStatusCode.NoContent, await reactivada.Content.ReadAsStringAsync(ct));

        // (c) Misma fila, ahora vigente. El conteo importa tanto como el estado: si la reactivación
        // insertase una fila nueva, el ítem volvería con otra identidad y las referencias al anterior
        // apuntarían a una concesión fantasma.
        await using var db = CrearContextoDirecto();
        var filas = await db.PermissionTemplateItems.Where(x => x.TemplateId == templateId).ToListAsync(ct);

        filas.Should().ContainSingle("retirar y reactivar mueven un estado; no crean ni destruyen filas");
        filas[0].Id.Should().Be(itemId);
        filas[0].IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task AltaConLaClaveDeUnItemRetirado_Responde409_LaClaveNoSeLibera()
    {
        if (!Fixture.IsAvailable) Assert.Skip("Docker es necesario para las pruebas de integración PostgreSQL.");
        var ct = TestContext.Current.CancellationToken;

        var contexto = await ProvisionarSuiteYRolAsync(ct);
        var templateId = await CrearPlantillaAsync(contexto, ct);
        var actionId = Guid.NewGuid();
        var itemId = await AnadirItemAsync(templateId, contexto.SystemSuiteId, actionId, ct);

        (await Client.DeleteAsync($"/api/v1/permission-templates/{templateId}/items/{itemId}", ct))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        // (d) ADR-0164 §2.3: la clave natural (plantilla, tipo, destino, acción) queda ocupada para
        // siempre. El rechazo llega del DOMINIO —conflicto legible— y no del índice único: si la
        // guarda no comparase por valor, esto caería a 23505 y saldría como un 409 opaco de
        // concurrencia, indistinguible de una carrera.
        var repetida = await Client.PostAsJsonAsync($"/api/v1/permission-templates/{templateId}/items", new
        {
            targetType = "SystemSuite",
            targetId = contexto.SystemSuiteId,
            actionId,
            isAllowed = true,
            isDenied = false,
        }, ct);

        repetida.StatusCode.Should().Be(HttpStatusCode.Conflict, await repetida.Content.ReadAsStringAsync(ct));
        (await repetida.Content.ReadAsStringAsync(ct))
            .Should().Contain("template_item_target_retired",
                "el conflicto nombra la retirada para que el cliente proponga reactivar, no repetir el alta");

        // Y no se coló una segunda fila con la misma clave.
        await using var db = CrearContextoDirecto();
        var filas = await db.PermissionTemplateItems
            .Where(x => x.TemplateId == templateId && x.TargetId == contexto.SystemSuiteId && x.ActionId == actionId)
            .ToListAsync(ct);
        filas.Should().ContainSingle();
    }

    [Fact]
    public async Task PublicarConTodasLasConcesionesRetiradas_SeRechaza()
    {
        if (!Fixture.IsAvailable) Assert.Skip("Docker es necesario para las pruebas de integración PostgreSQL.");
        var ct = TestContext.Current.CancellationToken;

        // Puerta que abre el borrado lógico: con el borrado físico, retirar el último ítem dejaba la
        // plantilla sin filas y `Publish` la rechazaba. Ahora la fila sobrevive, así que sin una
        // guarda sobre lo VIGENTE se publicaría un contrato que no concede nada — y sería asignable.
        var contexto = await ProvisionarSuiteYRolAsync(ct);
        var templateId = await CrearPlantillaAsync(contexto, ct);
        var itemId = await AnadirItemAsync(templateId, contexto.SystemSuiteId, Guid.NewGuid(), ct);

        (await Client.DeleteAsync($"/api/v1/permission-templates/{templateId}/items/{itemId}", ct))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var publicacion = await Client.PostAsync($"/api/v1/permission-templates/{templateId}/publish", null, ct);

        publicacion.IsSuccessStatusCode.Should().BeFalse(
            "una plantilla cuyas concesiones están todas retiradas no concede nada: publicarla sería publicar un contrato vacío");
        (await publicacion.Content.ReadAsStringAsync(ct)).Should().Contain("template_items_required");
    }

    // ── Utilidades de aprovisionamiento ─────────────────────────────────────

    private sealed record ContextoDePrueba(Guid TenantId, Guid SystemSuiteId, Guid RoleId);

    private sealed record ItemDelDetalle(Guid ItemId, bool IsActive);

    private async Task<List<ItemDelDetalle>> LeerItemsDelDetalleAsync(Guid templateId, CancellationToken ct)
    {
        var response = await Client.GetAsync($"/api/v1/permission-templates/{templateId}", ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        return payload.RootElement.GetProperty("items").EnumerateArray()
            .Select(i => new ItemDelDetalle(
                i.GetProperty("itemId").GetGuid(),
                i.GetProperty("isActive").GetBoolean()))
            .ToList();
    }

    private async Task<ContextoDePrueba> ProvisionarSuiteYRolAsync(CancellationToken ct)
    {
        // El host PostgreSQL corre con SeedDevData=false: no hay ningún inquilino sembrado, así que
        // cada prueba levanta el suyo (mismo patrón que PermissionTemplateSoftDeleteTests).
        var code = $"PTIR{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        var tenantResponse = await Client.PostAsJsonAsync("/api/v1/tenants", new
        {
            code,
            name = $"Inquilino de retirada de ítems {code}",
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
            name = "Consola de despacho",
            description = "Suite para las pruebas de retirada de ítems de plantilla.",
        }, ct);
        suiteResponse.StatusCode.Should().Be(HttpStatusCode.Created, await suiteResponse.Content.ReadAsStringAsync(ct));
        using var suitePayload = JsonDocument.Parse(await suiteResponse.Content.ReadAsStringAsync(ct));
        var systemSuiteId = suitePayload.RootElement.GetProperty("systemSuiteId").GetGuid();

        var roleResponse = await Client.PostAsJsonAsync($"/api/v1/system-suites/{systemSuiteId}/roles", new
        {
            code = $"ROLE{Guid.NewGuid():N}"[..12].ToUpperInvariant(),
            value = "Analista de despacho aduanero",
            description = "Rol para las pruebas de retirada de ítems de plantilla.",
            parentRoleId = (Guid?)null,
            hierarchyLevel = 0,
            promotionOrder = 0,
        }, ct);
        roleResponse.StatusCode.Should().Be(HttpStatusCode.Created, await roleResponse.Content.ReadAsStringAsync(ct));
        using var rolePayload = JsonDocument.Parse(await roleResponse.Content.ReadAsStringAsync(ct));
        var roleId = rolePayload.RootElement.GetProperty("roleId").GetGuid();

        return new ContextoDePrueba(tenantId, systemSuiteId, roleId);
    }

    private async Task<Guid> CrearPlantillaAsync(ContextoDePrueba contexto, CancellationToken ct)
    {
        var response = await Client.PostAsJsonAsync("/api/v1/permission-templates", new
        {
            tenantId = contexto.TenantId,
            roleId = contexto.RoleId,
            systemSuiteId = contexto.SystemSuiteId,
        }, ct);
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(ct));

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        return payload.RootElement.GetProperty("templateId").GetGuid();
    }

    private async Task<Guid> AnadirItemAsync(Guid templateId, Guid targetId, Guid actionId, CancellationToken ct)
    {
        var response = await Client.PostAsJsonAsync($"/api/v1/permission-templates/{templateId}/items", new
        {
            targetType = "SystemSuite",
            targetId,
            actionId,
            isAllowed = true,
            isDenied = false,
        }, ct);
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(ct));

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        return payload.RootElement.GetProperty("itemId").GetGuid();
    }

    /// <summary>
    /// Contexto EF conectado al MISMO contenedor que el API, con inquilino nulo para que ningún filtro
    /// global recorte la vista. Es la ventana al almacenamiento real: lo que el API presenta como
    /// retirado, aquí se ve como la fila que sigue existiendo.
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
