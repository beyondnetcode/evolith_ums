using Evolith.Ums.Infrastructure.Persistence.Configuration.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Evolith.Ums.Infrastructure.Persistence.Configuration.Configurations;

public sealed class FeatureFlagRecordConfiguration : IEntityTypeConfiguration<FeatureFlagRecord>
{
    public void Configure(EntityTypeBuilder<FeatureFlagRecord> builder)
    {
        builder.ToTable("FeatureFlags", ConfigurationPersistenceConstants.Schema);
        builder.HasKey(x => x.Id);

        builder.Property(x => x.FlagCode).HasMaxLength(100).IsRequired();
        builder.Property(x => x.FlagTargets).HasMaxLength(2000).IsRequired();
        builder.Property(x => x.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(x => x.UpdatedBy).HasMaxLength(100);
        builder.Property(x => x.AuditTimeSpan).HasMaxLength(100).IsRequired();
        builder.Property(x => x.RowVersion).IsRowVersion(); // FIX-03: optimistic concurrency

        builder.Property(x => x.SystemSuiteId).IsRequired();
        builder.Property(x => x.TenantId);

        builder.HasIndex(x => new { x.SystemSuiteId, x.FlagCode })
            .IsUnique()
            .HasFilter("\"StatusId\" != 3");
        builder.HasIndex(x => x.SystemSuiteId);
        builder.HasIndex(x => x.StatusId);
        builder.HasIndex(x => x.FlagTypeId);

        builder.HasMany(x => x.EvaluationLogs)
            .WithOne(x => x.FeatureFlag)
            .HasForeignKey(x => x.FeatureFlagId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Criteria)
            .WithOne(x => x.FeatureFlag)
            .HasForeignKey(x => x.FeatureFlagId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
