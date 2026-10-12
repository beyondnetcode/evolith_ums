using System.Text.Json;
using Evolith.Ums.Application.Configuration.IdpConfiguration.DTOs;

namespace Evolith.Ums.Infrastructure.Configuration.IdpResolution;

internal abstract class JsonPayloadIdpResolutionStrategyBase : IIdpResolutionStrategy
{
    public ResolvedIdpConfigurationDto Resolve(IdpResolutionContext context)
    {
        var props = context.Configuration.Props;
        using var payload = TryParse(props.ConfigPayload);

        return new ResolvedIdpConfigurationDto(
            props.Id.GetValue(),
            props.TenantId.GetValue(),
            props.SystemSuiteId.GetValue(),
            props.ProviderType.Name,
            Protocol,
            ResolveAuthority(payload),
            ResolveClientId(payload),
            ResolveMetadataAddress(payload),
            props.DomainHints,
            context.DomainMatched,
            props.ResolutionPriority,
            props.FallbackToId,
            props.Version,
            props.SecretRef);
    }

    protected abstract string Protocol { get; }

    protected virtual string? ResolveAuthority(JsonDocument? payload)
        => ReadFirst(payload, "authority", "issuer", "serverUrl", "baseUrl");

    protected virtual string? ResolveClientId(JsonDocument? payload)
        => ReadFirst(payload, "clientId", "applicationId");

    protected virtual string? ResolveMetadataAddress(JsonDocument? payload)
        => ReadFirst(payload, "metadataAddress", "metadataUrl", "discoveryDocument");

    protected static string? ReadFirst(JsonDocument? payload, params string[] names)
    {
        if (payload is null)
        {
            return null;
        }

        foreach (var name in names)
        {
            if (payload.RootElement.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
            {
                return value.GetString();
            }
        }

        return null;
    }

    private static JsonDocument? TryParse(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return null;
        }

        try
        {
            return JsonDocument.Parse(payload);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

internal sealed class InternalBcryptIdpResolutionStrategy : JsonPayloadIdpResolutionStrategyBase
{
    protected override string Protocol => "LOCAL";
}

internal sealed class ZitadelIdpResolutionStrategy : JsonPayloadIdpResolutionStrategyBase
{
    protected override string Protocol => "OIDC";
}

internal sealed class AzureAdIdpResolutionStrategy : JsonPayloadIdpResolutionStrategyBase
{
    protected override string Protocol => "OIDC";
}

internal sealed class OktaIdpResolutionStrategy : JsonPayloadIdpResolutionStrategyBase
{
    protected override string Protocol => "OIDC";
}

internal sealed class KeycloakIdpResolutionStrategy : JsonPayloadIdpResolutionStrategyBase
{
    protected override string Protocol => "OIDC";
}

internal sealed class Auth0IdpResolutionStrategy : JsonPayloadIdpResolutionStrategyBase
{
    protected override string Protocol => "OIDC";
}

internal sealed class GoogleIdpResolutionStrategy : JsonPayloadIdpResolutionStrategyBase
{
    protected override string Protocol => "OIDC";
}

internal sealed class LdapIdpResolutionStrategy : JsonPayloadIdpResolutionStrategyBase
{
    protected override string Protocol => "LDAP";

    protected override string? ResolveMetadataAddress(JsonDocument? payload) => null;
}

internal sealed class Saml2IdpResolutionStrategy : JsonPayloadIdpResolutionStrategyBase
{
    protected override string Protocol => "SAML2";

    protected override string? ResolveClientId(JsonDocument? payload)
        => ReadFirst(payload, "entityId", "clientId");
}

internal sealed class GenericOidcIdpResolutionStrategy : JsonPayloadIdpResolutionStrategyBase
{
    protected override string Protocol => "OIDC";
}
