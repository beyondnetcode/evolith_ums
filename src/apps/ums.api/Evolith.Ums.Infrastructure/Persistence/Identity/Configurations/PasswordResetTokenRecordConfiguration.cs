using Evolith.Ums.Infrastructure.Persistence.Identity.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Evolith.Ums.Infrastructure.Persistence.Identity.Configurations;

public sealed class PasswordResetTokenRecordConfiguration : IEntityTypeConfiguration<PasswordResetTokenRecord>
{
    public void Configure(EntityTypeBuilder<PasswordResetTokenRecord> builder)
    {
        builder.ToTable("PasswordResetTokens", IdentityPersistenceConstants.Schema);
        builder.HasKey(x => x.Id);

        // Único: el canje localiza el token por hash, y dos filas con el mismo hash harían
        // ambigua la resolución de cuál gastar.
        builder.Property(x => x.TokenHash).HasMaxLength(128).IsRequired();
        builder.HasIndex(x => x.TokenHash).IsUnique();

        builder.Property(x => x.Status).HasMaxLength(20).IsRequired();
        builder.Property(x => x.InvalidatedReason).HasMaxLength(60);

        builder.HasIndex(x => new { x.TenantId, x.UserId });   // invalidación de los vivos del usuario
        builder.HasIndex(x => x.ExpiresAtUtc);                  // purga de vencidos
    }
}
