using System.Diagnostics;
using Asp.Versioning;
using Asp.Versioning.Builder;
using BeyondNetCode.Shell.Bootstrapper.Impl;
using BeyondNetCode.Shell.Bootstrapper.Interface;
using Evolith.Ums.Application;
using Evolith.Ums.Domain.Identity;
using Evolith.Ums.Globalization;
using Evolith.Ums.Globalization.Access;
using Evolith.Ums.Infrastructure;
using Evolith.Ums.Infrastructure.Persistence;
using Evolith.Ums.Infrastructure.Persistence.Options;
using Evolith.Ums.Infrastructure.Persistence.Seeders;
using Evolith.Ums.Presentation.Bootstrapping.Bootstrappers;
using Evolith.Ums.Presentation.Endpoints;
using Evolith.Ums.Presentation.Endpoints.Approvals.AccessEnforcementPolicy;
using Evolith.Ums.Presentation.Endpoints.Approvals.AccessEnforcementPolicy.Queries;
using Evolith.Ums.Presentation.Endpoints.Approvals.ApprovalRequest;
using Evolith.Ums.Presentation.Endpoints.Approvals.ApprovalRequest.Queries;
using Evolith.Ums.Presentation.Endpoints.Approvals.ApprovalWorkflow;
using Evolith.Ums.Presentation.Endpoints.Approvals.ApprovalWorkflow.Queries;
using Evolith.Ums.Presentation.Endpoints.Approvals.DocumentType;
using Evolith.Ums.Presentation.Endpoints.Approvals.DocumentType.Queries;
using Evolith.Ums.Presentation.Endpoints.Approvals.NotificationRule;
using Evolith.Ums.Presentation.Endpoints.Approvals.NotificationRule.Queries;
using Evolith.Ums.Presentation.Endpoints.Approvals.UserDocument;
using Evolith.Ums.Presentation.Endpoints.Approvals.UserDocument.Queries;
using Evolith.Ums.Presentation.Endpoints.Audit.AuditRecord;
using Evolith.Ums.Presentation.Endpoints.Audit.AuditRecord.Queries;
using Evolith.Ums.Presentation.Endpoints.Authorization.AssignmentRule;
using Evolith.Ums.Presentation.Endpoints.Authorization.Profile;
using Evolith.Ums.Presentation.Endpoints.Authorization.Profile.Queries;
using Evolith.Ums.Presentation.Endpoints.Authorization.Role;
using Evolith.Ums.Presentation.Endpoints.Authorization.SystemSuite;
using Evolith.Ums.Presentation.Endpoints.Authorization.SystemSuite.Queries;
using Evolith.Ums.Presentation.Endpoints.Authorization.Template;
using Evolith.Ums.Presentation.Endpoints.Authorization.Template.Queries;
using Evolith.Ums.Presentation.Endpoints.Configuration.AppConfiguration;
using Evolith.Ums.Presentation.Endpoints.Configuration.AppConfiguration.Queries;
using Evolith.Ums.Presentation.Endpoints.Configuration.FeatureFlag;
using Evolith.Ums.Presentation.Endpoints.Configuration.FeatureFlag.Queries;
using Evolith.Ums.Presentation.Endpoints.Configuration.IdpConfiguration;
using Evolith.Ums.Presentation.Endpoints.Configuration.IdpConfiguration.Queries;
using Evolith.Ums.Presentation.Endpoints.Configuration.Parameter;
using Evolith.Ums.Presentation.Endpoints.Configuration.ParameterCatalog.Queries;
using Evolith.Ums.Presentation.Endpoints.Identity.Auth;
using Evolith.Ums.Presentation.Endpoints.Identity.Onboarding;
using Evolith.Ums.Presentation.Endpoints.Identity.Tenant;
using Evolith.Ums.Presentation.Endpoints.Identity.Tenant.Queries;
using Evolith.Ums.Presentation.Endpoints.Identity.UserAccount;
using Evolith.Ums.Presentation.Endpoints.Identity.UserAccount.Queries;
using Evolith.Ums.Presentation.Endpoints.Identity.UserManagementDelegation;
using Evolith.Ums.Presentation.Endpoints.Identity.UserManagementDelegation.Queries;
using Evolith.Ums.Presentation.Endpoints.Iga.RoleMaturityStatus.Queries;
using Evolith.Ums.Presentation.Endpoints.Iga.RolePromotionRequest;
using Evolith.Ums.Presentation.Endpoints.Iga.RolePromotionRequest.Queries;
using Evolith.Ums.Presentation.Extensions;
using Evolith.Ums.Presentation.Middleware;
using Evolith.Ums.Presentation.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Serilog;

