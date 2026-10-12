namespace Evolith.Ums.Domain.Enums;

public class MfaEnrollmentStatus : DomainEnumeration
{
    public static readonly MfaEnrollmentStatus NotEnrolled = new(1, nameof(NotEnrolled));
    public static readonly MfaEnrollmentStatus Enrolled = new(2, nameof(Enrolled));
    public static readonly MfaEnrollmentStatus Verified = new(3, nameof(Verified));

    private MfaEnrollmentStatus(int id, string name) : base(id, name) { }
}
