namespace Evolith.Ums.Application.Authorization.Template.Commands;

using BeyondNetCode.Shell.Ddd;
using Evolith.Ums.Application.Authorization.Template.DTOs;
using Evolith.Ums.Domain.Authorization;
using Evolith.Ums.Domain.Enums;

public sealed class AddTemplateItemCommandHandler : ICommandHandler<AddTemplateItemCommand, AddTemplateItemResponse>
{
    private readonly IPermissionTemplateRepository _repository;
    private readonly IUserContext _userContext;

    public AddTemplateItemCommandHandler(
        IPermissionTemplateRepository repository,
        IUserContext userContext)
    {
        _repository = repository;
        _userContext = userContext;
    }

    [AuthorizationAspect(ResourceCode = "manage_templates", ActionCode = "manage")]
    [AuditTrail]
    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]
    public async Task<Result<AddTemplateItemResponse>> Handle(
        AddTemplateItemCommand request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_userContext.UserId))
            return Result<AddTemplateItemResponse>.Failure("Authenticated user is required.");

        var template = await _repository.GetByIdAsync(request.TemplateId, cancellationToken);
        if (template is null) return Result<AddTemplateItemResponse>.Failure("Template not found.");

        // G-192 — se resuelve con el parseador común (recorta y no distingue mayúsculas) y el
        // mensaje enumera los destinos que el DOMINIO declara. Antes usaba FromDisplayName, que
        // exige coincidencia exacta, y recitaba una lista de cuatro que el enumerado ya había
        // dejado atrás: «Aggregate» y «Entity» son destinos legítimos del arco exclusivo.
        var targetType = DomainEnumerationParser.FromName<ExclusiveArcTarget>(request.TargetType);
        if (targetType is null)
            return Result<AddTemplateItemResponse>.Failure(
                $"Invalid target type '{request.TargetType}'. Valid: {string.Join(", ", AddTemplateItemCommandValidator.ValidTargetTypes)}.");

        var result = template.AddItem(
            targetType,
            IdValueObject.Load(request.TargetId),
            ActionId.Load(request.ActionId),
            request.IsAllowed,
            request.IsDenied,
            ActorId.Create(_userContext.UserId));

        if (result.IsFailure) return Result<AddTemplateItemResponse>.Failure(result.Error);

        await _repository.UpdateAsync(template, cancellationToken);
        await _repository.UnitOfWork.SaveEntitiesAsync(cancellationToken);
        return Result<AddTemplateItemResponse>.Success(new AddTemplateItemResponse(result.Value));
    }
}
