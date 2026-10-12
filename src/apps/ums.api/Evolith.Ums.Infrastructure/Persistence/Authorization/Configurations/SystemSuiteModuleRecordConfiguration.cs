using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Evolith.Ums.Infrastructure.Persistence.Authorization.Entities;

namespace Evolith.Ums.Infrastructure.Persistence.Authorization.Configurations;

public sealed class SystemSuiteModuleRecordConfiguration : IEntityTypeConfiguration<SystemSuiteModuleRecord>
{
    public void Configure(EntityTypeBuilder<SystemSuiteModuleRecord> builder)
    {
        builder.ToTable("SystemSuiteModules", AuthorizationPersistenceConstants.Schema);
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Code).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.Icon).HasMaxLength(64);
        builder.Property(x => x.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(x => x.UpdatedBy).HasMaxLength(100);
        builder.Property(x => x.AuditTimeSpan).HasMaxLength(100).IsRequired();

        builder.HasIndex(x => new { x.SystemSuiteId, x.Code }).IsUnique();
    }
}
