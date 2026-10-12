namespace Evolith.Ums.Domain.Enums;

public class DnsVerificationStatus : DomainEnumeration
{
    public static readonly DnsVerificationStatus Pending = new(1, nameof(Pending));
    public static readonly DnsVerificationStatus Verified = new(2, nameof(Verified));
    public static readonly DnsVerificationStatus Failed = new(3, nameof(Failed));

    private DnsVerificationStatus(int id, string name) : base(id, name) { }
}
