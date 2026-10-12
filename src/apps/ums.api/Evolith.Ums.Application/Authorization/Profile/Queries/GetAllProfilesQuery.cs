using Evolith.Ums.Application.Authorization.Profile.DTOs;

namespace Evolith.Ums.Application.Authorization.Profile.Queries;

public sealed record GetAllProfilesQuery(
    int Page = QueryDefaults.Page,
    int PageSize = QueryDefaults.PageSize,
    string? Search = null,
    string? Criteria = null,
    string? Status = null,
    string? SortBy = null,
    string? SortOrder = null,
    Guid? TenantId = null,
    Guid? UserId = null) : IQuery<PagedResult<ProfileDto>>;
