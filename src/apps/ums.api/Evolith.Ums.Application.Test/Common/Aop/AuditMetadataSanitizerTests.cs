using System.Text.Json;
using Evolith.Ums.Application.Common.Aop;
using FluentAssertions;
using Xunit;

namespace Evolith.Ums.Application.Test.Common.Aop;

/// <summary>
/// G-040 (residual #5, FR-072): el saneador de metadata de auditoría redacta los valores de las
/// claves sensibles (hash/PIN/llave/token/secreto/credencial) antes de persistir en una traza
/// append-only e inmutable (G-081), conservando la clave y sin tocar los datos legítimos.
/// </summary>
public sealed class AuditMetadataSanitizerTests
{
    private const string Redacted = AuditMetadataSanitizer.RedactionPlaceholder;

    // ── Nula / vacía / en blanco → sin fallo, se devuelve tal cual ──
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NullOrBlankMetadata_IsReturnedUnchanged(string? metadata)
    {
        AuditMetadataSanitizer.Sanitize(metadata).Should().Be(metadata);
    }

    // ── Clave sensible → valor redactado (conservando la clave) ──
    [Theory]
    [InlineData("password")]
    [InlineData("Password")]
    [InlineData("PasswordHash")]
    [InlineData("pwd")]
    [InlineData("userPwd")]
    [InlineData("hash")]
    [InlineData("passwordHash")]
    [InlineData("secret")]
    [InlineData("clientSecret")]
    [InlineData("token")]
    [InlineData("accessToken")]
    [InlineData("refreshToken")]
    [InlineData("Authorization")]
    [InlineData("apiKey")]
    [InlineData("api_key")]
    [InlineData("apikey")]
    [InlineData("privateKey")]
    [InlineData("signingKey")]
    [InlineData("pin")]
    [InlineData("userPin")]
    [InlineData("credential")]
    [InlineData("credentials")]
    [InlineData("authHeader")]
    public void SensitiveKey_ValueIsRedacted_KeyPreserved(string sensitiveKey)
    {
        var input = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            [sensitiveKey] = "el-secreto-en-claro",
        });

        var result = AuditMetadataSanitizer.Sanitize(input);

        using var doc = JsonDocument.Parse(result!);
        doc.RootElement.TryGetProperty(sensitiveKey, out var value).Should().BeTrue("la clave se conserva para trazabilidad");
        value.GetString().Should().Be(Redacted);
        result.Should().NotContain("el-secreto-en-claro");
    }

    // ── Clave normal → intacta (no se redacta ni se pierde) ──
    [Theory]
    [InlineData("handler")]
    [InlineData("Method")]
    [InlineData("requestType")]
    [InlineData("sessionTrackingId")]
    [InlineData("correlationId")]
    [InlineData("traceId")]
    [InlineData("spanId")]
    [InlineData("userName")]
    [InlineData("shippingCost")] // contiene «pin» como subcadena, pero no como token → no se redacta
    [InlineData("monkey")]       // contiene «key» como subcadena, pero no como token → no se redacta
    [InlineData("keyboardLayout")] // «key» pegado a más letras → no es token «key»
    public void NormalKey_ValueIsPreserved(string normalKey)
    {
        var input = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            [normalKey] = "valor-legitimo",
        });

        var result = AuditMetadataSanitizer.Sanitize(input);

        using var doc = JsonDocument.Parse(result!);
        doc.RootElement.GetProperty(normalKey).GetString().Should().Be("valor-legitimo");
        result.Should().NotContain(Redacted);
    }

    // ── Mezcla: solo lo sensible se redacta; lo legítimo permanece ──
    [Fact]
    public void MixedMetadata_RedactsOnlySensitiveValues()
    {
        var input = JsonSerializer.Serialize(new
        {
            Handler = "CreateUserHandler",
            SessionTrackingId = "sess-123",
            Password = "hunter2",
            ApiKey = "sk-live-abcdef",
        });

        var result = AuditMetadataSanitizer.Sanitize(input);

        using var doc = JsonDocument.Parse(result!);
        var root = doc.RootElement;
        root.GetProperty("Handler").GetString().Should().Be("CreateUserHandler");
        root.GetProperty("SessionTrackingId").GetString().Should().Be("sess-123");
        root.GetProperty("Password").GetString().Should().Be(Redacted);
        root.GetProperty("ApiKey").GetString().Should().Be(Redacted);
        result.Should().NotContain("hunter2").And.NotContain("sk-live-abcdef");
    }

    // ── Clave sensible en objeto anidado → se redacta en profundidad ──
    [Fact]
    public void NestedSensitiveKey_IsRedacted()
    {
        var input = "{\"outer\":{\"innerToken\":\"leak-me\",\"safe\":\"keep-me\"}}";

        var result = AuditMetadataSanitizer.Sanitize(input);

        using var doc = JsonDocument.Parse(result!);
        var outer = doc.RootElement.GetProperty("outer");
        outer.GetProperty("innerToken").GetString().Should().Be(Redacted);
        outer.GetProperty("safe").GetString().Should().Be("keep-me");
        result.Should().NotContain("leak-me");
    }

    // ── Subárbol bajo clave sensible → se redacta entero, sin descender ──
    [Fact]
    public void SensitiveKeyOverObject_RedactsWholeSubtree()
    {
        var input = "{\"credentials\":{\"user\":\"admin\",\"pwd\":\"1234\"}}";

        var result = AuditMetadataSanitizer.Sanitize(input);

        using var doc = JsonDocument.Parse(result!);
        doc.RootElement.GetProperty("credentials").ValueKind.Should().Be(JsonValueKind.String);
        doc.RootElement.GetProperty("credentials").GetString().Should().Be(Redacted);
        result.Should().NotContain("admin").And.NotContain("1234");
    }

    // ── Arreglo de objetos con claves sensibles → cada elemento se sanea ──
    [Fact]
    public void ArrayOfObjects_SanitizesEachElement()
    {
        var input = "{\"items\":[{\"name\":\"a\",\"secret\":\"s1\"},{\"name\":\"b\",\"secret\":\"s2\"}]}";

        var result = AuditMetadataSanitizer.Sanitize(input);

        using var doc = JsonDocument.Parse(result!);
        var items = doc.RootElement.GetProperty("items");
        items[0].GetProperty("name").GetString().Should().Be("a");
        items[0].GetProperty("secret").GetString().Should().Be(Redacted);
        items[1].GetProperty("secret").GetString().Should().Be(Redacted);
        result.Should().NotContain("s1").And.NotContain("s2");
    }

    // ── JSON vacío o sin claves sensibles → equivalente al de entrada ──
    [Fact]
    public void EmptyJsonObject_IsReturnedAsEmptyObject()
    {
        var result = AuditMetadataSanitizer.Sanitize("{}");
        using var doc = JsonDocument.Parse(result!);
        doc.RootElement.ValueKind.Should().Be(JsonValueKind.Object);
        doc.RootElement.EnumerateObject().Should().BeEmpty();
    }

    // ── No-JSON → sin fallo, devuelto tal cual (ambas vías reales garantizan JSON válido) ──
    [Fact]
    public void NonJsonMetadata_IsReturnedUnchanged()
    {
        const string notJson = "esto no es json";
        AuditMetadataSanitizer.Sanitize(notJson).Should().Be(notJson);
    }

    // ── Literal JSON null → sin fallo ──
    [Fact]
    public void JsonNullLiteral_IsReturnedUnchanged()
    {
        AuditMetadataSanitizer.Sanitize("null").Should().Be("null");
    }

    // ── IsSensitiveKey: contrato explícito de la lista de patrones ──
    [Theory]
    [InlineData("password", true)]
    [InlineData("PASSWORD", true)]
    [InlineData("passwordHash", true)]
    [InlineData("pwd", true)]
    [InlineData("api_key", true)]
    [InlineData("apiKey", true)]
    [InlineData("bearerToken", true)]
    [InlineData("pin", true)]
    [InlineData("key", true)]
    [InlineData("cred", true)]
    [InlineData("authorization", true)]
    [InlineData("handler", false)]
    [InlineData("correlationId", false)]
    [InlineData("shipping", false)]
    [InlineData("monkey", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsSensitiveKey_MatchesPatternList(string? key, bool expected)
    {
        AuditMetadataSanitizer.IsSensitiveKey(key).Should().Be(expected);
    }
}
