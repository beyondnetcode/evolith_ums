namespace Evolith.Ums.Domain.Authorization.SystemSuite;

using Evolith.Ums.Domain.Authorization.SystemSuite.Action;
using Evolith.Ums.Domain.Authorization.SystemSuite.AppSetting;
using Evolith.Ums.Domain.Authorization.SystemSuite.DomainResource;
using Evolith.Ums.Domain.Authorization.SystemSuite.Events;
using Evolith.Ums.Domain.Authorization.SystemSuite.MenuNode;
using Evolith.Ums.Domain.Authorization.SystemSuite.Module;
using ActionEntity = Evolith.Ums.Domain.Authorization.SystemSuite.Action.Action;
using AppSettingEntity = Evolith.Ums.Domain.Authorization.SystemSuite.AppSetting.AppSetting;
using DomainResourceEntity = Evolith.Ums.Domain.Authorization.SystemSuite.DomainResource.DomainResource;
using ModuleEntity = Evolith.Ums.Domain.Authorization.SystemSuite.Module.Module;

public sealed class SystemSuite : AggregateRoot<SystemSuite, SystemSuiteProps>
{
    private readonly List<ModuleEntity> _modules = new();
    private readonly List<AppSettingEntity> _appSettings = new();
    private readonly List<ActionEntity> _actions = new();
    private readonly List<DomainResourceEntity> _domainResources = new();

    public new SystemSuiteDomainEventsManager DomainEvents { get; }

    private SystemSuite(SystemSuiteProps props) : base(props)
    {
        DomainEvents = new SystemSuiteDomainEventsManager(this);

        if (TrackingState.IsNew)
        {
            DomainEvents.RaiseEvent(new SystemSuiteRegisteredEvent(props.Id.GetValue(), props.TenantId.GetValue(), props.Code.GetValue()));
        }
    }

    public TenantId TenantId => Props.TenantId;
    public Code Code => Props.Code;
    public Name Name => Props.Name;
    public Description Description => Props.Description;
    public SystemStatus Status => Props.Status;

    public IReadOnlyCollection<ModuleEntity> Modules => _modules.AsReadOnly();
    public IReadOnlyCollection<AppSettingEntity> AppSettings => _appSettings.AsReadOnly();
    public IReadOnlyCollection<ActionEntity> Actions => _actions.AsReadOnly();
    public IReadOnlyCollection<DomainResourceEntity> DomainResources => _domainResources.AsReadOnly();

    public SystemSuiteId GetId() => SystemSuiteId.Load(Props.Id.GetValue());

    public static Result<SystemSuite> Create(
        TenantId tenantId,
        Code code,
        Name name,
        Description description,
        ActorId createdBy)
    {
        var props = new SystemSuiteProps(IdValueObject.Create(), tenantId, code, name, description, SystemStatus.Active, createdBy);
        var suite = new SystemSuite(props);

        if (!suite.IsValid())
        {
            return Result<SystemSuite>.Failure(suite.BrokenRules.GetBrokenRulesAsString());
        }

        return Result<SystemSuite>.Success(suite);
    }

    public Result Update(Name name, Description description, ActorId updatedBy)
    {
        SetProps(Props.WithName(name).WithDescription(description));

        if (!IsValid())
        {
            return Result.Failure(BrokenRules.GetBrokenRulesAsString());
        }

        Props.Audit.Update(updatedBy.GetValue());
        return Result.Success();
    }

