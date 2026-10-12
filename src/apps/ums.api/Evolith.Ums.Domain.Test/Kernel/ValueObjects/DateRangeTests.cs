namespace Evolith.Ums.Domain.Test.Kernel.ValueObjects;

using Xunit;

public class DateRangeTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 28, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_WithStartOnly_ReturnsSuccess()
    {
        var result = DateRange.Create(Now);

        Assert.True(result.IsSuccess);
        Assert.Equal(Now, result.Value.StartsAt);
        Assert.Null(result.Value.EndsAt);
    }

    [Fact]
    public void Create_WithValidEnd_ReturnsSuccess()
    {
        var end = Now.AddDays(30);
        var result = DateRange.Create(Now, end);

        Assert.True(result.IsSuccess);
        Assert.Equal(Now, result.Value.StartsAt);
        Assert.Equal(end, result.Value.EndsAt);
    }

    [Fact]
    public void Create_WhenEndBeforeStart_ReturnsFailure()
    {
        var end = Now.AddDays(-1);
        var result = DateRange.Create(Now, end);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.ValueObject.DateRangeInvalid, result.Error);
    }

    [Fact]
    public void Create_WhenEndEqualsStart_ReturnsSuccess()
    {
        var result = DateRange.Create(Now, Now);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Default_ReturnsMinValueWithNoEnd()
    {
        var result = DateRange.Default();

        Assert.True(result.IsSuccess);
        Assert.Equal(DateTimeOffset.MinValue, result.Value.StartsAt);
        Assert.Null(result.Value.EndsAt);
    }

    [Fact]
    public void Contains_WhenValueWithinRange_ReturnsTrue()
    {
        var start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var end = new DateTimeOffset(2026, 12, 31, 23, 59, 59, TimeSpan.Zero);
        var range = DateRange.Create(start, end).Value;
        var value = new DateTimeOffset(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);

        Assert.True(range.Contains(value));
    }

    [Fact]
    public void Contains_WhenValueBeforeStart_ReturnsFalse()
    {
        var start = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
        var range = DateRange.Create(start).Value;
        var value = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        Assert.False(range.Contains(value));
    }

    [Fact]
    public void Contains_WhenValueAfterEnd_ReturnsFalse()
    {
        var start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var end = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
        var range = DateRange.Create(start, end).Value;
        var value = new DateTimeOffset(2026, 12, 31, 0, 0, 0, TimeSpan.Zero);

        Assert.False(range.Contains(value));
    }

    [Fact]
    public void Contains_WhenNoEndAndValueAfterStart_ReturnsTrue()
    {
        var start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var range = DateRange.Create(start).Value;
        var value = new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero);

        Assert.True(range.Contains(value));
    }

    [Fact]
    public void Contains_WhenValueEqualsStart_ReturnsTrue()
    {
        var start = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
        var range = DateRange.Create(start).Value;

        Assert.True(range.Contains(start));
    }

    [Fact]
    public void Contains_WhenValueEqualsEnd_ReturnsTrue()
    {
        var start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var end = new DateTimeOffset(2026, 12, 31, 0, 0, 0, TimeSpan.Zero);
        var range = DateRange.Create(start, end).Value;

        Assert.True(range.Contains(end));
    }
}
