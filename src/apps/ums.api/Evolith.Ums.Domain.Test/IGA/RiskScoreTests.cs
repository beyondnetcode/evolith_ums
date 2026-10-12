namespace Evolith.Ums.Domain.Test.IGA;

using Xunit;

/// <summary>Pruebas del objeto de valor <see cref="RiskScore"/> (rango [0, 100], FR-061).</summary>
public class RiskScoreTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(50)]
    [InlineData(100)]
    public void Create_WithinRange_ReturnsSuccess(int value)
    {
        var result = RiskScore.Create(value);

        Assert.True(result.IsSuccess);
        Assert.Equal(value, result.Value.GetValue());
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    [InlineData(1000)]
    public void Create_OutOfRange_ReturnsFailure(int value)
    {
        var result = RiskScore.Create(value);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.IGA.RiskScoreOutOfRange, result.Error);
    }

    [Fact]
    public void RiskScore_WithSameValue_AreEqual()
    {
        Assert.Equal(RiskScore.Create(42).Value, RiskScore.Create(42).Value);
    }
}
