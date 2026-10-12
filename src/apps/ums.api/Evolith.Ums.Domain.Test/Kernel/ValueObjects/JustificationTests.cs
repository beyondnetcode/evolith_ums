namespace Evolith.Ums.Domain.Test.Kernel.ValueObjects;

using Xunit;

public class JustificationTests
{
    [Fact]
    public void Create_WithValidText_ReturnsJustification()
    {
        var result = Justification.Create("This is a valid justification");

        Assert.Equal("This is a valid justification", result.GetValue());
        Assert.Empty(result.BrokenRules.GetBrokenRules());
    }

    [Fact]
    public void Create_WithWhitespace_TrimsValue()
    {
        var result = Justification.Create("  trimmed justification  ");

        Assert.Equal("trimmed justification", result.GetValue());
    }

    [Fact]
    public void Create_WithNull_ReturnsEmptyJustification()
    {
        var result = Justification.Create(null);

        Assert.Equal(string.Empty, result.GetValue());
    }

    [Fact]
    public void Default_ReturnsEmptyJustification()
    {
        var result = Justification.Default();

        Assert.Equal(string.Empty, result.GetValue());
    }
}
