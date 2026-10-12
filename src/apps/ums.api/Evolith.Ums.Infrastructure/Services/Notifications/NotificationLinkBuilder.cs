using Microsoft.Extensions.Options;
using Evolith.Ums.Application.Common.Interfaces;

namespace Evolith.Ums.Infrastructure.Services.Notifications;

/// <summary>
/// Resuelve los enlaces del portal desde <c>Notifications:PortalBaseUrl</c>.
///
/// <para>El código viaja en la consulta del enlace, y eso es una decisión con su contrapartida:
/// queda en el historial del navegador de quien lo abre. Se acepta porque el token vive 15
/// minutos, sirve una sola vez y no cambia nada por sí mismo —el cambio ocurre al canjearlo, con
/// una contraseña nueva—; y porque la pantalla lo borra de la barra de direcciones en cuanto lo
/// lee. La alternativa, que el titular copie 43 caracteres a mano desde el correo, es la que
/// hasta hoy hacía que este flujo se sintiera roto.</para>
/// </summary>
public sealed class NotificationLinkBuilder : INotificationLinkBuilder
{
    private readonly NotificationOptions _options;

    public NotificationLinkBuilder(IOptions<NotificationOptions> options)
    {
        _options = options.Value;
    }

    public string? EnlaceDeCanjeDeContrasena(string tenantCode, string email, string token)
    {
        var baseUrl = _options.PortalBaseUrl?.TrimEnd('/');
        if (string.IsNullOrWhiteSpace(baseUrl))
            return null;

        return $"{baseUrl}/login"
            + "?recuperar=1"
            + $"&inquilino={Uri.EscapeDataString(tenantCode)}"
            + $"&correo={Uri.EscapeDataString(email)}"
            + $"&codigo={Uri.EscapeDataString(token)}";
    }
}
