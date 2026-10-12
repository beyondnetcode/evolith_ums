using Evolith.Ums.Application.Configuration.IdpConfiguration.DTOs;

namespace Evolith.Ums.Application.Configuration.IdpConfiguration.Commands;

public sealed record CreateIdpConfigurationCommand(
    Guid TenantId,
    Guid SystemSuiteId,
    string ProviderType,
    IReadOnlyList<string> DomainHints,
    string ConfigPayload,
    string SecretRef,
    int ResolutionPriority,
    Guid? FallbackToId) : ICommand<CreateIdpConfigurationResponse>;
