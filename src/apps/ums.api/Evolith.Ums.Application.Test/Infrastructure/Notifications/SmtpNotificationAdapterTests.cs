namespace Evolith.Ums.Application.Test.Infrastructure.Notifications;

using System.IO;
using System.Net.Mail;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Evolith.Ums.Application.Common.Notifications;
using Evolith.Ums.Infrastructure.Services.Notifications;
using Xunit;

/// <summary>
/// G-270: el adaptador que entrega de verdad. Lo que estas pruebas fijan es lo que distingue a
/// este transporte del simulado que sustituye: compone un mensaje fiel y, cuando la entrega
/// falla, lo DICE en vez de devolver éxito.
///
/// <para>La salida a la red está detrás de <see cref="ISmtpDispatcher"/>, así que todo esto corre
/// sin relay: lo medido es la composición y el desenlace, que es donde han estado los fallos.</para>
/// </summary>
public class SmtpNotificationAdapterTests
{
    private const string Remitente = "sistemas@evolith.example";
    private const string Destinatario = "ana.torres@evolith.example";

    private sealed record MensajeCapturado(
        string DesdeDireccion,
        string DesdeNombre,
        (string Direccion, string Nombre)[] Para,
        string Asunto,
        string Cuerpo,
        bool EsHtml,
        Encoding? CodificacionCuerpo,
        string[] TiposDeVistaAlterna,
        string[] RecursosIncrustados,
        string? HtmlDeLaVistaAlterna);

    private sealed class DispatcherEspia : ISmtpDispatcher
    {
        public List<MensajeCapturado> Entregados { get; } = [];

        public SmtpOptions? UltimasOpciones { get; private set; }

        /// <summary>Direcciones cuyo envío debe fallar, para simular el relay caído.</summary>
        public HashSet<string> Rechaza { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Task SendAsync(MailMessage message, SmtpOptions options, CancellationToken cancellationToken = default)
        {
            UltimasOpciones = options;

            var para = message.To.Select(a => (a.Address, a.DisplayName)).ToArray();

            if (para.Any(p => Rechaza.Contains(p.Address)))
                return Task.FromException(new SmtpException(SmtpStatusCode.MailboxUnavailable, "buzon rechazado"));

            // Las vistas alternas se leen AQUI: el adaptador desecha el mensaje al terminar y
            // sus flujos se cierran con el.
            var vistas = message.AlternateViews.ToArray();
            var html = vistas.FirstOrDefault(v => v.ContentType.MediaType == "text/html");
            string? htmlLeido = null;
            if (html is not null)
            {
                html.ContentStream.Position = 0;
                htmlLeido = new StreamReader(html.ContentStream, Encoding.UTF8).ReadToEnd();
                html.ContentStream.Position = 0;
            }

            Entregados.Add(new MensajeCapturado(
                message.From!.Address,
                message.From!.DisplayName,
                para,
                message.Subject,
                message.Body,
                message.IsBodyHtml,
                message.BodyEncoding,
                vistas.Select(v => v.ContentType.MediaType ?? string.Empty).ToArray(),
                vistas.SelectMany(v => v.LinkedResources).Select(r => r.ContentId ?? string.Empty).ToArray(),
                htmlLeido));

            return Task.CompletedTask;
        }
    }

    private static SmtpNotificationAdapter Sujeto(DispatcherEspia dispatcher) =>
        new(
            dispatcher,
            Options.Create(new NotificationOptions
            {
                Provider = NotificationProvider.Smtp,
                Smtp = new SmtpOptions
                {
                    Host = "smtp.sendgrid.net",
                    Port = 25,
                    UseStartTls = true,
                    User = "apikey",
                    Password = "no-es-una-clave-real",
                    FromAddress = Remitente,
                    FromName = "UMS - Evolith",
                },
            }),
            NullLogger<SmtpNotificationAdapter>.Instance);

