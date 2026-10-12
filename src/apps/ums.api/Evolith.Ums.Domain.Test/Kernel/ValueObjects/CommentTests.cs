namespace Evolith.Ums.Domain.Test.Kernel.ValueObjects;

using Xunit;

public class CommentTests
{
    [Fact]
    public void Create_WithValidText_ReturnsComment()
    {
        var result = Comment.Create("This is a valid comment");

        Assert.Equal("This is a valid comment", result.GetValue());
        Assert.Empty(result.BrokenRules.GetBrokenRules());
    }

    [Fact]
    public void Create_WithWhitespace_TrimsValue()
    {
        var result = Comment.Create("  trimmed comment  ");

        Assert.Equal("trimmed comment", result.GetValue());
    }

    [Fact]
    public void Create_WithNull_ReturnsEmptyComment()
    {
        var result = Comment.Create(null);

        Assert.Equal(string.Empty, result.GetValue());
    }

    [Fact]
    public void Default_ReturnsEmptyComment()
    {
        var result = Comment.Default();

        Assert.Equal(string.Empty, result.GetValue());
    }
}
