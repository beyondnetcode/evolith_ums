namespace Evolith.Ums.Application.Common.Interfaces;

/// <summary>
/// Construye los enlaces del portal que viajan dentro de una notificación.
///
/// <para>POR QUÉ ES UN PUERTO Y NO UNA CONSTANTE. La dirección del portal es un hecho del
/// DESPLIEGUE —en UAT es `http://srvtms:8080/ums_web`, en otro sitio será otra cosa— y la capa
/// de aplicación no puede conocerla sin atarse a un entorno. La resuelve infraestructura desde
/// `Notifications:PortalBaseUrl`.</para>
/// </summary>
public interface INotificationLinkBuilder
{
    /// <summary>
    /// Enlace directo a la pantalla de canje, con el código ya puesto.
    ///
    /// <para>Devuelve <c>null</c> cuando el despliegue no declara la dirección del portal. Es
    /// deliberado y el llamante debe tratarlo así: el correo se envía igual con el código a la
    /// vista. Un restablecimiento no puede quedarse sin enviar porque falte una comodidad.</para>
    /// </summary>
    string? EnlaceDeCanjeDeContrasena(string tenantCode, string email, string token);
}
