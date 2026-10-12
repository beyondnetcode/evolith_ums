namespace Evolith.Ums.Application.Configuration.IdpConfiguration.DTOs;

public sealed record ResolvedIdpConfigurationDto(
    Guid IdpConfigurationId,
    Guid TenantId,
    Guid SystemSuiteId,
    string ProviderType,
    string Protocol,
    string? Authority,
    string? ClientId,
    string? MetadataAddress,
    IReadOnlyList<string> DomainHints,
    bool DomainMatched,
    int ResolutionPriority,
    Guid? FallbackToId,
    int Version,
    string SecretRef);
