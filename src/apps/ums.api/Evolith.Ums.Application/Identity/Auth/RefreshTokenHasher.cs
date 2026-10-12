namespace Evolith.Ums.Application.Identity.Auth;

using System.Security.Cryptography;
using System.Text;

/// <summary>
/// Hash de refresh tokens para persistencia (ADR-UMS-091). Se guarda el hash, nunca el
/// plaintext — como una contraseña. La búsqueda en renovación/revocación se hace
/// hasheando el token presentado y comparando. SHA-256 hex en minúsculas.
/// </summary>
public static class RefreshTokenHasher
{
    public static string Hash(string plaintext)
    {
        ArgumentException.ThrowIfNullOrEmpty(plaintext);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(plaintext));
        return Convert.ToHexStringLower(bytes);
    }
}
