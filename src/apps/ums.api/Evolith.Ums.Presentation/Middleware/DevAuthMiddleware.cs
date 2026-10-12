namespace Evolith.Ums.Presentation.Middleware;

using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Evolith.Ums.Application.Common.Interfaces;
using Evolith.Ums.Domain.Authorization.Graph;
using Evolith.Ums.Domain.Identity;

public sealed class DevAuthMiddleware
{
    // La identidad por omision es el administrador SEMBRADO de EVOLITH, no una cadena inventada.
    // Con `dev-user` —lo que habia— la peticion se atribuia a un usuario que no existe en ninguna
    // tabla: la auditoria registraba un identificador fantasma y, desde G-280, no habia a quien
    // construirle el grafo, asi que la identidad de desarrollo llegaba SIN scopes y solo podia
    // pasar por el atajo de Admin Root. Un usuario real hace ambas cosas posibles.
    private const string DefaultUserId = "5f4e3d14-1b0a-9f8e-7d6c-543210987654";
    private const string DefaultUserName = "admin@evolith.example";
    // ADR-0071 / FS-26: el Admin Root (propietario de gestión) es EVOLITH. La conveniencia
    // de desarrollo (peticiones no autenticadas) actúa por defecto como EVOLITH, y el
    // privilegio transversal is_internal_admin se otorga a su tenant. La derivación
    // autoritativa (login real) vive en AuthEndpoints: is_internal_admin = Tenant.IsManagementOwner.
    private const string DefaultTenantId = "5f4e3d2c-1b0a-9f8e-7d6c-543210987654"; // EVOLITH (Tenant Raíz)
    private const string ManagementOwnerTenantId = "5f4e3d2c-1b0a-9f8e-7d6c-543210987654"; // EVOLITH
    private const string UserIdHeader = "X-User-Id";
    private const string UserNameHeader = "X-User-Name";
    private const string TenantIdHeader = "X-Tenant-Id";
    private const string IsInternalAdminHeader = "X-Is-Internal-Admin";
    private const string DisableDevAuthHeader = "X-Disable-Dev-Auth";
    private const string AuthorizationHeader = "Authorization";
    private const string SessionCookieName = "ums.session";

    private readonly RequestDelegate _next;
    private readonly IHostEnvironment _environment;

    public DevAuthMiddleware(RequestDelegate next, IHostEnvironment environment)
    {
        _next = next;
        _environment = environment;
    }

    public async Task InvokeAsync(
        HttpContext context,
        ITenantContext tenantContext,
        IUserAccountRepository userAccounts,
        IAuthorizationGraphBuilder graphBuilder)
    {
        if (!_environment.IsDevelopment())
        {
            await _next(context);
            return;
        }

        // Public authentication endpoints must execute with the real anonymous context.
        // Default dev claims would force the internal admin tenant (EVOLITH) and break tenant-scoped
        // login, signup, forgot-password, and session bootstrap flows. Esto cubre tanto el flujo
        // interno (/api/v1/auth) como el de autenticación de sistemas cliente (/api/v1/client): sin
        // exentar /client, DevAuthMiddleware fijaba el contexto a EVOLITH y el usuario CLIENT
        // (tenant-scoped) no se resolvía → AUTH_006/401 (G-042). El tenant debe resolverse por el
        // TenantCode del cuerpo, no por el default de dev.
        if (context.Request.Path.StartsWithSegments("/api/v1/auth")
            || context.Request.Path.StartsWithSegments("/api/v1/client"))
        {
            await _next(context);
            return;
        }

        if (string.Equals(
                context.Request.Headers[DisableDevAuthHeader].FirstOrDefault(),
                "true",
                StringComparison.OrdinalIgnoreCase))
        {
            // G-042 (endurecimiento): X-Disable-Dev-Auth desactiva la inyección de claims de
            // desarrollo para poder ejercer la autenticación REAL (bearer/cookie). NO debe
            // convertirse en un pase anónimo: antes, la cabecera saltaba dev-auth y la petición
            // llegaba SIN identidad al endpoint (probado: X-Disable-Dev-Auth:true → 200). Ahora,
            // si no viaja una credencial real (cabecera Authorization o cookie ums.session), se
            // rechaza fail-closed (401) en lugar de continuar como anónimo. Los flujos legítimos
            // (login previo + cookie, o bearer) conservan el bypass; el resto queda vedado.
            var hasRealCredential =
                context.Request.Headers.ContainsKey(AuthorizationHeader)
                || context.Request.Cookies.ContainsKey(SessionCookieName);

            if (!hasRealCredential)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.Headers.WWWAuthenticate = "Bearer";
                return;
            }

            await _next(context);
            return;
        }

