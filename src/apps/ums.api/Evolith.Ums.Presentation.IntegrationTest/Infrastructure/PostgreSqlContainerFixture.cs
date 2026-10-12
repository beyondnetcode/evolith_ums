using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Evolith.Ums.Infrastructure.Persistence;

namespace Evolith.Ums.Presentation.IntegrationTest.Infrastructure;

/// <summary>
/// REC-15: xUnit v3 collection fixture that starts a SQL Server Testcontainer once
/// and bootstraps the UMS platform schema.  Shared across all tests in the
/// <c>[Collection("PostgreSql")]</c> collection — container spins up once and is
/// discarded at the end of the test run.
///
/// If Docker is unavailable the fixture sets <see cref="IsAvailable"/> = <c>false</c>
/// so individual tests can call <c>Skip.If(!fixture.IsAvailable, "Docker required")</c>
/// instead of failing with a raw exception.
///
/// xUnit v3 requires <see cref="ValueTask"/> on <see cref="IAsyncLifetime"/> methods.
/// </summary>
public sealed class PostgreSqlContainerFixture : IAsyncLifetime
{
    // Deferred — built inside InitializeAsync to avoid throwing in the constructor
    // when Docker is not reachable (e.g. on machines without Docker Desktop).
    private PostgreSqlContainer? _container;

    /// <summary>Full ADO.NET connection string pointing at the container database.</summary>
    public string ConnectionString { get; private set; } = string.Empty;

    /// <summary>
    /// <c>true</c> when the SQL Server container started successfully and the schema
    /// was bootstrapped.  Tests should call <c>Skip.If(!IsAvailable, "Docker required")</c>.
    /// </summary>
    public bool IsAvailable { get; private set; }

    public async ValueTask InitializeAsync()
    {
        try
        {
            _container = new PostgreSqlBuilder()
                .WithImage("postgres:15-alpine")
                .WithAutoRemove(true)
                .Build();

            await _container.StartAsync();

            ConnectionString = _container.GetConnectionString();

            // Bootstrap the UMS platform schema via EF Core migrations, as production does.
            var options = new DbContextOptionsBuilder<UmsPlatformDbContext>()
                .UseNpgsql(ConnectionString, sql => sql.EnableRetryOnFailure(3))
                .Options;

            await using (var ctx = new UmsPlatformDbContext(options, new SystemTenantContext(), new Moq.Mock<MediatR.IPublisher>().Object, Microsoft.Extensions.Logging.Abstractions.NullLogger<UmsPlatformDbContext>.Instance))
            {
                await ctx.Database.MigrateAsync();
            }

            // Los READ MODELS y la proyeccion de maestros viven en la misma base y tienen sus
            // propias migraciones. Migrar solo la plataforma dejaba sus tablas sin crear, y la
            // SIEMBRA —que publica eventos y los proyecta— moria contra `relation
            // "Authorization.PermissionTemplateReadModels" does not exist`. Sin ellas, esta base no
            // se puede sembrar, y sin sembrar no hay identidad a la que conceder permisos: es lo que
            // ataba a este anfitrion al atajo de Admin Root ([[G-280]]).
            var opcionesDeLectura = new DbContextOptionsBuilder<Evolith.Ums.ReadModels.ReadModelDbContext>()
                .UseNpgsql(ConnectionString, sql => sql.EnableRetryOnFailure(3))
                .Options;
            await using (var lectura = new Evolith.Ums.ReadModels.ReadModelDbContext(opcionesDeLectura))
            {
                await lectura.Database.MigrateAsync();
            }

            var opcionesDeProyeccion = new DbContextOptionsBuilder<Evolith.Ums.Infrastructure.MasterData.TenantProjectionDbContext>()
                .UseNpgsql(ConnectionString, sql => sql.EnableRetryOnFailure(3))
                .Options;
            await using (var proyeccion = new Evolith.Ums.Infrastructure.MasterData.TenantProjectionDbContext(opcionesDeProyeccion))
            {
                await proyeccion.Database.MigrateAsync();
            }

            IsAvailable = true;
        }
        catch (Exception ex)
        {
            await System.IO.File.WriteAllTextAsync("testcontainers-error.log", "Testcontainers failed: " + ex.ToString());
            IsAvailable = false;
            throw; // Fail the test run immediately if the DB cannot start
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Null tenant context → no global query filter applied (system / admin view)
    // ─────────────────────────────────────────────────────────────────────────
    private sealed class SystemTenantContext : ITenantContext
    {
        public Guid? OrganizationId  => null;
        public Guid? OriginalTenantId => null;
        public bool  IsInternalAdmin  => true;
        public void Initialize(Guid userTenantId, bool isInternalAdmin) { }
        public void SetOrganizationId(Guid organizationId) { }
        public void EnableCrossTenantAccess() { }
        public void DisableCrossTenantAccess() { }
    }
}
