namespace Evolith.Ums.Application.Test.Configuration.Parameter;

using Evolith.Ums.Application.Common.Interfaces;
using Evolith.Ums.Application.Configuration.Parameter.Commands;
using Evolith.Ums.Domain.Configuration;
using Evolith.Ums.Domain.Configuration.Parameter;
using Evolith.Ums.Domain.Configuration.Parameter.ValueObjects;
using Xunit;

/// <summary>
/// Tests de capa de aplicación del borrado LÓGICO de ParameterDefinition.
///
/// La política del propietario es que el borrado físico no existe: la fila permanece y lo que
/// cambia es su estado. Estas pruebas fijan esa política en el contrato del handler:
///   – Guarda de autenticación: sin usuario, no se toca el dominio.
///   – No encontrado: una definición inexistente —o ya eliminada, que las lecturas ocultan— da NotFound.
///   – Éxito: se marca `IsDeleted` y se persiste con UpdateAsync; NO existe DeleteAsync que llamar.
///   – Regla transaccional: con dependientes VIVOS → conflicto (409) codificado como
///     BlockedOperationError, sin tocar el agregado.
///   – Con esos mismos dependientes ya eliminados lógicamente (contadores de vivos a 0) → sí borra.
/// </summary>
public class DeleteParameterDefinitionCommandHandlerTests
{
    private readonly Mock<IParameterDefinitionRepository> _repo = new();
    private readonly Mock<IUserContext> _ctx = new();

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
            ActorId.Create("user-001")).Value;

    [Fact]
    public async Task Delete_SinDependientes_MarcaEliminadaYNoRetiraLaFila()
    {
        _ctx.Setup(u => u.UserId).Returns("user-001");
        var definition = MakeDefinition();
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(definition);
        _repo.Setup(r => r.CountLiveGlobalValuesAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(0);
        _repo.Setup(r => r.CountLiveTenantValuesAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(0);

        var handler = new DeleteParameterDefinitionCommandHandler(_repo.Object, _ctx.Object);
        var result  = await handler.Handle(new DeleteParameterDefinitionCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(definition.IsDeleted);
        Assert.False(definition.IsActive);   // una definición eliminada tampoco resuelve
        _repo.Verify(r => r.UpdateAsync(definition, It.IsAny<CancellationToken>()), Times.Once);
        _repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Delete_WhenNotFound_ReturnsFailure()
    {
        _ctx.Setup(u => u.UserId).Returns("user-001");
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((ParameterDefinition?)null);

        var handler = new DeleteParameterDefinitionCommandHandler(_repo.Object, _ctx.Object);
        var result  = await handler.Handle(new DeleteParameterDefinitionCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Common.NotFound, result.Error);
        _repo.Verify(r => r.UpdateAsync(It.IsAny<ParameterDefinition>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Delete_ConValoresGlobalesVivos_ReturnsBlockedConflict()
    {
        _ctx.Setup(u => u.UserId).Returns("user-001");
        var definition = MakeDefinition();
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(definition);
        _repo.Setup(r => r.CountLiveGlobalValuesAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(2);
        _repo.Setup(r => r.CountLiveTenantValuesAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(0);

        var handler = new DeleteParameterDefinitionCommandHandler(_repo.Object, _ctx.Object);
        var result  = await handler.Handle(new DeleteParameterDefinitionCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Configuration.ParameterHasActiveValues, result.Error);
        Assert.False(definition.IsDeleted);
        _repo.Verify(r => r.UpdateAsync(It.IsAny<ParameterDefinition>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Delete_ConValoresDeInquilinoVivos_ReturnsBlockedConflict()
    {
        _ctx.Setup(u => u.UserId).Returns("user-001");
        var definition = MakeDefinition();
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(definition);
        _repo.Setup(r => r.CountLiveGlobalValuesAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(0);
        _repo.Setup(r => r.CountLiveTenantValuesAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var handler = new DeleteParameterDefinitionCommandHandler(_repo.Object, _ctx.Object);
        var result  = await handler.Handle(new DeleteParameterDefinitionCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Configuration.ParameterHasActiveValues, result.Error);
        Assert.False(definition.IsDeleted);
        _repo.Verify(r => r.UpdateAsync(It.IsAny<ParameterDefinition>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Delete_ConDependientesYaEliminadosLogicamente_SiBorra()
    {
        _ctx.Setup(u => u.UserId).Returns("user-001");
        var definition = MakeDefinition();
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(definition);
        // Existen filas dependientes en la base, pero todas están eliminadas lógicamente: los
        // contadores de VIVOS devuelven 0 y la referencia deja de ser real → no bloquea.
        _repo.Setup(r => r.CountLiveGlobalValuesAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(0);
        _repo.Setup(r => r.CountLiveTenantValuesAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(0);

        var handler = new DeleteParameterDefinitionCommandHandler(_repo.Object, _ctx.Object);
        var result  = await handler.Handle(new DeleteParameterDefinitionCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(definition.IsDeleted);
        _repo.Verify(r => r.UpdateAsync(definition, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Delete_WhenUnauthenticated_ReturnsFailure()
    {
        _ctx.Setup(u => u.UserId).Returns("");

        var handler = new DeleteParameterDefinitionCommandHandler(_repo.Object, _ctx.Object);
        var result  = await handler.Handle(new DeleteParameterDefinitionCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("Authenticated user is required", result.Error);
    }
}
