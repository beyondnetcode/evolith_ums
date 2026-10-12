using System.Text.Json.Serialization;

namespace Evolith.Ums.Sdk.Client;

/// <summary>Body sent to <c>POST /api/v1/client/authenticate</c>.</summary>
public sealed record ClientAuthRequest(
    [property: JsonPropertyName("tenantCode")] string TenantCode,
    [property: JsonPropertyName("username")] string Username,
    [property: JsonPropertyName("password")] string Password,
    [property: JsonPropertyName("format")] string? Format = "JSON");
