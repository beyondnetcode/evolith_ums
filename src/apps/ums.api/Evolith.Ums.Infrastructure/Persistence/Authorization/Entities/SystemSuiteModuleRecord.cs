namespace Evolith.Ums.Infrastructure.Persistence.Authorization.Entities;

public sealed class SystemSuiteModuleRecord : IAuditableRecord
{
    public Guid Id { get; set; }
    public Guid SystemSuiteId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int StatusId { get; set; }
    public int SortOrder { get; set; }
    public string? Icon { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public string AuditTimeSpan { get; set; } = string.Empty;

    public SystemSuiteRecord SystemSuite { get; set; } = null!;
    public List<SystemSuiteNodeRecord> Nodes { get; set; } = [];
}
