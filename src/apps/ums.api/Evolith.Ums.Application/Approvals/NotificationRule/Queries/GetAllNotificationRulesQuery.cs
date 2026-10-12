using Evolith.Ums.Application.Approvals.NotificationRule.DTOs;

namespace Evolith.Ums.Application.Approvals.NotificationRule.Queries;

public sealed record GetAllNotificationRulesQuery(
    int Page = QueryDefaults.Page, int PageSize = QueryDefaults.PageSize, string? Search = null, string? Criteria = null,
    string? Status = null, string? SortBy = null, string? SortOrder = null,
    Guid? TenantId = null) : IQuery<PagedResult<NotificationRuleDto>>;
