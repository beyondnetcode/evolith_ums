namespace Evolith.Ums.Presentation.Extensions;

using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

/// <summary>
/// Nombres de los esquemas de autenticación de UMS. Se centralizan aquí porque un esquema es
/// una cadena mágica: repetirla endpoint por endpoint es la vía más corta a que uno de ellos
/// autentique por el esquema equivocado sin que nadie lo note (G-191).
/// </summary>
public static class UmsAuthSchemes
{
    /// <summary>Cookie `ums.session`: la sesión del portal web.</summary>
    public const string Cookie = CookieAuthenticationDefaults.AuthenticationScheme;

    /// <summary>Portador JWT firmado por UMS (RS256): los sistemas satélite.</summary>
    public const string Portador = JwtBearerDefaults.AuthenticationScheme;

    /// <summary>
    /// Esquema de política que decide, por petición, cuál de los dos anteriores aplica.
    /// Es el esquema POR DEFECTO del host.
    /// </summary>
    public const string Automatico = "UmsAuto";
}

/// <summary>
/// Políticas de autorización nombradas de UMS.
/// </summary>
public static class UmsAuthPolicies
{
    /// <summary>
    /// Superficie de sistemas satélite: exige portador VÁLIDO y descarta la cookie de forma
    /// explícita.
    ///
    /// Por qué una política nombrada y no repetir el esquema en cada endpoint: fijar el esquema
    /// suelto (`new AuthorizeAttribute { AuthenticationSchemes = "Bearer" }`) obliga a repetir la
    /// cadena en cada `MapGet`, y basta que un endpoint nuevo la omita para que herede el esquema
    /// por defecto y vuelva a autenticar por cookie — exactamente el defecto que G-191 corrige.
    /// Con la política, el contrato «esto es para satélites» se declara UNA vez, se reutiliza y
    /// sobrevive a cualquier cambio futuro del esquema por defecto.
    /// </summary>
    public const string Satelite = "UmsSatelite";
}

/// <summary>
/// Configura la autenticación de UMS: dos esquemas que conviven.
///
/// <para><b>Cookie (`ums.session`)</b> — el portal web. `POST /api/v1/auth/login` firma la cookie
/// y el navegador la reenvía sola.</para>
///
/// <para><b>Portador (JWT RS256 emitido por UMS)</b> — los sistemas satélite. `POST
/// /api/v1/client/authenticate` devuelve el token y el satélite lo presenta en `Authorization`.</para>
///
/// <para><b>Cuál se aplica</b>: el esquema por defecto es <see cref="UmsAuthSchemes.Automatico"/>,
/// un esquema de política que reenvía a portador cuando la petición trae `Authorization: Bearer`
/// y a cookie en cualquier otro caso. Antes (G-191) el defecto era la cookie fija: la cabecera
/// `Authorization` se ignoraba y el reto de la cookie contestaba con un 302 al `LoginPath`, de
/// modo que NINGÚN satélite podía consumir un endpoint autenticado — ni en desarrollo, donde el
/// manejador de portador ni siquiera se registraba.</para>
///
/// <para><b>La firma</b>: UMS emite sus propios tokens en RS256 con la clave privada de
/// `Jwt:PrivateKeyPem` (<c>MaterialDeFirma</c>) y publica SOLO la pública en
/// `/.well-known/jwks.json`, así que el manejador de portador valida ESO, en todos los
/// entornos. Quien puede verificar un portador de UMS ya no puede emitirlo (ADR-0157, G-199).
/// El manejador anterior apuntaba a una `Authority` OIDC externa: la API no estaba preparada para
/// validar lo que ella misma firma. La federación con un IdP externo ocurre en el LOGIN (motor de
/// autenticación, `AuthenticateUserCommand`), no en el servidor de recursos: quien entra por un
/// IdP también sale con un token de UMS.</para>
///
/// Claims que transporta el portador (ver <c>JwtTokenService</c>):
///   sub / email / name, tenant_id o tenant_code, role, scope*, feature*, is_internal_admin.
/// </summary>
public static class AuthenticationExtensions
{
    public static IServiceCollection AddUmsAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        ConfigurarProteccionDeDatos(services, configuration);

