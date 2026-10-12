using Evolith.Ums.Infrastructure.Persistence.Authorization.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Evolith.Ums.Infrastructure.Persistence.Authorization.Configurations;

public sealed class PermissionTemplateItemRecordConfiguration : IEntityTypeConfiguration<PermissionTemplateItemRecord>
{
    public void Configure(EntityTypeBuilder<PermissionTemplateItemRecord> builder)
    {
        builder.ToTable("PermissionTemplateItems", AuthorizationPersistenceConstants.Schema);
        builder.HasKey(x => x.Id);

        builder.Property(x => x.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(x => x.UpdatedBy).HasMaxLength(100);
        builder.Property(x => x.AuditTimeSpan).HasMaxLength(100).IsRequired();

        builder.HasIndex(x => new { x.TemplateId, x.TargetTypeId, x.TargetId, x.ActionId }).IsUnique();

        // El índice único empieza por TemplateId, así que no sirve para preguntar «qué plantillas
        // tocan este destino», que es lo que hacen las guardas de dependencia antes de borrar una
        // opción o un recurso. Sin él, esa comprobación recorre la tabla entera.
        builder.HasIndex(x => new { x.TargetId, x.IsActive })
               .HasDatabaseName("IX_PermissionTemplateItems_TargetId_IsActive");
    }
}
