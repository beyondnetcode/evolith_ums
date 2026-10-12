namespace Evolith.Ums.Application.Identity.Auth;

using System.Security.Cryptography;

/// <summary>
/// Genera el plaintext de un refresh token opaco (ADR-UMS-091): 64 bytes aleatorios
/// criptográficos en base64. Espeja el formato de <c>JwtTokenService.GenerateRefreshToken</c>,
/// pero vive en la capa de aplicación para que la rotación (que persiste el hash) no
/// dependa de la capa de presentación. El plaintext se devuelve una sola vez al cliente;
/// solo su hash SHA-256 se persiste (<see cref="RefreshTokenHasher"/>).
/// </summary>
public static class RefreshTokenGenerator
{
    public static string Generate()
    {
        var bytes = new byte[64];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes);
    }
}
