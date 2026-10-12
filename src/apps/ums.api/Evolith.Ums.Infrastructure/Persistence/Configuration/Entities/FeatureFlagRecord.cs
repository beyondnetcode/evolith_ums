using Evolith.Ums.Infrastructure.Persistence;

namespace Evolith.Ums.Infrastructure.Persistence.Configuration.Entities;

public sealed class FeatureFlagRecord : IAuditableRecord
{
    public Guid Id { get; set; }
    public string FlagCode { get; set; } = string.Empty;
    public int FlagTypeId { get; set; }
    public string FlagTargets { get; set; } = string.Empty;
    public int StatusId { get; set; }
    public int? LinkedResourceTypeId { get; set; }
    public Guid? LinkedResourceId { get; set; }
    public int? RolloutPercentage { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public string AuditTimeSpan { get; set; } = string.Empty;
    public byte[] RowVersion { get; set; } = [];

    public Guid SystemSuiteId { get; set; }
    public Guid? TenantId { get; set; }
    public List<FeatureFlagEvaluationLogRecord> EvaluationLogs { get; set; } = [];
    public List<FeatureFlagCriteriaRecord> Criteria { get; set; } = [];
}
