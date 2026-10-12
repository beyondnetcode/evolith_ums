namespace Evolith.Ums.Domain.Test.Identity.Tenant.TenantParameter;

using Evolith.Ums.Domain.Identity.Tenant.TenantParameter;
using Xunit;
using TenantAggregate = Evolith.Ums.Domain.Identity.Tenant.Tenant;
using TenantParameterEntity = Evolith.Ums.Domain.Identity.Tenant.TenantParameter.TenantParameter;

public class TenantParameterTests
{
    private static readonly ActorId ValidActor = ActorId.Create("user-001");

    private static TenantParameterEntity CreateStringParameter(string code = "STR-PARAM", string value = "old", string? allowedValues = null)
    {
        return TenantParameterEntity.Create(
            TenantId.Create(),
            code,
            "Parametro de prueba",
            value,
            TenantParameterValueType.String,
            TenantParameterCategory.Session,
            isSensitive: false,
            defaultValue: null,
            allowedValues: allowedValues,
            ValidActor).Value;
    }

    #region Create

    [Fact]
    public void Create_WithValidData_ReturnsSuccessActiveAndPreservesFields()
    {
        var result = TenantParameterEntity.Create(
            TenantId.Create(),
            "INT-PARAM",
            "Parametro entero",
            "10",
            TenantParameterValueType.Integer,
            TenantParameterCategory.Session,
            isSensitive: false,
            defaultValue: null,
            allowedValues: null,
            ValidActor);

        Assert.True(result.IsSuccess);
        Assert.Equal("INT-PARAM", result.Value.Code.GetValue());
        Assert.Equal("10", result.Value.Value);
        Assert.Equal(TenantParameterValueType.Integer, result.Value.ValueType);
        Assert.Equal(TenantParameterCategory.Session, result.Value.Category);
        Assert.True(result.Value.IsActive);
    }

    [Fact]
    public void Create_WithEmptyCode_ReturnsFailure()
    {
        var result = TenantParameterEntity.Create(
            TenantId.Create(),
            "",
            "Parametro sin codigo",
            "v",
            TenantParameterValueType.String,
            TenantParameterCategory.Session,
            isSensitive: false,
            defaultValue: null,
            allowedValues: null,
            ValidActor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Common.Required, result.Error);
    }

    #endregion

    #region UpdateValue

    [Fact]
    public void UpdateValue_WithValidValue_ReturnsSuccessAndRaisesUpdatedEvent()
    {
        var parameter = CreateStringParameter(value: "old");

        var result = parameter.UpdateValue("new", ValidActor);

        Assert.True(result.IsSuccess);
        Assert.Equal("new", parameter.Value);
        var events = parameter.DomainEvents.GetUncommittedChanges().ToList();
        Assert.Contains(events, e => e is TenantParameterUpdatedEvent);
    }

    [Fact]
    public void UpdateValue_WithTypeMismatch_ReturnsFailureAndKeepsValue()
    {
        var parameter = TenantParameterEntity.Create(
            TenantId.Create(),
            "INT-PARAM",
            "Parametro entero",
            "10",
            TenantParameterValueType.Integer,
            TenantParameterCategory.Session,
            isSensitive: false,
            defaultValue: null,
            allowedValues: null,
            ValidActor).Value;

        var result = parameter.UpdateValue("no-es-entero", ValidActor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.TenantParameter.InvalidValueType, result.Error);
        Assert.Equal("10", parameter.Value);
    }

    [Fact]
    public void UpdateValue_WhenNotInAllowedList_ReturnsFailureAndKeepsValue()
    {
        var parameter = CreateStringParameter(value: "ES", allowedValues: "ES,EN,PT");

        var result = parameter.UpdateValue("ZZ", ValidActor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.TenantParameter.ValueNotInAllowedList, result.Error);
        Assert.Equal("ES", parameter.Value);
    }

    [Fact]
    public void UpdateValue_WhenInAllowedList_ReturnsSuccess()
    {
        var parameter = CreateStringParameter(value: "ES", allowedValues: "ES,EN,PT");

        var result = parameter.UpdateValue("EN", ValidActor);

        Assert.True(result.IsSuccess);
        Assert.Equal("EN", parameter.Value);
    }

    #endregion

    #region Deactivate / Reactivate

    [Fact]
    public void Deactivate_SetsInactiveAndRaisesDeactivatedEvent()
    {
        var parameter = CreateStringParameter();

        var result = parameter.Deactivate(ValidActor);

        Assert.True(result.IsSuccess);
        Assert.False(parameter.IsActive);
        var events = parameter.DomainEvents.GetUncommittedChanges().ToList();
        Assert.Contains(events, e => e is TenantParameterDeactivatedEvent);
    }

