namespace Evolith.Ums.Domain.Identity.UserAccount.MfaEnrollment;

public sealed class MfaEnrollment : Entity<MfaEnrollment, MfaEnrollmentProps>
{
    private MfaEnrollment(MfaEnrollmentProps props) : base(props)
    {
    }

    public UserAccountId UserAccountId => Props.UserAccountId;
    public MfaMethod Method => Props.Method;
    public MfaEnrollmentStatus Status => Props.Status;

    public MfaEnrollmentId GetId() => MfaEnrollmentId.Load(Props.Id.GetValue());

    public static Result<MfaEnrollment> Create(
        UserAccountId userAccountId,
        MfaMethod method,
        ActorId createdBy)
    {
        var props = new MfaEnrollmentProps(IdValueObject.Create(), userAccountId, method, createdBy);
        var enrollment = new MfaEnrollment(props);

        if (!enrollment.IsValid())
        {
            return Result<MfaEnrollment>.Failure(enrollment.BrokenRules.GetBrokenRulesAsString());
        }

        return Result<MfaEnrollment>.Success(enrollment);
    }

    public Result Verify(ActorId updatedBy)
    {
        if (Props.Status == MfaEnrollmentStatus.Verified)
        {
            return Result.Failure(DomainErrors.UserAccount.MfaAlreadyVerified);
        }

        SetProps(Props.WithStatus(MfaEnrollmentStatus.Verified));
        Props.Audit.Update(updatedBy.GetValue());
        return Result.Success();
    }
}
