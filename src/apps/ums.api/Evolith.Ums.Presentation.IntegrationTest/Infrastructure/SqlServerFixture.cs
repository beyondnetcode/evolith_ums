using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Evolith.Ums.Application.Common.Interfaces;
using Evolith.Ums.Infrastructure.MasterData;
using Evolith.Ums.Infrastructure.Persistence;

namespace Evolith.Ums.Presentation.IntegrationTest.Infrastructure;

/// <summary>
/// Una base de SQL Server recién creada, para las pruebas que solo el motor puede responder ([[G-275]]).
///
/// <para>NO usa Testcontainers, y es deliberado: la imagen oficial de SQL Server 2019 es solo
/// `amd64` y en los equipos ARM del equipo se cae bajo la carga de migrar —medido: el contenedor
/// sale con código 0 en el mismo segundo del `CREATE DATABASE`—. Así que la instancia la pone quien
/// corre las pruebas, por <c>UMS_SQLSERVER_TEST_CONNECTION</c>, y sin ella las pruebas se OMITEN en
/// vez de fallar. Una prueba que exige una máquina x86 no puede ser una prueba que rompe el árbol en
/// las demás.</para>
///
/// <para>La base se crea con nombre único por corrida y se destruye al terminar: dos corridas
/// simultáneas contra el mismo servidor no se pisan.</para>
/// </summary>
public sealed class SqlServerFixture : IAsyncLifetime
{
    /// <summary>Variable con la cadena a un SQL Server 2019+ alcanzable. Sin ella, todo se omite.</summary>
    public const string VariableDeEntorno = "UMS_SQLSERVER_TEST_CONNECTION";

    private string _cadenaDelServidor = string.Empty;
    private string _base = string.Empty;

    /// <summary>Cadena a la base de esta corrida.</summary>
    public string ConnectionString { get; private set; } = string.Empty;

    /// <summary><c>true</c> si hay servidor y la base quedó migrada.</summary>
    public bool IsAvailable { get; private set; }

    /// <summary>Por qué se omitió, para que el motivo salga en el informe y no se lea como un hueco.</summary>
    public string MotivoDeOmision { get; private set; } = string.Empty;

    public async ValueTask InitializeAsync()
    {
        _cadenaDelServidor = Environment.GetEnvironmentVariable(VariableDeEntorno) ?? string.Empty;
        if (string.IsNullOrWhiteSpace(_cadenaDelServidor))
        {
            MotivoDeOmision = $"Sin {VariableDeEntorno}: no hay SQL Server que ejercitar.";
            return;
        }

        _base = $"ums_pruebas_{Guid.NewGuid():N}"[..24];

        try
        {
            var constructor = new SqlConnectionStringBuilder(_cadenaDelServidor) { InitialCatalog = "master" };
            await using (var conexion = new SqlConnection(constructor.ConnectionString))
            {
                await conexion.OpenAsync();
                await using var comando = conexion.CreateCommand();
                // El nombre lo compone esta clase a partir de un GUID, no de entrada externa.
                comando.CommandText = $"CREATE DATABASE [{_base}]";
                await comando.ExecuteNonQueryAsync();
            }

            constructor.InitialCatalog = _base;
            ConnectionString = constructor.ConnectionString;

            var opcionesPlataforma = new DbContextOptionsBuilder<UmsPlatformDbContext>()
                .UseSqlServer(ConnectionString, o => o.MigrationsAssembly(MigrationsDesignTime.EnsambladoSqlServer))
                .Options;
            await using (var ctx = new UmsPlatformDbContext(opcionesPlataforma, new ContextoDeSistema(),
                new Moq.Mock<MediatR.IPublisher>().Object,
                Microsoft.Extensions.Logging.Abstractions.NullLogger<UmsPlatformDbContext>.Instance))
            {
                await ctx.Database.MigrateAsync();
            }

            var opcionesProyeccion = new DbContextOptionsBuilder<TenantProjectionDbContext>()
                .UseSqlServer(ConnectionString, o => o.MigrationsAssembly(MigrationsDesignTime.EnsambladoSqlServer))
                .Options;
            await using (var ctx = new TenantProjectionDbContext(opcionesProyeccion))
            {
                // `MigrateAsync`, no `EnsureCreatedAsync`: la base ya existe —la creó la migración de
                // plataforma— y `EnsureCreated` no hace nada cuando la encuentra, asi que las tablas
                // de la proyeccion no llegaban a crearse. Medido: `Invalid object name
                // 'masterdata.tenant_projection'`.
                await ctx.Database.MigrateAsync();
            }

            IsAvailable = true;
        }
        catch (Exception ex)
        {
            // Un servidor inalcanzable o lento no puede tumbar la suite entera: se omite con motivo.
            MotivoDeOmision = $"SQL Server declarado en {VariableDeEntorno} pero inalcanzable: {ex.Message}";
            IsAvailable = false;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (string.IsNullOrEmpty(_base) || string.IsNullOrWhiteSpace(_cadenaDelServidor)) return;

        try
        {
            var constructor = new SqlConnectionStringBuilder(_cadenaDelServidor) { InitialCatalog = "master" };
            await using var conexion = new SqlConnection(constructor.ConnectionString);
            await conexion.OpenAsync();
            await using var comando = conexion.CreateCommand();
            // SINGLE_USER con ROLLBACK IMMEDIATE: si algo dejó una conexión viva, el DROP se colgaría.
            comando.CommandText = $"ALTER DATABASE [{_base}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{_base}];";
            await comando.ExecuteNonQueryAsync();
        }
        catch
        {
            // Dejar una base huérfana es preferible a fallar el desmontaje de la suite.
        }
    }

    /// <summary>Contexto de plataforma sobre la base de esta corrida.</summary>
    public UmsPlatformDbContext CrearContextoDePlataforma()
    {
        var opciones = new DbContextOptionsBuilder<UmsPlatformDbContext>()
            .UseSqlServer(ConnectionString, o => o.MigrationsAssembly(MigrationsDesignTime.EnsambladoSqlServer))
            .Options;
        return new UmsPlatformDbContext(opciones, new ContextoDeSistema(),
            new Moq.Mock<MediatR.IPublisher>().Object,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<UmsPlatformDbContext>.Instance);
    }

    /// <summary>Contexto de la proyección de maestros sobre la base de esta corrida.</summary>
    public TenantProjectionDbContext CrearContextoDeProyeccion()
    {
        var opciones = new DbContextOptionsBuilder<TenantProjectionDbContext>()
            .UseSqlServer(ConnectionString, o => o.MigrationsAssembly(MigrationsDesignTime.EnsambladoSqlServer))
            .Options;
        return new TenantProjectionDbContext(opciones);
    }

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