    [Fact]
    public void Reactivate_SetsActiveAndRaisesReactivatedEvent()
    {
        var parameter = CreateStringParameter();
        parameter.Deactivate(ValidActor);

        var result = parameter.Reactivate(ValidActor);

        Assert.True(result.IsSuccess);
        Assert.True(parameter.IsActive);
        var events = parameter.DomainEvents.GetUncommittedChanges().ToList();
        Assert.Contains(events, e => e is TenantParameterReactivatedEvent);
    }

    #endregion

    #region Delete (borrado lógico)

    [Fact]
    public void Delete_ConVinculoActivo_ReturnsFailure()
    {
        // Regla de cascada: un parámetro ACTIVO es una referencia viva de la configuración del
        // inquilino —el provider lo resuelve ahora mismo por su código— y no se puede eliminar.
        var parameter = CreateStringParameter();

        var result = parameter.Delete(ValidActor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.TenantParameter.HasActiveBinding, result.Error);
        Assert.False(parameter.IsDeleted);
    }

    [Fact]
    public void Delete_TrasDesactivar_ReturnsSuccessYMarcaEliminado()
    {
        // La desactivación ES la eliminación lógica del vínculo: una vez hecha, el borrado procede.
        // La fila conserva todos sus datos (código, valor, auditoría): solo cambia de visibilidad.
        var parameter = CreateStringParameter(value: "es-PE");
        parameter.Deactivate(ValidActor);

        var result = parameter.Delete(ValidActor);

        Assert.True(result.IsSuccess);
        Assert.True(parameter.IsDeleted);
        Assert.Equal("es-PE", parameter.Value);
        var events = parameter.DomainEvents.GetUncommittedChanges().ToList();
        Assert.Contains(events, e => e is TenantParameterDeletedEvent);
    }

    [Fact]
    public void Delete_CuandoYaEstaEliminado_ReturnsFailure()
    {
        var parameter = CreateStringParameter();
        parameter.Deactivate(ValidActor);
        parameter.Delete(ValidActor);

        var result = parameter.Delete(ValidActor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.TenantParameter.AlreadyDeleted, result.Error);
    }

    [Fact]
    public void Reactivate_TrasEliminar_ReturnsFailure()
    {
        // El borrado lógico es terminal: reactivar no puede ser la puerta trasera que lo resucite.
        var parameter = CreateStringParameter();
        parameter.Deactivate(ValidActor);
        parameter.Delete(ValidActor);

        var result = parameter.Reactivate(ValidActor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.TenantParameter.AlreadyDeleted, result.Error);
        Assert.False(parameter.IsActive);
    }

    #endregion

    #region Uniqueness among active parameters (Tenant aggregate)

    private static TenantAggregate CreateTenant()
    {
        return TenantAggregate.Create(
            Code.Create("TEN-001"),
            Name.Create("Inquilino de prueba"),
            OrganizationType.INTERNAL,
            ValidActor).Value;
    }

    [Fact]
    public void AddParameter_WithUniqueActiveCode_ReturnsSuccess()
    {
        var tenant = CreateTenant();

        var result = tenant.AddParameter(
            "PARAM-1", "desc", "v",
            TenantParameterValueType.String, TenantParameterCategory.Session,
            isSensitive: false, defaultValue: null, allowedValues: null, ValidActor);

        Assert.True(result.IsSuccess);
        Assert.Single(tenant.Parameters);
    }

    [Fact]
    public void AddParameter_WithDuplicateActiveCode_ReturnsFailure()
    {
        var tenant = CreateTenant();
        tenant.AddParameter(
            "PARAM-1", "desc", "v",
            TenantParameterValueType.String, TenantParameterCategory.Session,
            isSensitive: false, defaultValue: null, allowedValues: null, ValidActor);

        var result = tenant.AddParameter(
            "PARAM-1", "otra desc", "w",
            TenantParameterValueType.String, TenantParameterCategory.Session,
            isSensitive: false, defaultValue: null, allowedValues: null, ValidActor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.TenantParameter.CodeNotUnique, result.Error);
    }

    [Fact]
    public void AddParameter_WithCodeOfDeactivatedParameter_ReturnsSuccess()
    {
        var tenant = CreateTenant();
        tenant.AddParameter(
            "PARAM-1", "desc", "v",
            TenantParameterValueType.String, TenantParameterCategory.Session,
            isSensitive: false, defaultValue: null, allowedValues: null, ValidActor);
        tenant.DeactivateParameter("PARAM-1", ValidActor);

        var result = tenant.AddParameter(
            "PARAM-1", "desc nueva", "w",
            TenantParameterValueType.String, TenantParameterCategory.Session,
            isSensitive: false, defaultValue: null, allowedValues: null, ValidActor);

        Assert.True(result.IsSuccess);
        Assert.Single(tenant.Parameters, p => p.IsActive);
    }

    #endregion
}
