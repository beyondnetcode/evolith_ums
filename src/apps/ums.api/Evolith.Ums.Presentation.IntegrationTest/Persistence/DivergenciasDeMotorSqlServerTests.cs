using Microsoft.EntityFrameworkCore;
using Evolith.Ums.Application.Common.Interfaces;
using Evolith.Ums.Infrastructure.Persistence;

namespace Evolith.Ums.Presentation.IntegrationTest.Persistence;

/// <summary>
/// Las diferencias entre PostgreSQL y SQL Server, protegidas contra regresión ([[G-275]]).
///
/// <para>ADR-UMS-102 reabre SQL Server como motor elegible y acredita que funciona, pero todo se
/// comprobó a mano: las piezas que difieren —la traducción de los predicados de índice y la cascada
/// de la FK autorreferente— no tenían nada que se pusiera en rojo si alguien las rompía.</para>
///
/// <para>Estas pruebas se construyen sobre el MODELO, no sobre una conexión: EF resuelve el modelo
/// del proveedor sin hablar con ningún servidor, así que corren en cualquier máquina —incluidas las
/// ARM del equipo, donde la imagen amd64 de SQL Server no se sostiene bajo carga—. Las que sí
/// exigen motor viven en <c>MotorSqlServerTests</c> y se omiten cuando no hay ninguno.</para>
/// </summary>
public sealed class DivergenciasDeMotorSqlServerTests
{
    // No se conecta: EF construye el modelo del proveedor sin abrir la conexión. La cadena existe
    // solo porque `UseSqlServer` exige una.
    private const string CadenaQueNadieAbre = "Server=modelo-sin-servidor;Database=ums;TrustServerCertificate=True";
    private const string CadenaPostgresQueNadieAbre = "Host=modelo-sin-servidor;Database=ums;Username=x;Password=y";

    private static UmsPlatformDbContext ContextoSqlServer()
    {
        var opciones = new DbContextOptionsBuilder<UmsPlatformDbContext>()
            .UseSqlServer(CadenaQueNadieAbre, o => o.MigrationsAssembly(MigrationsDesignTime.EnsambladoSqlServer))
            .Options;
        return new UmsPlatformDbContext(opciones, new ContextoDeSistema(), new Moq.Mock<MediatR.IPublisher>().Object,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<UmsPlatformDbContext>.Instance);
    }

    private static UmsPlatformDbContext ContextoPostgres()
    {
        var opciones = new DbContextOptionsBuilder<UmsPlatformDbContext>()
            .UseNpgsql(CadenaPostgresQueNadieAbre)
            .Options;
        return new UmsPlatformDbContext(opciones, new ContextoDeSistema(), new Moq.Mock<MediatR.IPublisher>().Object,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<UmsPlatformDbContext>.Instance);
    }

    [Fact]
    public void NingunaFkAutorreferente_QuedaEnCascadaEnSqlServer()
    {
        // SQL Server rechaza en CREATE TABLE cualquier FK que pueda formar un ciclo de borrado
        // (error 1785), y una jerarquía autorreferenciada —un nodo de menú que apunta a su padre—
        // es exactamente eso. El modelo compartido las declara en cascada porque PostgreSQL las
        // admite; degradarlas a NO ACTION solo para SQL Server es lo que hace que la base se pueda
        // crear. Sin esta prueba, añadir una jerarquía nueva rompe el motor y nada se pone rojo.
        using var contexto = ContextoSqlServer();

        var enCascada = contexto.Model.GetEntityTypes()
            .SelectMany(e => e.GetForeignKeys())
            .Where(fk => fk.PrincipalEntityType == fk.DeclaringEntityType)
            .Where(fk => fk.DeleteBehavior == DeleteBehavior.Cascade)
            .Select(fk => $"{fk.DeclaringEntityType.ShortName()}.{string.Join(',', fk.Properties.Select(p => p.Name))}")
            .ToList();

        Assert.True(enCascada.Count == 0,
            "FK autorreferentes en cascada bajo SQL Server (error 1785 al crear la base): " + string.Join(" · ", enCascada));
    }

