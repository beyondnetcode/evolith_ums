using System.Net.Mail;
using System.Net.Mime;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Evolith.Ums.Application.Common.Interfaces;
using Evolith.Ums.Application.Common.Notifications;

// `Evolith.Ums.Domain.Enums` entra por GlobalUsings y trae su propio NotificationChannel —el del
// dominio de reglas de notificación, que es otra cosa—. El alias fija cuál se lee aquí: el canal
// del mensaje que se está entregando.
using NotificationChannel = Evolith.Ums.Application.Common.Notifications.NotificationChannel;

namespace Evolith.Ums.Infrastructure.Services.Notifications;

/// <summary>
/// Entrega por correo real (G-270).
///
/// <para><b>Falla en voz alta.</b> Lo que este adaptador sustituye —<see cref="SimulatedNotificationAdapter"/>—
/// daba por bueno cualquier envío: escribía en una consola que nadie capturaba y devolvía éxito, de modo
/// que un alta de usuario parecía entregar su credencial sin haber entregado nada. Aquí un fallo de
/// entrega se registra y se PROPAGA. Quien llama decide qué hacer con él; lo que ya no puede es
/// creerse notificado sin estarlo.</para>
///
/// <para><b>El cuerpo nunca se registra.</b> Lleva tokens de un solo uso y contraseñas temporales
/// (G-197). Del mensaje entregado solo se deja rastro del destinatario y el asunto — y el
/// destinatario lo enmascara <c>PiiSanitizerEnricher</c> antes de llegar al sink.</para>
/// </summary>
public sealed class SmtpNotificationAdapter : INotificationService
{
    private readonly ISmtpDispatcher _dispatcher;
    private readonly NotificationOptions _options;
    private readonly ILogger<SmtpNotificationAdapter> _logger;

    public SmtpNotificationAdapter(
        ISmtpDispatcher dispatcher,
        IOptions<NotificationOptions> options,
        ILogger<SmtpNotificationAdapter> logger)
    {
        _dispatcher = dispatcher;
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendAsync(UmsNotification notification, CancellationToken cancellationToken = default)
    {
        // SMTP entrega correo y nada más. Un canal que este transporte no habla no puede
        // «entregarse» en silencio: sería exactamente el defecto que este adaptador cierra.
        if (notification.Channel != NotificationChannel.Email)
        {
            throw new NotSupportedException(
                $"El transporte SMTP no entrega el canal {notification.Channel}. Registra un adaptador "
                + "para ese canal o no lo emitas: darlo por enviado es lo que G-270 prohíbe.");
        }

        var smtp = _options.Smtp;
        using var mensaje = Componer(notification, smtp);

        try
        {
            await _dispatcher.SendAsync(mensaje, smtp, cancellationToken).ConfigureAwait(false);
        }
        // S2139 pide registrar O relanzar, no ambas. Aquí las dos cosas son deliberadas y no
        // redundantes: quien llama decide el desenlace del flujo —el restablecimiento anónimo,
        // por ejemplo, se lo traga para no delatar si la cuenta existe—, pero el fallo del relay
        // tiene que quedar registrado con su host y su destinatario ANTES de que eso ocurra, o
        // nadie sabría nunca que el correo no salió. Ese silencio es justo el defecto que este
        // adaptador cierra (G-270).
#pragma warning disable S2139
        catch (Exception ex) when (ex is SmtpException or InvalidOperationException or IOException)
        {
            // El código de estado va aparte del texto de la excepción a propósito: `SmtpException`
            // envuelve el fallo en un «Failure sending mail» que no distingue una credencial
            // rechazada de un relay caído, y quien lea esto de madrugada necesita esa diferencia.
            var codigo = (ex as SmtpException)?.StatusCode;

            _logger.LogError(
                ex,
                "Entrega SMTP fallida. Host={Host} Puerto={Puerto} Codigo={CodigoSmtp} Destinatario={Recipient} Asunto={Subject}",
                smtp.Host,
                smtp.Port,
                codigo,
                notification.Recipient,
                notification.Subject);

            throw;
        }
#pragma warning restore S2139

        _logger.LogInformation(
            "Notificacion entregada al relay. Destinatario={Recipient} Asunto={Subject}",
            notification.Recipient,
            notification.Subject);
    }

    /// <summary>
    /// Intenta TODOS los mensajes y agrega los fallos al final.
    ///
    /// <para>Abortar en el primero dejaría a la mitad de los destinatarios sin aviso por culpa de
    /// una dirección mal escrita en la otra mitad, y sin manera de saber a quién le llegó.</para>
    /// </summary>
    public async Task SendBulkAsync(IEnumerable<UmsNotification> notifications, CancellationToken cancellationToken = default)
    {
        List<Exception>? fallos = null;

        foreach (var notification in notifications)
        {
            try
            {
                await SendAsync(notification, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                (fallos ??= []).Add(ex);
            }
        }

        if (fallos is not null)
            throw new AggregateException("Al menos una notificacion no pudo entregarse.", fallos);
    }

    private static MailMessage Componer(UmsNotification notification, SmtpOptions smtp)
    {
        var mensaje = new MailMessage
        {
            From = new MailAddress(smtp.FromAddress, smtp.FromName, Encoding.UTF8),
            Subject = notification.Subject,
            SubjectEncoding = Encoding.UTF8,
            // El cuerpo del mensaje es el TEXTO, siempre. Si además hay HTML, se añade como vista
            // alternativa y el cliente elige: los que pintan HTML muestran la versión con marca y
            // los que no —o quien tiene las imágenes bloqueadas, o usa lector de pantalla— siguen
            // leyendo esto. Un correo que solo existe en HTML también puntúa peor en los filtros
            // de correo no deseado.
            Body = notification.Body,
            BodyEncoding = Encoding.UTF8,
            IsBodyHtml = false,
        };

        mensaje.To.Add(string.IsNullOrWhiteSpace(notification.RecipientName)
            ? new MailAddress(notification.Recipient)
            : new MailAddress(notification.Recipient, notification.RecipientName, Encoding.UTF8));

        if (!string.IsNullOrWhiteSpace(notification.HtmlBody))
            mensaje.AlternateViews.Add(VistaHtml(notification.HtmlBody));

        return mensaje;
    }

    private static AlternateView VistaHtml(string html)
    {
        var vista = AlternateView.CreateAlternateViewFromString(html, Encoding.UTF8, MediaTypeNames.Text.Html);

        var logo = LogoIncrustado();
        if (logo is not null)
            vista.LinkedResources.Add(logo);

        return vista;
    }

    /// <summary>
    /// El logo, adjunto al mensaje y referenciado por <c>cid:</c>.
    ///
    /// <para>Devuelve <c>null</c> si el recurso no está en el ensamblado, y el correo sale
    /// igual —con el texto alternativo donde iría la imagen—. Quedarse sin notificar porque
    /// falta un logotipo seria cambiar un defecto cosmetico por uno grave.</para>
    /// </summary>
    private static LinkedResource? LogoIncrustado()
    {
        var ensamblado = typeof(SmtpNotificationAdapter).Assembly;
        var nombre = $"{typeof(SmtpNotificationAdapter).Namespace}.Assets.logo-evolith-white.png";

        var flujo = ensamblado.GetManifestResourceStream(nombre);
        if (flujo is null)
            return null;

        return new LinkedResource(flujo, MediaTypeNames.Image.Png)
        {
            ContentId = NotificationEmailLayout.LogoContentId,
            TransferEncoding = System.Net.Mime.TransferEncoding.Base64,
        };
    }
}
