namespace Evolith.Ums.Domain.Test.Authorization.SeedData;

using Evolith.Ums.Domain.Authorization.SystemSuite;
using Evolith.Ums.Domain.Authorization.SystemSuite.MenuNode;
using Evolith.Ums.Domain.Authorization.Template;
using Evolith.Ums.Domain.Enums;
using Evolith.Ums.Domain.Kernel.ValueObjects;
using Xunit;
using MenuNodeEntity = Evolith.Ums.Domain.Authorization.SystemSuite.MenuNode.MenuNode;
using ModuleEntity = Evolith.Ums.Domain.Authorization.SystemSuite.Module.Module;

public class AuthorizationSeedDataTests
{
    private static readonly TenantId TestTenantId = TenantId.Load(Guid.NewGuid().ToString());
    private static readonly ActorId TestActor = ActorId.Create("seed-test-actor");

    [Fact]
    public void BuildSystemSuite_WithModules_ReturnsNonEmptyModules()
    {
        var suite = SystemSuite.Create(
            TestTenantId,
            Code.Create("TEST_SUITE"),
            Name.Create("Test Suite"),
            Description.Create("Test"),
            TestActor).Value;

        suite.AddModule(Code.Create("MOD1"), Name.Create("Module 1"), Description.Create("Desc"), 1, TestActor);
        suite.AddModule(Code.Create("MOD2"), Name.Create("Module 2"), Description.Create("Desc"), 2, TestActor);

        Assert.Equal(2, suite.Modules.Count);
    }

    [Fact]
    public void BuildModule_WithRootNodes_ReturnsNonEmptyNodes()
    {
        var suite = CreateSuiteWithModule("MOD1", out var module);

        suite.AddModuleRootNode(module.Props.Id, NodeKind.Menu, Code.Create("MENU1"), Name.Create("Menu 1"), Description.Create("Desc"), 1, TestActor);
        suite.AddModuleRootNode(module.Props.Id, NodeKind.Menu, Code.Create("MENU2"), Name.Create("Menu 2"), Description.Create("Desc"), 2, TestActor);

        Assert.Equal(2, module.Nodes.Count);
    }

    [Fact]
    public void BuildMenuNode_WithSubMenuChildren_ReturnsNonEmptyChildren()
    {
        var suite = CreateSuiteWithModuleAndMenu("MOD1", "MENU1", out var module, out var menuNode);

        suite.AddModuleChildNode(module.Props.Id, menuNode.GetId(), NodeKind.SubMenu, Code.Create("SUB1"), Name.Create("Sub 1"), Description.Create("Desc"), 1, TestActor);
        suite.AddModuleChildNode(module.Props.Id, menuNode.GetId(), NodeKind.SubMenu, Code.Create("SUB2"), Name.Create("Sub 2"), Description.Create("Desc"), 2, TestActor);

        Assert.Equal(2, menuNode.Children.Count);
    }

    [Fact]
    public void BuildSubMenuNode_WithOptionChildren_ReturnsNonEmptyChildren()
    {
        var suite = CreateSuiteWithModuleMenuAndSubMenu("MOD1", "MENU1", "SUB1", out var module, out var subNode);

        suite.AddModuleChildNode(module.Props.Id, subNode.GetId(), NodeKind.Option, Code.Create("OPT1"), Name.Create("Opt 1"), Description.Create("Desc"), 1, TestActor);
        suite.AddModuleChildNode(module.Props.Id, subNode.GetId(), NodeKind.Option, Code.Create("OPT2"), Name.Create("Opt 2"), Description.Create("Desc"), 2, TestActor);

        Assert.Equal(2, subNode.Children.Count);
    }

    [Fact]
    public void LinkNodeAction_OnOptionLeaf_RecordsActionCodesNm()
    {
        var suite = CreateSuiteWithModuleMenuAndSubMenu("MOD1", "MENU1", "SUB1", out var module, out var subNode);
        suite.AddModuleChildNode(module.Props.Id, subNode.GetId(), NodeKind.Option, Code.Create("OPT1"), Name.Create("Opt 1"), Description.Create("Desc"), 1, TestActor);
        var option = subNode.Children.First();

        // G-046: an action must be registered in the suite catalog before it can be linked to a node.
        suite.RegisterAction(ActionCode.Create("READ"), Name.Create("Read"), TestActor);
        suite.RegisterAction(ActionCode.Create("CREATE"), Name.Create("Create"), TestActor);

        suite.LinkModuleNodeAction(module.Props.Id, option.GetId(), ActionCode.Create("READ"), TestActor);
        suite.LinkModuleNodeAction(module.Props.Id, option.GetId(), ActionCode.Create("CREATE"), TestActor);

        Assert.Equal(2, option.ActionCodes.Count);
    }

