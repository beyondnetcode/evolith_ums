namespace Evolith.Ums.Application.Identity.Tenant.Commands;

public sealed class SetManagementOwnerCommandHandler : ICommandHandler<SetManagementOwnerCommand>
{
    private readonly ITenantRepository _tenantRepository;
    private readonly IUserContext _userContext;

    public SetManagementOwnerCommandHandler(
        ITenantRepository tenantRepository,
        IUserContext userContext)
    {
        _tenantRepository = tenantRepository;
        _userContext = userContext;
    }

    [AuthorizationAspect(ResourceCode = "manage_tenants", ActionCode = "manage")]
    [AuditTrail]
    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]
    public async Task<Result> Handle(SetManagementOwnerCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_userContext.UserId))
        {
            return Result.Failure("Authenticated user is required.");
        }

        var tenant = await _tenantRepository.GetByIdAsync(request.TenantId, cancellationToken);
        if (tenant is null)
        {
            return Result.Failure("Tenant was not found.");
        }

        // G-045: la propiedad de gestión (management owner) es única en todo el sistema.
        // Existe un índice parcial único en BD (IX_Tenants_SingleManagementOwner). Sin este
        // chequeo explícito, otorgarla a un segundo tenant provoca una violación de índice
        // en SaveEntitiesAsync que se traduce en 500. Verificamos la unicidad ANTES de
        // persistir y devolvemos un Result.Failure que el mapeo de presentación resuelve a 409.
        if (request.Value)
        {
            var allTenants = await _tenantRepository.GetAllAsync(null, cancellationToken);
            var existingOwner = allTenants.FirstOrDefault(candidate =>
                candidate.IsManagementOwner &&
                candidate.Props.Id.GetValue() != request.TenantId);

            if (existingOwner is not null)
            {
                return Result.Failure(DomainErrors.Tenant.ManagementOwnerAlreadyExists);
            }
        }

        var result = tenant.SetManagementOwner(request.Value, ActorId.Create(_userContext.UserId));
        if (result.IsFailure)
        {
            return result;
        }

        await _tenantRepository.UpdateAsync(tenant, cancellationToken);
        await _tenantRepository.UnitOfWork.SaveEntitiesAsync(cancellationToken);

        return Result.Success();
    }
}
