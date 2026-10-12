using Evolith.Ums.Application.Approvals.DocumentType.DTOs;
using Evolith.Ums.Application.Common.Interfaces;
using Evolith.Ums.Domain.Approvals;
using static Evolith.Ums.Application.Common.QueryRequestNormalizer;

namespace Evolith.Ums.Application.Approvals.DocumentType.Queries;

public sealed class GetAllDocumentTypesQueryHandler : IQueryHandler<GetAllDocumentTypesQuery, PagedResult<DocumentTypeDto>>
{
    private readonly IDocumentTypeRepository _repository;
    private readonly ITenantContext? _tenantContext;

    public GetAllDocumentTypesQueryHandler(IDocumentTypeRepository repository, ITenantContext? tenantContext = null)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]

    [AuthorizationAspect(ResourceCode = "view_approval_rules", ActionCode = "view")]
    public async Task<Result<PagedResult<DocumentTypeDto>>> Handle(GetAllDocumentTypesQuery request, CancellationToken cancellationToken)
    {
        var page = NormalizePage(request.Page);
        var pageSize = NormalizePageSize(request.PageSize);
        var sortBy = NormalizeText(request.SortBy, "name").ToLowerInvariant();
        var sortOrder = NormalizeText(request.SortOrder, "asc").ToLowerInvariant();
        var search = NormalizeSearch(request.Search);

        var effectiveTenantId = (_tenantContext?.IsInternalAdmin == true)
            ? request.TenantId
            : _tenantContext?.OrganizationId;

        var items = effectiveTenantId.HasValue
            ? await _repository.GetByTenantIdAsync(effectiveTenantId.Value, cancellationToken)
            : await _repository.GetAllAsync(null, cancellationToken);

        var query = items.Select(d => new DocumentTypeDto(
            d.Props.Id.GetValue(), d.Props.TenantId.GetValue(), d.Props.Code.GetValue(),
            d.Props.Name.GetValue(), d.Props.Description.GetValue(), d.Props.Criticity.ToString()));

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(d => d.Name.Contains(search, StringComparison.OrdinalIgnoreCase));

        query = (sortBy, sortOrder) switch
        {
            ("code", "desc") => query.OrderByDescending(d => d.Code),
            ("criticity", "desc") => query.OrderByDescending(d => d.Criticity),
            _ => query.OrderBy(d => d.Name),
        };

        var totalItems = query.Count();
        var totalPages = totalItems == 0 ? 0 : (int)Math.Ceiling(totalItems / (double)pageSize);
        var paged = query.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return Result<PagedResult<DocumentTypeDto>>.Success(new PagedResult<DocumentTypeDto>(paged, page, pageSize, totalItems, totalPages));
    }
}
