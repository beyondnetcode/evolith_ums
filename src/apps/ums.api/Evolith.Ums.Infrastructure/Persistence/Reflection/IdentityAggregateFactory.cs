using System.Reflection;
using System.Text.Json;
using BeyondNetCode.Shell.Ddd.ValueObjects.Audit;
using Evolith.Ums.Domain.Enums;
using Evolith.Ums.Domain.Identity.Tenant;
using Evolith.Ums.Domain.Identity.Tenant.Branch;
using Evolith.Ums.Domain.Identity.Tenant.IdentityProvider;
using Evolith.Ums.Domain.Identity.Tenant.TenantParameter;
using Evolith.Ums.Domain.Identity.UserAccount;
using Evolith.Ums.Domain.Identity.UserAccount.MfaEnrollment;
using Evolith.Ums.Domain.Identity.UserAccount.PasswordCredential;
using Evolith.Ums.Domain.Identity.UserManagementDelegation;
using Evolith.Ums.Domain.Kernel.ValueObjects;
using Evolith.Ums.Infrastructure.Persistence.Identity.Entities;

namespace Evolith.Ums.Infrastructure.Persistence.Reflection;

using TenantAggregate = Evolith.Ums.Domain.Identity.Tenant.Tenant;
using TenantParameterAggregate = Evolith.Ums.Domain.Identity.Tenant.TenantParameter.TenantParameter;
using UserAccountAggregate = Evolith.Ums.Domain.Identity.UserAccount.UserAccount;
using UserManagementDelegationAggregate = Evolith.Ums.Domain.Identity.UserManagementDelegation.UserManagementDelegation;

