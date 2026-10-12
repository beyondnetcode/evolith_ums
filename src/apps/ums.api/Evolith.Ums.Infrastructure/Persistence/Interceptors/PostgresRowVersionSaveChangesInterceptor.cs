using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Evolith.Ums.Infrastructure.Persistence.Interceptors;

/// <summary>
/// EF Core SaveChanges interceptor that rotates the byte[] RowVersion concurrency token
/// on every UPDATE when running on PostgreSQL.
///
/// PostgreSQL has no SQL Server rowversion equivalent: the bytea column gets its initial
/// value from the gen_random_bytes(8) default on INSERT (see UmsPlatformDbContext), but the
/// store never changes it afterwards. Without rotation the token would stay constant for
/// the row's lifetime, so optimistic concurrency and the If-Match/ETag flow (REC-10) would
/// silently stop detecting conflicts. EF still emits
/// <c>UPDATE ... SET "RowVersion" = @new WHERE "RowVersion" = @original</c>, preserving the
/// SQL Server semantics.
/// </summary>
internal sealed class PostgresRowVersionSaveChangesInterceptor : SaveChangesInterceptor
{
    private const string RowVersionProperty = "RowVersion";

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        if (eventData.Context is not null)
            RotateRowVersions(eventData.Context);

        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
            RotateRowVersions(eventData.Context);

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static void RotateRowVersions(DbContext context)
    {
        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.State != EntityState.Modified)
                continue;

            var property = entry.Metadata.FindProperty(RowVersionProperty);
            if (property is null || property.ClrType != typeof(byte[]))
                continue;

            entry.Property(RowVersionProperty).CurrentValue = RandomNumberGenerator.GetBytes(8);
        }
    }
}
