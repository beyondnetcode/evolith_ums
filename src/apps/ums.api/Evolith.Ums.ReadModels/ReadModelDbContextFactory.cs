using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Evolith.Ums.ReadModels;

public class ReadModelDbContextFactory : IDesignTimeDbContextFactory<ReadModelDbContext>
{
    public ReadModelDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ReadModelDbContext>();
        // Cadena de solo diseño: la usa exclusivamente el tooling de EF Core (`dotnet ef migrations`),
        // nunca el runtime — que resuelve la conexión por configuración. Misma convención que
        // UmsPlatformDbContextFactory y TenantProjectionDbContextFactory.
        //
        // El discriminador se repite aquí en vez de reutilizar MigrationsDesignTime porque este
        // proyecto NO referencia Evolith.Ums.Infrastructure: la dependencia va en sentido contrario
        // (Infrastructure → ReadModels), e invertirla por una constante crearía un ciclo.
        var esSqlServer = string.Equals(
            Environment.GetEnvironmentVariable("UMS_MIGRATIONS_PROVIDER"),
            "SqlServer",
            StringComparison.OrdinalIgnoreCase);

#pragma warning disable S2068 // Cadenas de DISEÑO (EF Core tooling), solo locales; el runtime resuelve por configuración. No son secretos de producción.
        if (esSqlServer)
            optionsBuilder.UseSqlServer(
                "Server=localhost;Database=UmsReadModel;Trusted_Connection=True;TrustServerCertificate=True",
                o => o.MigrationsAssembly("Evolith.Ums.Infrastructure.Migrations.SqlServer"));
        else
            optionsBuilder.UseNpgsql("Host=localhost;Port=5433;Database=UmsReadModel;Username=postgres;Password=postgres");
#pragma warning restore S2068

        return new ReadModelDbContext(optionsBuilder.Options);
    }
}
