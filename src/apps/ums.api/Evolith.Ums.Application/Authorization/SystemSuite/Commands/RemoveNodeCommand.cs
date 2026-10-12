namespace Evolith.Ums.Application.Authorization.SystemSuite.Commands;

using FluentValidation;
using Evolith.Ums.Domain.Authorization;

public sealed record RemoveNodeCommand(Guid SystemSuiteId, Guid ModuleId, Guid NodeId) : ICommand;

public sealed class RemoveNodeCommandHandler : ICommandHandler<RemoveNodeCommand>
{
    private readonly ISystemSuiteRepository _repository;
    private readonly IUserContext _userContext;

    public RemoveNodeCommandHandler(ISystemSuiteRepository repository, IUserContext userContext)
    {
        _repository = repository;
        _userContext = userContext;
    }

    [AuditTrail]
    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]
    public async Task<Result> Handle(RemoveNodeCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_userContext.UserId))
            return Result.Failure("Authenticated user is required.");

        var suite = await _repository.GetByIdAsync(request.SystemSuiteId, cancellationToken);
        if (suite is null) return Result.Failure("System suite not found.");

        var result = suite.RemoveModuleNode(
            IdValueObject.Load(request.ModuleId),
            IdValueObject.Load(request.NodeId),
            ActorId.Create(_userContext.UserId));

        if (result.IsFailure) return result;

        await _repository.UpdateAsync(suite, cancellationToken);
        await _repository.UnitOfWork.SaveEntitiesAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed class RemoveNodeCommandValidator : AbstractValidator<RemoveNodeCommand>
{
    public RemoveNodeCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;
        RuleFor(x => x.SystemSuiteId).NotEmpty();
        RuleFor(x => x.ModuleId).NotEmpty();
        RuleFor(x => x.NodeId).NotEmpty();
    }
}