namespace Evolith.Ums.Presentation.Bootstrapping;

public static class UmsApiServiceBootstrappers
{
    public static IServiceCollection AddUmsApiServiceBootstrappers(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        new CompositeBootstrapper()
            .Add(new UmsCoreApplicationBootstrapper(services, configuration, environment))
            .Add(new UmsApiPlatformBootstrapper(services, configuration))
            .Add(new UmsApiDocumentationBootstrapper(services, configuration))
            .Add(new ConfigurationBootstrapper(services))
            .Run();

        return services;
    }
}

internal sealed class UmsCoreApplicationBootstrapper : IBootstrapper<IServiceCollection>
{
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _environment;

    public UmsCoreApplicationBootstrapper(
        IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        Result = services;
        _configuration = configuration;
        _environment = environment;
    }

    public IServiceCollection? Result { get; private set; }

    public void Run()
    {
        ArgumentNullException.ThrowIfNull(Result);

        Result.AddApplication();
        Result.AddInfrastructure(_configuration, _environment);
        Result.AddScoped<ILocalizationService, LocalizationService>();
        Result.AddMemoryCache(); // required by IdempotencyMiddleware (FIX-06)

        // HARDENING-02: JWT Bearer authentication. Disabled in dev (DevAuthMiddleware handles it).
        // Production: set Authentication:Enabled=true and configure Authority + Audience.
        Result.AddUmsAuthentication(_configuration, _environment);

        // JWT Token Service for session management
        // El material de firma es unico y de por vida: la clave se carga y valida una sola
        // vez, al arrancar. Si falta o es debil, UMS no arranca (ADR-0157 §4.7) en vez de
        // descubrirlo al primer login.
        Result.AddSingleton<Evolith.Ums.Presentation.Services.MaterialDeFirma>();
        Result.AddSingleton<IJwtTokenService, JwtTokenService>();
    }
}

internal sealed class UmsApiPlatformBootstrapper : IBootstrapper<IServiceCollection>
{
    private readonly IConfiguration _configuration;

    public UmsApiPlatformBootstrapper(IServiceCollection services, IConfiguration configuration)
    {
        Result = services;
        _configuration = configuration;
    }

    public IServiceCollection? Result { get; private set; }

    public void Run()
    {
        ArgumentNullException.ThrowIfNull(Result);

        Result.AddApiVersioning(options =>
        {
            options.DefaultApiVersion = new ApiVersion(1);
            options.AssumeDefaultVersionWhenUnspecified = true;
            options.ReportApiVersions = true;
            options.ApiVersionReader = new UrlSegmentApiVersionReader();
        });

        Result.AddProblemDetails();
        Result.AddEndpointsApiExplorer();

        // G-175: compresión de respuesta. `EnableForHttps` va explícito porque el valor por
        // defecto es false: sin él, en producción (que es HTTPS) no comprimiría nada.
        // El riesgo BREACH que motiva ese default se acota aquí porque la API no emite
        // formularios HTML ni refleja secretos en el cuerpo, y los tokens viajan en cabecera.
        Result.AddResponseCompression(options =>
        {
            options.EnableForHttps = true;
            options.Providers.Add<Microsoft.AspNetCore.ResponseCompression.BrotliCompressionProvider>();
            options.Providers.Add<Microsoft.AspNetCore.ResponseCompression.GzipCompressionProvider>();
            options.MimeTypes = Microsoft.AspNetCore.ResponseCompression.ResponseCompressionDefaults.MimeTypes
                .Concat(["application/problem+json", "application/xml", "text/yaml", "text/csv"]);
        });

        // REC-02: Real health checks — liveness + readiness + outbox backlog
        Result.AddInfrastructureHealthChecks(_configuration);

        // REC-06: OpenTelemetry — distributed tracing + metrics
        Result.AddUmsObservability(_configuration);
    }
}

