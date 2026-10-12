using Evolith.Ums.Application.Approvals.ApprovalWorkflow.DTOs;
using Evolith.Ums.Application.Common.Interfaces;
using Evolith.Ums.Domain.Approvals;
using static Evolith.Ums.Application.Common.QueryRequestNormalizer;

namespace Evolith.Ums.Application.Approvals.ApprovalWorkflow.Queries;

public sealed class GetAllApprovalWorkflowsQueryHandler : IQueryHandler<GetAllApprovalWorkflowsQuery, PagedResult<ApprovalWorkflowDto>>
{
    private readonly IApprovalWorkflowRepository _repository;
    private readonly ITenantContext? _tenantContext;

    public GetAllApprovalWorkflowsQueryHandler(IApprovalWorkflowRepository repository, ITenantContext? tenantContext = null)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]

    [AuthorizationAspect(ResourceCode = "view_approval_rules", ActionCode = "view")]
    public async Task<Result<PagedResult<ApprovalWorkflowDto>>> Handle(GetAllApprovalWorkflowsQuery request, CancellationToken cancellationToken)
    {
        var page = NormalizePage(request.Page);
        var pageSize = NormalizePageSize(request.PageSize);
        var sortBy = NormalizeText(request.SortBy, "name").ToLowerInvariant();
        var sortOrder = NormalizeText(request.SortOrder, "asc").ToLowerInvariant();
        var search = NormalizeSearch(request.Search);

        // Tenant isolation: admins may filter by any tenant (or null for all); regular users are
        // always scoped to their own tenant regardless of what request.TenantId says.
        var effectiveTenantId = (_tenantContext?.IsInternalAdmin == true)
            ? request.TenantId
            : _tenantContext?.OrganizationId;

        var items = effectiveTenantId.HasValue
            ? await _repository.GetByTenantIdAsync(effectiveTenantId.Value, cancellationToken)
            : await _repository.GetAllAsync(null, cancellationToken);

        var query = items.Select(w => new ApprovalWorkflowDto(
            w.Props.Id.GetValue(), w.Props.TenantId.GetValue(), w.Props.SystemSuiteId?.GetValue(),
            w.Props.Code.GetValue(), w.Props.Name.GetValue(), w.Props.Description.GetValue(),
            w.Props.TargetUserCategory.ToString(), w.Props.RequiresApproval,
            // WF2/WF3 (G-118): checklist observable con id.
            w.RequiredDocuments.Select(d => new RequiredDocumentDto(
                d.GetId().GetValue(), d.DocumentTypeId.GetValue(), d.IsMandatory)).ToList()));

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(w => w.Name.Contains(search, StringComparison.OrdinalIgnoreCase));

        query = (sortBy, sortOrder) switch
        {
            ("code", "desc") => query.OrderByDescending(w => w.Code),
            ("code", _) => query.OrderBy(w => w.Code),
            _ => query.OrderBy(w => w.Name),
        };

        var totalItems = query.Count();
        var totalPages = totalItems == 0 ? 0 : (int)Math.Ceiling(totalItems / (double)pageSize);
        var paged = query.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return Result<PagedResult<ApprovalWorkflowDto>>.Success(new PagedResult<ApprovalWorkflowDto>(paged, page, pageSize, totalItems, totalPages));
    }
}
