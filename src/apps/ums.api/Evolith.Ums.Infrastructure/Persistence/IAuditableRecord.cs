namespace Evolith.Ums.Infrastructure.Persistence;

/// <summary>
/// Marker interface for EF Core entity records that carry the standard UMS audit columns.
/// Implemented by all *Record classes so that <see cref="Interceptors.AuditSaveChangesInterceptor"/>
/// can stamp them automatically on every write (FIX-08).
///
/// Rules applied by the interceptor:
/// - EntityState.Added   → set CreatedAtUtc + CreatedBy if not already set (domain layer wins).
/// - EntityState.Modified → always refresh UpdatedAtUtc + UpdatedBy from IUserContext.
/// </summary>
public interface IAuditableRecord
{
    string  CreatedBy    { get; set; }
    DateTime CreatedAtUtc { get; set; }
    string? UpdatedBy    { get; set; }
    DateTime? UpdatedAtUtc { get; set; }
}
