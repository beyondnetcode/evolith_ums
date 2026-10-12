using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Evolith.Ums.Infrastructure.MasterData;

/// <summary>Design-time factory for `dotnet ef migrations` on the Tenant projection context.</summary>
public sealed class TenantProjectionDbContextFactory : IDesignTimeDbContextFactory<TenantProjectionDbContext>
{
    public TenantProjectionDbContext CreateDbContext(string[] args)
    {
        var builder = new DbContextOptionsBuilder<TenantProjectionDbContext>();

#pragma warning disable S2068 // Cadenas de DISEÑO (EF Core tooling), solo locales; el runtime resuelve por configuración. No son secretos de producción.
        if (Persistence.MigrationsDesignTime.EsSqlServer)
            builder.UseSqlServer(
                "Server=localhost;Database=ums;Trusted_Connection=True;TrustServerCertificate=True",
                o => o.MigrationsAssembly(Persistence.MigrationsDesignTime.EnsambladoSqlServer));
        else
            builder.UseNpgsql("Host=localhost;Port=5432;Database=ums;Username=postgres;Password=postgres");
#pragma warning restore S2068

        return new TenantProjectionDbContext(builder.Options);
    }
}
