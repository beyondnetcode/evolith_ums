namespace Evolith.Ums.Domain.Identity.TenantSignupRequest;

public class TenantSignupRequestProps : IProps
{
    public IdValueObject Id { get; private set; }
    public Name CompanyName { get; private set; }
    public CompanyReference CompanyReference { get; private set; }
    public Name ContactName { get; private set; }
    public Email ContactEmail { get; private set; }
    public TenantSignupRequestStatus Status { get; private set; }
    public TenantId? ApprovedTenantId { get; private set; }
    public AuditValueObject Audit { get; private set; }

    public TenantSignupRequestProps(
        IdValueObject id,
        Name companyName,
        CompanyReference companyReference,
        Name contactName,
        Email contactEmail,
        ActorId createdBy)
    {
        Id = id;
        CompanyName = companyName;
        CompanyReference = companyReference;
        ContactName = contactName;
        ContactEmail = contactEmail;
        Status = TenantSignupRequestStatus.Pending;
        Audit = AuditValueObject.Create(createdBy.GetValue());
    }

    public TenantSignupRequestProps(
        IdValueObject id,
        Name companyName,
        CompanyReference companyReference,
        Name contactName,
        Email contactEmail,
        TenantSignupRequestStatus status,
        TenantId? approvedTenantId,
        AuditValueObject audit)
    {
        Id = id;
        CompanyName = companyName;
        CompanyReference = companyReference;
        ContactName = contactName;
        ContactEmail = contactEmail;
        Status = status;
        ApprovedTenantId = approvedTenantId;
        Audit = audit;
    }

    public TenantSignupRequestProps WithStatus(TenantSignupRequestStatus status)
    {
        var clone = (TenantSignupRequestProps)MemberwiseClone();
        clone.Status = status;
        return clone;
    }

    public TenantSignupRequestProps WithApprovedTenantId(TenantId? tenantId)
    {
        var clone = (TenantSignupRequestProps)MemberwiseClone();
        clone.ApprovedTenantId = tenantId;
        return clone;
    }

    public object Clone() => MemberwiseClone();
}
