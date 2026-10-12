namespace Evolith.Ums.Domain.Test.Authorization.SystemSuite;

using Evolith.Ums.Domain.Authorization.SystemSuite;
using Evolith.Ums.Domain.Authorization.SystemSuite.DomainResource;
using Evolith.Ums.Domain.Authorization.SystemSuite.MenuNode;
using Xunit;

public class SystemSuiteTests
{
    private static readonly TenantId ValidTenantId = TenantId.Load(Guid.NewGuid().ToString());
    private static readonly Code ValidCode = Code.Create("SYS-001");
    private static readonly Name ValidName = Name.Create("Test System Suite");
    private static readonly Description ValidDescription = Description.Create("A test system suite");
    private static readonly ActorId ValidActor = ActorId.Create("user-001");
    #region Create

    [Fact]
    public void Create_WithValidData_ReturnsSuccess()
    {
        var result = SystemSuite.Create(ValidTenantId, ValidCode, ValidName, ValidDescription, ValidActor);

        Assert.True(result.IsSuccess);
        Assert.Equal(ValidTenantId, result.Value.TenantId);
        Assert.Equal(ValidCode, result.Value.Code);
        Assert.Equal(ValidName, result.Value.Name);
        Assert.Equal(ValidDescription, result.Value.Description);
        Assert.Equal(SystemStatus.Active, result.Value.Status);
        Assert.Empty(result.Value.Modules);
        Assert.Empty(result.Value.AppSettings);
        Assert.Empty(result.Value.Actions);
    }

    [Fact]
    public void Create_WithEmptyCode_ReturnsFailure()
    {
        var code = Code.Create("");

        var result = SystemSuite.Create(ValidTenantId, code, ValidName, ValidDescription, ValidActor);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Create_RaisesSystemSuiteRegisteredEvent()
    {
        var result = SystemSuite.Create(ValidTenantId, ValidCode, ValidName, ValidDescription, ValidActor);

        Assert.True(result.IsSuccess);
        var events = result.Value.DomainEvents.GetUncommittedChanges().ToList();
        Assert.Single(events);
        Assert.IsType<SystemSuiteRegisteredEvent>(events[0]);
    }

    #endregion

    #region Update

    [Fact]
    public void Update_WithValidData_ReturnsSuccess()
    {
        var suite = SystemSuite.Create(ValidTenantId, ValidCode, ValidName, ValidDescription, ValidActor).Value;
        var newName = Name.Create("Updated System Suite");
        var newDescription = Description.Create("Updated description");

        var result = suite.Update(newName, newDescription, ValidActor);

        Assert.True(result.IsSuccess);
        Assert.Equal(newName, suite.Name);
        Assert.Equal(newDescription, suite.Description);
    }

    #endregion

    #region SetStatus

    [Fact]
    public void SetStatus_WithValidStatus_ReturnsSuccess()
    {
        var suite = SystemSuite.Create(ValidTenantId, ValidCode, ValidName, ValidDescription, ValidActor).Value;

        var result = suite.SetStatus(SystemStatus.Deprecated, ValidActor);

        Assert.True(result.IsSuccess);
        Assert.Equal(SystemStatus.Deprecated, suite.Status);
    }

    [Fact]
    public void SetStatus_RaisesSystemSuiteStatusChangedEvent()
    {
        var suite = SystemSuite.Create(ValidTenantId, ValidCode, ValidName, ValidDescription, ValidActor).Value;

        suite.SetStatus(SystemStatus.Deprecated, ValidActor);

        var events = suite.DomainEvents.GetUncommittedChanges().ToList();
        Assert.Contains(events, e => e is SystemSuiteStatusChangedEvent);
    }

    #endregion

    #region Delete (eliminación lógica, G-246)

    [Fact]
    public void Delete_WhenStillInService_ReturnsFailure()
    {
        var suite = SystemSuite.Create(ValidTenantId, ValidCode, ValidName, ValidDescription, ValidActor).Value;

        var result = suite.Delete(SystemSuiteDependents.None, ValidActor);

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrors.Authorization.SystemSuiteNotDeprecated, result.Error);
    }

