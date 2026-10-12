using Evolith.Ums.Application.Common.Interfaces;
using Evolith.Ums.ContractTest.Provider;
using Evolith.Ums.Domain.Approvals;
using Evolith.Ums.Domain.Audit;
using Evolith.Ums.Domain.Audit.AuditRecord;
using Evolith.Ums.Domain.Authorization;
using Evolith.Ums.Domain.Configuration;
using Evolith.Ums.Domain.Identity;
using Evolith.Ums.Infrastructure.Persistence;
using Evolith.Ums.Infrastructure.Persistence.Audit;
using Evolith.Ums.Infrastructure.Persistence.Options;
using Evolith.Ums.Presentation;
using Evolith.Ums.ReadModels;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Evolith.Ums.ContractTest.Infrastructure;

/// <summary>
/// WebApplicationFactory for provider verification tests.
/// Starts the UMS API with InMemory stores so PactNet consumer tests can verify
/// contracts against a live server.
/// </summary>
public class ContractTestWebApplicationFactory : WebApplicationFactory<Program>
{
    static ContractTestWebApplicationFactory()
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT",              "Development");
        Environment.SetEnvironmentVariable("Persistence__Provider",               "InMemory");
        Environment.SetEnvironmentVariable("Persistence__SeedDevData",                     "true");
        Environment.SetEnvironmentVariable("Persistence__EnableOutbox",                    "false");
        Environment.SetEnvironmentVariable("Persistence__InitializePlatformStoreOnStartup","false");
        // Un host levantado en «Production» exige una clave de firma REAL: fuera de desarrollo no
        // hay par efímero de respaldo (ADR-0157 §4.7), y sin esto la factoría productiva no
        // arranca.
        //
        // Va como VARIABLE DE ENTORNO y no en el diccionario de `ConfigureAppConfiguration`: ese
        // diccionario lo pisa `appsettings.json`, que la construcción del host añade después.
        Environment.SetEnvironmentVariable("Jwt__PrivateKeyPem", ClaveDeFirmaDePrueba);
    }

    /// <summary>
    /// Entorno del host bajo prueba. Por defecto «Development» —el entorno que el
    /// proveedor de Pact necesita para que se registre `/_pact/provider-states` (G-101)—.
    /// Las pruebas que verifican el blindaje productivo lo sobrescriben con «Production».
    /// </summary>
    protected virtual string EnvironmentName => "Development";

    /// <summary>
    /// Clave de firma para la corrida. Se genera al azar en cada arranque: fijarla aquí la
    /// convertiría en una clave privada versionada, que es justo lo que ADR-0157 §4.7 prohíbe
    /// y el defecto que G-203 corrige.
    /// </summary>
    private static readonly string ClaveDeFirmaDePrueba = GenerarClaveRsaPem();

    private static string GenerarClaveRsaPem()
    {
        using var rsa = System.Security.Cryptography.RSA.Create(2048);
        return rsa.ExportRSAPrivateKeyPem();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(EnvironmentName);

        builder.ConfigureAppConfiguration((_, cfg) =>
        {
            cfg.Sources.Clear();
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Persistence:Provider"]                        = PersistenceProvider.InMemory.ToString(),
                ["Persistence:SeedDevData"]                     = bool.TrueString,
                ["Persistence:EnableOutbox"]                    = bool.FalseString,
                ["Persistence:InitializePlatformStoreOnStartup"]= bool.FalseString,
                ["Secrets:Source"]                              = "AppSettings",
                ["AllowedOrigins"]                              = "https://localhost",
            });
        });

        builder.ConfigureServices(services =>
        {
            if (string.Equals(EnvironmentName, "Development", StringComparison.OrdinalIgnoreCase))
            {
                services.AddTransient<IStartupFilter, PactProviderStateStartupFilter>();
            }

            services.RemoveAll<UmsPlatformDbContext>();
            services.AddDbContext<UmsPlatformDbContext>((_, options) =>
                options.UseInMemoryDatabase("ContractTestDb"));

            // Register ReadModelDbContext with InMemory so projection handlers can be resolved.
            services.RemoveAll<ReadModelDbContext>();
            services.AddDbContext<ReadModelDbContext>((_, options) =>
                options.UseInMemoryDatabase("ContractTestReadModelDb"));

            // Swap all SQL Server stores for their InMemory equivalents.
            foreach (var t in new[]
            {
                typeof(ITenantRepository), typeof(IUserAccountRepository),
                typeof(IUserManagementDelegationRepository), typeof(IProfileRepository),
                typeof(IAppConfigurationRepository), typeof(IFeatureFlagRepository),
                typeof(IIdpConfigurationRepository), typeof(IAuditRecordRepository),
                typeof(IApprovalWorkflowRepository), typeof(IApprovalRequestRepository),
                typeof(INotificationRuleRepository), typeof(IUnitOfWorkScope),
            }) services.RemoveAll(t);

            services.AddSingleton<InMemoryTenantRepository>();
            services.AddSingleton<ITenantRepository>(sp => sp.GetRequiredService<InMemoryTenantRepository>());

            services.AddSingleton<InMemoryUserAccountRepository>();
            services.AddSingleton<IUserAccountRepository>(sp => sp.GetRequiredService<InMemoryUserAccountRepository>());

            services.AddSingleton<InMemoryUserManagementDelegationRepository>();
            services.AddSingleton<IUserManagementDelegationRepository>(sp =>
                sp.GetRequiredService<InMemoryUserManagementDelegationRepository>());

            services.AddSingleton<InMemoryProfileRepository>();
            services.AddSingleton<IProfileRepository>(sp => sp.GetRequiredService<InMemoryProfileRepository>());

            services.AddSingleton<InMemoryAppConfigurationRepository>();
            services.AddSingleton<IAppConfigurationRepository>(sp =>
                sp.GetRequiredService<InMemoryAppConfigurationRepository>());

            services.AddSingleton<InMemoryFeatureFlagRepository>();
            services.AddSingleton<IFeatureFlagRepository>(sp =>
                sp.GetRequiredService<InMemoryFeatureFlagRepository>());

            services.AddSingleton<InMemoryIdpConfigurationRepository>();
            services.AddSingleton<IIdpConfigurationRepository>(sp =>
                sp.GetRequiredService<InMemoryIdpConfigurationRepository>());

            services.AddSingleton<InMemoryAuditRecordRepository>();
            services.AddSingleton<IAuditRecordRepository>(sp =>
                sp.GetRequiredService<InMemoryAuditRecordRepository>());

            services.AddSingleton<InMemoryApprovalWorkflowRepository>();
            services.AddSingleton<IApprovalWorkflowRepository>(sp =>
                sp.GetRequiredService<InMemoryApprovalWorkflowRepository>());

            services.AddSingleton<InMemoryApprovalRequestRepository>();
            services.AddSingleton<IApprovalRequestRepository>(sp =>
                sp.GetRequiredService<InMemoryApprovalRequestRepository>());

            services.AddSingleton<InMemoryNotificationRuleRepository>();
            services.AddSingleton<INotificationRuleRepository>(sp =>
                sp.GetRequiredService<InMemoryNotificationRuleRepository>());

            services.AddSingleton<IUnitOfWorkScope, NoOpUnitOfWorkScope>();
        });
    }
}
