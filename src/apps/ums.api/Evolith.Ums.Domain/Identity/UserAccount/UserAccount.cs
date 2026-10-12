namespace Evolith.Ums.Domain.Identity.UserAccount;

using Evolith.Ums.Domain.Events;
using Evolith.Ums.Domain.Enums;
using Evolith.Ums.Domain.Identity.UserAccount.MfaEnrollment;
using Evolith.Ums.Domain.Identity.UserAccount.PasswordCredential;
using MfaEnrollmentEntity = Evolith.Ums.Domain.Identity.UserAccount.MfaEnrollment.MfaEnrollment;
using PasswordCredentialEntity = Evolith.Ums.Domain.Identity.UserAccount.PasswordCredential.PasswordCredential;

public sealed class UserAccount : AggregateRoot<UserAccount, UserAccountProps>
{
    private readonly List<MfaEnrollmentEntity> _mfaEnrollments = new();
    private readonly List<PasswordCredentialEntity> _passwordCredentials = new();

    public new UserAccountDomainEventsManager DomainEvents { get; }

    private UserAccount(UserAccountProps props) : base(props)
    {
        DomainEvents = new UserAccountDomainEventsManager(this);

        if (TrackingState.IsNew)
        {
            DomainEvents.RaiseEvent(new UserRegisteredEvent(
                Props.Id.GetValue(),
                Props.TenantId.GetValue(),
                Props.BranchId?.GetValue(),
                Props.Category.Name,
                Props.IdentityReference?.GetValue()));
        }
    }

    public TenantId TenantId => Props.TenantId;
    public BranchId? BranchId => Props.BranchId;

    /// <summary>
    /// El sujeto con el que esta cuenta viaja en el token semantico de cliente.
    ///
    /// Se calcula IGUAL que el grafo (`AuthorizationGraphBuilderService`): `IdentityReference` si
    /// la hay, y el correo si no. Si las dos formulas divergieran, la revocacion dejaria de
    /// alcanzar al token —que es justo el defecto que G-202 describe— y lo haria en silencio.
    /// </summary>
    public string SujetoDelToken()
        => Props.IdentityReference?.GetValue() ?? Props.Email.GetValue();
    public Email Email => Props.Email;
    public Name? DisplayName => Props.DisplayName;
    public UserCategory Category => Props.Category;
    public UserStatus Status => Props.Status;
    public IdentityReference? IdentityReference => Props.IdentityReference;
    public IdentityReferenceType? IdentityReferenceType => Props.IdentityReferenceType;
    public DateTimeOffset? ExpiresAt => Props.ExpiresAt;

    // ADR-UMS-095: estado del bloqueo temporal por intentos fallidos.
    public int FailedLoginAttempts => Props.FailedLoginAttempts;
    public DateTimeOffset? LockedUntil => Props.LockedUntil;

    public IReadOnlyCollection<MfaEnrollmentEntity> MfaEnrollments => _mfaEnrollments.AsReadOnly();
    public IReadOnlyCollection<PasswordCredentialEntity> PasswordCredentials => _passwordCredentials.AsReadOnly();

    public PasswordHash? ActivePasswordHash => _passwordCredentials.FirstOrDefault(c => c.IsActive)?.PasswordHash;

    public UserAccountId GetId() => UserAccountId.Load(Props.Id.GetValue());

    public static Result<UserAccount> Create(
        TenantId tenantId,
        Email email,
        UserCategory category,
        IdentityReference? identityReference,
        IdentityReferenceType? identityReferenceType,
        ActorId createdBy,
        BranchId? branchId = null,
        UserAccountId? userAccountId = null,
        Name? displayName = null)
    {
        var id = userAccountId ?? UserAccountId.Load(IdValueObject.Create().GetValue());
        var props = new UserAccountProps(
            id,
            tenantId,
            email,
            category,
            identityReference,
            identityReferenceType,
            createdBy,
            branchId,
            displayName);

        var userAccount = new UserAccount(props);

        if (!userAccount.IsValid())
        {
            return Result<UserAccount>.Failure(userAccount.BrokenRules.GetBrokenRulesAsString());
        }

        return Result<UserAccount>.Success(userAccount);
    }