    /// <summary>
    /// Cambia el estado de servicio del sistema (Active ↔ Maintenance ↔ Deprecated).
    ///
    /// NO es la puerta de la eliminación lógica, y por eso cierra las dos direcciones:
    /// <list type="bullet">
    ///   <item><description>No deja ENTRAR a <see cref="SystemStatus.Deleted"/>. Si lo dejara, un
    ///   <c>PUT /status {"status":"Deleted"}</c> eliminaría el sistema saltándose la guarda de
    ///   cascada, que es justo lo que la regla de negocio existe para impedir.</description></item>
    ///   <item><description>No deja SALIR de él. «Eliminado» es terminal: resucitar un sistema
    ///   cuyos dependientes se dieron por perdidos dejaría el catálogo en un estado que nadie
    ///   validó.</description></item>
    /// </list>
    /// </summary>
    public Result SetStatus(SystemStatus status, ActorId updatedBy)
    {
        // Comparación por Id y no por referencia: el estado se rehidrata desde la base y la instancia
        // no tiene por qué ser la misma constante estática.
        if (Props.Status.Id == SystemStatus.Deleted.Id)
        {
            return Result.Failure(DomainErrors.Authorization.SystemSuiteDeletedIsTerminal);
        }

        if (status.Id == SystemStatus.Deleted.Id)
        {
            return Result.Failure(DomainErrors.Authorization.SystemSuiteDeletedNotSettable);
        }

        SetProps(Props.WithStatus(status));
        DomainEvents.RaiseEvent(new SystemSuiteStatusChangedEvent(Props.Id.GetValue(), status.Name));
        Props.Audit.Update(updatedBy.GetValue());
        return Result.Success();
    }

    /// <summary>
    /// Elimina LÓGICAMENTE el sistema: lo lleva al estado terminal <see cref="SystemStatus.Deleted"/>
    /// y lo saca de todas las lecturas, dejando la fila en la base (G-246).
    ///
    /// <para><b>Por qué no se borra la fila.</b> Sobre el catálogo se consultan datos antiguos —a qué
    /// sistema pertenecía un rol, un permiso concedido o un asiento de auditoría de hace dos años—.
    /// Una fila borrada de verdad se pierde para siempre y se lleva por delante esas respuestas. El
    /// mismo patrón que ya sigue <c>UserAccount</c> con <see cref="UserStatus.Deleted"/>.</para>
    ///
    /// <para><b>Las dos condiciones, y por qué son estas.</b></para>
    /// <list type="number">
    ///   <item><description><b>Archivado previo.</b> Solo se elimina lo que ya se dio de baja
    ///   (<see cref="SystemStatus.Deprecated"/>). Un sistema en servicio no debe desaparecer por un
    ///   DELETE suelto: retirarlo del servicio es una decisión explícita y anterior.</description></item>
    ///   <item><description><b>Regla de cascada.</b> Ninguna referencia VIVA puede quedar apuntando
    ///   al sistema. Es la misma regla que un <c>ON DELETE RESTRICT</c> de la base, trasladada al
    ///   plano lógico: lo que ya está eliminado lógicamente no estorba —es una lápida—, pero lo que
    ///   sigue vivo sí, porque quedaría colgando de un sistema que ya no se puede leer. Qué cuenta
    ///   como «vivo» lo resuelve <see cref="SystemSuiteDependents"/>.</description></item>
    /// </list>
    ///
    /// <para>La composición PROPIA del agregado (módulos, nodos, acciones, ajustes, recursos de
    /// dominio) no se toca ni se cuenta: no tiene existencia fuera del sistema y solo se lee a
    /// través de él, así que se oculta con él sin necesidad de marcarla una por una.</para>
    /// </summary>
    public Result Delete(SystemSuiteDependents dependents, ActorId deletedBy)
    {
        if (Props.Status.Id == SystemStatus.Deleted.Id)
        {
            return Result.Failure(DomainErrors.Authorization.SystemSuiteAlreadyDeleted);
        }

        if (Props.Status.Id != SystemStatus.Deprecated.Id)
        {
            return Result.Failure(DomainErrors.Authorization.SystemSuiteNotDeprecated);
        }

        if (dependents.LiveReferences > 0)
        {
            return Result.Failure(DomainErrors.Authorization.SystemSuiteHasDependents);
        }

        SetProps(Props.WithStatus(SystemStatus.Deleted));
        DomainEvents.RaiseEvent(new SystemSuiteDeletedEvent(Props.Id.GetValue(), Props.TenantId.GetValue(), Props.Code.GetValue()));
        TrackingState.MarkAsDirty();
        Props.Audit.Update(deletedBy.GetValue());
        return Result.Success();
    }

