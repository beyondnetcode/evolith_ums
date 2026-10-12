using Evolith.Ums.Application.Configuration.IdpConfiguration.DTOs;

namespace Evolith.Ums.Infrastructure.Configuration.IdpResolution;

internal interface IIdpResolutionStrategy
{
    ResolvedIdpConfigurationDto Resolve(IdpResolutionContext context);
}