    [Fact]
    public async Task Envia_ComponeElMensajeConRemitenteDestinatarioAsuntoYCuerpo()
    {
        var dispatcher = new DispatcherEspia();

        await Sujeto(dispatcher).SendAsync(new UmsNotification(
            Recipient: Destinatario,
            Subject: "Restablecimiento de Contraseña — UMS",
            Body: "Codigo: 123456",
            RecipientName: "Ana Torres"));

        var entregado = Assert.Single(dispatcher.Entregados);
        Assert.Equal(Remitente, entregado.DesdeDireccion);
        Assert.Equal("UMS - Evolith", entregado.DesdeNombre);
        Assert.Equal(Destinatario, Assert.Single(entregado.Para).Direccion);
        Assert.Equal("Ana Torres", Assert.Single(entregado.Para).Nombre);
        Assert.Equal("Restablecimiento de Contraseña — UMS", entregado.Asunto);
        Assert.Equal("Codigo: 123456", entregado.Cuerpo);
    }

    [Fact]
    public async Task Envia_ElCuerpoViajaComoTextoPlanoEnUtf8()
    {
        // Las plantillas son texto con saltos de línea significativos —el código de
        // restablecimiento va en su propia línea, indentado—. Marcarlas como HTML colapsaría
        // ese bloque contra el párrafo anterior, y el acento roto delataría la codificación.
        var dispatcher = new DispatcherEspia();

        await Sujeto(dispatcher).SendAsync(new UmsNotification(
            Recipient: Destinatario,
            Subject: "Aprobación",
            Body: "Linea 1\n\n    TOKEN-ÁÉÍÓÚ\n\nLinea 2"));

        var entregado = Assert.Single(dispatcher.Entregados);
        Assert.False(entregado.EsHtml);
        Assert.Equal(Encoding.UTF8, entregado.CodificacionCuerpo);
        Assert.Contains("TOKEN-ÁÉÍÓÚ", entregado.Cuerpo, StringComparison.Ordinal);
        Assert.Contains("\n\n", entregado.Cuerpo, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Envia_SinNombreDeDestinatario_UsaSoloLaDireccion()
    {
        var dispatcher = new DispatcherEspia();

        await Sujeto(dispatcher).SendAsync(new UmsNotification(
            Recipient: Destinatario,
            Subject: "Solicitud recibida",
            Body: "cuerpo"));

        var entregado = Assert.Single(dispatcher.Entregados);
        Assert.Equal(Destinatario, Assert.Single(entregado.Para).Direccion);
        Assert.Equal(string.Empty, Assert.Single(entregado.Para).Nombre);
    }

    [Fact]
    public async Task Envia_PasaAlDispatcherLasOpcionesDelRelay()
    {
        var dispatcher = new DispatcherEspia();

        await Sujeto(dispatcher).SendAsync(new UmsNotification(Destinatario, "asunto", "cuerpo"));

        Assert.NotNull(dispatcher.UltimasOpciones);
        Assert.Equal("smtp.sendgrid.net", dispatcher.UltimasOpciones!.Host);
        Assert.Equal(25, dispatcher.UltimasOpciones.Port);
        Assert.True(dispatcher.UltimasOpciones.UseStartTls);
        Assert.Equal("apikey", dispatcher.UltimasOpciones.User);
    }

    [Fact]
    public async Task Envia_CuandoElRelayRechaza_PropagaElFallo()
    {
        // El corazón de G-270: el adaptador anterior devolvía éxito pasara lo que pasara, y por
        // eso un alta de usuario podía darse por notificada sin haber entregado nada.
        var dispatcher = new DispatcherEspia();
        dispatcher.Rechaza.Add(Destinatario);

        await Assert.ThrowsAsync<SmtpException>(() =>
            Sujeto(dispatcher).SendAsync(new UmsNotification(Destinatario, "asunto", "cuerpo")));

        Assert.Empty(dispatcher.Entregados);
    }

    [Fact]
    public async Task Envia_CanalNoSoportado_NoSeDaPorEntregado()
    {
        // SMTP entrega correo. Aceptar un SMS y devolver éxito sería reintroducir el defecto
        // por otra puerta.
        var dispatcher = new DispatcherEspia();

        await Assert.ThrowsAsync<NotSupportedException>(() =>
            Sujeto(dispatcher).SendAsync(new UmsNotification(
                Recipient: "+51999999999",
                Subject: "asunto",
                Body: "cuerpo",
                Channel: NotificationChannel.Sms)));

        Assert.Empty(dispatcher.Entregados);
    }

    [Fact]
    public async Task EnviaLote_IntentaTodosAunqueUnoFalle()
    {
        var dispatcher = new DispatcherEspia();
        dispatcher.Rechaza.Add("roto@evolith.example");

        var lote = new[]
        {
            new UmsNotification("uno@evolith.example", "asunto", "cuerpo"),
            new UmsNotification("roto@evolith.example", "asunto", "cuerpo"),
            new UmsNotification("dos@evolith.example", "asunto", "cuerpo"),
        };

        var fallo = await Assert.ThrowsAsync<AggregateException>(() =>
            Sujeto(dispatcher).SendBulkAsync(lote));

        // El tercero se intentó pese al fallo del segundo: abortar habría dejado sin aviso a
        // quien no tenía nada que ver con la dirección rota.
        Assert.Equal(2, dispatcher.Entregados.Count);
        Assert.Equal(
            new[] { "dos@evolith.example", "uno@evolith.example" },
            dispatcher.Entregados
                .SelectMany(e => e.Para)
                .Select(p => p.Direccion)
                .OrderBy(d => d, StringComparer.Ordinal)
                .ToArray());
        Assert.Single(fallo.InnerExceptions);
        Assert.IsType<SmtpException>(fallo.InnerExceptions[0]);
    }

    [Fact]
    public async Task Envia_ConCuerpoHtml_ViajanLasDosVersionesYElLogoIncrustado()
    {
        // El texto sigue siendo el cuerpo del mensaje y el HTML va como vista alterna: el cliente
        // elige. Y el logo viaja DENTRO del correo, no enlazado a este servidor, que fuera de la
        // red no resuelve y que muchos clientes bloquearian de todos modos.
        var dispatcher = new DispatcherEspia();

        await Sujeto(dispatcher).SendAsync(new UmsNotification(
            Recipient: Destinatario,
            Subject: "Restablecimiento",
            Body: "Codigo: 123456",
            HtmlBody: NotificationEmailLayout.Render(
                "Restablecimiento",
                "Su codigo vence pronto",
                new BloqueCorreo.Codigo("Codigo", "123456"))));

        var entregado = Assert.Single(dispatcher.Entregados);
        Assert.Equal("Codigo: 123456", entregado.Cuerpo);
        Assert.False(entregado.EsHtml);
        Assert.Equal(new[] { "text/html" }, entregado.TiposDeVistaAlterna);
        Assert.Equal(new[] { NotificationEmailLayout.LogoContentId }, entregado.RecursosIncrustados);
        Assert.Contains("123456", entregado.HtmlDeLaVistaAlterna!, StringComparison.Ordinal);
        Assert.Contains($"cid:{NotificationEmailLayout.LogoContentId}", entregado.HtmlDeLaVistaAlterna!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Envia_SinCuerpoHtml_NoAgregaVistasAlternas()
    {
        var dispatcher = new DispatcherEspia();

        await Sujeto(dispatcher).SendAsync(new UmsNotification(Destinatario, "asunto", "cuerpo"));

        var entregado = Assert.Single(dispatcher.Entregados);
        Assert.Empty(entregado.TiposDeVistaAlterna);
        Assert.Empty(entregado.RecursosIncrustados);
    }

    [Fact]
    public async Task EnviaLote_CuandoTodoVaBien_NoLanza()
    {
        var dispatcher = new DispatcherEspia();

        await Sujeto(dispatcher).SendBulkAsync(
        [
            new UmsNotification("uno@evolith.example", "asunto", "cuerpo"),
            new UmsNotification("dos@evolith.example", "asunto", "cuerpo"),
        ]);

        Assert.Equal(2, dispatcher.Entregados.Count);
    }
}
