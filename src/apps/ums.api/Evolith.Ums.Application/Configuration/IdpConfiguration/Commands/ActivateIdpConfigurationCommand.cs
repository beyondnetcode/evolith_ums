namespace Evolith.Ums.Application.Configuration.IdpConfiguration.Commands;

public sealed record ActivateIdpConfigurationCommand(Guid IdpConfigurationId) : ICommand;
