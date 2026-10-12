using System.Reflection;
using System.Text.Json;
using BeyondNetCode.Shell.Ddd;
using BeyondNetCode.Shell.Ddd.ValueObjects.Audit;
using Evolith.Ums.Domain.Configuration.AppConfiguration;
using Evolith.Ums.Domain.Configuration.FeatureFlag;
using Evolith.Ums.Domain.Configuration.FeatureFlag.FlagEvaluationLog;
using Evolith.Ums.Domain.Configuration.IdpConfiguration;
using Evolith.Ums.Domain.Configuration.Parameter;
using Evolith.Ums.Domain.Configuration.Parameter.ValueObjects;
using Evolith.Ums.Domain.Enums;
using Evolith.Ums.Domain.Kernel.ValueObjects;
using Evolith.Ums.Infrastructure.Persistence.Configuration.Entities;

namespace Evolith.Ums.Infrastructure.Persistence.Reflection;

using AppConfigurationAggregate = Evolith.Ums.Domain.Configuration.AppConfiguration.AppConfiguration;
using FeatureFlagAggregate = Evolith.Ums.Domain.Configuration.FeatureFlag.FeatureFlag;
using FeatureFlagCriteriaEntity = Evolith.Ums.Domain.Configuration.FeatureFlag.Criteria.FeatureFlagCriteria;
using FeatureFlagEvaluationLogEntity = Evolith.Ums.Domain.Configuration.FeatureFlag.FlagEvaluationLog.FlagEvaluationLog;
using IdpConfigurationAggregate = Evolith.Ums.Domain.Configuration.IdpConfiguration.IdpConfiguration;

