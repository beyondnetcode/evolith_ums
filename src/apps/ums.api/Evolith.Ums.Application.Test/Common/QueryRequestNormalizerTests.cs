using Evolith.Ums.Application.Common;

namespace Evolith.Ums.Application.Test.Common;

public sealed class QueryRequestNormalizerTests
{
    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("  ", null)]
    [InlineData("all", null)]
    [InlineData("ALL", null)]
    [InlineData(" active ", "active")]
    public void NormalizeStatusFilter_UsesNullForNoFilter(string? value, string? expected)
    {
        Assert.Equal(expected, QueryRequestNormalizer.NormalizeStatusFilter(value));
    }

    [Theory]
    [InlineData("desc", QueryDefaults.Descending)]
    [InlineData("DESC", QueryDefaults.Descending)]
    [InlineData("asc", QueryDefaults.Ascending)]
    [InlineData("invalid", QueryDefaults.Ascending)]
    [InlineData(null, QueryDefaults.Ascending)]
    public void NormalizeSortOrder_UsesAscendingUnlessExplicitlyDescending(string? value, string expected)
    {
        Assert.Equal(expected, QueryRequestNormalizer.NormalizeSortOrder(value));
    }
}
