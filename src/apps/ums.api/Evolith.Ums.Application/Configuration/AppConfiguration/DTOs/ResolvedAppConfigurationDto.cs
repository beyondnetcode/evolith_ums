namespace Evolith.Ums.Application.Configuration.AppConfiguration.DTOs;

/// <summary>
/// Result of a hierarchy-aware resolve: includes the effective value, the scope
/// it came from, and whether the config entry was found at all.
/// </summary>
public sealed record ResolvedAppConfigurationDto(
    string  Code,
    string  Value,
    string  ResolvedScope,   // "Global" | "Tenant" | "Suite" | "Module"
    Guid?   SourceConfigId,
    bool    IsEncrypted,
    bool    Found);
