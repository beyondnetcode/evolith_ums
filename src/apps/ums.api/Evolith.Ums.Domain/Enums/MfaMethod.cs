namespace Evolith.Ums.Domain.Enums;

public class MfaMethod : DomainEnumeration
{
    public static readonly MfaMethod Totp = new(1, nameof(Totp));
    public static readonly MfaMethod WebAuthn = new(2, nameof(WebAuthn));
    public static readonly MfaMethod SmsOtp = new(3, nameof(SmsOtp));
    public static readonly MfaMethod EmailOtp = new(4, nameof(EmailOtp));

    private MfaMethod(int id, string name) : base(id, name) { }
}
