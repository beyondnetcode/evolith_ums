using Evolith.Ums.Infrastructure.Persistence.Approvals.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Evolith.Ums.Infrastructure.Persistence.Approvals.Configurations;

public sealed class ApprovalRequiredDocumentRecordConfiguration : IEntityTypeConfiguration<ApprovalRequiredDocumentRecord>
{


    public void Configure(EntityTypeBuilder<ApprovalRequiredDocumentRecord> builder)
    {
        builder.ToTable("ApprovalRequiredDocuments", ApprovalsPersistenceConstants.Schema);
        builder.HasKey(x => x.Id);

        builder.Property(x => x.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(x => x.UpdatedBy).HasMaxLength(100);
        builder.Property(x => x.AuditTimeSpan).HasMaxLength(100).IsRequired();

        builder.HasIndex(x => x.WorkflowId);
        builder.HasIndex(x => x.DocumentTypeId);
        builder.HasIndex(x => new { x.WorkflowId, x.DocumentTypeId }).IsUnique();
    }
}
