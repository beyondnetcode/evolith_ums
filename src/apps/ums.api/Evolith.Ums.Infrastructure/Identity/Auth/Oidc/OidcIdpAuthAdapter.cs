using System.Text.Json;
using Evolith.Ums.Domain.Identity.Auth;
using Evolith.Ums.Domain.Identity.Tenant.IdentityProvider;

namespace Evolith.Ums.Infrastructure.Identity.Auth.Oidc;

/// <summary>
/// Adaptador de autenticación OIDC real (Authorization Code + PKCE) sobre el
/// contrato <see cref="IIdpAuthAdapter"/> (ADR-UMS-094 · G-049). Reemplaza al
/// <c>StubIdpAuthAdapter</c> para las estrategias OIDC/Keycloak/GenericOidc.
///
/// El contrato <see cref="IIdpAuthAdapter.ValidateAsync"/> recibe un <c>credential</c>
/// único; en la fase de callback el nivel de presentación empaqueta en él —como JSON—
/// el <see cref="OidcCallback"/> (código + artefactos de sesión state/nonce/code_verifier).
/// El adaptador entonces:
/// <list type="number">
///   <item>resuelve los endpoints OIDC del inquilino (sin hardcodear);</item>
///   <item>valida que <c>state</c> coincida con el emitido en la autorización;</item>
///   <item>intercambia el código por tokens contra el token endpoint;</item>
///   <item>valida estrictamente el <c>id_token</c> (firma JWKS, iss/aud/exp/nbf/nonce);</item>
///   <item>mapea los claims a la identidad externa UMS.</item>
/// </list>
/// La construcción de la URL de autorización vive en
/// <see cref="OidcAuthorizationRequestFactory"/>.
/// </summary>
public sealed class OidcIdpAuthAdapter : IIdpAuthAdapter
{
    private readonly IOidcProviderConfigStore _configStore;
    private readonly IOidcTokenClient _tokenClient;
    private readonly OidcIdTokenValidator _idTokenValidator;

    public OidcIdpAuthAdapter(
        IOidcProviderConfigStore configStore,
        IOidcTokenClient tokenClient,
        OidcIdTokenValidator idTokenValidator)
    {
        _configStore = configStore;
        _tokenClient = tokenClient;
        _idTokenValidator = idTokenValidator;
    }

    public async Task<Result<ExternalIdentity>> ValidateAsync(
        IdentityProvider provider,
        string credential,
        CancellationToken cancellationToken = default)
    {
        var callbackResult = ParseCallback(credential);
        if (callbackResult.IsFailure)
        {
            return Result<ExternalIdentity>.Failure(callbackResult.Error);
        }

        var callback = callbackResult.Value;

        var configResult = await _configStore.GetAsync(provider, cancellationToken);
        if (configResult.IsFailure)
        {
            return Result<ExternalIdentity>.Failure(configResult.Error);
        }

        var config = configResult.Value;

        // Defensa CSRF: el 'state' del callback debe coincidir con el emitido.
        if (!FixedTimeEquals(callback.State, callback.ExpectedState))
        {
            return Result<ExternalIdentity>.Failure(OidcAuthErrors.StateMismatch);
        }

        var tokenResult = await _tokenClient.ExchangeAuthorizationCodeAsync(
            config.Endpoints,
            config.Client,
            callback.Code,
            callback.CodeVerifier,
            callback.RedirectUri,
            cancellationToken);

        if (tokenResult.IsFailure)
        {
            return Result<ExternalIdentity>.Failure(tokenResult.Error);
        }

        var idToken = tokenResult.Value.IdToken;
        if (string.IsNullOrWhiteSpace(idToken))
        {
            return Result<ExternalIdentity>.Failure(OidcAuthErrors.MissingIdToken);
        }

        return await _idTokenValidator.ValidateAsync(
            idToken,
            config.Endpoints,
            config.Client.ClientId,
            callback.ExpectedNonce,
            cancellationToken);
    }

    private static Result<OidcCallback> ParseCallback(string credential)
    {
        if (string.IsNullOrWhiteSpace(credential))
        {
            return Result<OidcCallback>.Failure(
                OidcAuthErrors.CallbackInvalid("El 'credential' del callback está vacío."));
        }

        try
        {
            var callback = JsonSerializer.Deserialize<OidcCallback>(credential, SerializerOptions);
            if (callback is null ||
                string.IsNullOrWhiteSpace(callback.Code) ||
                string.IsNullOrWhiteSpace(callback.State) ||
                string.IsNullOrWhiteSpace(callback.CodeVerifier) ||
                string.IsNullOrWhiteSpace(callback.RedirectUri))
            {
                return Result<OidcCallback>.Failure(
                    OidcAuthErrors.CallbackInvalid("Faltan campos obligatorios en el callback OIDC."));
            }

            return Result<OidcCallback>.Success(callback);
        }
        catch (JsonException)
        {
            return Result<OidcCallback>.Failure(
                OidcAuthErrors.CallbackInvalid("El 'credential' no es un callback OIDC JSON válido."));
        }
    }

    private static bool FixedTimeEquals(string a, string b)
    {
        var ba = System.Text.Encoding.UTF8.GetBytes(a);
        var bb = System.Text.Encoding.UTF8.GetBytes(b);
        return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(ba, bb);
    }

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };
}