        // Fail-fast de arranque. La regla vive en MaterialDeFirma —que es quien firma— y se
        // invoca aquí porque ese singleton es perezoso: sin esto, un despliegue sin clave
        // privada arranca «bien» y revienta en la primera petición (G-199, ADR-0157 §4.7).
        Services.MaterialDeFirma.ExigirClaveFueraDeDesarrollo(configuration["Jwt:PrivateKeyPem"], environment);

        services
            .AddAuthentication(UmsAuthSchemes.Automatico)
            .AddPolicyScheme(UmsAuthSchemes.Automatico, "Portador si viaja Authorization; cookie si no", options =>
            {
                // El selector rige TODAS las operaciones del esquema (autenticar, retar, prohibir),
                // así que el reto de una petición con portador inválido lo emite el manejador de
                // portador → 401 con `WWW-Authenticate`, nunca una redirección de cookie.
                options.ForwardDefaultSelector = contexto =>
                {
                    string? cabecera = contexto.Request.Headers.Authorization;
                    return cabecera is not null
                        && cabecera.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                            ? UmsAuthSchemes.Portador
                            : UmsAuthSchemes.Cookie;
                };
            })
            .AddCookie(UmsAuthSchemes.Cookie, options =>
            {
                options.Cookie.Name         = "ums.session";
                options.Cookie.HttpOnly     = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                options.ExpireTimeSpan      = TimeSpan.FromHours(1);
                options.SlidingExpiration   = true;
                options.LoginPath           = "/api/v1/auth/login";
                options.AccessDeniedPath    = "/api/v1/auth/denied";

                // Un 401 tiene que ser un 401. El manejador de cookie, pensado para páginas,
                // responde al reto con 302 hacia el formulario de acceso; un cliente de API no
                // sigue un `Location` y lo interpreta como éxito o como fallo de red. Bajo
                // `/api/**` la redirección se sustituye por el código que corresponde.
                options.Events = new CookieAuthenticationEvents
                {
                    OnRedirectToLogin        = ctx => ResponderSinRedirigir(ctx, StatusCodes.Status401Unauthorized),
                    OnRedirectToAccessDenied = ctx => ResponderSinRedirigir(ctx, StatusCodes.Status403Forbidden),
                };
            })
            .AddJwtBearer(UmsAuthSchemes.Portador, options =>
            {
                // Sin `Authority`: la clave la conoce el propio proceso. No hay descubrimiento OIDC
                // que resolver ni red que dependa de estar disponible para validar un token.

                // Se conserva el mapeo entrante de claims (sub → NameIdentifier, email → Email,
                // name → Name) para que el principal del portador y el de la cookie se lean con
                // los MISMOS tipos de claim; de eso dependen `UserContext`, la revocación y el
                // particionado del limitador de tasa.
                options.MapInboundClaims = true;

                options.Events = new JwtBearerEvents
                {
                    OnAuthenticationFailed = ctx =>
                    {
                        // Un token que no valida es un evento de seguridad: se registra el tipo de
                        // fallo, nunca el token.
                        var logger = ctx.HttpContext.RequestServices
                            .GetRequiredService<ILoggerFactory>()
                            .CreateLogger("UMS.Authentication");

                        logger.LogWarning(
                            "Falló la validación del portador. Path={Path} Error={Error}",
                            ctx.Request.Path,
                            ctx.Exception.GetType().Name);

                        return Task.CompletedTask;
                    },
                    OnTokenValidated = ctx =>
                    {
                        // Corre DESPUÉS de verificar la firma: los claims ya son de fiar.
                        var tenantContext = ctx.HttpContext.RequestServices
                            .GetService<Evolith.Ums.Application.Common.Interfaces.ITenantContext>();

                        if (tenantContext is null) return Task.CompletedTask;

                        var tenantIdClaim = ctx.Principal?.FindFirst("tenant_id")?.Value
                                         ?? ctx.Principal?.FindFirst("org_id")?.Value;

                        var isInternalAdminClaim = ctx.Principal?.FindFirst("is_internal_admin")?.Value;

                        if (Guid.TryParse(tenantIdClaim, out var tenantId))
                        {
                            var isInternalAdmin = string.Equals(
                                isInternalAdminClaim, "true", StringComparison.OrdinalIgnoreCase);

                            // El contexto puede venir ya inicializado por otro middleware; que lo
                            // esté no es un error.
                            try
                            {
                                tenantContext.Initialize(tenantId, isInternalAdmin);
                            }
                            catch (InvalidOperationException)
                            {
                                // Ya inicializado: se respeta el valor vigente.
                            }
                        }

                        return Task.CompletedTask;
                    },
                };
            });

