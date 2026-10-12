using Evolith.Ums.Infrastructure.Persistence.Configuration.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Evolith.Ums.Infrastructure.Persistence.Configuration.Configurations;

public sealed class FeatureFlagCriteriaRecordConfiguration : IEntityTypeConfiguration<FeatureFlagCriteriaRecord>
{
    public void Configure(EntityTypeBuilder<FeatureFlagCriteriaRecord> builder)
    {
        builder.ToTable("FeatureFlagCriteria", ConfigurationPersistenceConstants.Schema);
        builder.HasKey(x => x.Id);

        builder.Property(x => x.CriteriaType).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Operator).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Value).HasMaxLength(2000).IsRequired();

        builder.HasIndex(x => x.FeatureFlagId);

        builder.HasOne(x => x.FeatureFlag)
            .WithMany(x => x.Criteria)
            .HasForeignKey(x => x.FeatureFlagId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
