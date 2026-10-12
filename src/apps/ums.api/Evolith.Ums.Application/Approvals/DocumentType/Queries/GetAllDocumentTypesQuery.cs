using Evolith.Ums.Application.Approvals.DocumentType.DTOs;

namespace Evolith.Ums.Application.Approvals.DocumentType.Queries;

public sealed record GetAllDocumentTypesQuery(
    int Page = 1, int PageSize = 20, string? Search = null, string Criteria = "name",
    string SortBy = "name", string SortOrder = "asc", Guid? TenantId = null) : IQuery<PagedResult<DocumentTypeDto>>;
