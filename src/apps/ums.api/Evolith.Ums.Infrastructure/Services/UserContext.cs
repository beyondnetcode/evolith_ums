using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Evolith.Ums.Infrastructure.Services;

/// <summary>
/// Infrastructure implementation of IUserContext using ASP.NET Core HttpContext.
/// </summary>
public class UserContext : IUserContext
{
    /// <summary>
    /// Gobierna el atajo del Admin Root. `false` lo APAGA y obliga a que el privilegio salga de
    /// permisos concedidos, no del inquilino. Por omisión sigue ENCENDIDO: apagarlo sin haberle
    /// dado perfil real a las cuentas operativas deja la instancia sin nadie capaz de administrarla.
    /// </summary>
    public const string ClaveAtajo = "Authorization:InternalAdminBypass";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IConfiguration? _configuration;
    private readonly ILogger<UserContext>? _logger;

    public UserContext(
        IHttpContextAccessor httpContextAccessor,
        IConfiguration? configuration = null,
        ILogger<UserContext>? logger = null)
    {
        _httpContextAccessor = httpContextAccessor;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Se lee en CADA evaluación y no se cachea, por la misma razón que el modo de auditoría del
    /// aspecto: pasar de atajo a permisos es la maniobra delicada de esta migración, y poder
    /// revertirla cambiando una clave —sin recompilar ni redesplegar— es lo que la hace reversible.
    /// </summary>
    private bool AtajoActivo =>
        !string.Equals(_configuration?[ClaveAtajo], "false", StringComparison.OrdinalIgnoreCase);

    public string? UserId => _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);

    public string? UserName => _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.Name)
                               ?? _httpContextAccessor.HttpContext?.User?.FindFirstValue("name")
                               ?? "Anonymous";

    public string? TenantId => _httpContextAccessor.HttpContext?.User?.FindFirstValue("tenant_id");

    public bool IsAuthenticated => _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false;

    public bool HasPermission(string permission)
    {
        var user = _httpContextAccessor.HttpContext?.User;

        // ADR-0071 / FS-26: el Admin Root —propietario de gestión, `is_internal_admin=true`—
        // concede cualquier permiso sin mirar un solo scope.
        //
        // EL ATAJO SIGUE AHÍ, PERO YA NO ES INVISIBLE NI PERMANENTE. Dos cambios:
        //
        // 1. SE REGISTRA cada vez que concede, con el permiso y la cuenta. Sin eso no hay forma de
        //    saber quién depende de él, y sin esa lista apagarlo es a ciegas. Es la medida que
        //    falta para poder dar perfil real a las cuentas operativas: hoy `arranque.ums` NO TIENE
        //    PERFIL y crea suites, roles y cuentas solo por esta rama.
        //
        // 2. SE PUEDE APAGAR con `Authorization:InternalAdminBypass=false`, sin recompilar. Ese es
        //    el interruptor final de la migración, y va detrás del registro a propósito: primero se
        //    mide de qué se depende, después se corta.
        //
        // El claim NO viene del rol sino del INQUILINO (`isInternalAdmin = ctx.Tenant.IsManagementOwner`),
        // así que hoy alcanza a TODA cuenta de EVOLITH, no solo a un administrador.
        var esAdminInterno = string.Equals(
            user?.FindFirstValue("is_internal_admin"), "true", StringComparison.OrdinalIgnoreCase);

        if (esAdminInterno && AtajoActivo)
        {
            _logger?.LogInformation(
                "Autorización concedida por el atajo de Admin Root, sin evaluar permisos. "
                + "Permiso {Permiso} · cuenta {Cuenta} · inquilino {Inquilino}. "
                + "Apagar con {Clave}=false cuando esa cuenta tenga los scopes concedidos.",
                permission, UserName, TenantId, ClaveAtajo);
            return true;
        }

        // JWT uses 'scope' claim and format 'resource.action' (lowercase)
        var normalizedPerm = permission.Replace(":", ".").ToLowerInvariant();
        var scopeClaims = user?.FindAll("scope") ?? Enumerable.Empty<Claim>();

        // Split space-separated scopes if they are returned as a single string
        var scopes = scopeClaims.SelectMany(c => c.Value.Split(' '));

        return scopes.Any(s => s.Equals(normalizedPerm, StringComparison.OrdinalIgnoreCase));
    }
}
