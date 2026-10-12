using Evolith.Ums.Application.Configuration.FeatureFlag.DTOs;

namespace Evolith.Ums.Application.Configuration.FeatureFlag.Queries;

public sealed record GetFeatureFlagsBySystemSuiteQuery(Guid SystemSuiteId) : IQuery<IReadOnlyList<FeatureFlagDto>>;
