namespace Evolith.Ums.Application.Authorization.SystemSuite.Commands;

using Evolith.Ums.Domain.Authorization;
using FluentValidation;

public sealed record SetNodeStatusCommand(Guid SystemSuiteId, Guid ModuleId, Guid NodeId, bool Active) : ICommand;

public sealed class SetNodeStatusCommandHandler : ICommandHandler<SetNodeStatusCommand>
{
    private readonly ISystemSuiteRepository _repository;
    private readonly IUserContext _userContext;

    public SetNodeStatusCommandHandler(ISystemSuiteRepository repository, IUserContext userContext)
    {
        _repository = repository;
        _userContext = userContext;
    }

    [AuditTrail]
    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]
    public async Task<Result> Handle(SetNodeStatusCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_userContext.UserId))
            return Result.Failure("Authenticated user is required.");

        var suite = await _repository.GetByIdAsync(request.SystemSuiteId, cancellationToken);
        if (suite is null) return Result.Failure("System suite not found.");

        var moduleId = IdValueObject.Load(request.ModuleId);
        var nodeId = IdValueObject.Load(request.NodeId);
        var actor = ActorId.Create(_userContext.UserId);

        var result = request.Active
            ? suite.ActivateModuleNode(moduleId, nodeId, actor)
            : suite.DeactivateModuleNode(moduleId, nodeId, actor);

        if (result.IsFailure) return result;

        await _repository.UpdateAsync(suite, cancellationToken);
        await _repository.UnitOfWork.SaveEntitiesAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed class SetNodeStatusCommandValidator : AbstractValidator<SetNodeStatusCommand>
{
    public SetNodeStatusCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;
        RuleFor(x => x.SystemSuiteId).NotEmpty();
        RuleFor(x => x.ModuleId).NotEmpty();
        RuleFor(x => x.NodeId).NotEmpty();
    }
}
