namespace Evolith.Ums.Application.Configuration.IdpConfiguration.DTOs;

public sealed record IdpConfigurationDto(
    Guid IdpConfigurationId,
    Guid TenantId,
    Guid SystemSuiteId,
    string ProviderType,
    IReadOnlyList<string> DomainHints,
    string ConfigPayload,
    string SecretRef,
    string Status,
    int ResolutionPriority,
    Guid? FallbackToId,
    int Version);