    public Result<Guid> AddModule(Code code, Name name, Description description, int sortOrder, ActorId createdBy, string? icon = null)
    {
        BrokenRules.Clear();

        if (_modules.Any(m => m.Code.GetValue() == code.GetValue()))
        {
            BrokenRules.Add(new BrokenRule(nameof(Modules), DomainErrors.SystemSuite.ModuleCodeNotUnique));
        }

        if (!IsValid())
        {
            return Result<Guid>.Failure(BrokenRules.GetBrokenRulesAsString());
        }

        var moduleResult = ModuleEntity.Create(SystemId.Load(Props.Id.GetValue()), code, name, description, sortOrder, createdBy, icon);
        if (moduleResult.IsFailure)
        {
            return Result<Guid>.Failure(moduleResult.Error);
        }

        _modules.Add(moduleResult.Value);
        DomainEvents.RaiseEvent(new SystemSuiteModuleAddedEvent(Props.Id.GetValue(), moduleResult.Value.GetId().GetValue(), code.GetValue()));
        TrackingState.MarkAsDirty();
        Props.Audit.Update(createdBy.GetValue());
        return Result<Guid>.Success(moduleResult.Value.GetId().GetValue());
    }

    public Result RemoveModule(IdValueObject moduleId, ActorId updatedBy)
    {
        var module = FindModule(moduleId);
        if (module.IsFailure)
        {
            BrokenRules.Add(new BrokenRule(nameof(Modules), DomainErrors.Common.NotFound));
        }
        // Invariante ModuleHasActiveMenus (G-154): el agregado cuenta sus propios nodos-menú
        // activos, en vez de recibir el conteo como parámetro con default 0 (footgun: el handler
        // nunca lo poblaba → guarda inalcanzable). No se elimina un módulo con navegación activa.
        else if (module.Value.Nodes.Any(node => node.Status == ModuleStatus.Active))
        {
            BrokenRules.Add(new BrokenRule(nameof(Modules), DomainErrors.Authorization.ModuleHasActiveMenus));
        }

        if (!IsValid())
        {
            return Result.Failure(BrokenRules.GetBrokenRulesAsString());
        }

        _modules.Remove(module.Value);
        DomainEvents.RaiseEvent(new SystemSuiteModuleRemovedEvent(Props.Id.GetValue(), module.Value.GetId().GetValue()));
        TrackingState.MarkAsDirty();
        Props.Audit.Update(updatedBy.GetValue());
        return Result.Success();
    }

    public Result UpdateModule(IdValueObject moduleId, Name name, Description description, int sortOrder, ActorId updatedBy)
    {
        var module = FindModule(moduleId);
        if (module.IsFailure)
        {
            BrokenRules.Add(new BrokenRule(nameof(Modules), DomainErrors.Common.NotFound));
        }

        if (!IsValid())
        {
            return Result.Failure(BrokenRules.GetBrokenRulesAsString());
        }

        var updateResult = module.Value.Update(name, description, sortOrder, updatedBy);
        if (updateResult.IsFailure)
        {
            return Result.Failure(updateResult.Error);
        }

        TrackingState.MarkAsDirty();
        Props.Audit.Update(updatedBy.GetValue());
        return Result.Success();
    }

    public Result ActivateModule(IdValueObject moduleId, ActorId updatedBy)
    {
        var module = FindModule(moduleId);
        if (module.IsFailure)
        {
            BrokenRules.Add(new BrokenRule(nameof(Modules), DomainErrors.Common.NotFound));
        }

        if (!IsValid())
        {
            return Result.Failure(BrokenRules.GetBrokenRulesAsString());
        }

        var activateResult = module.Value.Activate(updatedBy);
        if (activateResult.IsFailure)
        {
            return Result.Failure(activateResult.Error);
        }

        DomainEvents.RaiseEvent(new SystemSuiteModuleStatusChangedEvent(Props.Id.GetValue(), module.Value.GetId().GetValue(), "Active"));
        TrackingState.MarkAsDirty();
        Props.Audit.Update(updatedBy.GetValue());
        return Result.Success();
    }

