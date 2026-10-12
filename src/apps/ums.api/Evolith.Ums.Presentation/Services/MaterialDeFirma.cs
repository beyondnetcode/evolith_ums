using System;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace Evolith.Ums.Presentation.Services;

/// <summary>
/// La clave con la que UMS firma sus portadores, y el material público que publica.
/// </summary>
/// <remarks>
/// <para>
/// Es <see href="https://github.com/beyondnetcode/evolith_arch32">ADR-0157</see> ejecutado:
/// <strong>UMS firma con clave privada RSA en RS256 y publica solo la pública</strong>.
/// La diferencia con HS256 no es de robustez criptográfica sino de quién puede emitir:
/// con un secreto compartido, <em>verificar</em> y <em>firmar</em> son la misma
/// capacidad, así que cada satélite que valida es también un emisor y ninguna firma
/// identifica su origen. Con RS256 la clave pública se reparte sin custodia y no
/// concede nada.
/// </para>
/// <para>
/// <strong>La clave privada no entra al repositorio bajo ninguna forma</strong> —ni
/// PEM, ni JWK, ni base64, ni marcador de posición—. Llega por configuración
/// (<c>Jwt:PrivateKeyPem</c>), y el arranque falla si no está: G-203 dejó escrito lo
/// que pasa cuando un marcador del repositorio acaba siendo la clave de todos los
/// entornos.
/// </para>
/// </remarks>
public sealed class MaterialDeFirma : IDisposable
{
    /// <summary>
    /// La regla de arranque de la clave de firma, en UN solo sitio. La comprueba el
    /// constructor —que es quien la necesita— y también la composición del host.
    ///
    /// <para>POR QUÉ TAMBIÉN EN LA COMPOSICIÓN. Esta clase se registra como singleton
    /// PEREZOSO: sin una llamada temprana, un despliegue sin clave privada arrancaría
    /// «bien» y solo fallaría en la primera petición que necesitara firmar o validar, con
    /// un 500 opaco y a horas de haberse desplegado. Antes esa red la tendía la validación
    /// de <c>Jwt:Secret</c>, que era eager; al retirarse con HS256 (G-199) había que
    /// reponerla aquí.</para>
    /// </summary>
    public static void ExigirClaveFueraDeDesarrollo(string? pem, IHostEnvironment entorno)
    {
        ArgumentNullException.ThrowIfNull(entorno);

        if (!string.IsNullOrWhiteSpace(pem) || entorno.IsDevelopment()) return;

        throw new InvalidOperationException(
            "Jwt:PrivateKeyPem no está configurado. UMS firma sus portadores con clave "
            + "privada RSA (ADR-0157) y sin ella no puede emitir ninguno. Se inyecta por "
            + "secreto del entorno; no existe valor por omisión, y no debe existir: un "
            + "marcador de posición acaba siendo la clave de todos los entornos (G-203).");
    }

    /// <summary>Tamaño mínimo de clave. Por debajo, UMS no arranca.</summary>
    private const int BitsMinimos = 2048;

    private readonly RSA rsa;

    /// <summary>Construye el material a partir de la configuración.</summary>
    /// <param name="configuration">Configuración de la aplicación.</param>
    /// <param name="entorno">Entorno de ejecución.</param>
    /// <param name="logger">Registro de arranque.</param>
    public MaterialDeFirma(
        IConfiguration configuration,
        IHostEnvironment entorno,
        ILogger<MaterialDeFirma> logger)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(entorno);
        ArgumentNullException.ThrowIfNull(logger);

        string? pem = configuration["Jwt:PrivateKeyPem"];

        rsa = RSA.Create();

