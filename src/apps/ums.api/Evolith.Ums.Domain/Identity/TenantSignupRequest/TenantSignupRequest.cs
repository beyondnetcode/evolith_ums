namespace Evolith.Ums.Domain.Identity.TenantSignupRequest;

public sealed class TenantSignupRequest : AggregateRoot<TenantSignupRequest, TenantSignupRequestProps>
{
    public TenantSignupRequest(TenantSignupRequestProps props) : base(props) { }

    public IdValueObject GetId() => Props.Id;
    public Name CompanyName => Props.CompanyName;
    public CompanyReference CompanyReference => Props.CompanyReference;
    public Name ContactName => Props.ContactName;
    public Email ContactEmail => Props.ContactEmail;
    public TenantSignupRequestStatus Status => Props.Status;
    public TenantId? ApprovedTenantId => Props.ApprovedTenantId;

    public static Result<TenantSignupRequest> Create(
        Name companyName,
        CompanyReference companyReference,
        Name contactName,
        Email contactEmail,
        ActorId createdBy,
        IdValueObject? signupRequestId = null)
    {
        var id = signupRequestId ?? IdValueObject.Create();
        var props = new TenantSignupRequestProps(id, companyName, companyReference, contactName, contactEmail, createdBy);
        var request = new TenantSignupRequest(props);

        if (!request.IsValid())
        {
            return Result<TenantSignupRequest>.Failure(request.BrokenRules.GetBrokenRulesAsString());
        }

        return Result<TenantSignupRequest>.Success(request);
    }

    public Result Approve(TenantId tenantId, ActorId updatedBy)
    {
        if (Status != TenantSignupRequestStatus.Pending)
        {
            BrokenRules.Add(new BrokenRule(nameof(Status), DomainErrors.Tenant.SignupRequestNotPending));
        }

        if (!IsValid())
        {
            return Result.Failure(BrokenRules.GetBrokenRulesAsString());
        }

        SetProps(Props.WithStatus(TenantSignupRequestStatus.Approved).WithApprovedTenantId(tenantId));
        TrackingState.MarkAsDirty();
        Props.Audit.Update(updatedBy.GetValue());
        return Result.Success();
    }
}
