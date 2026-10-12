using Microsoft.Extensions.Options;

namespace Evolith.Ums.Infrastructure.Services.Notifications;

/// <summary>
/// Transporte con el que sale una <see cref="Application.Common.Notifications.UmsNotification"/>.
/// </summary>
public enum NotificationProvider
{
    /// <summary>
    /// Vuelca el mensaje al log y no entrega nada. Solo desarrollo: en un entorno donde nadie lee
    /// la consola es indistinguible de no tener notificaciones (G-270), y además escribe el cuerpo
    /// —que lleva tokens y contraseñas temporales— al log (G-197).
    /// </summary>
    Simulated,

    /// <summary>Entrega por SMTP contra un relay real.</summary>
    Smtp,
}

/// <summary>
/// Sección <c>Notifications</c>. Elige el transporte y lo parametriza.
///
/// <para>El defecto es <see cref="NotificationProvider.Simulated"/> a propósito: es lo que corre
/// en desarrollo y en la suite de pruebas, donde no hay relay al que hablar. Cualquier entorno
/// desplegado debe declarar <see cref="NotificationProvider.Smtp"/> — y el arranque lo exige en
/// Production (ver <c>UseUmsApiPipeline</c> / los bootstrappers).</para>
/// </summary>
public sealed class NotificationOptions
{
    public const string SectionName = "Notifications";

    public NotificationProvider Provider { get; init; } = NotificationProvider.Simulated;

    public SmtpOptions Smtp { get; init; } = new();

    /// <summary>
    /// Raíz pública del portal —por ejemplo <c>http://srvtms:8080/ums_web</c>—, con la que se
    /// componen los enlaces que viajan en las notificaciones.
    ///
    /// <para>Vacía por omisión, y eso NO es un fallo de configuración: los correos siguen saliendo
    /// con su código a la vista. Un despliegue sin portal alcanzable por nombre estable prefiere
    /// no prometer un enlace que llevaría a ninguna parte.</para>
    /// </summary>
    public string PortalBaseUrl { get; init; } = string.Empty;
}

/// <summary>
/// Parámetros del relay SMTP.
///
/// <para><b>La contraseña NO viaja en el repositorio.</b> Los <c>appsettings</c> versionados la
/// traen vacía; el valor vivo se pone en el <c>appsettings.&lt;Entorno&gt;.json</c> del servidor
/// —que el guion de despliegue preserva entre despliegues— o en la variable de entorno
/// <c>Notifications__Smtp__Password</c>. Con SendGrid el usuario es literalmente <c>apikey</c> y
/// la contraseña es la clave de API, así que la clave es la credencial completa.</para>
/// </summary>
public sealed class SmtpOptions
{
    public string Host { get; init; } = string.Empty;

    public int Port { get; init; } = 587;

    /// <summary>
    /// STARTTLS. En <see cref="System.Net.Mail.SmtpClient"/> esto es <c>EnableSsl</c>, que negocia
    /// STARTTLS sobre el puerto de texto claro (25, 587 o 2525) — no es SMTPS implícito sobre 465,
    /// que esa clase no sabe hablar. Apagarlo manda la credencial en claro por la red.
    /// </summary>
    public bool UseStartTls { get; init; } = true;

