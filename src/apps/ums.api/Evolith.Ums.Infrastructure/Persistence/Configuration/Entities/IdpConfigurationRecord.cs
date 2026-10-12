using Evolith.Ums.Infrastructure.Persistence;

namespace Evolith.Ums.Infrastructure.Persistence.Configuration.Entities;

public sealed class IdpConfigurationRecord : IAuditableRecord
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid SystemSuiteId { get; set; }
    public int ProviderTypeId { get; set; }
    public string DomainHintsJson { get; set; } = string.Empty;
    public string ConfigPayload { get; set; } = string.Empty;
    public string SecretRef { get; set; } = string.Empty;
    public int StatusId { get; set; }
    public int ResolutionPriority { get; set; }
    public Guid? FallbackToId { get; set; }
    public int Version { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public string AuditTimeSpan { get; set; } = string.Empty;
    public byte[] RowVersion { get; set; } = [];
}
