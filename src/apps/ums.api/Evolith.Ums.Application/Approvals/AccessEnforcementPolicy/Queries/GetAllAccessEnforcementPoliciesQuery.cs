using Evolith.Ums.Application.Approvals.AccessEnforcementPolicy.DTOs;

namespace Evolith.Ums.Application.Approvals.AccessEnforcementPolicy.Queries;

public sealed record GetAllAccessEnforcementPoliciesQuery(
    int Page = QueryDefaults.Page, int PageSize = QueryDefaults.PageSize, string? Search = null, string? Criteria = null,
    string? Status = null, string? SortBy = null, string? SortOrder = null,
    Guid? TenantId = null) : IQuery<PagedResult<AccessEnforcementPolicyDto>>;
