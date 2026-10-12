using Evolith.Ums.Application.Authorization.Template.DTOs;

namespace Evolith.Ums.Application.Authorization.Template.Commands;

using Evolith.Ums.Domain.Authorization;

public sealed class PublishPermissionTemplateCommandHandler : ICommandHandler<PublishPermissionTemplateCommand>
{
    private readonly IPermissionTemplateRepository _templateRepository;
    private readonly IUserContext _userContext;

    public PublishPermissionTemplateCommandHandler(
        IPermissionTemplateRepository templateRepository,
        IUserContext userContext)
    {
        _templateRepository = templateRepository;
        _userContext = userContext;
    }

    [AuthorizationAspect(ResourceCode = "manage_templates", ActionCode = "manage")]
    [AuditTrail]
    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]
    public async Task<Result> Handle(PublishPermissionTemplateCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_userContext.UserId))
        {
            return Result.Failure("Authenticated user is required to publish a template.");
        }

        var template = await _templateRepository.GetByIdAsync(request.TemplateId, cancellationToken);
        if (template is null)
        {
            return Result.Failure("Template was not found.");
        }

        var result = template.Publish(ActorId.Create(_userContext.UserId));
        if (result.IsFailure)
        {
            return result;
        }

        await _templateRepository.UpdateAsync(template, cancellationToken);
        await _templateRepository.UnitOfWork.SaveEntitiesAsync(cancellationToken);

        return Result.Success();
    }
}
