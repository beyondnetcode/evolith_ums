namespace Evolith.Ums.Infrastructure.Persistence.Authorization.Entities;

public sealed class SystemSuiteActionRecord : IAuditableRecord
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid SystemSuiteId { get; set; }
    public Guid? ModuleId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public string AuditTimeSpan { get; set; } = string.Empty;

    public SystemSuiteRecord SystemSuite { get; set; } = null!;
}
