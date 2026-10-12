using Evolith.Ums.Domain.Authorization;
using Evolith.Ums.Domain.Authorization.Profile;
using Evolith.Ums.Domain.Authorization.Template;

namespace Evolith.Ums.Application.Authorization.Profile.Commands;

public sealed class AssignTemplateToProfileCommandHandler : ICommandHandler<AssignTemplateToProfileCommand>
{
    private readonly IProfileRepository _profileRepository;
    private readonly IPermissionTemplateRepository _templateRepository;
    private readonly IUserContext _userContext;

    public AssignTemplateToProfileCommandHandler(
        IProfileRepository profileRepository,
        IPermissionTemplateRepository templateRepository,
        IUserContext userContext)
    {
        _profileRepository = profileRepository;
        _templateRepository = templateRepository;
        _userContext = userContext;
    }

    [AuthorizationAspect(ResourceCode = "manage_profiles", ActionCode = "manage")]
    [AuditTrail]
    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]
    public async Task<Result> Handle(AssignTemplateToProfileCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_userContext.UserId))
        {
            return Result.Failure("Authenticated user is required to assign a template to a profile.");
        }

        var profile = await _profileRepository.GetByIdAsync(request.ProfileId, cancellationToken);
        if (profile is null)
        {
            return Result.Failure("Profile was not found.");
        }

        var template = await _templateRepository.GetByIdAsync(request.TemplateId, cancellationToken);
        if (template is null)
        {
            return Result.Failure("Permission template was not found.");
        }

        var result = profile.AssignTemplate(template, ActorId.Create(_userContext.UserId));
        if (result.IsFailure)
        {
            return result;
        }

        await _profileRepository.UpdateAsync(profile, cancellationToken);
        await _profileRepository.UnitOfWork.SaveEntitiesAsync(cancellationToken);

        return Result.Success();
    }
}
