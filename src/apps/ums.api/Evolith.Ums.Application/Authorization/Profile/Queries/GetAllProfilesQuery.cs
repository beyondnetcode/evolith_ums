using Evolith.Ums.Application.Authorization.Profile.DTOs;

namespace Evolith.Ums.Application.Authorization.Profile.Queries;

public sealed record GetAllProfilesQuery(
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    string Criteria = "userId",
    string Status = "all",
    string SortBy = "userId",
    string SortOrder = "asc",
    Guid? TenantId = null,
    Guid? UserId = null) : IQuery<PagedResult<ProfileDto>>;
