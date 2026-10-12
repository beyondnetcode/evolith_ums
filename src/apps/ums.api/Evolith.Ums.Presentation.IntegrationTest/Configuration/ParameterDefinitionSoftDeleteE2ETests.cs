using Microsoft.EntityFrameworkCore;
using Evolith.Ums.Infrastructure.Configuration;
using Evolith.Ums.Infrastructure.Persistence.Configuration.Entities;
using Evolith.Ums.Presentation.IntegrationTest.Infrastructure;

namespace Evolith.Ums.Presentation.IntegrationTest.Configuration;

/// <summary>
/// E2E sobre PostgreSQL real (Testcontainers) de la política de borrado del catálogo de parámetros:
/// <b>solo existe borrado lógico</b>. Sobre esta configuración se hacen consultas históricas, así que
/// la fila nunca puede desaparecer: una definición retirada hace un año tiene que seguir explicando
/// por qué el sistema se comportó como se comportó.
///
/// Lo que fija cada prueba:
///   1. `DELETE /parameter-definitions/{id}` responde 204 —el contrato HTTP no cambió— y la FILA
///      SIGUE EN LA BASE con `IsDeleted = true`. Sin esta segunda mitad, el cambio no está demostrado.
///   2. Tras el borrado, el GET responde 404 y la definición desaparece del listado.
///   3. Regla transaccional: con un valor global VIVO, el borrado se rechaza con 409.
///   4. Con ese mismo valor ya eliminado lógicamente, el borrado sí procede — y el valor también
///      sigue en la base.
///
/// Usa el host PostgreSQL (no el InMemory) porque solo ahí comandos y consultas del catálogo de
/// parámetros comparten el mismo almacén, que es justo lo que hay que observar.
/// </summary>
[Collection("PostgreSql")]
public sealed class ParameterDefinitionSoftDeleteE2ETests
{
    private static readonly Guid SeededTenantId = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6");

    private readonly PostgreSqlContainerFixture _fixture;
    private readonly PostgreSqlWebApplicationFactory? _factory;

    public ParameterDefinitionSoftDeleteE2ETests(PostgreSqlContainerFixture fixture)
    {
        _fixture = fixture;
        if (fixture.IsAvailable)
        {
            _factory = new PostgreSqlWebApplicationFactory(fixture.ConnectionString);
        }
    }

    [Fact]
    public async Task Delete_Devuelve204_YLaFilaSigueEnLaBase()
    {
        if (!_fixture.IsAvailable || _factory is null)
        {
            Assert.Skip("Docker required.");
            return;
        }

        var ct = TestContext.Current.CancellationToken;
        var admin = CreateAdminClient();
        var (definitionId, code) = await CreateDefinitionAsync(admin, ct);

        var deleteResponse = await admin.DeleteAsync($"/api/v1/parameter-definitions/{definitionId}", ct);
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent, "el contrato HTTP no cambia");

        // LA PRUEBA DE LA POLÍTICA: la fila sigue ahí, marcada, con su sello de quién y cuándo.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<UmsPlatformDbContext>();
            var record = await db.Set<ParameterDefinitionRecord>()
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(x => x.Id == definitionId, ct);

