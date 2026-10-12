using Evolith.Ums.Application.Identity.Tenant.Branch.DTOs;
using Evolith.Ums.Domain.Identity.Tenant;

namespace Evolith.Ums.Application.Identity.Tenant.Branch.Queries;

public sealed class GetBranchLifecycleQueryHandler : IQueryHandler<GetBranchLifecycleQuery, IReadOnlyList<BranchLifecycleEntryDto>>
{
    private readonly ITenantRepository _tenantRepository;

    public GetBranchLifecycleQueryHandler(ITenantRepository tenantRepository)
    {
        _tenantRepository = tenantRepository;
    }

    [AuthorizationAspect(ResourceCode = "view_tenants", ActionCode = "view")]
    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]
    public async Task<Result<IReadOnlyList<BranchLifecycleEntryDto>>> Handle(
        GetBranchLifecycleQuery request,
        CancellationToken cancellationToken)
    {
        var tenant = await _tenantRepository.GetByIdAsync(request.TenantId, cancellationToken);
        if (tenant is null)
        {
            return Result<IReadOnlyList<BranchLifecycleEntryDto>>.Failure("Tenant not found.");
        }

        // La resolución por id SÍ devuelve las cerradas (ADR-0164): preguntar por el pasado de una
        // sucursal cerrada es exactamente para lo que existe la bitácora, así que aquí no se filtra
        // por `IsClosed`. Lo que se comprueba es que la sucursal PERTENEZCA a este inquilino, para
        // que el identificador de otro no sirva de sonda.
        var branch = tenant.Branches.FirstOrDefault(b => b.GetId().GetValue() == request.BranchId);
        if (branch is null)
        {
            return Result<IReadOnlyList<BranchLifecycleEntryDto>>.Failure(DomainErrors.Tenant.BranchNotFound);
        }

        var entries = await _tenantRepository.GetBranchLifecycleAsync(
            request.TenantId, request.BranchId, cancellationToken);

        var dtos = entries
            .Select(e => new BranchLifecycleEntryDto(
                e.Id,
                e.Episode.Name,
                e.OccurredAtUtc,
                e.ActorId,
                e.NameSnapshot,
                e.GeofencingSnapshot,
                e.Reason))
            .ToList();

        return Result<IReadOnlyList<BranchLifecycleEntryDto>>.Success(dtos.AsReadOnly());
    }
}
