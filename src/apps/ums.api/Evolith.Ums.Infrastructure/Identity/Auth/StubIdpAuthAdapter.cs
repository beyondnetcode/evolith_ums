using Evolith.Ums.Domain.Identity.Auth;
using Evolith.Ums.Domain.Identity.Tenant.IdentityProvider;

namespace Evolith.Ums.Infrastructure.Identity.Auth;

/// <summary>
/// Development-only IDP adapter that accepts any credential starting with "MOCK-".
/// Never active in Production (controlled by DependencyInjection registration guard).
///
/// Usage: pass password = "MOCK-{email}" when authenticating against an IDP tenant.
/// Example: POST /client/authenticate { tenantCode: "RANSA", username: "user@ransa.pe", password: "MOCK-user@ransa.pe" }
///
/// Extracted email: if credential starts with "MOCK-", the email is the username param.
/// </summary>
public sealed class StubIdpAuthAdapter : IIdpAuthAdapter
{
    private const string MockPrefix = "MOCK-";

    public Task<Result<ExternalIdentity>> ValidateAsync(
        IdentityProvider  provider,
        string            credential,
        CancellationToken cancellationToken = default)
    {
        if (!credential.StartsWith(MockPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(
                Result<ExternalIdentity>.Failure(
                    $"AUTH_013: Stub IDP only accepts credentials starting with '{MockPrefix}'. " +
                    "Configure a real IDP adapter for production."));
        }

        // Extract email from credential (MOCK-email@domain.com or just MOCK-)
        var emailPart = credential.Length > MockPrefix.Length
            ? credential[MockPrefix.Length..]
            : "stub@mock.local";

        var identity = new ExternalIdentity(
            Email:       emailPart,
            ExternalId:  $"stub-{Guid.NewGuid():N}",
            DisplayName: emailPart.Split('@')[0],
            Claims:      new Dictionary<string, string>
            {
                ["sub"]           = $"stub|{emailPart}",
                ["email"]         = emailPart,
                ["email_verified"] = "true",
                ["iss"]           = "stub-idp",
                ["aud"]           = provider.Props.Code.GetValue(),
            });

        return Task.FromResult(Result<ExternalIdentity>.Success(identity));
    }
}