internal static class ConfigurationAggregateFactory
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Security Hotspot", "S3011:Reflection should not be used to increase accessibility of classes, methods, or fields",
        Justification = "ADR-UMS-099: reflexión sancionada para rehidratación de agregados. Reconstruir un agregado " +
                        "ya validado (ADR-UMS-069) desde persistencia sin exponer setters públicos preserva la pureza " +
                        "del dominio; las invariantes se cumplieron al crearse.")]
    private static readonly BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

    private static readonly Type AppConfigurationIdType = Type.GetType("Evolith.Ums.Domain.Kernel.ValueObjects.AppConfigurationId, Evolith.Ums.Domain")!;
    private static readonly Type FeatureFlagIdType = Type.GetType("Evolith.Ums.Domain.Kernel.ValueObjects.FeatureFlagId, Evolith.Ums.Domain")!;
    private static readonly Type IdpConfigurationIdType = Type.GetType("Evolith.Ums.Domain.Kernel.ValueObjects.IdpConfigurationId, Evolith.Ums.Domain")!;

    public static AppConfigurationAggregate RehydrateAppConfiguration(AppConfigurationRecord record)
    {
        var props = new AppConfigurationProps(
            LoadTypedId(AppConfigurationIdType, record.Id),
            record.TenantId.HasValue ? TenantId.Load(record.TenantId.Value) : null,
            record.SystemSuiteId.HasValue ? SystemSuiteId.Load(record.SystemSuiteId.Value) : null,
            record.ModuleId.HasValue ? IdValueObject.Load(record.ModuleId.Value) : null,
            Code.Create(record.Code),
            ConfigurationValue.Create(record.Value),
            Description.Create(record.Description),
            record.IsInheritable,
            record.IsEncrypted,
            ActorId.Create(record.CreatedBy),
            record.IsNonOverridable);

        props.Scope = DomainEnumerationMapper.FromValue<ConfigurationScope>(record.ScopeId);
        props.Version = record.Version;
        props.Status = DomainEnumerationMapper.FromValue<ConfigStatus>(record.StatusId);
        SetAudit(props, record.CreatedBy, record.CreatedAtUtc, record.UpdatedBy, record.UpdatedAtUtc, record.AuditTimeSpan);

        var aggregate = Construct<AppConfigurationAggregate, AppConfigurationProps>(props);
        aggregate.DomainEvents.MarkChangesAsCommitted();
        aggregate.BrokenRules.Clear();
        return aggregate;
    }

    public static IdpConfigurationAggregate RehydrateIdpConfiguration(IdpConfigurationRecord record)
    {
        var props = new IdpConfigurationProps(
            LoadTypedId(IdpConfigurationIdType, record.Id),
            TenantId.Load(record.TenantId),
            SystemSuiteId.Load(record.SystemSuiteId),
            DomainEnumerationMapper.FromValue<ProviderType>(record.ProviderTypeId),
            JsonSerializer.Deserialize<string[]>(record.DomainHintsJson) ?? [],
            record.ConfigPayload,
            record.SecretRef,
            record.ResolutionPriority,
            record.FallbackToId,
            ActorId.Create(record.CreatedBy));

        props.Status = DomainEnumerationMapper.FromValue<IdpConfigStatus>(record.StatusId);
        props.Version = record.Version;
        SetAudit(props, record.CreatedBy, record.CreatedAtUtc, record.UpdatedBy, record.UpdatedAtUtc, record.AuditTimeSpan);

        var aggregate = Construct<IdpConfigurationAggregate, IdpConfigurationProps>(props);
        aggregate.DomainEvents.MarkChangesAsCommitted();
        aggregate.BrokenRules.Clear();
        return aggregate;
    }

    public static FeatureFlagAggregate RehydrateFeatureFlag(
        FeatureFlagRecord record,
        IReadOnlyCollection<FeatureFlagEvaluationLogRecord> evaluationLogRecords,
        IReadOnlyCollection<FeatureFlagCriteriaRecord>? criteriaRecords = null)
    {
        var audit = AuditValueObject.Load(new AuditProps
        {
            CreatedBy = record.CreatedBy,
            CreatedAt = record.CreatedAtUtc,
            UpdatedBy = record.UpdatedBy,
            UpdatedAt = record.UpdatedAtUtc,
            TimeSpan = record.AuditTimeSpan
        });

        var props = new FeatureFlagProps(
            LoadTypedId(FeatureFlagIdType, record.Id),
            IdValueObject.Load(record.SystemSuiteId),
            record.TenantId.HasValue ? IdValueObject.Load(record.TenantId.Value) : null,
            record.FlagCode,
            DomainEnumerationMapper.FromValue<FlagType>(record.FlagTypeId),
            record.FlagTargets,
            DomainEnumerationMapper.FromValue<FlagStatus>(record.StatusId),
            record.LinkedResourceTypeId.HasValue ? DomainEnumerationMapper.FromValue<LinkedResourceType>(record.LinkedResourceTypeId.Value) : null,
            record.LinkedResourceId.HasValue ? IdValueObject.Load(record.LinkedResourceId.Value) : null,
            record.RolloutPercentage,
            audit);

        var aggregate = Construct<FeatureFlagAggregate, FeatureFlagProps>(props);
        var logs = evaluationLogRecords.Select(RehydrateEvaluationLog).ToList();
        SetField(aggregate, "_evaluationLog", logs);

        var criteria = (criteriaRecords ?? record.Criteria).Select(c =>
            FeatureFlagCriteriaEntity.Load(c.Id, c.CriteriaType, c.Operator, c.Value, c.CreatedAtUtc)).ToList();
        SetField(aggregate, "_criteria", criteria);

        aggregate.DomainEvents.MarkChangesAsCommitted();
        aggregate.BrokenRules.Clear();
        return aggregate;
    }

    private static FeatureFlagEvaluationLogEntity RehydrateEvaluationLog(FeatureFlagEvaluationLogRecord record)
    {
        var props = new FlagEvaluationLogProps(
            IdValueObject.Load(record.Id),
            record.EvaluatedBy,
            record.Result,
            record.Context)
        {
            EvaluatedAt = record.EvaluatedAtUtc,
        };

        return Construct<FeatureFlagEvaluationLogEntity, FlagEvaluationLogProps>(props);
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

    private static IdValueObject LoadTypedId(Type idType, Guid value)
    {
        var method = idType
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .SingleOrDefault(candidate =>
                candidate.Name == "Load"
                && candidate.GetParameters() is [{ ParameterType: var parameterType }] && parameterType == typeof(Guid))
            ?? throw new InvalidOperationException($"Static Load(Guid) method was not found on {idType.Name}.");

        return (IdValueObject)method.Invoke(null, [value])!;
    }

    // ── Parameter domain types ──────────────────────────────────────────────

    public static ParameterDefinition RehydrateParameterDefinition(ParameterDefinitionRecord r)
    {
        var audit = AuditValueObject.Load(new AuditProps
        {
            CreatedBy = r.CreatedBy, CreatedAt = r.CreatedAtUtc,
            UpdatedBy = r.UpdatedBy, UpdatedAt = r.UpdatedAtUtc, TimeSpan = r.AuditTimeSpan,
        });

        // Use the private rehydration constructor via reflection.
        // (13 parámetros desde que la definición lleva su marca de borrado lógico `IsDeleted`.)
        var propsCtor = typeof(ParameterDefinitionProps)
            .GetConstructors(InstanceFlags)
            .Single(c =>
            {
                var ps = c.GetParameters();
                return ps.Length == 13 && ps[11].ParameterType == typeof(string);  // version param
            });

        var props = (ParameterDefinitionProps)propsCtor.Invoke([
            IdValueObject.Load(r.Id),
            Code.Create(r.Code),
            ParameterName.Create(r.Name),
            Description.Create(r.Description ?? string.Empty),
            ParameterDataType.FromValue(r.DataTypeId),
            DefaultValue.Create(r.DefaultValue),
            ParameterScope.FromValue(r.ScopeId),
            r.IsActive, r.IsMandatory, r.DisplayOrder,
            r.IsDeleted,
            r.Version,
            audit,
        ]);

        var aggregate = Construct<ParameterDefinition, ParameterDefinitionProps>(props);
        aggregate.DomainEvents.MarkChangesAsCommitted();
        aggregate.BrokenRules.Clear();
        return aggregate;
    }

    public static ParameterGlobalValue RehydrateParameterGlobalValue(ParameterGlobalValueRecord r)
    {
        var audit = AuditValueObject.Load(new AuditProps
        {
            CreatedBy = r.CreatedBy, CreatedAt = r.CreatedAtUtc,
            UpdatedBy = r.UpdatedBy, UpdatedAt = r.UpdatedAtUtc, TimeSpan = r.AuditTimeSpan,
        });

        var propsCtor = typeof(ParameterGlobalValueProps)
            .GetConstructors(InstanceFlags)
            .Single(c =>
            {
                var ps = c.GetParameters();
                return ps.Length == 6 && ps[3].ParameterType == typeof(ConfigStatus);
            });

        var props = (ParameterGlobalValueProps)propsCtor.Invoke([
            IdValueObject.Load(r.Id),
            IdValueObject.Load(r.ParameterDefinitionId),
            EffectiveValue.Create(r.EffectiveValue),
            DomainEnumerationMapper.FromValue<ConfigStatus>(r.StatusId),
            r.Version,
            audit,
        ]);

        var aggregate = Construct<ParameterGlobalValue, ParameterGlobalValueProps>(props);
        aggregate.DomainEvents.MarkChangesAsCommitted();
        aggregate.BrokenRules.Clear();
        return aggregate;
    }

    public static ParameterTenantValue RehydrateParameterTenantValue(ParameterTenantValueRecord r)
    {
        var audit = AuditValueObject.Load(new AuditProps
        {
            CreatedBy = r.CreatedBy, CreatedAt = r.CreatedAtUtc,
            UpdatedBy = r.UpdatedBy, UpdatedAt = r.UpdatedAtUtc, TimeSpan = r.AuditTimeSpan,
        });

        var propsCtor = typeof(ParameterTenantValueProps)
            .GetConstructors(InstanceFlags)
            .Single(c =>
            {
                var ps = c.GetParameters();
                return ps.Length == 7 && ps[4].ParameterType == typeof(ConfigStatus);
            });

        var props = (ParameterTenantValueProps)propsCtor.Invoke([
            IdValueObject.Load(r.Id),
            TenantId.Load(r.TenantId),
            IdValueObject.Load(r.ParameterDefinitionId),
            OverrideValue.Create(r.OverrideValue),
            DomainEnumerationMapper.FromValue<ConfigStatus>(r.StatusId),
            r.Version,
            audit,
        ]);

        var aggregate = Construct<ParameterTenantValue, ParameterTenantValueProps>(props);
        aggregate.DomainEvents.MarkChangesAsCommitted();
        aggregate.BrokenRules.Clear();
        return aggregate;
    }
}
