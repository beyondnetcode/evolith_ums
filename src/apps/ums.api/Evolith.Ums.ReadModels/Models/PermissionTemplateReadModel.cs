namespace Evolith.Ums.ReadModels.Models;

public sealed class PermissionTemplateReadModel
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid RoleId { get; set; }
    public Guid SystemSuiteId { get; set; }
    public string Version { get; set; } = string.Empty;
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public string AuditTimeSpan { get; set; } = string.Empty;
    public ICollection<PermissionTemplateItemReadModel> Items { get; set; } = new List<PermissionTemplateItemReadModel>();
}
