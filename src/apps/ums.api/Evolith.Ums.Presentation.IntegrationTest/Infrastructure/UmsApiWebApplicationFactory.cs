using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Evolith.Ums.Domain.Audit.AuditRecord;
using Evolith.Ums.Domain.Approvals;
using Evolith.Ums.Domain.Enums;
using Evolith.Ums.Infrastructure.MasterData;
using Evolith.Ums.Infrastructure.Persistence;
using Evolith.Ums.Infrastructure.Persistence.Audit;
using Evolith.Ums.Infrastructure.Persistence.Options;
using Evolith.Ums.Presentation;

namespace Evolith.Ums.Presentation.IntegrationTest.Infrastructure;

public sealed class UmsApiWebApplicationFactory : WebApplicationFactory<Program>
{
    // Una base POR FÁBRICA, y no la misma para todos ([[G-285]]).
    //
    // Los nombres eran fijos —`TestDb`, `TestProjectionDb`, `TestReadModelDb`—, y EF cachea el
    // proveedor interno de InMemory por la huella de las opciones: dos hosts con el mismo nombre
    // comparten el MISMO almacén. Como cada clase de prueba levanta su propio host y xUnit las corre
    // en paralelo, todas escribían en la misma base y la guarda de idempotencia de la siembra hacía
    // el resto: el segundo host veía «ya sembrado» mientras el primero seguía sembrando, y sus
    // pruebas entraban contra una base a medias. Se veía como `login.Permissions` vacío —el usuario
    // existe, sus permisos todavía no— en pruebas que en aislamiento pasan siempre.
    //
    // El sufijo y la raíz propia aíslan cada host: se paga sembrar una vez por clase a cambio de que
    // el verde deje de depender del orden.
    private readonly string _sufijoDeBase = Guid.NewGuid().ToString("N")[..8];
    private readonly Microsoft.EntityFrameworkCore.Storage.InMemoryDatabaseRoot _raizDeBases = new();

