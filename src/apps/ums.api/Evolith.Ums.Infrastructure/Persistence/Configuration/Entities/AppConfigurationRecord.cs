using Evolith.Ums.Infrastructure.Persistence;

namespace Evolith.Ums.Infrastructure.Persistence.Configuration.Entities;

public sealed class AppConfigurationRecord : IAuditableRecord
{
    public Guid Id { get; set; }
    public Guid? TenantId { get; set; }
    public Guid? SystemSuiteId { get; set; }
    public Guid? ModuleId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int ScopeId { get; set; }
    public bool IsInheritable { get; set; }
    public bool IsEncrypted { get; set; }
    public bool IsNonOverridable { get; set; }
    public string Version { get; set; } = string.Empty;
    public int StatusId { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public string AuditTimeSpan { get; set; } = string.Empty;
    public byte[] RowVersion { get; set; } = [];
}