    public string User { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;

    /// <summary>Remitente. Debe ser un buzón que el relay tenga autorizado a enviar.</summary>
    public string FromAddress { get; init; } = string.Empty;

    public string FromName { get; init; } = "UMS";

    public int TimeoutSeconds { get; init; } = 30;
}

/// <summary>
/// Valida la sección al ARRANCAR, no en el primer envío.
///
/// <para>POR QUÉ AL ARRANCAR. Una notificación se emite en el peor momento para descubrir que la
/// configuración está incompleta: dentro de un alta de usuario, de un restablecimiento o de una
/// aprobación. Si falta el host o la credencial, el proceso debe negarse a levantar —bajo IIS eso
/// es un 500.30 en el despliegue, delante de quien despliega— en vez de fallar meses después
/// delante de un usuario.</para>
///
/// <para>Solo juzga la rama SMTP: con <see cref="NotificationProvider.Simulated"/> no hay nada que
/// exigir, y así la suite de pruebas y el desarrollo local no necesitan configuración alguna.</para>
/// </summary>
public sealed class NotificationOptionsValidator : IValidateOptions<NotificationOptions>
{
    public ValidateOptionsResult Validate(string? name, NotificationOptions options)
    {
        var fallos = new List<string>();
        var smtp = options.Smtp;

        // El portal se juzga siempre: el enlace lo compone quien arma la notificación, no el
        // transporte, así que una URL mal escrita rompe igual con el adaptador simulado.
        ValidarPortal(options, fallos);

        if (options.Provider != NotificationProvider.Smtp)
            return Resultado(fallos);

        if (string.IsNullOrWhiteSpace(smtp.Host))
            fallos.Add("Notifications:Smtp:Host es obligatorio con Provider=Smtp.");

        if (smtp.Port is < 1 or > 65535)
            fallos.Add($"Notifications:Smtp:Port fuera de rango: {smtp.Port}.");

        if (smtp.TimeoutSeconds < 1)
            fallos.Add($"Notifications:Smtp:TimeoutSeconds debe ser positivo: {smtp.TimeoutSeconds}.");

        if (string.IsNullOrWhiteSpace(smtp.FromAddress))
        {
            fallos.Add("Notifications:Smtp:FromAddress es obligatorio con Provider=Smtp.");
        }
        else if (!EsCorreoPlausible(smtp.FromAddress))
        {
            fallos.Add($"Notifications:Smtp:FromAddress no es una dirección válida: '{smtp.FromAddress}'.");
        }

        // Un usuario sin contraseña es el modo en que esto falla de verdad: el despliegue trae el
        // appsettings versionado —que la deja vacía— y nadie puso el valor vivo. El relay contesta
        // 535 en el primer envío, dentro de un alta de usuario. Se atrapa aquí.
        if (!string.IsNullOrWhiteSpace(smtp.User) && string.IsNullOrWhiteSpace(smtp.Password))
        {
            fallos.Add(
                "Notifications:Smtp:User está declarado sin Notifications:Smtp:Password. La contraseña "
                + "no viaja en el repositorio: ponla en el appsettings del servidor o en la variable de "
                + "entorno Notifications__Smtp__Password.");
        }

        if (!string.IsNullOrWhiteSpace(smtp.Password) && string.IsNullOrWhiteSpace(smtp.User))
            fallos.Add("Notifications:Smtp:Password está declarada sin Notifications:Smtp:User.");

        return Resultado(fallos);
    }

    /// <summary>
    /// Si se declara, tiene que ser una URL absoluta y navegable. Una relativa o un <c>mailto:</c>
    /// producirían un botón que no lleva a ninguna parte —o que el maquetador descarta en
    /// silencio— y el titular se quedaría mirando un correo sin salida.
    /// </summary>
    private static void ValidarPortal(NotificationOptions options, List<string> fallos)
    {
        if (string.IsNullOrWhiteSpace(options.PortalBaseUrl))
            return;

        var esNavegable = Uri.TryCreate(options.PortalBaseUrl, UriKind.Absolute, out var portal)
            && (portal.Scheme == Uri.UriSchemeHttp || portal.Scheme == Uri.UriSchemeHttps);

        if (!esNavegable)
        {
            fallos.Add(
                $"Notifications:PortalBaseUrl debe ser una URL http(s) absoluta: '{options.PortalBaseUrl}'.");
        }
    }

    private static ValidateOptionsResult Resultado(List<string> fallos) =>
        fallos.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(fallos);

    /// <summary>
    /// Comprobación deliberadamente pobre: solo descarta lo que <see cref="System.Net.Mail.MailAddress"/>
    /// rechazaría al construirse. No valida el dominio ni la autorización del remitente —eso lo
    /// decide el relay, y fingir aquí un juicio que no nos toca daría una falsa garantía.
    /// </summary>
    private static bool EsCorreoPlausible(string direccion)
    {
        try
        {
            _ = new System.Net.Mail.MailAddress(direccion);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
