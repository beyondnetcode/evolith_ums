namespace Evolith.Ums.Application.Common.Interfaces;

/// <summary>
/// REC-04: Scope de transacción cross-aggregate.
///
/// Cuando un command handler necesita modificar más de un aggregate en una operación
/// atómica (p. ej. aprobar una delegation + crear un audit record), envuelve ambas
/// operaciones en un BeginAsync/CommitAsync.
///
/// Uso:
/// <code>
/// await using var tx = await _unitOfWorkScope.BeginAsync(cancellationToken);
/// await _repoA.UnitOfWork.SaveEntitiesAsync(cancellationToken);
/// await _repoB.UnitOfWork.SaveEntitiesAsync(cancellationToken);
/// await tx.CommitAsync(cancellationToken);
/// </code>
///
/// Si el scope se dispone sin hacer Commit, hace Rollback automáticamente.
/// </summary>
public interface IUnitOfWorkScope
{
    /// <summary>Abre una nueva transacción de base de datos.</summary>
    Task<ITransactionScope> BeginAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// G-117: ejecuta <paramref name="operation"/> dentro de una transacción gestionada por la
    /// <c>ExecutionStrategy</c> del proveedor. Necesario cuando <c>EnableRetryOnFailure</c> está
    /// activo (NpgsqlRetryingExecutionStrategy): una transacción iniciada por el usuario vía
    /// <see cref="BeginAsync"/> lanza «does not support user-initiated transactions». La estrategia
    /// hace begin+commit y reintenta el bloque completo ante fallos transitorios (con rollback previo).
    /// </summary>
    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default);
}

/// <summary>Representa una transacción abierta; dispone (rollback) si no se hace Commit.</summary>
public interface ITransactionScope : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken = default);
    Task RollbackAsync(CancellationToken cancellationToken = default);
}
