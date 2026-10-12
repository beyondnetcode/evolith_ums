namespace Evolith.Ums.Application.Test.Identity.Auth.Oidc;

using System.Security.Cryptography;
using System.Text;
using Xunit;
using Evolith.Ums.Infrastructure.Identity.Auth.Oidc;

/// <summary>
/// Unit tests de la construcción de la URL de autorización (Authorization Code + PKCE
/// S256) y de las primitivas PKCE (ADR-UMS-094).
/// </summary>
public sealed class OidcAuthorizationRequestFactoryTests
{
    private static OidcProviderConfig Config() =>
        new(
            OidcTestSupport.Endpoints(),
            new OidcClientSettings(
                ClientId: "ums-client",
                ClientSecret: "secreto",
                RedirectUri: "https://ums.test/callback",
                Scopes: "openid email profile"));

    [Fact]
    public void Build_ProducesAuthorizationCodePkceUrlWithAllRequiredParameters()
    {
        var config = Config();

        var request = new OidcAuthorizationRequestFactory().Build(config);

        var uri   = new Uri(request.AuthorizationUrl);
        var query = ParseQuery(uri.Query);

        Assert.StartsWith(config.Endpoints.AuthorizationEndpoint, request.AuthorizationUrl);
        Assert.Equal("code", query["response_type"]);
        Assert.Equal("ums-client", query["client_id"]);
        Assert.Equal("https://ums.test/callback", query["redirect_uri"]);
        Assert.Equal("openid email profile", query["scope"]);
        Assert.Equal("S256", query["code_challenge_method"]);
        Assert.Equal(request.State, query["state"]);
        Assert.Equal(request.Nonce, query["nonce"]);
        Assert.Equal(request.CodeChallenge, query["code_challenge"]);
    }

    [Fact]
    public void Build_CodeChallengeIsS256OfCodeVerifier()
    {
        var request = new OidcAuthorizationRequestFactory().Build(Config());

        var expected = OidcPkce.ComputeS256Challenge(request.CodeVerifier);

        Assert.Equal(expected, request.CodeChallenge);
        // El code_verifier nunca viaja en la URL de autorización (sólo el challenge).
        Assert.DoesNotContain(request.CodeVerifier, request.AuthorizationUrl);
    }

    [Fact]
    public void Build_GeneratesFreshStateNonceVerifierEachTime()
    {
        var factory = new OidcAuthorizationRequestFactory();

        var a = factory.Build(Config());
        var b = factory.Build(Config());

        Assert.NotEqual(a.State, b.State);
        Assert.NotEqual(a.Nonce, b.Nonce);
        Assert.NotEqual(a.CodeVerifier, b.CodeVerifier);
    }

    [Fact]
    public void ComputeS256Challenge_MatchesManualSha256Base64Url()
    {
        const string verifier = "dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk";

        var challenge = OidcPkce.ComputeS256Challenge(verifier);

        var expected = OidcTestSupport.Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        Assert.Equal(expected, challenge);
    }

    [Fact]
    public void Build_WithLoginHint_IncludesLoginHint()
    {
        var request = new OidcAuthorizationRequestFactory().Build(Config(), loginHint: "user@ransa.pe");

        var query = ParseQuery(new Uri(request.AuthorizationUrl).Query);
        Assert.Equal("user@ransa.pe", query["login_hint"]);
    }

    private static Dictionary<string, string> ParseQuery(string query)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var idx = pair.IndexOf('=');
            var key = idx >= 0 ? pair[..idx] : pair;
            var val = idx >= 0 ? pair[(idx + 1)..] : string.Empty;
            result[Uri.UnescapeDataString(key)] = Uri.UnescapeDataString(val);
        }

        return result;
    }
}