    public Result Activate(ActorId updatedBy)
    {
        if (Status != UserStatus.Pending)
        {
            BrokenRules.Add(new BrokenRule(nameof(Status), DomainErrors.UserAccount.CannotActivate));
        }

        if (!IsValid())
        {
            return Result.Failure(BrokenRules.GetBrokenRulesAsString());
        }

        SetProps(Props.WithStatus(UserStatus.Active));
        DomainEvents.RaiseEvent(new UserActivatedEvent(Props.Id.GetValue(), Props.TenantId.GetValue()));
        TrackingState.MarkAsDirty();
        Props.Audit.Update(updatedBy.GetValue());
        return Result.Success();
    }

    /// <summary>
    /// Fija o corrige el nombre para mostrar.
    ///
    /// <para>El agregado admitía el nombre en el alta desde siempre, pero NADA podía cambiarlo
    /// después: no existía este método ni ningún comando que lo invocara. Una cuenta creada por API
    /// —o por la aplicación web, que usa el mismo endpoint— nacía sin nombre y se quedaba así para
    /// siempre; la única salida era borrarla, y el borrado lógico anonimiza el correo (REC-16), de
    /// modo que ni siquiera se podía recrear con la misma dirección.</para>
    ///
    /// <para>Una cuenta ELIMINADA no admite cambios: su correo ya fue anonimizado y escribirle un
    /// nombre reintroduciría un dato personal en una fila que se anonimizó a propósito. Una cuenta
    /// bloqueada sí: bloquear suspende el acceso, no la administración del dato.</para>
    /// </summary>
    public Result SetDisplayName(Name? displayName, ActorId updatedBy)
    {
        if (Status == UserStatus.Deleted)
        {
            BrokenRules.Add(new BrokenRule(nameof(Status), DomainErrors.UserAccount.CannotUpdateDeleted));
        }

        if (!IsValid())
        {
            return Result.Failure(BrokenRules.GetBrokenRulesAsString());
        }

        SetProps(Props.WithDisplayName(displayName));
        TrackingState.MarkAsDirty();
        Props.Audit.Update(updatedBy.GetValue());
        return Result.Success();
    }

    public Result Deny(ActorId updatedBy, string? reason = null)
    {
        if (Status != UserStatus.Pending)
            BrokenRules.Add(new BrokenRule(nameof(Status), DomainErrors.UserAccount.CannotDeny));

        if (!IsValid())
            return Result.Failure(BrokenRules.GetBrokenRulesAsString());

        SetProps(Props.WithStatus(UserStatus.Denied));
        DomainEvents.RaiseEvent(new UserSignupDeniedEvent(Props.Id.GetValue(), Props.TenantId.GetValue(), reason));
        TrackingState.MarkAsDirty();
        Props.Audit.Update(updatedBy.GetValue());
        return Result.Success();
    }

    public Result Block(Reason reason, ActorId updatedBy)
    {
        if (Status == UserStatus.Blocked)
        {
            BrokenRules.Add(new BrokenRule(nameof(Status), DomainErrors.UserAccount.AlreadyBlocked));
        }

        if (!IsValid())
        {
            return Result.Failure(BrokenRules.GetBrokenRulesAsString());
        }

        SetProps(Props.WithStatus(UserStatus.Blocked));
        DomainEvents.RaiseEvent(new UserBlockedEvent(
            Props.Id.GetValue(),
            Props.TenantId.GetValue(),
            reason.GetValue(),
            SujetoDelToken()));
        TrackingState.MarkAsDirty();
        Props.Audit.Update(updatedBy.GetValue());
        return Result.Success();
    }

    public Result Restore(ActorId updatedBy)
    {
        if (Status != UserStatus.Blocked)
        {
            BrokenRules.Add(new BrokenRule(nameof(Status), DomainErrors.UserAccount.CannotRestore));
        }

        if (!IsValid())
        {
            return Result.Failure(BrokenRules.GetBrokenRulesAsString());
        }

        SetProps(Props.WithStatus(UserStatus.Active));
        DomainEvents.RaiseEvent(new UserRestoredEvent(Props.Id.GetValue(), Props.TenantId.GetValue()));
        TrackingState.MarkAsDirty();
        Props.Audit.Update(updatedBy.GetValue());
        return Result.Success();
    }

