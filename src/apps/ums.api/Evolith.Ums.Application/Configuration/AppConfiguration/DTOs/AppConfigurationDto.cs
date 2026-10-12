namespace Evolith.Ums.Application.Configuration.AppConfiguration.DTOs;

public sealed record AppConfigurationDto(
    Guid AppConfigurationId,
    Guid? TenantId,
    Guid? SystemSuiteId,
    Guid? ModuleId,
    string Code,
    string Value,
    string Description,
    string Scope,
    bool IsInheritable,
    bool IsEncrypted,
    bool IsNonOverridable,
    string Version,
    string Status,
    /// <summary>
    /// REC-10: SQL Server RowVersion for optimistic concurrency. Exposed as ETag header
    /// by the Presentation layer. Send as If-Match header in PUT/PATCH requests.
    /// Null for InMemory-backed instances.
    /// </summary>
    byte[]? RowVersion = null);
