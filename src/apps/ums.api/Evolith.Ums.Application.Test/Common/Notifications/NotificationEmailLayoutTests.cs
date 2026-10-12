namespace Evolith.Ums.Application.Test.Common.Notifications;

using Evolith.Ums.Application.Common.Notifications;
using Xunit;

/// <summary>
/// La marca del correo (G-270). Estas pruebas no juzgan si el resultado es bonito —eso no se
/// automatiza— sino las tres cosas que sí rompen de verdad: que el dato viaje escapado, que un
/// enlace que no es http(s) no se pinte, y que el logo se referencie por el identificador que el
/// adaptador adjunta.
/// </summary>
public class NotificationEmailLayoutTests
{
    [Fact]
    public void Render_LlevaTituloParrafoCodigoYPie()
    {
        var html = NotificationEmailLayout.Render(
            "Restablecimiento de contraseña",
            "Su código vence en 15 minutos.",
            new BloqueCorreo.Parrafo("Hola Ana Torres,"),
            new BloqueCorreo.Codigo("Código de restablecimiento", "r_YGpT5n"));

        Assert.Contains("Restablecimiento de contrase", html, StringComparison.Ordinal);
        Assert.Contains("Hola Ana Torres,", html, StringComparison.Ordinal);
        Assert.Contains("r_YGpT5n", html, StringComparison.Ordinal);
        Assert.Contains("RUC 20100412447", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_ReferenciaElLogoPorSuIdentificadorDeContenido()
    {
        // El adaptador adjunta la imagen con ese mismo identificador. Si los dos lados dejan de
        // coincidir, el correo sale con el hueco de una imagen rota y nada falla.
        var html = NotificationEmailLayout.Render("t", "p", new BloqueCorreo.Parrafo("x"));

        Assert.Contains($"cid:{NotificationEmailLayout.LogoContentId}", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_EscapaLoQueVieneDeDatos()
    {
        // El nombre, el motivo de un rechazo o la razón social son datos, y un dato sin escapar
        // dentro de HTML es una inyección — que aquí acabaría en el buzón de alguien.
        var html = NotificationEmailLayout.Render(
            "<script>alert(1)</script>",
            "vista previa",
            new BloqueCorreo.Parrafo("Hola <b>Ana</b> & Cía,"),
            new BloqueCorreo.Datos([("Motivo", "<img onerror=alert(2)>")]));

        Assert.DoesNotContain("<script>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<img onerror", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<b>Ana</b>", html, StringComparison.Ordinal);
        Assert.Contains("&lt;script&gt;", html, StringComparison.Ordinal);
        Assert.Contains("&amp; C", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_PintaElBotonCuandoLaUrlEsNavegable()
    {
        var html = NotificationEmailLayout.Render(
            "t", "p",
            new BloqueCorreo.Boton("Definir mi nueva contraseña", "https://portal.evolith.test/login?recuperar=1"));

        Assert.Contains("href=\"https://portal.evolith.test/login?recuperar=1\"", html, StringComparison.Ordinal);
        Assert.Contains("Definir mi nueva contrase", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("/login?recuperar=1")]
    [InlineData("mailto:sistemas@evolith.example")]
    [InlineData("")]
    public void Render_NoPintaBotonesQueNoSonHttp(string url)
    {
        var html = NotificationEmailLayout.Render("t", "p", new BloqueCorreo.Boton("Ir", url));

        Assert.DoesNotContain("<a href", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_ElPreencabezadoNoSeVeAlAbrirElMensaje()
    {
        // Es la línea que el gestor de correo muestra junto al asunto en la bandeja. Sin esta
        // caja oculta, esa línea la rellena el cliente con lo primero que encuentre — que aquí
        // sería el texto alternativo del logo.
        var html = NotificationEmailLayout.Render("t", "Su código vence en 15 minutos.");

        Assert.Contains("display:none", html, StringComparison.Ordinal);
        Assert.Contains("Su c", html, StringComparison.Ordinal);
    }
}
