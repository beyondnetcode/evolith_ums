using Evolith.Ums.Application.Identity.Tenant.Branch.DTOs;

namespace Evolith.Ums.Application.Identity.Tenant.Branch.Commands;

public sealed class AddBranchCommandHandler : ICommandHandler<AddBranchCommand, AddBranchResponse>
{
    private readonly ITenantRepository _tenantRepository;
    private readonly IUserContext _userContext;
    private readonly ITenantScopePolicy _tenantScopePolicy;

    public AddBranchCommandHandler(
        ITenantRepository tenantRepository,
        IUserContext userContext,
        ITenantScopePolicy tenantScopePolicy)
    {
        _tenantRepository = tenantRepository;
        _userContext = userContext;
        _tenantScopePolicy = tenantScopePolicy;
    }

    [AuthorizationAspect(ResourceCode = "manage_tenants", ActionCode = "manage")]
    [AuditTrail]
    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]
    public async Task<Result<AddBranchResponse>> Handle(
        AddBranchCommand request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_userContext.UserId))
        {
            return Result<AddBranchResponse>.Failure("Authenticated user is required to add a branch.");
        }

        var tenant = await _tenantRepository.GetByIdAsync(request.TenantId, cancellationToken);
        if (tenant is null)
        {
            return Result<AddBranchResponse>.Failure("Tenant was not found.");
        }

        var scopeResult = await _tenantScopePolicy.EnsureManagementOwnerScopeAsync(request.TenantId, cancellationToken);
        if (scopeResult.IsFailure)
        {
            return Result<AddBranchResponse>.Failure(scopeResult.Error);
        }

        // G-161: verificación de unicidad AUTORITATIVA (ignora el filtro global por inquilino) ANTES de
        // materializar. La guarda en memoria de `Tenant.AddBranch` es correcta cuando el actor opera en
        // su propio inquilino, pero cuando un admin interno provisiona sobre OTRO inquilino el filtro
        // global vacía `tenant.Branches` y no vería el duplicado → antes devolvía 201 (no-op engañoso)
        // en vez de 409. La BD garantiza la integridad (índice único TenantId+Code), pero el contrato
        // debe ser consistente: se rechaza aquí con el mismo error de dominio.
        if (await _tenantRepository.BranchCodeExistsAsync(request.TenantId, request.Code, cancellationToken))
        {
            return Result<AddBranchResponse>.Failure(DomainErrors.Tenant.BranchCodeNotUnique);
        }

        var result = tenant.AddBranch(
            Code.Create(request.Code),
            Name.Create(request.Name),
            ActorId.Create(_userContext.UserId),
            string.IsNullOrWhiteSpace(request.GeofencingMetadata) ? null : Value.Create(request.GeofencingMetadata));

        if (result.IsFailure)
        {
            return Result<AddBranchResponse>.Failure(result.Error);
        }

        await _tenantRepository.UpdateAsync(tenant, cancellationToken);
        await _tenantRepository.UnitOfWork.SaveEntitiesAsync(cancellationToken);

        var branch = result.Value;
        return Result<AddBranchResponse>.Success(new AddBranchResponse(
            request.TenantId,
            branch.GetId().GetValue(),
            branch.Code.GetValue()));
    }
}
