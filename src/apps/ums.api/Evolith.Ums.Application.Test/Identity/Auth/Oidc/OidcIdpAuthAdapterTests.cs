namespace Evolith.Ums.Application.Test.Identity.Auth.Oidc;

using System.Security.Cryptography;
using System.Text.Json;
using Evolith.Ums.Domain.Identity.Tenant.IdentityProvider;
using Evolith.Ums.Infrastructure.Identity.Auth.Oidc;
using Xunit;

/// <summary>
/// Unit tests de la orquestación del <see cref="OidcIdpAuthAdapter"/> (Authorization
/// Code + PKCE): resolución de config → validación de state → intercambio de código →
/// validación del id_token. Todos los puertos (config/token/JWKS) son fakes; ningún
/// IdP real interviene (ADR-UMS-094 · slice 1).
/// </summary>
public sealed class OidcIdpAuthAdapterTests
{
    private const string Kid = "adapter-key";
    private const string ClientId = "ums-client";
    private const string Nonce = "nonce-abc";
    private const string State = "state-xyz";

    private readonly RSA _rsa = RSA.Create(2048);
    private readonly OidcEndpoints _endpoints = OidcTestSupport.Endpoints();

    private OidcProviderConfig BuildConfig() =>
        new(_endpoints, new OidcClientSettings(ClientId, "secreto", "https://ums.test/callback"));

    private OidcIdTokenValidator BuildValidator() =>
        new(FakeJwksProvider.WithKeys(OidcTestSupport.PublicJwk(_rsa, Kid)), TimeProvider.System);

    private static IdentityProvider BuildProvider()
        => IdentityProvider.Create(
            TenantId.Load(Guid.NewGuid()),
            Code.Create("KC"),
            Name.Create("Keycloak"),
            Description.Create("IdP corporativo"),
            IdpStrategy.Keycloak,
            ActorId.Create("test")).Value;

    private static string Callback(string state = State, string expectedState = State, string expectedNonce = Nonce)
        => JsonSerializer.Serialize(new
        {
            code = "auth-code-123",
            state,
            expectedState,
            expectedNonce,
            codeVerifier = "verifier-123",
            redirectUri = "https://ums.test/callback",
        });

    private string ValidIdToken()
        => OidcTestSupport.BuildIdToken(
            _rsa, Kid,
            OidcTestSupport.ValidPayload(_endpoints.Issuer, ClientId, Nonce));

    [Fact]
    public async Task ValidateAsync_HappyPath_ReturnsExternalIdentity()
    {
        var adapter = new OidcIdpAuthAdapter(
            FakeOidcProviderConfigStore.Returning(BuildConfig()),
            FakeOidcTokenClient.Returning(ValidIdToken()),
            BuildValidator());

        var result = await adapter.ValidateAsync(BuildProvider(), Callback());

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error : null);
        Assert.Equal("user@ransa.pe", result.Value.Email);
    }

    [Fact]
    public async Task ValidateAsync_StateMismatch_ReturnsFailure_AUTH033()
    {
        var adapter = new OidcIdpAuthAdapter(
            FakeOidcProviderConfigStore.Returning(BuildConfig()),
            FakeOidcTokenClient.Returning(ValidIdToken()),
            BuildValidator());

        var result = await adapter.ValidateAsync(BuildProvider(), Callback(state: "state-del-atacante"));

        Assert.True(result.IsFailure);
        Assert.Contains("AUTH_033", result.Error);
    }

    [Fact]
    public async Task ValidateAsync_TokenExchangeFails_ReturnsFailure_AUTH021()
    {
        var adapter = new OidcIdpAuthAdapter(
            FakeOidcProviderConfigStore.Returning(BuildConfig()),
            FakeOidcTokenClient.Failing(OidcAuthErrors.TokenExchangeFailed("HTTP 400.")),
            BuildValidator());

        var result = await adapter.ValidateAsync(BuildProvider(), Callback());

        Assert.True(result.IsFailure);
        Assert.Contains("AUTH_021", result.Error);
    }

    [Fact]
    public async Task ValidateAsync_MissingIdToken_ReturnsFailure_AUTH022()
    {
        var adapter = new OidcIdpAuthAdapter(
            FakeOidcProviderConfigStore.Returning(BuildConfig()),
            FakeOidcTokenClient.Returning(idToken: null),
            BuildValidator());

        var result = await adapter.ValidateAsync(BuildProvider(), Callback());

        Assert.True(result.IsFailure);
        Assert.Contains("AUTH_022", result.Error);
    }

    [Fact]
    public async Task ValidateAsync_ConfigResolutionFails_ReturnsFailure_AUTH020()
    {
        var adapter = new OidcIdpAuthAdapter(
            FakeOidcProviderConfigStore.Failing(OidcAuthErrors.ConfigNotFound("sin config")),
            FakeOidcTokenClient.Returning(ValidIdToken()),
            BuildValidator());

        var result = await adapter.ValidateAsync(BuildProvider(), Callback());

        Assert.True(result.IsFailure);
        Assert.Contains("AUTH_020", result.Error);
    }

    [Fact]
    public async Task ValidateAsync_MalformedCallback_ReturnsFailure_AUTH033()
    {
        var adapter = new OidcIdpAuthAdapter(
            FakeOidcProviderConfigStore.Returning(BuildConfig()),
            FakeOidcTokenClient.Returning(ValidIdToken()),
            BuildValidator());

        var result = await adapter.ValidateAsync(BuildProvider(), "esto-no-es-json");

        Assert.True(result.IsFailure);
        Assert.Contains("AUTH_033", result.Error);
    }

    [Fact]
    public async Task ValidateAsync_NonceMismatchInIdToken_ReturnsFailure_AUTH031()
    {
        // El callback declara un nonce distinto al que trae el id_token → rechazo.
        var adapter = new OidcIdpAuthAdapter(
            FakeOidcProviderConfigStore.Returning(BuildConfig()),
            FakeOidcTokenClient.Returning(ValidIdToken()),
            BuildValidator());

        var result = await adapter.ValidateAsync(
            BuildProvider(),
            Callback(expectedNonce: "otro-nonce"));

        Assert.True(result.IsFailure);
        Assert.Contains("AUTH_031", result.Error);
    }
}
