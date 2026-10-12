using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Evolith.Ums.Infrastructure.Persistence.Configuration.Entities;

namespace Evolith.Ums.Infrastructure.Persistence.Configuration.Configurations;

public sealed class FeatureFlagEvaluationLogRecordConfiguration : IEntityTypeConfiguration<FeatureFlagEvaluationLogRecord>
{
    public void Configure(EntityTypeBuilder<FeatureFlagEvaluationLogRecord> builder)
    {
        builder.ToTable("FeatureFlagEvaluationLogs", ConfigurationPersistenceConstants.Schema);
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Context).HasMaxLength(1000).IsRequired();

        builder.HasIndex(x => x.FeatureFlagId);
        builder.HasIndex(x => x.EvaluatedAtUtc);
    }
}
