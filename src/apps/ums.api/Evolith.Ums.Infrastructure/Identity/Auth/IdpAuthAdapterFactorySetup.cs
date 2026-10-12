using BeyondNetCode.Shell.Factory.Impl;
using Evolith.Ums.Application.Identity.Auth;
using Evolith.Ums.Domain.Enums;
using Evolith.Ums.Domain.Identity.Auth;
using Evolith.Ums.Infrastructure.Identity.Auth.Oidc;

namespace Evolith.Ums.Infrastructure.Identity.Auth;

/// <summary>
/// Shell.Factory registration for IIdpAuthAdapter — Production configuration.
///
/// Routes each IDP strategy name to its concrete adapter.
/// Each .When() condition matches the exact <c>IdpStrategy.Name</c> value so the
/// factory discriminates correctly when the dispatcher passes
/// <c>new IdpAuthAdapterCriteria(provider.Props.Strategy.Name)</c>.
///
/// Pattern: identical to IdpResolutionStrategyFactorySetup.
///
/// ADR-UMS-094 / G-049 (slice 1): el <see cref="OidcIdpAuthAdapter"/> real (Authorization
/// Code + PKCE, validación estricta del id_token) atiende las estrategias OIDC —
/// Keycloak, GenericOidc y la familia OIDC (AzureAd/Okta/Zitadel/Auth0/Google). Nunca
/// se usa el stub en producción. Las estrategias no-OIDC (SAML2/LDAP) siguen sin
/// adaptador → AUTH_012 hasta implementarse sobre el mismo contrato.
/// </summary>
internal sealed class IdpAuthAdapterFactorySetup : AbstractFactorySetupSource
{
    public IdpAuthAdapterFactorySetup()
    {
        For<IdpAuthAdapterCriteria, IIdpAuthAdapter>()
            .Create<OidcIdpAuthAdapter>()
            .When(x => x.StrategyName == IdpStrategy.Keycloak.Name);

        For<IdpAuthAdapterCriteria, IIdpAuthAdapter>()
            .Create<OidcIdpAuthAdapter>()
            .When(x => x.StrategyName == IdpStrategy.GenericOidc.Name);

        For<IdpAuthAdapterCriteria, IIdpAuthAdapter>()
            .Create<OidcIdpAuthAdapter>()
            .When(x => x.StrategyName == IdpStrategy.AzureAd.Name);

        For<IdpAuthAdapterCriteria, IIdpAuthAdapter>()
            .Create<OidcIdpAuthAdapter>()
            .When(x => x.StrategyName == IdpStrategy.Okta.Name);

        For<IdpAuthAdapterCriteria, IIdpAuthAdapter>()
            .Create<OidcIdpAuthAdapter>()
            .When(x => x.StrategyName == IdpStrategy.Zitadel.Name);

        For<IdpAuthAdapterCriteria, IIdpAuthAdapter>()
            .Create<OidcIdpAuthAdapter>()
            .When(x => x.StrategyName == IdpStrategy.Auth0.Name);

        For<IdpAuthAdapterCriteria, IIdpAuthAdapter>()
            .Create<OidcIdpAuthAdapter>()
            .When(x => x.StrategyName == IdpStrategy.Google.Name);
    }
}

/// <summary>
/// Shell.Factory registration for IIdpAuthAdapter — Development / Test override.
///
/// Registers <see cref="StubIdpAuthAdapter"/> for every known strategy so the
/// full IDP auth flow can be tested end-to-end with MOCK-* credentials.
/// This setup is registered ONLY in non-Production environments (see DependencyInjection.cs).
///
/// The stub succeeds only when credentials start with "MOCK-", so even if accidentally
/// loaded in Production it will not authenticate real credentials.
/// </summary>
internal sealed class IdpAuthAdapterStubFactorySetup : AbstractFactorySetupSource
{
    public IdpAuthAdapterStubFactorySetup()
    {
        // One entry per strategy so routing still works correctly — the stub is chosen
        // per strategy name, not via a catch-all that hides factory routing bugs.
        For<IdpAuthAdapterCriteria, IIdpAuthAdapter>()
            .Create<StubIdpAuthAdapter>()
            .When(x => x.StrategyName == IdpStrategy.AzureAd.Name);

        For<IdpAuthAdapterCriteria, IIdpAuthAdapter>()
            .Create<StubIdpAuthAdapter>()
            .When(x => x.StrategyName == IdpStrategy.Okta.Name);

        For<IdpAuthAdapterCriteria, IIdpAuthAdapter>()
            .Create<StubIdpAuthAdapter>()
            .When(x => x.StrategyName == IdpStrategy.Zitadel.Name);

        For<IdpAuthAdapterCriteria, IIdpAuthAdapter>()
            .Create<StubIdpAuthAdapter>()
            .When(x => x.StrategyName == IdpStrategy.Keycloak.Name);

        For<IdpAuthAdapterCriteria, IIdpAuthAdapter>()
            .Create<StubIdpAuthAdapter>()
            .When(x => x.StrategyName == IdpStrategy.Auth0.Name);

        For<IdpAuthAdapterCriteria, IIdpAuthAdapter>()
            .Create<StubIdpAuthAdapter>()
            .When(x => x.StrategyName == IdpStrategy.Google.Name);

        For<IdpAuthAdapterCriteria, IIdpAuthAdapter>()
            .Create<StubIdpAuthAdapter>()
            .When(x => x.StrategyName == IdpStrategy.Ldap.Name);

        For<IdpAuthAdapterCriteria, IIdpAuthAdapter>()
            .Create<StubIdpAuthAdapter>()
            .When(x => x.StrategyName == IdpStrategy.Saml2.Name);

        For<IdpAuthAdapterCriteria, IIdpAuthAdapter>()
            .Create<StubIdpAuthAdapter>()
            .When(x => x.StrategyName == IdpStrategy.GenericOidc.Name);
    }
}
