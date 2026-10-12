namespace Evolith.Ums.Domain.Test.Kernel.ValueObjects;

using Xunit;

public class CompanyReferenceTests
{
    [Fact]
    public void Create_WithValidText_ReturnsCompanyReference()
    {
        var result = CompanyReference.Create("COMP-2026-001");

        Assert.Equal("COMP-2026-001", result.GetValue());
        Assert.Empty(result.BrokenRules.GetBrokenRules());
    }

    [Fact]
    public void Create_WithWhitespace_TrimsValue()
    {
        var result = CompanyReference.Create("  COMP-REF  ");

        Assert.Equal("COMP-REF", result.GetValue());
    }

    [Fact]
    public void Create_WithNull_ReturnsEmptyCompanyReference()
    {
        var result = CompanyReference.Create(null);

        Assert.Equal(string.Empty, result.GetValue());
    }

    [Fact]
    public void Default_ReturnsEmptyCompanyReference()
    {
        var result = CompanyReference.Default();

        Assert.Equal(string.Empty, result.GetValue());
    }
}
