using Evolith.Ums.Infrastructure.Persistence.Identity.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Evolith.Ums.Infrastructure.Persistence.Identity.Configurations;

public sealed class UserManagementDelegationRecordConfiguration : IEntityTypeConfiguration<UserManagementDelegationRecord>
{
    public void Configure(EntityTypeBuilder<UserManagementDelegationRecord> builder)
    {
        builder.ToTable("UserManagementDelegations", IdentityPersistenceConstants.Schema);
        builder.HasKey(x => x.Id);

        builder.Property(x => x.AllowedActionsJson).HasMaxLength(500).IsRequired();
        builder.Property(x => x.RevocationReason).HasMaxLength(500);
        builder.Property(x => x.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(x => x.UpdatedBy).HasMaxLength(100);
        builder.Property(x => x.AuditTimeSpan).HasMaxLength(100).IsRequired();
        builder.Property(x => x.RowVersion).IsRowVersion(); // FIX-03: optimistic concurrency

        builder.HasIndex(x => x.TenantId);
        builder.HasIndex(x => x.DelegatingAdminId);
        builder.HasIndex(x => x.DelegatedAdminId);
        builder.HasIndex(x => new { x.TenantId, x.StatusId });
    }
}
