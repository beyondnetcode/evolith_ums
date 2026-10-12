using Evolith.Ums.Infrastructure.Persistence.Approvals.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Evolith.Ums.Infrastructure.Persistence.Approvals.Configurations;

public sealed class ApprovalWorkflowRecordConfiguration : IEntityTypeConfiguration<ApprovalWorkflowRecord>
{


    public void Configure(EntityTypeBuilder<ApprovalWorkflowRecord> builder)
    {
        builder.ToTable("ApprovalWorkflows", ApprovalsPersistenceConstants.Schema);
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Code).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(255).IsRequired();
        builder.Property(x => x.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(x => x.UpdatedBy).HasMaxLength(100);
        builder.Property(x => x.AuditTimeSpan).HasMaxLength(100).IsRequired();
        builder.Property(x => x.RowVersion).IsRowVersion();

        builder.HasIndex(x => x.TenantId);
        builder.HasIndex(x => x.Code);
        builder.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();

        builder.HasMany(x => x.RequiredDocuments)
            .WithOne()
            .HasForeignKey(x => x.WorkflowId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
