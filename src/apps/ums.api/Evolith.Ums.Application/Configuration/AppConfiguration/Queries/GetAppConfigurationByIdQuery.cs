using Evolith.Ums.Application.Configuration.AppConfiguration.DTOs;

namespace Evolith.Ums.Application.Configuration.AppConfiguration.Queries;

public sealed record GetAppConfigurationByIdQuery(Guid AppConfigurationId) : IQuery<AppConfigurationDto>;
