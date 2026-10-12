namespace Evolith.Ums.Domain.Enums;

public class IdentityReferenceType : DomainEnumeration
{
    public static readonly IdentityReferenceType HrId = new(1, nameof(HrId));
    public static readonly IdentityReferenceType VendorCode = new(2, nameof(VendorCode));
    public static readonly IdentityReferenceType GovernmentId = new(3, nameof(GovernmentId));
    public static readonly IdentityReferenceType PartnerRef = new(4, nameof(PartnerRef));

    private IdentityReferenceType(int id, string name) : base(id, name) { }
}
