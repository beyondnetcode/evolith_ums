using Evolith.Ums.Application.Approvals.ApprovalRequest.DTOs;
using Evolith.Ums.Application.Common.Interfaces;
using Evolith.Ums.Domain.Approvals;
using Evolith.Ums.Domain.Enums;
using static Evolith.Ums.Application.Common.QueryRequestNormalizer;

namespace Evolith.Ums.Application.Approvals.ApprovalRequest.Queries;

public sealed class GetAllApprovalRequestsQueryHandler : IQueryHandler<GetAllApprovalRequestsQuery, PagedResult<ApprovalRequestDto>>
{
    private readonly IApprovalRequestRepository _repository;
    private readonly ITenantContext? _tenantContext;

    public GetAllApprovalRequestsQueryHandler(IApprovalRequestRepository repository, ITenantContext? tenantContext = null)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]

    [AuthorizationAspect(ResourceCode = "view_approvals", ActionCode = "view")]
    public async Task<Result<PagedResult<ApprovalRequestDto>>> Handle(GetAllApprovalRequestsQuery request, CancellationToken cancellationToken)
    {
        var page = NormalizePage(request.Page);
        var pageSize = NormalizePageSize(request.PageSize);
        var status = NormalizeText(request.Status, "all");
        var sortBy = NormalizeText(request.SortBy, "status").ToLowerInvariant();
        var sortOrder = NormalizeText(request.SortOrder, "asc").ToLowerInvariant();
        var search = NormalizeSearch(request.Search);

        var effectiveTenantId = (_tenantContext?.IsInternalAdmin == true)
            ? request.TenantId
            : _tenantContext?.OrganizationId;

        var items = effectiveTenantId.HasValue
            ? await _repository.GetByTenantIdAsync(effectiveTenantId.Value, cancellationToken)
            : await _repository.GetAllAsync(null, cancellationToken);

        var query = items.Select(r =>
        {
            var audit = r.Props.Audit.GetValue();
            return new ApprovalRequestDto(
                r.Props.Id.GetValue(), r.Props.WorkflowId.GetValue(), r.Props.TargetUserId.GetValue(),
                r.Props.TargetProfileId?.GetValue(), ToBusinessStatus(r.Props.Status),
                r.RequestedSystemId.GetValue(), r.RequestedBranchId?.GetValue(), r.RequestedRoleId.GetValue(),
                r.Justification, r.GrantedRoleId?.GetValue(), r.DecisionReason,
                audit.UpdatedBy, audit.UpdatedAt);
        });

        if (!string.Equals(status, "all", StringComparison.OrdinalIgnoreCase))
            query = query.Where(r => string.Equals(r.Status, NormalizeStatusFilter(status), StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(r => r.WorkflowId.ToString().Contains(search, StringComparison.OrdinalIgnoreCase));

        // G-159: el parámetro userId de la query se ignoraba (contrato expuesto, filtro no cableado).
        // Se filtra por el usuario objetivo de la solicitud (TargetUserId), que es la semántica útil:
        // «solicitudes de aprobación que conciernen a este usuario».
        if (request.UserId.HasValue)
            query = query.Where(r => r.TargetUserId == request.UserId.Value);

        query = (sortBy, sortOrder) switch
        {
            ("status", "desc") => query.OrderByDescending(r => r.Status),
            _ => query.OrderBy(r => r.Status),
        };

        var totalItems = query.Count();
        var totalPages = totalItems == 0 ? 0 : (int)Math.Ceiling(totalItems / (double)pageSize);
        var paged = query.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return Result<PagedResult<ApprovalRequestDto>>.Success(new PagedResult<ApprovalRequestDto>(paged, page, pageSize, totalItems, totalPages));
    }

    private static string ToBusinessStatus(ApprovalStatus status)
        => status == ApprovalStatus.Rejected ? "Denied" : status.ToString();

    private static string NormalizeStatusFilter(string status)
        => string.Equals(status, ApprovalStatus.Rejected.ToString(), StringComparison.OrdinalIgnoreCase)
            ? "Denied"
            : status;
}
