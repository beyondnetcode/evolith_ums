using System.Text.Json.Serialization;

namespace Evolith.Ums.Domain.Authorization.Graph;

/// <summary>
/// Describes how the principal was authenticated: method (Local/IDP),
/// the identity provider used if any, MFA state, and session timing.
/// </summary>
public sealed record GraphAuthentication(
    string          Method,         // "Local" | "IDP"
    GraphIdpProvider? Provider,     // null when Method == "Local"
    bool            MfaRequired,
    DateTime        IssuedAt,
    DateTime        SessionExpiresAt);

public sealed record GraphIdpProvider(
    [property: JsonIgnore] Guid Id,
    string Name,
    string Code,
    [property: JsonPropertyName("value")] string Strategy);   // "AzureAd" | "Okta" | "GenericOidc" | etc.
