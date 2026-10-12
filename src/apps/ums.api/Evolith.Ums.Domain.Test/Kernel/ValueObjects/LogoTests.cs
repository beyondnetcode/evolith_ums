namespace Evolith.Ums.Domain.Test.Kernel.ValueObjects;

using Xunit;

public class LogoTests
{
    [Fact]
    public void Create_WithValidUrl_ReturnsLogo()
    {
        var result = Logo.Create("https://cdn.example.com/logo.png");

        Assert.Equal("https://cdn.example.com/logo.png", result.GetValue());
        Assert.Empty(result.BrokenRules.GetBrokenRules());
    }

    [Fact]
    public void Create_TrimsValue()
    {
        var result = Logo.Create("  https://cdn.example.com/logo.png  ");

        Assert.Equal("https://cdn.example.com/logo.png", result.GetValue());
    }

    [Fact]
    public void Create_WithNull_ReturnsEmptyLogo()
    {
        var result = Logo.Create(null);

        Assert.Equal(string.Empty, result.GetValue());
    }

    [Fact]
    public void Create_WithEmptyString_HasBrokenRuleForRequired()
    {
        var result = Logo.Create("");

        var brokenRules = result.BrokenRules.GetBrokenRules();
        Assert.NotEmpty(brokenRules);
        Assert.Contains(DomainErrors.ValueObject.PropertyRequired, brokenRules[0].Message);
    }

    [Fact]
    public void Create_WithWhitespaceOnly_HasBrokenRuleForRequired()
    {
        var result = Logo.Create("   ");

        var brokenRules = result.BrokenRules.GetBrokenRules();
        Assert.NotEmpty(brokenRules);
    }

    [Fact]
    public void Default_ReturnsEmptyLogo()
    {
        var result = Logo.Default();

        Assert.Equal(string.Empty, result.GetValue());
    }
}
