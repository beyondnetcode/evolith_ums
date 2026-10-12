namespace Evolith.Ums.Application.Configuration.Services;

/// <summary>
/// Encrypts and decrypts sensitive configuration values (BR-5 of FS-13).
/// Implementations must be thread-safe and idempotent on already-encrypted values.
/// </summary>
public interface IValueEncryptionService
{
    /// <summary>Encrypts a plaintext value. Returns the ciphertext string.</summary>
    string Encrypt(string plaintext);

    /// <summary>Decrypts a ciphertext produced by <see cref="Encrypt"/>.</summary>
    string Decrypt(string ciphertext);

    /// <summary>Returns true when the value string is already an encrypted payload.</summary>
    bool IsEncryptedValue(string value);
}
