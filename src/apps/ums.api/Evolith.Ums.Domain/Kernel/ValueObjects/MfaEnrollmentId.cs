namespace Evolith.Ums.Domain.Kernel.ValueObjects;

public class MfaEnrollmentId : IdValueObject
{
    private MfaEnrollmentId(Guid value) : base(value) { }
    public static new MfaEnrollmentId Create() => new MfaEnrollmentId(Guid.NewGuid());
    public static new MfaEnrollmentId Load(Guid value) => new MfaEnrollmentId(value);
    public static new MfaEnrollmentId Load(string value) => new MfaEnrollmentId(Guid.Parse(value));
}
