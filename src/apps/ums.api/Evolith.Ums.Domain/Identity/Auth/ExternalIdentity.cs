namespace Evolith.Ums.Domain.Identity.Auth;

/// <summary>
/// The identity asserted by an external Identity Provider after successful
/// token/credential validation. UMS maps this back to a UserAccount and
/// builds the authorization graph from the matched profile.
/// </summary>
public sealed record ExternalIdentity(
    string             Email,
    string?            ExternalId,         // subject claim from IDP token
    string?            DisplayName,
    IReadOnlyDictionary<string, string> Claims); // raw claims from the IDP token
