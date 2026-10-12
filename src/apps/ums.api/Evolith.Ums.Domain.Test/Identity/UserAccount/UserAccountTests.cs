#pragma warning disable S4144 // Scaffolding intencional para testing In-Memory
﻿namespace Evolith.Ums.Domain.Test.Identity.UserAccount;

using Evolith.Ums.Domain.Identity.UserAccount;
using Evolith.Ums.Domain.Identity.UserAccount.MfaEnrollment;
using Evolith.Ums.Domain.Identity.UserAccount.PasswordCredential;
using Xunit;

public class UserAccountTests
{
    private static readonly TenantId ValidTenantId = TenantId.Load(Guid.NewGuid().ToString());
    private static readonly BranchId ValidBranchId = BranchId.Load(Guid.NewGuid().ToString());
    private static readonly Email ValidEmail = Email.Create("user@example.com");
    private static readonly ActorId ValidActor = ActorId.Create("user-001");
    private static readonly UserCategory ValidCategory = UserCategory.Internal;
    private static readonly PasswordHash ValidPasswordHash = PasswordHash.Create("hashedpassword123");
    #region Create

    [Fact]
    public void Create_WithValidData_ReturnsSuccess()
    {
        var result = UserAccount.Create(ValidTenantId, ValidEmail, ValidCategory, null, null, ValidActor);

        Assert.True(result.IsSuccess);
        Assert.Equal(ValidTenantId, result.Value.TenantId);
        Assert.Equal(ValidEmail, result.Value.Email);
        Assert.Equal(ValidCategory, result.Value.Category);
        Assert.Equal(UserStatus.Pending, result.Value.Status);
        Assert.Empty(result.Value.MfaEnrollments);
        Assert.Empty(result.Value.PasswordCredentials);
    }

    [Fact]
    public void Create_WithBranchId_SetsBranchScope()
    {
        var result = UserAccount.Create(
            ValidTenantId,
            ValidEmail,
            ValidCategory,
            null,
            null,
            ValidActor,
            ValidBranchId);

        Assert.True(result.IsSuccess);
        Assert.Equal(ValidBranchId, result.Value.BranchId);
    }