public static class UmsApiApplicationBuilderExtensions
{
    public static async Task<WebApplication> InitializeUmsPlatformAsync(this WebApplication app)
    {
        DeclararEstadoDistribuido(app);

        var persistence = app.Services.GetRequiredService<IOptions<PersistenceOptions>>().Value;

        if (persistence.InitializePlatformStoreOnStartup)
        {
            using var scope = app.Services.CreateScope();
            var platformDbContext = scope.ServiceProvider.GetRequiredService<UmsPlatformDbContext>();

            // Migrar depende de que haya una base relacional, no del motor: cada proveedor
            // aporta su propio juego de migraciones (el de SQL Server vive en el ensamblado
            // Evolith.Ums.Infrastructure.Migrations.SqlServer). Comparar contra PostgreSql
            // dejaba la base de UAT vacía y en silencio: la siembra arrancaba después y fallaba
            // tabla por tabla con «nombre de objeto no válido».
            if (persistence.IsRelational)
            {
                await platformDbContext.Database.MigrateAsync();

                // G-072: el read model (proyecciones de fase 1) vive en la MISMA base
                // pero en su propio DbContext con sus propias migraciones. Sin migrarlo
                // aquí, su tabla (`Authorization.PermissionTemplateReadModels`) no existe
                // en un despliegue desde cero, y la primera proyección
                // (`PermissionTemplatePublishedEvent`, disparada por el seed) falla con
                // 42P01. Se migra en el mismo arranque, junto al contexto de plataforma.
                var readModelDbContext = scope.ServiceProvider
                    .GetRequiredService<Evolith.Ums.ReadModels.ReadModelDbContext>();
                await readModelDbContext.Database.MigrateAsync();
            }
        }

        // Arranque en frío: el mínimo con el que una base recién migrada puede empezar a operarse
        // —inquilino propietario de gestión y cuenta administradora—, sin dato de demostración
        // alguno. Va DESPUÉS de migrar (necesita las tablas) y ANTES de la siembra de desarrollo,
        // para que en un entorno de desarrollo el sembrador encuentre el inquilino ya creado y lo
        // reconcilie en vez de duplicarlo.
        //
        // Apagado por omisión. Es lo que permite inicializar QA y Production, donde SeedDevData
        // está en false y no debe encenderse: sin esto, esas bases quedaban con las 51 tablas
        // vacías y sin ningún usuario con el que entrar.
        if (persistence.Bootstrap.Enabled)
        {
            // Dos guardas que cierran fallos silenciosos, no hipotéticos.
            //
            // (1) Sin base relacional migrada, el arranque escribiría contra tablas que no existen
            //     —o, peor, contra repositorios en memoria: informaría de éxito y el administrador
            //     desaparecería en el siguiente reinicio—. `InitializePlatformStoreOnStartup` es
            //     justo lo que Production trae en `false`, así que la llamada no puede quedar fuera
            //     de esa condición sin quedar fuera también de las migraciones.
            if (!persistence.IsRelational || !persistence.InitializePlatformStoreOnStartup)
            {
                throw new InvalidOperationException(
                    "Persistence:Bootstrap exige una base relacional ya migrada en este arranque "
                    + "(Provider relacional + InitializePlatformStoreOnStartup=true). Enciéndelo en el "
                    + "proceso que migra, no en las réplicas que sirven tráfico.");
            }

            // (2) Con SeedDevData encendido, el sembrador de desarrollo corre DESPUÉS y crea sus
            //     propias cuentas. Convivir es legítimo en desarrollo, pero el arranque en frío
            //     exige tabla vacía, así que sería el sembrador quien mandase — y quien encendió
            //     ambas cosas casi nunca quería eso.
            if (persistence.SeedDevData)
            {
                app.Logger.LogWarning(
                    "Persistence:Bootstrap y Persistence:SeedDevData están ambos encendidos. El arranque "
                    + "en frío solo actúa sobre una plataforma sin cuentas, así que en una base ya sembrada "
                    + "no hará nada. Para un arranque limpio, apaga SeedDevData.");
            }

            await Evolith.Ums.Infrastructure.Persistence.Seeders.PlatformBootstrapper.BootstrapAsync(
                app.Services, app.Environment.IsProduction());
        }

        // G-127: la siembra del dataset FS-25 (datos de referencia + demo) se gobierna por el
        // flag SeedDevData DESACOPLADO del entorno, para habilitar el stage UAT (usuarios
        // personas reales que ingresan y encuentran datos visibles). Guarda de defensa en
        // profundidad: NUNCA se siembra en Production, aunque el flag venga en true por una
        // config errónea. Los backdoors propios de Development —DevAuth, el endpoint Pact
        // `/_pact/provider-states` y Swagger— siguen gated a IsDevelopment() por separado (ver
        // UseUmsApiPipeline / MapUmsApiSurface): UAT obtiene los datos SIN exponer esos backdoors.
        //
        // La guarda pasa de ignorar en silencio a FALLAR. Ignorar producía el peor desenlace: un
        // Production que arranca creyéndose sembrado, sin estarlo, y sin que nada lo dijera. La
        // combinación es imposible por definición —el dataset FS-25 trae credenciales por defecto—,
        // así que es un error de configuración y debe tratarse como tal (OWASP A05).
        if (persistence.SeedDevData && app.Environment.IsProduction())
        {
            throw new InvalidOperationException(
                "Persistence:SeedDevData está en true con ASPNETCORE_ENVIRONMENT=Production. El dataset "
                + "de desarrollo trae credenciales por defecto y no puede sembrarse en producción. Para "
                + "inicializar un despliegue productivo usa Persistence:Bootstrap.");
        }

        // G-270: el transporte de notificaciones no puede quedar en el simulado sin que se note.
        //
        // El simulado vuelca asunto y cuerpo a la consola y devuelve éxito. En un entorno donde
        // nadie lee esa consola —bajo IIS, con `stdoutLogEnabled="false"`, no la lee nadie— eso es
        // indistinguible de no tener notificaciones: el alta de un usuario da por entregada una
        // credencial que no salió de la máquina. En Production se rechaza; en cualquier otro
        // entorno desplegado se avisa, porque en UAT es una postura legítima mientras se decide.
        var notificaciones = app.Services
            .GetRequiredService<IOptions<Evolith.Ums.Infrastructure.Services.Notifications.NotificationOptions>>()
            .Value;

        if (notificaciones.Provider == Evolith.Ums.Infrastructure.Services.Notifications.NotificationProvider.Simulated)
        {
            if (app.Environment.IsProduction())
            {
                throw new InvalidOperationException(
                    "Notifications:Provider está en Simulated con ASPNETCORE_ENVIRONMENT=Production. Ese "
                    + "adaptador no entrega nada y además escribe el cuerpo —tokens y contraseñas "
                    + "temporales— al log. Declara Provider=Smtp con su relay.");
            }

            if (!app.Environment.IsDevelopment())
            {
                app.Logger.LogWarning(
                    "Notifications:Provider=Simulated en el entorno {Entorno}. NINGUNA notificación se "
                    + "entrega: restablecimientos, altas y aprobaciones se dan por enviados sin salir de "
                    + "esta máquina. Declara Provider=Smtp para entregarlas de verdad.",
                    app.Environment.EnvironmentName);
            }
        }

        if (persistence.SeedDevData)
        {
            await app.SeedDevelopmentDataAsync();
        }

        return app;
    }

