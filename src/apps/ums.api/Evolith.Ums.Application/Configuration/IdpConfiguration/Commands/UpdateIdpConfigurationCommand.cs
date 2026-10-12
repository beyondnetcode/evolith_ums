namespace Evolith.Ums.Application.Configuration.IdpConfiguration.Commands;

public sealed record UpdateIdpConfigurationCommand(
    Guid IdpConfigurationId,
    IReadOnlyList<string> DomainHints,
    string ConfigPayload,
    string SecretRef) : ICommand;
