namespace Evolith.Ums.Domain.Test.Approvals.AccessEnforcementPolicy;

using Evolith.Ums.Domain.Approvals.AccessEnforcementPolicy;
using Xunit;

public class AccessEnforcementPolicyTests
{
    private static readonly TenantId ValidTenantId = TenantId.Load(Guid.NewGuid().ToString());
    private static readonly ProfileId? ValidProfileId = ProfileId.Load(Guid.NewGuid().ToString());
    private static readonly RoleId? ValidRoleId = RoleId.Load(Guid.NewGuid().ToString());
    private static readonly AccessEnforcementAction ValidAction = AccessEnforcementAction.RestrictProfile;
    private static readonly ActorId ValidActor = ActorId.Create("user-001");
    #region Create

    [Fact]
    public void Create_WithProfileId_ReturnsSuccess()
    {
        var result = AccessEnforcementPolicy.Create(ValidTenantId, ValidProfileId, null, ValidAction, ValidActor);

        Assert.True(result.IsSuccess);
        Assert.Equal(ValidTenantId, result.Value.TenantId);
        Assert.Equal(ValidProfileId, result.Value.ProfileId);
        Assert.Null(result.Value.RoleId);
        Assert.Equal(ValidAction, result.Value.EnforcementAction);
        Assert.True(result.Value.IsActive);
    }

    [Fact]
    public void Create_WithRoleId_ReturnsSuccess()
    {
        var result = AccessEnforcementPolicy.Create(ValidTenantId, null, ValidRoleId, ValidAction, ValidActor);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.ProfileId);
        Assert.Equal(ValidRoleId, result.Value.RoleId);
    }

    [Fact]
    public void Create_WithBothProfileAndRole_ReturnsSuccess()
    {
        var result = AccessEnforcementPolicy.Create(ValidTenantId, ValidProfileId, ValidRoleId, ValidAction, ValidActor);

        Assert.True(result.IsSuccess);
        Assert.Equal(ValidProfileId, result.Value.ProfileId);
        Assert.Equal(ValidRoleId, result.Value.RoleId);
    }

    [Fact]
    public void Create_WithoutProfileOrRole_ReturnsFailure()
    {
        var result = AccessEnforcementPolicy.Create(ValidTenantId, null, null, ValidAction, ValidActor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Approvals.PolicyRequiresProfileOrRole, result.Error);
    }

    #endregion

    #region Deactivate

    [Fact]
    public void Deactivate_WhenActive_ReturnsSuccess()
    {
        var policy = AccessEnforcementPolicy.Create(ValidTenantId, ValidProfileId, null, ValidAction, ValidActor).Value;

        var result = policy.Deactivate(ValidActor);

        Assert.True(result.IsSuccess);
        Assert.False(policy.IsActive);
    }

    [Fact]
    public void Deactivate_WhenAlreadyInactive_ReturnsFailure()
    {
        var policy = AccessEnforcementPolicy.Create(ValidTenantId, ValidProfileId, null, ValidAction, ValidActor).Value;
        policy.Deactivate(ValidActor);

        var result = policy.Deactivate(ValidActor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Approvals.PolicyAlreadyInactive, result.Error);
    }

    #endregion

    #region UpdateAction

    [Fact]
    public void UpdateAction_WithValidData_ReturnsSuccess()
    {
        var policy = AccessEnforcementPolicy.Create(ValidTenantId, ValidProfileId, null, ValidAction, ValidActor).Value;
        var newAction = AccessEnforcementAction.BlockUser;

        var result = policy.UpdateAction(newAction, ValidActor);

        Assert.True(result.IsSuccess);
        Assert.Equal(newAction, policy.EnforcementAction);
    }

    // -------------------------------------------------------------------------
    // INVARIANTE DE IRREVERSIBILIDAD (G-051) — NO DEBILITAR.
    //
    // Deactivate es una transicion terminal: el agregado no expone reactivacion,
    // por lo que una politica desactivada queda congelada. UpdateAction sobre una
    // politica desactivada es una transicion invalida sobre un estado terminal y
    // debe rechazarse con Result.Failure (BrokenRule PolicyInactiveCannotUpdate).
    //
    // El test previo `UpdateAction_WhenInactive_StillSucceeds` afirmaba lo contrario
    // (exito), fijando el defecto. Estos tests son el candado de la invariante: no
    // deben relajarse para volver a permitir la mutacion de una politica inactiva.
    // -------------------------------------------------------------------------

    [Fact]
    public void UpdateAction_WhenInactive_ReturnsFailure()
    {
        var policy = AccessEnforcementPolicy.Create(ValidTenantId, ValidProfileId, null, ValidAction, ValidActor).Value;
        policy.Deactivate(ValidActor);
        var newAction = AccessEnforcementAction.RestrictProfile;

        var result = policy.UpdateAction(newAction, ValidActor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Approvals.PolicyInactiveCannotUpdate, result.Error);
    }

    [Fact]
    public void UpdateAction_WhenInactive_DoesNotMutateEnforcementAction()
    {
        var policy = AccessEnforcementPolicy.Create(ValidTenantId, ValidProfileId, null, ValidAction, ValidActor).Value;
        policy.Deactivate(ValidActor);

        policy.UpdateAction(AccessEnforcementAction.BlockUser, ValidActor);

        // La accion original permanece intacta: el estado terminal es inmutable.
        Assert.Equal(ValidAction, policy.EnforcementAction);
    }

    #endregion
}