    public Result DeactivateModule(IdValueObject moduleId, ActorId updatedBy)
    {
        var module = FindModule(moduleId);
        if (module.IsFailure)
        {
            BrokenRules.Add(new BrokenRule(nameof(Modules), DomainErrors.Common.NotFound));
        }

        if (!IsValid())
        {
            return Result.Failure(BrokenRules.GetBrokenRulesAsString());
        }

        var deactivateResult = module.Value.Deactivate(updatedBy);
        if (deactivateResult.IsFailure)
        {
            return Result.Failure(deactivateResult.Error);
        }

        DomainEvents.RaiseEvent(new SystemSuiteModuleStatusChangedEvent(Props.Id.GetValue(), module.Value.GetId().GetValue(), "Inactive"));
        TrackingState.MarkAsDirty();
        Props.Audit.Update(updatedBy.GetValue());
        return Result.Success();
    }

    /// <param name="isClientVisible">
    /// Si el ajuste puede viajar al cliente en el grafo. Por defecto NO: la bolsa clave/valor
    /// puede contener secretos, así que la exposición se decide ajuste a ajuste (G-178).
    /// </param>
    public Result AddAppSetting(
        ConfigurationKey key,
        ConfigurationValue value,
        ConfigurationScope scope,
        ActorId createdBy,
        bool isClientVisible = false)
    {
        if (_appSettings.Any(c => c.Key.GetValue() == key.GetValue() && c.Scope.Id == scope.Id))
        {
            BrokenRules.Add(new BrokenRule(nameof(AppSettings), DomainErrors.SystemSuite.ConfigurationKeyAlreadyExists));
        }

        if (!IsValid())
        {
            return Result.Failure(BrokenRules.GetBrokenRulesAsString());
        }

        var settingResult = AppSettingEntity.Create(key, value, scope, isClientVisible);
        if (settingResult.IsFailure)
        {
            return Result.Failure(settingResult.Error);
        }

        _appSettings.Add(settingResult.Value);
        TrackingState.MarkAsDirty();
        Props.Audit.Update(createdBy.GetValue());
        return Result.Success();
    }

    public Result UpdateAppSetting(ConfigurationKey key, ConfigurationValue newValue, ActorId updatedBy)
    {
        var setting = FindAppSetting(key);
        if (setting.IsFailure)
        {
            BrokenRules.Add(new BrokenRule(nameof(AppSettings), DomainErrors.SystemSuite.ConfigurationKeyNotFound));
        }

        if (!IsValid())
        {
            return Result.Failure(BrokenRules.GetBrokenRulesAsString());
        }

        var index = _appSettings.IndexOf(setting.Value);
        _appSettings[index] = setting.Value.WithValue(newValue);
        TrackingState.MarkAsDirty();
        Props.Audit.Update(updatedBy.GetValue());
        return Result.Success();
    }

    public Result RemoveAppSetting(ConfigurationKey key, ActorId updatedBy)
    {
        var setting = FindAppSetting(key);
        if (setting.IsFailure)
        {
            BrokenRules.Add(new BrokenRule(nameof(AppSettings), DomainErrors.SystemSuite.ConfigurationKeyNotFound));
        }

        if (!IsValid())
        {
            return Result.Failure(BrokenRules.GetBrokenRulesAsString());
        }

        _appSettings.Remove(setting.Value);
        TrackingState.MarkAsDirty();
        Props.Audit.Update(updatedBy.GetValue());
        return Result.Success();
    }

    // ── Árbol de navegación recursivo por módulo (MenuNode, ADR-0090) ──────────

    /// <summary>Fija o borra el icono de un módulo.</summary>
    public Result SetModuleIcon(IdValueObject moduleId, string? icon, ActorId updatedBy)
        => DelegateToModule(moduleId, m => m.SetIcon(icon, updatedBy), updatedBy);

