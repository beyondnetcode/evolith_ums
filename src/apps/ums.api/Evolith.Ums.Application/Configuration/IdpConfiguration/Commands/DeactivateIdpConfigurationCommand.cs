namespace Evolith.Ums.Application.Configuration.IdpConfiguration.Commands;

public sealed record DeactivateIdpConfigurationCommand(Guid IdpConfigurationId) : ICommand;
