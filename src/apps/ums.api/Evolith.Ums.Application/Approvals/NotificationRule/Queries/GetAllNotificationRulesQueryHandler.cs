using Evolith.Ums.Application.Approvals.NotificationRule.DTOs;
using Evolith.Ums.Application.Common.Interfaces;
using Evolith.Ums.Domain.Approvals;
using static Evolith.Ums.Application.Common.QueryRequestNormalizer;

namespace Evolith.Ums.Application.Approvals.NotificationRule.Queries;

public sealed class GetAllNotificationRulesQueryHandler : IQueryHandler<GetAllNotificationRulesQuery, PagedResult<NotificationRuleDto>>
{
    private readonly INotificationRuleRepository _repository;
    private readonly ITenantContext? _tenantContext;

    public GetAllNotificationRulesQueryHandler(INotificationRuleRepository repository, ITenantContext? tenantContext = null)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]

    [AuthorizationAspect(ResourceCode = "view_approval_rules", ActionCode = "view")]
    public async Task<Result<PagedResult<NotificationRuleDto>>> Handle(GetAllNotificationRulesQuery request, CancellationToken cancellationToken)
    {
        var page = NormalizePage(request.Page);
        var pageSize = NormalizePageSize(request.PageSize);
        var status = NormalizeText(request.Status, "all");
        var sortBy = NormalizeText(request.SortBy, "channel").ToLowerInvariant();
        var sortOrder = NormalizeText(request.SortOrder, "asc").ToLowerInvariant();

        var effectiveTenantId = (_tenantContext?.IsInternalAdmin == true)
            ? request.TenantId
            : _tenantContext?.OrganizationId;

        var items = effectiveTenantId.HasValue
            ? await _repository.GetByTenantIdAsync(effectiveTenantId.Value, cancellationToken)
            : await _repository.GetAllAsync(null, cancellationToken);

        var query = items.Select(r => new NotificationRuleDto(
            r.Props.Id.GetValue(), r.Props.TenantId.GetValue(), r.Props.Channel.ToString(),
            r.Props.Recipient.GetValue(), r.Props.IsActive));

        if (!string.Equals(status, "all", StringComparison.OrdinalIgnoreCase))
        {
            var isActive = string.Equals(status, "active", StringComparison.OrdinalIgnoreCase);
            query = query.Where(r => r.IsActive == isActive);
        }

        query = (sortBy, sortOrder) switch
        {
            ("channel", "desc") => query.OrderByDescending(r => r.Channel),
            _ => query.OrderBy(r => r.Channel),
        };

        var totalItems = query.Count();
        var totalPages = totalItems == 0 ? 0 : (int)Math.Ceiling(totalItems / (double)pageSize);
        var paged = query.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return Result<PagedResult<NotificationRuleDto>>.Success(new PagedResult<NotificationRuleDto>(paged, page, pageSize, totalItems, totalPages));
    }
}