    public static WebApplication UseUmsApiPipeline(this WebApplication app)
    {
        // G-175: primero de la cadena, para comprimir también lo que escriban los middlewares
        // posteriores. El grafo de autorización que devuelve el login son 43-50 KB de JSON muy
        // repetitivo: medido sobre capturas reales, Brotli lo deja en 3,7-5,3 KB (9-12x).
        app.UseResponseCompression();

        app.UseSessionTracking();
        app.UseSerilogRequestLogging(opts =>
        {
            opts.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
            {
                var requestContext = httpContext.RequestServices.GetRequiredService<IRequestContext>();
                diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value ?? string.Empty);
                diagnosticContext.Set("SessionTrackingId", requestContext.SessionTrackingId ?? string.Empty);
                diagnosticContext.Set("TraceId", requestContext.TraceId ?? Activity.Current?.TraceId.ToString() ?? string.Empty);
                diagnosticContext.Set("SpanId", requestContext.SpanId ?? Activity.Current?.SpanId.ToString() ?? string.Empty);
                diagnosticContext.Set("ErrorId", UserFacingErrorContext.GetErrorId(httpContext) ?? string.Empty);
            };
            opts.GetLevel = (ctx, _, _) =>
                ctx.Request.Path.StartsWithSegments("/health")
                    ? Serilog.Events.LogEventLevel.Verbose
                    : Serilog.Events.LogEventLevel.Information;
        });

