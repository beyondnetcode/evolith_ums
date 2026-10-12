using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Evolith.Ums.Presentation.Services;

namespace Evolith.Ums.Presentation.IntegrationTest.Security;

/// <summary>
/// G-199 — el material de firma RS256, comprobado sin levantar un host.
///
/// <para>POR QUÉ EXISTE. Al cerrar la etapa E3 de ADR-0157, la única prueba que llegaba a ejercitar
/// la lectura de <c>Jwt:PrivateKeyPem</c> era la factoría de contrato en «Production», y esa muere
/// antes de resolver el singleton por un defecto ajeno y anterior (`Persistence:SeedDevData` en
/// true con entorno productivo). Es decir: la rama que importa una clave REAL —la que corre en
/// todo despliegue— no la cubría nadie. Aquí se construye la clase directamente, que además es
/// mucho más rápido que un host.</para>
/// </summary>
public sealed class MaterialDeFirmaTests
{
    [Fact]
    public void ConClavePemValida_ImportaLaClaveYPublicaSuJwk()
    {
        using var origen = RSA.Create(2048);
        using var material = Crear(origen.ExportRSAPrivateKeyPem(), Environments.Production);

        material.Kid.Should().NotBeNullOrWhiteSpace(
            "el kid es la huella JWK de RFC 7638 y es lo que ata un token a la clave que lo firmó");

        material.Credenciales.Algorithm.Should().Be("RS256");
        material.ComoJwkPublico().Should().NotBeNull();
    }

    [Fact]
    public void ConClavePemValida_ElKidEsEstableEntreInstancias()
    {
        // La huella depende SOLO del material público, así que dos procesos que carguen la misma
        // clave deben publicar el mismo kid. Si no, un satélite que cachee el JWKS de una réplica
        // rechazaría los tokens de otra (ADR-0157 §4.7).
        using var origen = RSA.Create(2048);
        var pem = origen.ExportRSAPrivateKeyPem();

        using var primera = Crear(pem, Environments.Production);
        using var segunda = Crear(pem, Environments.Production);

        segunda.Kid.Should().Be(primera.Kid);
    }

    [Fact]
    public void FueraDeDesarrolloSinClave_FallaAlConstruir()
    {
        var construir = () => Crear(pem: null, Environments.Production);

        construir.Should().Throw<InvalidOperationException>(
                "una API de identidad que arranca sin poder firmar es peor que una que no arranca")
            .WithMessage("*Jwt:PrivateKeyPem*");
    }

    [Fact]
    public void EnDesarrolloSinClave_GeneraUnParEfimero()
    {
        using var material = Crear(pem: null, Environments.Development);

        material.Kid.Should().NotBeNullOrWhiteSpace(
            "en desarrollo se genera un par efímero para que el repositorio arranque sin ceremonia");
    }

    [Fact]
    public void ConClaveMasCortaQueElMinimo_FallaAlConstruir()
    {
        // Una clave corta no avisa en tiempo de ejecución: firma igual y deja el sistema débil sin
        // que nada falle. Por eso se rechaza al construir.
        using var corta = RSA.Create(1024);

        var construir = () => Crear(corta.ExportRSAPrivateKeyPem(), Environments.Production);

        construir.Should().Throw<InvalidOperationException>().WithMessage("*1024*");
    }

    [Fact]
    public void ConPemIlegible_FallaAlConstruir()
    {
        var construir = () => Crear("-----BEGIN RSA PRIVATE KEY-----\nno es una clave\n-----END RSA PRIVATE KEY-----",
            Environments.Production);

        construir.Should().Throw<InvalidOperationException>().WithMessage("*PEM*");
    }

    private static MaterialDeFirma Crear(string? pem, string entorno)
    {
        var configuracion = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Jwt:PrivateKeyPem"] = pem })
            .Build();

        return new MaterialDeFirma(configuracion, new EntornoFalso(entorno), NullLogger<MaterialDeFirma>.Instance);
    }

    private sealed class EntornoFalso(string nombre) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = nombre;

        public string ApplicationName { get; set; } = "Evolith.Ums.Presentation.IntegrationTest";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } =
            new PhysicalFileProvider(AppContext.BaseDirectory);
    }
}
