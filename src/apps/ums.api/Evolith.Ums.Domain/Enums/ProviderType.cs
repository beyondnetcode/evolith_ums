namespace Evolith.Ums.Domain.Enums;

public class ProviderType : DomainEnumeration
{
    public static readonly ProviderType InternalBcrypt = new(1, "INTERNAL_BCRYPT");
    public static readonly ProviderType Zitadel       = new(2, "ZITADEL");
    public static readonly ProviderType AzureAd       = new(3, "AZURE_AD");
    public static readonly ProviderType Okta          = new(4, "OKTA");
    public static readonly ProviderType Keycloak      = new(5, "KEYCLOAK");
    public static readonly ProviderType Auth0         = new(6, "AUTH0");
    public static readonly ProviderType Google        = new(7, "GOOGLE");
    public static readonly ProviderType Ldap          = new(8, "LDAP");
    public static readonly ProviderType Saml2         = new(9, "SAML2");
    public static readonly ProviderType GenericOidc   = new(10, "GENERIC_OIDC");

    private ProviderType(int id, string name) : base(id, name) { }
}
