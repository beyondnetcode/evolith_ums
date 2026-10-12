namespace Evolith.Ums.Infrastructure.Persistence.Configuration.Entities;

public sealed class FeatureFlagEvaluationLogRecord
{
    public Guid Id { get; set; }
    public Guid FeatureFlagId { get; set; }
    public Guid EvaluatedBy { get; set; }
    public bool Result { get; set; }
    public string Context { get; set; } = string.Empty;
    public DateTime EvaluatedAtUtc { get; set; }

    public FeatureFlagRecord FeatureFlag { get; set; } = null!;
}
