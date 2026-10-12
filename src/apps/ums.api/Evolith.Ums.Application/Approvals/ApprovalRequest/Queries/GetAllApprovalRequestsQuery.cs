using Evolith.Ums.Application.Approvals.ApprovalRequest.DTOs;

namespace Evolith.Ums.Application.Approvals.ApprovalRequest.Queries;

public sealed record GetAllApprovalRequestsQuery(
    int Page = 1, int PageSize = 20, string? Search = null, string Criteria = "status",
    string Status = "all", string SortBy = "status", string SortOrder = "asc",
    Guid? TenantId = null, Guid? UserId = null) : IQuery<PagedResult<ApprovalRequestDto>>;
