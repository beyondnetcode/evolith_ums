namespace Evolith.Ums.Application.Authorization.SystemSuite.Commands;

using Evolith.Ums.Application.Authorization.SystemSuite.DTOs;
using Evolith.Ums.Domain.Authorization;
using Evolith.Ums.Domain.Authorization.SystemSuite.MenuNode;
using FluentValidation;

/// <summary>Alta de un nodo del árbol recursivo (ADR-0090). ParentNodeId nulo ⇒ nodo raíz del módulo.</summary>
public sealed record AddNodeCommand(
    Guid SystemSuiteId,
    Guid ModuleId,
    Guid? ParentNodeId,
    string Kind,
    string Code,
    string Label,
    string Description,
    int SortOrder,
    string? Icon = null,
    string? Route = null) : ICommand<AddNodeResponse>;

public sealed class AddNodeCommandHandler : ICommandHandler<AddNodeCommand, AddNodeResponse>
{
    private readonly ISystemSuiteRepository _repository;
    private readonly IUserContext _userContext;

    public AddNodeCommandHandler(ISystemSuiteRepository repository, IUserContext userContext)
    {
        _repository = repository;
        _userContext = userContext;
    }

    [AuditTrail]
    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]
    public async Task<Result<AddNodeResponse>> Handle(AddNodeCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_userContext.UserId))
            return Result<AddNodeResponse>.Failure("Authenticated user is required.");

        if (!Enum.TryParse<NodeKind>(request.Kind, ignoreCase: true, out var kind))
            return Result<AddNodeResponse>.Failure($"Invalid node kind '{request.Kind}'.");

        var suite = await _repository.GetByIdAsync(request.SystemSuiteId, cancellationToken);
        if (suite is null) return Result<AddNodeResponse>.Failure("System suite not found.");

        var actor = ActorId.Create(_userContext.UserId);
        var moduleId = IdValueObject.Load(request.ModuleId);
        var code = Code.Create(request.Code);
        var label = Name.Create(request.Label);
        var description = Description.Create(request.Description);

        var presentation = MenuNodePresentation.Create(request.Icon, request.Route);

        var result = request.ParentNodeId is { } parentId
            ? suite.AddModuleChildNode(moduleId, IdValueObject.Load(parentId), kind, code, label, description, request.SortOrder, actor, presentation: presentation)
            : suite.AddModuleRootNode(moduleId, kind, code, label, description, request.SortOrder, actor, presentation: presentation);

        if (result.IsFailure) return Result<AddNodeResponse>.Failure(result.Error);

        await _repository.UpdateAsync(suite, cancellationToken);
        await _repository.UnitOfWork.SaveEntitiesAsync(cancellationToken);
        return Result<AddNodeResponse>.Success(new AddNodeResponse(result.Value));
    }


public sealed class AddNodeCommandValidator : AbstractValidator<AddNodeCommand>
{
    public AddNodeCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x.SystemSuiteId).NotEmpty();
        RuleFor(x => x.ModuleId).NotEmpty();
        RuleFor(x => x.Kind).NotEmpty().WithMessage("Node kind is required.");
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Node code is required.")
            .MaximumLength(100)
            .Matches(@"^[A-Za-z0-9_]+$").WithMessage("Node code may only contain letters, digits, and underscores.");
        RuleFor(x => x.Label).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(1000);
        RuleFor(x => x.SortOrder).GreaterThan(0);
        RuleFor(x => x.Icon).MaximumLength(64);
        // La ruta es relativa: una absoluta permitiría que un menú llevara a otro dominio.
        RuleFor(x => x.Route)
            .MaximumLength(400)
            .Must(r => string.IsNullOrWhiteSpace(r) || r.StartsWith('/'))
            .WithMessage("La ruta debe ser relativa y empezar por '/'.");
    }
}
}
