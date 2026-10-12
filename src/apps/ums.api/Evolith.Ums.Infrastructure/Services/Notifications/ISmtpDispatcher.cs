using System.Net.Mail;

namespace Evolith.Ums.Infrastructure.Services.Notifications;

/// <summary>
/// La salida a la red, aislada en su propio puerto.
///
/// <para>POR QUÉ EXISTE. Sin esta costura, comprobar que el mensaje se compone bien —remitente,
/// destinatario con nombre, asunto y cuerpo en UTF-8— exigiría un relay vivo, y una prueba que
/// necesita red no se ejecuta en la suite. El adaptador compone; esto entrega.</para>
/// </summary>
public interface ISmtpDispatcher
{
    Task SendAsync(MailMessage message, SmtpOptions options, CancellationToken cancellationToken = default);
}
