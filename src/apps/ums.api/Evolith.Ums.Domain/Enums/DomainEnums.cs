namespace Evolith.Ums.Domain.Enums;

public class TenantStatus : DomainEnumeration
{
    public static readonly TenantStatus Active = new(1, nameof(Active));
    public static readonly TenantStatus Suspended = new(2, nameof(Suspended));
    public static readonly TenantStatus Archived = new(3, nameof(Archived));

    private TenantStatus(int id, string name) : base(id, name) { }
}

public class TenantSignupRequestStatus : DomainEnumeration
{
    public static readonly TenantSignupRequestStatus Pending = new(1, nameof(Pending));
    public static readonly TenantSignupRequestStatus Approved = new(2, nameof(Approved));
    public static readonly TenantSignupRequestStatus Rejected = new(3, nameof(Rejected));

    private TenantSignupRequestStatus(int id, string name) : base(id, name) { }
}

public class IdpStrategy : DomainEnumeration
{
    public static readonly IdpStrategy InternalBcrypt = new(1, nameof(InternalBcrypt));
    public static readonly IdpStrategy Zitadel = new(2, nameof(Zitadel));
    public static readonly IdpStrategy AzureAd = new(3, nameof(AzureAd));
    public static readonly IdpStrategy Okta = new(4, nameof(Okta));
    public static readonly IdpStrategy Keycloak = new(5, nameof(Keycloak));
    public static readonly IdpStrategy Auth0 = new(6, nameof(Auth0));
    public static readonly IdpStrategy Google = new(7, nameof(Google));
    public static readonly IdpStrategy Ldap = new(8, nameof(Ldap));
    public static readonly IdpStrategy Saml2 = new(9, nameof(Saml2));
    public static readonly IdpStrategy GenericOidc = new(10, nameof(GenericOidc));

    private IdpStrategy(int id, string name) : base(id, name) { }
}
