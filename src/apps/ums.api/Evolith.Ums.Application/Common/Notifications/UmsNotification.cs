namespace Evolith.Ums.Application.Common.Notifications;

public sealed record UmsNotification(
    string Recipient,
    string Subject,
    string Body,
    NotificationChannel Channel = NotificationChannel.Email,
    string? RecipientName = null,
    Dictionary<string, string>? Metadata = null,

    /// <summary>
    /// Versión HTML del mismo mensaje, con la marca de Evolith. Opcional a propósito.
    ///
    /// <para><see cref="Body"/> NO es un resto que se pueda dejar morir: viaja siempre, y no
    /// como cortesía. Es lo que leen el cliente que no pinta HTML, el lector de pantalla que
    /// prefiere texto y quien tiene las imágenes bloqueadas; y un mensaje que solo existe en
    /// HTML puntúa peor en los filtros de correo no deseado. Los dos cuerpos dicen lo mismo:
    /// si uno cambia, el otro también.</para>
    /// </summary>
    string? HtmlBody = null
);
