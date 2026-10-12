using Evolith.Ums.Application.Approvals.AccessEnforcementPolicy.DTOs;

namespace Evolith.Ums.Application.Approvals.AccessEnforcementPolicy.Queries;

public sealed record GetAllAccessEnforcementPoliciesQuery(
    int Page = 1, int PageSize = 20, string? Search = null, string Criteria = "enforcementAction",
    string Status = "all", string SortBy = "enforcementAction", string SortOrder = "asc",
    Guid? TenantId = null) : IQuery<PagedResult<AccessEnforcementPolicyDto>>;
