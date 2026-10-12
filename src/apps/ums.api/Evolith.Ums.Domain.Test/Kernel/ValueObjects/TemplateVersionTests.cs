namespace Evolith.Ums.Domain.Test.Kernel.ValueObjects;

using Xunit;

public class TemplateVersionTests
{
    [Fact]
    public void Create_WithValidSemVer_ReturnsTemplateVersion()
    {
        var result = TemplateVersion.Create(1, 2, 3);

        Assert.Equal("1.2.3", result.GetValue());
        Assert.Empty(result.BrokenRules.GetBrokenRules());
    }

    [Fact]
    public void Create_WithZeroVersion_ReturnsValid()
    {
        var result = TemplateVersion.Create(0, 0, 0);

        Assert.Equal("0.0.0", result.GetValue());
        Assert.Empty(result.BrokenRules.GetBrokenRules());
    }

    [Fact]
    public void Create_WithLargeNumbers_ReturnsValid()
    {
        var result = TemplateVersion.Create(999, 999, 999);

        Assert.Equal("999.999.999", result.GetValue());
        Assert.Empty(result.BrokenRules.GetBrokenRules());
    }

    [Fact]
    public void Initial_ReturnsVersion010()
    {
        var result = TemplateVersion.Initial();

        Assert.Equal("0.1.0", result.GetValue());
        Assert.Empty(result.BrokenRules.GetBrokenRules());
    }

    [Fact]
    public void Segments_ParseSemVer()
    {
        var version = TemplateVersion.Create(3, 4, 5);

        Assert.Equal(3, version.Major);
        Assert.Equal(4, version.Minor);
        Assert.Equal(5, version.Patch);
    }

    [Fact]
    public void Next_IncrementsMinorAndResetsPatch()
    {
        Assert.Equal("0.2.0", TemplateVersion.Initial().Next().GetValue());
        Assert.Equal("1.3.0", TemplateVersion.Create(1, 2, 7).Next().GetValue());
    }

    [Fact]
    public void CompareTo_OrdersNumericallyBySegment()
    {
        Assert.True(TemplateVersion.Create(2, 0, 0).CompareTo(TemplateVersion.Create(10, 0, 0)) < 0);
        Assert.True(TemplateVersion.Create(1, 2, 0).CompareTo(TemplateVersion.Create(1, 1, 9)) > 0);
        Assert.Equal(0, TemplateVersion.Create(1, 1, 1).CompareTo(TemplateVersion.Create(1, 1, 1)));
        Assert.True(TemplateVersion.Initial().CompareTo(null) > 0);
    }
}
