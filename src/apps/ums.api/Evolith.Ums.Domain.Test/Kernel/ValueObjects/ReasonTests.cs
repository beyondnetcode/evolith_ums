namespace Evolith.Ums.Domain.Test.Kernel.ValueObjects;

using Xunit;

public class ReasonTests
{
    [Fact]
    public void Create_WithValidText_ReturnsReason()
    {
        var result = Reason.Create("This is a valid reason");

        Assert.Equal("This is a valid reason", result.GetValue());
        Assert.Empty(result.BrokenRules.GetBrokenRules());
    }

    [Fact]
    public void Create_WithWhitespace_TrimsValue()
    {
        var result = Reason.Create("  trimmed reason  ");

        Assert.Equal("trimmed reason", result.GetValue());
    }

    [Fact]
    public void Create_WithNull_ReturnsEmptyReason()
    {
        var result = Reason.Create(null);

        Assert.Equal(string.Empty, result.GetValue());
    }

    [Fact]
    public void Default_ReturnsEmptyReason()
    {
        var result = Reason.Default();

        Assert.Equal(string.Empty, result.GetValue());
    }
}
