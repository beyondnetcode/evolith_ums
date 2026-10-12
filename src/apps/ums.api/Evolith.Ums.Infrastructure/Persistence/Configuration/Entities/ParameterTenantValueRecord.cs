using Evolith.Ums.Infrastructure.Persistence;

namespace Evolith.Ums.Infrastructure.Persistence.Configuration.Entities;

public sealed class ParameterTenantValueRecord : IAuditableRecord
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid ParameterDefinitionId { get; set; }
    public string OverrideValue { get; set; } = string.Empty;
    public int StatusId { get; set; }
    public string Version { get; set; } = string.Empty;
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public string AuditTimeSpan { get; set; } = string.Empty;
}