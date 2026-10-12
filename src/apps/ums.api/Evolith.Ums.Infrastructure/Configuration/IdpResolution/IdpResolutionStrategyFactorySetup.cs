using BeyondNetCode.Shell.Factory.Impl;
using Evolith.Ums.Domain.Enums;

namespace Evolith.Ums.Infrastructure.Configuration.IdpResolution;

internal sealed class IdpResolutionStrategyFactorySetup : AbstractFactorySetupSource
{
    public IdpResolutionStrategyFactorySetup()
    {
        For<IdpResolutionStrategyCriteria, IIdpResolutionStrategy>().Create<InternalBcryptIdpResolutionStrategy>().When(x => x.ProviderType == ProviderType.InternalBcrypt.Name);
        For<IdpResolutionStrategyCriteria, IIdpResolutionStrategy>().Create<ZitadelIdpResolutionStrategy>().When(x => x.ProviderType == ProviderType.Zitadel.Name);
        For<IdpResolutionStrategyCriteria, IIdpResolutionStrategy>().Create<AzureAdIdpResolutionStrategy>().When(x => x.ProviderType == ProviderType.AzureAd.Name);
        For<IdpResolutionStrategyCriteria, IIdpResolutionStrategy>().Create<OktaIdpResolutionStrategy>().When(x => x.ProviderType == ProviderType.Okta.Name);
        For<IdpResolutionStrategyCriteria, IIdpResolutionStrategy>().Create<KeycloakIdpResolutionStrategy>().When(x => x.ProviderType == ProviderType.Keycloak.Name);
        For<IdpResolutionStrategyCriteria, IIdpResolutionStrategy>().Create<Auth0IdpResolutionStrategy>().When(x => x.ProviderType == ProviderType.Auth0.Name);
        For<IdpResolutionStrategyCriteria, IIdpResolutionStrategy>().Create<GoogleIdpResolutionStrategy>().When(x => x.ProviderType == ProviderType.Google.Name);
        For<IdpResolutionStrategyCriteria, IIdpResolutionStrategy>().Create<LdapIdpResolutionStrategy>().When(x => x.ProviderType == ProviderType.Ldap.Name);
        For<IdpResolutionStrategyCriteria, IIdpResolutionStrategy>().Create<Saml2IdpResolutionStrategy>().When(x => x.ProviderType == ProviderType.Saml2.Name);
        For<IdpResolutionStrategyCriteria, IIdpResolutionStrategy>().Create<GenericOidcIdpResolutionStrategy>().When(x => x.ProviderType == ProviderType.GenericOidc.Name);
    }
}