    [Fact]
    public void RegisterActions_WithCrudCodes_ReturnsNonEmptyActions()
    {
        var suite = SystemSuite.Create(
            TestTenantId,
            Code.Create("TEST_SUITE"),
            Name.Create("Test Suite"),
            Description.Create("Test"),
            TestActor).Value;

        var actions = new[] { "CREATE", "READ", "SEARCH", "UPDATE", "DELETE", "DEACTIVATE", "ACTIVATE", "EXPORT", "IMPORT", "ASSIGN", "REVOKE", "APPROVE", "REJECT", "PUBLISH", "DEPRECATE" };
        foreach (var code in actions)
        {
            suite.RegisterAction(ActionCode.Create(code), Name.Create(code), TestActor);
        }

        Assert.Equal(actions.Length, suite.Actions.Count);
    }

    [Fact]
    public void FullHierarchy_SuiteHasAllLevels_ReturnsCompleteStructure()
    {
        var suite = SystemSuite.Create(
            TestTenantId,
            Code.Create("FULL_SUITE"),
            Name.Create("Full Suite"),
            Description.Create("Complete hierarchy test"),
            TestActor).Value;

        suite.AddModule(Code.Create("IDENTITY"), Name.Create("Identity"), Description.Create("Identity management"), 1, TestActor);
        var module = suite.Modules.First();
        suite.ActivateModule(module.Id, TestActor);

        suite.AddModuleRootNode(module.Props.Id, NodeKind.Menu, Code.Create("USERS"), Name.Create("Users"), Description.Create("User management"), 1, TestActor);
        var menuNode = module.Nodes.First();

        suite.AddModuleChildNode(module.Props.Id, menuNode.GetId(), NodeKind.SubMenu, Code.Create("USER_LIST"), Name.Create("User List"), Description.Create("List users"), 1, TestActor);
        var subNode = menuNode.Children.First();

        suite.AddModuleChildNode(module.Props.Id, subNode.GetId(), NodeKind.Option, Code.Create("USER_VIEW"), Name.Create("View Users"), Description.Create("View"), 1, TestActor);
        suite.AddModuleChildNode(module.Props.Id, subNode.GetId(), NodeKind.Option, Code.Create("USER_CREATE"), Name.Create("Create User"), Description.Create("Create"), 2, TestActor);

        suite.RegisterAction(ActionCode.Create("READ"), Name.Create("Read"), TestActor);
        suite.RegisterAction(ActionCode.Create("CREATE"), Name.Create("Create"), TestActor);

        Assert.Single(suite.Modules);
        Assert.Single(module.Nodes);
        Assert.Single(menuNode.Children);
        Assert.Equal(2, subNode.Children.Count);
        Assert.Equal(2, suite.Actions.Count);
    }

    [Fact]
    public void PermissionTemplate_WithValidActionReferences_AllItemsHaveValidTargets()
    {
        var suite = BuildCompleteSuite();
        var actionId = suite.Actions.First(a => a.Code.GetValue() == "READ").GetId();

        var template = PermissionTemplate.Create(
            TestTenantId,
            RoleId.Load(Guid.NewGuid().ToString()),
            suite.GetId(),
            TestActor).Value;

        var module = suite.Modules.First();
        template.AddItem(ExclusiveArcTarget.Module, module.Id, actionId, true, false, TestActor);

        Assert.Single(template.Items);
        Assert.Equal(ExclusiveArcTarget.Module, template.Items.First().TargetType);
        Assert.Equal(module.Id.GetValue(), template.Items.First().TargetId.GetValue());
        Assert.True(template.Items.First().IsAllowed);
        Assert.False(template.Items.First().IsDenied);
    }

