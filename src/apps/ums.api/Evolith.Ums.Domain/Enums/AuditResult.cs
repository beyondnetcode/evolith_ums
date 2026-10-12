namespace Evolith.Ums.Domain.Enums;

public class AuditResult : DomainEnumeration
{
    public static readonly AuditResult Success = new(1, nameof(Success));
    public static readonly AuditResult Failure = new(2, nameof(Failure));
    public static readonly AuditResult Partial = new(3, nameof(Partial));

    private AuditResult(int id, string name) : base(id, name) { }
}
