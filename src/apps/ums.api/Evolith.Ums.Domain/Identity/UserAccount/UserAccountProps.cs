namespace Evolith.Ums.Domain.Identity.UserAccount;

public class UserAccountProps : IProps
{
    public IdValueObject Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public BranchId? BranchId { get; private set; }
    public Email Email { get; private set; }
    public Name? DisplayName { get; private set; }
    public UserCategory Category { get; private set; }
    public UserStatus Status { get; private set; }
    public IdentityReference? IdentityReference { get; private set; }
    public IdentityReferenceType? IdentityReferenceType { get; private set; }
    public DateTimeOffset? ExpiresAt { get; private set; }

    // ADR-UMS-095: bloqueo temporal de cuenta por intentos fallidos de autenticación.
    // Distinto del bloqueo administrativo permanente (Status == Blocked): este es
    // auto-expirable vía LockedUntil y no requiere intervención de un administrador.
    public int FailedLoginAttempts { get; private set; }
    public DateTimeOffset? LockedUntil { get; private set; }

    public AuditValueObject Audit { get; private set; }

    public UserAccountProps(
        IdValueObject id,
        TenantId tenantId,
        Email email,
        UserCategory category,
        IdentityReference? identityReference,
        IdentityReferenceType? identityReferenceType,
        ActorId createdBy,
        BranchId? branchId = null,
        Name? displayName = null)
    {
        Id = id;
        TenantId = tenantId;
        BranchId = branchId;
        Email = email;
        DisplayName = displayName;
        Category = category;
        Status = UserStatus.Pending;
        IdentityReference = identityReference;
        IdentityReferenceType = identityReferenceType;
        FailedLoginAttempts = 0;
        LockedUntil = null;
        Audit = AuditValueObject.Create(createdBy.GetValue());
    }

    public UserAccountProps(
        IdValueObject id,
        TenantId tenantId,
        BranchId? branchId,
        Email email,
        UserCategory category,
        UserStatus status,
        IdentityReference? identityReference,
        IdentityReferenceType? identityReferenceType,
        AuditValueObject audit,
        Name? displayName = null,
        DateTimeOffset? expiresAt = null,
        int failedLoginAttempts = 0,
        DateTimeOffset? lockedUntil = null)
    {
        Id = id;
        TenantId = tenantId;
        BranchId = branchId;
        Email = email;
        DisplayName = displayName;
        Category = category;
        Status = status;
        IdentityReference = identityReference;
        IdentityReferenceType = identityReferenceType;
        ExpiresAt = expiresAt;
        FailedLoginAttempts = failedLoginAttempts;
        LockedUntil = lockedUntil;
        Audit = audit;
    }

    public UserAccountProps WithStatus(UserStatus status)
    {
        var clone = (UserAccountProps)MemberwiseClone();
        clone.Status = status;
        return clone;
    }

    public UserAccountProps WithExpiresAt(DateTimeOffset expiresAt)
    {
        var clone = (UserAccountProps)MemberwiseClone();
        clone.ExpiresAt = expiresAt;
        return clone;
    }

    /// <summary>
    /// Nombre para mostrar. Admite <c>null</c> para volver a «sin nombre», que es un estado válido
    /// —una cuenta puede no tenerlo— y distinto de la cadena vacía.
    /// </summary>
    public UserAccountProps WithDisplayName(Name? displayName)
    {
        var clone = (UserAccountProps)MemberwiseClone();
        clone.DisplayName = displayName;
        return clone;
    }

    // ADR-UMS-095: muta el estado de bloqueo temporal de forma atómica (contador + vencimiento).
    public UserAccountProps WithLockoutState(int failedLoginAttempts, DateTimeOffset? lockedUntil)
    {
        var clone = (UserAccountProps)MemberwiseClone();
        clone.FailedLoginAttempts = failedLoginAttempts;
        clone.LockedUntil = lockedUntil;
        return clone;
    }

    public object Clone() => MemberwiseClone();
}
