namespace Evolith.Ums.Application.Authorization.Template.Commands;

using Evolith.Ums.Domain.Authorization;

public sealed class SetTemplateItemEffectCommandHandler : ICommandHandler<SetTemplateItemEffectCommand>
{
    private readonly IPermissionTemplateRepository _repository;
    private readonly IUserContext _userContext;

    public SetTemplateItemEffectCommandHandler(
        IPermissionTemplateRepository repository,
        IUserContext userContext)
    {
        _repository = repository;
        _userContext = userContext;
    }

    [AuthorizationAspect(ResourceCode = "manage_templates", ActionCode = "manage")]
    [AuditTrail]
    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]
    public async Task<Result> Handle(
        SetTemplateItemEffectCommand request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_userContext.UserId))
            return Result.Failure("Authenticated user is required.");

        var template = await _repository.GetByIdAsync(request.TemplateId, cancellationToken);
        if (template is null) return Result.Failure("Template not found.");

        var itemId = IdValueObject.Load(request.ItemId);
        var actor = ActorId.Create(_userContext.UserId);

        var result = request.Effect.ToUpperInvariant() switch
        {
            "ALLOW"   => template.SetItemAllow(itemId, actor),
            "DENY"    => template.SetItemDeny(itemId, actor),
            "NEUTRAL" => template.SetItemNeutral(itemId, actor),
            _ => Result.Failure($"Invalid effect '{request.Effect}'. Must be Allow, Deny, or Neutral.")
        };

        if (result.IsFailure) return result;

        await _repository.UpdateAsync(template, cancellationToken);
        await _repository.UnitOfWork.SaveEntitiesAsync(cancellationToken);
        return Result.Success();
    }
}
