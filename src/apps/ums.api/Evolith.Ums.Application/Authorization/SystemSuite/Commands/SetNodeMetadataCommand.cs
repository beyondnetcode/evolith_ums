namespace Evolith.Ums.Application.Authorization.SystemSuite.Commands;

using Evolith.Ums.Domain.Authorization;
using Evolith.Ums.Domain.Authorization.SystemSuite.MenuNode;
using FluentValidation;

/// <summary>Fija (reemplaza) los metadatos de gobernanza SDLC de un nodo (ADR-0090). Todos opcionales.</summary>
public sealed record SetNodeMetadataCommand(
    Guid SystemSuiteId,
    Guid ModuleId,
    Guid NodeId,
    string? Responsable,
    string? Criticidad,
    string? ProductoImpactado,
    string? ComponenteTecnico,
    string? Dependencias,
    string? Evidencias,
    string? TrazabilidadSdlc) : ICommand;

public sealed class SetNodeMetadataCommandHandler : ICommandHandler<SetNodeMetadataCommand>
{
    private readonly ISystemSuiteRepository _repository;
    private readonly IUserContext _userContext;

    public SetNodeMetadataCommandHandler(ISystemSuiteRepository repository, IUserContext userContext)
    {
        _repository = repository;
        _userContext = userContext;
    }

    [AuditTrail]
    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]
    public async Task<Result> Handle(SetNodeMetadataCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_userContext.UserId))
            return Result.Failure("Authenticated user is required.");

        var suite = await _repository.GetByIdAsync(request.SystemSuiteId, cancellationToken);
        if (suite is null) return Result.Failure("System suite not found.");

        var metadata = MenuNodeMetadata.Create(
            request.Responsable,
            request.Criticidad,
            request.ProductoImpactado,
            request.ComponenteTecnico,
            request.Dependencias,
            request.Evidencias,
            request.TrazabilidadSdlc);

        var result = suite.SetModuleNodeMetadata(
            IdValueObject.Load(request.ModuleId),
            IdValueObject.Load(request.NodeId),
            metadata,
            ActorId.Create(_userContext.UserId));

        if (result.IsFailure) return result;

        await _repository.UpdateAsync(suite, cancellationToken);
        await _repository.UnitOfWork.SaveEntitiesAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed class SetNodeMetadataCommandValidator : AbstractValidator<SetNodeMetadataCommand>
{
    public SetNodeMetadataCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;
        RuleFor(x => x.SystemSuiteId).NotEmpty();
        RuleFor(x => x.ModuleId).NotEmpty();
        RuleFor(x => x.NodeId).NotEmpty();
        RuleFor(x => x.Criticidad).MaximumLength(50);
        RuleFor(x => x.Responsable).MaximumLength(200);
        RuleFor(x => x.ProductoImpactado).MaximumLength(200);
        RuleFor(x => x.ComponenteTecnico).MaximumLength(200);
    }
}
