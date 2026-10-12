namespace Evolith.Ums.Application.Configuration.IdpConfiguration.Commands;

public sealed record ArchiveIdpConfigurationCommand(Guid IdpConfigurationId) : ICommand;
