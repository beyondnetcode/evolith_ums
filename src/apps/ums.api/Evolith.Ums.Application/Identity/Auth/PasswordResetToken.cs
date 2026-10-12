namespace Evolith.Ums.Application.Identity.Auth;

using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

/// <summary>
/// Secreto de restablecimiento de contraseña: generación del plaintext y hash de persistencia.
///
/// <para>El plaintext se entrega UNA sola vez y por el canal del buzón (correo), nunca en la
/// respuesta HTTP de la petición anónima: si viajara en el cuerpo, quien conoce el correo ya
/// tendría el secreto y la prueba de posesión del buzón —única razón de ser del flujo— no
/// probaría nada.</para>
///
/// <para>Codificado en Base64Url para poder viajar tal cual dentro de un enlace sin escapes.</para>
/// </summary>
public static class PasswordResetToken
{
    /// <summary>
    /// Vida del secreto. Corta a propósito: el token es equivalente a la contraseña durante su
    /// vigencia, así que la ventana de exposición de un buzón comprometido debe ser mínima.
    /// </summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(15);

    /// <summary>256 bits de entropía criptográfica: inadivinable por fuerza bruta en línea o fuera de ella.</summary>
    public static string Generate()
    {
        var bytes = new byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Base64Url.EncodeToString(bytes);
    }

    /// <summary>
    /// SHA-256 hex en minúsculas del plaintext. Se persiste el hash, nunca el token: quien lea
    /// la tabla no puede canjear nada. No lleva sal ni coste porque el secreto ya tiene 256 bits
    /// de entropía — el ataque de diccionario que justifica BCrypt en contraseñas no aplica.
    /// </summary>
    public static string Hash(string plaintext)
    {
        ArgumentException.ThrowIfNullOrEmpty(plaintext);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(plaintext)));
    }
}
