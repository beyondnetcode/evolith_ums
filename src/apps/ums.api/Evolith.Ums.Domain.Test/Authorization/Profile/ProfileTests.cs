namespace Evolith.Ums.Domain.Test.Authorization.Profile;

using Evolith.Ums.Domain.Authorization.Profile;
using Evolith.Ums.Domain.Authorization.Template;
using Evolith.Ums.Domain.Authorization.Template.PermissionTemplateItem;
using Xunit;

public class ProfileTests
{
    private static readonly TenantId ValidTenantId = TenantId.Load(Guid.NewGuid().ToString());
    private static readonly UserId ValidUserId = UserId.Load(Guid.NewGuid().ToString());
    private static readonly RoleId ValidRoleId = RoleId.Load(Guid.NewGuid().ToString());
    private static readonly BranchId? ValidBranchId = BranchId.Load(Guid.NewGuid().ToString());
    private static readonly ActorId ValidActor = ActorId.Create("user-001");
    #region Create

    [Fact]
    public void Create_WithBranchId_ReturnsSuccessWithBranchScoped()
    {
        var result = Profile.Create(ValidTenantId, ValidUserId, ValidRoleId, ValidBranchId, ValidActor);

        Assert.True(result.IsSuccess);
        Assert.Equal(ValidTenantId, result.Value.TenantId);
        Assert.Equal(ValidUserId, result.Value.UserId);
        Assert.Equal(ValidRoleId, result.Value.RoleId);
        Assert.Equal(ValidBranchId, result.Value.BranchId);
        Assert.Equal(ProfileScope.BranchScoped, result.Value.Scope);
        Assert.True(result.Value.IsActive);
        Assert.Empty(result.Value.Permissions);
    }

