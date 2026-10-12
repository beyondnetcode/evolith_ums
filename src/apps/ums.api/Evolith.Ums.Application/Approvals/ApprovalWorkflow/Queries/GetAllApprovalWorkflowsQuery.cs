using Evolith.Ums.Application.Approvals.ApprovalWorkflow.DTOs;

namespace Evolith.Ums.Application.Approvals.ApprovalWorkflow.Queries;

public sealed record GetAllApprovalWorkflowsQuery(
    int Page = QueryDefaults.Page, int PageSize = QueryDefaults.PageSize, string? Search = null, string? Criteria = null,
    string? SortBy = null, string? SortOrder = null, Guid? TenantId = null) : IQuery<PagedResult<ApprovalWorkflowDto>>;
