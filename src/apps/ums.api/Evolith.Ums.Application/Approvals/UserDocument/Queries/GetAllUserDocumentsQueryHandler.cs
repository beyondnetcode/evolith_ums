using Evolith.Ums.Application.Approvals.UserDocument.DTOs;
using Evolith.Ums.Application.Common.Interfaces;
using Evolith.Ums.Domain.Approvals;
using static Evolith.Ums.Application.Common.QueryRequestNormalizer;

namespace Evolith.Ums.Application.Approvals.UserDocument.Queries;

public sealed class GetAllUserDocumentsQueryHandler : IQueryHandler<GetAllUserDocumentsQuery, PagedResult<UserDocumentDto>>
{
    private readonly IUserDocumentRepository _repository;
    private readonly ITenantContext? _tenantContext;

    public GetAllUserDocumentsQueryHandler(IUserDocumentRepository repository, ITenantContext? tenantContext = null)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]

    [AuthorizationAspect(ResourceCode = "view_approvals", ActionCode = "view")]
    public async Task<Result<PagedResult<UserDocumentDto>>> Handle(GetAllUserDocumentsQuery request, CancellationToken cancellationToken)
    {
        var page = NormalizePage(request.Page);
        var pageSize = NormalizePageSize(request.PageSize);
        var status = NormalizeText(request.Status, "all");
        var sortBy = NormalizeText(request.SortBy, "expirationdate").ToLowerInvariant();
        var sortOrder = NormalizeText(request.SortOrder, "asc").ToLowerInvariant();
        var search = NormalizeSearch(request.Search);

        // Always fetch scoped to the user's tenant; then optionally filter by UserId
        var effectiveTenantId = (_tenantContext?.IsInternalAdmin == true) ? (Guid?)null : _tenantContext?.OrganizationId;
        var allForTenant = await _repository.GetAllAsync(effectiveTenantId, cancellationToken);
        var items = request.UserId.HasValue
            ? allForTenant.Where(d => d.Props.UserId.GetValue() == request.UserId.Value).ToList()
            : allForTenant.ToList();

        var query = items.Select(d => new UserDocumentDto(
            d.Props.Id.GetValue(), d.Props.UserId.GetValue(), d.Props.DocumentTypeId.GetValue(),
            d.Props.IssueDate, d.Props.ExpirationDate, d.Props.Status.ToString(),
            d.Props.Criticity.ToString(), d.Props.FileStoragePath.GetValue(),
            d.Props.FileChecksum, d.Props.NotificationStep));

        if (!string.Equals(status, "all", StringComparison.OrdinalIgnoreCase))
            query = query.Where(d => string.Equals(d.Status, status, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(d => d.FileStoragePath.Contains(search, StringComparison.OrdinalIgnoreCase));

        query = (sortBy, sortOrder) switch
        {
            ("status", "desc") => query.OrderByDescending(d => d.Status),
            ("expirationdate", "desc") => query.OrderByDescending(d => d.ExpirationDate),
            _ => query.OrderBy(d => d.ExpirationDate),
        };

        var totalItems = query.Count();
        var totalPages = totalItems == 0 ? 0 : (int)Math.Ceiling(totalItems / (double)pageSize);
        var paged = query.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return Result<PagedResult<UserDocumentDto>>.Success(new PagedResult<UserDocumentDto>(paged, page, pageSize, totalItems, totalPages));
    }
}