    [Fact]
    public void Create_WithInvalidEmail_ReturnsFailure()
    {
        var email = Email.Create("invalid-email");

        var result = UserAccount.Create(ValidTenantId, email, ValidCategory, null, null, ValidActor);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Create_RaisesUserRegisteredEvent()
    {
        var result = UserAccount.Create(ValidTenantId, ValidEmail, ValidCategory, null, null, ValidActor);

        Assert.True(result.IsSuccess);
        var events = result.Value.DomainEvents.GetUncommittedChanges().ToList();
        Assert.Single(events);
        Assert.IsType<UserRegisteredEvent>(events[0]);
    }

    [Fact]
    public void Create_WithIdentityReference_SetsIdentityReference()
    {
        var identityRef = IdentityReference.Create("external-id-123");
        var identityType = IdentityReferenceType.HrId;

        var result = UserAccount.Create(ValidTenantId, ValidEmail, ValidCategory, identityRef, identityType, ValidActor);

        Assert.True(result.IsSuccess);
        Assert.Equal(identityRef, result.Value.IdentityReference);
        Assert.Equal(identityType, result.Value.IdentityReferenceType);
    }

    #endregion

    #region Activate

    [Fact]
    public void Activate_WhenPending_ReturnsSuccess()
    {
        var user = UserAccount.Create(ValidTenantId, ValidEmail, ValidCategory, null, null, ValidActor).Value;

        var result = user.Activate(ValidActor);

        Assert.True(result.IsSuccess);
        Assert.Equal(UserStatus.Active, user.Status);
    }

    [Fact]
    public void Activate_WhenNotPending_ReturnsFailure()
    {
        var user = UserAccount.Create(ValidTenantId, ValidEmail, ValidCategory, null, null, ValidActor).Value;
        user.Activate(ValidActor);

        var result = user.Activate(ValidActor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.UserAccount.CannotActivate, result.Error);
    }

    [Fact]
    public void Activate_RaisesUserActivatedEvent()
    {
        var user = UserAccount.Create(ValidTenantId, ValidEmail, ValidCategory, null, null, ValidActor).Value;

        user.Activate(ValidActor);

        var events = user.DomainEvents.GetUncommittedChanges().ToList();
        Assert.Contains(events, e => e is UserActivatedEvent);
    }

    #endregion

    #region G-202 · el sujeto del token viaja en el evento de revocacion

    // El token semantico de cliente NO porta identificadores internos: su `sub` es el nombre de
    // usuario. La lista de revocacion se indexaba solo por id, asi que bloquear o eliminar una
    // cuenta NO invalidaba su token de satelite hasta que caducaba. El sujeto tiene que viajar EN
    // EL EVENTO, no consultarse despues: el borrado anonimiza los datos personales.

    [Fact]
    public void SujetoDelToken_SinIdentityReference_EsElCorreo()
    {
        var user = UserAccount.Create(ValidTenantId, ValidEmail, ValidCategory, null, null, ValidActor).Value;

        // Misma formula que `AuthorizationGraphBuilderService`: si divergieran, la revocacion
        // dejaria de alcanzar al token y lo haria en silencio.
        Assert.Equal(ValidEmail.GetValue(), user.SujetoDelToken());
    }

    [Fact]
    public void Block_EmiteElSujetoEnElEvento()
    {
        var user = UserAccount.Create(ValidTenantId, ValidEmail, ValidCategory, null, null, ValidActor).Value;
        user.Activate(ValidActor);

        user.Block(Reason.Create("Security violation"), ValidActor);

        var evento = user.DomainEvents.GetUncommittedChanges().OfType<UserBlockedEvent>().Single();
        Assert.Equal(user.SujetoDelToken(), evento.Subject);
    }

    [Fact]
    public void Delete_EmiteElSujetoEnElEvento()
    {
        var user = UserAccount.Create(ValidTenantId, ValidEmail, ValidCategory, null, null, ValidActor).Value;
        user.Activate(ValidActor);

        user.Delete(ValidActor);

        var evento = user.DomainEvents.GetUncommittedChanges().OfType<UserDeletedEvent>().Single();
        Assert.Equal(user.SujetoDelToken(), evento.Subject);
    }

    #endregion

    #region Block

    [Fact]
    public void Block_WhenActive_ReturnsSuccess()
    {
        var user = UserAccount.Create(ValidTenantId, ValidEmail, ValidCategory, null, null, ValidActor).Value;
        user.Activate(ValidActor);
        var reason = Reason.Create("Security violation");

        var result = user.Block(reason, ValidActor);

        Assert.True(result.IsSuccess);
        Assert.Equal(UserStatus.Blocked, user.Status);
    }

    [Fact]
    public void Block_WhenAlreadyBlocked_ReturnsFailure()
    {
        var user = UserAccount.Create(ValidTenantId, ValidEmail, ValidCategory, null, null, ValidActor).Value;
        user.Activate(ValidActor);
        var reason = Reason.Create("Security violation");
        user.Block(reason, ValidActor);

        var result = user.Block(reason, ValidActor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.UserAccount.AlreadyBlocked, result.Error);
    }

    [Fact]
    public void Block_RaisesUserBlockedEvent()
    {
        var user = UserAccount.Create(ValidTenantId, ValidEmail, ValidCategory, null, null, ValidActor).Value;
        user.Activate(ValidActor);
        var reason = Reason.Create("Security violation");

        user.Block(reason, ValidActor);

        var events = user.DomainEvents.GetUncommittedChanges().ToList();
        Assert.Contains(events, e => e is UserBlockedEvent);
    }

    #endregion

    #region Restore

    [Fact]
    public void Restore_WhenBlocked_ReturnsSuccess()
    {
        var user = UserAccount.Create(ValidTenantId, ValidEmail, ValidCategory, null, null, ValidActor).Value;
        user.Activate(ValidActor);
        var reason = Reason.Create("Security violation");
        user.Block(reason, ValidActor);

        var result = user.Restore(ValidActor);

        Assert.True(result.IsSuccess);
        Assert.Equal(UserStatus.Active, user.Status);
    }

    [Fact]
    public void Restore_WhenNotBlocked_ReturnsFailure()
    {
        var user = UserAccount.Create(ValidTenantId, ValidEmail, ValidCategory, null, null, ValidActor).Value;

        var result = user.Restore(ValidActor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.UserAccount.CannotRestore, result.Error);
    }

    [Fact]
    public void Restore_RaisesUserRestoredEvent()
    {
        var user = UserAccount.Create(ValidTenantId, ValidEmail, ValidCategory, null, null, ValidActor).Value;
        user.Activate(ValidActor);
        var reason = Reason.Create("Security violation");
        user.Block(reason, ValidActor);

        user.Restore(ValidActor);

        var events = user.DomainEvents.GetUncommittedChanges().ToList();
        Assert.Contains(events, e => e is UserRestoredEvent);
    }

    #endregion

    #region AddPassword

    [Fact]
    public void AddPassword_WithValidData_ReturnsSuccess()
    {
        var user = UserAccount.Create(ValidTenantId, ValidEmail, ValidCategory, null, null, ValidActor).Value;

        var result = user.AddPassword(ValidPasswordHash, ValidActor);

        Assert.True(result.IsSuccess);
        Assert.Single(user.PasswordCredentials);
        Assert.True(user.PasswordCredentials.First().IsActive);
    }

    [Fact]
    public void AddPassword_WhenBlocked_ReturnsFailure()
    {
        var user = UserAccount.Create(ValidTenantId, ValidEmail, ValidCategory, null, null, ValidActor).Value;
        user.Activate(ValidActor);
        var reason = Reason.Create("Security violation");
        user.Block(reason, ValidActor);

        var result = user.AddPassword(ValidPasswordHash, ValidActor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.UserAccount.BlockedCannotUpdateCredentials, result.Error);
    }

    [Fact]
    public void AddPassword_WhenAccountIsFederated_ReturnsFailure()
    {
        var user = UserAccount.Create(
            ValidTenantId,
            ValidEmail,
            ValidCategory,
            IdentityReference.Create("EMP-100"),
            IdentityReferenceType.HrId,
            ValidActor).Value;

        var result = user.AddPassword(ValidPasswordHash, ValidActor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.UserAccount.FederatedCannotUseLocalPassword, result.Error);
    }

    [Fact]
    public void AddPassword_WithEmptyHash_ReturnsFailure()
    {
        var user = UserAccount.Create(ValidTenantId, ValidEmail, ValidCategory, null, null, ValidActor).Value;
        var emptyHash = PasswordHash.Create("");

        var result = user.AddPassword(emptyHash, ValidActor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.UserAccount.PasswordHashRequired, result.Error);
    }

    [Fact]
    public void AddPassword_SecondPassword_DeactivatesFirst()
    {
        var user = UserAccount.Create(ValidTenantId, ValidEmail, ValidCategory, null, null, ValidActor).Value;
        user.AddPassword(ValidPasswordHash, ValidActor);
        var secondHash = PasswordHash.Create("newhashedpassword456");

        user.AddPassword(secondHash, ValidActor);

        Assert.Equal(2, user.PasswordCredentials.Count);
        Assert.False(user.PasswordCredentials.First().IsActive);
        Assert.True(user.PasswordCredentials.Last().IsActive);
    }

    #endregion

    #region ActivatePassword

    [Fact]
    public void ActivatePassword_WithValidId_ReturnsSuccess()
    {
        var user = UserAccount.Create(ValidTenantId, ValidEmail, ValidCategory, null, null, ValidActor).Value;
        user.AddPassword(ValidPasswordHash, ValidActor);
        var secondHash = PasswordHash.Create("newhashedpassword456");
        user.AddPassword(secondHash, ValidActor);
        var firstCredentialId = user.PasswordCredentials.First().GetId();

        var result = user.ActivatePassword(firstCredentialId, ValidActor);

        Assert.True(result.IsSuccess);
        Assert.True(user.PasswordCredentials.First().IsActive);
        Assert.False(user.PasswordCredentials.Last().IsActive);
    }

    [Fact]
    public void ActivatePassword_WhenNotFound_ReturnsFailure()
    {
        var user = UserAccount.Create(ValidTenantId, ValidEmail, ValidCategory, null, null, ValidActor).Value;
        var fakeId = IdValueObject.Create();

        var result = user.ActivatePassword(fakeId, ValidActor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Common.NotFound, result.Error);
    }

    [Fact]
    public void ActivatePassword_WhenBlocked_ReturnsFailure()
    {
        var user = UserAccount.Create(ValidTenantId, ValidEmail, ValidCategory, null, null, ValidActor).Value;
        user.AddPassword(ValidPasswordHash, ValidActor);
        user.Activate(ValidActor);
        var reason = Reason.Create("Security violation");
        user.Block(reason, ValidActor);
        var credentialId = user.PasswordCredentials.First().GetId();

        var result = user.ActivatePassword(credentialId, ValidActor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.UserAccount.BlockedCannotUpdateCredentials, result.Error);
    }

    #endregion

    #region RemovePassword

    [Fact]
    public void RemovePassword_WhenMultiplePasswords_ReturnsSuccess()
    {
        var user = UserAccount.Create(ValidTenantId, ValidEmail, ValidCategory, null, null, ValidActor).Value;
        user.AddPassword(ValidPasswordHash, ValidActor);
        var secondHash = PasswordHash.Create("newhashedpassword456");
        user.AddPassword(secondHash, ValidActor);
        var firstCredentialId = user.PasswordCredentials.First().GetId();

        var result = user.RemovePassword(firstCredentialId, ValidActor);

        Assert.True(result.IsSuccess);
        Assert.Single(user.PasswordCredentials);
    }

    [Fact]
    public void RemovePassword_WhenLastPassword_ReturnsFailure()
    {
        var user = UserAccount.Create(ValidTenantId, ValidEmail, ValidCategory, null, null, ValidActor).Value;
        user.AddPassword(ValidPasswordHash, ValidActor);
        var credentialId = user.PasswordCredentials.First().GetId();

        var result = user.RemovePassword(credentialId, ValidActor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.UserAccount.CannotRemoveLastPassword, result.Error);
    }

    [Fact]
    public void RemovePassword_WhenNotFound_ReturnsFailure()
    {
        var user = UserAccount.Create(ValidTenantId, ValidEmail, ValidCategory, null, null, ValidActor).Value;
        user.AddPassword(ValidPasswordHash, ValidActor);
        var fakeId = IdValueObject.Create();

        var result = user.RemovePassword(fakeId, ValidActor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Common.NotFound, result.Error);
    }

    #endregion

    #region EnrollMfa

    [Fact]
    public void EnrollMfa_WithValidData_ReturnsSuccess()
    {
        var user = UserAccount.Create(ValidTenantId, ValidEmail, ValidCategory, null, null, ValidActor).Value;
        var method = MfaMethod.Totp;

        var result = user.EnrollMfa(method, ValidActor);

        Assert.True(result.IsSuccess);
        Assert.Single(user.MfaEnrollments);
        Assert.Equal(method, user.MfaEnrollments.First().Method);
        Assert.Equal(MfaEnrollmentStatus.Enrolled, user.MfaEnrollments.First().Status);
    }

    [Fact]
    public void EnrollMfa_WhenAlreadyEnrolled_ReturnsFailure()
    {
        var user = UserAccount.Create(ValidTenantId, ValidEmail, ValidCategory, null, null, ValidActor).Value;
        var method = MfaMethod.Totp;
        user.EnrollMfa(method, ValidActor);

        var result = user.EnrollMfa(method, ValidActor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.UserAccount.MfaAlreadyEnrolled, result.Error);
    }

    [Fact]
    public void EnrollMfa_WhenBlocked_ReturnsFailure()
    {
        var user = UserAccount.Create(ValidTenantId, ValidEmail, ValidCategory, null, null, ValidActor).Value;
        user.Activate(ValidActor);
        var reason = Reason.Create("Security violation");
        user.Block(reason, ValidActor);
        var method = MfaMethod.Totp;

        var result = user.EnrollMfa(method, ValidActor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.UserAccount.BlockedCannotEnrollMfa, result.Error);
    }

    [Fact]
    public void EnrollMfa_RaisesMfaEnrolledEvent()
    {
        var user = UserAccount.Create(ValidTenantId, ValidEmail, ValidCategory, null, null, ValidActor).Value;
        var method = MfaMethod.Totp;

        user.EnrollMfa(method, ValidActor);

        var events = user.DomainEvents.GetUncommittedChanges().ToList();
        Assert.Contains(events, e => e is MfaEnrolledEvent);
    }

    #endregion

    #region VerifyMfaChallenge

    [Fact]
    public void VerifyMfaChallenge_WithValidEnrollment_ReturnsSuccess()
    {
        var user = UserAccount.Create(ValidTenantId, ValidEmail, ValidCategory, null, null, ValidActor).Value;
        var method = MfaMethod.Totp;
        user.EnrollMfa(method, ValidActor);
        var enrollmentId = user.MfaEnrollments.First().GetId();

        var result = user.VerifyMfaChallenge(enrollmentId, ValidActor);

        Assert.True(result.IsSuccess);
        Assert.Equal(MfaEnrollmentStatus.Verified, user.MfaEnrollments.First().Status);
    }

    [Fact]
    public void VerifyMfaChallenge_WhenEnrollmentNotFound_ReturnsFailure()
    {
        var user = UserAccount.Create(ValidTenantId, ValidEmail, ValidCategory, null, null, ValidActor).Value;
        var fakeId = IdValueObject.Create();

        var result = user.VerifyMfaChallenge(fakeId, ValidActor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Common.NotFound, result.Error);
    }

    [Fact]
    public void VerifyMfaChallenge_RaisesMfaVerifiedEvent()
    {
        var user = UserAccount.Create(ValidTenantId, ValidEmail, ValidCategory, null, null, ValidActor).Value;
        var method = MfaMethod.Totp;
        user.EnrollMfa(method, ValidActor);
        var enrollmentId = user.MfaEnrollments.First().GetId();

        user.VerifyMfaChallenge(enrollmentId, ValidActor);

        var events = user.DomainEvents.GetUncommittedChanges().ToList();
        Assert.Contains(events, e => e is MfaVerifiedEvent);
    }

    [Fact]
    public void VerifyMfaChallenge_WhenAlreadyVerified_ReturnsFailure()
    {
        var user = UserAccount.Create(ValidTenantId, ValidEmail, ValidCategory, null, null, ValidActor).Value;
        var method = MfaMethod.Totp;
        user.EnrollMfa(method, ValidActor);
        var enrollmentId = user.MfaEnrollments.First().GetId();
        user.VerifyMfaChallenge(enrollmentId, ValidActor);

        var result = user.VerifyMfaChallenge(enrollmentId, ValidActor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.UserAccount.MfaAlreadyVerified, result.Error);
    }

    #endregion

    #region SetValidityPeriod

    [Fact]
    public void SetValidityPeriod_WithFutureDate_ReturnsSuccess()
    {
        var user = UserAccount.Create(ValidTenantId, ValidEmail, ValidCategory, null, null, ValidActor).Value;
        var expiresAt = DateTimeOffset.UtcNow.AddDays(30);

        var result = user.SetValidityPeriod(expiresAt, 365, ValidActor);

        Assert.True(result.IsSuccess);
        Assert.Equal(expiresAt, user.ExpiresAt);
    }

    [Fact]
    public void SetValidityPeriod_WhenInPast_ReturnsFailure()
    {
        var user = UserAccount.Create(ValidTenantId, ValidEmail, ValidCategory, null, null, ValidActor).Value;
        var expiresAt = DateTimeOffset.UtcNow.AddDays(-1);

        var result = user.SetValidityPeriod(expiresAt, 365, ValidActor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.UserAccount.ValidityPeriodInPast, result.Error);
    }

    [Fact]
    public void SetValidityPeriod_WhenExceedsMaxDays_ReturnsFailure()
    {
        var user = UserAccount.Create(ValidTenantId, ValidEmail, ValidCategory, null, null, ValidActor).Value;
        var expiresAt = DateTimeOffset.UtcNow.AddDays(400);

        var result = user.SetValidityPeriod(expiresAt, 365, ValidActor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.UserAccount.ValidityPeriodExceedsMaximum, result.Error);
    }

    [Fact]
    public void SetValidityPeriod_RaisesValidityPeriodModifiedEvent()
    {
        var user = UserAccount.Create(ValidTenantId, ValidEmail, ValidCategory, null, null, ValidActor).Value;
        var expiresAt = DateTimeOffset.UtcNow.AddDays(30);

        user.SetValidityPeriod(expiresAt, 365, ValidActor);

        var events = user.DomainEvents.GetUncommittedChanges().ToList();
        Assert.Contains(events, e => e is ValidityPeriodModifiedEvent);
    }

    [Fact]
    public void SetValidityPeriod_UpdatesPreviousExpiresAtInEvent()
    {
        var user = UserAccount.Create(ValidTenantId, ValidEmail, ValidCategory, null, null, ValidActor).Value;
        var first = DateTimeOffset.UtcNow.AddDays(30);
        var second = DateTimeOffset.UtcNow.AddDays(60);
        user.SetValidityPeriod(first, 365, ValidActor);
        user.DomainEvents.MarkChangesAsCommitted();

        user.SetValidityPeriod(second, 365, ValidActor);

        var events = user.DomainEvents.GetUncommittedChanges().OfType<ValidityPeriodModifiedEvent>().Single();
        Assert.Equal(first, events.PreviousExpiresAt);
        Assert.Equal(second, events.NewExpiresAt);
    }

    #endregion

    #region RecordAuthenticationAttempt

    // ADR-UMS-095: parámetros de política de bloqueo por intentos fallidos usados por las pruebas.
    private const int MaxAttempts = 3;
    private const int LockoutMinutes = 15;

    [Fact]
    public void RecordAuthenticationAttempt_RaisesAuthenticationAttemptedEvent()
    {
        var user = UserAccount.Create(ValidTenantId, ValidEmail, ValidCategory, null, null, ValidActor).Value;

        user.RecordAuthenticationAttempt(true, DateTimeOffset.UtcNow, MaxAttempts, LockoutMinutes, "Valid credentials", "192.168.1.1", ValidActor);

        var events = user.DomainEvents.GetUncommittedChanges().ToList();
        Assert.Contains(events, e => e is AuthenticationAttemptedEvent);
    }

    [Fact]
    public void RecordAuthenticationAttempt_WithFailedAttempt_RaisesEventWithSuccessFalse()
    {
        var user = UserAccount.Create(ValidTenantId, ValidEmail, ValidCategory, null, null, ValidActor).Value;

        user.RecordAuthenticationAttempt(false, DateTimeOffset.UtcNow, MaxAttempts, LockoutMinutes, "Invalid password", "192.168.1.1", ValidActor);

        var events = user.DomainEvents.GetUncommittedChanges().ToList();
        var authEvent = events.OfType<AuthenticationAttemptedEvent>().First();
        Assert.False(authEvent.Success);
    }

    // ── ADR-UMS-095: bloqueo temporal de cuenta por intentos fallidos ──────────────

    [Fact]
    public void RecordAuthenticationAttempt_AfterMaxFailedAttempts_LocksAccount()
    {
        var user = UserAccount.Create(ValidTenantId, ValidEmail, ValidCategory, null, null, ValidActor).Value;
        var now = DateTimeOffset.UtcNow;

        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            user.RecordAuthenticationAttempt(false, now, MaxAttempts, LockoutMinutes, "Invalid password", "192.168.1.1", ValidActor);
        }

        Assert.True(user.IsLockedOut(now));
        Assert.Equal(MaxAttempts, user.FailedLoginAttempts);
        Assert.Equal(now.AddMinutes(LockoutMinutes), user.LockedUntil);
    }

