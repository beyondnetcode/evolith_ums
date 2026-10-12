namespace Evolith.Ums.Infrastructure.Persistence;

/// <summary>
/// Thrown when EF Core's <see cref="Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException"/>
/// is caught in a repository's SaveEntitiesAsync, indicating that another request modified the
/// same aggregate between read and write (lost update scenario — RISK-02 / FIX-03).
///
/// This exception is mapped to HTTP 409 Conflict by <see cref="Evolith.Ums.Presentation.Middleware.GlobalExceptionHandler"/>.
/// </summary>
public sealed class ConcurrencyConflictException(string aggregateName, Guid aggregateId)
    : Exception($"Concurrency conflict: aggregate '{aggregateName}' (id={aggregateId}) was modified by another request. Reload and retry.")
{
    public string AggregateName { get; } = aggregateName;
    public Guid   AggregateId   { get; } = aggregateId;
}
