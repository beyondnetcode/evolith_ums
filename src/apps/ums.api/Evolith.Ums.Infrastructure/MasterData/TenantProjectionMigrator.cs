using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Evolith.Ums.Infrastructure.MasterData;

/// <summary>
/// Applies the Tenant-projection migrations at startup (self-contained; no host wiring).
/// The platform context is migrated in <c>InitializeUmsPlatformAsync</c>, but the isolated
/// projection context (<see cref="TenantProjectionDbContext"/>) had no migrator — on a fresh
/// deploy <c>masterdata.tenant_projection</c> and the MassTransit InboxState/OutboxState tables
/// would not exist and the consumer would fail on its first message. Mirrors Tracker's interim
/// migrator; DS-08 replaces this with a migrate-Job Helm hook once replicas>1 (startup
/// migrations race). Registered only when a broker is configured (see DependencyInjection).
/// </summary>
public sealed class TenantProjectionMigrator : IHostedService
{
    private readonly IServiceProvider _services;

    public TenantProjectionMigrator(IServiceProvider services) => _services = services;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TenantProjectionDbContext>();
        await db.Database.MigrateAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
