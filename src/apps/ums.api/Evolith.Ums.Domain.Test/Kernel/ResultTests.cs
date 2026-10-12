namespace Evolith.Ums.Domain.Test.Kernel;

using Xunit;

public class ResultTests
{
    [Fact]
    public void Success_ReturnsSuccessfulResult()
    {
        var result = Result.Success();

        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Equal(string.Empty, result.Error);
    }

    [Fact]
    public void Failure_WithMessage_ReturnsFailedResult()
    {
        var result = Result.Failure("Something went wrong");

        Assert.False(result.IsSuccess);
        Assert.True(result.IsFailure);
        Assert.Equal("Something went wrong", result.Error);
    }
}

public class ResultOfTTests
{
    [Fact]
    public void Success_WithValue_ReturnsSuccessfulResult()
    {
        var result = Result<string>.Success("hello");

        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Equal("hello", result.Value);
    }

    [Fact]
    public void Failure_WithMessage_ReturnsFailedResult()
    {
        var result = Result<string>.Failure("Error occurred");

        Assert.False(result.IsSuccess);
        Assert.True(result.IsFailure);
        Assert.Equal("Error occurred", result.Error);
    }

    [Fact]
    public void Value_WhenSuccess_ReturnsValue()
    {
        var result = Result<int>.Success(42);

        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void Value_WhenFailure_ThrowsInvalidOperationException()
    {
        var result = Result<int>.Failure("Cannot get value");

        var ex = Assert.Throws<InvalidOperationException>(() => result.Value);
        Assert.Contains("Cannot access value of a failure result", ex.Message);
    }

    [Fact]
    public void Success_WithNullValue_ReturnsSuccessfulResult()
    {
        var result = Result<string>.Success(null!);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value);
    }

    [Fact]
    public void IsFailure_IsInverseOfIsSuccess()
    {
        var successResult = Result.Success();
        var failureResult = Result.Failure("error");

        Assert.False(successResult.IsFailure);
        Assert.True(failureResult.IsFailure);
    }
}
