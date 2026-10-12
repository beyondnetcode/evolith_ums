namespace Evolith.Ums.Infrastructure.Persistence.Authorization.Entities;

public sealed class SystemSuiteRecord : IAuditableRecord
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int StatusId { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public string AuditTimeSpan { get; set; } = string.Empty;
    public byte[] RowVersion { get; set; } = [];

    public List<SystemSuiteModuleRecord> Modules { get; set; } = [];
    public List<SystemSuiteAppSettingRecord> AppSettings { get; set; } = [];
    public List<SystemSuiteActionRecord> Actions { get; set; } = [];
    public List<SystemSuiteDomainResourceRecord> DomainResources { get; set; } = [];
}