    // Sin overrides globales: cada host de test recibe su configuración de forma
    // AISLADA vía ConfigureAppConfiguration (más abajo). Antes, un constructor
    // estático fijaba variables de entorno del PROCESO (`Persistence__Provider=
    // InMemory`) que se filtraban a los hosts de PostgreSqlWebApplicationFactory y
    // dejaban su UmsPlatformDbContext sin registrar — la causa raíz de ~80 fallos
    // de integración (G-014).
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, configurationBuilder) =>
        {
            configurationBuilder.Sources.Clear();
            configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Persistence:Provider"] = PersistenceProvider.InMemory.ToString(),
                ["Persistence:AggregateStoreMode"] = AggregateStoreMode.InMemory.ToString(),
                ["Persistence:UsePostgreSqlIdentityStores"] = bool.FalseString,
                ["Persistence:UsePostgreSqlAuthorizationStores"] = bool.FalseString,
                ["Persistence:UsePostgreSqlConfigurationStores"] = bool.FalseString,
                ["Persistence:SeedDevData"] = bool.TrueString,
                ["Persistence:EnableOutbox"] = bool.FalseString,
                ["Persistence:InitializePlatformStoreOnStartup"] = bool.FalseString,
                ["Jwt:Issuer"] = "ums-api",
                ["Jwt:Audience"] = "ums-web-app",
                ["Jwt:ExpirationMinutes"] = "60",
                ["Jwt:RefreshTokenExpirationDays"] = "7",
                ["Secrets:Source"] = "AppSettings",
                // El atajo de Admin Root esta APAGADO en este anfitrion, que es donde corre el
                // grueso de la suite (G-280). Es decir: estas pruebas atraviesan los manejadores
                // guardados por sus PERMISOS, no por la claim `is_internal_admin`.
                //
                // Costo llegar aqui. Con el apagado se paso de 163 rojas a 36, y las 36 tenian una
                // sola causa que solo se vio al dejar de fallar en silencio: el contexto de
                // inquilino se quedaba en el de la cabecera mientras el grafo se construia con el
                // del usuario, asi que el rol de su perfil no resolvia ([[G-286]]).
                ["Authorization:InternalAdminBypass"] = bool.FalseString,
                ["AllowedOrigins"] = "https://localhost",
            });
        });

        builder.ConfigureServices(services =>
        {
            // G-014 (causa a, 2.ª capa): antes se añadía aquí AddMassTransitTestHarness(), que registra
            // un SEGUNDO bus de MassTransit y sustituye al de producción SIN su ConfigureJsonSerializerOptions
            // (DependencyInjection.ConfigurePayload). Ese saneamiento elimina la propiedad IMetadata
            // (BeyondNetCode.Shell.Ddd) del payload; sin él, publicar CUALQUIER evento de dominio lanza
            // SerializationException ("Exception creating proxy ... IMetadata ... does not have an
            // implementation"), lo que hacía fallar los seeders y devolver 400 en todo endpoint que emite
            // eventos —incluido el flujo IGA (G-052)—. Ningún test consume ITestHarness, y la factoría de
            // PostgreSQL ya opera sin arnés sobre el bus in-memory de producción; se retira el arnés para
            // que el host InMemory use ese mismo bus (con ConfigurePayload) y la publicación no rompa.

            // G-014 (causa a, 1.ª capa): en el host InMemory coexistían DOS proveedores EF Core en el mismo
            // ServiceProvider —el InMemory de UmsPlatformDbContext (que registra esta factoría) y el
            // Npgsql que AddInfrastructure registra SIEMPRE, de forma incondicional, para
            // TenantProjectionDbContext (proyección MMS, ADR-0083/ADR-0107)—. EF Core lo detecta al
            // materializar cualquier DbContext y lanza "Only a single database provider can be
            // registered", lo que ABORTA todos los seeders (RunSeederAsync traga la excepción) y deja
            // la base sin datos semilla: de ahí la cascada de fallos, incluido el E2E de IGA (G-052)
            // que depende del seed de RoleMaturityStatus.
            //
            // Fix (solo test-infra): se retira el conjunto COMPLETO de descriptores de cada DbContext
            // —opciones genéricas, la configuración de opciones específica del proveedor (EF Core 9+) y
            // el propio contexto— y se re-registran AMBOS sobre InMemory, de modo que NO quede ningún
            // servicio del proveedor Npgsql en el contenedor cuando el host es InMemory.
            ReplaceDbContextWithInMemory<UmsPlatformDbContext>(services, $"TestDb-{_sufijoDeBase}");
            ReplaceDbContextWithInMemory<TenantProjectionDbContext>(services, $"TestProjectionDb-{_sufijoDeBase}");
            // G-014 (causa raíz del CUELGUE en CI): el `ReadModelDbContext` (proyecciones de fase 1)
            // seguía registrado con Npgsql apuntando a `127.0.0.1:5433` (appsettings.Development). En
            // el host InMemory, cualquier proyección (p. ej. `PermissionTemplatePublishedEvent`) hacía
            // que ese contexto intentara CONECTAR a PostgreSQL: en máquinas con un Postgres en 5433 el
            // fallo se enmascaraba, pero en un entorno limpio (runner de CI, contenedor) da «Connection
            // refused» y, con `EnableRetryOnFailure(3)`, se cuelga en reintentos. Se re-registra también
            // sobre InMemory para que el host InMemory NO toque PostgreSQL en absoluto.
            ReplaceDbContextWithInMemory<Evolith.Ums.ReadModels.ReadModelDbContext>(services, $"TestReadModelDb-{_sufijoDeBase}");

            services.RemoveAll<ITenantRepository>();
            services.RemoveAll<IUserAccountRepository>();
            services.RemoveAll<IUserManagementDelegationRepository>();
            services.RemoveAll<IProfileRepository>();
            services.RemoveAll<IAppConfigurationRepository>();
            services.RemoveAll<IFeatureFlagRepository>();
            services.RemoveAll<IIdpConfigurationRepository>();
            services.RemoveAll<IAuditRecordRepository>();
            services.RemoveAll<IApprovalWorkflowRepository>();
            services.RemoveAll<IApprovalRequestRepository>();
            services.RemoveAll<INotificationRuleRepository>();
            services.RemoveAll<IUnitOfWorkScope>();

            services.RemoveAll<InMemoryTenantRepository>();
            services.RemoveAll<InMemoryUserAccountRepository>();
            services.RemoveAll<InMemoryUserManagementDelegationRepository>();
            services.RemoveAll<InMemoryProfileRepository>();
            services.RemoveAll<InMemoryAppConfigurationRepository>();
            services.RemoveAll<InMemoryFeatureFlagRepository>();
            services.RemoveAll<InMemoryIdpConfigurationRepository>();
            services.RemoveAll<InMemoryAuditRecordRepository>();
            services.RemoveAll<InMemoryApprovalWorkflowRepository>();
            services.RemoveAll<InMemoryApprovalRequestRepository>();
            services.RemoveAll<InMemoryNotificationRuleRepository>();

            services.AddSingleton<InMemoryTenantRepository>();
            services.AddSingleton<ITenantRepository>(sp => sp.GetRequiredService<InMemoryTenantRepository>());

            services.AddSingleton<InMemoryUserAccountRepository>();
            services.AddSingleton<IUserAccountRepository>(sp => sp.GetRequiredService<InMemoryUserAccountRepository>());

            services.AddSingleton<InMemoryUserManagementDelegationRepository>();
            services.AddSingleton<IUserManagementDelegationRepository>(sp => sp.GetRequiredService<InMemoryUserManagementDelegationRepository>());

            services.AddSingleton<InMemoryProfileRepository>();
            services.AddSingleton<IProfileRepository>(sp => sp.GetRequiredService<InMemoryProfileRepository>());

            services.AddSingleton<InMemoryAppConfigurationRepository>();
            services.AddSingleton<IAppConfigurationRepository>(sp => sp.GetRequiredService<InMemoryAppConfigurationRepository>());

            services.AddSingleton<InMemoryFeatureFlagRepository>();
            services.AddSingleton<IFeatureFlagRepository>(sp => sp.GetRequiredService<InMemoryFeatureFlagRepository>());

            services.AddSingleton<InMemoryIdpConfigurationRepository>();
            services.AddSingleton<IIdpConfigurationRepository>(sp => sp.GetRequiredService<InMemoryIdpConfigurationRepository>());

            services.AddSingleton<InMemoryAuditRecordRepository>();
            services.AddSingleton<IAuditRecordRepository>(sp => sp.GetRequiredService<InMemoryAuditRecordRepository>());

            services.AddSingleton<InMemoryApprovalWorkflowRepository>();
            services.AddSingleton<IApprovalWorkflowRepository>(sp => sp.GetRequiredService<InMemoryApprovalWorkflowRepository>());

            services.AddSingleton<InMemoryApprovalRequestRepository>();
            services.AddSingleton<IApprovalRequestRepository>(sp => sp.GetRequiredService<InMemoryApprovalRequestRepository>());

            services.AddSingleton<InMemoryNotificationRuleRepository>();
            services.AddSingleton<INotificationRuleRepository>(sp => sp.GetRequiredService<InMemoryNotificationRuleRepository>());

            services.AddSingleton<IUnitOfWorkScope, NoOpUnitOfWorkScope>();

            // G-014 (residual): el health check "postgresql" (AddInfrastructureHealthChecks) se
            // registra según el Provider capturado al construir los servicios y no ve el override
            // InMemory de esta factoría (mismo pitfall de timing que la causa b.1). En el host
            // InMemory no hay PostgreSQL a la escucha en 127.0.0.1:5432, así que ese check queda
            // Unhealthy y /health responde 503, rompiendo HealthEndpointTests y los de
            // SessionTrackingMiddleware (que consultan /health). Se retira el registro del check
            // postgresql —análogo al retiro de los DbContext Npgsql— para que /health refleje solo
            // los checks efectivamente aplicables al host InMemory.
            services.PostConfigure<HealthCheckServiceOptions>(options =>
            {
                var postgresqlCheck = options.Registrations
                    .FirstOrDefault(registration => registration.Name == "postgresql");
                if (postgresqlCheck is not null)
                {
                    options.Registrations.Remove(postgresqlCheck);
                }
            });
        });
    }

    // Retira todo lo que dejó un AddDbContext<TContext> previo (incluida la configuración de opciones
    // específica del proveedor — IDbContextOptionsConfiguration<T>, EF Core 9+) y re-registra el
    // contexto sobre InMemory. Así el proveedor anterior (p. ej. Npgsql) deja de estar presente y no
    // colisiona con InMemory en el mismo ServiceProvider (G-014, causa a).
    private void ReplaceDbContextWithInMemory<TContext>(IServiceCollection services, string databaseName)
        where TContext : DbContext
    {
        services.RemoveAll<TContext>();
        services.RemoveAll<DbContextOptions<TContext>>();
        services.RemoveAll<Microsoft.EntityFrameworkCore.Infrastructure.IDbContextOptionsConfiguration<TContext>>();
        // La raíz explícita, además del nombre único: es la que garantiza que el almacén no se
        // comparta aunque dos fábricas coincidieran en el nombre ([[G-285]]).
        services.AddDbContext<TContext>(options => options.UseInMemoryDatabase(databaseName, _raizDeBases));
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);
        SeedConfigurationAggregates(host.Services);
        SeedApprovalAggregates(host.Services);
        return host;
    }

    private static void SeedConfigurationAggregates(IServiceProvider services)
    {
        using var scope = services.CreateScope();

        var appConfigurationRepository = scope.ServiceProvider.GetRequiredService<InMemoryAppConfigurationRepository>();
        var featureFlagRepository = scope.ServiceProvider.GetRequiredService<InMemoryFeatureFlagRepository>();
        var idpConfigurationRepository = scope.ServiceProvider.GetRequiredService<InMemoryIdpConfigurationRepository>();

        var actor = ActorId.Create("00000000-0000-0000-0000-000000000111");
        var tenantId = TenantId.Load(Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6"));
        var systemSuiteId = SystemSuiteId.Load(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        var moduleId = IdValueObject.Load(Guid.Parse("22222222-2222-2222-2222-222222222222"));

        if (featureFlagRepository.GetBySystemSuiteAndCodeAsync(systemSuiteId.GetValue(), "tenant_dashboard_enabled").GetAwaiter().GetResult() is null)
        {
            var featureFlag = FeatureFlag.Create(
                systemSuiteId,
                null,
                "tenant_dashboard_enabled",
                FlagType.Boolean,
                "tenant-console",
                LinkedResourceType.Module,
                moduleId,
                null,
                actor).Value;

            featureFlag.Activate(actor);
            featureFlagRepository.Seed(featureFlag);
        }

        if (appConfigurationRepository.GetByScopeAndCodeAsync(tenantId.GetValue(), systemSuiteId.GetValue(), moduleId.GetValue(), "session_timeout_minutes").GetAwaiter().GetResult() is null)
        {
            var appConfiguration = AppConfiguration.Create(
                tenantId,
                systemSuiteId,
                moduleId,
                Code.Create("session_timeout_minutes"),
                ConfigurationValue.Create("30"),
                Description.Create("Session timeout in minutes for tenant console."),
                true,
                false,
                actor).Value;

            appConfiguration.Publish(actor);
            appConfigurationRepository.Seed(appConfiguration);
        }

        if (idpConfigurationRepository.GetByTenantIdAsync(tenantId.GetValue()).GetAwaiter().GetResult().Count == 0)
        {
            var idpConfiguration = IdpConfiguration.Create(
                tenantId,
                systemSuiteId,
                ProviderType.AzureAd,
                ["beyondnet.com", "ums.local"],
                "{\"authority\":\"https://login.microsoftonline.com/common\"}",
                "kv/idp/azuread",
                10,
                null,
                actor).Value;

            idpConfiguration.Activate(actor);
            idpConfigurationRepository.Seed(idpConfiguration);
        }

        var notificationRuleRepository = scope.ServiceProvider.GetRequiredService<InMemoryNotificationRuleRepository>();
        if (notificationRuleRepository.GetByTenantIdAsync(tenantId.GetValue()).GetAwaiter().GetResult().Count == 0)
        {
            var notificationRule = Evolith.Ums.Domain.Approvals.NotificationRule.NotificationRule.Create(
                tenantId,
                NotificationChannel.Email,
                TextValueObject.Create("alerts@beyondnet.com"),
                actor).Value;

            notificationRuleRepository.Seed(notificationRule);
        }
    }

    private static void SeedApprovalAggregates(IServiceProvider services)
    {
        using var scope = services.CreateScope();

        var workflowRepository = scope.ServiceProvider.GetRequiredService<InMemoryApprovalWorkflowRepository>();
        var actor = ActorId.Create("00000000-0000-0000-0000-000000000111");
        var tenantId = TenantId.Load(Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6"));

        if (workflowRepository.GetAllAsync().GetAwaiter().GetResult().Count > 0)
        {
            return;
        }

        var manualWorkflow = Evolith.Ums.Domain.Approvals.ApprovalWorkflow.ApprovalWorkflow.Create(
            tenantId,
            Code.Create("manual-approval"),
            Name.Create("Manual Approval"),
            Description.Create("Workflow requiring manual approval."),
            UserCategory.Internal,
            true,
            null,
            actor,
            requiredDocumentCount: 1).Value;
        SetAggregateId(manualWorkflow.Props, Guid.Parse("88888888-1111-1111-1111-111111111111"));
        workflowRepository.Seed(manualWorkflow);

        var autoWorkflow = Evolith.Ums.Domain.Approvals.ApprovalWorkflow.ApprovalWorkflow.Create(
            tenantId,
            Code.Create("auto-approval"),
            Name.Create("Auto Approval"),
            Description.Create("Workflow that auto-approves requests."),
            UserCategory.Internal,
            false,
            null,
            actor).Value;
        SetAggregateId(autoWorkflow.Props, Guid.Parse("88888888-2222-2222-2222-222222222222"));
        workflowRepository.Seed(autoWorkflow);
    }

    private static void SetAggregateId(object props, Guid id)
    {
        var idProperty = props.GetType().GetProperty("Id");
        idProperty!.SetValue(props, IdValueObject.Load(id));
    }
}
