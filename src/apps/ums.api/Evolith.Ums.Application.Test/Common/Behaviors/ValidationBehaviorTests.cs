namespace Evolith.Ums.Application.Test.Common.Behaviors;

using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Evolith.Ums.Application.Common.Behaviors;
using Evolith.Ums.Domain.Kernel;

public class ValidationBehaviorTests
{
    private readonly Mock<IValidator<TestCommand>> _validatorMock;
    private readonly ValidationBehavior<TestCommand, Result> _behavior;

    public ValidationBehaviorTests()
    {
        _validatorMock = new Mock<IValidator<TestCommand>>();
        _behavior = new ValidationBehavior<TestCommand, Result>(new[] { _validatorMock.Object });
    }

    #region Handle - Success Scenarios

    [Fact]
    public async Task Handle_WhenValidationPasses_CallsNext()
    {
        var command = new TestCommand("Valid");
        var validationResult = new ValidationResult();
        _validatorMock.Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<TestCommand>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(validationResult);
        var nextCalled = false;
        Task<Result> Next() { nextCalled = true; return Task.FromResult(Result.Success()); }

        var result = await _behavior.Handle(command, Next, CancellationToken.None);

        Assert.True(nextCalled);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Handle_WhenNoValidators_CallsNext()
    {
        var behavior = new ValidationBehavior<TestCommand, Result>(Array.Empty<IValidator<TestCommand>>());
        var command = new TestCommand("Valid");
        var nextCalled = false;
        Task<Result> Next() { nextCalled = true; return Task.FromResult(Result.Success()); }

        var result = await behavior.Handle(command, Next, CancellationToken.None);

        Assert.True(nextCalled);
        Assert.True(result.IsSuccess);
    }

    #endregion

    #region Handle - Failure Scenarios

    [Fact]
    public async Task Handle_WhenValidationFails_ReturnsFailureResult()
    {
        var command = new TestCommand("");
        var validationResult = new ValidationResult(new[]
        {
            new ValidationFailure("Name", "Name is required")
        });
        _validatorMock.Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<TestCommand>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(validationResult);
        Task<Result> Next() => throw new InvalidOperationException("Should not be called");

        var result = await _behavior.Handle(command, Next, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.StartsWith("Validation.Failed:", result.Error);
        Assert.Contains("Name is required", result.Error);
    }

    [Fact]
    public async Task Handle_WhenMultipleValidationErrors_ReturnsAllErrors()
    {
        var command = new TestCommand("");
        var validationResult = new ValidationResult(new[]
        {
            new ValidationFailure("Name", "Name is required"),
            new ValidationFailure("Code", "Code is invalid")
        });
        _validatorMock.Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<TestCommand>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(validationResult);
        Task<Result> Next() => throw new InvalidOperationException("Should not be called");

        var result = await _behavior.Handle(command, Next, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.StartsWith("Validation.Failed:", result.Error);
        Assert.Contains("Name is required", result.Error);
        Assert.Contains("Code is invalid", result.Error);
    }

    [Fact]
    public async Task Handle_WhenDuplicateErrors_ReturnsDistinctErrors()
    {
        var command = new TestCommand("");
        var validationResult = new ValidationResult(new[]
        {
            new ValidationFailure("Name", "Name is required"),
            new ValidationFailure("Name", "Name is required")
        });
        _validatorMock.Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<TestCommand>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(validationResult);
        Task<Result> Next() => throw new InvalidOperationException("Should not be called");

        var result = await _behavior.Handle(command, Next, CancellationToken.None);

        Assert.True(result.IsFailure);
        var errorCount = result.Error.Split(';').Length;
        Assert.Equal(1, errorCount);
    }

    #endregion

    #region Generic Result Tests

    [Fact]
    public async Task Handle_WithGenericResult_WhenValidationFails_ReturnsFailureResult()
    {
        var validatorMock = new Mock<IValidator<TestCommandWithResponse>>();
        var behavior = new ValidationBehavior<TestCommandWithResponse, Result<TestResponse>>(new[] { validatorMock.Object });
        var command = new TestCommandWithResponse("");
        var validationResult = new ValidationResult(new[]
        {
            new ValidationFailure("Name", "Name is required")
        });
        validatorMock.Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<TestCommandWithResponse>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(validationResult);
        Task<Result<TestResponse>> Next() => throw new InvalidOperationException("Should not be called");

        var result = await behavior.Handle(command, Next, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.StartsWith("Validation.Failed:", result.Error);
        Assert.Contains("Name is required", result.Error);
    }

    [Fact]
    public async Task Handle_WithGenericResult_WhenValidationPasses_CallsNext()
    {
        var validatorMock = new Mock<IValidator<TestCommandWithResponse>>();
        var behavior = new ValidationBehavior<TestCommandWithResponse, Result<TestResponse>>(new[] { validatorMock.Object });
        var command = new TestCommandWithResponse("Valid");
        var validationResult = new ValidationResult();
        validatorMock.Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<TestCommandWithResponse>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(validationResult);
        var nextCalled = false;
        Task<Result<TestResponse>> Next() { nextCalled = true; return Task.FromResult(Result<TestResponse>.Success(new TestResponse(Guid.NewGuid()))); }

        var result = await behavior.Handle(command, Next, CancellationToken.None);

        Assert.True(nextCalled);
        Assert.True(result.IsSuccess);
    }

    #endregion

    #region Unsupported Type Tests

    [Fact]
    public async Task Handle_WithUnsupportedReturnType_ThrowsInvalidOperationException()
    {
        var validatorMock = new Mock<IValidator<TestCommand>>();
        var behavior = new ValidationBehavior<TestCommand, string>(new[] { validatorMock.Object });
        var command = new TestCommand("");
        var validationResult = new ValidationResult(new[]
        {
            new ValidationFailure("Name", "Name is required")
        });
        validatorMock.Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<TestCommand>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(validationResult);
        Task<string> Next() => Task.FromResult("result");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            behavior.Handle(command, Next, CancellationToken.None));
    }

    #endregion

    public record TestCommand(string Name) : IRequest<Result>;
    public record TestCommandWithResponse(string Name) : IRequest<Result<TestResponse>>;
    public record TestResponse(Guid Id);
}