            record.Should().NotBeNull("el borrado es lógico: la fila jamás se elimina");
            record!.IsDeleted.Should().BeTrue();
            record.Code.Should().Be(code, "el dato histórico se conserva íntegro");
            record.DeletedAtUtc.Should().NotBeNull("hay que poder decir cuándo se retiró");
            record.DeletedBy.Should().NotBeNullOrWhiteSpace("hay que poder decir quién la retiró");
            record.IsActive.Should().BeFalse("una definición eliminada tampoco resuelve");
        }

        // …y, aun así, para las lecturas ya no existe.
        (await admin.GetAsync($"/api/v1/parameter-definitions/{definitionId}", ct))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        var listBody = await (await admin.GetAsync("/api/v1/parameter-definitions", ct))
            .Content.ReadAsStringAsync(ct);
        listBody.Should().NotContain(code, "lo eliminado no aparece en los listados");
    }

    [Fact]
    public async Task Delete_ConValorGlobalVivo_Devuelve409()
    {
        if (!_fixture.IsAvailable || _factory is null)
        {
            Assert.Skip("Docker required.");
            return;
        }

        var ct = TestContext.Current.CancellationToken;
        var admin = CreateAdminClient();
        var (definitionId, _) = await CreateDefinitionAsync(admin, ct);
        await CreateGlobalValueAsync(admin, definitionId, ct);

        var deleteResponse = await admin.DeleteAsync($"/api/v1/parameter-definitions/{definitionId}", ct);

        deleteResponse.StatusCode.Should().Be(HttpStatusCode.Conflict,
            because: "no se elimina lógicamente algo con referencias reales vivas");

        var body = await deleteResponse.Content.ReadAsStringAsync(ct);
        body.Should().Contain("parameter_has_active_values");
        body.Should().Contain("ParameterGlobalValue", "la respuesta dice QUÉ bloquea");

        // La definición sigue viva: un rechazo no deja rastro.
        (await admin.GetAsync($"/api/v1/parameter-definitions/{definitionId}", ct))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Delete_ConValorGlobalYaEliminadoLogicamente_SiProcede()
    {
        if (!_fixture.IsAvailable || _factory is null)
        {
            Assert.Skip("Docker required.");
            return;
        }

        var ct = TestContext.Current.CancellationToken;
        var admin = CreateAdminClient();
        var (definitionId, _) = await CreateDefinitionAsync(admin, ct);
        var valueId = await CreateGlobalValueAsync(admin, definitionId, ct);

        // Con el valor vivo, bloquea…
        (await admin.DeleteAsync($"/api/v1/parameter-definitions/{definitionId}", ct))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);

        // …se elimina lógicamente el dependiente…
        (await admin.DeleteAsync($"/api/v1/parameter-definitions/{definitionId}/global-values/{valueId}", ct))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        // …y entonces la definición ya se puede eliminar: la referencia dejó de ser real.
        (await admin.DeleteAsync($"/api/v1/parameter-definitions/{definitionId}", ct))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Ninguna de las dos filas se perdió.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<UmsPlatformDbContext>();

        var definitionRecord = await db.Set<ParameterDefinitionRecord>()
            .IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == definitionId, ct);
        definitionRecord.Should().NotBeNull();
        definitionRecord!.IsDeleted.Should().BeTrue();

        var valueRecord = await db.Set<ParameterGlobalValueRecord>()
            .IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == valueId, ct);
        valueRecord.Should().NotBeNull("el valor dependiente tampoco se borra físicamente");
        valueRecord!.StatusId.Should().Be(ConfigStatus.Deleted.Id);
    }

    [Fact]
    public async Task Delete_ConOverrideDeInquilinoVivo_Devuelve409_YTrasEliminarloProcede()
    {
        if (!_fixture.IsAvailable || _factory is null)
        {
            Assert.Skip("Docker required.");
            return;
        }

        var ct = TestContext.Current.CancellationToken;
        var admin = CreateAdminClient();
        var (definitionId, _) = await CreateDefinitionAsync(admin, ct);

        var createValue = await admin.PostAsJsonAsync(
            $"/api/v1/parameter-definitions/{definitionId}/tenant-values",
            new { definitionId, tenantId = SeededTenantId, value = "48" }, ct);
        createValue.StatusCode.Should().Be(HttpStatusCode.Created);
        using var valuePayload = JsonDocument.Parse(await createValue.Content.ReadAsStringAsync(ct));
        var valueId = valuePayload.RootElement.GetGuid();

        (await admin.DeleteAsync($"/api/v1/parameter-definitions/{definitionId}", ct))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);

        (await admin.DeleteAsync($"/api/v1/parameter-definitions/{definitionId}/tenant-values/{valueId}", ct))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await admin.DeleteAsync($"/api/v1/parameter-definitions/{definitionId}", ct))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<UmsPlatformDbContext>();
        var valueRecord = await db.Set<ParameterTenantValueRecord>()
            .IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == valueId, ct);
        valueRecord.Should().NotBeNull();
        valueRecord!.StatusId.Should().Be(ConfigStatus.Deleted.Id);
    }

    [Fact]
    public async Task Delete_DosVeces_LaSegundaDevuelve404()
    {
        if (!_fixture.IsAvailable || _factory is null)
        {
            Assert.Skip("Docker required.");
            return;
        }

        var ct = TestContext.Current.CancellationToken;
        var admin = CreateAdminClient();
        var (definitionId, _) = await CreateDefinitionAsync(admin, ct);

        (await admin.DeleteAsync($"/api/v1/parameter-definitions/{definitionId}", ct))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await admin.DeleteAsync($"/api/v1/parameter-definitions/{definitionId}", ct))
            .StatusCode.Should().Be(HttpStatusCode.NotFound, "para las lecturas ya no existe");
    }

    /// <summary>
    /// La ranura del catálogo se LIBERA al borrar. Contra PostgreSQL real, que es lo único que
    /// demuestra que el índice único parcial existe: si siguiera siendo total, el segundo alta no
    /// devolvería 201 sino un 23505 convertido en 500.
    ///
    /// Cubre las cuatro mitades: se recrea, las dos filas siguen ahí, la lectura por código resuelve
    /// la viva, y duplicar una viva sigue siendo conflicto legible.
    /// </summary>
    [Fact]
    public async Task Definicion_TrasBorrarse_LiberaSuCodigo_YLaLecturaResuelveLaViva()
    {
        if (!_fixture.IsAvailable || _factory is null)
        {
            Assert.Skip("Docker required.");
            return;
        }

        var ct = TestContext.Current.CancellationToken;
        var admin = CreateAdminClient();
        var (primeraId, code) = await CreateDefinitionAsync(admin, ct);

        (await admin.DeleteAsync($"/api/v1/parameter-definitions/{primeraId}", ct))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        // (a) El MISMO código vuelve a poder declararse.
        var segundaId = await CreateDefinitionWithCodeAsync(admin, code, ct);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<UmsPlatformDbContext>();

        // (b) Las dos filas conviven; la vieja marcada. Se lee SIN filtros: preguntar a la API no
        //     distinguiría «oculto» de «borrado» (ADR-0164 §5).
        var filas = await db.Set<ParameterDefinitionRecord>()
            .IgnoreQueryFilters()
            .Where(x => x.Code == code)
            .ToListAsync(ct);

        filas.Should().HaveCount(2, "liberar la ranura no borra la fila anterior");
        filas.Single(x => x.Id == primeraId).IsDeleted.Should().BeTrue();
        filas.Single(x => x.Id == segundaId).IsDeleted.Should().BeFalse();

        // (c) La lectura por código resuelve LA VIVA: el filtro global deja una sola candidata.
        var repositorio = scope.ServiceProvider.GetRequiredService<IParameterDefinitionRepository>();
        var viva = await repositorio.GetByCodeAsync(code, ct);
        viva.Should().NotBeNull();
        viva!.Props.Id.GetValue().Should().Be(segundaId, "nunca se resuelve la lápida");

        (await admin.GetAsync($"/api/v1/parameter-definitions/{segundaId}", ct))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await admin.GetAsync($"/api/v1/parameter-definitions/{primeraId}", ct))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        // (d) Duplicar una VIVA sigue siendo conflicto de dominio legible, no un error de índice.
        var duplicada = await PostDefinitionAsync(admin, code, ct);
        duplicada.IsSuccessStatusCode.Should().BeFalse("dos definiciones vivas no pueden compartir código");
        (await duplicada.Content.ReadAsStringAsync(ct))
            .Should().Contain("parameter_code_not_unique", "el rechazo lo nombra el dominio, no PostgreSQL");
    }

    /// <summary>
    /// Mismo criterio para los VALORES: global y de inquilino. Borrar el valor de un parámetro no
    /// puede impedir volver a fijarlo, y las lápidas no pueden hacer ambigua la resolución.
    /// </summary>
    [Fact]
    public async Task ValorGlobal_TrasBorrarse_SePuedeVolverAFijar_YSoloUnoVivo()
    {
        if (!_fixture.IsAvailable || _factory is null)
        {
            Assert.Skip("Docker required.");
            return;
        }

        var ct = TestContext.Current.CancellationToken;
        var admin = CreateAdminClient();
        var (definitionId, _) = await CreateDefinitionAsync(admin, ct);

        var primerValorId = await CreateGlobalValueAsync(admin, definitionId, ct);
        (await admin.DeleteAsync($"/api/v1/parameter-definitions/{definitionId}/global-values/{primerValorId}", ct))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        // (a) Vuelve a admitir un valor global.
        var segundoValorId = await CreateGlobalValueAsync(admin, definitionId, ct);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<UmsPlatformDbContext>();

        // (b) Las dos filas siguen en la base.
        var filas = await db.Set<ParameterGlobalValueRecord>()
            .IgnoreQueryFilters()
            .Where(x => x.ParameterDefinitionId == definitionId)
            .ToListAsync(ct);
        filas.Should().HaveCount(2);
        filas.Single(x => x.Id == primerValorId).StatusId.Should().Be(ConfigStatus.Deleted.Id);
        filas.Single(x => x.Id == segundoValorId).StatusId.Should().NotBe(ConfigStatus.Deleted.Id);

        // (c) La resolución devuelve el VIVO. Se instancia el servicio contra este mismo DbContext
        //     porque el host no lo registra; lo que importa es su consulta, no cómo se resuelve.
        //     Sin el filtro de estado, el `ToDictionary` interno reventaría por clave duplicada en
        //     cuanto conviven la lápida y el valor nuevo — la ambigüedad se paga en tiempo de
        //     ejecución, no en una aserción.
        var resolucion = new ParameterResolutionService(db);
        var globales = await resolucion.GetGlobalParametersAsync(ct);
        globales.Should().ContainSingle(p => p.DefinitionId == definitionId);

        var repositorio = scope.ServiceProvider.GetRequiredService<IParameterGlobalValueRepository>();
        var vivo = await repositorio.GetByDefinitionIdAsync(definitionId, ct);
        vivo.Should().NotBeNull();
        vivo!.Props.Id.GetValue().Should().Be(segundoValorId);

        // (d) Un segundo valor sobre el VIVO sigue rechazándose.
        var duplicado = await admin.PostAsJsonAsync(
            $"/api/v1/parameter-definitions/{definitionId}/global-values",
            new { definitionId, value = "99" }, ct);
        duplicado.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task ValorDeInquilino_TrasBorrarse_SePuedeVolverAFijar_YSoloUnoVivo()
    {
        if (!_fixture.IsAvailable || _factory is null)
        {
            Assert.Skip("Docker required.");
            return;
        }

        var ct = TestContext.Current.CancellationToken;
        var admin = CreateAdminClient();
        var (definitionId, _) = await CreateDefinitionAsync(admin, ct);

        var primerValorId = await CreateTenantValueAsync(admin, definitionId, "48", ct);
        (await admin.DeleteAsync($"/api/v1/parameter-definitions/{definitionId}/tenant-values/{primerValorId}", ct))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var segundoValorId = await CreateTenantValueAsync(admin, definitionId, "72", ct);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<UmsPlatformDbContext>();

        var filas = await db.Set<ParameterTenantValueRecord>()
            .IgnoreQueryFilters()
            .Where(x => x.TenantId == SeededTenantId && x.ParameterDefinitionId == definitionId)
            .ToListAsync(ct);
        filas.Should().HaveCount(2);
        filas.Single(x => x.Id == primerValorId).StatusId.Should().Be(ConfigStatus.Deleted.Id);
        filas.Single(x => x.Id == segundoValorId).StatusId.Should().NotBe(ConfigStatus.Deleted.Id);

        var repositorio = scope.ServiceProvider.GetRequiredService<IParameterTenantValueRepository>();
        var vivo = await repositorio.GetByTenantAndDefinitionAsync(SeededTenantId, definitionId, ct);
        vivo.Should().NotBeNull();
        vivo!.Props.Id.GetValue().Should().Be(segundoValorId, "la resolución del inquilino usa el override VIVO");

        var duplicado = await admin.PostAsJsonAsync(
            $"/api/v1/parameter-definitions/{definitionId}/tenant-values",
            new { definitionId, tenantId = SeededTenantId, value = "96" }, ct);
        duplicado.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Delete_ComoNoAdministrador_Devuelve403()
    {
        if (!_fixture.IsAvailable || _factory is null)
        {
            Assert.Skip("Docker required.");
            return;
        }

        var ct = TestContext.Current.CancellationToken;
        var admin = CreateAdminClient();
        var (definitionId, _) = await CreateDefinitionAsync(admin, ct);

        var nonAdmin = CreateClient(isInternalAdmin: false);

        (await nonAdmin.DeleteAsync($"/api/v1/parameter-definitions/{definitionId}", ct))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden, "la autorización del borrado no cambia");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static async Task<(Guid Id, string Code)> CreateDefinitionAsync(HttpClient admin, CancellationToken ct)
    {
        // Código realista del dominio aduanero + sufijo único; Code lo normaliza a mayúsculas.
        var code = $"SESSION_TIMEOUT_{Guid.NewGuid():N}"[..24].ToUpperInvariant();
        return (await CreateDefinitionWithCodeAsync(admin, code, ct), code);
    }

    private static async Task<Guid> CreateDefinitionWithCodeAsync(HttpClient admin, string code, CancellationToken ct)
    {
        var response = await PostDefinitionAsync(admin, code, ct);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        return payload.RootElement.GetGuid();
    }

    private static Task<HttpResponseMessage> PostDefinitionAsync(HttpClient admin, string code, CancellationToken ct)
        => admin.PostAsJsonAsync("/api/v1/parameter-definitions", new
        {
            code,
            name = "Tiempo de expiración de sesión",
            description = "Minutos de inactividad antes de cerrar la sesión del operador aduanero",
            dataTypeId = 2,          // Number
            defaultValue = "30",
            scopeId = 3,             // GlobalAndTenant
            isMandatory = false,
            displayOrder = 10,
        }, ct);

    private static async Task<Guid> CreateTenantValueAsync(
        HttpClient admin, Guid definitionId, string value, CancellationToken ct)
    {
        var response = await admin.PostAsJsonAsync(
            $"/api/v1/parameter-definitions/{definitionId}/tenant-values",
            new { definitionId, tenantId = SeededTenantId, value }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        return payload.RootElement.GetGuid();
    }

    private static async Task<Guid> CreateGlobalValueAsync(HttpClient admin, Guid definitionId, CancellationToken ct)
    {
        var response = await admin.PostAsJsonAsync(
            $"/api/v1/parameter-definitions/{definitionId}/global-values",
            new { definitionId, value = "45" }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        return payload.RootElement.GetGuid();
    }

    private HttpClient CreateAdminClient() => CreateClient(isInternalAdmin: true);

    private HttpClient CreateClient(bool isInternalAdmin)
    {
        var client = _factory!.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
        });
        client.DefaultRequestHeaders.Add("X-Tenant-Id", SeededTenantId.ToString());
        client.DefaultRequestHeaders.Add("X-Is-Internal-Admin", isInternalAdmin ? "true" : "false");
        return client;
    }
}
