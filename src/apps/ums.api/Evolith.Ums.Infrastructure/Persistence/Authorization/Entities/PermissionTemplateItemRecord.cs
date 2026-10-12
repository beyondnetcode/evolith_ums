namespace Evolith.Ums.Infrastructure.Persistence.Authorization.Entities;

public sealed class PermissionTemplateItemRecord : IAuditableRecord
{
    public Guid Id { get; set; }
    public Guid TemplateId { get; set; }
    public int TargetTypeId { get; set; }
    public Guid TargetId { get; set; }
    public Guid ActionId { get; set; }
    public bool IsAllowed { get; set; }
    public bool IsDenied { get; set; }
    public bool IsActive { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public string AuditTimeSpan { get; set; } = string.Empty;

    public PermissionTemplateRecord? Template { get; set; }
}
