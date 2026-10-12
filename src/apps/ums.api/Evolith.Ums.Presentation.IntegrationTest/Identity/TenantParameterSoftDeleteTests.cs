using Evolith.Ums.Domain.Identity.Tenant.TenantParameter;
using Evolith.Ums.Domain.Kernel;
using Evolith.Ums.Infrastructure.Persistence.Identity.TenantParameter;
using Evolith.Ums.Presentation.IntegrationTest.Infrastructure;
using Microsoft.EntityFrameworkCore;
using TenantParameterAggregate = Evolith.Ums.Domain.Identity.Tenant.TenantParameter.TenantParameter;

namespace Evolith.Ums.Presentation.IntegrationTest.Identity;

/// <summary>
/// Política del propietario: SOLO existe borrado lógico. El repositorio de parámetros de inquilino
/// hacía <c>dbContext.TenantParameters.Remove(...)</c> y la configuración histórica del inquilino
/// —qué valor regía, quién lo puso y cuándo— se perdía sin remedio.
///
/// El agregado NO tiene endpoint DELETE (nadie llamaba a <c>DeleteAsync</c>: el borrado físico era
/// código muerto alcanzable solo desde infraestructura), así que la evidencia se toma en el nivel
/// donde el borrado ocurre de verdad —repositorio sobre PostgreSQL real— en vez de simularla por HTTP:
///
///   a. <c>DeleteAsync</c> responde igual que antes (sin excepción, sin valor de retorno nuevo).
///   b. La FILA SIGUE EN LA BASE, con todos sus datos y con <c>IsDeleted = true</c>.
///   c. Las lecturas la ocultan: el repositorio devuelve null / listas sin ella.
///   d. Con el vínculo VIVO (parámetro activo) el dominio rechaza con el código de operación
///      bloqueada que la presentación traduce a 409.
///   e. Con ese vínculo ya eliminado lógicamente (parámetro desactivado), el borrado sí procede.
/// </summary>
[Collection("PostgreSql")]
public sealed class TenantParameterSoftDeleteTests
{
    private readonly PostgreSqlContainerFixture _fixture;
    private static readonly ActorId Actor = ActorId.Create("integration-test");

