using System.Net;
using System.Text;

namespace Evolith.Ums.Application.Common.Notifications;

/// <summary>
/// Pieza con la que se arma el cuerpo HTML de una notificación.
///
/// <para>Cada plantilla describe QUÉ dice —párrafos, un código, un botón, una ficha de datos— y
/// <see cref="NotificationEmailLayout"/> decide CÓMO se ve. Así la marca vive en un solo sitio:
/// cambiar el color de la cabecera no obliga a tocar catorce plantillas.</para>
/// </summary>
public abstract record BloqueCorreo
{
    private BloqueCorreo() { }

    /// <summary>Texto corrido.</summary>
    public sealed record Parrafo(string Texto) : BloqueCorreo;

    /// <summary>
    /// Un secreto que el destinatario tiene que copiar: código de restablecimiento, contraseña
    /// temporal. Va en caja monoespaciada y con quiebre de línea forzado, porque los tokens de
    /// UMS miden 43 caracteres y sin eso desbordan el ancho del mensaje.
    /// </summary>
    public sealed record Codigo(string Etiqueta, string Valor) : BloqueCorreo;

    /// <summary>Llamada a la acción. Solo se pinta si la URL es http(s); ver <c>UrlSegura</c>.</summary>
    public sealed record Boton(string Texto, string Url) : BloqueCorreo;

    /// <summary>Ficha de pares etiqueta/valor — solicitante, organización, rol.</summary>
    public sealed record Datos(IReadOnlyList<(string Etiqueta, string Valor)> Filas) : BloqueCorreo;

    /// <summary>Letra pequeña: vigencia, advertencia de «si no fue usted», a quién avisar.</summary>
    public sealed record Nota(string Texto) : BloqueCorreo;
}

/// <summary>
/// La marca de Evolith aplicada al correo, en un solo lugar.
///
/// <para><b>Por qué es todo tablas y estilos en línea.</b> Un cliente de correo no es un
/// navegador: Outlook de escritorio compone con el motor de Word, Gmail descarta la mayor parte
/// de lo que venga en <c>&lt;style&gt;</c>, y ninguno garantiza flexbox, grid ni hojas externas.
/// Lo que sí respetan todos son tablas anidadas y atributos <c>style</c> en cada celda. El
/// resultado se lee mal como HTML moderno y se ve bien donde tiene que verse.</para>
///
/// <para><b>El logo viaja incrustado</b> (<c>cid:</c>), no enlazado. Un <c>&lt;img src="http://…"&gt;</c>
/// apuntando a este servidor no cargaría fuera de la red —y muchos clientes bloquean la imagen
/// remota por omisión hasta que el lector la autoriza—, así que la imagen se adjunta al propio
/// mensaje y se referencia por su identificador de contenido.</para>
///
/// <para><b>Todo texto se escapa.</b> Los nombres, motivos de rechazo y razones sociales que
/// entran aquí vienen de datos, y en HTML un dato sin escapar es una inyección.</para>
/// </summary>
public static class NotificationEmailLayout
{
    /// <summary>
    /// Identificador con el que el adaptador de correo adjunta el logo. Lo consume
    /// <c>SmtpNotificationAdapter</c>: si cambia aquí, cambia allí, y por eso es una constante
    /// pública y no una cadena suelta repetida en dos capas.
    /// </summary>
    public const string LogoContentId = "logo-evolith";

    // Paleta de marca — semilla `#0f3e67` de ADR-0228, la misma de la que derivan los tokens del
    // portal (`tokens-evolith.generated.css`). No se leen de allí porque ese CSS es del navegador
    // y aquí hace falta el literal: el correo no puede resolver variables.
    private const string Navy = "#0f3e67";
    private const string NavyOscuro = "#042139";
    private const string Verde = "#41a62a";
    private const string FondoPagina = "#f0f5fa";
    private const string BordeSuave = "#dbe7f2";
    private const string BordeCodigo = "#b8cfe4";
    private const string TextoPrincipal = "#0a2843";
    private const string TextoSecundario = "#4b5f73";
    private const string TextoPie = "#8db0d1";

    private const string Tipografia =
        "Montserrat,'Segoe UI',Roboto,Helvetica,Arial,sans-serif";

    private const string PieLegal =
        "Evolith UMS &middot; RUC 20100412447 &middot; Operador Log&iacute;stico Aduanero desde 1978";

