namespace Evolith.Ums.Domain.Test.Kernel.ValueObjects;

using Xunit;

public class ConfigurationKeyTests
{
    [Fact]
    public void Create_WithValidKey_ReturnsConfigurationKey()
    {
        var result = ConfigurationKey.Create("MaxLoginAttempts");

        Assert.Equal("MaxLoginAttempts", result.GetValue());
        Assert.Empty(result.BrokenRules.GetBrokenRules());
    }

    [Fact]
    public void Create_TrimsValue()
    {
        var result = ConfigurationKey.Create("  SessionTimeout  ");

        Assert.Equal("SessionTimeout", result.GetValue());
    }

    [Fact]
    public void Create_WithNull_ReturnsEmptyKey()
    {
        var result = ConfigurationKey.Create(null);

        Assert.Equal(string.Empty, result.GetValue());
    }

    [Fact]
    public void Create_WithEmptyString_HasBrokenRuleForRequired()
    {
        var result = ConfigurationKey.Create("");

        var brokenRules = result.BrokenRules.GetBrokenRules();
        Assert.NotEmpty(brokenRules);
        Assert.Contains(DomainErrors.ValueObject.PropertyRequired, brokenRules[0].Message);
    }

    [Fact]
    public void Create_WithWhitespaceOnly_HasBrokenRuleForRequired()
    {
        var result = ConfigurationKey.Create("   ");

        var brokenRules = result.BrokenRules.GetBrokenRules();
        Assert.NotEmpty(brokenRules);
    }

    [Fact]
    public void Create_WithKeyOver100Chars_HasBrokenRuleForTooLong()
    {
        var longKey = new string('A', 101);
        var result = ConfigurationKey.Create(longKey);

        var brokenRules = result.BrokenRules.GetBrokenRules();
        Assert.NotEmpty(brokenRules);
        Assert.Contains(DomainErrors.SystemSuite.ConfigurationKeyTooLong, brokenRules[0].Message);
    }

    [Fact]
    public void Create_WithKeyExactly100Chars_ReturnsValid()
    {
        var key = new string('A', 100);
        var result = ConfigurationKey.Create(key);

        Assert.Empty(result.BrokenRules.GetBrokenRules());
    }
}
