namespace Evolith.Ums.Domain.Test.Kernel.ValueObjects;

using Xunit;

public class TextValueObjectTests
{
    [Fact]
    public void Create_WithValidText_ReturnsTextValueObject()
    {
        var result = TextValueObject.Create("some text");

        Assert.Equal("some text", result.GetValue());
    }

    [Fact]
    public void Create_WithNull_ReturnsEmptyTextValueObject()
    {
        var result = TextValueObject.Create(null);

        Assert.Equal(string.Empty, result.GetValue());
    }

    [Fact]
    public void Create_DoesNotTrimValue()
    {
        var result = TextValueObject.Create("  text with spaces  ");

        Assert.Equal("  text with spaces  ", result.GetValue());
    }
}