        // Los parámetros de validación se resuelven TARDE, contra la configuración final del host y
        // no contra la que hubiera en el momento del registro. No es un rodeo: los hosts de prueba
        // (y cualquier fuente que se añada después, como Key Vault) inyectan su configuración
        // mientras se construye la aplicación, es decir DESPUÉS de este método. Capturar aquí el
        // secreto producía el peor fallo posible —la API firmaba con una clave y validaba con otra,
        // rechazando sus propios tokens con `SecurityTokenSignatureKeyNotFound`—, que es justo el
        // mismo pitfall de temporización que ya se documentó con la cadena de conexión (G-014).
        // `JwtTokenService`, que firma, también lee la configuración tarde: así ambos lados
        // coinciden por construcción.
        services.AddOptions<JwtBearerOptions>(UmsAuthSchemes.Portador)
            .Configure<IConfiguration, Services.MaterialDeFirma>((options, config, material) =>
            {
                var emisor = config["Jwt:Issuer"] ?? "ums-api";
                var audiencia = config["Jwt:Audience"] ?? "ums-web-app";

                // Emisores/audiencias adicionales aceptados (p. ej. un alias de despliegue). El
                // emisor y la audiencia propios SIEMPRE se aceptan: son los que UMS estampa al firmar.
                var emisoresValidos = new[] { emisor }
                    .Concat(config.GetSection("Authentication:ValidIssuers").Get<string[]>() ?? [])
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();

                var audienciasValidas = new[] { audiencia }
                    .Concat(config.GetSection("Authentication:ValidAudiences").Get<string[]>() ?? [])
                    .Append(config["Authentication:Audience"])
                    .Where(valor => !string.IsNullOrWhiteSpace(valor))
                    .Select(valor => valor!)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();

                options.RequireHttpsMetadata = config.GetValue("Authentication:RequireHttpsMetadata", true);

                // SOLO RS256 (ADR-0157 §4.9, etapa E3 — cerrada). La ventana de transición
                // aceptaba ADEMÁS la clave simétrica porque los portadores HS256 emitidos antes
                // de E1 seguían vivos hasta caducar y rechazarlos habría echado de su sesión a
                // quien la tuviera abierta. No había ninguno: el producto nunca llegó a
                // producción bajo el esquema simétrico, así que no había sesión que proteger y
                // la ventana se cierra sin coste. El secreto compartido se borra del código y NO
                // queda tras una bandera: una bandera que reactiva HS256 es la vulnerabilidad
                // con un interruptor.

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKeys        = [material.ClavePublica],
                    // La lista blanca de UN solo algoritmo es lo que impide la confusión de
                    // `alg` de RFC 8725 §2.1: sin ella, un token que declarase HS256 podría
                    // intentar verificarse contra la clave RSA **pública** —que es material
                    // publicado— y pasar, con lo que cualquiera emitiría portadores válidos de
                    // cualquier usuario y cualquier inquilino.
                    ValidAlgorithms          = [SecurityAlgorithms.RsaSha256],
                    ValidateIssuer           = true,
                    ValidIssuers             = emisoresValidos,
                    ValidateAudience         = true,
                    ValidAudiences           = audienciasValidas,
                    ValidateLifetime         = true,
                    ClockSkew                = TimeSpan.FromSeconds(30),
                };
            });

        services.AddAuthorization(options =>
        {
            options.AddPolicy(UmsAuthPolicies.Satelite, policy => policy
                .AddAuthenticationSchemes(UmsAuthSchemes.Portador)
                .RequireAuthenticatedUser());
        });

        return services;
    }

    /// <summary>
    /// Sustituye la redirección del manejador de cookie por un código de estado cuando la petición
    /// va a la API. Fuera de `/api/**` (p. ej. Swagger) se conserva el comportamiento de navegador.
    /// </summary>
    private static Task ResponderSinRedirigir(
        Microsoft.AspNetCore.Authentication.RedirectContext<CookieAuthenticationOptions> contexto,
        int codigo)
    {
        if (contexto.Request.Path.StartsWithSegments("/api"))
        {
            contexto.Response.StatusCode = codigo;
            return Task.CompletedTask;
        }

        contexto.Response.Redirect(contexto.RedirectUri);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Configures the Swagger UI to accept Bearer tokens.
    /// Only adds the security definition; does not enforce it on endpoints
    /// (authorization policy enforcement is separate).
    /// </summary>
    public static void AddSwaggerBearerAuth(this Swashbuckle.AspNetCore.SwaggerGen.SwaggerGenOptions options)
    {
        options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
        {
            Name         = "Authorization",
            Type         = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
            Scheme       = "bearer",
            BearerFormat = "JWT",
            In           = Microsoft.OpenApi.Models.ParameterLocation.Header,
            Description  = "Enter a valid JWT access token. Example: 'eyJhbGci...'",
        });

        options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
        {
            {
                new Microsoft.OpenApi.Models.OpenApiSecurityScheme
                {
                    Reference = new Microsoft.OpenApi.Models.OpenApiReference
                    {
                        Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                        Id   = "Bearer",
                    },
                },
                Array.Empty<string>()
            },
        });
    }

    /// <summary>
    /// Fija la identidad del anillo de claves de Data Protection y, con Redis disponible, lo
    /// persiste para que TODAS las réplicas compartan las mismas claves.
    ///
    /// Sin esto, cada pod genera su propio anillo efímero: la cookie `ums.session` emitida por
    /// un pod no la descifra ninguno de los demás, así que un despliegue con más de una réplica
    /// produce 401 aparentemente aleatorios y cada reinicio invalida todas las sesiones vivas
    /// (G-169). `SetApplicationName` va SIEMPRE —también sin Redis— porque el nombre por defecto
    /// deriva de la ruta del contenido: dos pods con rutas distintas ya no se entienden aunque
    /// compartieran el almacén.
    /// </summary>
    private static void ConfigurarProteccionDeDatos(IServiceCollection services, IConfiguration configuration)
    {
        var proteccion = services.AddDataProtection().SetApplicationName("evolith-ums");

        var redis = Evolith.Ums.Infrastructure.Configuration.CadenaDeRedis.Normalizar(
            configuration["Redis:Connection"] ?? configuration["REDIS_CONNECTION"]);

        if (string.IsNullOrWhiteSpace(redis))
        {
            // Anillo en memoria: válido para desarrollo, pruebas y despliegue de UNA réplica.
            return;
        }

        proteccion.PersistKeysToStackExchangeRedis(
            StackExchange.Redis.ConnectionMultiplexer.Connect(redis),
            "ums:dataprotection-keys");
    }
}
