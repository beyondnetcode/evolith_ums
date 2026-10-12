namespace Evolith.Ums.Infrastructure.Identity.Auth.Oidc;

/// <summary>
/// Codificación/decodificación Base64Url (RFC 7515) sin dependencias externas.
/// Usada por PKCE y por la verificación de firma del <c>id_token</c>.
/// </summary>
internal static class OidcBase64Url
{
    public static string Encode(ReadOnlySpan<byte> bytes) =>
        Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    public static byte[] Decode(string input)
    {
        var s = input.Replace('-', '+').Replace('_', '/');
        s += (s.Length % 4) switch
        {
            2 => "==",
            3 => "=",
            _ => string.Empty,
        };
        return Convert.FromBase64String(s);
    }
}