    /// <summary>
    /// REC-16: Soft-delete — transitions the account to the <see cref="UserStatus.Deleted"/> terminal state.
    /// Call <c>SoftDeleteAsync</c> on the repository afterwards to persist and anonymize PII in the DB.
    /// </summary>
    public Result Delete(ActorId deletedBy, int activeProfileCount = 0)
    {
        if (Status == UserStatus.Deleted)
        {
            BrokenRules.Add(new BrokenRule(nameof(Status), DomainErrors.UserAccount.AlreadyDeleted));
        }

        if (activeProfileCount > 0)
        {
            BrokenRules.Add(new BrokenRule(nameof(Status), DomainErrors.UserAccount.HasActiveProfiles));
        }

        if (!IsValid())
        {
            return Result.Failure(BrokenRules.GetBrokenRulesAsString());
        }

        SetProps(Props.WithStatus(UserStatus.Deleted));
        DomainEvents.RaiseEvent(new UserDeletedEvent(
            Props.Id.GetValue(), Props.TenantId.GetValue(), SujetoDelToken()));
        TrackingState.MarkAsDirty();
        Props.Audit.Update(deletedBy.GetValue());
        return Result.Success();
    }

    public Result AddPassword(PasswordHash passwordHash, ActorId createdBy)
    {
        if (Status == UserStatus.Blocked)
        {
            BrokenRules.Add(new BrokenRule(nameof(Status), DomainErrors.UserAccount.BlockedCannotUpdateCredentials));
        }

        if (IdentityReference is not null)
        {
            BrokenRules.Add(new BrokenRule(nameof(PasswordCredentials), DomainErrors.UserAccount.FederatedCannotUseLocalPassword));
        }

        if (passwordHash.IsEmpty())
        {
            BrokenRules.Add(new BrokenRule(nameof(PasswordHash), DomainErrors.UserAccount.PasswordHashRequired));
        }

        if (!IsValid())
        {
            return Result.Failure(BrokenRules.GetBrokenRulesAsString());
        }

        var credentialResult = PasswordCredentialEntity.Create(GetId(), passwordHash, createdBy);
        if (credentialResult.IsFailure)
        {
            return Result.Failure(credentialResult.Error);
        }

        foreach (var credential in _passwordCredentials)
        {
            credential.DeactivateInternal();
        }
        credentialResult.Value.ActivateInternal();

        _passwordCredentials.Add(credentialResult.Value);
        TrackingState.MarkAsDirty();
        Props.Audit.Update(createdBy.GetValue());
        return Result.Success();
    }

    public Result ActivatePassword(IdValueObject credentialId, ActorId updatedBy)
    {
        if (Status == UserStatus.Blocked)
        {
            BrokenRules.Add(new BrokenRule(nameof(Status), DomainErrors.UserAccount.BlockedCannotUpdateCredentials));
        }

        if (IdentityReference is not null)
        {
            BrokenRules.Add(new BrokenRule(nameof(PasswordCredentials), DomainErrors.UserAccount.FederatedCannotUseLocalPassword));
        }

        var credential = FindPasswordCredential(credentialId);
        if (credential.IsFailure)
        {
            BrokenRules.Add(new BrokenRule(nameof(PasswordCredentials), DomainErrors.Common.NotFound));
        }

        if (!IsValid())
        {
            return Result.Failure(BrokenRules.GetBrokenRulesAsString());
        }

        foreach (var c in _passwordCredentials)
        {
            c.DeactivateInternal();
        }

        credential.Value.ActivateInternal();
        TrackingState.MarkAsDirty();
        Props.Audit.Update(updatedBy.GetValue());
        return Result.Success();
    }

    public Result RemovePassword(IdValueObject credentialId, ActorId updatedBy)
    {
        if (Status == UserStatus.Blocked)
        {
            BrokenRules.Add(new BrokenRule(nameof(Status), DomainErrors.UserAccount.BlockedCannotUpdateCredentials));
        }

        var credential = FindPasswordCredential(credentialId);
        if (credential.IsFailure)
        {
            BrokenRules.Add(new BrokenRule(nameof(PasswordCredentials), DomainErrors.Common.NotFound));
        }

        if (credential.IsSuccess && credential.Value.IsActive && _passwordCredentials.Count == 1)
        {
            BrokenRules.Add(new BrokenRule(nameof(PasswordCredentials), DomainErrors.UserAccount.CannotRemoveLastPassword));
        }

        if (!IsValid())
        {
            return Result.Failure(BrokenRules.GetBrokenRulesAsString());
        }

        _passwordCredentials.Remove(credential.Value);
        TrackingState.MarkAsDirty();
        Props.Audit.Update(updatedBy.GetValue());
        return Result.Success();
    }

