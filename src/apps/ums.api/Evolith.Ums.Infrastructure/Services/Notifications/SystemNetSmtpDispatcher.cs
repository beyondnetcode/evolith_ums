using System.Net;
using System.Net.Mail;

namespace Evolith.Ums.Infrastructure.Services.Notifications;

/// <summary>
/// Entrega con el cliente SMTP del BCL.
///
/// <para>POR QUÉ ESTE CLIENTE Y NO OTRO. No añade dependencia al paquete —MailKit sería una
/// entrada nueva al stack, y el stack autorizado se decide en <c>evolith_arch</c>, no aquí— y
/// habla lo que el relay necesita: STARTTLS sobre 25/587/2525 con AUTH LOGIN. Su límite conocido
/// es SMTPS implícito sobre 465, que no soporta; si algún día hiciera falta, se sustituye esta
/// clase y nada más, que es justo para lo que existe <see cref="ISmtpDispatcher"/>.</para>
///
/// <para>El cliente se construye por envío. Es lo correcto aquí: <c>SmtpClient</c> no es seguro
/// entre hilos y no admite envíos concurrentes sobre la misma instancia, y el coste de abrir la
/// conexión es despreciable frente a la frecuencia con la que UMS notifica.</para>
/// </summary>
public sealed class SystemNetSmtpDispatcher : ISmtpDispatcher
{
    public async Task SendAsync(MailMessage message, SmtpOptions options, CancellationToken cancellationToken = default)
    {
        // S5332 exige EnableSsl literal en true. Aquí sale de la configuración, y con razón: hay
        // relays internos sin TLS y esa es una decisión de despliegue. El valor por omisión de
        // `UseStartTls` es true, de modo que apagarlo exige escribirlo — que es lo que se quiere
        // que cueste. Los appsettings de UAT y Production lo declaran encendido.
#pragma warning disable S5332
        using var client = new SmtpClient(options.Host, options.Port)
        {
            DeliveryMethod = SmtpDeliveryMethod.Network,
            EnableSsl = options.UseStartTls,
            Timeout = options.TimeoutSeconds * 1000,
        };
#pragma warning restore S5332

        // El orden importa: asignar Credentials con UseDefaultCredentials en true las descarta,
        // y el relay contestaría 535 con la credencial correcta en la mano.
        client.UseDefaultCredentials = false;

        if (!string.IsNullOrWhiteSpace(options.User))
            client.Credentials = new NetworkCredential(options.User, options.Password);

        await client.SendMailAsync(message, cancellationToken).ConfigureAwait(false);
    }
}
