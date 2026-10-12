namespace Evolith.Ums.Domain.Identity.UserAccount.MfaEnrollment;

public class MfaEnrollmentProps : IProps
{
    public IdValueObject Id { get; private set; }
    public UserAccountId UserAccountId { get; private set; }
    public MfaMethod Method { get; private set; }
    public MfaEnrollmentStatus Status { get; private set; }
    public AuditValueObject Audit { get; private set; }

    public MfaEnrollmentProps(
        IdValueObject id,
        UserAccountId userAccountId,
        MfaMethod method,
        ActorId createdBy)
    {
        Id = id;
        UserAccountId = userAccountId;
        Method = method;
        Status = MfaEnrollmentStatus.Enrolled;
        Audit = AuditValueObject.Create(createdBy.GetValue());
    }

    public MfaEnrollmentProps(
        IdValueObject id,
        UserAccountId userAccountId,
        MfaMethod method,
        MfaEnrollmentStatus status,
        AuditValueObject audit)
    {
        Id = id;
        UserAccountId = userAccountId;
        Method = method;
        Status = status;
        Audit = audit;
    }

    public MfaEnrollmentProps WithStatus(MfaEnrollmentStatus status)
    {
        var clone = (MfaEnrollmentProps)MemberwiseClone();
        clone.Status = status;
        return clone;
    }

    public object Clone() => MemberwiseClone();
}