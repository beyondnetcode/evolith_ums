using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Evolith.Ums.Infrastructure.Persistence.Identity.Entities;

namespace Evolith.Ums.Infrastructure.Persistence.Identity.Configurations;

public sealed class TenantIdentityProviderRecordConfiguration : IEntityTypeConfiguration<TenantIdentityProviderRecord>
{
    public void Configure(EntityTypeBuilder<TenantIdentityProviderRecord> builder)
    {
        builder.ToTable("TenantIdentityProviders", IdentityPersistenceConstants.Schema);
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Code).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(x => x.UpdatedBy).HasMaxLength(100);
        builder.Property(x => x.AuditTimeSpan).HasMaxLength(100).IsRequired();

        builder.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
    }
}
