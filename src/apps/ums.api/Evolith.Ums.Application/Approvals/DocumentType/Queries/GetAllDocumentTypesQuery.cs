using Evolith.Ums.Application.Approvals.DocumentType.DTOs;

namespace Evolith.Ums.Application.Approvals.DocumentType.Queries;

public sealed record GetAllDocumentTypesQuery(
    int Page = QueryDefaults.Page, int PageSize = QueryDefaults.PageSize, string? Search = null, string? Criteria = null,
    string? SortBy = null, string? SortOrder = null, Guid? TenantId = null) : IQuery<PagedResult<DocumentTypeDto>>;
