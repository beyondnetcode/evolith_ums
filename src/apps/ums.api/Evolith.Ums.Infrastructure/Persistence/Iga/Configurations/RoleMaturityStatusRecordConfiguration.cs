using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Evolith.Ums.Infrastructure.Persistence.Iga.Entities;

namespace Evolith.Ums.Infrastructure.Persistence.Iga.Configurations;

public sealed class RoleMaturityStatusRecordConfiguration : IEntityTypeConfiguration<RoleMaturityStatusRecord>
{
    public void Configure(EntityTypeBuilder<RoleMaturityStatusRecord> builder)
    {
        builder.ToTable("RoleMaturityStatuses", IgaPersistenceConstants.Schema);
        builder.HasKey(x => x.Id);

        builder.Property(x => x.PerformanceScore).HasColumnType("numeric(4,2)");
        builder.Property(x => x.BlockingFactor).HasMaxLength(500);
        builder.Property(x => x.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(x => x.UpdatedBy).HasMaxLength(100);
        builder.Property(x => x.AuditTimeSpan).HasMaxLength(100).IsRequired();

        builder.HasIndex(x => x.TenantId);
        builder.HasIndex(x => new { x.TenantId, x.UserId });
        // Un estado de madurez por usuario y rol dentro del inquilino (fuente de elegibilidad).
        builder.HasIndex(x => new { x.TenantId, x.UserId, x.RoleId }).IsUnique();
    }
}
