using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Evolith.Ums.Infrastructure.Persistence.Approvals.Entities;

namespace Evolith.Ums.Infrastructure.Persistence.Approvals.Configurations;

public sealed class AccessEnforcementPolicyRecordConfiguration : IEntityTypeConfiguration<AccessEnforcementPolicyRecord>
{


    public void Configure(EntityTypeBuilder<AccessEnforcementPolicyRecord> builder)
    {
        builder.ToTable("AccessEnforcementPolicies", ApprovalsPersistenceConstants.Schema);
        builder.HasKey(x => x.Id);

        builder.Property(x => x.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(x => x.UpdatedBy).HasMaxLength(100);
        builder.Property(x => x.AuditTimeSpan).HasMaxLength(100).IsRequired();
        builder.Property(x => x.RowVersion).IsRowVersion();

        builder.HasIndex(x => x.TenantId);
        builder.HasIndex(x => new { x.TenantId, x.ProfileId }).HasFilter("\"ProfileId\" IS NOT NULL");
        builder.HasIndex(x => new { x.TenantId, x.RoleId }).HasFilter("\"RoleId\" IS NOT NULL");
    }
}
