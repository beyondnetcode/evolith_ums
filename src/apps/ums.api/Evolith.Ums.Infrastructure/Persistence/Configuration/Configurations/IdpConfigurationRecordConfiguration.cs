using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Evolith.Ums.Infrastructure.Persistence.Configuration.Entities;

namespace Evolith.Ums.Infrastructure.Persistence.Configuration.Configurations;

public sealed class IdpConfigurationRecordConfiguration : IEntityTypeConfiguration<IdpConfigurationRecord>
{
    public void Configure(EntityTypeBuilder<IdpConfigurationRecord> builder)
    {
        builder.ToTable("IdpConfigurations", ConfigurationPersistenceConstants.Schema);
        builder.HasKey(x => x.Id);

        builder.Property(x => x.DomainHintsJson).HasMaxLength(4000).IsRequired();
        builder.Property(x => x.ConfigPayload).HasMaxLength(20000).IsRequired();
        builder.Property(x => x.SecretRef).HasMaxLength(500).IsRequired();
        builder.Property(x => x.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(x => x.UpdatedBy).HasMaxLength(100);
        builder.Property(x => x.AuditTimeSpan).HasMaxLength(100).IsRequired();
        builder.Property(x => x.RowVersion).IsRowVersion(); // FIX-03: optimistic concurrency

        builder.HasIndex(x => x.TenantId);
        builder.HasIndex(x => x.SystemSuiteId);
        builder.HasIndex(x => x.ProviderTypeId);
        builder.HasIndex(x => new { x.TenantId, x.SystemSuiteId, x.ResolutionPriority });
    }
}
