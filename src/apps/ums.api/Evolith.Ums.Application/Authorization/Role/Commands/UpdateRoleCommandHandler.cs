using Evolith.Ums.Domain.Authorization;

namespace Evolith.Ums.Application.Authorization.Role.Commands;

public sealed class UpdateRoleCommandHandler : ICommandHandler<UpdateRoleCommand>
{
    private readonly IRoleRepository _repository;
    private readonly IUserContext _userContext;

    public UpdateRoleCommandHandler(IRoleRepository repository, IUserContext userContext)
    {
        _repository = repository;
        _userContext = userContext;
    }

    [AuthorizationAspect(ResourceCode = "manage_roles", ActionCode = "manage")]
    [AuditTrail]
    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]
    public async Task<Result> Handle(UpdateRoleCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_userContext.UserId))
        {
            return Result.Failure("Authenticated user is required to update a role.");
        }

        var role = await _repository.GetByIdAsync(request.RoleId, cancellationToken);
        if (role is null)
        {
            return Result.Failure("No se pudo actualizar el rol porque ya no existe.");
        }

        if (request.ParentRoleId == request.RoleId)
        {
            return Result.Failure("No se pudo actualizar el rol porque un rol no puede ser su propio padre.");
        }

        if (request.ParentRoleId.HasValue)
        {
            var parent = await _repository.GetByIdAsync(request.ParentRoleId.Value, cancellationToken);
            if (parent is null || parent.SystemSuiteId.GetValue() != role.SystemSuiteId.GetValue())
            {
                return Result.Failure("No se pudo actualizar el rol porque el rol padre no pertenece a la misma suite.");
            }

            if (request.HierarchyLevel != parent.HierarchyLevel + 1)
            {
                return Result.Failure($"No se pudo actualizar el rol porque el nivel jerárquico debe ser {parent.HierarchyLevel + 1} para el rol padre seleccionado.");
            }

            var visited = new HashSet<Guid>();
            while (parent is not null && parent.ParentRoleId is not null && visited.Add(parent.GetId().GetValue()))
            {
                if (parent.ParentRoleId.GetValue() == role.GetId().GetValue())
                {
                    return Result.Failure("No se pudo actualizar el rol porque la jerarquía seleccionada produciría un ciclo.");
                }

                parent = await _repository.GetByIdAsync(parent.ParentRoleId.GetValue(), cancellationToken);
            }
        }

        var result = role.Update(
            Name.Create(request.Value),
            Description.Create(request.Description),
            request.ParentRoleId.HasValue ? RoleId.Load(request.ParentRoleId.Value) : null,
            request.HierarchyLevel,
            request.PromotionOrder,
            ActorId.Create(_userContext.UserId));
        if (result.IsFailure)
        {
            return result;
        }

        await _repository.UpdateAsync(role, cancellationToken);
        await _repository.UnitOfWork.SaveEntitiesAsync(cancellationToken);
        return Result.Success();
    }
}
