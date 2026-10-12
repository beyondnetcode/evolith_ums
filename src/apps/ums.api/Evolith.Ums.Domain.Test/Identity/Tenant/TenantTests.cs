#pragma warning disable S1144
﻿namespace Evolith.Ums.Domain.Test.Identity.Tenant;

using Evolith.Ums.Domain.Identity.Tenant;
using Xunit;

public class TenantTests
{
    private static readonly Code ValidCode = Code.Create("TEST-001");
    private static readonly Name ValidName = Name.Create("Test Tenant");
    private static readonly ActorId ValidActor = ActorId.Create("user-001");
    private static readonly OrganizationType ValidType = OrganizationType.INTERNAL;
    private static readonly Description ValidDescription = Description.Create("Test IdP");

    #region Create

    [Fact]
    public void Create_WithValidData_ReturnsSuccess()
    {
        var result = Tenant.Create(ValidCode, ValidName, ValidType, ValidActor);

        Assert.True(result.IsSuccess);
        Assert.Equal(ValidCode, result.Value.Code);
        Assert.Equal(ValidName, result.Value.Name);
        Assert.Equal(ValidType, result.Value.Type);
        Assert.Equal(TenantStatus.Active, result.Value.Status);
        Assert.Empty(result.Value.Branches);
    }