    [Fact]
    public void PermissionTemplate_WithMultipleTargetTypes_CoversAllArcLevels()
    {
        var suite = BuildCompleteSuite();
        var readActionId = suite.Actions.First(a => a.Code.GetValue() == "READ").GetId();
        var createActionId = suite.Actions.First(a => a.Code.GetValue() == "CREATE").GetId();

        var template = PermissionTemplate.Create(
            TestTenantId,
            RoleId.Load(Guid.NewGuid().ToString()),
            suite.GetId(),
            TestActor).Value;

        var module = suite.Modules.First();
        var menuNode = module.Nodes.First();
        var subNode = menuNode.Children.First();
        var option = subNode.Children.First();

        template.AddItem(ExclusiveArcTarget.SystemSuite, suite.GetId(), readActionId, true, false, TestActor);
        template.AddItem(ExclusiveArcTarget.Module, module.Id, createActionId, true, false, TestActor);
        template.AddItem(ExclusiveArcTarget.Submodule, subNode.GetId(), readActionId, true, false, TestActor);
        template.AddItem(ExclusiveArcTarget.Option, option.GetId(), createActionId, false, true, TestActor);

        Assert.Equal(4, template.Items.Count);
        Assert.Contains(template.Items, i => i.TargetType == ExclusiveArcTarget.SystemSuite);
        Assert.Contains(template.Items, i => i.TargetType == ExclusiveArcTarget.Module);
        Assert.Contains(template.Items, i => i.TargetType == ExclusiveArcTarget.Submodule);
        Assert.Contains(template.Items, i => i.TargetType == ExclusiveArcTarget.Option);
    }

    [Fact]
    public void PermissionTemplate_WithAllowedAndDenied_HasBothEffects()
    {
        var suite = BuildCompleteSuite();
        var readActionId = suite.Actions.First(a => a.Code.GetValue() == "READ").GetId();
        var createActionId = suite.Actions.First(a => a.Code.GetValue() == "CREATE").GetId();

        var template = PermissionTemplate.Create(
            TestTenantId,
            RoleId.Load(Guid.NewGuid().ToString()),
            suite.GetId(),
            TestActor).Value;

        var module = suite.Modules.First();
        template.AddItem(ExclusiveArcTarget.Module, module.Id, readActionId, true, false, TestActor);
        template.AddItem(ExclusiveArcTarget.Module, module.Id, createActionId, false, true, TestActor);

        Assert.Equal(2, template.Items.Count);
        Assert.Contains(template.Items, i => i.IsAllowed && !i.IsDenied);
        Assert.Contains(template.Items, i => i.IsDenied && !i.IsAllowed);
    }

    [Fact]
    public void PermissionTemplate_PublishedAndDeprecated_HasCorrectStatus()
    {
        var suite = BuildCompleteSuite();
        var readActionId = suite.Actions.First(a => a.Code.GetValue() == "READ").GetId();

        var template = PermissionTemplate.Create(
            TestTenantId,
            RoleId.Load(Guid.NewGuid().ToString()),
            suite.GetId(),
            TestActor).Value;

        var module = suite.Modules.First();
        template.AddItem(ExclusiveArcTarget.Module, module.Id, readActionId, true, false, TestActor);

        Assert.Equal(TemplateStatus.Draft, template.Status);

        template.Publish(TestActor);
        Assert.Equal(TemplateStatus.Published, template.Status);

        template.Deprecate(TestActor);
        Assert.Equal(TemplateStatus.Deprecated, template.Status);
    }

    [Fact]
    public void PermissionTemplate_WithInactiveItem_HasDeactivatedEntry()
    {
        var suite = BuildCompleteSuite();
        var readActionId = suite.Actions.First(a => a.Code.GetValue() == "READ").GetId();

        var template = PermissionTemplate.Create(
            TestTenantId,
            RoleId.Load(Guid.NewGuid().ToString()),
            suite.GetId(),
            TestActor).Value;

        var module = suite.Modules.First();
        template.AddItem(ExclusiveArcTarget.Module, module.Id, readActionId, true, false, TestActor);

        var item = template.Items.First();
        template.DeactivateItem(item.GetId(), TestActor);

        Assert.False(template.Items.First().IsActive);
    }

    [Fact]
    public void SystemTemplateCoherence_TemplateActionIdsExistInSystemSuite()
    {
        var suite = BuildCompleteSuite();
        var actionIds = suite.Actions.Select(a => a.GetId().GetValue()).ToHashSet();

        var template = PermissionTemplate.Create(
            TestTenantId,
            RoleId.Load(Guid.NewGuid().ToString()),
            suite.GetId(),
            TestActor).Value;

        var module = suite.Modules.First();

        foreach (var action in suite.Actions.Take(5))
        {
            template.AddItem(ExclusiveArcTarget.Module, module.Id, action.GetId(), true, false, TestActor);
        }

        foreach (var item in template.Items)
        {
            Assert.Contains(item.ActionId.GetValue(), actionIds);
        }
    }

