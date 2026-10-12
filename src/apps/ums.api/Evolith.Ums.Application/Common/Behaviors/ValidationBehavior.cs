namespace Evolith.Ums.Application.Common.Behaviors;

using Evolith.Ums.Domain.Kernel;
using FluentValidation;
using MediatR;

public sealed class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!_validators.Any())
        {
            return await next();
        }

        var context = new ValidationContext<TRequest>(request);
        var validationResults = await Task.WhenAll(
            _validators.Select(validator => validator.ValidateAsync(context, cancellationToken)));

        // FIX-10: Use only ErrorMessage (not PropertyName) to prevent internal dotted-path
        // fragments like "Command.TenantId.Value" from matching DomainErrorStatusMapper
        // substring rules ("not found", "unauthorized", etc.) and returning the wrong HTTP
        // status code. The "Validation.Failed:" sentinel lets DomainErrorStatusMapper
        // short-circuit to 422 without inspecting individual messages.
        var errors = validationResults
            .SelectMany(result => result.Errors)
            .Where(error => error is not null)
            .Select(error => error.ErrorMessage)
            .Distinct()
            .ToArray();

        if (errors.Length == 0)
        {
            return await next();
        }

        return CreateValidationResult($"Validation.Failed: {string.Join("; ", errors)}");
    }

    private static readonly Func<string, TResponse>? _failureFactory = CreateFailureFactory();

    private static Func<string, TResponse>? CreateFailureFactory()
    {
        if (typeof(TResponse) == typeof(Result))
        {
            return error => (TResponse)(object)Result.Failure(error);
        }

        if (typeof(TResponse).IsGenericType &&
            typeof(TResponse).GetGenericTypeDefinition() == typeof(Result<>))
        {
            var valueType = typeof(TResponse).GetGenericArguments()[0];
            var failureMethod = typeof(Result<>)
                .MakeGenericType(valueType)
                .GetMethod(nameof(Result.Failure), new[] { typeof(string) });

            if (failureMethod is not null)
            {
                return (Func<string, TResponse>)Delegate.CreateDelegate(typeof(Func<string, TResponse>), failureMethod);
            }
        }

        return null;
    }

    private static TResponse CreateValidationResult(string error)
    {
        if (_failureFactory is not null)
        {
            return _failureFactory(error);
        }

        throw new InvalidOperationException($"ValidationBehavior cannot create a failure result for '{typeof(TResponse).Name}'.");
    }
}