    public Result<Guid> AddModuleRootNode(IdValueObject moduleId, NodeKind kind, Code code, Name label, Description description, int sortOrder, ActorId createdBy, MenuNodeMetadata? metadata = null, MenuNodePresentation? presentation = null)
        => DelegateToModule(moduleId, m => m.AddRootNode(kind, code, label, description, sortOrder, createdBy, metadata, presentation), createdBy);

    public Result<Guid> AddModuleChildNode(IdValueObject moduleId, IdValueObject parentNodeId, NodeKind kind, Code code, Name label, Description description, int sortOrder, ActorId createdBy, MenuNodeMetadata? metadata = null, MenuNodePresentation? presentation = null)
        => DelegateToModule(moduleId, m => m.AddChildNode(parentNodeId, kind, code, label, description, sortOrder, createdBy, metadata, presentation), createdBy);

    public Result UpdateModuleNode(IdValueObject moduleId, IdValueObject nodeId, Name label, Description description, int sortOrder, ActorId updatedBy)
        => DelegateToModule(moduleId, m => m.UpdateNode(nodeId, label, description, sortOrder, updatedBy), updatedBy);

    public Result RemoveModuleNode(IdValueObject moduleId, IdValueObject nodeId, ActorId updatedBy)
        => DelegateToModule(moduleId, m => m.RemoveNode(nodeId, updatedBy), updatedBy);

    public Result ActivateModuleNode(IdValueObject moduleId, IdValueObject nodeId, ActorId updatedBy)
        => DelegateToModule(moduleId, m => m.ActivateNode(nodeId, updatedBy), updatedBy);

    public Result DeactivateModuleNode(IdValueObject moduleId, IdValueObject nodeId, ActorId updatedBy)
        => DelegateToModule(moduleId, m => m.DeactivateNode(nodeId, updatedBy), updatedBy);

    public Result LinkModuleNodeAction(IdValueObject moduleId, IdValueObject nodeId, ActionCode actionCode, ActorId updatedBy)
    {
        // G-046: only actions registered in this suite's catalog may be linked to a node.
        // Linking an unregistered action code would produce a dangling permission reference.
        if (FindAction(actionCode).IsFailure)
        {
            return Result.Failure(DomainErrors.SystemSuite.ActionNotRegistered);
        }

        return DelegateToModule(moduleId, m => m.LinkNodeAction(nodeId, actionCode, updatedBy), updatedBy);
    }

    public Result UnlinkModuleNodeAction(IdValueObject moduleId, IdValueObject nodeId, ActionCode actionCode, ActorId updatedBy)
        => DelegateToModule(moduleId, m => m.UnlinkNodeAction(nodeId, actionCode, updatedBy), updatedBy);

    public Result SetModuleNodeMetadata(IdValueObject moduleId, IdValueObject nodeId, MenuNodeMetadata metadata, ActorId updatedBy)
        => DelegateToModule(moduleId, m => m.SetNodeMetadata(nodeId, metadata, updatedBy), updatedBy);

    private Result DelegateToModule(IdValueObject moduleId, Func<ModuleEntity, Result> action, ActorId updatedBy)
    {
        var module = FindModule(moduleId);
        if (module.IsFailure)
            return Result.Failure(module.Error);

        var result = action(module.Value);
        if (result.IsFailure) return result;

        TrackingState.MarkAsDirty();
        Props.Audit.Update(updatedBy.GetValue());
        return Result.Success();
    }

    // Variante que propaga el id del recurso creado por el módulo (p.ej. el id del nodo
    // recién añadido), para que los creates devuelvan el id y no fuercen un GET posterior.
    private Result<T> DelegateToModule<T>(IdValueObject moduleId, Func<ModuleEntity, Result<T>> action, ActorId updatedBy)
    {
        var module = FindModule(moduleId);
        if (module.IsFailure)
            return Result<T>.Failure(module.Error);

        var result = action(module.Value);
        if (result.IsFailure) return result;

        TrackingState.MarkAsDirty();
        Props.Audit.Update(updatedBy.GetValue());
        return result;
    }

