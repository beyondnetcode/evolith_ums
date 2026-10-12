using Evolith.Ums.Application.Identity.Tenant.Branch.DTOs;
using Evolith.Ums.Domain.Authorization;

namespace Evolith.Ums.Application.Identity.Tenant.Branch.Commands;

/// <summary>
/// Cierra definitivamente una sucursal (ADR-0164). Antes esto la BORRABA de la base.
///
/// <para><b>Guarda de cascada.</b> Se cuentan las referencias VIVAS a la sucursal en los dos
/// agregados que la apuntan —cuentas y perfiles— y, si hay alguna, se rechaza con 409 nombrando
/// cuántas de cada clase. Es imprescindible que la haga la aplicación: ni <c>Profiles.BranchId</c>
/// ni <c>UserAccounts.BranchId</c> tienen clave ajena contra <c>TenantBranches</c>, así que la base
/// nunca habría dicho nada — de hecho el borrado físico anterior huerfanizaba esas filas en
/// silencio.</para>
///
/// <para>Lo ya eliminado no bloquea: los dos recuentos miran solo lo activo (§2.2).</para>
/// </summary>
public sealed class CloseBranchCommandHandler : ICommandHandler<CloseBranchCommand, CloseBranchResponse>
{
    private readonly ITenantRepository _tenantRepository;
    private readonly IUserAccountRepository _userAccountRepository;
    private readonly IProfileRepository _profileRepository;
    private readonly IUserContext _userContext;
    private readonly ITenantScopePolicy _tenantScopePolicy;

    public CloseBranchCommandHandler(
        ITenantRepository tenantRepository,
        IUserAccountRepository userAccountRepository,
        IProfileRepository profileRepository,
        IUserContext userContext,
        ITenantScopePolicy tenantScopePolicy)
    {
        _tenantRepository = tenantRepository;
        _userAccountRepository = userAccountRepository;
        _profileRepository = profileRepository;
        _userContext = userContext;
        _tenantScopePolicy = tenantScopePolicy;
    }

    [AuthorizationAspect(ResourceCode = "manage_tenants", ActionCode = "manage")]
    [AuditTrail]
    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]
    public async Task<Result<CloseBranchResponse>> Handle(
        CloseBranchCommand request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_userContext.UserId))
        {
            return Result<CloseBranchResponse>.Failure("Authenticated user is required to close a branch.");
        }

        var tenant = await _tenantRepository.GetByIdAsync(request.TenantId, cancellationToken);
        if (tenant is null)
        {
            return Result<CloseBranchResponse>.Failure("Tenant was not found.");
        }

        var scopeResult = await _tenantScopePolicy.EnsureManagementOwnerScopeAsync(request.TenantId, cancellationToken);
        if (scopeResult.IsFailure)
        {
            return Result<CloseBranchResponse>.Failure(scopeResult.Error);
        }

        // ── Guarda de cascada: referencias vivas a la sucursal (ADR-0164 §2.2) ──
        var activeUserCount = await _userAccountRepository.CountActiveByBranchAsync(
            request.BranchId, cancellationToken);
        var activeProfileCount = await _profileRepository.CountActiveByBranchAsync(
            request.BranchId, cancellationToken);

        if (activeUserCount > 0 || activeProfileCount > 0)
        {
            // Se enumeran las DOS clases en la misma respuesta, no la primera que aparece: quien
            // opera necesita saber todo lo que tiene que resolver antes de reintentar.
            var deps = new List<BlockingDependency>();
            if (activeUserCount > 0) deps.Add(new BlockingDependency("UserAccount", "Active", activeUserCount));
            if (activeProfileCount > 0) deps.Add(new BlockingDependency("Profile", "Active", activeProfileCount));

            return Result<CloseBranchResponse>.Failure(
                BlockedOperationError.Encode(DomainErrors.Tenant.BranchHasLiveReferences, deps));
        }

        // El dominio revalida los recuentos: la guarda de arriba existe para poder adjuntar el
        // desglose, no para sustituir a la invariante.
        var result = tenant.CloseBranch(
            IdValueObject.Load(request.BranchId),
            ActorId.Create(_userContext.UserId),
            activeUserCount,
            activeProfileCount,
            request.Reason);

        if (result.IsFailure)
        {
            return Result<CloseBranchResponse>.Failure(result.Error);
        }

        await _tenantRepository.UpdateAsync(tenant, cancellationToken);
        await _tenantRepository.UnitOfWork.SaveEntitiesAsync(cancellationToken);

        return Result<CloseBranchResponse>.Success(new CloseBranchResponse(request.TenantId));
    }
}