    public TenantParameterSoftDeleteTests(PostgreSqlContainerFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Delete_ConVinculoActivo_EsRechazadoPorElDominio_YNoTocaLaBase()
    {
        if (!_fixture.IsAvailable) Assert.Skip("Docker es necesario para las pruebas de integración PostgreSQL.");
        var ct = TestContext.Current.CancellationToken;

        var tenantId = Guid.NewGuid();
        var parametro = NuevoParametro(tenantId, "AUTH_GRAPH_DEFAULT_FORMAT", "JSON");

        await using (var db = CrearContexto())
        {
            var repo = new PostgreSqlTenantParameterRepository(db);
            await repo.AddAsync(parametro, ct);
            await repo.UnitOfWork.SaveEntitiesAsync(ct);
        }

        // (d) Referencia viva: el parámetro está ACTIVO, o sea que la configuración del inquilino lo
        // resuelve ahora mismo por su código. El borrado se rechaza con el código de bloqueo.
        var resultado = parametro.Delete(Actor);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error.Should().Contain(DomainErrors.TenantParameter.HasActiveBinding);
        parametro.IsDeleted.Should().BeFalse();

        await using var verificacion = CrearContexto();
        var fila = await verificacion.TenantParameters
            .IgnoreQueryFilters()
            .SingleAsync(x => x.Id == parametro.GetId().GetValue(), ct);
        fila.IsDeleted.Should().BeFalse("un borrado rechazado no puede haber escrito nada");
    }

    [Fact]
    public async Task Delete_TrasDesactivar_DejaLaFilaEnLaBase_YLaOcultaDeLasLecturas()
    {
        if (!_fixture.IsAvailable) Assert.Skip("Docker es necesario para las pruebas de integración PostgreSQL.");
        var ct = TestContext.Current.CancellationToken;

        var tenantId = Guid.NewGuid();
        var parametro = NuevoParametro(tenantId, "EXPORT_PROFILE_PERMISSION_GRAPH_DEFAULT_FORMAT", "XML");
        var parametroId = parametro.GetId().GetValue();

        await using (var db = CrearContexto())
        {
            var repo = new PostgreSqlTenantParameterRepository(db);
            await repo.AddAsync(parametro, ct);
            await repo.UnitOfWork.SaveEntitiesAsync(ct);
        }

        // (e) Se desactiva primero —esa desactivación ES la eliminación lógica del vínculo— y entonces
        // el borrado procede.
        parametro.Deactivate(Actor).IsSuccess.Should().BeTrue();
        parametro.Delete(Actor).IsSuccess.Should().BeTrue();

        await using (var db = CrearContexto())
        {
            var repo = new PostgreSqlTenantParameterRepository(db);
            // (a) Misma firma y mismo comportamiento observable que antes: no lanza y no devuelve nada.
            await repo.DeleteAsync(parametro, ct);
            await repo.UnitOfWork.SaveEntitiesAsync(ct);
        }

        // (b) LA FILA SIGUE EN LA BASE, con su valor y su auditoría intactos. Es la prueba que fija la
        // política: hay que saltarse el filtro global para verla, pero está.
        await using var verificacion = CrearContexto();
        var fila = await verificacion.TenantParameters
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(x => x.Id == parametroId, ct);

        fila.Should().NotBeNull("el borrado es LÓGICO: la fila no puede desaparecer de la tabla");
        fila!.IsDeleted.Should().BeTrue();
        fila.Value.Should().Be("XML", "el histórico conserva el valor que regía cuando se eliminó");
        fila.Code.Should().Be("EXPORT_PROFILE_PERMISSION_GRAPH_DEFAULT_FORMAT");

        // (c) Las lecturas la ocultan, tanto por el filtro global como por el repositorio.
        (await verificacion.TenantParameters.SingleOrDefaultAsync(x => x.Id == parametroId, ct))
            .Should().BeNull("el filtro global de borrado lógico saca la fila de toda consulta ordinaria");

        var lectura = new PostgreSqlTenantParameterRepository(verificacion);
        (await lectura.GetByIdAsync(parametroId, ct)).Should().BeNull();
        (await lectura.GetByCodeAsync(tenantId, "EXPORT_PROFILE_PERMISSION_GRAPH_DEFAULT_FORMAT", ct)).Should().BeNull();
        (await lectura.GetByTenantIdAsync(tenantId, ct)).Should().BeEmpty();
        (await lectura.ExistsActiveCodeAsync(tenantId, "EXPORT_PROFILE_PERMISSION_GRAPH_DEFAULT_FORMAT", ct))
            .Should().BeFalse();
    }

    [Fact]
    public async Task DeleteAsync_SinPasarPorElDominio_SeCorta()
    {
        if (!_fixture.IsAvailable) Assert.Skip("Docker es necesario para las pruebas de integración PostgreSQL.");
        var ct = TestContext.Current.CancellationToken;

        // La DECISIÓN de eliminar es del dominio, que es donde vive la guardia de cascada. Si alguien
        // llama al repositorio saltándosela, se corta en vez de escribir un borrado que nadie validó:
        // así el borrado físico no puede reaparecer por la puerta de atrás.
        var parametro = NuevoParametro(Guid.NewGuid(), "SESSION_IDLE_TIMEOUT_MINUTES", "30");

        await using var db = CrearContexto();
        var repo = new PostgreSqlTenantParameterRepository(db);
        await repo.AddAsync(parametro, ct);
        await repo.UnitOfWork.SaveEntitiesAsync(ct);

        var act = async () => await repo.DeleteAsync(parametro, ct);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Delete_PermiteVolverAAltaElMismoCodigo()
    {
        if (!_fixture.IsAvailable) Assert.Skip("Docker es necesario para las pruebas de integración PostgreSQL.");
        var ct = TestContext.Current.CancellationToken;

        // El índice único parcial IX_TenantParameters_TenantId_Code_IsActive solo cubre filas ACTIVAS.
        // Como el borrado lógico exige desactivación previa, la fila eliminada queda fuera del índice y
        // el inquilino puede volver a dar de alta ese código sin chocar con 23505.
        var tenantId = Guid.NewGuid();
        const string codigo = "AUTH_GRAPH_ALLOWED_FORMATS";

        var original = NuevoParametro(tenantId, codigo, "JSON,XML");
        await using (var db = CrearContexto())
        {
            var repo = new PostgreSqlTenantParameterRepository(db);
            await repo.AddAsync(original, ct);
            await repo.UnitOfWork.SaveEntitiesAsync(ct);
        }

        original.Deactivate(Actor);
        original.Delete(Actor);
        await using (var db = CrearContexto())
        {
            var repo = new PostgreSqlTenantParameterRepository(db);
            await repo.DeleteAsync(original, ct);
            await repo.UnitOfWork.SaveEntitiesAsync(ct);
        }

        var reemplazo = NuevoParametro(tenantId, codigo, "JSON,XML,YAML");
        await using (var db = CrearContexto())
        {
            var repo = new PostgreSqlTenantParameterRepository(db);
            await repo.AddAsync(reemplazo, ct);
            await repo.UnitOfWork.SaveEntitiesAsync(ct);
        }

        await using var verificacion = CrearContexto();
        var filas = await verificacion.TenantParameters
            .IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId && x.Code == codigo)
            .ToListAsync(ct);

        filas.Should().HaveCount(2, "la eliminada permanece como histórico junto a la nueva");
        filas.Count(x => x.IsDeleted).Should().Be(1);
        filas.Single(x => !x.IsDeleted).Value.Should().Be("JSON,XML,YAML");
    }

    // ── Utilidades ──────────────────────────────────────────────────────────

    private static TenantParameterAggregate NuevoParametro(Guid tenantId, string codigo, string valor)
        => TenantParameterAggregate.Create(
            TenantId.Load(tenantId),
            codigo,
            "Parámetro de inquilino para la prueba de borrado lógico.",
            valor,
            TenantParameterValueType.String,
            TenantParameterCategory.Export,
            isSensitive: false,
            defaultValue: null,
            allowedValues: null,
            Actor).Value;

    /// <summary>
    /// Contexto EF contra el contenedor real, con inquilino nulo: el filtro de aislamiento no recorta y
    /// el ÚNICO predicado que queda sobre TenantParameters es el de borrado lógico, que es lo que se
    /// quiere observar.
    /// </summary>
    private UmsPlatformDbContext CrearContexto()
    {
        var options = new DbContextOptionsBuilder<UmsPlatformDbContext>()
            .UseNpgsql(_fixture.ConnectionString)
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