    [Fact]
    public void Create_WithoutBranchId_ReturnsSuccessWithOrgWide()
    {
        var result = Profile.Create(ValidTenantId, ValidUserId, ValidRoleId, null, ValidActor);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.BranchId);
        Assert.Equal(ProfileScope.OrgWide, result.Value.Scope);
    }

    [Fact]
    public void Create_RaisesProfileCreatedEvent()
    {
        var result = Profile.Create(ValidTenantId, ValidUserId, ValidRoleId, ValidBranchId, ValidActor);

        Assert.True(result.IsSuccess);
        var events = result.Value.DomainEvents.GetUncommittedChanges().ToList();
        Assert.Single(events);
        Assert.IsType<ProfileCreatedEvent>(events[0]);
    }

    #endregion

    #region Deactivate

    [Fact]
    public void Deactivate_WhenActive_ReturnsSuccess()
    {
        var profile = Profile.Create(ValidTenantId, ValidUserId, ValidRoleId, ValidBranchId, ValidActor).Value;

        var result = profile.Deactivate(ValidActor);

        Assert.True(result.IsSuccess);
        Assert.False(profile.IsActive);
    }

    [Fact]
    public void Deactivate_WhenAlreadyInactive_ReturnsFailure()
    {
        var profile = Profile.Create(ValidTenantId, ValidUserId, ValidRoleId, ValidBranchId, ValidActor).Value;
        profile.Deactivate(ValidActor);

        var result = profile.Deactivate(ValidActor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Authorization.ProfileAlreadyInactive, result.Error);
    }

    [Fact]
    public void Deactivate_RaisesProfileDeactivatedEvent()
    {
        var profile = Profile.Create(ValidTenantId, ValidUserId, ValidRoleId, ValidBranchId, ValidActor).Value;

        profile.Deactivate(ValidActor);

        var events = profile.DomainEvents.GetUncommittedChanges().ToList();
        Assert.Contains(events, e => e is ProfileDeactivatedEvent);
    }

    #endregion

    #region Activate

    [Fact]
    public void Activate_WhenInactive_ReturnsSuccess()
    {
        var profile = Profile.Create(ValidTenantId, ValidUserId, ValidRoleId, ValidBranchId, ValidActor).Value;
        profile.Deactivate(ValidActor);

        var result = profile.Activate(ValidActor);

        Assert.True(result.IsSuccess);
        Assert.True(profile.IsActive);
    }

    [Fact]
    public void Activate_WhenAlreadyActive_ReturnsFailure()
    {
        var profile = Profile.Create(ValidTenantId, ValidUserId, ValidRoleId, ValidBranchId, ValidActor).Value;

        var result = profile.Activate(ValidActor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Authorization.ProfileAlreadyActive, result.Error);
    }

    [Fact]
    public void Activate_RaisesProfileActivatedEvent()
    {
        var profile = Profile.Create(ValidTenantId, ValidUserId, ValidRoleId, ValidBranchId, ValidActor).Value;
        profile.Deactivate(ValidActor);

        profile.Activate(ValidActor);

        var events = profile.DomainEvents.GetUncommittedChanges().ToList();
        Assert.Contains(events, e => e is ProfileActivatedEvent);
    }

    #endregion

    #region AssignTemplate

    [Fact]
    public void AssignTemplate_WithValidTemplate_ReturnsSuccess()
    {
        var profile = Profile.Create(ValidTenantId, ValidUserId, ValidRoleId, ValidBranchId, ValidActor).Value;
        var template = PermissionTemplate.Create(ValidTenantId, ValidRoleId, SystemSuiteId.Load(Guid.NewGuid().ToString()), ValidActor).Value;
        template.AddItem(ExclusiveArcTarget.Module, IdValueObject.Create(), ActionId.Load(Guid.NewGuid().ToString()), true, false, ValidActor);
        template.Publish(ValidActor);

        var result = profile.AssignTemplate(template, ValidActor);

        Assert.True(result.IsSuccess);
        Assert.NotEmpty(profile.Permissions);
    }

    [Fact]
    public void AssignTemplate_NoMaterializaLosItemsRetirados()
    {
        // ADR-0164, la mitad que de verdad importa: retirar dejó de borrar la fila, así que
        // `template.Items` ahora incluye lo retirado. Si la materialización siguiera leyéndola, la
        // concesión retirada se copiaría al perfil como permiso ACTIVO y el grafo concedería por
        // ella. Habríamos cambiado un borrado por una brecha.
        var profile = Profile.Create(ValidTenantId, ValidUserId, ValidRoleId, ValidBranchId, ValidActor).Value;
        var template = PermissionTemplate.Create(ValidTenantId, ValidRoleId, SystemSuiteId.Load(Guid.NewGuid().ToString()), ValidActor).Value;

        var vigente = IdValueObject.Create();
        var retirado = IdValueObject.Create();
        template.AddItem(ExclusiveArcTarget.Module, vigente, ActionId.Load(Guid.NewGuid().ToString()), true, false, ValidActor);
        template.AddItem(ExclusiveArcTarget.Module, retirado, ActionId.Load(Guid.NewGuid().ToString()), true, false, ValidActor);

        var itemRetirado = template.Items.Single(i => i.TargetId.GetValue() == retirado.GetValue());
        Assert.True(template.DeactivateItem(itemRetirado.GetId(), ValidActor).IsSuccess);
        Assert.True(template.Publish(ValidActor).IsSuccess);

        var result = profile.AssignTemplate(template, ValidActor);

        Assert.True(result.IsSuccess);
        // La plantilla conserva las dos filas; el perfil recibe UNA sola concesión.
        Assert.Equal(2, template.Items.Count);
        Assert.Single(profile.Permissions);
        Assert.Equal(vigente.GetValue(), profile.Permissions.Single().TargetId.GetValue());
        Assert.DoesNotContain(profile.Permissions, p => p.TargetId.GetValue() == retirado.GetValue());
    }

    [Fact]
    public void AssignTemplate_WhenProfileInactive_ReturnsFailure()
    {
        var profile = Profile.Create(ValidTenantId, ValidUserId, ValidRoleId, ValidBranchId, ValidActor).Value;
        profile.Deactivate(ValidActor);
        var template = PermissionTemplate.Create(ValidTenantId, ValidRoleId, SystemSuiteId.Load(Guid.NewGuid().ToString()), ValidActor).Value;
        template.Publish(ValidActor);

        var result = profile.AssignTemplate(template, ValidActor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Authorization.ProfileAlreadyInactive, result.Error);
    }

    [Fact]
    public void AssignTemplate_WhenTenantMismatch_ReturnsFailure()
    {
        var profile = Profile.Create(ValidTenantId, ValidUserId, ValidRoleId, ValidBranchId, ValidActor).Value;
        var otherTenantId = TenantId.Load(Guid.NewGuid().ToString());
        var template = PermissionTemplate.Create(otherTenantId, ValidRoleId, SystemSuiteId.Load(Guid.NewGuid().ToString()), ValidActor).Value;
        template.Publish(ValidActor);

        var result = profile.AssignTemplate(template, ValidActor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Authorization.TemplateTenantMismatch, result.Error);
    }

    [Fact]
    public void AssignTemplate_WhenTemplateNotPublished_ReturnsFailure()
    {
        var profile = Profile.Create(ValidTenantId, ValidUserId, ValidRoleId, ValidBranchId, ValidActor).Value;
        var template = PermissionTemplate.Create(ValidTenantId, ValidRoleId, SystemSuiteId.Load(Guid.NewGuid().ToString()), ValidActor).Value;

        var result = profile.AssignTemplate(template, ValidActor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Authorization.TemplateNotPublishedForProfile, result.Error);
    }

    [Fact]
    public void AssignTemplate_WhenAlreadyLinked_ReturnsFailure()
    {
        var profile = Profile.Create(ValidTenantId, ValidUserId, ValidRoleId, ValidBranchId, ValidActor).Value;
        var template = PermissionTemplate.Create(ValidTenantId, ValidRoleId, SystemSuiteId.Load(Guid.NewGuid().ToString()), ValidActor).Value;
        template.AddItem(ExclusiveArcTarget.Module, IdValueObject.Create(), ActionId.Load(Guid.NewGuid().ToString()), true, false, ValidActor);
        template.Publish(ValidActor);
        profile.AssignTemplate(template, ValidActor);

        var result = profile.AssignTemplate(template, ValidActor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Authorization.ProfileTemplateAlreadyLinked, result.Error);
    }

    [Fact]
    public void AssignTemplate_RaisesTemplateLinkedToProfileEvent()
    {
        var profile = Profile.Create(ValidTenantId, ValidUserId, ValidRoleId, ValidBranchId, ValidActor).Value;
        var template = PermissionTemplate.Create(ValidTenantId, ValidRoleId, SystemSuiteId.Load(Guid.NewGuid().ToString()), ValidActor).Value;
        template.AddItem(ExclusiveArcTarget.Module, IdValueObject.Create(), ActionId.Load(Guid.NewGuid().ToString()), true, false, ValidActor);
        template.Publish(ValidActor);

        profile.AssignTemplate(template, ValidActor);

        var events = profile.DomainEvents.GetUncommittedChanges().ToList();
        Assert.Contains(events, e => e is TemplateLinkedToProfileEvent);
    }

    [Fact]
    public void Deactivate_CascadesToAllActivePermissions()
    {
        var profile = Profile.Create(ValidTenantId, ValidUserId, ValidRoleId, ValidBranchId, ValidActor).Value;
        var template = PermissionTemplate.Create(ValidTenantId, ValidRoleId, SystemSuiteId.Load(Guid.NewGuid().ToString()), ValidActor).Value;
        template.AddItem(ExclusiveArcTarget.Module, IdValueObject.Create(), ActionId.Load(Guid.NewGuid().ToString()), true, false, ValidActor);
        template.AddItem(ExclusiveArcTarget.Module, IdValueObject.Create(), ActionId.Load(Guid.NewGuid().ToString()), false, true, ValidActor);
        template.Publish(ValidActor);

        var assignResult = profile.AssignTemplate(template, ValidActor);

        Assert.True(assignResult.IsSuccess);
        Assert.All(profile.Permissions, permission => Assert.True(permission.IsActive));

        var deactivateResult = profile.Deactivate(ValidActor);

        Assert.True(deactivateResult.IsSuccess);
        Assert.All(profile.Permissions, permission => Assert.False(permission.IsActive));
    }

    #endregion

    #region UnassignTemplate (ADR-UMS-106: desligar es una operación del modelo)

    [Fact]
    public void UnassignTemplate_DesactivaLoQueLaPlantillaConcedia()
    {
        // Lo que faltaba en el modelo: un perfil no podía devolverse a una sola versión de su
        // plantilla salvo desactivándolo y creando otro ([[G-272]]).
        var profile = Profile.Create(ValidTenantId, ValidUserId, ValidRoleId, ValidBranchId, ValidActor).Value;
        var template = PermissionTemplate.Create(ValidTenantId, ValidRoleId, SystemSuiteId.Load(Guid.NewGuid().ToString()), ValidActor).Value;
        template.AddItem(ExclusiveArcTarget.Module, IdValueObject.Create(), ActionId.Load(Guid.NewGuid().ToString()), true, false, ValidActor);
        template.AddItem(ExclusiveArcTarget.Module, IdValueObject.Create(), ActionId.Load(Guid.NewGuid().ToString()), true, false, ValidActor);
        template.Publish(ValidActor);
        Assert.True(profile.AssignTemplate(template, ValidActor).IsSuccess);

        var result = profile.UnassignTemplate(TemplateId.Load(template.GetId().GetValue()), ValidActor);

        Assert.True(result.IsSuccess);
        // Se DESACTIVA, no se borra: el grafo ignora lo inactivo y el rastro de que existió permanece.
        Assert.Equal(2, profile.Permissions.Count);
        Assert.All(profile.Permissions, p => Assert.False(p.IsActive));
    }

    [Fact]
    public void UnassignTemplate_CuandoNoEstaEnlazada_ReturnsFailure()
    {
        var profile = Profile.Create(ValidTenantId, ValidUserId, ValidRoleId, ValidBranchId, ValidActor).Value;

        var result = profile.UnassignTemplate(TemplateId.Load(Guid.NewGuid()), ValidActor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Authorization.ProfileTemplateNotLinked, result.Error);
    }

    [Fact]
    public void UnassignTemplate_RaisesTemplateUnlinkedFromProfileEvent()
    {
        var profile = Profile.Create(ValidTenantId, ValidUserId, ValidRoleId, ValidBranchId, ValidActor).Value;
        var template = PermissionTemplate.Create(ValidTenantId, ValidRoleId, SystemSuiteId.Load(Guid.NewGuid().ToString()), ValidActor).Value;
        template.AddItem(ExclusiveArcTarget.Module, IdValueObject.Create(), ActionId.Load(Guid.NewGuid().ToString()), true, false, ValidActor);
        template.Publish(ValidActor);
        profile.AssignTemplate(template, ValidActor);

        profile.UnassignTemplate(TemplateId.Load(template.GetId().GetValue()), ValidActor);

        var events = profile.DomainEvents.GetUncommittedChanges().ToList();
        Assert.Contains(events, e => e is TemplateUnlinkedFromProfileEvent);
    }

    [Fact]
    public void AssignTemplate_TrasDesligar_VuelveAMaterializar()
    {
        // ADR-UMS-106 D5: la guarda de «ya enlazada» mira solo lo VIVO. Sin eso, desligar y volver a
        // asignar —el flujo con el que se corrige una plantilla mal publicada— quedaría bloqueado
        // por materializaciones que ya no conceden nada.
        var profile = Profile.Create(ValidTenantId, ValidUserId, ValidRoleId, ValidBranchId, ValidActor).Value;
        var template = PermissionTemplate.Create(ValidTenantId, ValidRoleId, SystemSuiteId.Load(Guid.NewGuid().ToString()), ValidActor).Value;
        template.AddItem(ExclusiveArcTarget.Module, IdValueObject.Create(), ActionId.Load(Guid.NewGuid().ToString()), true, false, ValidActor);
        template.Publish(ValidActor);
        profile.AssignTemplate(template, ValidActor);
        profile.UnassignTemplate(TemplateId.Load(template.GetId().GetValue()), ValidActor);

        var result = profile.AssignTemplate(template, ValidActor);

        Assert.True(result.IsSuccess);
        Assert.Single(profile.Permissions, p => p.IsActive);
    }

    #endregion

    #region ChangeRole (ADR-UMS-096: efecto de la promoción de rol IGA)

    [Fact]
    public void ChangeRole_WhenActiveAndDifferentRole_ReturnsSuccessAndReassigns()
    {
        var profile = Profile.Create(ValidTenantId, ValidUserId, ValidRoleId, ValidBranchId, ValidActor).Value;
        var newRoleId = RoleId.Load(Guid.NewGuid().ToString());

        var result = profile.ChangeRole(newRoleId, ValidActor);

        Assert.True(result.IsSuccess);
        Assert.Equal(newRoleId.GetValue(), profile.RoleId.GetValue());
    }

    [Fact]
    public void ChangeRole_RaisesProfileRoleChangedEvent()
    {
        var profile = Profile.Create(ValidTenantId, ValidUserId, ValidRoleId, ValidBranchId, ValidActor).Value;
        var newRoleId = RoleId.Load(Guid.NewGuid().ToString());

        profile.ChangeRole(newRoleId, ValidActor);

        var events = profile.DomainEvents.GetUncommittedChanges().ToList();
        var changed = Assert.IsType<ProfileRoleChangedEvent>(Assert.Single(events, e => e is ProfileRoleChangedEvent));
        Assert.Equal(ValidRoleId.GetValue(), changed.PreviousRoleId);
        Assert.Equal(newRoleId.GetValue(), changed.NewRoleId);
    }

    [Fact]
    public void ChangeRole_WhenSameRole_ReturnsFailure()
    {
        var profile = Profile.Create(ValidTenantId, ValidUserId, ValidRoleId, ValidBranchId, ValidActor).Value;

        var result = profile.ChangeRole(ValidRoleId, ValidActor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Authorization.ProfileRoleUnchanged, result.Error);
        Assert.Equal(ValidRoleId.GetValue(), profile.RoleId.GetValue());
    }

    [Fact]
    public void ChangeRole_WhenProfileInactive_ReturnsFailure()
    {
        var profile = Profile.Create(ValidTenantId, ValidUserId, ValidRoleId, ValidBranchId, ValidActor).Value;
        profile.Deactivate(ValidActor);
        var newRoleId = RoleId.Load(Guid.NewGuid().ToString());

        var result = profile.ChangeRole(newRoleId, ValidActor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Authorization.ProfileAlreadyInactive, result.Error);
    }

    #endregion

    #region Permission Overrides

    // AT06/F1 (regresión): el override de un permiso REAL debe resolverse por su identidad canónica
    // (Props.Id, la que expone el read-model vía GetId()). El Id base de Entity<> se regenera aleatorio
    // en construcción — distinto de Props.Id ya desde Create — y la rehidratación no llama SetId, así
    // que FindPermission buscando por el Id base devolvía PermissionNotFound (404 en la API: override/
    // activate/deactivate inalcanzables). Los tests previos solo cubrían casos negativos (id falso),
    // por eso el bug pasó. Este test ejercita el camino positivo con el id canónico del permiso.
    [Fact]
    public void OverridePermissionDeny_WithCanonicalPermissionId_Succeeds()
    {
        var profile = Profile.Create(ValidTenantId, ValidUserId, ValidRoleId, ValidBranchId, ValidActor).Value;
        var template = PermissionTemplate.Create(ValidTenantId, ValidRoleId, SystemSuiteId.Load(Guid.NewGuid().ToString()), ValidActor).Value;
        template.AddItem(ExclusiveArcTarget.Module, IdValueObject.Create(), ActionId.Load(Guid.NewGuid().ToString()), true, false, ValidActor);
        template.Publish(ValidActor);
        profile.AssignTemplate(template, ValidActor);

        var permission = profile.Permissions.First();
        var canonicalId = permission.GetId();          // == Props.Id (lo que expone el DTO)

        // Documenta la causa raíz: el Id base diverge de la identidad canónica.
        Assert.NotEqual(permission.Id.GetValue(), canonicalId.GetValue());

        var result = profile.OverridePermissionDeny(canonicalId, ValidActor);

        Assert.True(result.IsSuccess);
        Assert.True(permission.IsDenied);
        Assert.True(permission.IsOverride);
    }

    [Fact]
    public void OverridePermissionAllow_WhenProfileInactive_ReturnsFailure()
    {
        var profile = Profile.Create(ValidTenantId, ValidUserId, ValidRoleId, ValidBranchId, ValidActor).Value;
        profile.Deactivate(ValidActor);
        var fakeId = IdValueObject.Create();

        var result = profile.OverridePermissionAllow(fakeId, ValidActor);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void OverridePermissionDeny_WhenProfileInactive_ReturnsFailure()
    {
        var profile = Profile.Create(ValidTenantId, ValidUserId, ValidRoleId, ValidBranchId, ValidActor).Value;
        profile.Deactivate(ValidActor);
        var fakeId = IdValueObject.Create();

        var result = profile.OverridePermissionDeny(fakeId, ValidActor);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void OverridePermissionNeutral_WhenProfileInactive_ReturnsFailure()
    {
        var profile = Profile.Create(ValidTenantId, ValidUserId, ValidRoleId, ValidBranchId, ValidActor).Value;
        profile.Deactivate(ValidActor);
        var fakeId = IdValueObject.Create();

        var result = profile.OverridePermissionNeutral(fakeId, ValidActor);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void OverridePermissionAllow_WhenPermissionNotFound_ReturnsFailure()
    {
        var profile = Profile.Create(ValidTenantId, ValidUserId, ValidRoleId, ValidBranchId, ValidActor).Value;
        var fakeId = IdValueObject.Create();

        var result = profile.OverridePermissionAllow(fakeId, ValidActor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Authorization.PermissionNotFound, result.Error);
    }

    [Fact]
    public void DeactivatePermission_WhenPermissionNotFound_ReturnsFailure()
    {
        var profile = Profile.Create(ValidTenantId, ValidUserId, ValidRoleId, ValidBranchId, ValidActor).Value;
        var fakeId = IdValueObject.Create();

        var result = profile.DeactivatePermission(fakeId, ValidActor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Authorization.PermissionNotFound, result.Error);
    }

    [Fact]
    public void ActivatePermission_WhenPermissionNotFound_ReturnsFailure()
    {
        var profile = Profile.Create(ValidTenantId, ValidUserId, ValidRoleId, ValidBranchId, ValidActor).Value;
        var fakeId = IdValueObject.Create();

        var result = profile.ActivatePermission(fakeId, ValidActor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Authorization.PermissionNotFound, result.Error);
    }

    #endregion
}
