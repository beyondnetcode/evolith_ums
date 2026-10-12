using Evolith.Ums.Infrastructure.Persistence.Configuration.Entities;
using Evolith.Ums.Presentation.IntegrationTest.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Evolith.Ums.Presentation.IntegrationTest.Configuration;

/// <summary>
/// E2E sobre PostgreSQL real (Testcontainers) de la excepción que el propietario del producto acotó
/// sobre ADR-0164 §2.3 el 2026-08-04: <b>el borrado lógico de una configuración LIBERA su ranura</b>.
///
/// Por qué hace falta el motor real y no basta el host InMemory: lo que aquí se demuestra es que el
/// índice único de `AppConfigurations` es PARCIAL. Con el índice total, volver a crear la misma
/// (ámbito, código) tras borrarla reventaba con 23505 → 500. El almacén en memoria no tiene índices,
/// así que allí la regla se puede cumplir por accidente.
///
/// El ámbito elegido tiene los tres identificadores presentes (inquilino + sistema + módulo) a
/// propósito. PostgreSQL trata los NULL como distintos dentro de un índice único, de modo que en el
/// ámbito Global —los tres NULL— el índice nunca llegó a arbitrar nada: la unicidad la sostiene solo
/// la comprobación del handler. Probar sobre el ámbito de módulo es lo único que interroga al índice.
/// </summary>
[Collection("PostgreSql")]
public sealed class AppConfigurationRanuraLiberadaE2ETests
{
    private static readonly Guid SeededTenantId = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6");
    private static readonly Guid SeededSystemSuiteId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private readonly PostgreSqlContainerFixture _fixture;
    private readonly PostgreSqlWebApplicationFactory? _factory;

    public AppConfigurationRanuraLiberadaE2ETests(PostgreSqlContainerFixture fixture)
    {
        _fixture = fixture;
        if (fixture.IsAvailable)
        {
            _factory = new PostgreSqlWebApplicationFactory(fixture.ConnectionString);
        }
    }

    [Fact]
    public async Task ElIndiceUnicoDeConfiguraciones_EsParcial_EnLaBaseReal()
    {
        if (!_fixture.IsAvailable || _factory is null)
        {
            Assert.Skip("Docker required.");
            return;
        }

        var ct = TestContext.Current.CancellationToken;
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<UmsPlatformDbContext>();

        // Se pregunta al catálogo del motor, no al modelo de EF: es la diferencia entre creer que la
        // migración se aplicó y comprobarlo.
        var definiciones = new List<string>();
        var connection = db.Database.GetDbConnection();
        await db.Database.OpenConnectionAsync(ct);
        await using (var command = connection.CreateCommand())
        {
            command.CommandText =
                "SELECT indexdef FROM pg_indexes " +
                "WHERE schemaname = 'ums_configuration' AND indexname IN (" +
                "  'IX_AppConfigurations_TenantId_SystemSuiteId_ModuleId_Code'," +
                "  'IX_ParameterDefinitions_Code'," +
                "  'IX_ParameterGlobalValues_ParameterDefinitionId'," +
                "  'IX_ParameterTenantValues_TenantId_ParameterDefinitionId')";

            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                definiciones.Add(reader.GetString(0));
            }
        }

