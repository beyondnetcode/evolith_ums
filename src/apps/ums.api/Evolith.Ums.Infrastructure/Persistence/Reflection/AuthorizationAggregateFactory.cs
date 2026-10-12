using System.Collections.Concurrent;
using System.Reflection;
using Evolith.Ums.Domain.Authorization.Profile;
using Evolith.Ums.Domain.Authorization.Profile.ProfilePermission;
using Evolith.Ums.Domain.Authorization.SystemSuite.AppSetting;
using Evolith.Ums.Domain.Authorization.SystemSuite.Module;
using Evolith.Ums.Domain.Authorization.SystemSuite.MenuNode;
using Evolith.Ums.Domain.Authorization.SystemSuite.Action;
using Evolith.Ums.Domain.Authorization.SystemSuite.DomainResource;
using Evolith.Ums.Domain.Authorization.Template;
using Evolith.Ums.Domain.Authorization.Role;
using Evolith.Ums.Domain.Authorization.AssignmentRule;
using Evolith.Ums.Domain.Authorization.Template.PermissionTemplateItem;
using Evolith.Ums.Domain.Enums;
using Evolith.Ums.Domain.Kernel.ValueObjects;
using Evolith.Ums.Infrastructure.Persistence.Authorization.Entities;
using BeyondNetCode.Shell.Ddd;
using BeyondNetCode.Shell.Ddd.ValueObjects.Audit;

namespace Evolith.Ums.Infrastructure.Persistence.Reflection;

using ProfileAggregate = Evolith.Ums.Domain.Authorization.Profile.Profile;
using PermissionTemplateAggregate = Evolith.Ums.Domain.Authorization.Template.PermissionTemplate;
using RoleAggregate = Evolith.Ums.Domain.Authorization.Role.Role;
using SystemSuiteAggregate = Evolith.Ums.Domain.Authorization.SystemSuite.SystemSuite;
using AssignmentRuleAggregate = Evolith.Ums.Domain.Authorization.AssignmentRule.TemplateAssignmentRule;
using ModuleEntity = Evolith.Ums.Domain.Authorization.SystemSuite.Module.Module;
using MenuNodeEntity = Evolith.Ums.Domain.Authorization.SystemSuite.MenuNode.MenuNode;
using ActionEntity = Evolith.Ums.Domain.Authorization.SystemSuite.Action.Action;
using DomainResourceEntity = Evolith.Ums.Domain.Authorization.SystemSuite.DomainResource.DomainResource;
using PermissionTemplateItemEntity = Evolith.Ums.Domain.Authorization.Template.PermissionTemplateItem.PermissionTemplateItem;

