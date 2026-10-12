using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Evolith.Ums.Infrastructure.Persistence.Identity.Entities;

namespace Evolith.Ums.Infrastructure.Persistence.Identity.Configurations;

public sealed class RefreshTokenRecordConfiguration : IEntityTypeConfiguration<RefreshTokenRecord>
{
    public void Configure(EntityTypeBuilder<RefreshTokenRecord> builder)
    {
        builder.ToTable("RefreshTokens", IdentityPersistenceConstants.Schema);
        builder.HasKey(x => x.Id);

        // Solo el hash del token; único para localizarlo en renovación/revocación.
        builder.Property(x => x.TokenHash).HasMaxLength(128).IsRequired();
        builder.HasIndex(x => x.TokenHash).IsUnique();

        builder.Property(x => x.Status).HasMaxLength(20).IsRequired();
        builder.Property(x => x.RevokedReason).HasMaxLength(60);

        // G-295/G-297: nullables a propósito. Las familias vivas al desplegar el cambio no tienen
        // estos datos, y exigirlos las mataría de golpe — una migración de seguridad que cierra la
        // sesión de todo el mundo se revierte el mismo día.
        builder.Property(x => x.SessionId).HasMaxLength(64);   // `sid` = Guid en formato "N" (32)
        builder.Property(x => x.SystemCode).HasMaxLength(100); // mismo largo que el resto de códigos

        builder.HasIndex(x => x.FamilyId);          // invalidación de familia (reuso/revocación)
        builder.HasIndex(x => new { x.TenantId, x.UserId });
        builder.HasIndex(x => x.ExpiresAtUtc);
    }
}
