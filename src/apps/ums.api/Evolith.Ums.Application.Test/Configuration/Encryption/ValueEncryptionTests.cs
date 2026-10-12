namespace Evolith.Ums.Application.Test.Configuration.Encryption;

using Evolith.Ums.Infrastructure.Configuration;
using Microsoft.Extensions.Configuration;
using Xunit;

/// <summary>
/// Tests for AesValueEncryptionService: round-trip correctness, prefix detection,
/// idempotency, and redaction logic used by query handlers (BR-5, FS-13).
/// </summary>
public class ValueEncryptionTests
{
    private static AesValueEncryptionService MakeService(string? key = null)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(key is null
                ? []
                : new Dictionary<string, string?> { ["AppConfiguration:EncryptionKey"] = key })
            .Build();
        return new AesValueEncryptionService(config);
    }

    private static string ValidBase64Key()
    {
        var key = new byte[32];
        System.Security.Cryptography.RandomNumberGenerator.Fill(key);
        return Convert.ToBase64String(key);
    }

    // ── Round-trip ────────────────────────────────────────────────────────────

    [Fact]
    public void EncryptThenDecrypt_ReturnsOriginalValue()
    {
        var svc       = MakeService(ValidBase64Key());
        const string plain = "super-secret-api-key-12345";

        var cipher = svc.Encrypt(plain);
        var result = svc.Decrypt(cipher);

        Assert.Equal(plain, result);
    }

    [Fact]
    public void Encrypt_ProducesPrefix()
    {
        var svc    = MakeService(ValidBase64Key());
        var cipher = svc.Encrypt("value");

        Assert.StartsWith(AesValueEncryptionService.Prefix, cipher);
    }

    [Fact]
    public void Encrypt_TwiceSamePlaintext_ProducesDifferentCiphertext()
    {
        var svc   = MakeService(ValidBase64Key());
        var c1    = svc.Encrypt("value");
        var c2    = svc.Encrypt("value");

        // Different IV per call → different ciphertext
        Assert.NotEqual(c1, c2);
    }

    // ── Detection ─────────────────────────────────────────────────────────────

    [Fact]
    public void IsEncryptedValue_DetectsEncryptedPayload()
    {
        var svc    = MakeService(ValidBase64Key());
        var cipher = svc.Encrypt("hello");

        Assert.True(svc.IsEncryptedValue(cipher));
    }

    [Fact]
    public void IsEncryptedValue_ReturnsFalseForPlaintext()
    {
        var svc = MakeService(ValidBase64Key());

        Assert.False(svc.IsEncryptedValue("plain-value"));
    }

    // ── Idempotency ───────────────────────────────────────────────────────────

    [Fact]
    public void Encrypt_AlreadyEncryptedValue_IsNotDoubleEncrypted()
    {
        var svc    = MakeService(ValidBase64Key());
        var cipher = svc.Encrypt("value");

        // Calling Encrypt again on an already-encrypted string must return it unchanged.
        var again = svc.Encrypt(cipher);

        Assert.Equal(cipher, again);
    }

    [Fact]
    public void Decrypt_PlaintextPassthrough_WhenNotEncrypted()
    {
        var svc = MakeService(ValidBase64Key());

        var result = svc.Decrypt("already-plain");

        Assert.Equal("already-plain", result);
    }

    // ── Dev fallback (no key configured) ─────────────────────────────────────

    [Fact]
    public void DevFallback_EncryptDecrypt_RoundTrip()
    {
        var svc    = MakeService(key: null); // uses zero-byte dev key
        var cipher = svc.Encrypt("dev-value");
        var result = svc.Decrypt(cipher);

        Assert.Equal("dev-value", result);
    }

    // ── Key length validation ─────────────────────────────────────────────────

    [Fact]
    public void InvalidKeyLength_ThrowsOnConstruction()
    {
        var shortKey = Convert.ToBase64String(new byte[16]); // 128-bit, not 256-bit

        Assert.Throws<InvalidOperationException>(() => MakeService(shortKey));
    }
}