    public Result<Guid> RegisterAction(ActionCode code, Name name, ActorId createdBy)
    {
        if (_actions.Any(a => a.Code.GetValue() == code.GetValue()))
        {
            BrokenRules.Add(new BrokenRule(nameof(Actions), DomainErrors.Common.Duplicate));
        }

        if (!IsValid())
        {
            return Result<Guid>.Failure(BrokenRules.GetBrokenRulesAsString());
        }

        var actionResult = ActionEntity.Create(Props.TenantId, GetId(), null, code, name, createdBy);
        if (actionResult.IsFailure)
        {
            return Result<Guid>.Failure(actionResult.Error);
        }

        _actions.Add(actionResult.Value);
        DomainEvents.RaiseEvent(new SystemSuiteActionRegisteredEvent(Props.Id.GetValue(), code.GetValue()));
        TrackingState.MarkAsDirty();
        Props.Audit.Update(createdBy.GetValue());
        return Result<Guid>.Success(actionResult.Value.GetId().GetValue());
    }

    public Result RemoveAction(ActionCode code, ActorId updatedBy)
    {
        var action = FindAction(code);
        if (action.IsFailure)
        {
            BrokenRules.Add(new BrokenRule(nameof(Actions), DomainErrors.Common.NotFound));
        }

        if (!IsValid())
        {
            return Result.Failure(BrokenRules.GetBrokenRulesAsString());
        }

        _actions.Remove(action.Value);
        DomainEvents.RaiseEvent(new SystemSuiteActionRemovedEvent(Props.Id.GetValue(), code.GetValue()));
        TrackingState.MarkAsDirty();
        Props.Audit.Update(updatedBy.GetValue());
        return Result.Success();
    }

    public Result RenameAction(ActionCode code, Name newName, ActorId updatedBy)
    {
        var action = FindAction(code);
        if (action.IsFailure)
        {
            BrokenRules.Add(new BrokenRule(nameof(Actions), DomainErrors.Common.NotFound));
        }

        if (!IsValid())
        {
            return Result.Failure(BrokenRules.GetBrokenRulesAsString());
        }

        action.Value.Props.Name = newName;
        TrackingState.MarkAsDirty();
        Props.Audit.Update(updatedBy.GetValue());
        return Result.Success();
    }

    // ── Domain Resource lifecycle ─────────────────────────────────────────────

    public Result<Guid> AddDomainResource(ModuleId? moduleId, IdValueObject? parentResourceId, DomainResourceType type, Code code, Name name, Description description, ActorId createdBy)
    {
        BrokenRules.Clear();

        if (_domainResources.Any(dr => dr.Code.GetValue() == code.GetValue()))
        {
            BrokenRules.Add(new BrokenRule(nameof(DomainResources), DomainErrors.Common.Duplicate));
        }

        // DDD rule: DomainMethod must belong to a parent resource
        if (type == DomainResourceType.DomainMethod && parentResourceId is null)
        {
            BrokenRules.Add(new BrokenRule(nameof(DomainResources), DomainErrors.Authorization.DomainMethodRequiresParent));
        }

        // DDD rule: parent, when provided, must exist and must be an Aggregate or Entity
        if (parentResourceId is not null)
        {
            var parent = _domainResources.FirstOrDefault(r => r.Props.Id.GetValue() == parentResourceId.GetValue());
            if (parent is null)
            {
                BrokenRules.Add(new BrokenRule(nameof(DomainResources), DomainErrors.Authorization.ParentResourceNotFound));
            }
            else if (parent.Type == DomainResourceType.DomainMethod)
            {
                BrokenRules.Add(new BrokenRule(nameof(DomainResources), DomainErrors.Authorization.DomainMethodCannotBeParent));
            }
        }

        if (!IsValid())
        {
            return Result<Guid>.Failure(BrokenRules.GetBrokenRulesAsString());
        }

        var drResult = DomainResourceEntity.Create(GetId(), moduleId, parentResourceId, type, code, name, description, createdBy);
        if (drResult.IsFailure)
        {
            return Result<Guid>.Failure(drResult.Error);
        }

        _domainResources.Add(drResult.Value);
        TrackingState.MarkAsDirty();
        Props.Audit.Update(createdBy.GetValue());
        return Result<Guid>.Success(drResult.Value.GetId().GetValue());
    }

