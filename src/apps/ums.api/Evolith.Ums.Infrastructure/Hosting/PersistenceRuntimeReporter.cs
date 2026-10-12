using Evolith.Ums.Infrastructure.Persistence.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Evolith.Ums.Infrastructure.Hosting;

public sealed class PersistenceRuntimeReporter(
    IOptions<PersistenceOptions> persistenceOptions,
    ILogger<PersistenceRuntimeReporter> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        var options = persistenceOptions.Value;

        logger.LogInformation(
            "UMS persistence configured with provider {Provider}, aggregate store mode {AggregateStoreMode}, PostgreSQL identity stores {UsePostgreSqlIdentityStores}, PostgreSQL authorization stores {UsePostgreSqlAuthorizationStores}, outbox enabled {EnableOutbox}.",
            options.Provider,
            options.AggregateStoreMode,
            options.UsePostgreSqlIdentityStores,
            options.UsePostgreSqlAuthorizationStores,
            options.EnableOutbox);

        if (options.Provider == PersistenceProvider.PostgreSql
            && options.AggregateStoreMode == AggregateStoreMode.InMemory
            && !options.UsePostgreSqlIdentityStores)
        {
            logger.LogWarning(
                "PostgreSQL is configured as the platform provider, but aggregate repositories still run in-memory. This is a valid transitional modular-monolith mode, not the final production persistence model.");
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
