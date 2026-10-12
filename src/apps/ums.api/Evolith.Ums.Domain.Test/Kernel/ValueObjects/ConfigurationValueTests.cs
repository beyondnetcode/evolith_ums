namespace Evolith.Ums.Domain.Test.Kernel.ValueObjects;

using Xunit;

public class ConfigurationValueTests
{
    [Fact]
    public void Create_WithValidValue_ReturnsConfigurationValue()
    {
        var result = ConfigurationValue.Create("some-config-value");

        Assert.Equal("some-config-value", result.GetValue());
        Assert.Empty(result.BrokenRules.GetBrokenRules());
    }

    [Fact]
    public void Create_TrimsValue()
    {
        var result = ConfigurationValue.Create("  trimmed-value  ");

        Assert.Equal("trimmed-value", result.GetValue());
    }

    [Fact]
    public void Create_WithNull_ReturnsEmptyValue()
    {
        var result = ConfigurationValue.Create(null);

        Assert.Equal(string.Empty, result.GetValue());
    }

    [Fact]
    public void Create_WithEmptyString_HasBrokenRuleForRequired()
    {
        var result = ConfigurationValue.Create("");

        var brokenRules = result.BrokenRules.GetBrokenRules();
        Assert.NotEmpty(brokenRules);
        Assert.Contains(DomainErrors.ValueObject.PropertyRequired, brokenRules[0].Message);
    }

    [Fact]
    public void Create_WithWhitespaceOnly_HasBrokenRuleForRequired()
    {
        var result = ConfigurationValue.Create("   ");

        var brokenRules = result.BrokenRules.GetBrokenRules();
        Assert.NotEmpty(brokenRules);
    }

    [Fact]
    public void Create_WithValueOver2000Chars_HasBrokenRuleForTooLong()
    {
        var longValue = new string('A', 2001);
        var result = ConfigurationValue.Create(longValue);

        var brokenRules = result.BrokenRules.GetBrokenRules();
        Assert.NotEmpty(brokenRules);
        Assert.Contains(DomainErrors.SystemSuite.ConfigurationValueTooLong, brokenRules[0].Message);
    }

    [Fact]
    public void Create_WithValueExactly2000Chars_ReturnsValid()
    {
        var value = new string('A', 2000);
        var result = ConfigurationValue.Create(value);

        Assert.Empty(result.BrokenRules.GetBrokenRules());
    }
}
