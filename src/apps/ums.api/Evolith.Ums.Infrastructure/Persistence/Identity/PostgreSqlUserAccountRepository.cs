using System.Security.Cryptography;
using System.Text;
using Evolith.Ums.Domain.Identity;
using Evolith.Ums.Domain.Kernel;
using Evolith.Ums.Domain.Kernel.ValueObjects;
using Evolith.Ums.Infrastructure.Persistence;
using Evolith.Ums.Infrastructure.Persistence.Identity.Entities;
using Evolith.Ums.Infrastructure.Persistence.Reflection;
using Microsoft.EntityFrameworkCore;

namespace Evolith.Ums.Infrastructure.Persistence.Identity;

using UserAccountAggregate = Evolith.Ums.Domain.Identity.UserAccount.UserAccount;

public sealed class PostgreSqlUserAccountRepository(UmsPlatformDbContext dbContext) : IUserAccountRepository, IUnitOfWork
{
    private readonly HashSet<UserAccountAggregate> _trackedAggregates = [];

    public IUnitOfWork UnitOfWork => this;

    public async Task<UserAccountAggregate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var record = await dbContext.UserAccounts
            .AsSplitQuery()
            .Include(x => x.MfaEnrollments)
            .Include(x => x.PasswordCredentials)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        return record is null ? null : Rehydrate(record);
    }

    public Task<UserAccountAggregate?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default)
        => GetByIdAsync(id, cancellationToken);

    public async Task<UserAccountAggregate?> GetByEmailAsync(Email email, CancellationToken cancellationToken = default)
    {
        var record = await dbContext.UserAccounts
            .AsSplitQuery()
            .Include(x => x.MfaEnrollments)
            .Include(x => x.PasswordCredentials)
            .FirstOrDefaultAsync(x => x.Email == email.GetValue(), cancellationToken);

        return record is null ? null : Rehydrate(record);
    }

    public async Task<UserAccountAggregate?> GetByTenantAndEmailAsync(
        Guid tenantId,
        Email email,
        bool includeDeleted = false,
        CancellationToken cancellationToken = default)
    {
        IQueryable<UserAccountRecord> query = dbContext.UserAccounts
            .AsSplitQuery()
            .Include(x => x.MfaEnrollments)
            .Include(x => x.PasswordCredentials);

        if (includeDeleted)
        {
            query = query.IgnoreQueryFilters();
        }

        var record = await query.FirstOrDefaultAsync(
            x => x.TenantId == tenantId && x.Email == email.GetValue(),
            cancellationToken);

        return record is null ? null : Rehydrate(record);
    }

    public async Task<IReadOnlyList<UserAccountAggregate>> GetAllAsync(Guid? tenantId = null, CancellationToken cancellationToken = default)
    {
        IQueryable<UserAccountRecord> query = dbContext.UserAccounts.AsSplitQuery()
            .Include(x => x.MfaEnrollments)
            .Include(x => x.PasswordCredentials);

        if (tenantId.HasValue)
        {
            query = query.Where(x => x.TenantId == tenantId.Value);
        }

        var records = await query.OrderBy(x => x.Email).ToListAsync(cancellationToken);

        return records.Select(Rehydrate).ToList();
    }

    /// <inheritdoc/>
    public async Task<(IReadOnlyList<UserAccountAggregate> Items, int TotalCount)> GetPagedAsync(
        int page, int pageSize, string? search, string? status, string sortBy, string sortOrder,
        Guid? tenantId = null, CancellationToken cancellationToken = default)
    {
        // REC-12: DB-level pagination — only retrieve the requested page from SQL Server.
        var query = dbContext.UserAccounts.AsQueryable();

        if (tenantId.HasValue)
            query = query.Where(u => u.TenantId == tenantId.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var lower = search.ToLower();
            query = query.Where(u => u.Email.ToLower().Contains(lower));
        }

        if (!string.IsNullOrWhiteSpace(status) &&
            !string.Equals(status, QueryDefaults.AllStatuses, StringComparison.OrdinalIgnoreCase))
        {
            // StatusId filtering would require the int value; application filters after fetch
            // for status since it maps int → enum name at the domain layer.
        }

        var totalCount = await query.CountAsync(cancellationToken);

        query = (sortBy?.ToLower(), sortOrder?.ToLower()) switch
        {
            ("email", QueryDefaults.Descending) => query.OrderByDescending(u => u.Email),
            _                 => query.OrderBy(u => u.Email),
        };

        var records = await query
            .AsSplitQuery()
            .Include(x => x.MfaEnrollments)
            .Include(x => x.PasswordCredentials)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (records.Select(Rehydrate).ToList(), totalCount);
    }

    public async Task<IReadOnlyList<UserAccountAggregate>> GetByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var records = await dbContext.UserAccounts
            .AsSplitQuery()
            .Include(x => x.MfaEnrollments)
            .Include(x => x.PasswordCredentials)
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.Email)
            .ToListAsync(cancellationToken);

        return records.Select(Rehydrate).ToList();
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Design note: Uses EF change tracking (not ExecuteUpdateAsync) so the soft-delete and
    /// GDPR anonymization changes are batched with the outbox message in a single
    /// SaveChangesAsync call when the caller later invokes SaveEntitiesAsync.
    ///
    /// The caller MUST first call UpdateAsync (to register the aggregate in _trackedAggregates
    /// for outbox message creation) and then call SoftDeleteAsync (which overwrites the email
    /// with the anonymized token). Both modifications target the same EF identity-map instance.
    /// </remarks>
    public async Task<bool> SoftDeleteAsync(Guid id, string deletedBy, CancellationToken cancellationToken = default)
    {
        // IgnoreQueryFilters so we can detect an already-deleted row (return false = idempotent).
        // EF's identity map returns the same tracked instance that UpdateAsync already loaded,
        // so modifying `record` here is additive to the changes Apply() already staged.
        var record = await dbContext.UserAccounts
            .IgnoreQueryFilters()
            .Include(x => x.PasswordCredentials)
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);

        if (record is null) return false;

        var now = DateTime.UtcNow;
        record.IsDeleted = true;
        record.DeletedAtUtc = now;
        record.DeletedBy = deletedBy;
        // GDPR: replace PII with a deterministic, irreversible token (SHA-256 of the GUID).
        record.Email = BuildAnonymizedEmail(id);
        record.IdentityReference = null;
        // GDPR (user-account.md:464): DisplayName is PII and the BCrypt hash must be nulled on
        // terminal deletion — the presence of a credential record would otherwise imply a local
        // account. Both are cleared here so no reversible secret or PII survives the soft-delete.
        record.DisplayName = null;
        foreach (var credential in record.PasswordCredentials)
        {
            credential.PasswordHash = string.Empty;
        }
        record.AnonymizedAtUtc = now;
        // EF change tracker now has IsDeleted=true + anonymized email pending; SaveChangesAsync
        // (called from SaveEntitiesAsync by the handler) will commit everything atomically.
        return true;
    }

    private static string BuildAnonymizedEmail(Guid id)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(id.ToString()));
        var prefix = Convert.ToHexString(hash)[..16].ToLower();
        return $"gdpr_del_{prefix}@anonymized.invalid";
    }

    public Task AddAsync(UserAccountAggregate aggregate, CancellationToken cancellationToken = default)
    {
        dbContext.UserAccounts.Add(ToRecord(aggregate));
        _trackedAggregates.Add(aggregate);
        return Task.CompletedTask;
    }

    public async Task UpdateAsync(UserAccountAggregate aggregate, CancellationToken cancellationToken = default)
    {
        var existing = await dbContext.UserAccounts
            .Include(x => x.MfaEnrollments)
            .Include(x => x.PasswordCredentials)
            .FirstOrDefaultAsync(x => x.Id == aggregate.Props.Id.GetValue(), cancellationToken)
            ?? throw new InvalidOperationException($"User account {aggregate.Props.Id.GetValue()} does not exist.");

        Apply(existing, aggregate);
        _trackedAggregates.Add(aggregate);
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => dbContext.SaveChangesAsync(cancellationToken);

    public async Task<bool> SaveEntitiesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var aggregate in _trackedAggregates)
        {
            await dbContext.PublishDomainEventsAsync(aggregate.DomainEvents.GetUncommittedChanges(), cancellationToken);
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException ex)
        {
            // FIX-03: surface optimistic concurrency failures as 409 Conflict
            var entry = ex.Entries.FirstOrDefault();
            var id = (Guid)(entry?.Property("Id").CurrentValue ?? Guid.Empty);
            throw new ConcurrencyConflictException(entry?.Metadata.Name ?? "Unknown", id);
        }

        foreach (var aggregate in _trackedAggregates)
        {
            aggregate.DomainEvents.MarkChangesAsCommitted();
        }

        _trackedAggregates.Clear();
        return true;
    }

    public void Dispose() => dbContext.Dispose();

    /// <summary>
    /// Prueba de «primer arranque». <c>IgnoreQueryFilters</c> no es una optimización ni una
    /// comodidad: el filtro global de <c>UserAccountRecord</c> combina <c>!IsDeleted</c> con el
    /// inquilino en contexto, y sin desactivarlo una base cuyas cuentas estén todas soft-borradas
    /// respondería «vacía». El arranque en frío la tomaría por virgen y crearía otro administrador
    /// con la credencial del entorno.
    /// </summary>
    public Task<bool> AnyUserAccountExistsAsync(CancellationToken cancellationToken = default)
        => dbContext.UserAccounts.IgnoreQueryFilters().AnyAsync(cancellationToken);

    // ── Dependency guard queries ────────────────────────────────────────────

    public Task<int> CountActiveByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => dbContext.UserAccounts.CountAsync(
            u => u.TenantId == tenantId && u.StatusId == 2 /* Active */,
            cancellationToken);

    public Task<int> CountActiveByBranchAsync(Guid branchId, CancellationToken cancellationToken = default)
        => dbContext.UserAccounts.CountAsync(
            u => u.BranchId == branchId && u.StatusId == 2 /* Active */,
            cancellationToken);

    private static UserAccountAggregate Rehydrate(UserAccountRecord record)
        => IdentityAggregateFactory.RehydrateUserAccount(record, record.MfaEnrollments, record.PasswordCredentials);

    private static UserAccountRecord ToRecord(UserAccountAggregate aggregate)
    {
        var audit = aggregate.Props.Audit.GetValue();
        return new UserAccountRecord
        {
            Id = aggregate.Props.Id.GetValue(),
            TenantId = aggregate.Props.TenantId.GetValue(),
            BranchId = aggregate.Props.BranchId?.GetValue(),
            Email = aggregate.Email.GetValue(),
            DisplayName = aggregate.DisplayName?.GetValue(),
            CategoryId = aggregate.Category.Id,
            StatusId = aggregate.Status.Id,
            IdentityReference = aggregate.IdentityReference?.GetValue(),
            IdentityReferenceTypeId = aggregate.IdentityReferenceType?.Id,
            ExpiresAtUtc = aggregate.ExpiresAt.HasValue ? aggregate.ExpiresAt.Value.UtcDateTime : null,
            // ADR-UMS-095: bloqueo temporal por intentos fallidos.
            FailedLoginAttempts = aggregate.FailedLoginAttempts,
            LockedUntilUtc = aggregate.LockedUntil.HasValue ? aggregate.LockedUntil.Value.UtcDateTime : null,
            CreatedBy = audit.CreatedBy,
            CreatedAtUtc = audit.CreatedAt,
            UpdatedBy = audit.UpdatedBy,
            UpdatedAtUtc = audit.UpdatedAt,
            AuditTimeSpan = audit.TimeSpan,
            MfaEnrollments = aggregate.MfaEnrollments.Select(x =>
            {
                var a = x.Props.Audit.GetValue();
                return new UserAccountMfaEnrollmentRecord
                {
                    Id = x.Props.Id.GetValue(),
                    UserAccountId = x.Props.UserAccountId.GetValue(),
                    MethodId = x.Method.Id,
                    StatusId = x.Status.Id,
                    CreatedBy = a.CreatedBy,
                    CreatedAtUtc = a.CreatedAt,
                    UpdatedBy = a.UpdatedBy,
                    UpdatedAtUtc = a.UpdatedAt,
                    AuditTimeSpan = a.TimeSpan,
                };
            }).ToList(),
            PasswordCredentials = aggregate.PasswordCredentials.Select(x =>
            {
                var a = x.Props.Audit.GetValue();
                return new UserAccountPasswordCredentialRecord
                {
                    Id = x.Props.Id.GetValue(),
                    UserAccountId = x.Props.UserAccountId.GetValue(),
                    PasswordHash = x.PasswordHash.GetValue(),
                    IsActive = x.IsActive,
                    CreatedBy = a.CreatedBy,
                    CreatedAtUtc = a.CreatedAt,
                    UpdatedBy = a.UpdatedBy,
                    UpdatedAtUtc = a.UpdatedAt,
                    AuditTimeSpan = a.TimeSpan,
                };
            }).ToList(),
        };
    }

    private void Apply(UserAccountRecord target, UserAccountAggregate source)
    {
        var replacement = ToRecord(source);

        target.TenantId = replacement.TenantId;
        target.BranchId = replacement.BranchId;
        target.Email = replacement.Email;
        target.DisplayName = replacement.DisplayName;
        target.CategoryId = replacement.CategoryId;
        target.StatusId = replacement.StatusId;
        target.IdentityReference = replacement.IdentityReference;
        target.IdentityReferenceTypeId = replacement.IdentityReferenceTypeId;
        target.ExpiresAtUtc = replacement.ExpiresAtUtc;
        // ADR-UMS-095: bloqueo temporal por intentos fallidos.
        target.FailedLoginAttempts = replacement.FailedLoginAttempts;
        target.LockedUntilUtc = replacement.LockedUntilUtc;
        target.CreatedBy = replacement.CreatedBy;
        target.CreatedAtUtc = replacement.CreatedAtUtc;
        target.UpdatedBy = replacement.UpdatedBy;
        target.UpdatedAtUtc = replacement.UpdatedAtUtc;
        target.AuditTimeSpan = replacement.AuditTimeSpan;

        EfChildCollectionReconciler.ReconcileById(
            dbContext,
            target.MfaEnrollments,
            replacement.MfaEnrollments,
            enrollment => enrollment.Id,
            UpdateMfaEnrollment);

        EfChildCollectionReconciler.ReconcileById(
            dbContext,
            target.PasswordCredentials,
            replacement.PasswordCredentials,
            credential => credential.Id,
            UpdatePasswordCredential);
    }

    private static void UpdateMfaEnrollment(UserAccountMfaEnrollmentRecord target, UserAccountMfaEnrollmentRecord source)
    {
        target.UserAccountId = source.UserAccountId;
        target.MethodId = source.MethodId;
        target.StatusId = source.StatusId;
        target.CreatedBy = source.CreatedBy;
        target.CreatedAtUtc = source.CreatedAtUtc;
        target.UpdatedBy = source.UpdatedBy;
        target.UpdatedAtUtc = source.UpdatedAtUtc;
        target.AuditTimeSpan = source.AuditTimeSpan;
    }

    private static void UpdatePasswordCredential(UserAccountPasswordCredentialRecord target, UserAccountPasswordCredentialRecord source)
    {
        target.UserAccountId = source.UserAccountId;
        target.PasswordHash = source.PasswordHash;
        target.IsActive = source.IsActive;
        target.CreatedBy = source.CreatedBy;
        target.CreatedAtUtc = source.CreatedAtUtc;
        target.UpdatedBy = source.UpdatedBy;
        target.UpdatedAtUtc = source.UpdatedAtUtc;
        target.AuditTimeSpan = source.AuditTimeSpan;
    }
}
