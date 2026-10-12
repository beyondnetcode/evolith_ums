using Evolith.Ums.Application.Approvals.AccessEnforcementPolicy.DTOs;
using Evolith.Ums.Application.Common.Interfaces;
using Evolith.Ums.Domain.Approvals;
using static Evolith.Ums.Application.Common.QueryRequestNormalizer;

namespace Evolith.Ums.Application.Approvals.AccessEnforcementPolicy.Queries;

public sealed class GetAllAccessEnforcementPoliciesQueryHandler : IQueryHandler<GetAllAccessEnforcementPoliciesQuery, PagedResult<AccessEnforcementPolicyDto>>
{
    private readonly IAccessEnforcementPolicyRepository _repository;
    private readonly ITenantContext? _tenantContext;

    public GetAllAccessEnforcementPoliciesQueryHandler(IAccessEnforcementPolicyRepository repository, ITenantContext? tenantContext = null)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]

    [AuthorizationAspect(ResourceCode = "view_approval_rules", ActionCode = "view")]
    public async Task<Result<PagedResult<AccessEnforcementPolicyDto>>> Handle(GetAllAccessEnforcementPoliciesQuery request, CancellationToken cancellationToken)
    {
        var page = NormalizePage(request.Page);
        var pageSize = NormalizePageSize(request.PageSize);
        var status = NormalizeText(request.Status, "all");
        var sortBy = NormalizeText(request.SortBy, "enforcementaction").ToLowerInvariant();
        var sortOrder = NormalizeText(request.SortOrder, "asc").ToLowerInvariant();

        var effectiveTenantId = (_tenantContext?.IsInternalAdmin == true)
            ? request.TenantId
            : _tenantContext?.OrganizationId;

        var items = effectiveTenantId.HasValue
            ? await _repository.GetByTenantIdAsync(effectiveTenantId.Value, cancellationToken)
            : await _repository.GetAllAsync(null, cancellationToken);

        var query = items.Select(p => new AccessEnforcementPolicyDto(
            p.Props.Id.GetValue(), p.Props.TenantId.GetValue(), p.Props.ProfileId?.GetValue(),
            p.Props.RoleId?.GetValue(), p.Props.EnforcementAction.ToString(), p.Props.IsActive,
            p.Props.GracePeriodDays));

        if (!string.Equals(status, "all", StringComparison.OrdinalIgnoreCase))
        {
            var isActive = string.Equals(status, "active", StringComparison.OrdinalIgnoreCase);
            query = query.Where(p => p.IsActive == isActive);
        }

        query = (sortBy, sortOrder) switch
        {
            ("action", "desc") => query.OrderByDescending(p => p.EnforcementAction),
            _ => query.OrderBy(p => p.EnforcementAction),
        };

        var totalItems = query.Count();
        var totalPages = totalItems == 0 ? 0 : (int)Math.Ceiling(totalItems / (double)pageSize);
        var paged = query.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return Result<PagedResult<AccessEnforcementPolicyDto>>.Success(new PagedResult<AccessEnforcementPolicyDto>(paged, page, pageSize, totalItems, totalPages));
    }
}
