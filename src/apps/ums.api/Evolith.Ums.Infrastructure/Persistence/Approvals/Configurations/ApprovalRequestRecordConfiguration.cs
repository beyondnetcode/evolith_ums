using Evolith.Ums.Infrastructure.Persistence.Approvals.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Evolith.Ums.Infrastructure.Persistence.Approvals.Configurations;

public sealed class ApprovalRequestRecordConfiguration : IEntityTypeConfiguration<ApprovalRequestRecord>
{


    public void Configure(EntityTypeBuilder<ApprovalRequestRecord> builder)
    {
        builder.ToTable("ApprovalRequests", ApprovalsPersistenceConstants.Schema);
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Justification).HasMaxLength(1000);
        builder.Property(x => x.DecisionReason).HasMaxLength(1000);
        builder.Property(x => x.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(x => x.UpdatedBy).HasMaxLength(100);
        builder.Property(x => x.AuditTimeSpan).HasMaxLength(100).IsRequired();
        builder.Property(x => x.RowVersion).IsRowVersion();

        builder.HasIndex(x => x.WorkflowId);
        builder.HasIndex(x => x.TargetUserId);
        builder.HasIndex(x => x.TargetProfileId);
        builder.HasIndex(x => new { x.TargetUserId, x.RequestedSystemId, x.RequestedBranchId, x.StatusId });
    }
}