internal static class AuthorizationAggregateFactory
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Security Hotspot", "S3011:Reflection should not be used to increase accessibility of classes, methods, or fields",
        Justification = "ADR-UMS-099: reflexión sancionada para rehidratación de agregados. Reconstruir un agregado " +
                        "ya validado (ADR-UMS-069) desde persistencia sin exponer setters públicos preserva la pureza " +
                        "del dominio; las invariantes se cumplieron al crearse.")]
    private static readonly BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

    public static ProfileAggregate RehydrateProfile(
        ProfileRecord profileRecord,
        IReadOnlyCollection<ProfilePermissionRecord> permissionRecords)
    {
        var audit = AuditValueObject.Load(new AuditProps
        {
            CreatedBy = profileRecord.CreatedBy,
            CreatedAt = profileRecord.CreatedAtUtc,
            UpdatedBy = profileRecord.UpdatedBy,
            UpdatedAt = profileRecord.UpdatedAtUtc,
            TimeSpan = profileRecord.AuditTimeSpan
        });

        var props = new ProfileProps(
            ProfileId.Load(profileRecord.Id),
            TenantId.Load(profileRecord.TenantId),
            UserId.Load(profileRecord.UserId),
            RoleId.Load(profileRecord.RoleId),
            profileRecord.BranchId.HasValue ? BranchId.Load(profileRecord.BranchId.Value) : null,
            DomainEnumerationMapper.FromValue<ProfileScope>(profileRecord.ScopeId),
            profileRecord.IsActive,
            audit);

        var profile = Construct<ProfileAggregate, ProfileProps>(props);
        var permissions = permissionRecords.Select(RehydratePermission).ToList();

        SetField(profile, "_permissions", permissions);
        profile.DomainEvents.MarkChangesAsCommitted();
        profile.BrokenRules.Clear();

        return profile;
    }

    private static ProfilePermission RehydratePermission(ProfilePermissionRecord record)
    {
        var props = new ProfilePermissionProps(
            ProfilePermissionId.Load(record.Id),
            ProfileId.Load(record.ProfileId),
            TemplateId.Load(record.TemplateId),
            DomainEnumerationMapper.FromValue<ExclusiveArcTarget>(record.TargetTypeId),
            IdValueObject.Load(record.TargetId),
            ActionId.Load(record.ActionId),
            record.IsAllowed,
            record.IsDenied,
            record.IsActive,
            record.IsOverride,
            ActorId.Create(record.CreatedBy));

        SetAudit(props, record.CreatedBy, record.CreatedAtUtc, record.UpdatedBy, record.UpdatedAtUtc, record.AuditTimeSpan);
        return Construct<ProfilePermission, ProfilePermissionProps>(props);
    }

    public static SystemSuiteAggregate RehydrateSystemSuite(
        SystemSuiteRecord systemSuiteRecord,
        IReadOnlyCollection<SystemSuiteModuleRecord> moduleRecords,
        IReadOnlyCollection<SystemSuiteAppSettingRecord> appSettingRecords,
        IReadOnlyCollection<SystemSuiteActionRecord> actionRecords,
        IReadOnlyCollection<SystemSuiteDomainResourceRecord> domainResourceRecords)
    {
        var props = new Evolith.Ums.Domain.Authorization.SystemSuite.SystemSuiteProps(
            IdValueObject.Load(systemSuiteRecord.Id),
            TenantId.Load(systemSuiteRecord.TenantId),
            Code.Create(systemSuiteRecord.Code),
            Name.Create(systemSuiteRecord.Name),
            Description.Create(systemSuiteRecord.Description),
            DomainEnumerationMapper.FromValue<SystemStatus>(systemSuiteRecord.StatusId),
            ActorId.Create(systemSuiteRecord.CreatedBy));

        SetAudit(props, systemSuiteRecord.CreatedBy, systemSuiteRecord.CreatedAtUtc, systemSuiteRecord.UpdatedBy, systemSuiteRecord.UpdatedAtUtc, systemSuiteRecord.AuditTimeSpan);

        var aggregate = Construct<SystemSuiteAggregate, Evolith.Ums.Domain.Authorization.SystemSuite.SystemSuiteProps>(props);
        var modules = moduleRecords.OrderBy(x => x.SortOrder).Select(RehydrateModule).ToList();
        var appSettings = appSettingRecords.Select(RehydrateAppSetting).ToList();
        var actions = actionRecords.Select(RehydrateAction).ToList();
        var domainResources = domainResourceRecords.Select(RehydrateDomainResource).ToList();

        SetField(aggregate, "_modules", modules);
        SetField(aggregate, "_appSettings", appSettings);
        SetField(aggregate, "_actions", actions);
        SetField(aggregate, "_domainResources", domainResources);
        aggregate.DomainEvents.MarkChangesAsCommitted();
        aggregate.BrokenRules.Clear();

        return aggregate;
    }

    public static PermissionTemplateAggregate RehydratePermissionTemplate(
        PermissionTemplateRecord templateRecord,
        IReadOnlyCollection<PermissionTemplateItemRecord> itemRecords)
    {
        var props = new PermissionTemplateProps(
            IdValueObject.Load(templateRecord.Id),
            TenantId.Load(templateRecord.TenantId),
            RoleId.Load(templateRecord.RoleId),
            SystemSuiteId.Load(templateRecord.SystemSuiteId),
            ConstructStringValueObject<TemplateVersion>(templateRecord.Version),
            DomainEnumerationMapper.FromValue<TemplateStatus>(templateRecord.StatusId),
            ActorId.Create(templateRecord.CreatedBy));

        SetAudit(props, templateRecord.CreatedBy, templateRecord.CreatedAtUtc, templateRecord.UpdatedBy, templateRecord.UpdatedAtUtc, templateRecord.AuditTimeSpan);

        var aggregate = Construct<PermissionTemplateAggregate, PermissionTemplateProps>(props);
        var items = itemRecords.Select(RehydrateTemplateItem).ToList();

        SetField(aggregate, "_items", items);
        aggregate.DomainEvents.MarkChangesAsCommitted();
        aggregate.BrokenRules.Clear();

        return aggregate;
    }

    public static AssignmentRuleAggregate RehydrateAssignmentRule(TemplateAssignmentRuleRecord record)
    {
        var audit = AuditValueObject.Load(new AuditProps
        {
            CreatedBy = record.CreatedBy,
            CreatedAt = record.CreatedAtUtc,
            UpdatedBy = record.UpdatedBy,
            UpdatedAt = record.UpdatedAtUtc,
            TimeSpan = record.AuditTimeSpan
        });

        var props = new TemplateAssignmentRuleProps(
            IdValueObject.Load(record.Id),
            TenantId.Load(record.TenantId),
            TemplateId.Load(record.TemplateId),
            RoleId.Load(record.RoleId),
            record.Priority,
            (TemplateAssignmentRuleStatus)record.StatusId,
            audit);

        var aggregate = Construct<AssignmentRuleAggregate, TemplateAssignmentRuleProps>(props);
        aggregate.DomainEvents.MarkChangesAsCommitted();
        aggregate.BrokenRules.Clear();
        return aggregate;
    }

    public static RoleAggregate RehydrateRole(RoleRecord record)
    {
        var audit = AuditValueObject.Load(new AuditProps
        {
            CreatedBy = record.CreatedBy,
            CreatedAt = record.CreatedAtUtc,
            UpdatedBy = record.UpdatedBy,
            UpdatedAt = record.UpdatedAtUtc,
            TimeSpan = record.AuditTimeSpan
        });

        var props = new RoleProps(
            IdValueObject.Load(record.Id),
            TenantId.Load(record.TenantId),
            SystemSuiteId.Load(record.SystemSuiteId),
            record.ParentRoleId.HasValue ? RoleId.Load(record.ParentRoleId.Value) : null,
            Code.Create(record.Code),
            Name.Create(record.Value),
            Description.Create(record.Description),
            record.HierarchyLevel,
            record.PromotionOrder,
            record.IsActive,
            audit);

        var aggregate = Construct<RoleAggregate, RoleProps>(props);
        aggregate.DomainEvents.MarkChangesAsCommitted();
        aggregate.BrokenRules.Clear();
        return aggregate;
    }

    private static ModuleEntity RehydrateModule(SystemSuiteModuleRecord record)
    {
        var props = new ModuleProps(
            IdValueObject.Load(record.Id),
            SystemId.Load(record.SystemSuiteId),
            Code.Create(record.Code),
            Name.Create(record.Name),
            Description.Create(record.Description),
            DomainEnumerationMapper.FromValue<ModuleStatus>(record.StatusId),
            record.SortOrder,
            ActorId.Create(record.CreatedBy),
            record.Icon);

        SetAudit(props, record.CreatedBy, record.CreatedAtUtc, record.UpdatedBy, record.UpdatedAtUtc, record.AuditTimeSpan);
        var module = Construct<ModuleEntity, ModuleProps>(props);

        // Árbol recursivo (ADR-0090): los nodos llegan planos (todos con el mismo
        // ModuleId); se agrupan por padre y se reconstruye el árbol en memoria.
        var allNodes = record.Nodes.ToList();
        var childrenByParent = allNodes
            .Where(n => n.ParentNodeId.HasValue)
            .GroupBy(n => n.ParentNodeId!.Value)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<SystemSuiteNodeRecord>)g.OrderBy(x => x.SortOrder).ToList());
        var roots = allNodes
            .Where(n => !n.ParentNodeId.HasValue)
            .OrderBy(n => n.SortOrder)
            .Select(r => RehydrateNode(r, childrenByParent))
            .ToList();
        SetField(module, "_nodes", roots);

        module.BrokenRules.Clear();
        return module;
    }

    private static MenuNodeEntity RehydrateNode(
        SystemSuiteNodeRecord record,
        IReadOnlyDictionary<Guid, IReadOnlyList<SystemSuiteNodeRecord>> childrenByParent)
    {
        var metadata = MenuNodeMetadata.Create(
            record.Responsable,
            record.Criticidad,
            record.ProductoImpactado,
            record.ComponenteTecnico,
            record.Dependencias,
            record.Evidencias,
            record.TrazabilidadSdlc);

        var props = new MenuNodeProps(
            IdValueObject.Load(record.Id),
            ModuleId.Load(record.ModuleId),
            record.ParentNodeId.HasValue ? IdValueObject.Load(record.ParentNodeId.Value) : null,
            (NodeKind)record.NodeKindId,
            Code.Create(record.Code),
            Name.Create(record.Label),
            Description.Create(record.Description),
            DomainEnumerationMapper.FromValue<ModuleStatus>(record.StatusId),
            record.SortOrder,
            metadata,
            ActorId.Create(record.CreatedBy),
            MenuNodePresentation.Create(record.Icon, record.Route));

        SetAudit(props, record.CreatedBy, record.CreatedAtUtc, record.UpdatedBy, record.UpdatedAtUtc, record.AuditTimeSpan);
        var node = Construct<MenuNodeEntity, MenuNodeProps>(props);

        var children = childrenByParent.TryGetValue(record.Id, out var childRecords)
            ? childRecords.Select(c => RehydrateNode(c, childrenByParent)).ToList()
            : new List<MenuNodeEntity>();
        SetField(node, "_children", children);

        var actionCodes = record.Actions
            .Select(a => ActionCode.Create(a.ActionCode))
            .ToList();
        SetField(node, "_actionCodes", actionCodes);

        node.BrokenRules.Clear();
        return node;
    }

    private static ActionEntity RehydrateAction(SystemSuiteActionRecord record)
    {
        var props = new ActionProps(
            IdValueObject.Load(record.Id),
            TenantId.Load(record.TenantId),
            SystemSuiteId.Load(record.SystemSuiteId),
            record.ModuleId.HasValue ? ModuleId.Load(record.ModuleId.Value) : null,
            ActionCode.Create(record.Code),
            Name.Create(record.Name),
            ActorId.Create(record.CreatedBy));

        SetAudit(props, record.CreatedBy, record.CreatedAtUtc, record.UpdatedBy, record.UpdatedAtUtc, record.AuditTimeSpan);
        var action = Construct<ActionEntity, ActionProps>(props);
        action.BrokenRules.Clear();
        return action;
    }

    private static DomainResourceEntity RehydrateDomainResource(SystemSuiteDomainResourceRecord record)
    {
        var props = new DomainResourceProps(
            IdValueObject.Load(record.Id),
            SystemSuiteId.Load(record.SystemSuiteId),
            record.ModuleId.HasValue ? ModuleId.Load(record.ModuleId.Value) : null,
            record.ParentResourceId.HasValue ? IdValueObject.Load(record.ParentResourceId.Value) : null,
            DomainEnumerationMapper.FromName<DomainResourceType>(record.Type),
            Code.Create(record.Code),
            Name.Create(record.Name),
            Description.Create(record.Description),
            ActorId.Create(record.CreatedBy));

        SetAudit(props, record.CreatedBy, record.CreatedAtUtc, record.UpdatedBy, record.UpdatedAtUtc, record.AuditTimeSpan);
        var domainResource = Construct<DomainResourceEntity, DomainResourceProps>(props);
        domainResource.BrokenRules.Clear();
        return domainResource;
    }

    private static AppSetting RehydrateAppSetting(SystemSuiteAppSettingRecord record)
    {
        var method = typeof(AppSetting).GetMethod(nameof(AppSetting.Create), BindingFlags.Static | BindingFlags.Public)
            ?? throw new InvalidOperationException("AppSetting.Create factory not found.");

        var result = (Result<AppSetting>)method.Invoke(null,
            [ConfigurationKey.Create(record.ConfigKey),
             ConfigurationValue.Create(record.ConfigValue),
             DomainEnumerationMapper.FromValue<ConfigurationScope>(record.ScopeId),
             record.IsClientVisible])!;

        if (result.IsFailure)
        {
            throw new InvalidOperationException($"Unable to rehydrate app setting {record.ConfigKey}: {result.Error}");
        }

        return result.Value;
    }

    private static PermissionTemplateItemEntity RehydrateTemplateItem(PermissionTemplateItemRecord record)
    {
        var props = new PermissionTemplateItemProps(
            IdValueObject.Load(record.Id),
            TemplateId.Load(record.TemplateId),
            DomainEnumerationMapper.FromValue<ExclusiveArcTarget>(record.TargetTypeId),
            IdValueObject.Load(record.TargetId),
            ActionId.Load(record.ActionId),
            record.IsAllowed,
            record.IsDenied,
            record.IsActive,
            ActorId.Create(record.CreatedBy));

        SetAudit(props, record.CreatedBy, record.CreatedAtUtc, record.UpdatedBy, record.UpdatedAtUtc, record.AuditTimeSpan);
        var item = Construct<PermissionTemplateItemEntity, PermissionTemplateItemProps>(props);
        item.BrokenRules.Clear();
        return item;
    }

    // ── Memoización de metadatos de reflexión (R-12) ─────────────────────────
    //
    // Rehidratar el grafo de una suite instancia del orden de 500 entidades por login, y cada
    // una resolvía de nuevo su constructor, su campo o su propiedad por reflexión. Buscar
    // metadatos es la parte cara de la reflexión; invocar, una vez resueltos, es barato. Los
    // metadatos de un tipo no cambian en tiempo de ejecución, así que se cachean para siempre.
    // `ConcurrentDictionary` porque la rehidratación ocurre en peticiones concurrentes.

    private static readonly ConcurrentDictionary<(Type Entity, Type Props), ConstructorInfo> CtorCache = new();
    private static readonly ConcurrentDictionary<(Type Target, string Field), FieldInfo> FieldCache = new();
    private static readonly ConcurrentDictionary<Type, PropertyInfo> AuditCache = new();
    private static readonly ConcurrentDictionary<Type, ConstructorInfo> StringCtorCache = new();

    private static TEntity Construct<TEntity, TProps>(TProps props)
        where TEntity : class
        where TProps : class
    {
        var ctor = CtorCache.GetOrAdd((typeof(TEntity), typeof(TProps)), static llave =>
            llave.Entity.GetConstructor(InstanceFlags, null, [llave.Props], null)
                ?? throw new InvalidOperationException($"Constructor for {llave.Entity.Name} not found."));

        return (TEntity)ctor.Invoke([props]);
    }

    private static void SetField<TTarget>(object target, string fieldName, TTarget value)
    {
        var field = FieldCache.GetOrAdd((target.GetType(), fieldName), static llave =>
            llave.Target.GetField(llave.Field, InstanceFlags)
                ?? throw new InvalidOperationException($"Field {llave.Field} not found on {llave.Target.Name}."));

        field.SetValue(target, value);
    }

    private static void SetAudit(object props, string createdBy, DateTime createdAtUtc, string? updatedBy, DateTime? updatedAtUtc, string auditTimeSpan)
    {
        var property = AuditCache.GetOrAdd(props.GetType(), static tipo =>
            tipo.GetProperty("Audit", InstanceFlags)
                ?? throw new InvalidOperationException($"Audit property not found on {tipo.Name}."));

        property.SetValue(props, AuditValueObject.Load(new AuditProps
        {
            CreatedBy = createdBy,
            CreatedAt = createdAtUtc,
            UpdatedBy = updatedBy,
            UpdatedAt = updatedAtUtc,
            TimeSpan = auditTimeSpan,
        }));
    }

    private static TValueObject ConstructStringValueObject<TValueObject>(string value)
        where TValueObject : class
    {
        var ctor = StringCtorCache.GetOrAdd(typeof(TValueObject), static tipo =>
            tipo.GetConstructor(InstanceFlags, null, [typeof(string)], null)
                ?? throw new InvalidOperationException($"String constructor for {tipo.Name} not found."));

        return (TValueObject)ctor.Invoke([value]);
    }
}
