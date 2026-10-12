namespace Evolith.Ums.Infrastructure.Persistence.Identity;

using Microsoft.EntityFrameworkCore;
using Evolith.Ums.Application.Identity.Auth;
using Evolith.Ums.Infrastructure.Persistence.Identity.Entities;

/// <summary>
/// Implementación de <see cref="IPasswordResetTokenStore"/> sobre <see cref="UmsPlatformDbContext"/>.
/// Persiste solo el hash del token (G-188). Funciona con Npgsql y con el EF InMemory de los tests.
/// </summary>
public sealed class PasswordResetTokenStore : IPasswordResetTokenStore
{
    private readonly UmsPlatformDbContext _db;

    public PasswordResetTokenStore(UmsPlatformDbContext db)
    {
        _db = db;
    }

    public async Task IssueAsync(
        Guid tenantId,
        Guid userId,
        string tokenHash,
        DateTime issuedAtUtc,
        DateTime expiresAtUtc,
        CancellationToken cancellationToken = default)
    {
        // Un solo secreto pendiente por usuario: pedir el restablecimiento de nuevo anula el
        // enlace anterior. Sin esto, cada solicitud dejaría un token vivo más y la ventana de
        // exposición crecería con el número de solicitudes, no con la vida del token.
        MarkAsInvalidated(
            await ActiveTokensOf(tenantId, userId).ToListAsync(cancellationToken).ConfigureAwait(false),
            "reissue",
            issuedAtUtc);

        _db.PasswordResetTokens.Add(new PasswordResetTokenRecord
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = userId,
            TokenHash = tokenHash,
            Status = PasswordResetTokenStatuses.Active,
            IssuedAtUtc = issuedAtUtc,
            ExpiresAtUtc = expiresAtUtc,
        });

        // Un único SaveChanges ⇒ la invalidación de los anteriores y la emisión del nuevo
        // entran juntas o no entra ninguna.
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<PasswordResetTokenSnapshot?> FindByHashAsync(
        string tokenHash,
        CancellationToken cancellationToken = default)
    {
        var record = await _db.PasswordResetTokens
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.TokenHash == tokenHash, cancellationToken)
            .ConfigureAwait(false);

        return record is null
            ? null
            : new PasswordResetTokenSnapshot(
                record.Id,
                record.TenantId,
                record.UserId,
                record.Status,
                record.IssuedAtUtc,
                record.ExpiresAtUtc);
    }

    public async Task ConsumeAsync(
        Guid tokenId,
        DateTime consumedAtUtc,
        CancellationToken cancellationToken = default)
    {
        var record = await _db.PasswordResetTokens
            .FirstOrDefaultAsync(r => r.Id == tokenId && r.Status == PasswordResetTokenStatuses.Active, cancellationToken)
            .ConfigureAwait(false);

        if (record is null)
        {
            return;
        }

        record.Status = PasswordResetTokenStatuses.Used;
        record.ConsumedAtUtc = consumedAtUtc;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task InvalidateActiveForUserAsync(
        Guid tenantId,
        Guid userId,
        string reason,
        DateTime invalidatedAtUtc,
        CancellationToken cancellationToken = default)
    {
        var records = await ActiveTokensOf(tenantId, userId).ToListAsync(cancellationToken).ConfigureAwait(false);
        if (records.Count == 0)
        {
            return;
        }

        MarkAsInvalidated(records, reason, invalidatedAtUtc);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private IQueryable<PasswordResetTokenRecord> ActiveTokensOf(Guid tenantId, Guid userId) =>
        _db.PasswordResetTokens.Where(r =>
            r.TenantId == tenantId
            && r.UserId == userId
            && r.Status == PasswordResetTokenStatuses.Active);

    private static void MarkAsInvalidated(
        List<PasswordResetTokenRecord> records,
        string reason,
        DateTime invalidatedAtUtc)
    {
        foreach (var record in records)
        {
            record.Status = PasswordResetTokenStatuses.Invalidated;
            record.InvalidatedReason = reason;
            record.ConsumedAtUtc = invalidatedAtUtc;
        }
    }
}
