using Evolith.Ums.Application.Approvals.UserDocument.DTOs;

namespace Evolith.Ums.Application.Approvals.UserDocument.Queries;

public sealed record GetAllUserDocumentsQuery(
    int Page = QueryDefaults.Page, int PageSize = QueryDefaults.PageSize, string? Search = null, string? Criteria = null,
    string? Status = null, string? SortBy = null, string? SortOrder = null,
    Guid? UserId = null, Guid? TenantId = null) : IQuery<PagedResult<UserDocumentDto>>;
