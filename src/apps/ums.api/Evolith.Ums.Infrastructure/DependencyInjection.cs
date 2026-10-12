namespace Evolith.Ums.Infrastructure;

#pragma warning disable S125

using MediatR;
using MassTransit;
using System.Reflection;
using System.Text.Json.Serialization.Metadata;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Polly;
using BeyondNetCode.Shell.Aop.Microsoft.Extensions.DependencyInjection.Aspects.Installer;
using Evolith.Ums.Domain.Audit.AuditRecord;
using Evolith.Ums.Domain.Approvals;
using Evolith.Ums.Domain.Authorization;
using Evolith.Ums.Domain.Configuration;
using Evolith.Ums.Domain.Identity;
using Evolith.Ums.Domain.Identity.Repositories.TenantParameter;
using Evolith.Ums.Domain.IGA;
using Evolith.Ums.Infrastructure.Persistence.Iga;
using Evolith.Ums.Infrastructure.Persistence.Audit;
using Evolith.Ums.Infrastructure.Persistence.Approvals;
using Evolith.Ums.Infrastructure.Persistence.Authorization;
using Evolith.Ums.Infrastructure.Persistence.Configuration;
using Evolith.Ums.Domain.Configuration;
using Evolith.Ums.Infrastructure.Hosting;
using Evolith.Ums.Infrastructure.Persistence;
using Evolith.Ums.Infrastructure.Persistence.Identity;
using Evolith.Ums.Infrastructure.Persistence.Identity.TenantParameter;
using Evolith.Ums.Infrastructure.Persistence.Interceptors;
using Evolith.Ums.Infrastructure.Persistence.Options;
using Evolith.Ums.Infrastructure.Services;
using Evolith.Ums.Application.Approvals.ApprovalRequest.Services;
using Evolith.Ums.Application.Identity.Tenant.TenantParameter.Services;
using Evolith.Ums.Application.Authorization.Profile.Exporters;
using Evolith.Ums.Infrastructure.Approvals.ApprovalRequest;
using Evolith.Ums.Application.Approvals.NotificationRule.Services;
using Evolith.Ums.Infrastructure.Approvals.NotificationRule;
using Evolith.Ums.Application.Configuration.IdpConfiguration.Services;
using Evolith.Ums.Application.Configuration.Services;
using Evolith.Ums.Domain.Configuration.FeatureFlag;
using Evolith.Ums.Infrastructure.Configuration;
using Evolith.Ums.Infrastructure.Configuration.IdpResolution;
using BeyondNetCode.Shell.Factory.Installer.Extensions;
using Evolith.Ums.Infrastructure.Persistence.Authorization.Exporters;
using Evolith.Ums.Application.Common.Interfaces;
using Evolith.Ums.Infrastructure.Services.Notifications;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment? environment = null)
    {
        services
            .AddOptions<PersistenceOptions>()
            .Bind(configuration.GetSection(PersistenceOptions.SectionName))
            .Validate(
                options => options.Provider == PersistenceProvider.InMemory
                    || !string.IsNullOrWhiteSpace(configuration.GetConnectionString("DefaultConnection")),
                "ConnectionStrings:DefaultConnection is required when Persistence.Provider is PostgreSql or SqlServer.")
            .ValidateOnStart();

        services.AddHttpContextAccessor();

        services.AddScoped<IUserContext, UserContext>();
        services.AddScoped<ITenantContext, TenantContext>();
        services.AddSingleton<IPasswordHashingService, BcryptPasswordHashingService>();
        services.AddScoped<RequestContext>();
        services.AddScoped<IRequestContext>(sp => sp.GetRequiredService<RequestContext>());

        // Trazabilidad funcional (ADR-0096; ADR-UMS-084/085): localizador legible + transacción
        // funcional con narrativa y desenlace. Concreta e interfaz comparten la misma instancia
        // con alcance de petición para que middleware y handlers narren la misma transacción.
        services.AddScoped<Observability.TransactionLocatorFactory>();
        services.AddScoped<ITransactionLocatorFactory>(sp =>
            sp.GetRequiredService<Observability.TransactionLocatorFactory>());
        services.AddScoped<Observability.FunctionalTransaction>();
        services.AddScoped<IFunctionalTransaction>(sp =>
            sp.GetRequiredService<Observability.FunctionalTransaction>());
        services.AddSingleton<IValueEncryptionService, AesValueEncryptionService>();
        services.AddScoped<IFeatureFlagEvaluator, FeatureFlagEvaluator>();
        services.AddScoped<IIdpConfigurationResolver, IdpConfigurationResolver>();
        services.AddScoped<INotificationRecipientResolver, NotificationRecipientResolver>();
        // Notificaciones (G-270). El transporte lo elige la configuracion, no el entorno: UAT y
        // Production declaran `Notifications:Provider=Smtp` y desarrollo se queda con el simulado,
        // que es el unico que no necesita un relay al que hablar.
        //
        // La validacion corre al ARRANCAR (ValidateOnStart) porque una notificacion se emite
        // siempre dentro de otra cosa —un alta, un restablecimiento, una aprobacion— y descubrir
        // alli que falta la credencial es descubrirlo tarde.
        services
            .AddOptions<NotificationOptions>()
            .Bind(configuration.GetSection(NotificationOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<Microsoft.Extensions.Options.IValidateOptions<NotificationOptions>,
                              NotificationOptionsValidator>();

        var notificaciones = configuration
            .GetSection(NotificationOptions.SectionName)
            .Get<NotificationOptions>() ?? new NotificationOptions();

        // Los enlaces del portal que viajan en el correo. Va fuera del if: no depende del
        // transporte, y con la URL sin declarar devuelve null y el mensaje se apoya en el codigo.
        services.AddSingleton<INotificationLinkBuilder, NotificationLinkBuilder>();

        if (notificaciones.Provider == NotificationProvider.Smtp)
        {
            services.AddSingleton<ISmtpDispatcher, SystemNetSmtpDispatcher>();
            services.AddScoped<INotificationService, SmtpNotificationAdapter>();
        }
        else
        {
            services.AddScoped<INotificationService, SimulatedNotificationAdapter>();
        }
        services.AddScoped<IApprovalRequestCreationPolicyResolver, ApprovalRequestCreationPolicyResolver>();
        // G-040: la auditoría automática se encola por el Transactional Outbox de MassTransit
        // (entrega POST-commit al AuditTrailPersistenceConsumer), no por un canal en memoria con
        // descarte silencioso. Scoped porque publica con el IPublishEndpoint de la petición.
        services.AddScoped<IAuditTrailSink, AuditTrailOutboxSink>();
        // G-066 / ADR-0098 D7: única vía de salida al bróker inter-sistema (eventos de integración
        // explícitos). Los eventos de dominio se despachan en proceso (MediatR, post-commit).
        services.AddScoped<Evolith.Ums.Application.Common.Interfaces.IIntegrationEventPublisher,
            Evolith.Ums.Infrastructure.Messaging.MassTransitIntegrationEventPublisher>();

        var isNotProduction = environment is null || !environment.IsProduction();
        services.AddUmsFactories(isNotProduction);

        services.AddScoped<ITenantParameterProvider, TenantParameterProvider>();
        services.AddScoped<ITenantExportConfigurationProvider, TenantExportConfigurationProvider>();
        // Política de refresh token resuelta desde la config jerárquica, fail-closed (ADR-UMS-091 / FR-015).
        services.AddScoped<Evolith.Ums.Application.Identity.Auth.IRefreshTokenPolicyProvider,
            Evolith.Ums.Application.Identity.Auth.RefreshTokenPolicyProvider>();
        // Almacén de refresh tokens (solo hash) sobre UmsPlatformDbContext (ADR-UMS-091 / FR-015).
        services.AddScoped<Evolith.Ums.Application.Identity.Auth.IRefreshTokenStore,
            Evolith.Ums.Infrastructure.Persistence.Identity.RefreshTokenStore>();
        // Almacén de tokens de restablecimiento de contraseña (solo hash) — G-188.
        services.AddScoped<Evolith.Ums.Application.Identity.Auth.IPasswordResetTokenStore,
            Evolith.Ums.Infrastructure.Persistence.Identity.PasswordResetTokenStore>();
        // Nivelador del tiempo de respuesta de los flujos anónimos indistinguibles (G-188).
        // Singleton: no tiene estado y su presupuesto es una constante del proceso.
        services.AddSingleton<Evolith.Ums.Application.Identity.Auth.IResponseTimingNormalizer,
            Evolith.Ums.Application.Identity.Auth.ResponseTimingNormalizer>();

        // Auth Graph Engine services
        services.AddScoped<Evolith.Ums.Domain.Authorization.Graph.IAuthorizationGraphBuilder,
                           Evolith.Ums.Application.Authorization.Graph.AuthorizationGraphBuilderService>();
        services.AddScoped<Evolith.Ums.Application.Authorization.Graph.IAuthGraphFormatProvider,
                           Evolith.Ums.Application.Authorization.Graph.AuthGraphFormatProvider>();
        services.AddScoped<Evolith.Ums.Domain.Identity.Auth.IAuthMethodResolver,
                           Evolith.Ums.Application.Identity.Auth.AuthMethodResolverService>();
        services.AddScoped<Evolith.Ums.Domain.Identity.Auth.ILocalAuthStrategy,
                           Evolith.Ums.Application.Identity.Auth.LocalAuthStrategyService>();
        services.AddScoped<Evolith.Ums.Domain.Identity.Auth.IIdpAuthStrategy,
                           Evolith.Ums.Infrastructure.Identity.Auth.IdpAuthStrategyDispatcher>();
        // FR-042 (ADR-UMS-097 §2.3/§2.4, slice 2b): orquestador del fallback encadenado por indisponibilidad.
        services.AddScoped<Evolith.Ums.Domain.Identity.Auth.IIdpChainAuthenticator,
                           Evolith.Ums.Application.Identity.Auth.IdpChainAuthenticator>();
        services.AddScoped<Evolith.Ums.Application.Common.Interfaces.IAuthAuditService,
                           Evolith.Ums.Infrastructure.Identity.Auth.AuthAuditService>();

        // --- Adaptador OIDC real (Authorization Code + PKCE) — ADR-UMS-094 / G-049 (slice 1) ---
        // Puertos HTTP inyectables (token endpoint + JWKS): en producción hablan con el IdP;
        // en unit tests se sustituyen por fakes con llaves/respuestas controladas.
        services.AddHttpClient<Evolith.Ums.Infrastructure.Identity.Auth.Oidc.IOidcTokenClient,
                               Evolith.Ums.Infrastructure.Identity.Auth.Oidc.HttpOidcTokenClient>();
        services.AddHttpClient<Evolith.Ums.Infrastructure.Identity.Auth.Oidc.IJwksProvider,
                               Evolith.Ums.Infrastructure.Identity.Auth.Oidc.HttpJwksProvider>();
        // Endpoints OIDC leídos de la configuración del inquilino (IdpConfiguration), no hardcodeados.
        services.AddScoped<Evolith.Ums.Infrastructure.Identity.Auth.Oidc.IOidcProviderConfigStore,
                           Evolith.Ums.Infrastructure.Identity.Auth.Oidc.IdpConfigurationOidcProviderConfigStore>();
        services.AddScoped<Evolith.Ums.Infrastructure.Identity.Auth.Oidc.OidcIdTokenValidator>();
        services.AddScoped<Evolith.Ums.Infrastructure.Identity.Auth.Oidc.OidcAuthorizationRequestFactory>();
        // El adaptador lo instancia Shell.Factory (por estrategia); registrado también aquí
        // para que sus dependencias resuelvan vía el proveedor de servicios.
        services.AddScoped<Evolith.Ums.Infrastructure.Identity.Auth.Oidc.OidcIdpAuthAdapter>();
        // Default serializer (JSON) — injected directly into CommandHandler
        services.AddTransient<Evolith.Ums.Application.Authorization.Graph.Serializers.IAuthorizationGraphSerializer,
                              Evolith.Ums.Infrastructure.Authorization.Graph.JsonAuthorizationGraphSerializer>();

        // OPS-01 / HARDENING-03: Token revocation store.
        // G-069: Redis-backed configuration cache (Pub/Sub).
        // Con Redis configurado → estado e invalidaciones compartidos entre pods.
        // Sin él → InMemory, correcto solo en nodo único / dev / pruebas.
        //
        // G-169: se acepta también `REDIS_CONNECTION` porque es la variable que inyecta el chart
        // de Helm. ASP.NET solo mapea variables de entorno a claves jerárquicas con doble guion
        // bajo (`Redis__Connection`), así que leer únicamente `Redis:Connection` hacía que en
        // Kubernetes NUNCA se activara Redis: el despliegue creía tener caché distribuida y
        // corría con las implementaciones en memoria, sin un solo aviso.
        // Normalizado: el chart entrega una URI y `ConnectionMultiplexer` quiere `host:puerto`.
        // Ver `CadenaDeRedis` — el puerto duplicado que tumbaba el arranque salía de aquí.
        var redisConnection = Evolith.Ums.Infrastructure.Configuration.CadenaDeRedis.Normalizar(
            configuration["Redis:Connection"] ?? configuration["REDIS_CONNECTION"]);
        if (!string.IsNullOrWhiteSpace(redisConnection))
        {
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration         = redisConnection;
                options.InstanceName          = "ums:";   // Namespace prefix to avoid key collisions in shared Redis.
            });

            // Required for Pub/Sub in RedisConfigurationCache
            services.AddSingleton<StackExchange.Redis.IConnectionMultiplexer>(sp =>
                StackExchange.Redis.ConnectionMultiplexer.Connect(redisConnection));

            services.AddSingleton<ITokenRevocationStore, RedisTokenRevocationStore>();
            services.AddSingleton<IConfigurationCache, RedisConfigurationCache>();
            // G-248: el cupo de peticiones se cuenta donde lo ven todas las réplicas.
            services.AddSingleton<ILimitadorDePeticiones, Services.LimitadorDePeticionesDistribuido>();
        }
        else
        {
            // Respaldo en proceso: `IDistributedCache` debe resolver siempre porque la
            // idempotencia depende de él (G-169). Sin Redis solo deduplica dentro del pod.
            services.AddDistributedMemoryCache();
            services.AddSingleton<ITokenRevocationStore, InMemoryTokenRevocationStore>();
            services.AddSingleton<IConfigurationCache, InMemoryConfigurationCache>();
            // Sin Redis el cupo vuelve a ser por proceso: correcto solo con UNA réplica, y el
            // arranque ya lo declara en el log (G-248).
            services.AddSingleton<ILimitadorDePeticiones, Services.LimitadorDePeticionesEnProceso>();
        }

        // G-247: las sesiones cerradas van sobre `IDistributedCache`, que aquí arriba ya quedó
        // resuelto a Redis o a memoria del proceso. Una sola implementación: lo que cambia entre
        // los dos modos es el alcance, no el código.
        services.AddSingleton<ISessionRevocationStore, Services.SessionRevocationStore>();

        // El grafo retenido de cada sesion, por el MISMO camino y por la misma razon: memoria del
        // proceso por omision --el minimo por defecto-- y Redis en cuanto la cadena este puesta.
        // Va fuera del if/else a proposito: IDistributedCache resuelve en las dos ramas, y un
        // segundo interruptor solo anadiria una forma de que ambos discrepen.
        services.AddSingleton<Application.Common.Interfaces.ICacheDeGrafo,
            Authorization.Graph.CacheDeGrafoDistribuida>();

        // HARDENING-03: Register Infrastructure MediatR notification handlers (UserDeleted, UserBlocked → revoke tokens).
        // MediatR.AddApplication() only scans Evolith.Ums.Application; Infrastructure handlers must be registered here.
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly));
        services.AddHostedService<PersistenceRuntimeReporter>();

        var persistence = configuration.GetSection(PersistenceOptions.SectionName).Get<PersistenceOptions>() ?? new();

        // Los manejadores de proyección de read models (MediatR en proceso, fase 1) dependen de
        // ReadModelDbContext, así que solo se registran cuando hay una base relacional detrás.
        // Bajo InMemory no hay dónde proyectar; bajo PostgreSQL o SQL Server sí, y cada uno
        // aporta su propio juego de migraciones para ese contexto.
        if (persistence.IsRelational)
        {
            services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Evolith.Ums.ReadModels.Projections.PermissionTemplateProjectionHandler).Assembly));
        }

        // IMetadata (BeyondNetCode.Shell.Ddd) declares a SetMetadata method, which MassTransit's
        // dynamic interface proxy cannot implement — serializing any domain event would throw
        // SerializationException. Metadata is transport-irrelevant, so strip it from payloads.
        void ConfigurePayload(IBusFactoryConfigurator cfg) =>
            cfg.ConfigureJsonSerializerOptions(options =>
            {
                options.TypeInfoResolver = (options.TypeInfoResolver
                        ?? new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver())
                    .WithAddedModifier(typeInfo =>
                    {
                        for (var i = typeInfo.Properties.Count - 1; i >= 0; i--)
                        {
                            if (typeInfo.Properties[i].PropertyType == typeof(BeyondNetCode.Shell.Ddd.Interfaces.IMetadata))
                            {
                                typeInfo.Properties.RemoveAt(i);
                            }
                        }
                    });
                return options;
            });

        // ADR-0083/ADR-0107: consume the MMS master-Tenant projection when a broker is configured.
        // The projection read model + the MassTransit inbox (idempotency) live in an isolated context.
        var rabbitMqConnection = configuration.GetConnectionString("RabbitMq");

        // DS-07: the projection context must never silently fall back to the platform DB
        // ("Default") or to localhost. When a broker is configured (kind/prod), MasterDataDb is
        // required — fail loud rather than project the master-Tenant read model into the wrong
        // database. A localhost default is kept ONLY for the broker-less dev/in-memory path.
        var masterDataDb = configuration.GetConnectionString("MasterDataDb");
        if (!string.IsNullOrWhiteSpace(rabbitMqConnection) && string.IsNullOrWhiteSpace(masterDataDb))
            throw new InvalidOperationException(
                "ConnectionStrings:MasterDataDb is required when ConnectionStrings:RabbitMq is configured — " +
                "the MMS tenant projection must not fall back to the platform DB or localhost.");

        services.AddDbContext<MasterData.TenantProjectionDbContext>(options =>
        {
#pragma warning disable S2068 // Cadena de conexión de FALLBACK local de desarrollo; la real llega por configuración (masterDataDb). No es un secreto de producción.
            if (persistence.Provider == PersistenceProvider.PostgreSql)
                options.UseNpgsql(
                    masterDataDb ?? "Host=localhost;Port=5432;Database=ums;Username=postgres;Password=postgres");
            else
                options.UseSqlServer(
                    masterDataDb ?? "Server=localhost;Database=ums;Trusted_Connection=True;TrustServerCertificate=True",
                    o => o.MigrationsAssembly(MigrationsDesignTime.EnsambladoSqlServer));
#pragma warning restore S2068
        });

        // GAP-001 / DS-08: migrate the projection store at startup. The platform context is
        // migrated in InitializeUmsPlatformAsync, but the isolated projection context had no
        // migrator — on a fresh deploy masterdata.tenant_projection + the MassTransit
        // InboxState/OutboxState tables would not exist and the consumer would fail on its first
        // message. Mirror Tracker's interim hosted migrator. Registered only when a broker is
        // configured: dev/tests use the in-memory bus (no real projection) and must not contact
        // the localhost Npgsql fallback at startup. DS-08 replaces this startup migrator with a
        // migrate-Job Helm hook once replicas>1 (concurrent startup migrations race).
        if (!string.IsNullOrWhiteSpace(rabbitMqConnection))
            services.AddHostedService<MasterData.TenantProjectionMigrator>();

        services.AddMassTransit(x =>
        {
            x.SetKebabCaseEndpointNameFormatter();

            x.AddConsumers(Assembly.GetExecutingAssembly());

            if (!string.IsNullOrWhiteSpace(rabbitMqConnection))
            {
                // Cross-service broker (kind/prod): receive MMS tenant events over RabbitMQ,
                // with the EF inbox for exactly-once-effective consumption (ADR-0033/ADR-0063).
                x.AddEntityFrameworkOutbox<MasterData.TenantProjectionDbContext>(o =>
                {
                    if (persistence.Provider == PersistenceProvider.PostgreSql)
                        o.UsePostgres();
                    else
                        o.UseSqlServer();
                });

                // G-066 / ADR-0098 D4 / KB-TXN-001: transactional bus-outbox on the WRITE context so
                // domain events are staged with the aggregate change set and delivered AFTER commit
                // (no «phantom messages»). UseBusOutbox() routes IPublishEndpoint through the outbox.
                x.AddEntityFrameworkOutbox<UmsPlatformDbContext>(o =>
                {
                    if (persistence.Provider == PersistenceProvider.PostgreSql)
                        o.UsePostgres();
                    else
                        o.UseSqlServer();
                    o.UseBusOutbox();
                });
                x.UsingRabbitMq((context, cfg) =>
                {
                    cfg.Host(rabbitMqConnection);
                    ConfigurePayload(cfg);
                    cfg.ConfigureEndpoints(context);
                });
            }
            else
            {
                // Dev/tests (no broker): keep the in-process bus (existing behavior).
                x.UsingInMemory((context, cfg) =>
                {
                    ConfigurePayload(cfg);
                    cfg.ConfigureEndpoints(context);
                });
            }
        });

        // REC-04: Cross-aggregate transaction scope. Depende de que haya una base relacional
        // con transacciones reales, no del motor concreto.
        if (persistence.IsRelational)
            services.AddScoped<IUnitOfWorkScope, UnitOfWorkScope>();
        else
            services.AddSingleton<IUnitOfWorkScope, NoOpUnitOfWorkScope>();

        if (persistence.IsRelational)
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection must be configured for relational persistence.");

            var esPostgres = persistence.Provider == PersistenceProvider.PostgreSql;

            services.AddScoped<OrganizationDbContextInterceptor>();
            services.AddScoped<AuditSaveChangesInterceptor>();
            services.AddScoped<AuditAppendOnlyGuardInterceptor>(); // G-081: rechaza UPDATE/DELETE de la traza de auditoría (no repudio)

            // Solo PostgreSQL: emula el token de concurrencia porque no tiene tipo `rowversion`
            // (bytea + pgcrypto + rotación en UPDATE). SQL Server lo genera el motor, así que
            // registrar el interceptor allí duplicaría el trabajo y pisaría el valor del store.
            if (esPostgres)
                services.AddScoped<PostgresRowVersionSaveChangesInterceptor>();

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
                if (esPostgres)
                {
                    options.UseNpgsql(connectionString, pgOptions =>
                    {
                        pgOptions.MigrationsHistoryTable("__EFMigrationsHistory", UmsPlatformDbContext.DefaultSchema);
                        pgOptions.EnableRetryOnFailure(3);
                    });
                }
                else
                {
                    options.UseSqlServer(connectionString, sqlOptions =>
                    {
                        sqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", UmsPlatformDbContext.DefaultSchema);
                        sqlOptions.MigrationsAssembly(MigrationsDesignTime.EnsambladoSqlServer);
                        sqlOptions.EnableRetryOnFailure(3);
                    });
                }

                // El interceptor de RowVersion solo existe bajo PostgreSQL (ver arriba), así que
                // la lista se compone según el proveedor: pedirlo al contenedor bajo SQL Server
                // lanzaría InvalidOperationException por servicio no registrado.
                var interceptores = new List<Microsoft.EntityFrameworkCore.Diagnostics.IInterceptor>
                {
                    serviceProvider.GetRequiredService<OrganizationDbContextInterceptor>(),
                    serviceProvider.GetRequiredService<AuditSaveChangesInterceptor>(),
                    serviceProvider.GetRequiredService<AuditAppendOnlyGuardInterceptor>(),
                };

                if (esPostgres)
                    interceptores.Add(serviceProvider.GetRequiredService<PostgresRowVersionSaveChangesInterceptor>());

                options.AddInterceptors(interceptores);

                options.ConfigureWarnings(warnings =>
                    warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
            });

            // Los read models (proyecciones de fase 1) viven en la misma base que la plataforma.
            services.AddDbContext<Evolith.Ums.ReadModels.ReadModelDbContext>(options =>
            {
                if (esPostgres)
                    options.UseNpgsql(connectionString, pgOptions => pgOptions.EnableRetryOnFailure(3));
                else
                    options.UseSqlServer(connectionString, sqlOptions =>
                    {
                        sqlOptions.MigrationsAssembly(MigrationsDesignTime.EnsambladoSqlServer);
                        sqlOptions.EnableRetryOnFailure(3);
                    });
            });
        }

        if (persistence.IsRelational && persistence.UsePostgreSqlIdentityStores)
        {
            services.AddScoped<ITenantRepository, PostgreSqlTenantRepository>();
            services.AddScoped<ITenantParameterRepository, PostgreSqlTenantParameterRepository>();
            services.AddScoped<ITenantSignupRequestRepository, PostgreSqlTenantSignupRequestRepository>();
            services.AddScoped<IUserAccountRepository, PostgreSqlUserAccountRepository>();
            services.AddScoped<IUserManagementDelegationRepository, PostgreSqlUserManagementDelegationRepository>();
        }
        else
        {
            services.AddSingleton<InMemoryTenantRepository>();
            services.AddSingleton<ITenantRepository>(sp => sp.GetRequiredService<InMemoryTenantRepository>());

            services.AddSingleton<InMemoryTenantParameterRepository>();
            services.AddSingleton<ITenantParameterRepository>(sp => sp.GetRequiredService<InMemoryTenantParameterRepository>());

            services.AddSingleton<InMemoryTenantSignupRequestRepository>();
            services.AddSingleton<ITenantSignupRequestRepository>(sp => sp.GetRequiredService<InMemoryTenantSignupRequestRepository>());

            services.AddSingleton<InMemoryUserAccountRepository>();
            services.AddSingleton<IUserAccountRepository>(sp => sp.GetRequiredService<InMemoryUserAccountRepository>());

            services.AddSingleton<InMemoryUserManagementDelegationRepository>();
            services.AddSingleton<IUserManagementDelegationRepository>(sp => sp.GetRequiredService<InMemoryUserManagementDelegationRepository>());
        }

        if (persistence.IsRelational && persistence.UsePostgreSqlAuthorizationStores)
        {
            services.AddScoped<IProfileRepository, PostgreSqlProfileRepository>();
            services.AddScoped<ISystemSuiteRepository, PostgreSqlSystemSuiteRepository>();
            services.AddScoped<IPermissionTemplateRepository, PostgreSqlPermissionTemplateRepository>();
            services.AddScoped<IRoleRepository, PostgreSqlRoleRepository>();
            services.AddScoped<ITemplateAssignmentRuleRepository, PostgreSqlTemplateAssignmentRuleRepository>();
        }
        else
        {
            services.AddSingleton<InMemoryProfileRepository>();
            services.AddSingleton<IProfileRepository>(sp => sp.GetRequiredService<InMemoryProfileRepository>());

            services.AddSingleton<InMemorySystemSuiteRepository>();
            services.AddSingleton<ISystemSuiteRepository>(sp => sp.GetRequiredService<InMemorySystemSuiteRepository>());

            services.AddSingleton<InMemoryPermissionTemplateRepository>();
            services.AddSingleton<IPermissionTemplateRepository>(sp => sp.GetRequiredService<InMemoryPermissionTemplateRepository>());

            services.AddSingleton<InMemoryRoleRepository>();
            services.AddSingleton<IRoleRepository>(sp => sp.GetRequiredService<InMemoryRoleRepository>());

            services.AddSingleton<InMemoryTemplateAssignmentRuleRepository>();
            services.AddSingleton<ITemplateAssignmentRuleRepository>(sp => sp.GetRequiredService<InMemoryTemplateAssignmentRuleRepository>());
        }

        if (persistence.IsRelational && persistence.UsePostgreSqlConfigurationStores)
        {
            services.AddScoped<IAppConfigurationRepository, PostgreSqlAppConfigurationRepository>();
            services.AddScoped<IFeatureFlagRepository, PostgreSqlFeatureFlagRepository>();
            services.AddScoped<IIdpConfigurationRepository, PostgreSqlIdpConfigurationRepository>();
            services.AddScoped<IParameterDefinitionRepository, PostgreSqlParameterDefinitionRepository>();
            services.AddScoped<IParameterGlobalValueRepository, PostgreSqlParameterGlobalValueRepository>();
            services.AddScoped<IParameterTenantValueRepository, PostgreSqlParameterTenantValueRepository>();
        }
        else
        {
            services.AddSingleton<InMemoryAppConfigurationRepository>();
            services.AddSingleton<IAppConfigurationRepository>(sp => sp.GetRequiredService<InMemoryAppConfigurationRepository>());

            services.AddSingleton<InMemoryFeatureFlagRepository>();
            services.AddSingleton<IFeatureFlagRepository>(sp => sp.GetRequiredService<InMemoryFeatureFlagRepository>());

            services.AddSingleton<InMemoryIdpConfigurationRepository>();
            services.AddSingleton<IIdpConfigurationRepository>(sp => sp.GetRequiredService<InMemoryIdpConfigurationRepository>());

            services.AddSingleton<InMemoryParameterRepositories>();
            services.AddSingleton<IParameterDefinitionRepository>(sp => sp.GetRequiredService<InMemoryParameterRepositories>());
            services.AddSingleton<IParameterGlobalValueRepository>(sp => sp.GetRequiredService<InMemoryParameterRepositories>());
            services.AddSingleton<IParameterTenantValueRepository>(sp => sp.GetRequiredService<InMemoryParameterRepositories>());
        }

        if (persistence.IsRelational)
        {
            services.AddScoped<IAuditRecordRepository, PostgreSqlAuditRecordRepository>();
        }
        else
        {
            services.AddSingleton<InMemoryAuditRecordRepository>();
            services.AddSingleton<IAuditRecordRepository>(sp => sp.GetRequiredService<InMemoryAuditRecordRepository>());
        }

        if (persistence.IsRelational && persistence.UsePostgreSqlApprovalsStores)
        {
            services.AddScoped<IApprovalWorkflowRepository, PostgreSqlApprovalWorkflowRepository>();
            services.AddScoped<IApprovalRequestRepository, PostgreSqlApprovalRequestRepository>();
            services.AddScoped<INotificationRuleRepository, PostgreSqlNotificationRuleRepository>();
            services.AddScoped<IDocumentTypeRepository, PostgreSqlDocumentTypeRepository>();
            services.AddScoped<IUserDocumentRepository, PostgreSqlUserDocumentRepository>();
            services.AddScoped<IAccessEnforcementPolicyRepository, PostgreSqlAccessEnforcementPolicyRepository>();
        }
        else
        {
            services.AddSingleton<InMemoryApprovalWorkflowRepository>();
            services.AddSingleton<IApprovalWorkflowRepository>(sp => sp.GetRequiredService<InMemoryApprovalWorkflowRepository>());

            services.AddSingleton<InMemoryApprovalRequestRepository>();
            services.AddSingleton<IApprovalRequestRepository>(sp => sp.GetRequiredService<InMemoryApprovalRequestRepository>());

            services.AddSingleton<InMemoryNotificationRuleRepository>();
            services.AddSingleton<INotificationRuleRepository>(sp => sp.GetRequiredService<InMemoryNotificationRuleRepository>());

            services.AddSingleton<InMemoryDocumentTypeRepository>();
            services.AddSingleton<IDocumentTypeRepository>(sp => sp.GetRequiredService<InMemoryDocumentTypeRepository>());

            services.AddSingleton<InMemoryUserDocumentRepository>();
            services.AddSingleton<IUserDocumentRepository>(sp => sp.GetRequiredService<InMemoryUserDocumentRepository>());

            services.AddSingleton<InMemoryAccessEnforcementPolicyRepository>();
            services.AddSingleton<IAccessEnforcementPolicyRepository>(sp => sp.GetRequiredService<InMemoryAccessEnforcementPolicyRepository>());
        }

        // IGA (ADR-UMS-093): repositorios del contexto de gobierno de identidad. Bajo PostgreSQL usan
        // UmsPlatformDbContext (scoped); en dev/tests la variante en memoria (singleton).
        if (persistence.IsRelational && persistence.UsePostgreSqlIgaStores)
        {
            services.AddScoped<IRoleMaturityStatusRepository, PostgreSqlRoleMaturityStatusRepository>();
            services.AddScoped<IRolePromotionRequestRepository, PostgreSqlRolePromotionRequestRepository>();
        }
        else
        {
            services.AddSingleton<InMemoryRoleMaturityStatusRepository>();
            services.AddSingleton<IRoleMaturityStatusRepository>(sp => sp.GetRequiredService<InMemoryRoleMaturityStatusRepository>());

            services.AddSingleton<InMemoryRolePromotionRequestRepository>();
            services.AddSingleton<IRolePromotionRequestRepository>(sp => sp.GetRequiredService<InMemoryRolePromotionRequestRepository>());
        }

        // ── AOP: DispatchProxy aspect-oriented infrastructure ──────────────────────
        // AddAop() registers the built-in aspects (LoggerAspect, AdviceAspect, RetryAspect),
        // the PointCut, AspectExecutor and IFactory<ILogger> / IFactory<IAdvice> singletons.
        //
        // Two ILogger adapters are available — select via [LoggerAspect(Type = typeof(...))] :
        //
        //   IMelLogger       → MelLogger (MEL, Debug level, PII-safe)
        //                      Lightweight; ideal for dev and low-noise production paths.
        //
        //   IUmsLogger       → UmsSerilogLogger (Serilog, Information level, observability-aware)
        //                      Enriches every line with TenantId, CorrelationId, TraceId, SpanId
        //                      and BoundedContext; ships to OTel → Loki via Serilog OTel sink.
        //                      Requires an active IUserContext (scoped) — use on command handlers.
        //
        // AddAopProxy<TService, TImpl>() wraps the concrete handler with a DispatchProxy.
        // It registers TImpl as itself AND replaces the TService registration; MediatR
        // resolves the proxy (last registration wins) and delegates to the concrete handler.
        services.AddAop(builder =>
        {
            builder.AddAspect<AuditTrailAspect>();
            builder.AddAspect<TransactionAspect>();
            builder.AddAspect<TenantValidationAspect>();
            builder.AddAspect<AuthorizationAspect>();
        });

        services.AddKeyedTransient<BeyondNetCode.Shell.Aop.Aspects.ILogger, Evolith.Ums.Infrastructure.Aop.MelLogger>(
            typeof(Evolith.Ums.Application.Common.Aop.IMelLogger));

        services.AddKeyedTransient<BeyondNetCode.Shell.Aop.Aspects.ILogger, Evolith.Ums.Infrastructure.Aop.UmsSerilogLogger>(
            typeof(Evolith.Ums.Application.Common.Aop.IUmsLogger));

        RegisterMediatRAopProxies(services, typeof(Evolith.Ums.Application.DependencyInjection).Assembly);

        return services;
    }

    private static void RegisterMediatRAopProxies(IServiceCollection services, Assembly applicationAssembly)
    {
        var addAopProxyMethod = typeof(ServiceCollectionExtension)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(method =>
                method.Name == nameof(ServiceCollectionExtension.AddAopProxy)
                && method.IsGenericMethodDefinition
                && method.GetGenericArguments().Length == 2);

        var handlerContracts = applicationAssembly
            .DefinedTypes
            .Where(type => type is { IsClass: true, IsAbstract: false })
            .SelectMany(type => type.ImplementedInterfaces
                .Where(implemented =>
                    implemented.IsGenericType
                    && implemented.GetGenericTypeDefinition() == typeof(IRequestHandler<,>))
                .Select(implemented => new
                {
                    ServiceType = implemented,
                    ImplementationType = type.AsType(),
                }))
            .ToList();

        foreach (var contract in handlerContracts)
        {
            addAopProxyMethod
                .MakeGenericMethod(contract.ServiceType, contract.ImplementationType)
                .Invoke(null, [services, ServiceLifetime.Scoped]);
        }
    }

    /// <summary>
    /// REC-02: Registers real health checks (liveness, readiness, outbox backlog, SQL Server).
    /// Call from Program.cs: <c>builder.Services.AddInfrastructureHealthChecks(configuration);</c>
    /// Then map endpoints: <c>app.MapInfrastructureHealthCheckEndpoints();</c>
    /// </summary>
    public static IServiceCollection AddInfrastructureHealthChecks(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var persistence = configuration.GetSection(PersistenceOptions.SectionName).Get<PersistenceOptions>() ?? new();

        var builder = services.AddHealthChecks();

        if (persistence.IsRelational)
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection");
            if (!string.IsNullOrWhiteSpace(connectionString))
            {
                if (persistence.Provider == PersistenceProvider.PostgreSql)
                {
                    builder.AddNpgSql(
                        connectionString,
                        name: "postgresql",
                        tags: ["ready", "db"]);
                }
                else
                {
                    builder.AddSqlServer(
                        connectionString,
                        name: "sqlserver",
                        tags: ["ready", "db"]);
                }
            }
        }

        return services;
    }
}