    [Fact]
    public void Delete_WhenDeprecatedWithoutLiveReferences_MarksDeletedAndRaisesEvent()
    {
        var suite = SystemSuite.Create(ValidTenantId, ValidCode, ValidName, ValidDescription, ValidActor).Value;
        // Su propia composición no cuenta como referencia externa: se oculta con el agregado.
        suite.AddModule(Code.Create("MOD-001"), ValidName, ValidDescription, 1, ValidActor);
        suite.SetStatus(SystemStatus.Deprecated, ValidActor);

        var result = suite.Delete(SystemSuiteDependents.None, ValidActor);

        Assert.True(result.IsSuccess);
        // La prueba de que es borrado LÓGICO en el plano del dominio: el agregado sigue existiendo,
        // con su composición intacta, y lo único que cambió es el estado.
        Assert.Equal(SystemStatus.Deleted, suite.Status);
        Assert.Single(suite.Modules);
        Assert.Contains(suite.DomainEvents.GetUncommittedChanges(), e => e is SystemSuiteDeletedEvent);
    }

    [Fact]
    public void Delete_WithLiveReferences_ReturnsFailureAndLeavesStatusUntouched()
    {
        var suite = SystemSuite.Create(ValidTenantId, ValidCode, ValidName, ValidDescription, ValidActor).Value;
        suite.SetStatus(SystemStatus.Deprecated, ValidActor);

        var result = suite.Delete(SystemSuiteDependents.None with { Roles = 1 }, ValidActor);

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrors.Authorization.SystemSuiteHasDependents, result.Error);
        Assert.Equal(SystemStatus.Deprecated, suite.Status);
        Assert.DoesNotContain(suite.DomainEvents.GetUncommittedChanges(), e => e is SystemSuiteDeletedEvent);
    }

    [Fact]
    public void Delete_WithReferenceAlreadyLogicallyDeleted_Succeeds()
    {
        var suite = SystemSuite.Create(ValidTenantId, ValidCode, ValidName, ValidDescription, ValidActor).Value;
        suite.SetStatus(SystemStatus.Deprecated, ValidActor);

        // El conteo llega ya depurado desde el repositorio: lo eliminado lógicamente no se cuenta,
        // así que el dominio ve cero referencias vivas y deja eliminar. Es la otra mitad de la regla
        // de cascada — la que evita que un sistema quede bloqueado para siempre por lápidas.
        var result = suite.Delete(SystemSuiteDependents.None, ValidActor);

        Assert.True(result.IsSuccess);
        Assert.Equal(SystemStatus.Deleted, suite.Status);
    }

    [Fact]
    public void Delete_WhenAlreadyDeleted_ReturnsFailure()
    {
        var suite = SystemSuite.Create(ValidTenantId, ValidCode, ValidName, ValidDescription, ValidActor).Value;
        suite.SetStatus(SystemStatus.Deprecated, ValidActor);
        suite.Delete(SystemSuiteDependents.None, ValidActor);

        var result = suite.Delete(SystemSuiteDependents.None, ValidActor);

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrors.Authorization.SystemSuiteAlreadyDeleted, result.Error);
    }

    [Fact]
    public void SetStatus_ToDeleted_ReturnsFailure()
    {
        // Si esto se permitiera, `PUT /status {"status":"Deleted"}` eliminaría el sistema sin pasar
        // por la guarda de cascada. La puerta de la eliminación es una sola.
        var suite = SystemSuite.Create(ValidTenantId, ValidCode, ValidName, ValidDescription, ValidActor).Value;

        var result = suite.SetStatus(SystemStatus.Deleted, ValidActor);

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrors.Authorization.SystemSuiteDeletedNotSettable, result.Error);
        Assert.Equal(SystemStatus.Active, suite.Status);
    }

    [Fact]
    public void SetStatus_OnDeletedSuite_ReturnsFailure()
    {
        var suite = SystemSuite.Create(ValidTenantId, ValidCode, ValidName, ValidDescription, ValidActor).Value;
        suite.SetStatus(SystemStatus.Deprecated, ValidActor);
        suite.Delete(SystemSuiteDependents.None, ValidActor);

        var result = suite.SetStatus(SystemStatus.Active, ValidActor);

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrors.Authorization.SystemSuiteDeletedIsTerminal, result.Error);
        Assert.Equal(SystemStatus.Deleted, suite.Status);
    }

    #endregion

    #region AddModule

    [Fact]
    public void AddModule_WithValidData_ReturnsSuccess()
    {
        var suite = SystemSuite.Create(ValidTenantId, ValidCode, ValidName, ValidDescription, ValidActor).Value;
        var moduleCode = Code.Create("MOD-001");
        var moduleName = Name.Create("Test Module");
        var moduleDescription = Description.Create("A test module");

        var result = suite.AddModule(moduleCode, moduleName, moduleDescription, 1, ValidActor);

        Assert.True(result.IsSuccess);
        Assert.Single(suite.Modules);
    }

    [Fact]
    public void AddModule_WithDuplicateCode_ReturnsFailure()
    {
        var suite = SystemSuite.Create(ValidTenantId, ValidCode, ValidName, ValidDescription, ValidActor).Value;
        var moduleCode = Code.Create("MOD-001");
        var moduleName = Name.Create("Test Module");
        var moduleDescription = Description.Create("A test module");
        suite.AddModule(moduleCode, moduleName, moduleDescription, 1, ValidActor);

        var result = suite.AddModule(moduleCode, Name.Create("Another Module"), Description.Create("Another"), 2, ValidActor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.SystemSuite.ModuleCodeNotUnique, result.Error);
    }

    [Fact]
    public void AddModule_RaisesSystemSuiteModuleAddedEvent()
    {
        var suite = SystemSuite.Create(ValidTenantId, ValidCode, ValidName, ValidDescription, ValidActor).Value;
        var moduleCode = Code.Create("MOD-001");
        var moduleName = Name.Create("Test Module");
        var moduleDescription = Description.Create("A test module");

        suite.AddModule(moduleCode, moduleName, moduleDescription, 1, ValidActor);

        var events = suite.DomainEvents.GetUncommittedChanges().ToList();
        Assert.Contains(events, e => e is SystemSuiteModuleAddedEvent);
    }

    #endregion

    #region RemoveModule

    [Fact]
    public void RemoveModule_WhenModuleExists_ReturnsSuccess()
    {
        var suite = SystemSuite.Create(ValidTenantId, ValidCode, ValidName, ValidDescription, ValidActor).Value;
        var moduleCode = Code.Create("MOD-001");
        var moduleName = Name.Create("Test Module");
        var moduleDescription = Description.Create("A test module");
        suite.AddModule(moduleCode, moduleName, moduleDescription, 1, ValidActor);
        var moduleId = suite.Modules.First().GetId();

        var result = suite.RemoveModule(moduleId, ValidActor);

        Assert.True(result.IsSuccess);
        Assert.Empty(suite.Modules);
    }

    [Fact]
    public void RemoveModule_WhenModuleNotFound_ReturnsFailure()
    {
        var suite = SystemSuite.Create(ValidTenantId, ValidCode, ValidName, ValidDescription, ValidActor).Value;
        var fakeId = IdValueObject.Create();

        var result = suite.RemoveModule(fakeId, ValidActor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Common.NotFound, result.Error);
    }

    [Fact]
    public void RemoveModule_RaisesSystemSuiteModuleRemovedEvent()
    {
        var suite = SystemSuite.Create(ValidTenantId, ValidCode, ValidName, ValidDescription, ValidActor).Value;
        var moduleCode = Code.Create("MOD-001");
        var moduleName = Name.Create("Test Module");
        var moduleDescription = Description.Create("A test module");
        suite.AddModule(moduleCode, moduleName, moduleDescription, 1, ValidActor);
        var moduleId = suite.Modules.First().GetId();

        suite.RemoveModule(moduleId, ValidActor);

        var events = suite.DomainEvents.GetUncommittedChanges().ToList();
        Assert.Contains(events, e => e is SystemSuiteModuleRemovedEvent);
    }

    [Fact]
    public void RemoveModule_WhenModuleHasActiveMenus_ReturnsFailure()
    {
        var suite = SystemSuite.Create(ValidTenantId, ValidCode, ValidName, ValidDescription, ValidActor).Value;
        var moduleCode = Code.Create("MOD-001");
        var moduleName = Name.Create("Test Module");
        var moduleDescription = Description.Create("A test module");
        suite.AddModule(moduleCode, moduleName, moduleDescription, 1, ValidActor);
        var moduleId = suite.Modules.First().GetId();
        // El agregado cuenta sus propios nodos-menú activos (G-154): módulo activo + un nodo Menú
        // (nace Active) → la remoción debe fallar por ModuleHasActiveMenus.
        suite.ActivateModule(moduleId, ValidActor);
        suite.AddModuleRootNode(
            moduleId,
            NodeKind.Menu,
            Code.Create("MENU-001"),
            Name.Create("Menú Activo"),
            Description.Create("Un menú activo del módulo"),
            1,
            ValidActor);

        var result = suite.RemoveModule(moduleId, ValidActor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Authorization.ModuleHasActiveMenus, result.Error);
    }

    #endregion

    #region UpdateModule

    [Fact]
    public void UpdateModule_WhenModuleExists_ReturnsSuccess()
    {
        var suite = SystemSuite.Create(ValidTenantId, ValidCode, ValidName, ValidDescription, ValidActor).Value;
        var moduleCode = Code.Create("MOD-001");
        var moduleName = Name.Create("Test Module");
        var moduleDescription = Description.Create("A test module");
        suite.AddModule(moduleCode, moduleName, moduleDescription, 1, ValidActor);
        var moduleId = suite.Modules.First().GetId();
        var newName = Name.Create("Updated Module");
        var newDescription = Description.Create("Updated description");

        var result = suite.UpdateModule(moduleId, newName, newDescription, 2, ValidActor);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void UpdateModule_WhenModuleNotFound_ReturnsFailure()
    {
        var suite = SystemSuite.Create(ValidTenantId, ValidCode, ValidName, ValidDescription, ValidActor).Value;
        var fakeId = IdValueObject.Create();
        var newName = Name.Create("Updated Module");
        var newDescription = Description.Create("Updated description");

        var result = suite.UpdateModule(fakeId, newName, newDescription, 2, ValidActor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Common.NotFound, result.Error);
    }

    #endregion

    #region ActivateModule

    [Fact]
    public void ActivateModule_WhenModuleExists_ReturnsSuccess()
    {
        var suite = SystemSuite.Create(ValidTenantId, ValidCode, ValidName, ValidDescription, ValidActor).Value;
        var moduleCode = Code.Create("MOD-001");
        var moduleName = Name.Create("Test Module");
        var moduleDescription = Description.Create("A test module");
        suite.AddModule(moduleCode, moduleName, moduleDescription, 1, ValidActor);
        var moduleId = suite.Modules.First().GetId();

        var result = suite.ActivateModule(moduleId, ValidActor);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void ActivateModule_WhenModuleNotFound_ReturnsFailure()
    {
        var suite = SystemSuite.Create(ValidTenantId, ValidCode, ValidName, ValidDescription, ValidActor).Value;
        var fakeId = IdValueObject.Create();

        var result = suite.ActivateModule(fakeId, ValidActor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Common.NotFound, result.Error);
    }

    [Fact]
    public void ActivateModule_RaisesSystemSuiteModuleStatusChangedEvent()
    {
        var suite = SystemSuite.Create(ValidTenantId, ValidCode, ValidName, ValidDescription, ValidActor).Value;
        var moduleCode = Code.Create("MOD-001");
        var moduleName = Name.Create("Test Module");
        var moduleDescription = Description.Create("A test module");
        suite.AddModule(moduleCode, moduleName, moduleDescription, 1, ValidActor);
        var moduleId = suite.Modules.First().GetId();

        suite.ActivateModule(moduleId, ValidActor);

        var events = suite.DomainEvents.GetUncommittedChanges().ToList();
        Assert.Contains(events, e => e is SystemSuiteModuleStatusChangedEvent);
    }

    #endregion

    #region DeactivateModule

    [Fact]
    public void DeactivateModule_WhenModuleExists_ReturnsSuccess()
    {
        var suite = SystemSuite.Create(ValidTenantId, ValidCode, ValidName, ValidDescription, ValidActor).Value;
        var moduleCode = Code.Create("MOD-001");
        var moduleName = Name.Create("Test Module");
        var moduleDescription = Description.Create("A test module");
        suite.AddModule(moduleCode, moduleName, moduleDescription, 1, ValidActor);
        suite.ActivateModule(suite.Modules.First().GetId(), ValidActor);
        var moduleId = suite.Modules.First().GetId();

        var result = suite.DeactivateModule(moduleId, ValidActor);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void DeactivateModule_RaisesSystemSuiteModuleStatusChangedEventWithInactive()
    {
        var suite = SystemSuite.Create(ValidTenantId, ValidCode, ValidName, ValidDescription, ValidActor).Value;
        var moduleCode = Code.Create("MOD-001");
        var moduleName = Name.Create("Test Module");
        var moduleDescription = Description.Create("A test module");
        suite.AddModule(moduleCode, moduleName, moduleDescription, 1, ValidActor);
        suite.ActivateModule(suite.Modules.First().GetId(), ValidActor);
        var moduleId = suite.Modules.First().GetId();

        suite.DeactivateModule(moduleId, ValidActor);

        var events = suite.DomainEvents.GetUncommittedChanges().ToList();
        var statusChangedEvent = events.OfType<SystemSuiteModuleStatusChangedEvent>().Last();
        Assert.Equal("Inactive", statusChangedEvent.NewStatus);
    }

    #endregion

    #region AddAppSetting

    [Fact]
    public void AddAppSetting_WithValidData_ReturnsSuccess()
    {
        var suite = SystemSuite.Create(ValidTenantId, ValidCode, ValidName, ValidDescription, ValidActor).Value;
        var key = ConfigurationKey.Create("MaxUsers");
        var value = ConfigurationValue.Create("100");
        var scope = ConfigurationScope.Global;

        var result = suite.AddAppSetting(key, value, scope, ValidActor);

        Assert.True(result.IsSuccess);
        Assert.Single(suite.AppSettings);
    }

    [Fact]
    public void AddAppSetting_WithDuplicateKeyAndScope_ReturnsFailure()
    {
        var suite = SystemSuite.Create(ValidTenantId, ValidCode, ValidName, ValidDescription, ValidActor).Value;
        var key = ConfigurationKey.Create("MaxUsers");
        var value = ConfigurationValue.Create("100");
        var scope = ConfigurationScope.Global;
        suite.AddAppSetting(key, value, scope, ValidActor);

        var result = suite.AddAppSetting(key, ConfigurationValue.Create("200"), scope, ValidActor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.SystemSuite.ConfigurationKeyAlreadyExists, result.Error);
    }

    [Fact]
    public void AddAppSetting_SameKeyDifferentScope_ReturnsSuccess()
    {
        var suite = SystemSuite.Create(ValidTenantId, ValidCode, ValidName, ValidDescription, ValidActor).Value;
        var key = ConfigurationKey.Create("MaxUsers");
        var value = ConfigurationValue.Create("100");
        suite.AddAppSetting(key, value, ConfigurationScope.Global, ValidActor);

        var result = suite.AddAppSetting(key, ConfigurationValue.Create("50"), ConfigurationScope.Tenant, ValidActor);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, suite.AppSettings.Count);
    }

    #endregion

    #region UpdateAppSetting

    [Fact]
    public void UpdateAppSetting_WhenKeyExists_ReturnsSuccess()
    {
        var suite = SystemSuite.Create(ValidTenantId, ValidCode, ValidName, ValidDescription, ValidActor).Value;
        var key = ConfigurationKey.Create("MaxUsers");
        var value = ConfigurationValue.Create("100");
        suite.AddAppSetting(key, value, ConfigurationScope.Global, ValidActor);
        var newValue = ConfigurationValue.Create("200");

        var result = suite.UpdateAppSetting(key, newValue, ValidActor);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void UpdateAppSetting_WhenKeyNotFound_ReturnsFailure()
    {
        var suite = SystemSuite.Create(ValidTenantId, ValidCode, ValidName, ValidDescription, ValidActor).Value;
        var key = ConfigurationKey.Create("NonExistent");
        var newValue = ConfigurationValue.Create("200");

        var result = suite.UpdateAppSetting(key, newValue, ValidActor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.SystemSuite.ConfigurationKeyNotFound, result.Error);
    }

    #endregion

    #region RemoveAppSetting

    [Fact]
    public void RemoveAppSetting_WhenKeyExists_ReturnsSuccess()
    {
        var suite = SystemSuite.Create(ValidTenantId, ValidCode, ValidName, ValidDescription, ValidActor).Value;
        var key = ConfigurationKey.Create("MaxUsers");
        var value = ConfigurationValue.Create("100");
        suite.AddAppSetting(key, value, ConfigurationScope.Global, ValidActor);

        var result = suite.RemoveAppSetting(key, ValidActor);

        Assert.True(result.IsSuccess);
        Assert.Empty(suite.AppSettings);
    }

    [Fact]
    public void RemoveAppSetting_WhenKeyNotFound_ReturnsFailure()
    {
        var suite = SystemSuite.Create(ValidTenantId, ValidCode, ValidName, ValidDescription, ValidActor).Value;
        var key = ConfigurationKey.Create("NonExistent");

        var result = suite.RemoveAppSetting(key, ValidActor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.SystemSuite.ConfigurationKeyNotFound, result.Error);
    }

    #endregion

    #region RegisterAction

    [Fact]
    public void RegisterAction_WithValidData_ReturnsSuccess()
    {
        var suite = SystemSuite.Create(ValidTenantId, ValidCode, ValidName, ValidDescription, ValidActor).Value;
        var actionCode = ActionCode.Create("Read");
        var actionName = Name.Create("Read Access");

        var result = suite.RegisterAction(actionCode, actionName, ValidActor);

        Assert.True(result.IsSuccess);
        Assert.Single(suite.Actions);
    }

    [Fact]
    public void RegisterAction_WithDuplicateCode_ReturnsFailure()
    {
        var suite = SystemSuite.Create(ValidTenantId, ValidCode, ValidName, ValidDescription, ValidActor).Value;
        var actionCode = ActionCode.Create("Read");
        var actionName = Name.Create("Read Access");
        suite.RegisterAction(actionCode, actionName, ValidActor);

        var result = suite.RegisterAction(actionCode, Name.Create("Read Again"), ValidActor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Common.Duplicate, result.Error);
    }

    [Fact]
    public void RegisterAction_RaisesSystemSuiteActionRegisteredEvent()
    {
        var suite = SystemSuite.Create(ValidTenantId, ValidCode, ValidName, ValidDescription, ValidActor).Value;
        var actionCode = ActionCode.Create("Read");
        var actionName = Name.Create("Read Access");

        suite.RegisterAction(actionCode, actionName, ValidActor);

        var events = suite.DomainEvents.GetUncommittedChanges().ToList();
        Assert.Contains(events, e => e is SystemSuiteActionRegisteredEvent);
    }

    #endregion

    #region RemoveAction

    [Fact]
    public void RemoveAction_WhenActionExists_ReturnsSuccess()
    {
        var suite = SystemSuite.Create(ValidTenantId, ValidCode, ValidName, ValidDescription, ValidActor).Value;
        var actionCode = ActionCode.Create("Read");
        var actionName = Name.Create("Read Access");
        suite.RegisterAction(actionCode, actionName, ValidActor);

        var result = suite.RemoveAction(actionCode, ValidActor);

        Assert.True(result.IsSuccess);
        Assert.Empty(suite.Actions);
    }

    [Fact]
    public void RemoveAction_WhenActionNotFound_ReturnsFailure()
    {
        var suite = SystemSuite.Create(ValidTenantId, ValidCode, ValidName, ValidDescription, ValidActor).Value;
        var actionCode = ActionCode.Create("NonExistent");

        var result = suite.RemoveAction(actionCode, ValidActor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Common.NotFound, result.Error);
    }

    [Fact]
    public void RemoveAction_RaisesSystemSuiteActionRemovedEvent()
    {
        var suite = SystemSuite.Create(ValidTenantId, ValidCode, ValidName, ValidDescription, ValidActor).Value;
        var actionCode = ActionCode.Create("Read");
        var actionName = Name.Create("Read Access");
        suite.RegisterAction(actionCode, actionName, ValidActor);

        suite.RemoveAction(actionCode, ValidActor);

        var events = suite.DomainEvents.GetUncommittedChanges().ToList();
        Assert.Contains(events, e => e is SystemSuiteActionRemovedEvent);
    }

    #region Domain Resources
    [Fact]
    public void AddDomainResource_WithValidData_ReturnsSuccess()
    {
        var suite = SystemSuite.Create(
            TenantId.Create(),
            Code.Create("TEST"),
            Name.Create("Test Suite"),
            Description.Create("Test"),
            ValidActor).Value;

        var result = suite.AddDomainResource(
            null,
            null,
            DomainResourceType.Aggregate,
            Code.Create("INVOICE"),
            Name.Create("Invoice Aggregate"),
            Description.Create("Manage invoices"),
            ValidActor);

        Assert.True(result.IsSuccess);
        Assert.Single(suite.DomainResources);
    }

    [Fact]
    public void RemoveDomainResource_WhenResourceExists_ReturnsSuccess()
    {
        var suite = SystemSuite.Create(TenantId.Create(), Code.Create("TEST"), Name.Create("Suite"), Description.Create("Desc"), ValidActor).Value;
        suite.AddDomainResource(null, null, DomainResourceType.Aggregate, Code.Create("INV"), Name.Create("Invoice"), Description.Create("Desc"), ValidActor);
        var resource = suite.DomainResources.First();

        var result = suite.RemoveDomainResource(resource.GetId(), ValidActor);

        Assert.True(result.IsSuccess);
        Assert.Empty(suite.DomainResources);
    }

    [Fact]
    public void RemoveDomainResource_WhenTemplateItemsRemain_ReturnsFailure()
    {
        var suite = SystemSuite.Create(TenantId.Create(), Code.Create("TEST"), Name.Create("Suite"), Description.Create("Desc"), ValidActor).Value;
        suite.AddDomainResource(null, null, DomainResourceType.Aggregate, Code.Create("INV"), Name.Create("Invoice"), Description.Create("Desc"), ValidActor);
        var resource = suite.DomainResources.First();

        var result = suite.RemoveDomainResource(resource.GetId(), ValidActor, templateItemCount: 1);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Authorization.DomainResourceHasTemplateItems, result.Error);
    }

    [Fact]
    public void AddDomainResource_DomainMethodWithoutParent_ReturnsBrokenRule()
    {
        var suite = SystemSuite.Create(TenantId.Create(), Code.Create("TEST"), Name.Create("Suite"), Description.Create("Desc"), ValidActor).Value;

        var result = suite.AddDomainResource(
            null,
            null,
            DomainResourceType.DomainMethod,
            Code.Create("RESET_PWD"),
            Name.Create("ResetPassword()"),
            Description.Create("Resets user password"),
            ValidActor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Authorization.DomainMethodRequiresParent, result.Error);
    }

    [Fact]
    public void AddDomainResource_DomainMethodWithDomainMethodParent_ReturnsBrokenRule()
    {
        var suite = SystemSuite.Create(TenantId.Create(), Code.Create("TEST"), Name.Create("Suite"), Description.Create("Desc"), ValidActor).Value;

        // Add an Aggregate first, then add a DomainMethod as child
        suite.AddDomainResource(null, null, DomainResourceType.Aggregate, Code.Create("USERS"), Name.Create("Users"), Description.Create("Desc"), ValidActor);
        var aggregate = suite.DomainResources.First();

        suite.AddDomainResource(null, aggregate.Props.Id, DomainResourceType.DomainMethod, Code.Create("RESET_PWD"), Name.Create("ResetPassword()"), Description.Create("Desc"), ValidActor);
        var domainMethod = suite.DomainResources.Last();

        // Now try to add a DomainMethod with another DomainMethod as parent
        var result = suite.AddDomainResource(
            null,
            domainMethod.Props.Id,
            DomainResourceType.DomainMethod,
            Code.Create("NESTED_METHOD"),
            Name.Create("Nested Method"),
            Description.Create("Invalid nesting"),
            ValidActor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Authorization.DomainMethodCannotBeParent, result.Error);
    }

    [Fact]
    public void AddDomainResource_DomainMethodWithValidParent_Succeeds()
    {
        var suite = SystemSuite.Create(TenantId.Create(), Code.Create("TEST"), Name.Create("Suite"), Description.Create("Desc"), ValidActor).Value;
        suite.AddDomainResource(null, null, DomainResourceType.Aggregate, Code.Create("USERS"), Name.Create("Users"), Description.Create("Desc"), ValidActor);
        var aggregate = suite.DomainResources.First();

        var result = suite.AddDomainResource(
            null,
            aggregate.Props.Id,
            DomainResourceType.DomainMethod,
            Code.Create("RESET_PWD"),
            Name.Create("ResetPassword()"),
            Description.Create("Resets user password through the aggregate"),
            ValidActor);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, suite.DomainResources.Count);
        Assert.Equal(aggregate.Props.Id.GetValue(), suite.DomainResources.Last().Props.ParentResourceId?.GetValue());
    }

    [Fact]
    public void AddDomainResource_EntityWithValidAggregateParent_Succeeds()
    {
        var suite = SystemSuite.Create(TenantId.Create(), Code.Create("TEST"), Name.Create("Suite"), Description.Create("Desc"), ValidActor).Value;
        suite.AddDomainResource(null, null, DomainResourceType.Aggregate, Code.Create("ORDERS"), Name.Create("Orders"), Description.Create("Desc"), ValidActor);
        var aggregate = suite.DomainResources.First();

        var result = suite.AddDomainResource(
            null,
            aggregate.Props.Id,
            DomainResourceType.Entity,
            Code.Create("ORDER_LINE"),
            Name.Create("OrderLine"),
            Description.Create("Line item within an order"),
            ValidActor);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, suite.DomainResources.Count);
        Assert.Equal(aggregate.Props.Id.GetValue(), suite.DomainResources.Last().Props.ParentResourceId?.GetValue());
    }
    #endregion

    #endregion
}