internal static class IdentityAggregateFactory
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Security Hotspot", "S3011:Reflection should not be used to increase accessibility of classes, methods, or fields",
        Justification = "ADR-UMS-099: reflexión sancionada para rehidratación de agregados. Reconstruir un agregado " +
                        "ya validado (ADR-UMS-069) desde persistencia sin exponer setters públicos preserva la pureza " +
                        "del dominio; las invariantes se cumplieron al crearse.")]
    private static readonly BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

    public static TenantAggregate RehydrateTenant(
        TenantRecord tenantRecord,
        IReadOnlyCollection<TenantBranchRecord> branchRecords,
        IReadOnlyCollection<TenantIdentityProviderRecord> providerRecords)
    {
        var audit = AuditValueObject.Load(new AuditProps
        {
            CreatedBy = tenantRecord.CreatedBy,
            CreatedAt = tenantRecord.CreatedAtUtc,
            UpdatedBy = tenantRecord.UpdatedBy,
            UpdatedAt = tenantRecord.UpdatedAtUtc,
            TimeSpan = tenantRecord.AuditTimeSpan
        });

        var props = new TenantProps(
            TenantId.Load(tenantRecord.Id),
            Code.Create(tenantRecord.Code),
            Name.Create(tenantRecord.Name),
            DomainEnumerationMapper.FromValue<OrganizationType>(tenantRecord.OrganizationTypeId),
            DomainEnumerationMapper.FromValue<IdpStrategy>(tenantRecord.IdpStrategyId),
            string.IsNullOrWhiteSpace(tenantRecord.CompanyReference) ? null : CompanyReference.Create(tenantRecord.CompanyReference),
            tenantRecord.ParentTenantId.HasValue ? TenantId.Load(tenantRecord.ParentTenantId.Value) : null,
            tenantRecord.IsManagementOwner,
            // FR-042 (ADR-UMS-097 §2.2): suite por defecto del inquilino (nullable/retrocompatible).
            tenantRecord.DefaultSystemSuiteId.HasValue ? SystemSuiteId.Load(tenantRecord.DefaultSystemSuiteId.Value) : null,
            DomainEnumerationMapper.FromValue<TenantStatus>(tenantRecord.StatusId),
            audit);

        var tenant = Construct<TenantAggregate, TenantProps>(props);

        var branches = branchRecords.Select(RehydrateBranch).ToList();
        var providers = providerRecords.Select(RehydrateIdentityProvider).ToList();

        SetField(tenant, "_branches", branches);
        SetField(tenant, "_identityProviders", providers);
        tenant.DomainEvents.MarkChangesAsCommitted();
        tenant.BrokenRules.Clear();

        return tenant;
    }

    public static UserAccountAggregate RehydrateUserAccount(
        UserAccountRecord record,
        IReadOnlyCollection<UserAccountMfaEnrollmentRecord> enrollmentRecords,
        IReadOnlyCollection<UserAccountPasswordCredentialRecord> passwordRecords)
    {
        var audit = AuditValueObject.Load(new AuditProps
        {
            CreatedBy = record.CreatedBy,
            CreatedAt = record.CreatedAtUtc,
            UpdatedBy = record.UpdatedBy,
            UpdatedAt = record.UpdatedAtUtc,
            TimeSpan = record.AuditTimeSpan
        });

        var expiresAt = record.ExpiresAtUtc.HasValue
            ? (DateTimeOffset?)new DateTimeOffset(record.ExpiresAtUtc.Value, TimeSpan.Zero)
            : null;

        // ADR-UMS-095: rehidratación del estado de bloqueo temporal.
        var lockedUntil = record.LockedUntilUtc.HasValue
            ? (DateTimeOffset?)new DateTimeOffset(record.LockedUntilUtc.Value, TimeSpan.Zero)
            : null;

        var props = new UserAccountProps(
            UserAccountId.Load(record.Id),
            TenantId.Load(record.TenantId),
            record.BranchId.HasValue ? BranchId.Load(record.BranchId.Value) : null,
            Email.Create(record.Email),
            DomainEnumerationMapper.FromValue<UserCategory>(record.CategoryId),
            DomainEnumerationMapper.FromValue<UserStatus>(record.StatusId),
            string.IsNullOrWhiteSpace(record.IdentityReference) ? null : IdentityReference.Create(record.IdentityReference),
            record.IdentityReferenceTypeId.HasValue ? DomainEnumerationMapper.FromValue<IdentityReferenceType>(record.IdentityReferenceTypeId.Value) : null,
            audit,
            string.IsNullOrWhiteSpace(record.DisplayName) ? null : Name.Create(record.DisplayName),
            expiresAt,
            record.FailedLoginAttempts,
            lockedUntil);

        var account = Construct<UserAccountAggregate, UserAccountProps>(props);

        var enrollments = enrollmentRecords.Select(RehydrateEnrollment).ToList();
        var passwords = passwordRecords.Select(RehydratePasswordCredential).ToList();

        SetField(account, "_mfaEnrollments", enrollments);
        SetField(account, "_passwordCredentials", passwords);
        account.DomainEvents.MarkChangesAsCommitted();
        account.BrokenRules.Clear();

        return account;
    }

    public static UserManagementDelegationAggregate RehydrateUserManagementDelegation(UserManagementDelegationRecord record)
    {
        var actionIds = JsonSerializer.Deserialize<List<int>>(record.AllowedActionsJson) ?? [];
        var allowedActions = actionIds
            .Select(id => DomainEnumerationMapper.FromValue<DelegatedAction>(id))
            .ToList();

        var audit = AuditValueObject.Load(new AuditProps
        {
            CreatedBy = record.CreatedBy,
            CreatedAt = record.CreatedAtUtc,
            UpdatedBy = record.UpdatedBy,
            UpdatedAt = record.UpdatedAtUtc,
            TimeSpan = record.AuditTimeSpan
        });

        var props = new UserManagementDelegationProps(
            DelegationId.Load(record.Id),
            TenantId.Load(record.TenantId),
            UserAccountId.Load(record.DelegatingAdminId),
            UserAccountId.Load(record.DelegatedAdminId),
            DomainEnumerationMapper.FromValue<DelegationScopeType>(record.ScopeTypeId),
            record.ScopeId,
            allowedActions,
            record.ValidFrom,
            record.ValidUntil,
            record.MaxDurationDays,
            record.RequiresApproval,
            DomainEnumerationMapper.FromValue<DelegationStatus>(record.StatusId),
            record.ApprovalRequestId,
            record.RevokedAt,
            record.RevokedBy,
            record.RevocationReason,
            audit);

        var delegation = Construct<UserManagementDelegationAggregate, UserManagementDelegationProps>(props);
        delegation.DomainEvents.MarkChangesAsCommitted();
        delegation.BrokenRules.Clear();

        return delegation;
    }

    public static TenantParameterAggregate RehydrateTenantParameter(TenantParameterRecord record)
    {
        var props = new TenantParameterProps(
            TenantParameterId.Load(record.Id),
            TenantId.Load(record.TenantId),
            Code.Create(record.Code),
            Description.Create(record.Description),
            record.Value,
            DomainEnumerationMapper.FromValue<TenantParameterValueType>(record.ValueTypeId),
            DomainEnumerationMapper.FromValue<TenantParameterCategory>(record.CategoryId),
            record.IsActive,
            record.IsSensitive,
            record.DefaultValue,
            record.AllowedValues,
            AuditValueObject.Load(new AuditProps
            {
                CreatedBy = record.CreatedBy,
                CreatedAt = record.CreatedAtUtc,
                UpdatedBy = record.UpdatedBy,
                UpdatedAt = record.UpdatedAtUtc,
                TimeSpan = record.AuditTimeSpan
            }),
            record.IsDeleted);

        var parameter = Construct<TenantParameterAggregate, TenantParameterProps>(props);
        parameter.DomainEvents.MarkChangesAsCommitted();
        parameter.BrokenRules.Clear();

        return parameter;
    }

    private static Branch RehydrateBranch(TenantBranchRecord record)
    {
        var props = new BranchProps(
            BranchId.Load(record.Id),
            TenantId.Load(record.TenantId),
            Code.Create(record.Code),
            Name.Create(record.Name),
            string.IsNullOrWhiteSpace(record.GeofencingMetadata) ? null : Value.Create(record.GeofencingMetadata),
            ActorId.Create(record.CreatedBy));

        props.IsActive = record.IsActive;
        // ADR-0164: el cierre definitivo se rehidrata como estado; la BITÁCORA no, a propósito.
        // Ninguna invariante del agregado depende de la historia, y `Branch.Create` —que sí anota el
        // episodio de apertura— no interviene aquí: la rehidratación usa el constructor privado, así
        // que una relectura del inquilino no puede inventar asientos que nunca ocurrieron.
        props.IsClosed = record.IsClosed;
        props.ClosedAtUtc = record.ClosedAtUtc;
        props.ClosedBy = record.ClosedBy;
        SetAudit(props, record.CreatedBy, record.CreatedAtUtc, record.UpdatedBy, record.UpdatedAtUtc, record.AuditTimeSpan);

        return Construct<Branch, BranchProps>(props);
    }

    private static IdentityProvider RehydrateIdentityProvider(TenantIdentityProviderRecord record)
    {
        var props = new IdentityProviderProps(
            IdentityProviderId.Load(record.Id),
            TenantId.Load(record.TenantId),
            Code.Create(record.Code),
            Name.Create(record.Name),
            Description.Create(record.Description),
            DomainEnumerationMapper.FromValue<IdpStrategy>(record.StrategyId),
            ActorId.Create(record.CreatedBy));

        props.IsActive = record.IsActive;
        SetAudit(props, record.CreatedBy, record.CreatedAtUtc, record.UpdatedBy, record.UpdatedAtUtc, record.AuditTimeSpan);

        return Construct<IdentityProvider, IdentityProviderProps>(props);
    }

    private static MfaEnrollment RehydrateEnrollment(UserAccountMfaEnrollmentRecord record)
    {
        var audit = AuditValueObject.Load(new AuditProps
        {
            CreatedBy = record.CreatedBy,
            CreatedAt = record.CreatedAtUtc,
            UpdatedBy = record.UpdatedBy,
            UpdatedAt = record.UpdatedAtUtc,
            TimeSpan = record.AuditTimeSpan
        });

        var props = new MfaEnrollmentProps(
            MfaEnrollmentId.Load(record.Id),
            UserAccountId.Load(record.UserAccountId),
            DomainEnumerationMapper.FromValue<MfaMethod>(record.MethodId),
            DomainEnumerationMapper.FromValue<MfaEnrollmentStatus>(record.StatusId),
            audit);

        return Construct<MfaEnrollment, MfaEnrollmentProps>(props);
    }

    private static PasswordCredential RehydratePasswordCredential(UserAccountPasswordCredentialRecord record)
    {
        var audit = AuditValueObject.Load(new AuditProps
        {
            CreatedBy = record.CreatedBy,
            CreatedAt = record.CreatedAtUtc,
            UpdatedBy = record.UpdatedBy,
            UpdatedAt = record.UpdatedAtUtc,
            TimeSpan = record.AuditTimeSpan
        });

        var props = new PasswordCredentialProps(
            PasswordCredentialId.Load(record.Id),
            UserAccountId.Load(record.UserAccountId),
            PasswordHash.Create(record.PasswordHash),
            record.IsActive,
            audit);

        return Construct<PasswordCredential, PasswordCredentialProps>(props);
    }

    private static TEntity Construct<TEntity, TProps>(TProps props)
        where TEntity : class
        where TProps : class
    {
        var ctor = typeof(TEntity).GetConstructor(InstanceFlags, null, [typeof(TProps)], null)
            ?? throw new InvalidOperationException($"Constructor for {typeof(TEntity).Name} not found.");

        return (TEntity)ctor.Invoke([props]);
    }

    private static void SetField<TTarget>(object target, string fieldName, TTarget value)
    {
        var field = target.GetType().GetField(fieldName, InstanceFlags)
            ?? throw new InvalidOperationException($"Field {fieldName} not found on {target.GetType().Name}.");

        field.SetValue(target, value);
    }

    private static void SetAudit(object props, string createdBy, DateTime createdAtUtc, string? updatedBy, DateTime? updatedAtUtc, string auditTimeSpan)
    {
        var property = props.GetType().GetProperty("Audit", InstanceFlags)
            ?? throw new InvalidOperationException($"Audit property not found on {props.GetType().Name}.");

        property.SetValue(props, AuditValueObject.Load(new AuditProps
        {
            CreatedBy = createdBy,
            CreatedAt = createdAtUtc,
            UpdatedBy = updatedBy,
            UpdatedAt = updatedAtUtc,
            TimeSpan = auditTimeSpan,
        }));
    }
}