        if (string.IsNullOrWhiteSpace(pem))
        {
            // En desarrollo se genera un par efímero. No es una excepción a la regla de
            // §4.7 —«la clave privada no entra al repositorio en ninguna forma»—: es la
            // única manera de cumplirla sin dejar el repositorio inarrancable, y es lo
            // mismo que el ADR prescribe para los arneses de prueba. Cambia en cada
            // arranque, así que los tokens no sobreviven a un reinicio: en desarrollo eso
            // es una molestia, y en producción sería un incidente, por lo que allí falla.
            ExigirClaveFueraDeDesarrollo(pem, entorno);

            rsa.KeySize = BitsMinimos;

            // El respaldo efímero sirve para UN proceso. Con varias réplicas cada una firma
            // con su propia clave y publica en su JWKS solo la suya, así que un token emitido
            // por una réplica NO verifica contra el JWKS que responde otra. Medido el
            // 2026-08-04 en kind con dos réplicas: la mitad de los tokens se rechazaban, de
            // forma intermitente y sin ningún error que lo explicara. El proceso no puede
            // saber cuántas réplicas hay, así que lo dice en voz alta y que lo vea quien
            // despliegue.
            logger.LogWarning(
                "UMS ha generado una clave de firma EFÍMERA (kid distinto en cada arranque) porque "
                + "Jwt:PrivateKeyPem no está configurado. Solo es válido con UNA réplica: con varias, "
                + "cada una firma con una clave distinta y los tokens de una no se verifican contra el "
                + "JWKS de otra. Configura Jwt:PrivateKeyPem antes de escalar (ADR-0157 §4.7).");
        }
        else
        {
            try
            {
                rsa.ImportFromPem(pem);
            }
            catch (Exception error) when (error is ArgumentException or CryptographicException)
            {
                // Las DOS excepciones, no una. `ImportFromPem` lanza ArgumentException cuando no
                // reconoce la etiqueta PEM, y CryptographicException cuando la etiqueta es válida
                // pero el cuerpo no --«ASN1 corrupted data»--. Solo se capturaba la primera, así
                // que una clave mal copiada al secreto del entorno abortaba el arranque con un
                // error de ASN.1 que no nombra el ajuste que hay que corregir. Medido el
                // 2026-08-18 al cubrir esta rama con `MaterialDeFirmaTests`.
                throw new InvalidOperationException(
                    "Jwt:PrivateKeyPem no es una clave RSA en PEM legible.", error);
            }
        }

        // Fail-closed de arranque: una clave corta no avisa en tiempo de ejecución,
        // firma igual y deja el sistema debil sin que nada falle.
        if (rsa.KeySize < BitsMinimos)
        {
            throw new InvalidOperationException(
                $"La clave de firma tiene {rsa.KeySize} bits y el mínimo es {BitsMinimos} (ADR-0157 §4.7).");
        }

        RSAParameters publicas = rsa.ExportParameters(includePrivateParameters: false);
        Modulo = Base64Url(publicas.Modulus!);
        Exponente = Base64Url(publicas.Exponent!);
        Kid = HuellaJwk(Modulo, Exponente);

        var clave = new RsaSecurityKey(rsa) { KeyId = Kid };
        Credenciales = new SigningCredentials(clave, SecurityAlgorithms.RsaSha256);
        ClavePublica = new RsaSecurityKey(publicas) { KeyId = Kid };
    }

    /// <summary>Identificador de la clave, que viaja en la cabecera del token.</summary>
    /// <remarks>
    /// Es la huella JWK de RFC 7638, no un número de serie: dos despliegues con la
    /// misma clave obtienen el mismo <c>kid</c> y dos claves distintas nunca colisionan.
    /// De eso depende que la rotación pueda solaparse (ADR-0157 §4.4).
    /// </remarks>
    public string Kid { get; }

    /// <summary>Credenciales con las que se firma. RS256, siempre.</summary>
    public SigningCredentials Credenciales { get; }

    /// <summary>Clave pública, para que UMS verifique lo que él mismo emitió.</summary>
    public RsaSecurityKey ClavePublica { get; }

    private string Modulo { get; }

    private string Exponente { get; }

    /// <summary>
    /// Proyecta la clave pública como JWK, tal como sale en el JWKS.
    /// </summary>
    /// <remarks>
    /// Declara <c>kty</c>, <c>use</c>, <c>alg</c>, <c>kid</c>, <c>n</c> y <c>e</c>, y
    /// <strong>nada más</strong>. RFC 7517 §5 y OIDC Discovery §3 prohíben que un JWK
    /// Set publicado contenga material privado o simétrico: un JWKS con una <c>d</c>
    /// dentro no es un error de formato, es un incidente de seguridad.
    /// </remarks>
    /// <returns>El JWK público.</returns>
    public object ComoJwkPublico() => new
    {
        kty = "RSA",
        use = "sig",
        alg = "RS256",
        kid = Kid,
        n = Modulo,
        e = Exponente,
    };

    /// <summary>Libera la clave.</summary>
    public void Dispose() => rsa.Dispose();

    private static string Base64Url(byte[] valor) =>
        Convert.ToBase64String(valor).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    /// <summary>Huella JWK de RFC 7638: SHA-256 del JSON canónico con las claves ordenadas.</summary>
    private static string HuellaJwk(string modulo, string exponente)
    {
        // El orden de los miembros y la ausencia de espacios NO son estilo: RFC 7638 §3
        // exige exactamente esta forma canónica, y cualquier desviación produce una
        // huella distinta para la misma clave.
        string canonico = JsonSerializer.Serialize(new
        {
            e = exponente,
            kty = "RSA",
            n = modulo,
        });

        return Base64Url(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(canonico)));
    }
}