    [Fact]
    public void LasFkAutorreferentes_SiguenEnCascadaEnPostgres()
    {
        // La otra mitad: la degradación es SOLO para SQL Server. Si alguien la aplicara al modelo
        // compartido, PostgreSQL perdería el borrado en cascada sin que nadie lo decidiera.
        using var contexto = ContextoPostgres();

        var autorreferentes = contexto.Model.GetEntityTypes()
            .SelectMany(e => e.GetForeignKeys())
            .Where(fk => fk.PrincipalEntityType == fk.DeclaringEntityType)
            .ToList();

        Assert.NotEmpty(autorreferentes);
        Assert.Contains(autorreferentes, fk => fk.DeleteBehavior == DeleteBehavior.Cascade);
    }

    [Fact]
    public void TodoIndiceFiltrado_UsaLaSintaxisDeSqlServer()
    {
        // Los predicados se escriben a mano en las configuraciones con sintaxis PostgreSQL
        // —identificadores entre comillas dobles, `true`/`false`, `!=`— y se traducen para este
        // motor. SQL Server rechaza el predicado sin traducir con el error 10735. Un predicado
        // nuevo con una forma no cubierta pasaría hoy inadvertido hasta el despliegue.
        using var contexto = ContextoSqlServer();

        var mal = contexto.Model.GetEntityTypes()
            .SelectMany(e => e.GetIndexes())
            .Select(i => new { Entidad = i.DeclaringEntityType.ShortName(), Filtro = i.GetFilter() })
            .Where(i => !string.IsNullOrWhiteSpace(i.Filtro))
            .Where(i => i.Filtro!.Contains('"', StringComparison.Ordinal)
                        || i.Filtro.Contains("!=", StringComparison.Ordinal)
                        || i.Filtro.Contains("= true", StringComparison.OrdinalIgnoreCase)
                        || i.Filtro.Contains("= false", StringComparison.OrdinalIgnoreCase))
            .Select(i => $"{i.Entidad}: {i.Filtro}")
            .ToList();

        Assert.True(mal.Count == 0,
            "Predicados de índice sin traducir a SQL Server (error 10735): " + string.Join(" · ", mal));
    }

    [Fact]
    public void LaTraduccionNoPierdeNingunIndiceFiltrado()
    {
        // Traducir no puede significar «descartar»: todo índice con predicado en PostgreSQL debe
        // seguir teniéndolo en SQL Server. Una traducción que devolviera vacío dejaría el índice sin
        // filtro —cubriendo de más, y rompiendo la unicidad parcial— sin romper nada visible.
        //
        // Se compara índice a índice y no por total: SQL Server añade por su cuenta el predicado
        // `IS NOT NULL` a los índices ÚNICOS sobre columnas anulables (medido: 17 frente a 14), y
        // esos tres de más son suyos, no una traducción nuestra.
        using var sqlServer = ContextoSqlServer();
        using var postgres = ContextoPostgres();

        static Dictionary<string, string> Filtrados(UmsPlatformDbContext c) => c.Model.GetEntityTypes()
            .SelectMany(e => e.GetIndexes())
            .Where(i => !string.IsNullOrWhiteSpace(i.GetFilter()))
            .ToDictionary(
                i => $"{i.DeclaringEntityType.ShortName()}[{string.Join(',', i.Properties.Select(p => p.Name))}]",
                i => i.GetFilter()!,
                StringComparer.Ordinal);

        var enPostgres = Filtrados(postgres);
        var enSqlServer = Filtrados(sqlServer);

        Assert.True(enPostgres.Count > 0, "el modelo debería tener índices parciales; si no, esta prueba no mide nada");

        var perdidos = enPostgres.Keys.Where(k => !enSqlServer.ContainsKey(k)).ToList();
        Assert.True(perdidos.Count == 0,
            "índices que pierden su predicado bajo SQL Server: " + string.Join(" · ", perdidos));
    }

    [Fact]
    public void ElGuionDeCreacion_SaleEnSintaxisDeSqlServer()
    {
        // La comprobación de conjunto: el DDL completo que EF generaría para este motor. Atrapa lo
        // que las anteriores miran por partes y, además, cualquier resto de sintaxis de PostgreSQL
        // que se colara en el modelo compartido (`gen_random_bytes` para el rowversion, por ejemplo).
        using var contexto = ContextoSqlServer();

        var guion = contexto.Database.GenerateCreateScript();

        Assert.Contains("CREATE TABLE", guion, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("gen_random_bytes", guion, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ON CONFLICT", guion, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Sin inquilino en contexto: el filtro global no se aplica (vista de sistema).</summary>
    private sealed class ContextoDeSistema : ITenantContext
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