    [Fact]
    public void RecordAuthenticationAttempt_BelowThreshold_DoesNotLock()
    {
        var user = UserAccount.Create(ValidTenantId, ValidEmail, ValidCategory, null, null, ValidActor).Value;
        var now = DateTimeOffset.UtcNow;

        user.RecordAuthenticationAttempt(false, now, MaxAttempts, LockoutMinutes, "Invalid password", "192.168.1.1", ValidActor);

        Assert.False(user.IsLockedOut(now));
        Assert.Equal(1, user.FailedLoginAttempts);
        Assert.Null(user.LockedUntil);
    }

    [Fact]
    public void RecordAuthenticationAttempt_Success_ResetsCounterAndLock()
    {
        var user = UserAccount.Create(ValidTenantId, ValidEmail, ValidCategory, null, null, ValidActor).Value;
        var now = DateTimeOffset.UtcNow;
        user.RecordAuthenticationAttempt(false, now, MaxAttempts, LockoutMinutes, "Invalid password", "192.168.1.1", ValidActor);
        user.RecordAuthenticationAttempt(false, now, MaxAttempts, LockoutMinutes, "Invalid password", "192.168.1.1", ValidActor);

        user.RecordAuthenticationAttempt(true, now, MaxAttempts, LockoutMinutes, "Login successful", "192.168.1.1", ValidActor);

        Assert.Equal(0, user.FailedLoginAttempts);
        Assert.Null(user.LockedUntil);
        Assert.False(user.IsLockedOut(now));
    }

