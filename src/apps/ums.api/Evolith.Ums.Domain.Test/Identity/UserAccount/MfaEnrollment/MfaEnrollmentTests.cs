namespace Evolith.Ums.Domain.Test.Identity.UserAccount.MfaEnrollment;

using Evolith.Ums.Domain.Identity.UserAccount.MfaEnrollment;
using Xunit;

public class MfaEnrollmentTests
{
    private static readonly UserAccountId ValidUserAccountId = UserAccountId.Load(Guid.NewGuid().ToString());
    private static readonly ActorId ValidActor = ActorId.Create("user-001");

    #region Create

    [Fact]
    public void Create_WithValidData_ReturnsSuccess()
    {
        var result = MfaEnrollment.Create(ValidUserAccountId, MfaMethod.Totp, ValidActor);

        Assert.True(result.IsSuccess);
        Assert.Equal(ValidUserAccountId, result.Value.UserAccountId);
        Assert.Equal(MfaMethod.Totp, result.Value.Method);
    }

    [Fact]
    public void Create_StartsInEnrolledStatus_NotVerifiedNorNotEnrolled()
    {
        var result = MfaEnrollment.Create(ValidUserAccountId, MfaMethod.Totp, ValidActor);

        Assert.True(result.IsSuccess);
        Assert.Equal(MfaEnrollmentStatus.Enrolled, result.Value.Status);
        Assert.NotEqual(MfaEnrollmentStatus.NotEnrolled, result.Value.Status);
        Assert.NotEqual(MfaEnrollmentStatus.Verified, result.Value.Status);
    }

    [Theory]
    [InlineData(1)] // Totp
    [InlineData(2)] // WebAuthn
    [InlineData(3)] // SmsOtp
    [InlineData(4)] // EmailOtp
    public void Create_WithAnyMethod_StartsEnrolled(int methodId)
    {
        var method = DomainEnumeration.FromValue<MfaMethod>(methodId)!;

        var result = MfaEnrollment.Create(ValidUserAccountId, method, ValidActor);

        Assert.True(result.IsSuccess);
        Assert.Equal(method, result.Value.Method);
        Assert.Equal(MfaEnrollmentStatus.Enrolled, result.Value.Status);
    }

    [Fact]
    public void GetId_ReturnsStableNonEmptyIdentifier()
    {
        var enrollment = MfaEnrollment.Create(ValidUserAccountId, MfaMethod.Totp, ValidActor).Value;

        var first = enrollment.GetId();
        var second = enrollment.GetId();

        Assert.NotEqual(Guid.Empty, first.GetValue());
        Assert.Equal(first.GetValue(), second.GetValue());
    }

    #endregion

    #region Verify

    [Fact]
    public void Verify_WhenEnrolled_TransitionsToVerified()
    {
        var enrollment = MfaEnrollment.Create(ValidUserAccountId, MfaMethod.Totp, ValidActor).Value;

        var result = enrollment.Verify(ValidActor);

        Assert.True(result.IsSuccess);
        Assert.Equal(MfaEnrollmentStatus.Verified, enrollment.Status);
    }

    [Fact]
    public void Verify_WhenAlreadyVerified_ReturnsFailureAndKeepsStatus()
    {
        var enrollment = MfaEnrollment.Create(ValidUserAccountId, MfaMethod.Totp, ValidActor).Value;
        enrollment.Verify(ValidActor);

        var result = enrollment.Verify(ValidActor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.UserAccount.MfaAlreadyVerified, result.Error);
        Assert.Equal(MfaEnrollmentStatus.Verified, enrollment.Status);
    }

    #endregion
}
