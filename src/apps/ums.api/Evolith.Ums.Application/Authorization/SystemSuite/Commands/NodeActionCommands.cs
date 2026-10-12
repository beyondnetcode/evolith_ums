namespace Evolith.Ums.Application.Authorization.SystemSuite.Commands;

using FluentValidation;
using Evolith.Ums.Domain.Authorization;

// ── Vincular funcionalidad (N:M) a un nodo hoja (Opción) ──

public sealed record LinkNodeActionCommand(Guid SystemSuiteId, Guid ModuleId, Guid NodeId, string ActionCode) : ICommand;

public sealed class LinkNodeActionCommandHandler : ICommandHandler<LinkNodeActionCommand>
{
    private readonly ISystemSuiteRepository _repository;
    private readonly IUserContext _userContext;

    public LinkNodeActionCommandHandler(ISystemSuiteRepository repository, IUserContext userContext)
    {
        _repository = repository;
        _userContext = userContext;
    }

    [AuditTrail]
    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]
    public async Task<Result> Handle(LinkNodeActionCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_userContext.UserId))
            return Result.Failure("Authenticated user is required.");

        var suite = await _repository.GetByIdAsync(request.SystemSuiteId, cancellationToken);
        if (suite is null) return Result.Failure("System suite not found.");

        var result = suite.LinkModuleNodeAction(
            IdValueObject.Load(request.ModuleId),
            IdValueObject.Load(request.NodeId),
            Domain.Kernel.ValueObjects.ActionCode.Create(request.ActionCode),
            ActorId.Create(_userContext.UserId));

        if (result.IsFailure) return result;

        await _repository.UpdateAsync(suite, cancellationToken);
        await _repository.UnitOfWork.SaveEntitiesAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed class LinkNodeActionCommandValidator : AbstractValidator<LinkNodeActionCommand>
{
    public LinkNodeActionCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;
        RuleFor(x => x.SystemSuiteId).NotEmpty();
        RuleFor(x => x.ModuleId).NotEmpty();
        RuleFor(x => x.NodeId).NotEmpty();
        RuleFor(x => x.ActionCode).NotEmpty().MaximumLength(100);
    }
}

// ── Desvincular funcionalidad ──

public sealed record UnlinkNodeActionCommand(Guid SystemSuiteId, Guid ModuleId, Guid NodeId, string ActionCode) : ICommand;

public sealed class UnlinkNodeActionCommandHandler : ICommandHandler<UnlinkNodeActionCommand>
{
    private readonly ISystemSuiteRepository _repository;
    private readonly IUserContext _userContext;

    public UnlinkNodeActionCommandHandler(ISystemSuiteRepository repository, IUserContext userContext)
    {
        _repository = repository;
        _userContext = userContext;
    }

    [AuditTrail]
    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]
    public async Task<Result> Handle(UnlinkNodeActionCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_userContext.UserId))
            return Result.Failure("Authenticated user is required.");

        var suite = await _repository.GetByIdAsync(request.SystemSuiteId, cancellationToken);
        if (suite is null) return Result.Failure("System suite not found.");

        var result = suite.UnlinkModuleNodeAction(
            IdValueObject.Load(request.ModuleId),
            IdValueObject.Load(request.NodeId),
            Domain.Kernel.ValueObjects.ActionCode.Create(request.ActionCode),
            ActorId.Create(_userContext.UserId));

        if (result.IsFailure) return result;

        await _repository.UpdateAsync(suite, cancellationToken);
        await _repository.UnitOfWork.SaveEntitiesAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed class UnlinkNodeActionCommandValidator : AbstractValidator<UnlinkNodeActionCommand>
{
    public UnlinkNodeActionCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;
        RuleFor(x => x.SystemSuiteId).NotEmpty();
        RuleFor(x => x.ModuleId).NotEmpty();
        RuleFor(x => x.NodeId).NotEmpty();
        RuleFor(x => x.ActionCode).NotEmpty();
    }
}
