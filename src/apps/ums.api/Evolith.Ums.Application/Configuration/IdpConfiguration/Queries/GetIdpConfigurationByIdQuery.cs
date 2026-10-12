using Evolith.Ums.Application.Configuration.IdpConfiguration.DTOs;

namespace Evolith.Ums.Application.Configuration.IdpConfiguration.Queries;

public sealed record GetIdpConfigurationByIdQuery(Guid IdpConfigurationId) : IQuery<IdpConfigurationDto>;
