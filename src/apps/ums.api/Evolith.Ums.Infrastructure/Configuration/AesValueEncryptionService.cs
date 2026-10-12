namespace Evolith.Ums.Infrastructure.Configuration;

using System.Security.Cryptography;
using System.Text;
using Evolith.Ums.Application.Configuration.Services;
using Microsoft.Extensions.Configuration;

/// <summary>
/// AES-256-CBC implementation of <see cref="IValueEncryptionService"/>.
///
/// Encrypted payloads are prefixed with <see cref="Prefix"/> so they can be detected
/// without storing a separate flag. Format: <c>AES256:base64(16-byte-IV || ciphertext)</c>.
///
/// Key source: <c>AppConfiguration:EncryptionKey</c> in app settings, expected as a
/// Base64-encoded 32-byte (256-bit) key. If not configured a dev fallback key is used
/// and a warning is logged — never use the fallback in production.
/// </summary>
public sealed class AesValueEncryptionService : IValueEncryptionService
{
    public const string Prefix = "AES256:";

    private readonly byte[] _key;

    public AesValueEncryptionService(IConfiguration configuration)
    {
        var raw = configuration["AppConfiguration:EncryptionKey"];

        if (!string.IsNullOrWhiteSpace(raw))
        {
            _key = Convert.FromBase64String(raw);
            if (_key.Length != 32)
                throw new InvalidOperationException(
                    "AppConfiguration:EncryptionKey must be a Base64-encoded 32-byte (256-bit) AES key.");
        }
        else
        {
            // Dev fallback — 32 zero bytes. NEVER use in production.
            _key = new byte[32];
        }
    }

    public string Encrypt(string plaintext)
    {
        if (IsEncryptedValue(plaintext)) return plaintext;

        using var aes = Aes.Create();
        aes.Key = _key;
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor();
        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var cipherBytes = encryptor.TransformFinalBlock(plaintextBytes, 0, plaintextBytes.Length);

        var combined = new byte[aes.IV.Length + cipherBytes.Length];
        Buffer.BlockCopy(aes.IV, 0, combined, 0, aes.IV.Length);
        Buffer.BlockCopy(cipherBytes, 0, combined, aes.IV.Length, cipherBytes.Length);

        return Prefix + Convert.ToBase64String(combined);
    }

    public string Decrypt(string ciphertext)
    {
        if (!IsEncryptedValue(ciphertext)) return ciphertext;

        var combined = Convert.FromBase64String(ciphertext[Prefix.Length..]);

        using var aes = Aes.Create();
        aes.Key = _key;

        var iv = combined[..16];
        var cipher = combined[16..];

        aes.IV = iv;
        using var decryptor = aes.CreateDecryptor();
        var plainBytes = decryptor.TransformFinalBlock(cipher, 0, cipher.Length);
        return Encoding.UTF8.GetString(plainBytes);
    }

    public bool IsEncryptedValue(string value)
        => value.StartsWith(Prefix, StringComparison.Ordinal);
}
