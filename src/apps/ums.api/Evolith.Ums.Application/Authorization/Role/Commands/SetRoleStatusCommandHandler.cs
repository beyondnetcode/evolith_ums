using Evolith.Ums.Domain.Authorization;
using Evolith.Ums.Domain.Kernel;

namespace Evolith.Ums.Application.Authorization.Role.Commands;

public sealed class SetRoleStatusCommandHandler : ICommandHandler<SetRoleStatusCommand>
{
    private readonly IRoleRepository _repository;
    private readonly IProfileRepository _profileRepository;
    private readonly IUserContext _userContext;

    public SetRoleStatusCommandHandler(
        IRoleRepository repository,
        IProfileRepository profileRepository,
        IUserContext userContext)
    {
        _repository = repository;
        _profileRepository = profileRepository;
        _userContext = userContext;
    }

    [AuthorizationAspect(ResourceCode = "manage_roles", ActionCode = "manage")]
    [AuditTrail]
    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]
    public async Task<Result> Handle(SetRoleStatusCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_userContext.UserId))
        {
            return Result.Failure("Authenticated user is required to change a role status.");
        }

        var role = await _repository.GetByIdAsync(request.RoleId, cancellationToken);
        if (role is null)
        {
            return Result.Failure("No se pudo cambiar el estado porque el rol no existe.");
        }

        // ── Dependency guard (deactivate only) ────────────────────────────────
        if (!request.IsActive)
        {
            var activeProfileCount = await _profileRepository.CountActiveByRoleAsync(
                request.RoleId, cancellationToken);

            var activeChildCount = await _repository.CountActiveChildRolesAsync(
                request.RoleId, cancellationToken);

            if (activeProfileCount > 0 || activeChildCount > 0)
            {
                var deps = new List<BlockingDependency>();
                if (activeProfileCount > 0)
                    deps.Add(new("Profile", "Active", activeProfileCount));
                if (activeChildCount > 0)
                    deps.Add(new("Role", "Active", activeChildCount));

                var errorCode = activeProfileCount > 0
                    ? DomainErrors.Authorization.RoleHasActiveProfiles
                    : DomainErrors.Authorization.RoleHasActiveChildRoles;

                return Result.Failure(BlockedOperationError.Encode(errorCode, deps));
            }
        }

        var actor = ActorId.Create(_userContext.UserId);
        var result = request.IsActive ? role.Activate(actor) : role.Deactivate(actor);
        if (result.IsFailure)
        {
            return result;
        }

        await _repository.UpdateAsync(role, cancellationToken);
        await _repository.UnitOfWork.SaveEntitiesAsync(cancellationToken);
        return Result.Success();
    }
}
