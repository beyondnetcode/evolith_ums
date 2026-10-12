using Evolith.Ums.Application.Configuration.FeatureFlag.DTOs;

namespace Evolith.Ums.Application.Configuration.FeatureFlag.Queries;

public sealed record GetFeatureFlagByIdQuery(Guid FeatureFlagId) : IQuery<FeatureFlagDto>;