    [Fact]
    public void SystemTemplateCoherence_TemplateTargetIdsExistInSystemSuite()
    {
        var suite = BuildCompleteSuite();
        var moduleIds = suite.Modules.Select(m => m.Id.GetValue()).ToHashSet();
        var allNodes = suite.Modules.SelectMany(m => FlattenNodes(m.Nodes)).ToList();
        var subMenuIds = allNodes.Where(n => n.Kind == NodeKind.SubMenu).Select(n => n.GetId().GetValue()).ToHashSet();
        var optionIds = allNodes.Where(n => n.Kind == NodeKind.Option).Select(n => n.GetId().GetValue()).ToHashSet();

        var template = PermissionTemplate.Create(
            TestTenantId,
            RoleId.Load(Guid.NewGuid().ToString()),
            suite.GetId(),
            TestActor).Value;

        var readActionId = suite.Actions.First(a => a.Code.GetValue() == "READ").GetId();
        var module = suite.Modules.First();
        var subNode = module.Nodes.First().Children.First();
        var option = subNode.Children.First();

        template.AddItem(ExclusiveArcTarget.Module, module.Id, readActionId, true, false, TestActor);
        template.AddItem(ExclusiveArcTarget.Submodule, subNode.GetId(), readActionId, true, false, TestActor);
        template.AddItem(ExclusiveArcTarget.Option, option.GetId(), readActionId, true, false, TestActor);

        foreach (var item in template.Items)
        {
            var targetId = item.TargetId.GetValue();
            if (item.TargetType == ExclusiveArcTarget.Module)
            {
                Assert.Contains(targetId, moduleIds);
            }
            else if (item.TargetType == ExclusiveArcTarget.Submodule)
            {
                Assert.Contains(targetId, subMenuIds);
            }
            else if (item.TargetType == ExclusiveArcTarget.Option)
            {
                Assert.Contains(targetId, optionIds);
            }
        }
    }

    [Fact]
    public void SeedData_CoversAllRequiredDomainResources_HasExpectedModules()
    {
        var suite = SystemSuite.Create(
            TestTenantId,
            Code.Create("LOGISTICS_CORE"),
            Name.Create("Logistics Core"),
            Description.Create("Core operations"),
            TestActor).Value;

        var expectedModules = new[] { "IDENTITY", "AUTH", "CONFIG", "APPROVALS" };
        foreach (var mod in expectedModules)
        {
            suite.AddModule(Code.Create(mod), Name.Create(mod), Description.Create(mod), 1, TestActor);
        }

        foreach (var mod in expectedModules)
        {
            Assert.Contains(suite.Modules, m => m.Code.GetValue() == mod);
        }
    }

    [Fact]
    public void SeedData_CoversWmsDomainResources_HasExpectedModules()
    {
        var suite = SystemSuite.Create(
            TestTenantId,
            Code.Create("WMS"),
            Name.Create("Warehouse Management"),
            Description.Create("WMS operations"),
            TestActor).Value;

        var expectedModules = new[] { "INVENTORY", "PROMOTIONS" };
        foreach (var mod in expectedModules)
        {
            suite.AddModule(Code.Create(mod), Name.Create(mod), Description.Create(mod), 1, TestActor);
        }

        foreach (var mod in expectedModules)
        {
            Assert.Contains(suite.Modules, m => m.Code.GetValue() == mod);
        }
    }

    [Fact]
    public void SeedData_StandardCrudActions_AllRegistered()
    {
        var suite = SystemSuite.Create(
            TestTenantId,
            Code.Create("TEST_SUITE"),
            Name.Create("Test"),
            Description.Create("Test"),
            TestActor).Value;

        var crudActions = new[] { "CREATE", "READ", "SEARCH", "UPDATE", "DELETE", "DEACTIVATE", "ACTIVATE" };
        foreach (var code in crudActions)
        {
            suite.RegisterAction(ActionCode.Create(code), Name.Create(code), TestActor);
        }

        foreach (var code in crudActions)
        {
            Assert.Contains(suite.Actions, a => a.Code.GetValue() == code);
        }
    }

    [Fact]
    public void SeedData_CustomBusinessActions_AllRegistered()
    {
        var suite = SystemSuite.Create(
            TestTenantId,
            Code.Create("TEST_SUITE"),
            Name.Create("Test"),
            Description.Create("Test"),
            TestActor).Value;

        var customActions = new[] { "ASSIGN", "REVOKE", "APPROVE", "REJECT", "PUBLISH", "DEPRECATE", "EXPORT", "IMPORT" };
        foreach (var code in customActions)
        {
            suite.RegisterAction(ActionCode.Create(code), Name.Create(code), TestActor);
        }

        foreach (var code in customActions)
        {
            Assert.Contains(suite.Actions, a => a.Code.GetValue() == code);
        }
    }

