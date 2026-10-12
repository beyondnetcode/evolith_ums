using Evolith.Ums.Application.Approvals.NotificationRule.DTOs;

namespace Evolith.Ums.Application.Approvals.NotificationRule.Queries;

public sealed record GetAllNotificationRulesQuery(
    int Page = 1, int PageSize = 20, string? Search = null, string Criteria = "channel",
    string Status = "all", string SortBy = "channel", string SortOrder = "asc",
    Guid? TenantId = null) : IQuery<PagedResult<NotificationRuleDto>>;
