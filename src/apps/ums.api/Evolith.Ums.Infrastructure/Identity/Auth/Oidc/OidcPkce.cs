using System.Security.Cryptography;
using System.Text;

namespace Evolith.Ums.Infrastructure.Identity.Auth.Oidc;

/// <summary>
/// Primitivas PKCE (RFC 7636) y valores anti-repetición (state/nonce) para el flujo
/// Authorization Code + PKCE. Todos los valores se generan con un RNG criptográfico.
/// </summary>
public static class OidcPkce
{
    /// <summary>Genera un <c>code_verifier</c> de alta entropía (43 chars, 32 bytes).</summary>
    public static string GenerateCodeVerifier()
        => OidcBase64Url.Encode(RandomNumberGenerator.GetBytes(32));

    /// <summary>Calcula el <c>code_challenge</c> con el método S256: BASE64URL(SHA256(verifier)).</summary>
    public static string ComputeS256Challenge(string codeVerifier)
    {
        var hash = SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier));
        return OidcBase64Url.Encode(hash);
    }

    /// <summary>Genera un valor opaco (state/nonce) de 32 bytes.</summary>
    public static string GenerateOpaqueValue()
        => OidcBase64Url.Encode(RandomNumberGenerator.GetBytes(32));
}
