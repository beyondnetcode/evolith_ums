using System;
using System.Linq;
using Evolith.Ums.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using BeyondNetCode.Shell.Aop;

namespace Evolith.Ums.Application.Common.Aop;

public sealed class AuthorizationAspect : AbstractAspect<AuthorizationAspectAttribute>
{
    /// <summary>
    /// Nombre de la clave de configuración que pone TODA la autorización en modo auditoría.
    /// `Authorization:Mode=AuditOnly` evalúa y registra, pero no deniega.
    /// </summary>
    public const string ClaveModo = "Authorization:Mode";

    /// <summary>Valor de <see cref="ClaveModo"/> que activa la auditoría sin denegación.</summary>
    public const string ModoAuditoria = "AuditOnly";

    private readonly IUserContext _userContext;
    private readonly IConfiguration? _configuration;

    public AuthorizationAspect(IUserContext userContext, IConfiguration? configuration = null)
    {
        _userContext = userContext;
        _configuration = configuration;
    }

    /// <summary>
    /// El modo global. Se lee en cada evaluación y no se cachea a propósito: pasar de auditoría a
    /// aplicación es la maniobra más delicada de esta migración, y poder revertirla cambiando una
    /// clave de configuración —sin recompilar y sin redesplegar— es lo que la hace reversible.
    /// </summary>
    private bool EsAuditoriaGlobal =>
        string.Equals(_configuration?[ClaveModo], ModoAuditoria, StringComparison.OrdinalIgnoreCase);

    public override void Apply(IJoinPoint joinPoint)
    {
        var attribute = GetAttribute(joinPoint);

        // If not on the interface method, check the MediatR Request (args[0])
        if (attribute is null && joinPoint.Arguments != null && joinPoint.Arguments.Length > 0 && joinPoint.Arguments[0] != null)
        {
            attribute = (AuthorizationAspectAttribute?)Attribute.GetCustomAttribute(joinPoint.Arguments[0]!.GetType(), typeof(AuthorizationAspectAttribute));
        }

        // Check the target class as fallback
        if (attribute is null && joinPoint.TargetType != null)
        {
            attribute = (AuthorizationAspectAttribute?)Attribute.GetCustomAttribute(joinPoint.TargetType, typeof(AuthorizationAspectAttribute));
        }

        if (attribute is null)
        {
            // FAIL-CLOSED, igual que la rama de abajo (G-098). Antes esta rama DEJABA PASAR, y la
            // asimetría era el agujero: si el aspecto llega a ejecutarse es porque el pointcut
            // encontró `[AuthorizationAspect]` en el método —intención explícita de proteger— y no
            // poder resolver el atributo después es un fallo del andamiaje, no un permiso
            // concedido. Un método marcado como protegido no debe ejecutarse sin control.
            //
            // Hoy es inalcanzable —si el pointcut disparó, `GetAttribute` lo encuentra— y por eso
            // el cambio es seguro. Se corrige justamente porque nadie lo notaría el día que dejara
            // de serlo.
            Console.WriteLine($"[AOP] Access DENIED for {joinPoint.TargetType?.Name}: aspect fired but no AuthorizationAspectAttribute could be resolved (fail-closed)");
            // Tambien 403: el sujeto esta identificado y la operacion no se le concede. Que la causa
            // sea una metadata rota y no un permiso ausente no cambia lo que el cliente debe hacer,
            // y decirle «inicie sesion de nuevo» lo mandaria a reintentar algo que no arregla nada.
            throw new AccesoDenegadoException(
                $"<sin atributo: {joinPoint.TargetType?.Name}>");
        }

        // Convención sobre Configuración: Inferir permisos basados en el nombre de la clase o método
        var resourceCode = attribute.ResourceCode;
        var actionCode = attribute.ActionCode;

        if (string.IsNullOrEmpty(resourceCode) || string.IsNullOrEmpty(actionCode))
        {
            var targetName = joinPoint.TargetType.Name;
            // Ej: CreateUserCommandHandler -> "user", "create"
            if (targetName.EndsWith("CommandHandler"))
            {
                var cleanName = targetName.Replace("CommandHandler", "");
                if (cleanName.StartsWith("Create")) { actionCode ??= "create"; resourceCode ??= cleanName.Substring(6).ToLowerInvariant(); }
                else if (cleanName.StartsWith("Update")) { actionCode ??= "update"; resourceCode ??= cleanName.Substring(6).ToLowerInvariant(); }
                else if (cleanName.StartsWith("Delete")) { actionCode ??= "delete"; resourceCode ??= cleanName.Substring(6).ToLowerInvariant(); }
            }
            else if (targetName.EndsWith("QueryHandler"))
            {
                var cleanName = targetName.Replace("QueryHandler", "");
                if (cleanName.StartsWith("Get")) { actionCode ??= "read"; resourceCode ??= cleanName.Substring(3).ToLowerInvariant(); }
                else if (cleanName.StartsWith("List")) { actionCode ??= "read"; resourceCode ??= cleanName.Substring(4).ToLowerInvariant(); }
            }
        }

        if (!string.IsNullOrEmpty(resourceCode) && !string.IsNullOrEmpty(actionCode))
        {
            var permission = $"{resourceCode}:{actionCode}";
            Console.WriteLine($"[AOP] Evaluating permission '{permission}' for {joinPoint.TargetType?.Name}");

            if (!_userContext.HasPermission(permission))
            {
                // AUDITORÍA: se registra y se deja pasar. El registro nombra el permiso exacto que
                // faltó, que es lo que convierte «algo se va a romper» en una lista de scopes que
                // conceder antes de aplicar de verdad.
                if (attribute.AuditOnly || EsAuditoriaGlobal)
                {
                    Console.WriteLine($"[AOP] AUDIT — habría DENEGADO '{permission}' en {joinPoint.TargetType?.Name}; se deja pasar por modo auditoría");
                    Proceed(joinPoint);
                    return;
                }

                Console.WriteLine($"[AOP] Access DENIED for '{permission}'");
                // 403, no 401: el sujeto esta identificado y le falta el permiso. Decir «debe
                // iniciar sesion de nuevo» manda al cliente a reintentar algo que no arregla nada.
                throw new AccesoDenegadoException(permission);
            }
            Console.WriteLine($"[AOP] Access GRANTED for '{permission}'");
        }
        else
        {
            // Fail-closed (G-098): el atributo [AuthorizationAspect] está presente
            // —intención explícita de proteger el handler— pero el permiso no pudo
            // determinarse (el nombre no casa la convención Create/Update/Delete/
            // Get/List y no se declararon ResourceCode/ActionCode). Denegar por
            // defecto en lugar de dejar pasar: un método marcado como protegido
            // nunca debe ejecutarse sin control de acceso (evita escalada de
            // privilegios). Espejo de la rama de denegación anterior.
            Console.WriteLine($"[AOP] Access DENIED for {joinPoint.TargetType?.Name}: authorization required but permission could not be determined (fail-closed)");
            throw new UnauthorizedAccessException(
                $"Access denied. Authorization is required for '{joinPoint.TargetType?.Name}' but the permission could not be determined.");
        }

        Proceed(joinPoint);
    }

    private void Proceed(IJoinPoint joinPoint)
    {
        if (GetNext() is null)
            joinPoint.Proceed();
        else
            GetNext()!.Apply(joinPoint);
    }
}