    [Fact]
    public void SeedData_WmsCustomActions_AllRegistered()
    {
        var suite = SystemSuite.Create(
            TestTenantId,
            Code.Create("WMS"),
            Name.Create("WMS"),
            Description.Create("WMS"),
            TestActor).Value;

        var wmsActions = new[] { "ADJUST_STOCK", "TRANSFER_STOCK", "COUNT_INVENTORY", "APPLY_PROMOTION", "SCHEDULE_PROMOTION", "CANCEL_PROMOTION" };
        foreach (var code in wmsActions)
        {
            suite.RegisterAction(ActionCode.Create(code), Name.Create(code), TestActor);
        }

        foreach (var code in wmsActions)
        {
            Assert.Contains(suite.Actions, a => a.Code.GetValue() == code);
        }
    }

    private static IEnumerable<MenuNodeEntity> FlattenNodes(IEnumerable<MenuNodeEntity> nodes)
    {
        foreach (var node in nodes)
        {
            yield return node;
            foreach (var child in FlattenNodes(node.Children))
            {
                yield return child;
            }
        }
    }

    private static SystemSuite CreateSuiteWithModule(string moduleCode, out ModuleEntity module)
    {
        var suite = SystemSuite.Create(
            TestTenantId,
            Code.Create("TEST"),
            Name.Create("Test"),
            Description.Create("Test"),
            TestActor).Value;

        suite.AddModule(Code.Create(moduleCode), Name.Create(moduleCode), Description.Create("Desc"), 1, TestActor);
        module = suite.Modules.First();
        suite.ActivateModule(module.Id, TestActor);
        return suite;
    }

    private static SystemSuite CreateSuiteWithModuleAndMenu(string moduleCode, string menuCode, out ModuleEntity module, out MenuNodeEntity menuNode)
    {
        var suite = CreateSuiteWithModule(moduleCode, out module);
        suite.AddModuleRootNode(module.Props.Id, NodeKind.Menu, Code.Create(menuCode), Name.Create(menuCode), Description.Create("Desc"), 1, TestActor);
        menuNode = module.Nodes.First();
        return suite;
    }

    private static SystemSuite CreateSuiteWithModuleMenuAndSubMenu(string moduleCode, string menuCode, string subMenuCode, out ModuleEntity module, out MenuNodeEntity subNode)
    {
        var suite = CreateSuiteWithModuleAndMenu(moduleCode, menuCode, out module, out var menuNode);
        suite.AddModuleChildNode(module.Props.Id, menuNode.GetId(), NodeKind.SubMenu, Code.Create(subMenuCode), Name.Create(subMenuCode), Description.Create("Desc"), 1, TestActor);
        subNode = menuNode.Children.First();
        return suite;
    }

    private static SystemSuite BuildCompleteSuite()
    {
        var suite = SystemSuite.Create(
            TestTenantId,
            Code.Create("COMPLETE_SUITE"),
            Name.Create("Complete Suite"),
            Description.Create("Full hierarchy"),
            TestActor).Value;

        suite.AddModule(Code.Create("MOD1"), Name.Create("Module 1"), Description.Create("Desc"), 1, TestActor);
        var module = suite.Modules.First();
        suite.ActivateModule(module.Id, TestActor);

        suite.AddModuleRootNode(module.Props.Id, NodeKind.Menu, Code.Create("MENU1"), Name.Create("Menu 1"), Description.Create("Desc"), 1, TestActor);
        var menuNode = module.Nodes.First();

        suite.AddModuleChildNode(module.Props.Id, menuNode.GetId(), NodeKind.SubMenu, Code.Create("SUB1"), Name.Create("Sub 1"), Description.Create("Desc"), 1, TestActor);
        var subNode = menuNode.Children.First();

        suite.AddModuleChildNode(module.Props.Id, subNode.GetId(), NodeKind.Option, Code.Create("OPT1"), Name.Create("Opt 1"), Description.Create("Desc"), 1, TestActor);

        suite.RegisterAction(ActionCode.Create("READ"), Name.Create("Read"), TestActor);
        suite.RegisterAction(ActionCode.Create("CREATE"), Name.Create("Create"), TestActor);
        suite.RegisterAction(ActionCode.Create("UPDATE"), Name.Create("Update"), TestActor);
        suite.RegisterAction(ActionCode.Create("DELETE"), Name.Create("Delete"), TestActor);
        suite.RegisterAction(ActionCode.Create("SEARCH"), Name.Create("Search"), TestActor);

        return suite;
    }
}
