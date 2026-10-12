using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Evolith.Ums.Infrastructure.Persistence.Authorization.Entities;

namespace Evolith.Ums.Infrastructure.Persistence.Authorization.Configurations;

public sealed class SystemSuiteDomainResourceRecordConfiguration : IEntityTypeConfiguration<SystemSuiteDomainResourceRecord>
{
    public void Configure(EntityTypeBuilder<SystemSuiteDomainResourceRecord> builder)
    {
        builder.ToTable("SystemSuiteDomainResources", AuthorizationPersistenceConstants.Schema);
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ParentResourceId).IsRequired(false);
        builder.Property(x => x.Type).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Code).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(500);
        builder.Property(x => x.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(x => x.UpdatedBy).HasMaxLength(100);
        builder.Property(x => x.AuditTimeSpan).HasMaxLength(100).IsRequired();

        builder.HasIndex(x => x.ModuleId)

               .HasDatabaseName("IX_SystemSuiteDomainResources_ModuleId");


        builder.HasIndex(x => new { x.SystemSuiteId, x.Code }).IsUnique();
    }
}
