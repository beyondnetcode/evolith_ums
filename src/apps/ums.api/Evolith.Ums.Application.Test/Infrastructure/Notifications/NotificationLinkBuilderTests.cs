namespace Evolith.Ums.Application.Test.Infrastructure.Notifications;

using Evolith.Ums.Infrastructure.Services.Notifications;
using Microsoft.Extensions.Options;
using Xunit;

/// <summary>
/// El enlace que lleva el correo de restablecimiento (G-270).
/// </summary>
public class NotificationLinkBuilderTests
{
    private static NotificationLinkBuilder Sujeto(string portal) =>
        new(Options.Create(new NotificationOptions { PortalBaseUrl = portal }));

    [Fact]
    public void SinPortalDeclarado_NoHayEnlace()
    {
        // Y no es un fallo: el correo sale igual con el código a la vista. Un restablecimiento no
        // puede quedarse sin enviar porque falte una comodidad.
        var enlace = Sujeto(string.Empty).EnlaceDeCanjeDeContrasena("EVOLITH", "ana@evolith.example", "tok");

        Assert.Null(enlace);
    }

    [Fact]
    public void ConPortalDeclarado_ApuntaAlCanjeConSusParametros()
    {
        var enlace = Sujeto("http://srvtms:8080/ums_web")
            .EnlaceDeCanjeDeContrasena("EVOLITH", "ana@evolith.example", "r_YGpT5n");

        Assert.Equal(
            "http://srvtms:8080/ums_web/login?recuperar=1&inquilino=EVOLITH&correo=ana%40evolith.example&codigo=r_YGpT5n",
            enlace);
    }

    [Fact]
    public void LaBarraFinalDelPortalNoDuplicaLaDelCamino()
    {
        var enlace = Sujeto("http://srvtms:8080/ums_web/")
            .EnlaceDeCanjeDeContrasena("EVOLITH", "ana@evolith.example", "tok");

        Assert.StartsWith("http://srvtms:8080/ums_web/login?", enlace, StringComparison.Ordinal);
        Assert.DoesNotContain("//login", enlace, StringComparison.Ordinal);
    }

    [Fact]
    public void CodificaLoQueVaEnLaConsulta()
    {
        // Los tokens de UMS son base64url y no traen caracteres problemáticos, pero el código de
        // inquilino y el correo salen de datos: sin codificar, un `&` partiría la consulta y el
        // enlace llegaría roto.
        var enlace = Sujeto("https://portal.test")
            .EnlaceDeCanjeDeContrasena("A&B", "ana+uat@evolith.example", "a b/c");

        Assert.Contains("inquilino=A%26B", enlace, StringComparison.Ordinal);
        Assert.Contains("correo=ana%2Buat%40evolith.example", enlace, StringComparison.Ordinal);
        Assert.Contains("codigo=a%20b%2Fc", enlace, StringComparison.Ordinal);
    }
}