        // G-191: si la petición trae un portador, decide el manejador REAL de portador, no las
        // claims de conveniencia. Este middleware corre ANTES de UseAuthentication, así que
        // inyectar identidad aquí enmascaraba el rechazo de un token inválido o caducado: la
        // autenticación fallaba, el principal de desarrollo sobrevivía y el endpoint respondía
        // 200. Con la exención, un portador inválido es 401 también en desarrollo.
        if (context.Request.Headers.ContainsKey(AuthorizationHeader))
        {
            await _next(context);
            return;
        }

        if (context.User?.Identity?.IsAuthenticated != true)
        {
            var userId = context.Request.Headers[UserIdHeader].FirstOrDefault();
            var userName = context.Request.Headers[UserNameHeader].FirstOrDefault();
            var tenantId = context.Request.Headers[TenantIdHeader].FirstOrDefault();
            var isInternalAdminHeader = context.Request.Headers[IsInternalAdminHeader].FirstOrDefault();

            userId ??= DefaultUserId;
            userName ??= DefaultUserName;
            tenantId ??= DefaultTenantId;

            var isInternalAdmin = isInternalAdminHeader?.ToLower() == "true"
                || tenantId == ManagementOwnerTenantId;

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, userId),
                new(ClaimTypes.Name, userName),
                new("tenant_id", tenantId),
                new("is_internal_admin", isInternalAdmin.ToString().ToLower()),
            };

            // G-280: la identidad de desarrollo llega ahora con los MISMOS scopes que tendria en
            // produccion, construyendo el grafo real del usuario suplantado.
            //
            // Antes solo se inyectaban las cuatro claims de arriba —ni una de `scope`—, asi que
            // toda peticion por cabeceras dependia del atajo de Admin Root para pasar los 182
            // manejadores guardados. Eso convertia el arnes en un juez que no podia ver lo que
            // juzga: con el atajo encendido todo pasaba, y con el apagado nada, en ninguno de los
            // dos casos por el motivo correcto.
            //
            // Se atajan la AUTENTICACION (quien eres, por cabecera) y no la AUTORIZACION (que
            // puedes, por catalogo), que es justo la distincion que este middleware borraba.
            // El contexto de inquilino se inicializa ANTES de construir el grafo, y el orden no es
            // cosmetico: los repositorios filtran por inquilino con ese contexto, asi que sin el la
            // consulta del usuario vuelve vacia y los scopes nunca se anaden. Medido: 35
            // evaluaciones, 35 denegadas y ni una concedida.
            if (Guid.TryParse(tenantId, out var parsedTenantId))
            {
                tenantContext.Initialize(parsedTenantId, isInternalAdmin);
            }

            await ScopesDelGrafo.AnadirAsync(claims, userId, tenantId, isInternalAdmin, tenantContext, userAccounts, graphBuilder, context.RequestAborted);

            var identity = new ClaimsIdentity(claims, "Dev");
            context.User = new ClaimsPrincipal(identity);
        }

        await _next(context);
    }

}

public static class DevAuthMiddlewareExtensions
{
    public static IApplicationBuilder UseDevAuth(this IApplicationBuilder app)
        => app.UseMiddleware<DevAuthMiddleware>();
}
