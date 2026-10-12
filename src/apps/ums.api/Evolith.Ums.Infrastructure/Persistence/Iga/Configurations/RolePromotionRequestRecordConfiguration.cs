using Evolith.Ums.Infrastructure.Persistence.Iga.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Evolith.Ums.Infrastructure.Persistence.Iga.Configurations;

public sealed class RolePromotionRequestRecordConfiguration : IEntityTypeConfiguration<RolePromotionRequestRecord>
{
    public void Configure(EntityTypeBuilder<RolePromotionRequestRecord> builder)
    {
        builder.ToTable("RolePromotionRequests", IgaPersistenceConstants.Schema);
        builder.HasKey(x => x.Id);

        builder.Property(x => x.DecisionReason).HasMaxLength(1000);
        builder.Property(x => x.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(x => x.UpdatedBy).HasMaxLength(100);
        builder.Property(x => x.AuditTimeSpan).HasMaxLength(100).IsRequired();

        builder.HasIndex(x => x.TargetUserId);
        builder.HasIndex(x => x.TenantId);
        builder.HasIndex(x => new { x.TenantId, x.StatusId });
    }
}
