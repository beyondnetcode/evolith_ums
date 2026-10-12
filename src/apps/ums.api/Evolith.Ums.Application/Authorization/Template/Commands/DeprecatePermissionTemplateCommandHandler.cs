namespace Evolith.Ums.Application.Authorization.Template.Commands;

using Evolith.Ums.Domain.Authorization;
using Evolith.Ums.Domain.Kernel;

public sealed class DeprecatePermissionTemplateCommandHandler
    : ICommandHandler<DeprecatePermissionTemplateCommand>
{
    private readonly IPermissionTemplateRepository _repository;
    private readonly IProfileRepository _profileRepository;
    private readonly IUserContext _userContext;

    public DeprecatePermissionTemplateCommandHandler(
        IPermissionTemplateRepository repository,
        IProfileRepository profileRepository,
        IUserContext userContext)
    {
        _repository = repository;
        _profileRepository = profileRepository;
        _userContext = userContext;
    }

    [AuthorizationAspect(ResourceCode = "manage_templates", ActionCode = "manage")]
    [AuditTrail]
    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]
    public async Task<Result> Handle(
        DeprecatePermissionTemplateCommand request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_userContext.UserId))
            return Result.Failure("Authenticated user is required.");

        var template = await _repository.GetByIdAsync(request.TemplateId, cancellationToken);
        if (template is null) return Result.Failure("Template not found.");

        // ── Dependency guard: active profiles using this template ──────────────
        var activeProfileCount = await _profileRepository.CountActiveByTemplateAsync(
            request.TemplateId, cancellationToken);

        if (activeProfileCount > 0)
        {
            var deps = new List<BlockingDependency>
            {
                new("Profile", "Active", activeProfileCount),
            };
            return Result.Failure(BlockedOperationError.Encode(
                DomainErrors.Authorization.TemplateHasActiveProfiles, deps));
        }

        var result = template.Deprecate(ActorId.Create(_userContext.UserId));
        if (result.IsFailure) return result;

        await _repository.UpdateAsync(template, cancellationToken);
        await _repository.UnitOfWork.SaveEntitiesAsync(cancellationToken);
        return Result.Success();
    }
}
