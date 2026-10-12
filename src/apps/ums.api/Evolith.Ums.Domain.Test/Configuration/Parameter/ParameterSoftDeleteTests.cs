namespace Evolith.Ums.Domain.Test.Configuration.Parameter;

using Evolith.Ums.Domain.Configuration.Parameter;
using Evolith.Ums.Domain.Configuration.Parameter.ValueObjects;
using Xunit;

/// <summary>
/// Invariantes de dominio del borrado LÓGICO del catálogo de parámetros.
///
/// La política del propietario es que el borrado físico no existe: eliminar es una transición de
/// estado, y no se puede eliminar algo que todavía tiene referencias reales. Estas pruebas fijan
/// las dos mitades de esa regla en el agregado, donde vive la invariante.
/// </summary>
public class ParameterSoftDeleteTests
{
    private static readonly ActorId ValidActor = ActorId.Create("user-001");
    private static readonly TenantId ValidTenantId = TenantId.Load(Guid.NewGuid());
    private static readonly IdValueObject ValidDefinitionId = IdValueObject.Create();

    private static ParameterDefinition MakeDefinition() =>
        ParameterDefinition.Create(
            Code.Create("PARAM-001"),
            ParameterName.Create("Parameter 1"),
            Description.Create("Test parameter"),
            ParameterDataType.String,
            DefaultValue.Create("default"),
            ParameterScope.GlobalAndTenant,
            isActive: true,
            isMandatory: false,
            displayOrder: 1,
            ValidActor).Value;

    // ── ParameterDefinition ──────────────────────────────────────────────────

    [Fact]
    public void Definition_Delete_SinDependientesVivos_MarcaEliminadaYDesactiva()
    {
        var definition = MakeDefinition();

        var result = definition.Delete(ValidActor);

        Assert.True(result.IsSuccess);
        Assert.True(definition.IsDeleted);
        Assert.False(definition.IsActive);
    }

    [Fact]
    public void Definition_Delete_ConValorGlobalVivo_ReturnsFailure()
    {
        var definition = MakeDefinition();

        var result = definition.Delete(ValidActor, liveGlobalValueCount: 1);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Configuration.ParameterHasActiveValues, result.Error);
        Assert.False(definition.IsDeleted);
    }

    [Fact]
    public void Definition_Delete_ConValorDeInquilinoVivo_ReturnsFailure()
    {
        var definition = MakeDefinition();

        var result = definition.Delete(ValidActor, liveTenantValueCount: 3);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Configuration.ParameterHasActiveValues, result.Error);
        Assert.False(definition.IsDeleted);
    }

    [Fact]
    public void Definition_Delete_DosVeces_ReturnsFailure()
    {
        var definition = MakeDefinition();
        Assert.True(definition.Delete(ValidActor).IsSuccess);

        var second = definition.Delete(ValidActor);

        Assert.True(second.IsFailure);
        Assert.Contains(DomainErrors.Configuration.ParameterAlreadyDeleted, second.Error);
    }

    // ── ParameterGlobalValue / ParameterTenantValue ──────────────────────────

    [Fact]
    public void GlobalValue_Delete_DesdeBorrador_PasaAEstadoTerminal()
    {
        var value = ParameterGlobalValue.Create(
            ValidDefinitionId, EffectiveValue.Create("hello"), ParameterDataType.String, ValidActor).Value;

        var result = value.Delete(ValidActor);

        Assert.True(result.IsSuccess);
        Assert.Equal(ConfigStatus.Deleted, value.Status);
    }

    [Fact]
    public void GlobalValue_Delete_DesdeArchivado_TambienEsPosible()
    {
        // Archivar no libera la referencia; eliminar sí. Por eso Delete admite cualquier estado vivo.
        var value = ParameterGlobalValue.Create(
            ValidDefinitionId, EffectiveValue.Create("hello"), ParameterDataType.String, ValidActor).Value;
        value.Publish(ValidActor);
        value.Archive(ValidActor);

        var result = value.Delete(ValidActor);

        Assert.True(result.IsSuccess);
        Assert.Equal(ConfigStatus.Deleted, value.Status);
    }

    [Fact]
    public void GlobalValue_Delete_DosVeces_ReturnsFailure()
    {
        var value = ParameterGlobalValue.Create(
            ValidDefinitionId, EffectiveValue.Create("hello"), ParameterDataType.String, ValidActor).Value;
        Assert.True(value.Delete(ValidActor).IsSuccess);

        Assert.True(value.Delete(ValidActor).IsFailure);
    }

    [Fact]
    public void TenantValue_Delete_PasaAEstadoTerminal()
    {
        var value = ParameterTenantValue.Create(
            ValidTenantId,
            ValidDefinitionId,
            OverrideValue.Create("true"),
            ParameterDataType.Boolean,
            ParameterScope.GlobalAndTenant,
            ValidActor).Value;

        var result = value.Delete(ValidActor);

        Assert.True(result.IsSuccess);
        Assert.Equal(ConfigStatus.Deleted, value.Status);
    }

    [Fact]
    public void TenantValue_Delete_DosVeces_ReturnsFailure()
    {
        var value = ParameterTenantValue.Create(
            ValidTenantId,
            ValidDefinitionId,
            OverrideValue.Create("true"),
            ParameterDataType.Boolean,
            ParameterScope.GlobalAndTenant,
            ValidActor).Value;
        Assert.True(value.Delete(ValidActor).IsSuccess);

        Assert.True(value.Delete(ValidActor).IsFailure);
    }
}
