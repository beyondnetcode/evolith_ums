using IdpConfigurationAggregate = Evolith.Ums.Domain.Configuration.IdpConfiguration.IdpConfiguration;

namespace Evolith.Ums.Infrastructure.Configuration.IdpResolution;

internal sealed record IdpResolutionContext(IdpConfigurationAggregate Configuration, bool DomainMatched);