        app.UseCulture();
        // ADR-0096 / ADR-UMS-085: abre la transacción funcional por fuera del manejador global
        // para garantizar el desenlace y que el localizador legible viaje en la respuesta de error.
        app.UseFunctionalTransaction();
        app.UseGlobalExceptionHandler();
        // G-248: aquí estaba `UseRateLimiter()`, y estaba mal por dos motivos a la vez. Contaba en
        // proceso —con N réplicas el cupo efectivo era N veces el declarado— y corría ANTES de
        // `UseAuthentication`, así que `HttpContext.User` venía vacío y la clave de reparto caía
        // siempre a la IP: dos usuarios distintos compartían cupo. El límite vive ahora más abajo,
        // después de autenticar, que es el único punto donde se sabe a quién se limita.

        // ADR-UMS-101: el DOCUMENTO OpenAPI y la INTERFAZ Swagger son cosas distintas y se
        // gobiernan por separado.
        //
        // El documento es el contrato de la API: estático, sin efectos y legible por máquinas.
        // Lo consumen los generadores de cliente, los validadores de contrato y los dos SDK.
        // Se sirve SIEMPRE, en todo entorno. Ocultarlo no defiende nada —quien puede llamar a la
        // API ya puede descubrir sus rutas— y a cambio deja al consumidor sin forma de obtener el
        // contrato: hasta ahora solo existía levantando la aplicación en Development.
        app.UseSwagger();