    [Fact]
    public void Create_WithEmptyCode_ReturnsFailure()
    {
        var code = Code.Create("");

        var result = Tenant.Create(code, ValidName, ValidType, ValidActor);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Create_WithEmptyName_ReturnsFailure()
    {
        var name = Name.Create("");

        var result = Tenant.Create(ValidCode, name, ValidType, ValidActor);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Create_RaisesTenantCreatedEvent()
    {
        var result = Tenant.Create(ValidCode, ValidName, ValidType, ValidActor);

        Assert.True(result.IsSuccess);
        var events = result.Value.DomainEvents.GetUncommittedChanges().ToList();
        Assert.Single(events);
        Assert.IsType<TenantCreatedEvent>(events[0]);
    }

    #endregion

    #region AddBranch

    [Fact]
    public void AddBranch_WithValidData_ReturnsSuccess()
    {
        var tenant = Tenant.Create(ValidCode, ValidName, ValidType, ValidActor).Value;
        var branchCode = Code.Create("BR-001");
        var branchName = Name.Create("Branch One");

        var result = tenant.AddBranch(branchCode, branchName, ValidActor);

        Assert.True(result.IsSuccess);
        Assert.Single(tenant.Branches);
    }

    [Fact]
    public void AddBranch_WithDuplicateCode_ReturnsFailure()
    {
        var tenant = Tenant.Create(ValidCode, ValidName, ValidType, ValidActor).Value;
        var branchCode = Code.Create("BR-001");
        var branchName = Name.Create("Branch One");

        tenant.AddBranch(branchCode, branchName, ValidActor);
        var result = tenant.AddBranch(branchCode, Name.Create("Branch Two"), ValidActor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Tenant.BranchCodeNotUnique, result.Error);
    }

    [Fact]
    public void AddBranch_RaisesBranchCreatedEvent()
    {
        var tenant = Tenant.Create(ValidCode, ValidName, ValidType, ValidActor).Value;
        var branchCode = Code.Create("BR-001");
        var branchName = Name.Create("Branch One");

        tenant.AddBranch(branchCode, branchName, ValidActor);

        var events = tenant.DomainEvents.GetUncommittedChanges().ToList();
        Assert.Contains(events, e => e is BranchCreatedEvent);
    }

    #endregion

    #region Branch BrokenRules

    [Fact]
    public void AddBranch_WithEmptyCode_ReturnsFailureWithBrokenRules()
    {
        var tenant = Tenant.Create(ValidCode, ValidName, ValidType, ValidActor).Value;
        var branchCode = Code.Create("");
        var branchName = Name.Create("Branch One");

        var result = tenant.AddBranch(branchCode, branchName, ValidActor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Common.Required, result.Error);
    }

    [Fact]
    public void AddBranch_WithEmptyName_ReturnsFailureWithBrokenRules()
    {
        var tenant = Tenant.Create(ValidCode, ValidName, ValidType, ValidActor).Value;
        var branchCode = Code.Create("BR-001");
        var branchName = Name.Create("");

        var result = tenant.AddBranch(branchCode, branchName, ValidActor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.ValueObject.PropertyRequired, result.Error);
    }

    #endregion

    #region CloseBranch (ADR-0164: el borrado es lógico)

    [Fact]
    public void CloseBranch_WhenBranchNotFound_ReturnsFailure()
    {
        var tenant = Tenant.Create(ValidCode, ValidName, ValidType, ValidActor).Value;
        var fakeId = IdValueObject.Create();

        var result = tenant.CloseBranch(fakeId, ValidActor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Common.NotFound, result.Error);
    }

    /// <summary>
    /// La prueba central de ADR-0164 §2.1 en el dominio: el cierre NO encoge la colección. Antes esto
    /// hacía `_branches.Remove(...)` y el reconciliador de EF lo traducía en un DELETE.
    /// </summary>
    [Fact]
    public void CloseBranch_NoQuitaLaSucursalDeLaColeccion()
    {
        var tenant = NuevoInquilinoConSucursal(out var branchId);

        var result = tenant.CloseBranch(branchId, ValidActor);

        Assert.True(result.IsSuccess);
        Assert.Single(tenant.Branches);
        Assert.True(tenant.Branches.First().IsClosed);
        Assert.False(tenant.Branches.First().IsActive);
        Assert.NotNull(tenant.Branches.First().ClosedAtUtc);
        Assert.Equal(ValidActor.GetValue(), tenant.Branches.First().ClosedBy);
    }

    [Fact]
    public void CloseBranch_SobreSucursalActiva_ProcedeSinExigirDesactivarAntes()
    {
        // Cerrar y desactivar son verbos independientes (ADR-0164 §2.4): ninguno es el paso previo
        // del otro. Una sucursal que opera puede cerrarse sin la ceremonia de desactivarla primero.
        var tenant = NuevoInquilinoConSucursal(out var branchId);
        Assert.True(tenant.Branches.First().IsActive);

        var result = tenant.CloseBranch(branchId, ValidActor);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void CloseBranch_ConUsuariosActivos_EsRechazado()
    {
        var tenant = NuevoInquilinoConSucursal(out var branchId);

        var result = tenant.CloseBranch(branchId, ValidActor, activeUserCount: 3);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Tenant.BranchHasLiveReferences, result.Error);
        Assert.False(tenant.Branches.First().IsClosed);
    }

    [Fact]
    public void CloseBranch_ConPerfilesActivos_EsRechazado()
    {
        var tenant = NuevoInquilinoConSucursal(out var branchId);

        var result = tenant.CloseBranch(branchId, ValidActor, activeProfileCount: 2);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Tenant.BranchHasLiveReferences, result.Error);
    }

    [Fact]
    public void CloseBranch_ConReferenciasYaEliminadas_Procede()
    {
        // Contraprueba obligatoria de ADR-0164 §2.2: lo YA eliminado no bloquea. Los recuentos que
        // recibe el dominio son de referencias ACTIVAS, así que un cero significa «no queda nada
        // vivo», no «nunca hubo nada».
        var tenant = NuevoInquilinoConSucursal(out var branchId);

        var result = tenant.CloseBranch(branchId, ValidActor, activeUserCount: 0, activeProfileCount: 0);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void CloseBranch_DosVeces_EsRechazado()
    {
        var tenant = NuevoInquilinoConSucursal(out var branchId);
        tenant.CloseBranch(branchId, ValidActor);

        var result = tenant.CloseBranch(branchId, ValidActor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Tenant.BranchAlreadyClosed, result.Error);
    }

    [Fact]
    public void CloseBranch_RaisesBranchClosedEvent()
    {
        var tenant = NuevoInquilinoConSucursal(out var branchId);

        tenant.CloseBranch(branchId, ValidActor);

        var events = tenant.DomainEvents.GetUncommittedChanges().ToList();
        var cierre = Assert.Single(events.OfType<BranchClosedEvent>());
        Assert.Equal("BR-001", cierre.Code);
    }

    /// <summary>ADR-0164 §2.3: el código de una sucursal cerrada no se libera.</summary>
    [Fact]
    public void AddBranch_ConElCodigoDeUnaSucursalCerrada_EsRechazado()
    {
        var tenant = NuevoInquilinoConSucursal(out var branchId);
        tenant.CloseBranch(branchId, ValidActor);

        var result = tenant.AddBranch(Code.Create("BR-001"), Name.Create("Otra sucursal"), ValidActor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Tenant.BranchCodeNotUnique, result.Error);
    }

    [Fact]
    public void ReactivateBranch_SobreSucursalCerrada_EsRechazado()
    {
        // La puerta de atrás cerrada (ADR-0164 §2.4): al estado terminal no se llega —ni se sale de
        // él— manipulando el estado reversible.
        var tenant = NuevoInquilinoConSucursal(out var branchId);
        tenant.CloseBranch(branchId, ValidActor);

        var result = tenant.ReactivateBranch(branchId, ValidActor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Tenant.BranchClosed, result.Error);
        Assert.False(tenant.Branches.First().IsActive);
    }

    [Fact]
    public void DeactivateBranch_SobreSucursalCerrada_EsRechazado()
    {
        var tenant = NuevoInquilinoConSucursal(out var branchId);
        tenant.CloseBranch(branchId, ValidActor);

        var result = tenant.DeactivateBranch(branchId, ValidActor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Tenant.BranchClosed, result.Error);
    }

    #endregion

    #region Bitácora de episodios (ADR-0164)

    [Fact]
    public void AddBranch_AnotaElEpisodioDeApertura()
    {
        var tenant = NuevoInquilinoConSucursal(out _);

        var asientos = tenant.Branches.First().PendingLifecycleEntries;

        var apertura = Assert.Single(asientos);
        Assert.Equal(BranchLifecycleEpisode.Opened, apertura.Episode);
        Assert.Equal(ValidActor.GetValue(), apertura.ActorId);
        Assert.Equal("Branch One", apertura.NameSnapshot);
    }

    [Fact]
    public void LaBitacora_RegistraCadaEpisodioConSuFechaYSuAutor()
    {
        var tenant = NuevoInquilinoConSucursal(out var branchId);
        var otroActor = ActorId.Create("user-002");

        tenant.DeactivateBranch(branchId, ValidActor, reason: "Cierre temporal por obras");
        tenant.ReactivateBranch(branchId, otroActor, reason: "Reapertura tras obras");
        tenant.CloseBranch(branchId, otroActor, reason: "Cese de operaciones en la plaza");

        var asientos = tenant.Branches.First().PendingLifecycleEntries.ToList();

        Assert.Equal(4, asientos.Count);
        Assert.Equal(BranchLifecycleEpisode.Opened, asientos[0].Episode);
        Assert.Equal(BranchLifecycleEpisode.Deactivated, asientos[1].Episode);
        Assert.Equal(BranchLifecycleEpisode.Reactivated, asientos[2].Episode);
        Assert.Equal(BranchLifecycleEpisode.Closed, asientos[3].Episode);

        Assert.Equal(ValidActor.GetValue(), asientos[1].ActorId);
        Assert.Equal("Cierre temporal por obras", asientos[1].Reason);
        Assert.Equal(otroActor.GetValue(), asientos[2].ActorId);
        Assert.Equal(otroActor.GetValue(), asientos[3].ActorId);
        Assert.Equal("Cese de operaciones en la plaza", asientos[3].Reason);
        Assert.All(asientos, a => Assert.NotEqual(default, a.OccurredAtUtc));
        Assert.All(asientos, a => Assert.Equal(branchId.GetValue(), a.BranchId));
    }

    /// <summary>
    /// La razón de ser de la bitácora: dos ÉPOCAS de la misma sucursal deben poder distinguirse. El
    /// asiento guarda la foto (nombre, geocerca) de cada época, así que una auditoría posterior no ve
    /// los datos de hoy proyectados sobre un despacho de entonces.
    /// </summary>
    [Fact]
    public void LaBitacora_ConservaLaFotoDeCadaEpoca()
    {
        var tenant = Tenant.Create(ValidCode, ValidName, ValidType, ValidActor).Value;
        tenant.AddBranch(Code.Create("BR-001"), Name.Create("Almacén Callao"), ValidActor, Value.Create("LIMA-CALLAO"));
        var branchId = tenant.Branches.First().GetId();

        tenant.DeactivateBranch(branchId, ValidActor);
        // Entre época y época la sucursal se muda y cambia de nombre.
        tenant.UpdateBranch(branchId, Name.Create("Terminal Paita"), Value.Create("PIURA-PAITA"), ValidActor);
        tenant.ReactivateBranch(branchId, ValidActor);

        var asientos = tenant.Branches.First().PendingLifecycleEntries.ToList();

        Assert.Equal("Almacén Callao", asientos[1].NameSnapshot);
        Assert.Equal("LIMA-CALLAO", asientos[1].GeofencingSnapshot);
        Assert.Equal("Terminal Paita", asientos[2].NameSnapshot);
        Assert.Equal("PIURA-PAITA", asientos[2].GeofencingSnapshot);
    }

    #endregion

    #region DeactivateBranch

    [Fact]
    public void DeactivateBranch_WhenBranchNotFound_ReturnsFailure()
    {
        var tenant = Tenant.Create(ValidCode, ValidName, ValidType, ValidActor).Value;
        var fakeId = IdValueObject.Create();

        var result = tenant.DeactivateBranch(fakeId, ValidActor);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void DeactivateBranch_WhenBranchIsActive_ReturnsSuccess()
    {
        var tenant = Tenant.Create(ValidCode, ValidName, ValidType, ValidActor).Value;
        var branchCode = Code.Create("BR-001");
        var branchName = Name.Create("Branch One");
        tenant.AddBranch(branchCode, branchName, ValidActor);
        var branchId = tenant.Branches.First().GetId();

        var result = tenant.DeactivateBranch(branchId, ValidActor);

        Assert.True(result.IsSuccess);
        Assert.False(tenant.Branches.First().IsActive);
    }

    [Fact]
    public void DeactivateBranch_WhenBranchAlreadyInactive_ReturnsFailure()
    {
        var tenant = Tenant.Create(ValidCode, ValidName, ValidType, ValidActor).Value;
        var branchCode = Code.Create("BR-001");
        var branchName = Name.Create("Branch One");
        tenant.AddBranch(branchCode, branchName, ValidActor);
        var branchId = tenant.Branches.First().GetId();
        tenant.DeactivateBranch(branchId, ValidActor);

        var result = tenant.DeactivateBranch(branchId, ValidActor);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void DeactivateBranch_RaisesBranchDeactivatedEvent()
    {
        var tenant = Tenant.Create(ValidCode, ValidName, ValidType, ValidActor).Value;
        var branchCode = Code.Create("BR-001");
        var branchName = Name.Create("Branch One");
        tenant.AddBranch(branchCode, branchName, ValidActor);
        var branchId = tenant.Branches.First().GetId();

        tenant.DeactivateBranch(branchId, ValidActor);

        var events = tenant.DomainEvents.GetUncommittedChanges().ToList();
        Assert.Contains(events, e => e is BranchDeactivatedEvent);
    }

    #endregion

    #region ReactivateBranch

    [Fact]
    public void ReactivateBranch_WhenBranchIsInactive_ReturnsSuccess()
    {
        var tenant = Tenant.Create(ValidCode, ValidName, ValidType, ValidActor).Value;
        var branchCode = Code.Create("BR-001");
        var branchName = Name.Create("Branch One");
        tenant.AddBranch(branchCode, branchName, ValidActor);
        var branchId = tenant.Branches.First().GetId();
        tenant.DeactivateBranch(branchId, ValidActor);

        var result = tenant.ReactivateBranch(branchId, ValidActor);

        Assert.True(result.IsSuccess);
        Assert.True(tenant.Branches.First().IsActive);
    }

    [Fact]
    public void ReactivateBranch_WhenBranchAlreadyActive_ReturnsFailure()
    {
        var tenant = Tenant.Create(ValidCode, ValidName, ValidType, ValidActor).Value;
        var branchCode = Code.Create("BR-001");
        var branchName = Name.Create("Branch One");
        tenant.AddBranch(branchCode, branchName, ValidActor);
        var branchId = tenant.Branches.First().GetId();

        var result = tenant.ReactivateBranch(branchId, ValidActor);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void ReactivateBranch_RaisesBranchReactivatedEvent()
    {
        var tenant = Tenant.Create(ValidCode, ValidName, ValidType, ValidActor).Value;
        var branchCode = Code.Create("BR-001");
        var branchName = Name.Create("Branch One");
        tenant.AddBranch(branchCode, branchName, ValidActor);
        var branchId = tenant.Branches.First().GetId();
        tenant.DeactivateBranch(branchId, ValidActor);

        tenant.ReactivateBranch(branchId, ValidActor);

        var events = tenant.DomainEvents.GetUncommittedChanges().ToList();
        Assert.Contains(events, e => e is BranchReactivatedEvent);
    }

    #endregion

    #region Suspend

    [Fact]
    public void Suspend_WhenActive_ReturnsSuccess()
    {
        var tenant = Tenant.Create(ValidCode, ValidName, ValidType, ValidActor).Value;

        var result = tenant.Suspend(ValidActor);

        Assert.True(result.IsSuccess);
        Assert.Equal(TenantStatus.Suspended, tenant.Status);
    }

    [Fact]
    public void Suspend_RaisesTenantSuspendedEvent()
    {
        var tenant = Tenant.Create(ValidCode, ValidName, ValidType, ValidActor).Value;

        tenant.Suspend(ValidActor);

        var events = tenant.DomainEvents.GetUncommittedChanges().ToList();
        Assert.Contains(events, e => e is TenantSuspendedEvent);
    }

    [Fact]
    public void Suspend_WhenHasActiveUsers_ReturnsFailure()
    {
        var tenant = Tenant.Create(ValidCode, ValidName, ValidType, ValidActor).Value;

        var result = tenant.Suspend(ValidActor, activeUserCount: 1);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Tenant.HasActiveUsers, result.Error);
    }

    [Fact]
    public void Suspend_WhenHasActiveBranches_ReturnsFailure()
    {
        var tenant = Tenant.Create(ValidCode, ValidName, ValidType, ValidActor).Value;

        var result = tenant.Suspend(ValidActor, activeBranchCount: 1);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Tenant.HasActiveBranches, result.Error);
    }

    #endregion

    #region Activate

    [Fact]
    public void Activate_WhenSuspended_ReturnsSuccess()
    {
        var tenant = Tenant.Create(ValidCode, ValidName, ValidType, ValidActor).Value;
        tenant.Suspend(ValidActor);

        var result = tenant.Activate(ValidActor);

        Assert.True(result.IsSuccess);
        Assert.Equal(TenantStatus.Active, tenant.Status);
    }

    [Fact]
    public void Activate_RaisesTenantActivatedEvent()
    {
        var tenant = Tenant.Create(ValidCode, ValidName, ValidType, ValidActor).Value;
        tenant.Suspend(ValidActor);

        tenant.Activate(ValidActor);

        var events = tenant.DomainEvents.GetUncommittedChanges().ToList();
        Assert.Contains(events, e => e is TenantActivatedEvent);
    }

    #endregion

    #region Archive

    [Fact]
    public void Archive_WhenActive_ReturnsSuccess()
    {
        var tenant = Tenant.Create(ValidCode, ValidName, ValidType, ValidActor).Value;

        var result = tenant.Archive(ValidActor);

        Assert.True(result.IsSuccess);
        Assert.Equal(TenantStatus.Archived, tenant.Status);
    }

    [Fact]
    public void Archive_WhenAlreadyArchived_ReturnsFailure()
    {
        var tenant = Tenant.Create(ValidCode, ValidName, ValidType, ValidActor).Value;
        tenant.Archive(ValidActor);

        var result = tenant.Archive(ValidActor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Tenant.ArchivedCannotArchive, result.Error);
    }

    [Fact]
    public void Archive_WhenHasActiveIdp_ReturnsFailure()
    {
        var tenant = Tenant.Create(ValidCode, ValidName, ValidType, ValidActor).Value;

        var result = tenant.Archive(ValidActor, activeIdpCount: 1);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Tenant.HasActiveIdpConfig, result.Error);
    }

    #endregion

    #region RegisterIdentityProvider

    [Fact]
    public void RegisterIdentityProvider_WithValidData_ReturnsSuccess()
    {
        var tenant = Tenant.Create(ValidCode, ValidName, ValidType, ValidActor).Value;
        var code = Code.Create("IDP-001");
        var name = Name.Create("Azure AD");
        var description = Description.Create("Corporate Azure AD");

        var result = tenant.RegisterIdentityProvider(code, name, description, IdpStrategy.AzureAd, ValidActor);

        Assert.True(result.IsSuccess);
        Assert.Single(tenant.IdentityProviders);
        Assert.False(tenant.IdentityProviders.First().IsActive);
    }

    [Fact]
    public void RegisterIdentityProvider_WithDuplicateCode_ReturnsFailure()
    {
        var tenant = Tenant.Create(ValidCode, ValidName, ValidType, ValidActor).Value;
        var code = Code.Create("IDP-001");
        var name = Name.Create("Azure AD");
        var description = Description.Create("Corporate Azure AD");

        tenant.RegisterIdentityProvider(code, name, description, IdpStrategy.AzureAd, ValidActor);
        var result = tenant.RegisterIdentityProvider(code, Name.Create("Azure AD Backup"), description, IdpStrategy.AzureAd, ValidActor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Tenant.IdpCodeNotUnique, result.Error);
    }

    [Fact]
    public void RegisterIdentityProvider_WithEmptyCode_ReturnsFailure()
    {
        var tenant = Tenant.Create(ValidCode, ValidName, ValidType, ValidActor).Value;
        var code = Code.Create("");
        var name = Name.Create("Azure AD");
        var description = Description.Create("Corporate Azure AD");

        var result = tenant.RegisterIdentityProvider(code, name, description, IdpStrategy.AzureAd, ValidActor);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void RegisterIdentityProvider_RaisesIdentityProviderRegisteredEvent()
    {
        var tenant = Tenant.Create(ValidCode, ValidName, ValidType, ValidActor).Value;
        var code = Code.Create("IDP-001");
        var name = Name.Create("Azure AD");
        var description = Description.Create("Corporate Azure AD");

        tenant.RegisterIdentityProvider(code, name, description, IdpStrategy.AzureAd, ValidActor);

        var events = tenant.DomainEvents.GetUncommittedChanges().ToList();
        Assert.Contains(events, e => e is IdentityProviderRegisteredEvent);
    }

    [Fact]
    public void RegisterMultipleIdentityProviders_AllStartInactive()
    {
        var tenant = Tenant.Create(ValidCode, ValidName, ValidType, ValidActor).Value;
        var description = Description.Create("Test");

        tenant.RegisterIdentityProvider(Code.Create("IDP-001"), Name.Create("Azure AD"), description, IdpStrategy.AzureAd, ValidActor);
        tenant.RegisterIdentityProvider(Code.Create("IDP-002"), Name.Create("Okta"), description, IdpStrategy.Okta, ValidActor);

        Assert.Equal(2, tenant.IdentityProviders.Count);
        Assert.All(tenant.IdentityProviders, ip => Assert.False(ip.IsActive));
        Assert.Null(tenant.GetActiveIdentityProvider());
    }

    #endregion

    #region ActivateIdentityProvider

    [Fact]
    public void ActivateIdentityProvider_WithValidId_ReturnsSuccess()
    {
        var tenant = Tenant.Create(ValidCode, ValidName, ValidType, ValidActor).Value;
        var description = Description.Create("Test");
        tenant.RegisterIdentityProvider(Code.Create("IDP-001"), Name.Create("Azure AD"), description, IdpStrategy.AzureAd, ValidActor);
        var idpId = tenant.IdentityProviders.First().GetId();

        var result = tenant.ActivateIdentityProvider(idpId, ValidActor);

        Assert.True(result.IsSuccess);
        Assert.True(tenant.IdentityProviders.First().IsActive);
    }

    [Fact]
    public void ActivateIdentityProvider_WhenNotFound_ReturnsFailure()
    {
        var tenant = Tenant.Create(ValidCode, ValidName, ValidType, ValidActor).Value;
        var fakeId = IdValueObject.Create();

        var result = tenant.ActivateIdentityProvider(fakeId, ValidActor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Tenant.IdpNotFound, result.Error);
    }

    [Fact]
    public void ActivateIdentityProvider_WhenAlreadyActive_ReturnsFailure()
    {
        var tenant = Tenant.Create(ValidCode, ValidName, ValidType, ValidActor).Value;
        var description = Description.Create("Test");
        tenant.RegisterIdentityProvider(Code.Create("IDP-001"), Name.Create("Azure AD"), description, IdpStrategy.AzureAd, ValidActor);
        var idpId = tenant.IdentityProviders.First().GetId();
        tenant.ActivateIdentityProvider(idpId, ValidActor);

        var result = tenant.ActivateIdentityProvider(idpId, ValidActor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Tenant.IdpAlreadyActive, result.Error);
    }

    [Fact]
    public void ActivateIdentityProvider_DeactivatesAllOthers()
    {
        var tenant = Tenant.Create(ValidCode, ValidName, ValidType, ValidActor).Value;
        var description = Description.Create("Test");
        tenant.RegisterIdentityProvider(Code.Create("IDP-001"), Name.Create("Azure AD"), description, IdpStrategy.AzureAd, ValidActor);
        tenant.RegisterIdentityProvider(Code.Create("IDP-002"), Name.Create("Okta"), description, IdpStrategy.Okta, ValidActor);
        tenant.RegisterIdentityProvider(Code.Create("IDP-003"), Name.Create("Keycloak"), description, IdpStrategy.Keycloak, ValidActor);

        var secondIdpId = tenant.IdentityProviders.Skip(1).First().GetId();
        tenant.ActivateIdentityProvider(secondIdpId, ValidActor);

        var activeCount = tenant.IdentityProviders.Count(ip => ip.IsActive);
        Assert.Equal(1, activeCount);
        Assert.True(tenant.IdentityProviders.Skip(1).First().IsActive);
    }

    [Fact]
    public void ActivateIdentityProvider_RaisesIdentityProviderActivatedEvent()
    {
        var tenant = Tenant.Create(ValidCode, ValidName, ValidType, ValidActor).Value;
        var description = Description.Create("Test");
        tenant.RegisterIdentityProvider(Code.Create("IDP-001"), Name.Create("Azure AD"), description, IdpStrategy.AzureAd, ValidActor);
        var idpId = tenant.IdentityProviders.First().GetId();

        tenant.ActivateIdentityProvider(idpId, ValidActor);

        var events = tenant.DomainEvents.GetUncommittedChanges().ToList();
        Assert.Contains(events, e => e is IdentityProviderActivatedEvent);
    }

    [Fact]
    public void GetActiveIdentityProvider_ReturnsActiveOne()
    {
        var tenant = Tenant.Create(ValidCode, ValidName, ValidType, ValidActor).Value;
        var description = Description.Create("Test");
        tenant.RegisterIdentityProvider(Code.Create("IDP-001"), Name.Create("Azure AD"), description, IdpStrategy.AzureAd, ValidActor);
        tenant.RegisterIdentityProvider(Code.Create("IDP-002"), Name.Create("Okta"), description, IdpStrategy.Okta, ValidActor);

        var secondIdpId = tenant.IdentityProviders.Skip(1).First().GetId();
        tenant.ActivateIdentityProvider(secondIdpId, ValidActor);

        var active = tenant.GetActiveIdentityProvider();
        Assert.NotNull(active);
        Assert.Equal(Code.Create("IDP-002"), active.Code);
    }

    #endregion

    #region DeactivateIdentityProvider

    [Fact]
    public void DeactivateIdentityProvider_WhenActive_ReturnsSuccess()
    {
        var tenant = Tenant.Create(ValidCode, ValidName, ValidType, ValidActor).Value;
        var description = Description.Create("Test");
        tenant.RegisterIdentityProvider(Code.Create("IDP-001"), Name.Create("Azure AD"), description, IdpStrategy.AzureAd, ValidActor);
        var idpId = tenant.IdentityProviders.First().GetId();
        tenant.ActivateIdentityProvider(idpId, ValidActor);

        var result = tenant.DeactivateIdentityProvider(idpId, ValidActor);

        Assert.True(result.IsSuccess);
        Assert.False(tenant.IdentityProviders.First().IsActive);
    }

    [Fact]
    public void DeactivateIdentityProvider_WhenNotFound_ReturnsFailure()
    {
        var tenant = Tenant.Create(ValidCode, ValidName, ValidType, ValidActor).Value;
        var fakeId = IdValueObject.Create();

        var result = tenant.DeactivateIdentityProvider(fakeId, ValidActor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Tenant.IdpNotFound, result.Error);
    }

    [Fact]
    public void DeactivateIdentityProvider_WhenAlreadyInactive_ReturnsFailure()
    {
        var tenant = Tenant.Create(ValidCode, ValidName, ValidType, ValidActor).Value;
        var description = Description.Create("Test");
        tenant.RegisterIdentityProvider(Code.Create("IDP-001"), Name.Create("Azure AD"), description, IdpStrategy.AzureAd, ValidActor);
        var idpId = tenant.IdentityProviders.First().GetId();

        var result = tenant.DeactivateIdentityProvider(idpId, ValidActor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Tenant.IdpAlreadyInactive, result.Error);
    }

    [Fact]
    public void DeactivateIdentityProvider_RaisesIdentityProviderDeactivatedEvent()
    {
        var tenant = Tenant.Create(ValidCode, ValidName, ValidType, ValidActor).Value;
        var description = Description.Create("Test");
        tenant.RegisterIdentityProvider(Code.Create("IDP-001"), Name.Create("Azure AD"), description, IdpStrategy.AzureAd, ValidActor);
        var idpId = tenant.IdentityProviders.First().GetId();
        tenant.ActivateIdentityProvider(idpId, ValidActor);

        tenant.DeactivateIdentityProvider(idpId, ValidActor);

        var events = tenant.DomainEvents.GetUncommittedChanges().ToList();
        Assert.Contains(events, e => e is IdentityProviderDeactivatedEvent);
    }

    #endregion

    #region RemoveIdentityProvider

    [Fact]
    public void RemoveIdentityProvider_WhenInactive_ReturnsSuccess()
    {
        var tenant = Tenant.Create(ValidCode, ValidName, ValidType, ValidActor).Value;
        var description = Description.Create("Test");
        tenant.RegisterIdentityProvider(Code.Create("IDP-001"), Name.Create("Azure AD"), description, IdpStrategy.AzureAd, ValidActor);
        var idpId = tenant.IdentityProviders.First().GetId();

        var result = tenant.RemoveIdentityProvider(idpId, ValidActor);

        Assert.True(result.IsSuccess);
        Assert.Empty(tenant.IdentityProviders);
    }

    [Fact]
    public void RemoveIdentityProvider_WhenNotFound_ReturnsFailure()
    {
        var tenant = Tenant.Create(ValidCode, ValidName, ValidType, ValidActor).Value;
        var fakeId = IdValueObject.Create();

        var result = tenant.RemoveIdentityProvider(fakeId, ValidActor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Tenant.IdpNotFound, result.Error);
    }

    [Fact]
    public void RemoveIdentityProvider_WhenActive_ReturnsFailure()
    {
        var tenant = Tenant.Create(ValidCode, ValidName, ValidType, ValidActor).Value;
        var description = Description.Create("Test");
        tenant.RegisterIdentityProvider(Code.Create("IDP-001"), Name.Create("Azure AD"), description, IdpStrategy.AzureAd, ValidActor);
        var idpId = tenant.IdentityProviders.First().GetId();
        tenant.ActivateIdentityProvider(idpId, ValidActor);

        var result = tenant.RemoveIdentityProvider(idpId, ValidActor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Common.Invalid, result.Error);
    }

    [Fact]
    public void RemoveIdentityProvider_RaisesIdentityProviderRemovedEvent()
    {
        var tenant = Tenant.Create(ValidCode, ValidName, ValidType, ValidActor).Value;
        var description = Description.Create("Test");
        tenant.RegisterIdentityProvider(Code.Create("IDP-001"), Name.Create("Azure AD"), description, IdpStrategy.AzureAd, ValidActor);
        var idpId = tenant.IdentityProviders.First().GetId();

        tenant.RemoveIdentityProvider(idpId, ValidActor);

        var events = tenant.DomainEvents.GetUncommittedChanges().ToList();
        Assert.Contains(events, e => e is IdentityProviderRemovedEvent);
    }

    #endregion

    #region Tenant Status Edge Cases

    [Fact]
    public void Suspend_WhenArchived_ReturnsFailure()
    {
        var tenant = Tenant.Create(ValidCode, ValidName, ValidType, ValidActor).Value;
        tenant.Suspend(ValidActor);

        var propsType = typeof(TenantProps);
        var statusProp = propsType.GetProperty("Status");
        statusProp!.SetValue(tenant.Props, TenantStatus.Archived);

        var result = tenant.Suspend(ValidActor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Tenant.ArchivedCannotSuspend, result.Error);
    }

    [Fact]
    public void Activate_WhenArchived_ReturnsFailure()
    {
        var tenant = Tenant.Create(ValidCode, ValidName, ValidType, ValidActor).Value;
        tenant.Suspend(ValidActor);

        var propsType = typeof(TenantProps);
        var statusProp = propsType.GetProperty("Status");
        statusProp!.SetValue(tenant.Props, TenantStatus.Archived);

        var result = tenant.Activate(ValidActor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Tenant.ArchivedCannotActivate, result.Error);
    }

    [Fact]
    public void ReactivateBranch_WhenBranchNotFound_ReturnsFailure()
    {
        var tenant = Tenant.Create(ValidCode, ValidName, ValidType, ValidActor).Value;
        var fakeId = IdValueObject.Create();

        var result = tenant.ReactivateBranch(fakeId, ValidActor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Common.NotFound, result.Error);
    }

    #endregion

    /// <summary>Inquilino con una sucursal BR-001 «Branch One» recién dada de alta.</summary>
    private static Tenant NuevoInquilinoConSucursal(out IdValueObject branchId)
    {
        var tenant = Tenant.Create(ValidCode, ValidName, ValidType, ValidActor).Value;
        tenant.AddBranch(Code.Create("BR-001"), Name.Create("Branch One"), ValidActor);
        branchId = tenant.Branches.First().GetId();
        return tenant;
    }
}

#pragma warning restore S1144
