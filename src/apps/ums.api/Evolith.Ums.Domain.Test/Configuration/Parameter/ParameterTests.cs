namespace Evolith.Ums.Domain.Test.Configuration.Parameter;

using Evolith.Ums.Domain.Configuration.Parameter;
using Evolith.Ums.Domain.Configuration.Parameter.ValueObjects;
using Xunit;

public class ParameterTests
{
    private static readonly ActorId ValidActor = ActorId.Create("user-001");
    private static readonly Code ValidCode = Code.Create("PARAM-001");
    private static readonly ParameterName ValidName = ParameterName.Create("Parameter 1");
    private static readonly Description ValidDescription = Description.Create("Test parameter");
    private static readonly DefaultValue ValidDefaultValue = DefaultValue.Create("default");
    private static readonly ParameterScope ValidScope = ParameterScope.GlobalAndTenant;
    private static readonly TenantId ValidTenantId = TenantId.Load(Guid.NewGuid());
    private static readonly IdValueObject ValidDefinitionId = IdValueObject.Create();

    [Fact]
    public void ParameterDefinition_Create_WhenCodeAlreadyExists_ReturnsFailure()
    {
        var result = ParameterDefinition.Create(
            ValidCode,
            ValidName,
            ValidDescription,
            ParameterDataType.String,
            ValidDefaultValue,
            ValidScope,
            true,
            false,
            1,
            ValidActor,
            existingDefinitionCount: 1);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Configuration.ParameterCodeNotUnique, result.Error);
    }

    [Fact]
    public void ParameterDefinition_Archive_WhenActiveValuesExist_ReturnsFailure()
    {
        var definition = ParameterDefinition.Create(
            ValidCode,
            ValidName,
            ValidDescription,
            ParameterDataType.String,
            ValidDefaultValue,
            ValidScope,
            true,
            false,
            1,
            ValidActor).Value;

        var result = definition.Archive(ValidActor, globalValueCount: 1, tenantValueCount: 0);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Configuration.ParameterHasActiveValues, result.Error);
    }

    [Fact]
    public void ParameterDefinition_Archive_WhenNoActiveValues_ReturnsSuccess()
    {
        var definition = ParameterDefinition.Create(
            ValidCode,
            ValidName,
            ValidDescription,
            ParameterDataType.String,
            ValidDefaultValue,
            ValidScope,
            true,
            false,
            1,
            ValidActor).Value;

        var result = definition.Archive(ValidActor);

        Assert.True(result.IsSuccess);
        Assert.False(definition.IsActive);
    }

    [Fact]
    public void ParameterGlobalValue_Create_WithInvalidNumber_ReturnsFailure()
    {
        var result = ParameterGlobalValue.Create(
            ValidDefinitionId,
            EffectiveValue.Create("not-a-number"),
            ParameterDataType.Number,
            ValidActor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Configuration.ParameterValueInvalidType, result.Error);
    }

    [Fact]
    public void ParameterGlobalValue_UpdateValue_WithInvalidJson_ReturnsFailure()
    {
        var globalValue = ParameterGlobalValue.Create(
            ValidDefinitionId,
            EffectiveValue.Create("{\"ok\":true}"),
            ParameterDataType.Json,
            ValidActor).Value;

        var result = globalValue.UpdateValue(EffectiveValue.Create("{invalid"), ParameterDataType.Json, ValidActor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Configuration.ParameterValueInvalidType, result.Error);
    }

    [Fact]
    public void ParameterGlobalValue_Archive_WhenTenantValuesExist_ReturnsFailure()
    {
        var globalValue = ParameterGlobalValue.Create(
            ValidDefinitionId,
            EffectiveValue.Create("hello"),
            ParameterDataType.String,
            ValidActor).Value;
        globalValue.Publish(ValidActor);

        var result = globalValue.Archive(ValidActor, activeTenantValueCount: 1);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Configuration.ParameterGlobalValueInUse, result.Error);
    }

    [Fact]
    public void ParameterTenantValue_Create_WhenOverrideNotAllowed_ReturnsFailure()
    {
        var result = ParameterTenantValue.Create(
            ValidTenantId,
            ValidDefinitionId,
            OverrideValue.Create("true"),
            ParameterDataType.Boolean,
            ParameterScope.GlobalOnly,
            ValidActor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Configuration.ParameterOverrideNotAllowed, result.Error);
    }

    [Fact]
    public void ParameterTenantValue_Create_WithInvalidType_ReturnsFailure()
    {
        var result = ParameterTenantValue.Create(
            ValidTenantId,
            ValidDefinitionId,
            OverrideValue.Create("maybe"),
            ParameterDataType.Boolean,
            ParameterScope.GlobalAndTenant,
            ValidActor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Configuration.ParameterValueInvalidType, result.Error);
    }

    [Fact]
    public void ParameterTenantValue_UpdateValue_WithValidType_ReturnsSuccess()
    {
        var tenantValue = ParameterTenantValue.Create(
            ValidTenantId,
            ValidDefinitionId,
            OverrideValue.Create("false"),
            ParameterDataType.Boolean,
            ParameterScope.GlobalAndTenant,
            ValidActor).Value;

        var result = tenantValue.UpdateValue(OverrideValue.Create("true"), ParameterDataType.Boolean, ParameterScope.GlobalAndTenant, ValidActor);

        Assert.True(result.IsSuccess);
    }
}