        // La interfaz SÍ es superficie: un cliente interactivo que dispara peticiones reales
        // contra el entorno. Se gobierna por ApiSettings:EnableSwagger, que hasta ADR-UMS-101
        // estaba en los cuatro appsettings sin que ningún código la leyera. Queda encendida en
        // Development y UAT —donde existe para explorar— y apagada en Production.
        if (app.Configuration.GetValue("ApiSettings:EnableSwagger", false))
        {
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "UMS Tenant API v1");
                c.RoutePrefix = "swagger";
            });
        }

        app.UseCors("DefaultPolicy");
        app.UseSecurityHeaders();
        if (app.Environment.IsDevelopment())
        {
            app.UseDevAuth();
        }
        app.UseAuthentication();
        // G-248: DESPUÉS de autenticar —para que el cupo sea de quien llama y no de una IP que
        // comparten todos— y ANTES de autorizar. El orden importa en los dos lados. Puesto antes de
        // autenticar, la identidad aún no está resuelta y dos usuarios distintos comparten cupo.
        // Puesto después de autorizar, las peticiones rechazadas con 401 no llegan a contarse, y
        // son justo las que más interesa contar cuando alguien prueba credenciales a ciegas.
        app.UseLimiteDePeticiones(app.Configuration);
        app.UseAuthorization();
        app.UseIdempotency(); // FIX-07: Must run after auth so cached responses are never served to unauthenticated callers.
        app.UseTenantContext();
        app.UseTokenRevocation();
        if (!app.Environment.IsDevelopment())
            app.UseHttpsRedirection();

        return app;
    }

    private static async Task SeedDevelopmentDataAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        await CoreDevDataSeeder.SeedAllAsync(scope.ServiceProvider);
    }

    public static WebApplication MapUmsApiSurface(this WebApplication app)
    {
        var versionedGroup = app.CreateVersionedApiGroup();

        app.MapHealthSurface();
        app.MapAuthEndpoints();
        app.MapClientAuthEndpoints();

        // El material publico con el que los satelites verifican (ADR-0157 §4.1). Va
        // fuera de /api y sin version: `.well-known` es una ruta reservada por RFC 8615
        // y los validadores de .NET y de Node la buscan ahi, no donde nos convenga.
        app.MapJwksEndpoints();

        versionedGroup.MapUmsCommandEndpoints();
        versionedGroup.MapUmsQueryEndpoints();

        return app;
    }

    internal static RouteGroupBuilder CreateVersionedApiGroup(this WebApplication app)
    {
        var versionSet = app.NewApiVersionSet()
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        return app.MapGroup("api/v{apiVersion:apiVersion}")
            .WithApiVersionSet(versionSet);
    }

    internal static IEndpointRouteBuilder MapHealthSurface(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/health/live", () => Results.Ok(new
        {
            Status = "Alive",
            Service = "UMS API",
            Timestamp = DateTimeOffset.UtcNow,
        }))
        .WithName("GetLiveness")
        .WithTags("Platform")
        .ExcludeFromDescription();

        endpoints.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
        {
            Predicate = hc => hc.Tags.Contains("ready"),
            ResponseWriter = HealthCheckResponseWriter.WriteJsonAsync,
        })
        .WithName("GetReadiness")
        .WithTags("Platform");

        endpoints.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
        {
            ResponseWriter = HealthCheckResponseWriter.WriteJsonAsync,
        })
        .WithName("GetHealth")
        .WithTags("Platform");

        return endpoints;
    }

    internal static RouteGroupBuilder MapUmsCommandEndpoints(this RouteGroupBuilder versionedGroup)
    {
        versionedGroup
            .MapIdentityCommandEndpoints()
            .MapAuthorizationCommandEndpoints()
            .MapAuditCommandEndpoints()
            .MapApprovalsCommandEndpoints()
            .MapIgaCommandEndpoints()
            .MapConfigurationCommandEndpoints();

        return versionedGroup;
    }

    internal static RouteGroupBuilder MapUmsQueryEndpoints(this RouteGroupBuilder versionedGroup)
    {
        versionedGroup
            .MapIdentityQueryEndpoints()
            .MapAuthorizationQueryEndpoints()
            .MapAuditQueryEndpoints()
            .MapApprovalsQueryEndpoints()
            .MapIgaQueryEndpoints()
            .MapConfigurationQueryEndpoints();

        return versionedGroup;
    }

    internal static RouteGroupBuilder MapIdentityCommandEndpoints(this RouteGroupBuilder versionedGroup)
    {
        versionedGroup.MapTenantEndpoints();
        versionedGroup.MapTenantBranchEndpoints();
        versionedGroup.MapTenantIdentityProviderEndpoints();
        versionedGroup.MapUserAccountEndpoints();
        versionedGroup.MapDelegationEndpoints();
        versionedGroup.MapOnboardingInboxEndpoints();

        return versionedGroup;
    }

    internal static RouteGroupBuilder MapIdentityQueryEndpoints(this RouteGroupBuilder versionedGroup)
    {
        versionedGroup.MapTenantQueryEndpoints();
        versionedGroup.MapBranchQueryEndpoints();
        versionedGroup.MapIdentityProviderQueryEndpoints();
        versionedGroup.MapUserAccountQueryEndpoints();
        versionedGroup.MapDelegationQueryEndpoints();

        return versionedGroup;
    }

    internal static RouteGroupBuilder MapAuthorizationCommandEndpoints(this RouteGroupBuilder versionedGroup)
    {
        versionedGroup.MapProfileEndpoints();
        versionedGroup.MapSystemSuiteEndpoints();
        versionedGroup.MapPermissionTemplateEndpoints();
        versionedGroup.MapRoleEndpoints();
        versionedGroup.MapTemplateAssignmentRuleEndpoints();

        return versionedGroup;
    }

    internal static RouteGroupBuilder MapAuthorizationQueryEndpoints(this RouteGroupBuilder versionedGroup)
    {
        versionedGroup.MapProfileQueryEndpoints();
        versionedGroup.MapSystemSuiteQueryEndpoints();
        versionedGroup.MapPermissionTemplateQueryEndpoints();

        return versionedGroup;
    }

    internal static RouteGroupBuilder MapAuditCommandEndpoints(this RouteGroupBuilder versionedGroup)
    {
        versionedGroup.MapAuditRecordEndpoints();
        return versionedGroup;
    }

    internal static RouteGroupBuilder MapAuditQueryEndpoints(this RouteGroupBuilder versionedGroup)
    {
        versionedGroup.MapAuditRecordQueryEndpoints();
        return versionedGroup;
    }

    internal static RouteGroupBuilder MapApprovalsCommandEndpoints(this RouteGroupBuilder versionedGroup)
    {
        versionedGroup.MapApprovalWorkflowEndpoints();
        versionedGroup.MapApprovalRequestEndpoints();
        versionedGroup.MapDocumentTypeEndpoints();
        versionedGroup.MapUserDocumentEndpoints();
        versionedGroup.MapAccessEnforcementPolicyEndpoints();
        versionedGroup.MapNotificationRuleEndpoints();

        return versionedGroup;
    }

    internal static RouteGroupBuilder MapApprovalsQueryEndpoints(this RouteGroupBuilder versionedGroup)
    {
        versionedGroup.MapApprovalWorkflowQueryEndpoints();
        versionedGroup.MapApprovalRequestQueryEndpoints();
        versionedGroup.MapDocumentTypeQueryEndpoints();
        versionedGroup.MapUserDocumentQueryEndpoints();
        versionedGroup.MapAccessEnforcementPolicyQueryEndpoints();
        versionedGroup.MapNotificationRuleQueryEndpoints();

        return versionedGroup;
    }

    internal static RouteGroupBuilder MapIgaCommandEndpoints(this RouteGroupBuilder versionedGroup)
    {
        versionedGroup.MapRolePromotionRequestEndpoints();

        return versionedGroup;
    }

    internal static RouteGroupBuilder MapIgaQueryEndpoints(this RouteGroupBuilder versionedGroup)
    {
        versionedGroup.MapRolePromotionRequestQueryEndpoints();
        versionedGroup.MapRoleMaturityStatusQueryEndpoints();

        return versionedGroup;
    }

    internal static RouteGroupBuilder MapConfigurationCommandEndpoints(this RouteGroupBuilder versionedGroup)
    {
        versionedGroup.MapAppConfigurationEndpoints();
        versionedGroup.MapFeatureFlagEndpoints();
        versionedGroup.MapIdpConfigurationEndpoints();
        versionedGroup.MapParameterEndpoints();

        return versionedGroup;
    }

    internal static RouteGroupBuilder MapConfigurationQueryEndpoints(this RouteGroupBuilder versionedGroup)
    {
        versionedGroup.MapAppConfigurationQueryEndpoints();
        versionedGroup.MapFeatureFlagQueryEndpoints();
        versionedGroup.MapIdpConfigurationQueryEndpoints();
        versionedGroup.MapParameterCatalogQueryEndpoints();

        return versionedGroup;
    }

    /// <summary>
    /// Declara en el arranque qué implementación de estado compartido quedó activa.
    ///
    /// Existe porque el modo degradado era indistinguible del correcto: si la cadena de Redis no
    /// resolvía, la API arrancaba igual de contenta con las implementaciones en memoria y el
    /// clúster se comportaba mal solo bajo carga y con más de una réplica (G-169). Un despliegue
    /// multi-réplica que lea `InMemory` en esta línea sabe que va a servir sesiones incoherentes
    /// ANTES de que se lo diga un usuario.
    /// </summary>
    private static void DeclararEstadoDistribuido(WebApplication app)
    {
        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Ums.Startup");

        var revocacion = app.Services.GetRequiredService<ITokenRevocationStore>().GetType().Name;
        var configuracion = app.Services.GetRequiredService<Evolith.Ums.Application.Configuration.Services.IConfigurationCache>().GetType().Name;
        var distribuido = !revocacion.StartsWith("InMemory", StringComparison.Ordinal);

        if (distribuido)
        {
            logger.LogInformation(
                "Estado compartido: DISTRIBUIDO. Revocación={Revocacion}, Configuración={Configuracion}. Apto para múltiples réplicas.",
                revocacion, configuracion);
            return;
        }

        logger.LogWarning(
            "Estado compartido: EN MEMORIA (sin Redis). Revocación={Revocacion}, Configuración={Configuracion}. " +
            "Válido solo con UNA réplica: con más de una, la revocación de tokens y la configuración no se propagan entre pods. " +
            "Configure `Redis:Connection` o la variable `REDIS_CONNECTION` para habilitar el modo distribuido (G-169).",
            revocacion, configuracion);
    }

}

