using Evolith.Ums.Application.Configuration.FeatureFlag.DTOs;

namespace Evolith.Ums.Application.Configuration.FeatureFlag.Queries;

public sealed record GetAllFeatureFlagsQuery(
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    string Criteria = "flagCode",
    string Status = "all",
    string SortBy = "flagCode",
    string SortOrder = "asc",
    string? FlagType = null,
    string? LinkedResourceType = null) : IQuery<PagedResult<FeatureFlagDto>>;
