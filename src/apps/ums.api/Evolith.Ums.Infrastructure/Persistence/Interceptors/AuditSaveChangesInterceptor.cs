using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Evolith.Ums.Infrastructure.Persistence.Interceptors;

/// <summary>
/// EF Core SaveChanges interceptor that automatically stamps audit fields on any
/// <see cref="IAuditableRecord"/> entity before changes are persisted (FIX-08).
///
/// Rules:
/// - <see cref="EntityState.Added"/>    → set CreatedAtUtc + CreatedBy only when they are
///                                        not already populated (domain layer stamping wins).
/// - <see cref="EntityState.Modified"/> → always refresh UpdatedAtUtc + UpdatedBy from
///                                        <see cref="IUserContext"/> so the latest actor is
///                                        captured even when a repository's Apply() method
///                                        forgets to copy audit fields.
///
/// Fallback safety: if IUserContext is unavailable or unauthenticated, a safe sentinel
/// ("system") is used so auditable columns are never left null/empty.
/// </summary>
internal sealed class AuditSaveChangesInterceptor(IUserContext userContext) : SaveChangesInterceptor
{
    private const string SystemActor = "system";

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        if (eventData.Context is not null)
            StampAuditFields(eventData.Context);

        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
            StampAuditFields(eventData.Context);

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void StampAuditFields(DbContext context)
    {
        var actor = userContext.IsAuthenticated && !string.IsNullOrWhiteSpace(userContext.UserId)
            ? userContext.UserId
            : SystemActor;

        var now = DateTime.UtcNow;

        foreach (var entry in context.ChangeTracker.Entries<IAuditableRecord>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    // Only stamp if the domain/repository layer has not already set them.
                    if (entry.Entity.CreatedAtUtc == default)
                        entry.Entity.CreatedAtUtc = now;

                    if (string.IsNullOrWhiteSpace(entry.Entity.CreatedBy))
                        entry.Entity.CreatedBy = actor;
                    break;

                case EntityState.Modified:
                    // Always refresh — ensures the latest actor/timestamp is recorded
                    // even when a repository's Apply() method omits the audit copy.
                    entry.Entity.UpdatedAtUtc = now;
                    entry.Entity.UpdatedBy    = actor;
                    break;
            }
        }
    }
}