    public Result UpdateDomainResource(IdValueObject resourceId, Name name, Description description, ActorId updatedBy)
    {
        var resource = FindDomainResource(resourceId);
        if (resource.IsFailure)
        {
            BrokenRules.Add(new BrokenRule(nameof(DomainResources), DomainErrors.Common.NotFound));
        }

        if (!IsValid())
        {
            return Result.Failure(BrokenRules.GetBrokenRulesAsString());
        }

        var updateResult = resource.Value.Update(name, description, updatedBy);
        if (updateResult.IsFailure)
        {
            return Result.Failure(updateResult.Error);
        }

        TrackingState.MarkAsDirty();
        Props.Audit.Update(updatedBy.GetValue());
        return Result.Success();
    }

    public Result RemoveDomainResource(IdValueObject resourceId, ActorId updatedBy, int templateItemCount = 0)
    {
        var resource = FindDomainResource(resourceId);
        if (resource.IsFailure)
        {
            BrokenRules.Add(new BrokenRule(nameof(DomainResources), DomainErrors.Common.NotFound));
        }

        if (templateItemCount > 0)
        {
            BrokenRules.Add(new BrokenRule(nameof(DomainResources), DomainErrors.Authorization.DomainResourceHasTemplateItems));
        }

        if (!IsValid())
        {
            return Result.Failure(BrokenRules.GetBrokenRulesAsString());
        }

        _domainResources.Remove(resource.Value);
        TrackingState.MarkAsDirty();
        Props.Audit.Update(updatedBy.GetValue());
        return Result.Success();
    }

    private Result<ModuleEntity> FindModule(IdValueObject moduleId)
    {
        var module = _modules.FirstOrDefault(m =>
            m.Props.Id.GetValue() == moduleId.GetValue() ||
            m.Id.GetValue() == moduleId.GetValue());
        return module is null
            ? Result<ModuleEntity>.Failure(DomainErrors.Common.NotFound)
            : Result<ModuleEntity>.Success(module);
    }

    private Result<AppSettingEntity> FindAppSetting(ConfigurationKey key)
    {
        var setting = _appSettings.FirstOrDefault(c => c.Key.GetValue() == key.GetValue());
        return setting is null
            ? Result<AppSettingEntity>.Failure(DomainErrors.SystemSuite.ConfigurationKeyNotFound)
            : Result<AppSettingEntity>.Success(setting);
    }

    private Result<ActionEntity> FindAction(ActionCode code)
    {
        // ActionCode is a value object whose base type does not overload operator ==, so a
        // reference comparison (a.Code == code) only matches the very same instance. FindAction
        // receives freshly built ActionCode instances (e.g. from LinkModuleNodeAction's G-046
        // guard, endpoints, seeders), so it must compare by value.
        var action = _actions.FirstOrDefault(a => a.Code.GetValue() == code.GetValue());
        return action is null
            ? Result<ActionEntity>.Failure(DomainErrors.Common.NotFound)
            : Result<ActionEntity>.Success(action);
    }

    private Result<DomainResourceEntity> FindDomainResource(IdValueObject resourceId)
    {
        var resource = _domainResources.FirstOrDefault(r => r.Props.Id.GetValue() == resourceId.GetValue());
        return resource is null
            ? Result<DomainResourceEntity>.Failure(DomainErrors.Common.NotFound)
            : Result<DomainResourceEntity>.Success(resource);
    }
}