        definiciones.Should().HaveCount(4, "los cuatro índices existen en el esquema");
        definiciones.Should().OnlyContain(d => d.Contains("WHERE", StringComparison.Ordinal),
            "los cuatro son índices PARCIALES: sin el WHERE, la lápida seguiría ocupando la ranura");
        definiciones.Should().OnlyContain(d => d.Contains("UNIQUE", StringComparison.Ordinal),
            "liberar la ranura no relaja la unicidad entre filas vivas");
    }

    /// <summary>
    /// El ciclo completo contra el índice real: crear, borrar y volver a crear con el MISMO ámbito y
    /// código. Cubre las cuatro mitades del cambio (recrear · las dos filas siguen ahí · la lectura
    /// resuelve la viva · el duplicado de una viva sigue en conflicto).
    /// </summary>
    [Fact]
    public async Task Configuracion_DeModulo_TrasBorrarse_SeVuelveACrear_YLaLecturaResuelveLaViva()
    {
        if (!_fixture.IsAvailable || _factory is null)
        {
            Assert.Skip("Docker required.");
            return;
        }

        var ct = TestContext.Current.CancellationToken;
        var admin = CreateAdminClient();
        var moduleId = Guid.NewGuid();
        var code = $"DESPACHO_ALERTA_DIAS_{Guid.NewGuid():N}"[..30].ToUpperInvariant();

        async Task<HttpResponseMessage> CrearAsync(string value) =>
            await admin.PostAsJsonAsync("/api/v1/app-configurations", new
            {
                tenantId = SeededTenantId,
                systemSuiteId = SeededSystemSuiteId,
                moduleId,
                code,
                value,
                description = "Días de antelación para avisar del vencimiento de un despacho",
                isInheritable = false,
                isEncrypted = false,
            }, ct);

        var primera = await CrearAsync("5");
        primera.StatusCode.Should().Be(HttpStatusCode.Created);
        using var primeraPayload = JsonDocument.Parse(await primera.Content.ReadAsStringAsync(ct));
        var primeraId = primeraPayload.RootElement.GetProperty("appConfigurationId").GetGuid();

        (await admin.DeleteAsync($"/api/v1/app-configurations/{primeraId}", ct))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        // (a) La ranura quedó libre: el mismo ámbito y código admiten un alta nueva. Contra el índice
        //     total esto era un 23505 → 500, y por la vía del handler un 409.
        var segunda = await CrearAsync("7");
        segunda.StatusCode.Should().Be(HttpStatusCode.Created,
            "borrar la configuración de un parámetro no puede impedir volver a configurarlo");
        using var segundaPayload = JsonDocument.Parse(await segunda.Content.ReadAsStringAsync(ct));
        var segundaId = segundaPayload.RootElement.GetProperty("appConfigurationId").GetGuid();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<UmsPlatformDbContext>();

        // (b) Las DOS filas siguen en la tabla. Se leen sin filtros: la API no distingue «oculto» de
        //     «borrado», así que preguntarle a ella no probaría nada (ADR-0164 §5).
        var filas = await db.Set<AppConfigurationRecord>()
            .IgnoreQueryFilters()
            .Where(x => x.Code == code)
            .ToListAsync(ct);

        filas.Should().HaveCount(2, "esto no es volver al borrado físico");
        filas.Single(x => x.Id == primeraId).StatusId.Should().Be(ConfigStatus.Deleted.Id);
        filas.Single(x => x.Id == segundaId).StatusId.Should().NotBe(ConfigStatus.Deleted.Id);

        // (c) La lectura por ámbito+código resuelve LA VIVA, sin ambigüedad, con la lápida presente.
        var repositorio = scope.ServiceProvider.GetRequiredService<IAppConfigurationRepository>();
        var viva = await repositorio.GetByScopeAndCodeAsync(SeededTenantId, SeededSystemSuiteId, moduleId, code, ct);
        viva.Should().NotBeNull();
        viva!.Props.Id.GetValue().Should().Be(segundaId, "el lookup por código nunca devuelve la lápida");

        (await admin.GetAsync($"/api/v1/app-configurations/{segundaId}", ct))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await admin.GetAsync($"/api/v1/app-configurations/{primeraId}", ct))
            .StatusCode.Should().Be(HttpStatusCode.NotFound, "la eliminada sigue oculta");

        // (d) Duplicar la VIVA sigue dando conflicto legible del dominio, no un error de índice.
        var duplicada = await CrearAsync("9");
        duplicada.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await duplicada.Content.ReadAsStringAsync(ct))
            .Should().Contain("already exists", "el conflicto tiene que decir qué pasa");
    }

    private HttpClient CreateAdminClient()
    {
        var client = _factory!.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
        });
        client.DefaultRequestHeaders.Add("X-Tenant-Id", SeededTenantId.ToString());
        client.DefaultRequestHeaders.Add("X-Is-Internal-Admin", "true");
        return client;
    }
}
