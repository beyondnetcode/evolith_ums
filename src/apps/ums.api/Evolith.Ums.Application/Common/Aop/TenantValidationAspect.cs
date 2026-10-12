using System;
using System.Linq;
using System.Reflection;
using Evolith.Ums.Application.Common.Interfaces;
using BeyondNetCode.Shell.Aop;

namespace Evolith.Ums.Application.Common.Aop;

public sealed class TenantValidationAspect : AbstractAspect<TenantValidationAspectAttribute>
{
    private readonly IUserContext _userContext;

    public TenantValidationAspect(IUserContext userContext)
    {
        _userContext = userContext;
    }

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, PropertyInfo?> _tenantIdPropertyCache = new();

    public override void Apply(IJoinPoint joinPoint)
    {
        var attribute = GetAttribute(joinPoint);
        if (attribute is null)
        {
            // El handler no está marcado con [TenantValidationAspect]: no hay
            // intención de validar inquilino → nada que hacer.
            Proceed(joinPoint);
            return;
        }

        var userTenantId = _userContext.TenantId;

        // Actor global / pre-inquilino (sin TenantId propio): excepción legítima y
        // DOCUMENTADA (G-102). El signup/auto-registro previo al inquilino, la
        // creación de inquilino (el operador global carece de TenantId) y la
        // administración de plataforma dirigida a un inquilino operan legítimamente
        // sin un inquilino propio: no existe frontera de inquilino que cruzar, así
        // que se procede a propósito. NO es un fail-open: es la regla de dominio
        // para el actor sin inquilino (distinguir admin-interno/pre-tenant del
        // cruce indebido, tal como prescribe G-102).
        if (string.IsNullOrWhiteSpace(userTenantId))
        {
            Proceed(joinPoint);
            return;
        }

        // Llamante VINCULADO a un inquilino sobre un handler marcado explícitamente
        // como tenant-scoped: la petición DEBE dirigirse a su propio inquilino.
        // Si el inquilino objetivo no puede confirmarse igual al del usuario,
        // DENEGAR (fail-closed, espejo de G-098): un handler marcado para validación
        // de inquilino nunca debe ejecutarse sin que el inquilino quede confirmado.
        // Esto cierra el soft fail-open previo (si no se podía determinar el
        // inquilino de la petición, se dejaba pasar), vía real de cruce indebido.
        var request = joinPoint.Arguments?.FirstOrDefault(a => a is not System.Threading.CancellationToken);

        string? requestTenantId = null;
        if (request is not null)
        {
            var requestType = request.GetType();
            var tenantIdProperty = _tenantIdPropertyCache.GetOrAdd(
                requestType,
                type => type.GetProperty("TenantId", BindingFlags.Instance | BindingFlags.Public));
            requestTenantId = tenantIdProperty?.GetValue(request)?.ToString();
        }

        if (string.IsNullOrWhiteSpace(requestTenantId) ||
            !string.Equals(requestTenantId, userTenantId, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException(
                $"Tenant validation failed. User belongs to tenant '{userTenantId}', " +
                $"but the request tenant could not be confirmed to match (target: '{requestTenantId ?? "<none>"}').");
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
