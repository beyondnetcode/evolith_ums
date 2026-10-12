namespace Evolith.Ums.Infrastructure.Persistence.Identity;

using Microsoft.EntityFrameworkCore;
using Evolith.Ums.Application.Identity.Auth;
using Evolith.Ums.Infrastructure.Persistence.Identity.Entities;

/// <summary>
/// Implementación de <see cref="IRefreshTokenStore"/> sobre <see cref="UmsPlatformDbContext"/>.
/// Persiste solo el hash del token (ADR-UMS-091). Funciona con el proveedor Npgsql y con
/// el EF InMemory de los tests; bajo la postura fail-closed solo se invoca cuando un
/// inquilino activa la capacidad, así que en tests (deshabilitado por defecto) no se toca.
/// </summary>
public sealed class RefreshTokenStore : IRefreshTokenStore
{
    private readonly UmsPlatformDbContext _db;

    public RefreshTokenStore(UmsPlatformDbContext db)
    {
        _db = db;
    }

    public async Task IssueAsync(
        Guid tenantId,
        Guid userId,
        Guid familyId,
        string tokenHash,
        DateTime issuedAtUtc,
        DateTime expiresAtUtc,
        string? sessionId,
        string? systemCode,
        CancellationToken cancellationToken = default)
    {
        var record = new RefreshTokenRecord
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = userId,
            FamilyId = familyId,
            TokenHash = tokenHash,
            Status = RefreshTokenStatuses.Active,
            IssuedAtUtc = issuedAtUtc,
            ExpiresAtUtc = expiresAtUtc,
            RenewalCount = 0,
            SessionId = sessionId,
            SystemCode = systemCode,
        };

        _db.RefreshTokens.Add(record);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<RefreshTokenSnapshot?> FindByHashAsync(
        string tokenHash,
        CancellationToken cancellationToken = default)
    {
        var record = await _db.RefreshTokens
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.TokenHash == tokenHash, cancellationToken)
            .ConfigureAwait(false);

        if (record is null)
        {
            return null;
        }

        return new RefreshTokenSnapshot(
            record.Id,
            record.TenantId,
            record.UserId,
            record.FamilyId,
            record.Status,
            record.IssuedAtUtc,
            record.ExpiresAtUtc,
            record.RenewalCount,
            record.SessionId,
            record.SystemCode);
    }

    public async Task RotateAsync(
        RefreshTokenSnapshot current,
        Guid newTokenId,
        string newTokenHash,
        DateTime issuedAtUtc,
        DateTime expiresAtUtc,
        CancellationToken cancellationToken = default)
    {
        // Rotación: el token vigente queda Rotated apuntando al nuevo, y el nuevo entra
        // Active en la misma familia. Un único SaveChanges ⇒ atómico.
        var old = await _db.RefreshTokens
            .FirstOrDefaultAsync(r => r.Id == current.Id, cancellationToken)
            .ConfigureAwait(false);

        if (old is not null)
        {
            old.Status = RefreshTokenStatuses.Rotated;
            old.ReplacedByTokenId = newTokenId;
        }

        _db.RefreshTokens.Add(new RefreshTokenRecord
        {
            Id = newTokenId,
            TenantId = current.TenantId,
            UserId = current.UserId,
            FamilyId = current.FamilyId,
            TokenHash = newTokenHash,
            Status = RefreshTokenStatuses.Active,
            IssuedAtUtc = issuedAtUtc,
            ExpiresAtUtc = expiresAtUtc,
            RenewalCount = current.RenewalCount + 1,
            // G-295/G-297: la sesión y el sistema pedido son de la FAMILIA, no del token. Rotar es
            // cambiar de credencial, no de sesión ni de producto; si no se heredaran aquí, la
            // primera rotación borraría lo que el ingreso recordaba y el defecto reaparecería una
            // renovación más tarde.
            SessionId = current.SessionId,
            SystemCode = current.SystemCode,
        });

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task RevokeFamilyAsync(
        Guid familyId,
        string reason,
        DateTime revokedAtUtc,
        CancellationToken cancellationToken = default)
    {
        // Invalida la familia entera (reuso o revocación explícita). Idempotente:
        // solo toca los que aún no están revocados.
        var members = await _db.RefreshTokens
            .Where(r => r.FamilyId == familyId && r.Status != RefreshTokenStatuses.Revoked)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var member in members)
        {
            member.Status = RefreshTokenStatuses.Revoked;
            member.RevokedAtUtc = revokedAtUtc;
            member.RevokedReason = reason;
        }

        if (members.Count > 0)
        {
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task RevokeAllForUserAsync(
        Guid tenantId,
        Guid userId,
        string reason,
        DateTime revokedAtUtc,
        CancellationToken cancellationToken = default)
    {
        // Revocación explícita del logout: cierra todas las familias vivas del usuario
        // en el inquilino. Idempotente: solo toca los que aún no están revocados.
        var members = await _db.RefreshTokens
            .Where(r => r.TenantId == tenantId && r.UserId == userId && r.Status != RefreshTokenStatuses.Revoked)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var member in members)
        {
            member.Status = RefreshTokenStatuses.Revoked;
            member.RevokedAtUtc = revokedAtUtc;
            member.RevokedReason = reason;
        }

        if (members.Count > 0)
        {
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
