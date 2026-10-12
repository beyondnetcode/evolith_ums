import re
import sys

file_path = "/Users/beyondnet/Source/ums/src/apps/ums.api/Evolith.Ums.Infrastructure/DependencyInjection.cs"
with open(file_path, "r") as f:
    content = f.read()

# 1. Add Npgsql to Validation
content = content.replace(
    'Persistence.Provider is SqlServer or Sqlite."',
    'Persistence.Provider is SqlServer, Sqlite or PostgreSql."'
)
content = content.replace(
    'options.Provider == PersistenceProvider.InMemory\n                    || !string.IsNullOrWhiteSpace(configuration.GetConnectionString("DefaultConnection"))',
    'options.Provider == PersistenceProvider.InMemory\n                    || !string.IsNullOrWhiteSpace(configuration.GetConnectionString("DefaultConnection"))'
)

# 2. Add IUnitOfWorkScope
content = content.replace(
    'if (persistence.Provider == PersistenceProvider.SqlServer || persistence.Provider == PersistenceProvider.Sqlite)',
    'if (persistence.Provider == PersistenceProvider.SqlServer || persistence.Provider == PersistenceProvider.Sqlite || persistence.Provider == PersistenceProvider.PostgreSql)'
)

# 3. Add DbContext
postgres_block = """        else if (persistence.Provider == PersistenceProvider.PostgreSql)
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection must be configured for PostgreSQL persistence.");

            services.AddScoped<OrganizationDbContextInterceptor>();
            services.AddScoped<AuditSaveChangesInterceptor>();

            services.AddResiliencePipeline("ums-postgres", pipelineBuilder =>
            {
                pipelineBuilder
                    .AddRetry(new Polly.Retry.RetryStrategyOptions
                    {
                        MaxRetryAttempts = 3,
                        Delay = TimeSpan.FromMilliseconds(200),
                        BackoffType = Polly.DelayBackoffType.Exponential,
                    })
                    .AddCircuitBreaker(new Polly.CircuitBreaker.CircuitBreakerStrategyOptions
                    {
                        FailureRatio    = 0.5,
                        SamplingDuration = TimeSpan.FromSeconds(30),
                        MinimumThroughput = 10,
                        BreakDuration    = TimeSpan.FromSeconds(60),
                    });
            });

            services.AddDbContext<UmsPlatformDbContext>((serviceProvider, options) =>
            {
                options.UseNpgsql(connectionString, pgOptions =>
                {
                    pgOptions.MigrationsHistoryTable("__EFMigrationsHistory", UmsPlatformDbContext.DefaultSchema);
                    pgOptions.EnableRetryOnFailure(3);
                });

                options.AddInterceptors(
                    serviceProvider.GetRequiredService<OrganizationDbContextInterceptor>(),
                    serviceProvider.GetRequiredService<AuditSaveChangesInterceptor>());
            });
        }
"""
content = content.replace(
    '        if ((persistence.Provider == PersistenceProvider.SqlServer && persistence.UseSqlServerIdentityStores) ||',
    postgres_block + '\n        if ((persistence.Provider == PersistenceProvider.SqlServer && persistence.UseSqlServerIdentityStores) ||'
)

# 4. Add to repository registrations
content = content.replace(
    '(persistence.Provider == PersistenceProvider.Sqlite && persistence.UseSqliteIdentityStores))',
    '(persistence.Provider == PersistenceProvider.Sqlite && persistence.UseSqliteIdentityStores) ||\n            (persistence.Provider == PersistenceProvider.PostgreSql && persistence.UsePostgreSqlIdentityStores))'
)
content = content.replace(
    '(persistence.Provider == PersistenceProvider.Sqlite && persistence.UseSqliteAuthorizationStores))',
    '(persistence.Provider == PersistenceProvider.Sqlite && persistence.UseSqliteAuthorizationStores) ||\n            (persistence.Provider == PersistenceProvider.PostgreSql && persistence.UsePostgreSqlAuthorizationStores))'
)
content = content.replace(
    '(persistence.Provider == PersistenceProvider.Sqlite && persistence.UseSqliteConfigurationStores))',
    '(persistence.Provider == PersistenceProvider.Sqlite && persistence.UseSqliteConfigurationStores) ||\n            (persistence.Provider == PersistenceProvider.PostgreSql && persistence.UsePostgreSqlConfigurationStores))'
)
content = content.replace(
    '(persistence.Provider == PersistenceProvider.Sqlite && persistence.UseSqliteApprovalsStores))',
    '(persistence.Provider == PersistenceProvider.Sqlite && persistence.UseSqliteApprovalsStores) ||\n            (persistence.Provider == PersistenceProvider.PostgreSql && persistence.UsePostgreSqlApprovalsStores))'
)

# 5. Add NpgSql Health Checks
health_check_block = """            if (persistence.Provider == PersistenceProvider.PostgreSql)
            {
                var connectionString = configuration.GetConnectionString("DefaultConnection");
                if (!string.IsNullOrWhiteSpace(connectionString))
                {
                    builder.AddNpgSql(
                        connectionString,
                        name: "postgresql",
                        tags: ["ready", "db"]);
                }
            }
"""
content = content.replace(
    '// Outbox backlog monitor',
    health_check_block + '\n            // Outbox backlog monitor'
)

with open(file_path, "w") as f:
    f.write(content)
print("DependencyInjection.cs updated successfully.")