    /// <summary>
    /// Arma el mensaje completo.
    /// </summary>
    /// <param name="titulo">Encabezado dentro del cuerpo. Suele repetir el asunto.</param>
    /// <param name="preencabezado">
    /// Las palabras que el gestor de correo muestra junto al asunto en la bandeja. Sin esto, esa
    /// línea la rellena el cliente con lo primero que encuentre en el HTML — que aquí sería el
    /// texto alternativo del logo.
    /// </param>
    public static string Render(string titulo, string preencabezado, params BloqueCorreo[] bloques)
    {
        var html = new StringBuilder(4096);

        html.Append("<!DOCTYPE html><html lang=\"es\"><head><meta charset=\"utf-8\">")
            .Append("<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">")
            .Append("<title>").Append(E(titulo)).Append("</title></head>")
            .Append("<body style=\"margin:0;padding:0;background:").Append(FondoPagina)
            .Append(";font-family:").Append(Tipografia).Append(";\">");

        // Preencabezado: se lee en la bandeja de entrada y no se ve al abrir el mensaje.
        html.Append("<div style=\"display:none;max-height:0;overflow:hidden;opacity:0;\">")
            .Append(E(preencabezado))
            .Append("</div>");

        html.Append("<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" style=\"background:")
            .Append(FondoPagina).Append(";\"><tr><td align=\"center\" style=\"padding:24px 12px;\">")
            .Append("<table role=\"presentation\" width=\"600\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" style=\"width:600px;max-width:100%;background:#ffffff;border:1px solid ")
            .Append(BordeSuave).Append(";border-radius:12px;overflow:hidden;\">");

        // Cabecera con el logo sobre el navy corporativo.
        html.Append("<tr><td style=\"background:").Append(Navy).Append(";padding:22px 28px;\">")
            .Append("<img src=\"cid:").Append(LogoContentId)
            .Append("\" alt=\"Evolith\" height=\"34\" style=\"height:34px;display:block;border:0;\"></td></tr>")
            .Append("<tr><td style=\"height:4px;background:").Append(Verde)
            .Append(";font-size:0;line-height:0;\">&nbsp;</td></tr>");

        html.Append("<tr><td style=\"padding:28px;\">")
            .Append("<h1 style=\"margin:0 0 18px;font-size:20px;line-height:28px;font-weight:700;color:")
            .Append(TextoPrincipal).Append(";\">").Append(E(titulo)).Append("</h1>");

        foreach (var bloque in bloques)
            html.Append(Renderizar(bloque));

        html.Append("</td></tr>");

        html.Append("<tr><td style=\"background:").Append(NavyOscuro).Append(";padding:18px 28px;color:")
            .Append(TextoPie).Append(";font-size:12px;line-height:18px;\">")
            .Append(PieLegal)
            .Append("<br>Mensaje autom&aacute;tico del sistema de gesti&oacute;n de usuarios. No responda a este correo.")
            .Append("</td></tr>");

        html.Append("</table></td></tr></table></body></html>");

        return html.ToString();
    }

    private static string Renderizar(BloqueCorreo bloque) => bloque switch
    {
        BloqueCorreo.Parrafo p =>
            $"<p style=\"margin:0 0 14px;font-size:15px;line-height:23px;color:{TextoPrincipal};\">{E(p.Texto)}</p>",

        BloqueCorreo.Codigo c =>
            "<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" style=\"margin:0 0 18px;\"><tr><td style=\"background:"
            + FondoPagina + ";border:1px dashed " + BordeCodigo + ";border-radius:10px;padding:16px;\">"
            + $"<div style=\"margin:0 0 6px;font-size:11px;letter-spacing:.6px;text-transform:uppercase;color:{TextoSecundario};\">{E(c.Etiqueta)}</div>"
            + $"<div style=\"font-family:Consolas,'Courier New',monospace;font-size:15px;line-height:22px;color:{TextoPrincipal};word-break:break-all;\">{E(c.Valor)}</div>"
            + "</td></tr></table>",

        // Botón en tabla, no en <a> suelto: Outlook ignora el relleno de un enlace en línea y el
        // botón se quedaría en un texto azul subrayado.
        BloqueCorreo.Boton b when UrlSegura(b.Url) =>
            "<table role=\"presentation\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" style=\"margin:4px 0 20px;\"><tr>"
            + $"<td align=\"center\" bgcolor=\"{Navy}\" style=\"border-radius:8px;\">"
            + $"<a href=\"{E(b.Url)}\" style=\"display:inline-block;padding:13px 26px;font-size:15px;font-weight:600;color:#ffffff;text-decoration:none;\">{E(b.Texto)}</a>"
            + "</td></tr></table>",

        // Una URL que no es http(s) no se pinta. `javascript:` en el href de un correo es el
        // camino corto a un incidente, y aqui la URL la compone el servidor pero la arma con datos.
        BloqueCorreo.Boton => string.Empty,

        BloqueCorreo.Datos d => RenderizarDatos(d),

        BloqueCorreo.Nota n =>
            $"<p style=\"margin:0 0 12px;font-size:13px;line-height:20px;color:{TextoSecundario};\">{E(n.Texto)}</p>",

        _ => string.Empty,
    };

    private static string RenderizarDatos(BloqueCorreo.Datos datos)
    {
        var html = new StringBuilder();
        html.Append("<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" style=\"margin:0 0 18px;border-collapse:collapse;\">");

        foreach (var (etiqueta, valor) in datos.Filas)
        {
            html.Append("<tr>")
                .Append($"<td style=\"padding:7px 12px 7px 0;font-size:13px;color:{TextoSecundario};white-space:nowrap;border-bottom:1px solid {BordeSuave};\">")
                .Append(E(etiqueta)).Append("</td>")
                .Append($"<td style=\"padding:7px 0;font-size:14px;font-weight:600;color:{TextoPrincipal};border-bottom:1px solid {BordeSuave};\">")
                .Append(E(valor)).Append("</td></tr>");
        }

        return html.Append("</table>").ToString();
    }

    private static bool UrlSegura(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    private static string E(string? texto) => WebUtility.HtmlEncode(texto ?? string.Empty);
}
