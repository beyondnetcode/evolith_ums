namespace Evolith.Ums.Domain.Test.Kernel.ValueObjects;

using Xunit;

public class ValueTests
{
    [Fact]
    public void Create_WithValidText_ReturnsValue()
    {
        var result = Value.Create("some-value");

        Assert.Equal("some-value", result.GetValue());
        Assert.Empty(result.BrokenRules.GetBrokenRules());
    }

    [Fact]
    public void Create_TrimsValue()
    {
        var result = Value.Create("  trimmed-value  ");

        Assert.Equal("trimmed-value", result.GetValue());
    }

    [Fact]
    public void Create_WithNull_ReturnsEmptyValue()
    {
        var result = Value.Create(null);

        Assert.Equal(string.Empty, result.GetValue());
    }

    [Fact]
    public void Default_ReturnsEmptyValue()
    {
        var result = Value.Default();

        Assert.Equal(string.Empty, result.GetValue());
    }
}