    public Result EnrollMfa(MfaMethod method, ActorId createdBy)
    {
        if (Status == UserStatus.Blocked)
        {
            BrokenRules.Add(new BrokenRule(nameof(Status), DomainErrors.UserAccount.BlockedCannotEnrollMfa));
        }

        if (_mfaEnrollments.Any(e => e.Method == method))
        {
            BrokenRules.Add(new BrokenRule(nameof(MfaEnrollments), DomainErrors.UserAccount.MfaAlreadyEnrolled));
        }

        if (!IsValid())
        {
            return Result.Failure(BrokenRules.GetBrokenRulesAsString());
        }

        var enrollmentResult = MfaEnrollmentEntity.Create(GetId(), method, createdBy);
        if (enrollmentResult.IsFailure)
        {
            return Result.Failure(enrollmentResult.Error);
        }

        _mfaEnrollments.Add(enrollmentResult.Value);
        DomainEvents.RaiseEvent(new MfaEnrolledEvent(
            Props.Id.GetValue(),
            Props.TenantId.GetValue(),
            method.Name));
        TrackingState.MarkAsDirty();
        Props.Audit.Update(createdBy.GetValue());
        return Result.Success();
    }

    public Result VerifyMfaChallenge(IdValueObject enrollmentId, ActorId updatedBy)
    {
        var enrollment = FindMfaEnrollment(enrollmentId);
        if (enrollment.IsFailure)
        {
            BrokenRules.Add(new BrokenRule(nameof(MfaEnrollments), DomainErrors.Common.NotFound));
        }

        if (!IsValid())
        {
            return Result.Failure(BrokenRules.GetBrokenRulesAsString());
        }

        var verifyResult = enrollment.Value.Verify(updatedBy);
        if (verifyResult.IsFailure)
        {
            return verifyResult;
        }

        DomainEvents.RaiseEvent(new MfaVerifiedEvent(
            Props.Id.GetValue(),
            Props.TenantId.GetValue(),
            enrollment.Value.Method.Name));
        TrackingState.MarkAsDirty();
        Props.Audit.Update(updatedBy.GetValue());
        return Result.Success();
    }

    public Result SetValidityPeriod(DateTimeOffset expiresAt, int maxDays, ActorId updatedBy)
    {
        if (expiresAt <= DateTimeOffset.UtcNow)
        {
            BrokenRules.Add(new BrokenRule(nameof(ExpiresAt), DomainErrors.UserAccount.ValidityPeriodInPast));
        }

        if (expiresAt > DateTimeOffset.UtcNow.AddDays(maxDays))
        {
            BrokenRules.Add(new BrokenRule(nameof(ExpiresAt), DomainErrors.UserAccount.ValidityPeriodExceedsMaximum));
        }

        if (!IsValid())
        {
            return Result.Failure(BrokenRules.GetBrokenRulesAsString());
        }

        var previous = Props.ExpiresAt;
        SetProps(Props.WithExpiresAt(expiresAt));
        DomainEvents.RaiseEvent(new ValidityPeriodModifiedEvent(
            Props.Id.GetValue(),
            Props.TenantId.GetValue(),
            previous,
            expiresAt));
        TrackingState.MarkAsDirty();
        Props.Audit.Update(updatedBy.GetValue());
        return Result.Success();
    }

    public Result RevokeEnrollment(IdValueObject enrollmentId, ActorId updatedBy)
    {
        var enrollment = FindMfaEnrollment(enrollmentId);
        if (enrollment.IsFailure)
        {
            BrokenRules.Add(new BrokenRule(nameof(MfaEnrollments), DomainErrors.UserAccount.MfaEnrollmentNotFound));
        }

        if (!IsValid())
        {
            return Result.Failure(BrokenRules.GetBrokenRulesAsString());
        }

        _mfaEnrollments.Remove(enrollment.Value);
        DomainEvents.RaiseEvent(new MfaEnrollmentRevokedEvent(
            Props.Id.GetValue(),
            Props.TenantId.GetValue(),
            enrollmentId.GetValue(),
            enrollment.Value.Method.Name));
        TrackingState.MarkAsDirty();
        Props.Audit.Update(updatedBy.GetValue());
        return Result.Success();
    }

