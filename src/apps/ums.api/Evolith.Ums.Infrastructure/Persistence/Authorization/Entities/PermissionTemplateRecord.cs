namespace Evolith.Ums.Infrastructure.Persistence.Authorization.Entities;

public sealed class PermissionTemplateRecord : IAuditableRecord
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid RoleId { get; set; }
    public Guid SystemSuiteId { get; set; }
    public string Version { get; set; } = string.Empty;
    public int StatusId { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public string AuditTimeSpan { get; set; } = string.Empty;
    public byte[] RowVersion { get; set; } = [];

    public List<PermissionTemplateItemRecord> Items { get; set; } = [];
}
