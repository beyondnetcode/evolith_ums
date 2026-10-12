using Microsoft.EntityFrameworkCore;

namespace Evolith.Ums.Infrastructure.Persistence;

internal static class EfChildCollectionReconciler
{
    public static void ReconcileById<TEntity>(
        DbContext dbContext,
        ICollection<TEntity> tracked,
        IEnumerable<TEntity> replacement,
        Func<TEntity, Guid> idSelector,
        Action<TEntity, TEntity> update)
        where TEntity : class
    {
        var replacementById = replacement.ToDictionary(idSelector);
        var trackedById = tracked.ToDictionary(idSelector);

        foreach (var (id, entity) in trackedById)
        {
            if (!replacementById.ContainsKey(id))
            {
                tracked.Remove(entity);
            }
        }

        foreach (var (id, replacementEntity) in replacementById)
        {
            if (trackedById.TryGetValue(id, out var trackedEntity))
            {
                update(trackedEntity, replacementEntity);
                continue;
            }

            tracked.Add(replacementEntity);
            dbContext.Entry(replacementEntity).State = EntityState.Added;
        }
    }
}
