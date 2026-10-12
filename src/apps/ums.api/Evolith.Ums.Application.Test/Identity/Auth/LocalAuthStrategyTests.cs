namespace Evolith.Ums.Application.Test.Identity.Auth;

using Evolith.Ums.Application.Common.Interfaces;
using Evolith.Ums.Application.Identity.Auth;
using Moq;
using Xunit;

/// <summary>
/// Tests for LocalAuthStrategyService — BCrypt credential validation.
/// </summary>
public class LocalAuthStrategyTests
{
    private readonly Mock<IPasswordHashingService> _hasher = new();
    private LocalAuthStrategyService CreateSut() => new(_hasher.Object);

    private static Evolith.Ums.Domain.Identity.UserAccount.UserAccount MakeUserWithCredential(string hashedPassword)
    {
        var actor = ActorId.Create("test");
        var user  = Evolith.Ums.Domain.Identity.UserAccount.UserAccount.Create(
            TenantId.Load(Guid.NewGuid()),
            Email.Create("user@test.com"),
            Evolith.Ums.Domain.Enums.UserCategory.Internal,
            null, null,
            actor).Value;

        // Add an active password credential via the aggregate method
        user.AddPassword(
            Evolith.Ums.Domain.Kernel.ValueObjects.PasswordHash.Create(hashedPassword),
            actor);
        user.DomainEvents.MarkChangesAsCommitted();
        return user;
    }

    private static Evolith.Ums.Domain.Identity.UserAccount.UserAccount MakeUserWithNoCredential()
    {
        var actor = ActorId.Create("test");
        var user  = Evolith.Ums.Domain.Identity.UserAccount.UserAccount.Create(
            TenantId.Load(Guid.NewGuid()),
            Email.Create("nopass@test.com"),
            Evolith.Ums.Domain.Enums.UserCategory.Internal,
            null, null,
            actor).Value;
        user.DomainEvents.MarkChangesAsCommitted();
        return user;
    }

    [Fact]
    public void Authenticate_ValidCredentials_ReturnsSuccess()
    {
        var user = MakeUserWithCredential("$hash$");
        _hasher.Setup(h => h.Verify("correct", "$hash$")).Returns(true);

        var result = CreateSut().Authenticate(user, "correct");

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Authenticate_WrongPassword_ReturnsFailure()
    {
        var user = MakeUserWithCredential("$hash$");
        _hasher.Setup(h => h.Verify(It.IsAny<string>(), "$hash$")).Returns(false);

        var result = CreateSut().Authenticate(user, "wrong");

        Assert.True(result.IsFailure);
        Assert.Contains("AUTH_006", result.Error);
    }

    [Fact]
    public void Authenticate_UserHasNoActiveCredential_ReturnsFailure()
    {
        var user = MakeUserWithNoCredential();

        var result = CreateSut().Authenticate(user, "any-password");

        Assert.True(result.IsFailure);
        Assert.Contains("AUTH_006", result.Error);
    }
}
