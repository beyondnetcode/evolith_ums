namespace Evolith.Ums.Domain.Test.Kernel.ValueObjects;

using Xunit;

public class EmailTests
{
    [Fact]
    public void Create_WithValidEmail_ReturnsEmail()
    {
        var result = Email.Create("user@example.com");

        Assert.Equal("user@example.com", result.GetValue());
        Assert.Empty(result.BrokenRules.GetBrokenRules());
    }

    [Fact]
    public void Create_TrimsAndLowercasesValue()
    {
        var result = Email.Create("  User@Example.COM  ");

        Assert.Equal("user@example.com", result.GetValue());
    }

    [Fact]
    public void Create_WithNull_ReturnsEmptyEmail()
    {
        var result = Email.Create(null);

        Assert.Equal(string.Empty, result.GetValue());
    }

    [Fact]
    public void Create_WithEmptyString_HasBrokenRuleForRequired()
    {
        var result = Email.Create("");

        var brokenRules = result.BrokenRules.GetBrokenRules();
        Assert.NotEmpty(brokenRules);
        Assert.Contains(DomainErrors.ValueObject.PropertyRequired, brokenRules[0].Message);
    }

    [Fact]
    public void Create_WithWhitespaceOnly_HasBrokenRuleForRequired()
    {
        var result = Email.Create("   ");

        var brokenRules = result.BrokenRules.GetBrokenRules();
        Assert.NotEmpty(brokenRules);
    }

    [Fact]
    public void Create_WithInvalidEmailFormat_HasBrokenRuleForInvalidEmail()
    {
        var result = Email.Create("not-an-email");

        var brokenRules = result.BrokenRules.GetBrokenRules();
        Assert.NotEmpty(brokenRules);
        Assert.Contains(DomainErrors.UserAccount.InvalidEmail, brokenRules[0].Message);
    }

    [Fact]
    public void Create_WithValidSubdomainEmail_ReturnsValid()
    {
        var result = Email.Create("user@sub.domain.example.com");

        Assert.Empty(result.BrokenRules.GetBrokenRules());
    }
}
