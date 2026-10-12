namespace Evolith.Ums.Domain.Test.Identity.UserAccount.PasswordCredential;

using Evolith.Ums.Domain.Identity.UserAccount.PasswordCredential;
using Xunit;

public class PasswordCredentialTests
{
    private static readonly UserAccountId ValidUserAccountId = UserAccountId.Load(Guid.NewGuid().ToString());
    private static readonly PasswordHash ValidPasswordHash = PasswordHash.Create("hashedpassword123");
    private static readonly ActorId ValidActor = ActorId.Create("user-001");
    #region Create

    [Fact]
    public void Create_WithValidData_ReturnsSuccess()
    {
        var result = PasswordCredential.Create(ValidUserAccountId, ValidPasswordHash, ValidActor);

        Assert.True(result.IsSuccess);
        Assert.Equal(ValidUserAccountId, result.Value.UserAccountId);
        Assert.Equal(ValidPasswordHash, result.Value.PasswordHash);
        Assert.False(result.Value.IsActive);
    }

    [Fact]
    public void Create_WithEmptyPasswordHash_ReturnsFailure()
    {
        var emptyHash = PasswordHash.Create("");

        var result = PasswordCredential.Create(ValidUserAccountId, emptyHash, ValidActor);

        Assert.True(result.IsFailure);
    }

    #endregion

    #region ActivateInternal

    [Fact]
    public void ActivateInternal_SetsIsActiveToTrue()
    {
        var credential = PasswordCredential.Create(ValidUserAccountId, ValidPasswordHash, ValidActor).Value;

        credential.ActivateInternal();

        Assert.True(credential.IsActive);
    }

    #endregion

    #region DeactivateInternal

    [Fact]
    public void DeactivateInternal_SetsIsActiveToFalse()
    {
        var credential = PasswordCredential.Create(ValidUserAccountId, ValidPasswordHash, ValidActor).Value;
        credential.ActivateInternal();

        credential.DeactivateInternal();

        Assert.False(credential.IsActive);
    }

    #endregion
}
