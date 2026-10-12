namespace Evolith.Ums.Application.Test.Identity.Auth.Oidc;

using Evolith.Ums.Infrastructure.Identity.Auth.Oidc;
using Xunit;

/// <summary>
/// Unit tests del parseo de la configuración OIDC desde el <c>ConfigPayload</c> del
/// inquilino: los endpoints se leen de configuración, nunca se hardcodean (ADR-UMS-094).
/// </summary>
public sealed class OidcProviderConfigParserTests
{
    [Fact]
    public void Parse_FullDiscoveryPayload_ReadsAllEndpoints()
    {
        const string payload = """
        {
          "issuer": "https://idp.test/realms/evolith",
          "authorization_endpoint": "https://idp.test/auth",
          "token_endpoint": "https://idp.test/token",
          "jwks_uri": "https://idp.test/certs",
          "client_id": "ums-client",
          "client_secret": "s3cr3t",
          "redirect_uri": "https://ums.test/callback",
          "scope": "openid email"
        }
        """;

        var result = OidcProviderConfigParser.Parse(payload);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error : null);
        Assert.Equal("https://idp.test/auth", result.Value.Endpoints.AuthorizationEndpoint);
        Assert.Equal("https://idp.test/token", result.Value.Endpoints.TokenEndpoint);
        Assert.Equal("https://idp.test/certs", result.Value.Endpoints.JwksUri);
        Assert.Equal("https://idp.test/realms/evolith", result.Value.Endpoints.Issuer);
        Assert.Equal("ums-client", result.Value.Client.ClientId);
        Assert.Equal("s3cr3t", result.Value.Client.ClientSecret);
        Assert.Equal("openid email", result.Value.Client.Scopes);
    }

    [Fact]
    public void Parse_IssuerOnly_DerivesKeycloakEndpoints()
    {
        const string payload = """
        {
          "issuer": "https://idp.test/realms/evolith",
          "clientId": "ums-client",
          "redirectUri": "https://ums.test/callback"
        }
        """;

        var result = OidcProviderConfigParser.Parse(payload);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error : null);
        Assert.Equal("https://idp.test/realms/evolith/protocol/openid-connect/auth", result.Value.Endpoints.AuthorizationEndpoint);
        Assert.Equal("https://idp.test/realms/evolith/protocol/openid-connect/token", result.Value.Endpoints.TokenEndpoint);
        Assert.Equal("https://idp.test/realms/evolith/protocol/openid-connect/certs", result.Value.Endpoints.JwksUri);
        Assert.Equal("openid email profile", result.Value.Client.Scopes); // por defecto
    }

    [Fact]
    public void Parse_ExplicitClientSecretArgument_OverridesPayload()
    {
        const string payload = """
        { "issuer": "https://idp.test/realms/evolith", "clientId": "ums-client", "redirectUri": "https://ums.test/cb" }
        """;

        var result = OidcProviderConfigParser.Parse(payload, clientSecret: "desde-vault");

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error : null);
        Assert.Equal("desde-vault", result.Value.Client.ClientSecret);
    }

    [Fact]
    public void Parse_MissingRequiredKeys_ReturnsFailure_AUTH020()
    {
        const string payload = """{ "issuer": "https://idp.test/realms/evolith" }""";

        var result = OidcProviderConfigParser.Parse(payload);

        Assert.True(result.IsFailure);
        Assert.Contains("AUTH_020", result.Error);
        Assert.Contains("client_id", result.Error);
        Assert.Contains("redirect_uri", result.Error);
    }

    [Fact]
    public void Parse_InvalidJson_ReturnsFailure_AUTH020()
    {
        var result = OidcProviderConfigParser.Parse("no-es-json");

        Assert.True(result.IsFailure);
        Assert.Contains("AUTH_020", result.Error);
    }

    [Fact]
    public void Parse_EmptyPayload_ReturnsFailure_AUTH020()
    {
        var result = OidcProviderConfigParser.Parse("   ");

        Assert.True(result.IsFailure);
        Assert.Contains("AUTH_020", result.Error);
    }
}
