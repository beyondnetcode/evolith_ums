using Evolith.Ums.Application.Approvals.ApprovalRequest.DTOs;

namespace Evolith.Ums.Application.Approvals.ApprovalRequest.Queries;

public sealed record GetAllApprovalRequestsQuery(
    int Page = QueryDefaults.Page, int PageSize = QueryDefaults.PageSize, string? Search = null, string? Criteria = null,
    string? Status = null, string? SortBy = null, string? SortOrder = null,
    Guid? TenantId = null, Guid? UserId = null) : IQuery<PagedResult<ApprovalRequestDto>>;
