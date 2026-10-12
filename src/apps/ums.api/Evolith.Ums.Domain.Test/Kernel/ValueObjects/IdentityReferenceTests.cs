namespace Evolith.Ums.Domain.Test.Kernel.ValueObjects;

using Xunit;

public class IdentityReferenceTests
{
    [Fact]
    public void Create_WithValidReference_ReturnsIdentityReference()
    {
        var result = IdentityReference.Create("idp-user-12345");

        Assert.Equal("idp-user-12345", result.GetValue());
    }

    [Fact]
    public void Create_TrimsValue()
    {
        var result = IdentityReference.Create("  trimmed-ref  ");

        Assert.Equal("trimmed-ref", result.GetValue());
    }

    [Fact]
    public void Create_WithNull_ReturnsEmptyReference()
    {
        var result = IdentityReference.Create(null);

        Assert.Equal(string.Empty, result.GetValue());
    }

    [Fact]
    public void Empty_ReturnsEmptyIdentityReference()
    {
        var result = IdentityReference.Empty();

        Assert.Equal(string.Empty, result.GetValue());
    }

    [Fact]
    public void IsEmpty_WhenEmpty_ReturnsTrue()
    {
        var reference = IdentityReference.Empty();

        Assert.True(reference.IsEmpty());
    }

    [Fact]
    public void IsEmpty_WhenHasValue_ReturnsFalse()
    {
        var reference = IdentityReference.Create("some-reference");

        Assert.False(reference.IsEmpty());
    }

    [Fact]
    public void IsEmpty_WhenWhitespaceOnly_ReturnsTrue()
    {
        var reference = IdentityReference.Create("   ");

        Assert.True(reference.IsEmpty());
    }
}
