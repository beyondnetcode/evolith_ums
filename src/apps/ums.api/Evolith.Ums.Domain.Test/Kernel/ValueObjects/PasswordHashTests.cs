namespace Evolith.Ums.Domain.Test.Kernel.ValueObjects;

using Xunit;

public class PasswordHashTests
{
    [Fact]
    public void Create_WithValidHash_ReturnsPasswordHash()
    {
        var result = PasswordHash.Create("AQAAAAEAACcQAAAAEHq...");

        Assert.Equal("AQAAAAEAACcQAAAAEHq...", result.GetValue());
    }

    [Fact]
    public void Create_TrimsValue()
    {
        var result = PasswordHash.Create("  hash-value  ");

        Assert.Equal("hash-value", result.GetValue());
    }

    [Fact]
    public void Create_WithNull_ReturnsEmptyHash()
    {
        var result = PasswordHash.Create(null);

        Assert.Equal(string.Empty, result.GetValue());
    }

    [Fact]
    public void Empty_ReturnsEmptyPasswordHash()
    {
        var result = PasswordHash.Empty();

        Assert.Equal(string.Empty, result.GetValue());
    }

    [Fact]
    public void IsEmpty_WhenEmpty_ReturnsTrue()
    {
        var hash = PasswordHash.Empty();

        Assert.True(hash.IsEmpty());
    }

    [Fact]
    public void IsEmpty_WhenHasValue_ReturnsFalse()
    {
        var hash = PasswordHash.Create("some-hash-value");

        Assert.False(hash.IsEmpty());
    }

    [Fact]
    public void IsEmpty_WhenWhitespaceOnly_ReturnsTrue()
    {
        var hash = PasswordHash.Create("   ");

        Assert.True(hash.IsEmpty());
    }
}