    [Fact]
    public void RecordAuthenticationAttempt_WhileLocked_DoesNotIncrement()
    {
        var user = UserAccount.Create(ValidTenantId, ValidEmail, ValidCategory, null, null, ValidActor).Value;
        var now = DateTimeOffset.UtcNow;
        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            user.RecordAuthenticationAttempt(false, now, MaxAttempts, LockoutMinutes, "Invalid password", "192.168.1.1", ValidActor);
        }
        var lockedUntilAfterLock = user.LockedUntil;

        // Un intento fallido adicional dentro de la ventana no incrementa ni extiende el bloqueo.
        user.RecordAuthenticationAttempt(false, now.AddMinutes(1), MaxAttempts, LockoutMinutes, "Invalid password", "192.168.1.1", ValidActor);

        Assert.Equal(MaxAttempts, user.FailedLoginAttempts);
        Assert.Equal(lockedUntilAfterLock, user.LockedUntil);
    }

    [Fact]
    public void IsLockedOut_AfterLockExpires_ReturnsFalse()
    {
        var user = UserAccount.Create(ValidTenantId, ValidEmail, ValidCategory, null, null, ValidActor).Value;
        var now = DateTimeOffset.UtcNow;
        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            user.RecordAuthenticationAttempt(false, now, MaxAttempts, LockoutMinutes, "Invalid password", "192.168.1.1", ValidActor);
        }

        Assert.True(user.IsLockedOut(now));
        Assert.False(user.IsLockedOut(now.AddMinutes(LockoutMinutes).AddSeconds(1)));
    }

    [Fact]
    public void RecordAuthenticationAttempt_FailAfterLockExpired_StartsNewCount()
    {
        var user = UserAccount.Create(ValidTenantId, ValidEmail, ValidCategory, null, null, ValidActor).Value;
        var now = DateTimeOffset.UtcNow;
        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            user.RecordAuthenticationAttempt(false, now, MaxAttempts, LockoutMinutes, "Invalid password", "192.168.1.1", ValidActor);
        }
        var afterExpiry = now.AddMinutes(LockoutMinutes).AddSeconds(1);

        // Tras expirar el bloqueo, un nuevo fallo vuelve a contar (el contador sigue en curso hasta un login exitoso).
        user.RecordAuthenticationAttempt(false, afterExpiry, MaxAttempts, LockoutMinutes, "Invalid password", "192.168.1.1", ValidActor);

        Assert.Equal(MaxAttempts + 1, user.FailedLoginAttempts);
    }

    #endregion

    #region RevokeEnrollment

    [Fact]
    public void RevokeEnrollment_WithValidId_ReturnsSuccessAndRemovesEnrollment()
    {
        var user = UserAccount.Create(ValidTenantId, ValidEmail, ValidCategory, null, null, ValidActor).Value;
        user.EnrollMfa(MfaMethod.Totp, ValidActor);
        var enrollmentId = user.MfaEnrollments.First().GetId();

        var result = user.RevokeEnrollment(enrollmentId, ValidActor);

        Assert.True(result.IsSuccess);
        Assert.Empty(user.MfaEnrollments);
    }

    [Fact]
    public void RevokeEnrollment_WhenNotFound_ReturnsFailure()
    {
        var user = UserAccount.Create(ValidTenantId, ValidEmail, ValidCategory, null, null, ValidActor).Value;
        var fakeId = IdValueObject.Create();

        var result = user.RevokeEnrollment(fakeId, ValidActor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.UserAccount.MfaEnrollmentNotFound, result.Error);
    }

    [Fact]
    public void RevokeEnrollment_RaisesMfaEnrollmentRevokedEvent()
    {
        var user = UserAccount.Create(ValidTenantId, ValidEmail, ValidCategory, null, null, ValidActor).Value;
        user.EnrollMfa(MfaMethod.Totp, ValidActor);
        var enrollmentId = user.MfaEnrollments.First().GetId();

        user.RevokeEnrollment(enrollmentId, ValidActor);

        var events = user.DomainEvents.GetUncommittedChanges().ToList();
        Assert.Contains(events, e => e is MfaEnrollmentRevokedEvent);
    }

    [Fact]
    public void HasVerifiedMfaEnrollment_WhenNoneVerified_ReturnsFalse()
    {
        var user = UserAccount.Create(ValidTenantId, ValidEmail, ValidCategory, null, null, ValidActor).Value;
        user.EnrollMfa(MfaMethod.Totp, ValidActor);

        Assert.False(user.HasVerifiedMfaEnrollment([MfaMethod.Totp]));
    }

    [Fact]
    public void HasVerifiedMfaEnrollment_WhenVerified_ReturnsTrue()
    {
        var user = UserAccount.Create(ValidTenantId, ValidEmail, ValidCategory, null, null, ValidActor).Value;
        user.EnrollMfa(MfaMethod.Totp, ValidActor);
        var enrollmentId = user.MfaEnrollments.First().GetId();
        user.VerifyMfaChallenge(enrollmentId, ValidActor);

        Assert.True(user.HasVerifiedMfaEnrollment([MfaMethod.Totp]));
    }

    [Fact]
    public void HasVerifiedMfaEnrollment_WhenNoEnrollments_ReturnsFalse()
    {
        var user = UserAccount.Create(ValidTenantId, ValidEmail, ValidCategory, null, null, ValidActor).Value;

        Assert.False(user.HasVerifiedMfaEnrollment([MfaMethod.Totp]));
    }

    [Fact]
    public void HasVerifiedMfaEnrollment_WhenMethodNotAllowed_ReturnsFalse()
    {
        var user = UserAccount.Create(ValidTenantId, ValidEmail, ValidCategory, null, null, ValidActor).Value;
        user.EnrollMfa(MfaMethod.Totp, ValidActor);
        var enrollmentId = user.MfaEnrollments.First().GetId();
        user.VerifyMfaChallenge(enrollmentId, ValidActor);

        Assert.False(user.HasVerifiedMfaEnrollment([MfaMethod.EmailOtp]));
    }

    #endregion

    #region Delete (REC-16)

    [Fact]
    public void Delete_WhenActive_ReturnsSuccess()
    {
        var user = UserAccount.Create(ValidTenantId, ValidEmail, ValidCategory, null, null, ValidActor).Value;
        user.Activate(ValidActor);

        var result = user.Delete(ValidActor);

        Assert.True(result.IsSuccess);
        Assert.Equal(UserStatus.Deleted, user.Status);
    }

    [Fact]
    public void Delete_WhenPending_ReturnsSuccess()
    {
        // Pending accounts can also be deleted (e.g. invited but never activated)
        var user = UserAccount.Create(ValidTenantId, ValidEmail, ValidCategory, null, null, ValidActor).Value;

        var result = user.Delete(ValidActor);

        Assert.True(result.IsSuccess);
        Assert.Equal(UserStatus.Deleted, user.Status);
    }

    [Fact]
    public void Delete_WhenAlreadyDeleted_ReturnsFailure()
    {
        var user = UserAccount.Create(ValidTenantId, ValidEmail, ValidCategory, null, null, ValidActor).Value;
        user.Delete(ValidActor); // first delete

        var result = user.Delete(ValidActor); // second delete

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.UserAccount.AlreadyDeleted, result.Error);
    }

    [Fact]
    public void Delete_WhenHasActiveProfiles_ReturnsFailure()
    {
        var user = UserAccount.Create(ValidTenantId, ValidEmail, ValidCategory, null, null, ValidActor).Value;
        user.Activate(ValidActor);

        var result = user.Delete(ValidActor, activeProfileCount: 1);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.UserAccount.HasActiveProfiles, result.Error);
    }

    [Fact]
    public void Delete_RaisesUserDeletedEvent()
    {
        var user = UserAccount.Create(ValidTenantId, ValidEmail, ValidCategory, null, null, ValidActor).Value;
        user.Activate(ValidActor);
        user.DomainEvents.MarkChangesAsCommitted();

        user.Delete(ValidActor);

        var events = user.DomainEvents.GetUncommittedChanges().ToList();
        Assert.Contains(events, e => e is UserDeletedEvent);
    }

    [Fact]
    public void Delete_UserDeletedEvent_CarriesCorrectTenantId()
    {
        var user = UserAccount.Create(ValidTenantId, ValidEmail, ValidCategory, null, null, ValidActor).Value;
        user.Activate(ValidActor);
        user.DomainEvents.MarkChangesAsCommitted();

        user.Delete(ValidActor);

        var deletedEvent = user.DomainEvents.GetUncommittedChanges()
            .OfType<UserDeletedEvent>()
            .Single();

        Assert.Equal(ValidTenantId.GetValue(), deletedEvent.TenantId);
    }

    #endregion

    #region Deny (EP-09)

    [Fact]
    public void Deny_WhenPending_TransitionsToDenied()
    {
        var user = UserAccount.Create(ValidTenantId, ValidEmail, ValidCategory, null, null, ValidActor).Value;

        var result = user.Deny(ValidActor);

        Assert.True(result.IsSuccess);
        Assert.Equal(UserStatus.Denied, user.Status);
    }

    [Fact]
    public void Deny_WhenNotPending_ReturnsFailure()
    {
        var user = UserAccount.Create(ValidTenantId, ValidEmail, ValidCategory, null, null, ValidActor).Value;
        user.Activate(ValidActor);

        var result = user.Deny(ValidActor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.UserAccount.CannotDeny, result.Error);
    }

    [Fact]
    public void Deny_RaisesUserSignupDeniedEvent()
    {
        var user = UserAccount.Create(ValidTenantId, ValidEmail, ValidCategory, null, null, ValidActor).Value;
        user.DomainEvents.MarkChangesAsCommitted();

        user.Deny(ValidActor, "Duplicate request");

        var evt = user.DomainEvents.GetUncommittedChanges()
            .OfType<UserSignupDeniedEvent>()
            .Single();

        Assert.Equal(ValidTenantId.GetValue(), evt.TenantId);
        Assert.Equal("Duplicate request", evt.Reason);
    }

    [Fact]
    public void Deny_WithoutReason_StillSucceeds()
    {
        var user = UserAccount.Create(ValidTenantId, ValidEmail, ValidCategory, null, null, ValidActor).Value;

        var result = user.Deny(ValidActor);

        Assert.True(result.IsSuccess);
        Assert.Equal(UserStatus.Denied, user.Status);
    }

    [Fact]
    public void Deny_DeniedIsTerminalState_CannotBeActivated()
    {
        var user = UserAccount.Create(ValidTenantId, ValidEmail, ValidCategory, null, null, ValidActor).Value;
        user.Deny(ValidActor);

        var activate = user.Activate(ValidActor);

        Assert.True(activate.IsFailure);
    }

    #endregion

    #region SetDisplayName

    [Fact]
    public void SetDisplayName_ConNombre_LoFija()
    {
        var user = UserAccount.Create(ValidTenantId, ValidEmail, ValidCategory, null, null, ValidActor).Value;

        var result = user.SetDisplayName(Name.Create("Carlos Villanueva"), ValidActor);

        Assert.True(result.IsSuccess);
        Assert.Equal("Carlos Villanueva", user.DisplayName!.GetValue());
    }

    [Fact]
    public void SetDisplayName_CorrigeUnNombreAnterior()
    {
        var user = UserAccount.Create(
            ValidTenantId, ValidEmail, ValidCategory, null, null, ValidActor,
            displayName: Name.Create("Nombre Viejo")).Value;

        user.SetDisplayName(Name.Create("Nombre Nuevo"), ValidActor);

        Assert.Equal("Nombre Nuevo", user.DisplayName!.GetValue());
    }

    [Fact]
    public void SetDisplayName_ConNull_DejaLaCuentaSinNombre()
    {
        // «Sin nombre» es un estado válido y distinto de la cadena vacía: debe poder volverse a él.
        var user = UserAccount.Create(
            ValidTenantId, ValidEmail, ValidCategory, null, null, ValidActor,
            displayName: Name.Create("Alguien")).Value;

        var result = user.SetDisplayName(null, ValidActor);

        Assert.True(result.IsSuccess);
        Assert.Null(user.DisplayName);
    }

    [Fact]
    public void SetDisplayName_SobreCuentaEliminada_Falla()
    {
        // El borrado lógico anonimiza el correo (REC-16); escribir un nombre reintroduciría el dato
        // personal que ese borrado se llevó a propósito.
        var user = UserAccount.Create(ValidTenantId, ValidEmail, ValidCategory, null, null, ValidActor).Value;
        user.Delete(ValidActor);

        var result = user.SetDisplayName(Name.Create("Carlos Villanueva"), ValidActor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.UserAccount.CannotUpdateDeleted, result.Error);
    }

    [Fact]
    public void SetDisplayName_SobreCuentaBloqueada_Funciona()
    {
        // Bloquear suspende el ACCESO, no la administración del dato: corregir el nombre de una
        // cuenta bloqueada es legítimo.
        var user = UserAccount.Create(ValidTenantId, ValidEmail, ValidCategory, null, null, ValidActor).Value;
        user.Activate(ValidActor);
        user.Block(Reason.Create("motivo"), ValidActor);

        var result = user.SetDisplayName(Name.Create("Carlos Villanueva"), ValidActor);

        Assert.True(result.IsSuccess);
        Assert.Equal("Carlos Villanueva", user.DisplayName!.GetValue());
    }

    #endregion
}

#pragma warning restore S4144