    public bool HasVerifiedMfaEnrollment(IEnumerable<MfaMethod>? allowedMethods = null)
    {
        var verifiedEnrollments = _mfaEnrollments.Where(e => e.Status == MfaEnrollmentStatus.Verified);

        if (allowedMethods is null)
        {
            return verifiedEnrollments.Any();
        }

        var allowedMethodNames = new HashSet<string>(
            allowedMethods.Select(method => method.Name),
            StringComparer.OrdinalIgnoreCase);

        return verifiedEnrollments.Any(enrollment => allowedMethodNames.Contains(enrollment.Method.Name));
    }

    /// <summary>
    /// ADR-UMS-095: registra un intento de autenticación y aplica el bloqueo temporal por
    /// intentos fallidos. El dominio es determinista: recibe el instante actual (<paramref name="now"/>)
    /// y los parámetros de política (<paramref name="maxAttempts"/>, <paramref name="lockoutMinutes"/>)
    /// desde la aplicación; no lee reloj ni configuración por su cuenta.
    /// </summary>
    /// <remarks>
    /// - <c>success == false</c>: incrementa <see cref="FailedLoginAttempts"/>; al alcanzar
    ///   <paramref name="maxAttempts"/> fija <see cref="LockedUntil"/> = <paramref name="now"/> +
    ///   <paramref name="lockoutMinutes"/>. No incrementa si ya está bloqueado (<see cref="IsLockedOut"/>).
    /// - <c>success == true</c>: resetea contador y bloqueo.
    /// El bloqueo temporal es independiente del bloqueo administrativo permanente (<see cref="Block"/> /
    /// <see cref="UserStatus.Blocked"/>), que prevalece porque el flujo de autenticación rechaza antes
    /// a las cuentas no activas.
    /// </remarks>
    public Result RecordAuthenticationAttempt(
        bool success,
        DateTimeOffset now,
        int maxAttempts,
        int lockoutMinutes,
        string reason,
        string ipAddress,
        ActorId actor)
    {
        if (success)
        {
            if (Props.FailedLoginAttempts != 0 || Props.LockedUntil is not null)
            {
                SetProps(Props.WithLockoutState(0, null));
                TrackingState.MarkAsDirty();
                Props.Audit.Update(actor.GetValue());
            }
        }
        else if (!IsLockedOut(now))
        {
            var attempts = Props.FailedLoginAttempts + 1;
            var lockedUntil = maxAttempts > 0 && attempts >= maxAttempts
                ? (DateTimeOffset?)now.AddMinutes(lockoutMinutes)
                : Props.LockedUntil;

            SetProps(Props.WithLockoutState(attempts, lockedUntil));
            TrackingState.MarkAsDirty();
            Props.Audit.Update(actor.GetValue());
        }

        DomainEvents.RaiseEvent(new AuthenticationAttemptedEvent(
            Props.Id.GetValue(),
            Props.TenantId.GetValue(),
            success,
            reason,
            ipAddress));
        return Result.Success();
    }

    /// <summary>
    /// ADR-UMS-095: consulta pura — indica si la cuenta está bajo bloqueo temporal en el instante dado.
    /// </summary>
    public bool IsLockedOut(DateTimeOffset now)
        => Props.LockedUntil is not null && Props.LockedUntil > now;

    private Result<PasswordCredentialEntity> FindPasswordCredential(IdValueObject credentialId)
    {
        // AT06/F1 (misma clase de bug): identidad canónica = Props.Id; el Id base de Entity<> se
        // regenera aleatorio en cada construcción y la rehidratación no llama SetId → buscar por c.Id
        // fallaba tras recargar (activate/remove de credencial por id → 404). Cf. FindMfaEnrollment.
        var credential = _passwordCredentials.FirstOrDefault(c => c.Props.Id.GetValue() == credentialId.GetValue());
        return credential is null
            ? Result<PasswordCredentialEntity>.Failure(DomainErrors.Common.NotFound)
            : Result<PasswordCredentialEntity>.Success(credential);
    }

    private Result<MfaEnrollmentEntity> FindMfaEnrollment(IdValueObject enrollmentId)
    {
        var enrollment = _mfaEnrollments.FirstOrDefault(e => e.GetId().GetValue() == enrollmentId.GetValue());
        return enrollment is null
            ? Result<MfaEnrollmentEntity>.Failure(DomainErrors.Common.NotFound)
            : Result<MfaEnrollmentEntity>.Success(enrollment);
    }
}