internal sealed class UmsApiDocumentationBootstrapper : IBootstrapper<IServiceCollection>
{
    private readonly IConfiguration _configuration;

    public UmsApiDocumentationBootstrapper(IServiceCollection services, IConfiguration configuration)
    {
        Result = services;
        _configuration = configuration;
    }

    public IServiceCollection? Result { get; private set; }

    public void Run()
    {
        ArgumentNullException.ThrowIfNull(Result);

        Result.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "UMS Tenant API",
                Version = "v1",
                Description = "User Management System — modular monolith API with REST commands and queries, prepared for SQL Server platform persistence.",
            });

            options.AddSecurityDefinition("DevUserId", new OpenApiSecurityScheme
            {
                Name = "X-User-Id",
                Type = SecuritySchemeType.ApiKey,
                In = ParameterLocation.Header,
                Description = "Development-only header. Sets the authenticated user id used by command handlers.",
            });

            // HARDENING-02: JWT Bearer security definition for production Swagger UI
            options.AddSwaggerBearerAuth();

            options.OperationFilter<LanguageHeaderOperationFilter>();
        });

        Result.AddCors(options =>
        {
            options.AddPolicy("DefaultPolicy", policy =>
            {
                policy
                    .WithOrigins(_configuration.GetValue<string>("AllowedOrigins")?.Split(',') ?? Array.Empty<string>())
                    .AllowAnyMethod()
                    .AllowAnyHeader()
                    .AllowCredentials()
                    .WithExposedHeaders(
                        ObservabilityHeaders.SessionTrackingId,
                        "traceparent",
                        "api-supported-versions",
                        "api-deprecated-versions");
            });
        });
    }
}
