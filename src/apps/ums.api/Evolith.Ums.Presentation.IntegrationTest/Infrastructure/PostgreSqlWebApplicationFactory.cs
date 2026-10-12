using System;
using Evolith.Ums.Domain.Approvals;
using Evolith.Ums.Domain.Approvals.ApprovalWorkflow;
using Evolith.Ums.Domain.Approvals.NotificationRule;
using Evolith.Ums.Domain.Audit.AuditRecord;
using Evolith.Ums.Domain.Configuration;
using Evolith.Ums.Domain.Configuration.AppConfiguration;
using Evolith.Ums.Domain.Configuration.FeatureFlag;
using Evolith.Ums.Domain.Configuration.IdpConfiguration;
using Evolith.Ums.Domain.Enums;
using Evolith.Ums.Domain.Kernel.ValueObjects;
using Evolith.Ums.Infrastructure.MasterData;
using Evolith.Ums.Infrastructure.Persistence;
using Evolith.Ums.Infrastructure.Persistence.Approvals.Entities;
using Evolith.Ums.Infrastructure.Persistence.Configuration.Entities;
using Evolith.Ums.Infrastructure.Persistence.Options;
using Evolith.Ums.Presentation;
using Evolith.Ums.ReadModels;
using MassTransit;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Evolith.Ums.Presentation.IntegrationTest.Infrastructure
{
    /// <summary>
    /// REC-15: <see cref="WebApplicationFactory{TEntryPoint}"/> that wires the UMS API
    /// to the SQL Server Testcontainer started by <see cref="PostgreSqlContainerFixture"/>.
    /// </summary>
    public sealed class PostgreSqlWebApplicationFactory(string connectionString)
        : WebApplicationFactory<Program>
    {
        /// <summary>
        /// Siembra este anfitrion —y con ella, aplica el modelo de permisos— cuando la maquina puede.
        ///
        /// <para>Se enciende con <c>UMS_SEMBRAR_ANFITRION_PG=1</c>. Ver el comentario de
        /// <c>Persistence:SeedDevData</c> mas abajo: lo que lo impide por omision es la memoria de la
        /// VM de Docker, no el codigo.</para>
        /// </summary>
        internal static bool SembrarEsteAnfitrion =>
            Environment.GetEnvironmentVariable("UMS_SEMBRAR_ANFITRION_PG") == "1";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");

            builder.ConfigureAppConfiguration((_, cfg) =>
            {
                cfg.Sources.Clear();
                cfg.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    // Persistence — full SQL Server mode
                    ["Persistence:Provider"] = PersistenceProvider.PostgreSql.ToString(),
                    ["Persistence:AggregateStoreMode"] = AggregateStoreMode.PostgreSql.ToString(),
                    ["Persistence:UsePostgreSqlIdentityStores"] = bool.TrueString,
                    ["Persistence:UsePostgreSqlAuthorizationStores"] = bool.TrueString,
                    ["Persistence:UsePostgreSqlConfigurationStores"] = bool.TrueString,
                    // La siembra de este anfitrion es OPCIONAL, y la razon no es de codigo sino de
                    // memoria (G-280). Sin sembrar, el administrador no existe aqui y su identidad
                    // llega sin un solo scope —medido: 97 peticiones «sin cuenta … SIN scopes»—, asi
                    // que estas pruebas dependen del atajo de Admin Root; con el apagado, una base
                    // vacia no tiene a quien conceder permisos.
                    //
                    // Encenderla exigio primero que la fixture migrara los READ MODELS y la
                    // proyeccion de maestros —sin esas tablas la siembra muere proyectando—, y eso
                    // ya esta hecho. Lo que NO cabe es la maquina: con la siembra completa, el
                    // contenedor de PostgreSQL murio con **exit 137** —lo mato el sistema— y la
                    // corrida quedo colgada. La VM de Docker tiene 7,7 GB y los comparte con tres
                    // nodos kind y un SQL Server.
                    //
                    // Asi que se enciende por variable de entorno: donde haya memoria —o con los
                    // otros clusteres apagados— se siembra y este anfitrion puede correr tambien con
                    // el atajo apagado. Donde no la haya, la suite sigue verde y el motivo queda
                    // dicho en vez de silenciado.
                    ["Persistence:SeedDevData"] = SembrarEsteAnfitrion ? bool.TrueString : bool.FalseString,
                    ["Persistence:EnableOutbox"] = bool.FalseString,
                    ["Persistence:InitializePlatformStoreOnStartup"] = bool.FalseString,
                    ["Jwt:Issuer"] = "ums-api",
                    ["Jwt:Audience"] = "ums-web-app",
                    ["Jwt:ExpirationMinutes"] = "60",
                    ["Jwt:RefreshTokenExpirationDays"] = "7",

                    // Connection
                    ["ConnectionStrings:DefaultConnection"] = connectionString,

                    // Misc
                    ["Secrets:Source"] = "AppSettings",
                    // El atajo de Admin Root queda ENCENDIDO aqui, y no por descuido: con
                    // `InternalAdminBypass=false` esta suite pasa de 15 a 163 rojas —medido el
                    // 2026-08-20—, porque 148 de sus pruebas atraviesan los manejadores guardados por
                    // la sola claim `is_internal_admin`. Apagarlo antes de que su andamiaje tenga
                    // identidades reales no mide autorizacion: rompe la suite entera. El pendiente esta
                    // registrado en G-280 con su medicion; se apaga cuando las identidades esten.
                    // El atajo sigue la misma suerte que la siembra: sin datos sembrados no hay
                    // identidad a la que conceder permisos, asi que apagarlo aqui solo produciria
                    // rojas que no miden autorizacion. El anfitrion InMemory —el grueso de la
                    // suite— ya corre con el apagado (G-280).
                    ["Authorization:InternalAdminBypass"] = SembrarEsteAnfitrion ? bool.FalseString : bool.TrueString,
                    ["AllowedOrigins"] = "https://localhost",
                });
            });

            builder.ConfigureTestServices(services =>
            {
                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = "Test";
                    options.DefaultChallengeScheme = "Test";
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", options => { });

                // G-014 (causa b) — CAUSA REAL: el UmsPlatformDbContext (el sistema bajo prueba) acababa
                // conectando a la cadena por defecto de appsettings (Host=localhost;Database=UmsDev), NO a la
                // del Testcontainer → "Connection refused" y arranque del host roto (IntegrationTestBase cae a
                // un HttpClient vacío ⇒ TODA la clase E2E/PostgreSQL falla). Motivo: AddInfrastructure captura
                // ConnectionStrings:DefaultConnection en tiempo de REGISTRO, y el override de este factory
                // (ConfigureAppConfiguration) aún no es visible entonces —mismo pitfall de timing que la causa
                // (a)—. El host InMemory lo evita re-registrando el contexto; el host PostgreSQL nunca hizo el
                // equivalente para la cadena. Fix: re-registrar UmsPlatformDbContext y ReadModelDbContext
                // apuntando al Testcontainer real (cuya cadena es el parámetro `connectionString`, ya migrado
                // por PostgreSqlContainerFixture). Se re-registra sin interceptores, igual que el host InMemory.
                ReplaceNpgsqlDbContext<UmsPlatformDbContext>(services, connectionString);
                ReplaceNpgsqlDbContext<ReadModelDbContext>(services, connectionString);

                // Defensa: el TenantProjectionConsumer (registrado SIEMPRE por AddConsumers) consume los
                // TenantEvent que publican los E2E y escribe en TenantProjectionDbContext, que en este host
                // conserva su fallback Npgsql a localhost:5432 (no hay MasterDataDb). Una vez que el fix de
                // arriba revive el flujo E2E, esas publicaciones dispararían el consumer contra localhost:5432.
                // Se apunta la proyección MMS a InMemory (el consumer escribe ahí, inofensivo).
                services.RemoveAll<TenantProjectionDbContext>();
                services.RemoveAll<DbContextOptions<TenantProjectionDbContext>>();
                services.RemoveAll<Microsoft.EntityFrameworkCore.Infrastructure.IDbContextOptionsConfiguration<TenantProjectionDbContext>>();
                services.AddDbContext<TenantProjectionDbContext>(options => options.UseInMemoryDatabase("TestProjectionDb"));
            });
        }

        protected override IHost CreateHost(IHostBuilder builder)
        {
            var host = base.CreateHost(builder);
            // G-014 (causa b, capa 3 — aislamiento): el Testcontainer es de colección (una sola BD para
            // todos los tests) y no se reseteaba entre pruebas. Como cada test crea su propio host aquí,
            // truncamos toda la data ANTES de re-sembrar la línea base, dando a cada test una pizarra limpia
            // y eliminando los choques de clave duplicada (los tests E2E crean entidades con IDs/constraints
            // fijos que colisionaban con las del test anterior). La colección xUnit serializa sus tests, así
            // que no hay carreras sobre la BD compartida.
            ResetDatabase(host.Services);
            PostgresTestSeeder.SeedConfigurationAggregates(host.Services);
            PostgresTestSeeder.SeedApprovalAggregates(host.Services);
            return host;
        }

        // Vacía toda la data del contenedor (TRUNCATE ... CASCADE de cada tabla, preservando el historial de
        // migraciones) para aislar cada test. Dinámico: no hay que mantener la lista de tablas a mano.
        private static void ResetDatabase(IServiceProvider services)
        {
            using var scope = services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<UmsPlatformDbContext>();
            db.Database.ExecuteSqlRaw(@"
                DO $$
                DECLARE r RECORD;
                BEGIN
                    FOR r IN (
                        SELECT schemaname, tablename
                        FROM pg_tables
                        WHERE schemaname NOT IN ('pg_catalog', 'information_schema')
                          AND tablename <> '__EFMigrationsHistory'
                    )
                    LOOP
                        EXECUTE 'TRUNCATE TABLE ' || quote_ident(r.schemaname) || '.' || quote_ident(r.tablename) || ' RESTART IDENTITY CASCADE';
                    END LOOP;
                END $$;");
        }

        // Retira el registro de <typeparamref name="TContext"/> que dejó AddInfrastructure (contexto,
        // DbContextOptions<T> y la configuración de opciones específica del proveedor —EF Core 9+—, cuya
        // cadena de conexión se capturó de appsettings) y lo re-registra sobre el Npgsql del Testcontainer.
        private static void ReplaceNpgsqlDbContext<TContext>(IServiceCollection services, string connectionString)
            where TContext : DbContext
        {
            services.RemoveAll<TContext>();
            services.RemoveAll<DbContextOptions<TContext>>();
            services.RemoveAll<Microsoft.EntityFrameworkCore.Infrastructure.IDbContextOptionsConfiguration<TContext>>();
            services.AddDbContext<TContext>(options => options.UseNpgsql(connectionString, sql => sql.EnableRetryOnFailure(3)));
        }
    }

    // Shared seeder for PostgreSQL test container – uses EfCore DbContext directly
    internal static class PostgresTestSeeder
    {
        public static void SeedConfigurationAggregates(IServiceProvider services)
        {
            using var scope = services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<Evolith.Ums.Infrastructure.Persistence.UmsPlatformDbContext>();

            var actor = ActorId.Create("00000000-0000-0000-0000-000000000111");
            var tenantId = TenantId.Load(Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6"));
            var systemSuiteId = SystemSuiteId.Load(Guid.Parse("11111111-1111-1111-1111-111111111111"));
            var moduleId = IdValueObject.Load(Guid.Parse("22222222-2222-2222-2222-222222222222"));

            // Feature flag
            if (!db.Set<FeatureFlagRecord>().Any(f => f.SystemSuiteId == systemSuiteId.GetValue() && f.FlagCode == "tenant_dashboard_enabled"))
            {
                var featureFlagRecord = new FeatureFlagRecord
                {
                    Id = Guid.NewGuid(),
                    FlagCode = "tenant_dashboard_enabled",
                    FlagTypeId = FlagType.Boolean.Id,
                    FlagTargets = "tenant-console",
                    StatusId = FlagStatus.Active.Id,
                    LinkedResourceTypeId = (int?)LinkedResourceType.Module.Id,
                    LinkedResourceId = moduleId.GetValue(),
                    RolloutPercentage = null,
                    CreatedBy = actor.GetValue().ToString(),
                    CreatedAtUtc = DateTime.UtcNow,
                    SystemSuiteId = systemSuiteId.GetValue(),
                    TenantId = null
                };
                db.Set<FeatureFlagRecord>().Add(featureFlagRecord);
            }

            // App configuration
            if (!db.Set<AppConfigurationRecord>().Any(a => a.TenantId == tenantId.GetValue() && a.SystemSuiteId == systemSuiteId.GetValue() && a.ModuleId == moduleId.GetValue() && a.Code == "session_timeout_minutes"))
            {
                var appConfigRecord = new AppConfigurationRecord
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId.GetValue(),
                    SystemSuiteId = systemSuiteId.GetValue(),
                    ModuleId = moduleId.GetValue(),
                    Code = "session_timeout_minutes",
                    Value = "30",
                    Description = "Session timeout in minutes for tenant console.",
                    ScopeId = ConfigurationScope.Tenant.Id,
                    IsInheritable = true,
                    IsEncrypted = false,
                    IsNonOverridable = false,
                    Version = "1.0.0",
                    StatusId = ConfigStatus.Published.Id,
                    CreatedBy = actor.GetValue().ToString(),
                    CreatedAtUtc = DateTime.UtcNow
                };
                db.Set<AppConfigurationRecord>().Add(appConfigRecord);
            }

            // Idp configuration
            if (!db.Set<IdpConfigurationRecord>().Any(i => i.TenantId == tenantId.GetValue()))
            {
                var idpConfigRecord = new IdpConfigurationRecord
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId.GetValue(),
                    SystemSuiteId = systemSuiteId.GetValue(),
                    ProviderTypeId = ProviderType.AzureAd.Id,
                    DomainHintsJson = "[\"beyondnet.com\", \"ums.local\"]",
                    ConfigPayload = "{\\\"authority\\\":\\\"https://login.microsoftonline.com/common\\\"}",
                    SecretRef = "kv/idp/azuread",
                    StatusId = 0,
                    ResolutionPriority = 10,
                    Version = 1,
                    CreatedBy = actor.GetValue().ToString(),
                    CreatedAtUtc = DateTime.UtcNow
                };
                db.Set<IdpConfigurationRecord>().Add(idpConfigRecord);
            }

            // Notification rule
            if (!db.Set<NotificationRuleRecord>().Any(n => n.TenantId == tenantId.GetValue()))
            {
                var ruleRecord = new NotificationRuleRecord
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId.GetValue(),
                    ChannelId = NotificationChannel.Email.Id,
                    Recipient = "alerts@beyondnet.com",
                    IsActive = true,
                    CreatedBy = actor.GetValue().ToString(),
                    CreatedAtUtc = DateTime.UtcNow
                };
                db.Set<NotificationRuleRecord>().Add(ruleRecord);
            }

            db.SaveChanges();
        }

        public static void SeedApprovalAggregates(IServiceProvider services)
        {
            using var scope = services.CreateScope();
            // G-014 (causa b, capa 2): appsettings activa UsePostgreSqlApprovalsStores=true, así que en este
            // host los approvals SON PostgreSQL. El sembrado hacía db.Set<ApprovalWorkflow>() sobre el
            // UmsPlatformDbContext, pero ApprovalWorkflow es el AGREGADO DE DOMINIO —no está mapeado en el
            // modelo (el contexto mapea ApprovalWorkflowRecord)— y EF lanzaba "Cannot create a DbSet for
            // 'ApprovalWorkflow' ..." abortando el arranque del host y hundiendo las ~73 clases E2E de la
            // colección. Se siembra vía el repositorio de approvals (que mapea el agregado a
            // ApprovalWorkflowRecord y persiste vía su UnitOfWork sobre el Testcontainer real).
            var workflowRepository = scope.ServiceProvider.GetRequiredService<IApprovalWorkflowRepository>();
            var actor = ActorId.Create("00000000-0000-0000-0000-000000000111");
            var tenantId = TenantId.Load(Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6"));

            if (workflowRepository.GetAllAsync().GetAwaiter().GetResult().Count > 0)
                return;

            var manualWorkflow = ApprovalWorkflow.Create(
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
            workflowRepository.AddAsync(manualWorkflow).GetAwaiter().GetResult();

            var autoWorkflow = ApprovalWorkflow.Create(
                tenantId,
                Code.Create("auto-approval"),
                Name.Create("Auto Approval"),
                Description.Create("Workflow that auto-approves requests."),
                UserCategory.Internal,
                false,
                null,
                actor).Value;
            SetAggregateId(autoWorkflow.Props, Guid.Parse("88888888-2222-2222-2222-222222222222"));
            workflowRepository.AddAsync(autoWorkflow).GetAwaiter().GetResult();

            workflowRepository.UnitOfWork.SaveChangesAsync().GetAwaiter().GetResult();
        }

        private static void SetAggregateId(object props, Guid id)
        {
            var idProperty = props.GetType().GetProperty("Id");
            idProperty!.SetValue(props, IdValueObject.Load(id));
        }
    }
}
