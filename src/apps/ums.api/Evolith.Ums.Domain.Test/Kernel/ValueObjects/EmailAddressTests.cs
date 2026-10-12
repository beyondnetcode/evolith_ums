namespace Evolith.Ums.Domain.Test.Kernel.ValueObjects;

using Xunit;

public class EmailAddressTests
{
    [Fact]
    public void Create_WithValidEmail_ReturnsSuccess()
    {
        var result = EmailAddress.Create("user@example.com");

        Assert.True(result.IsSuccess);
        Assert.Equal("user@example.com", result.Value.GetValue());
    }

    [Fact]
    public void Create_TrimsAndLowercasesEmail()
    {
        var result = EmailAddress.Create("  User@Example.COM  ");

        Assert.True(result.IsSuccess);
        Assert.Equal("user@example.com", result.Value.GetValue());
    }

    [Fact]
    public void Create_WithInvalidEmail_ReturnsFailure()
    {
        var result = EmailAddress.Create("not-an-email");

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.ValueObject.EmailRequired, result.Error);
    }

    [Fact]
    public void Create_WithEmptyString_ReturnsFailure()
    {
        var result = EmailAddress.Create("");

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Create_WithWhitespaceOnly_ReturnsFailure()
    {
        var result = EmailAddress.Create("   ");

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Create_WithMissingAtSymbol_ReturnsFailure()
    {
        var result = EmailAddress.Create("userexample.com");

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Create_WithMissingDomain_ReturnsFailure()
    {
        var result = EmailAddress.Create("user@");

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Default_ReturnsEmptyEmailAddress()
    {
        var result = EmailAddress.Default();

        Assert.Equal(string.Empty, result.GetValue());
    }
}
