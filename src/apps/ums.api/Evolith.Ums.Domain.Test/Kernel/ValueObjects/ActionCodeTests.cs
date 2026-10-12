namespace Evolith.Ums.Domain.Test.Kernel.ValueObjects;

using Xunit;

public class ActionCodeTests
{
    [Fact]
    public void Create_WithValidCode_ReturnsActionCode()
    {
        var result = ActionCode.Create("CREATE");

        Assert.Equal("CREATE", result.GetValue());
        Assert.Empty(result.BrokenRules.GetBrokenRules());
    }

    [Fact]
    public void Create_TrimsAndUpperCasesValue()
    {
        var result = ActionCode.Create("  create  ");

        Assert.Equal("CREATE", result.GetValue());
    }

    [Fact]
    public void Create_WithNull_ReturnsEmptyActionCode()
    {
        var result = ActionCode.Create(null);

        Assert.Equal(string.Empty, result.GetValue());
    }

    [Fact]
    public void Create_WithEmptyString_HasBrokenRuleForRequired()
    {
        var result = ActionCode.Create("");

        var brokenRules = result.BrokenRules.GetBrokenRules();
        Assert.NotEmpty(brokenRules);
        Assert.Contains(DomainErrors.ValueObject.PropertyRequired, brokenRules[0].Message);
    }

    [Fact]
    public void Create_WithWhitespaceOnly_HasBrokenRuleForRequired()
    {
        var result = ActionCode.Create("   ");

        var brokenRules = result.BrokenRules.GetBrokenRules();
        Assert.NotEmpty(brokenRules);
    }

    [Fact]
    public void Create_WithCodeOver50Chars_HasBrokenRuleForTooLong()
    {
        var longCode = new string('A', 51);
        var result = ActionCode.Create(longCode);

        var brokenRules = result.BrokenRules.GetBrokenRules();
        Assert.NotEmpty(brokenRules);
        Assert.Contains(DomainErrors.SystemSuite.ActionCodeTooLong, brokenRules[0].Message);
    }

    [Fact]
    public void Create_WithCodeExactly50Chars_ReturnsValid()
    {
        var code = new string('A', 50);
        var result = ActionCode.Create(code);

        Assert.Empty(result.BrokenRules.GetBrokenRules());
    }
}
