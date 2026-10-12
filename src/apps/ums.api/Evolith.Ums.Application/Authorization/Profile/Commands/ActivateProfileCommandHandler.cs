using Evolith.Ums.Application.Authorization.Profile.DTOs;

namespace Evolith.Ums.Application.Authorization.Profile.Commands;

using Evolith.Ums.Domain.Authorization;

public sealed class ActivateProfileCommandHandler : ICommandHandler<ActivateProfileCommand>
{
    private readonly IProfileRepository _profileRepository;
    private readonly IUserContext _userContext;

    public ActivateProfileCommandHandler(
        IProfileRepository profileRepository,
        IUserContext userContext)
    {
        _profileRepository = profileRepository;
        _userContext = userContext;
    }

    [AuthorizationAspect(ResourceCode = "manage_profiles", ActionCode = "manage")]
    [AuditTrail]
    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]
    public async Task<Result> Handle(ActivateProfileCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_userContext.UserId))
        {
            return Result.Failure("Authenticated user is required to activate a profile.");
        }

        var profile = await _profileRepository.GetByIdAsync(request.ProfileId, cancellationToken);
        if (profile is null)
        {
            return Result.Failure("Profile was not found.");
        }

        var result = profile.Activate(ActorId.Create(_userContext.UserId));
        if (result.IsFailure)
        {
            return result;
        }

        await _profileRepository.UpdateAsync(profile, cancellationToken);
        await _profileRepository.UnitOfWork.SaveEntitiesAsync(cancellationToken);

        return Result.Success();
    }
}
