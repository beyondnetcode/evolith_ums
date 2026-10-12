#pragma warning disable S1186
using System;
using BeyondNetCode.Shell.Aop;
using Evolith.Ums.Application.Common.Aop;
using Evolith.Ums.Application.Common.Interfaces;
using FluentAssertions;
using Moq;
using Xunit;

namespace Evolith.Ums.Application.Test.Common.Aop;

public class TenantValidationAspectTests
{
    private readonly Mock<IUserContext> _userContextMock;
    private readonly TenantValidationAspect _sut;
    private readonly Mock<IJoinPoint> _joinPointMock;

    public TenantValidationAspectTests()
    {
        _userContextMock = new Mock<IUserContext>();
        _sut = new TenantValidationAspect(_userContextMock.Object);
        _joinPointMock = new Mock<IJoinPoint>();
    }

    private void SetHandler(Type targetType, string methodName)
    {
        _joinPointMock.Setup(j => j.TargetType).Returns(targetType);
        _joinPointMock.Setup(j => j.MethodInfo).Returns(targetType.GetMethod(methodName)!);
    }

    // ---------------------------------------------------------------------
    // Camino sin opt-in: el handler NO lleva [TenantValidationAspect] → el
    // aspecto no valida y procede (el atributo se resuelve por MethodInfo).
    // ---------------------------------------------------------------------

    [Fact]
    public void Apply_WhenNoAttribute_Proceeds()
    {
        // Arrange
        SetHandler(typeof(UnscopedHandler), nameof(UnscopedHandler.Handle));
        _joinPointMock.Setup(j => j.Arguments).Returns(new object?[] { new TenantScopedRequest { TenantId = "T1" } });
        _userContextMock.Setup(u => u.TenantId).Returns("T2"); // aunque difiera, sin atributo no se valida

        // Act
        _sut.Apply(_joinPointMock.Object);

        // Assert
        _joinPointMock.Verify(j => j.Proceed(), Times.Once);
    }

    // ---------------------------------------------------------------------
    // Flujo legítimo SIN inquilino (G-102): actor global / pre-inquilino
    // (signup, creación de inquilino, admin de plataforma). El usuario carece
    // de TenantId → no hay frontera que cruzar → PROCEDE a propósito.
    // Esta prueba fija la excepción legítima: endurecer el aspecto NO debe
    // romper los flujos sin inquilino (regla de dominio, no fail-open).
    // ---------------------------------------------------------------------

    [Fact]
    public void Apply_TenantlessCaller_Proceeds()
    {
        // Arrange: atributo presente, pero el usuario no está vinculado a un inquilino.
        SetHandler(typeof(TenantScopedHandler), nameof(TenantScopedHandler.Handle));
        _joinPointMock.Setup(j => j.Arguments).Returns(new object?[] { new TenantScopedRequest { TenantId = "T1" } });
        _userContextMock.Setup(u => u.TenantId).Returns((string?)null);

        // Act
        _sut.Apply(_joinPointMock.Object);

        // Assert
        _joinPointMock.Verify(j => j.Proceed(), Times.Once);
    }

    // ---------------------------------------------------------------------
    // Camino permitido: usuario e inquilino de la petición coinciden.
    // ---------------------------------------------------------------------

    [Fact]
    public void Apply_MatchingTenant_Proceeds()
    {
        // Arrange
        SetHandler(typeof(TenantScopedHandler), nameof(TenantScopedHandler.Handle));
        _joinPointMock.Setup(j => j.Arguments).Returns(new object?[] { new TenantScopedRequest { TenantId = "T1" } });
        _userContextMock.Setup(u => u.TenantId).Returns("T1");

        // Act
        _sut.Apply(_joinPointMock.Object);

        // Assert
        _joinPointMock.Verify(j => j.Proceed(), Times.Once);
    }

    // ---------------------------------------------------------------------
    // Ruta de DENEGACIÓN por cruce de inquilino (comportamiento existente):
    // usuario de un inquilino apunta a una petición de OTRO inquilino → DENIEGA.
    // ---------------------------------------------------------------------

    [Fact]
    public void Apply_CrossTenant_DeniesAndDoesNotProceed()
    {
        // Arrange
        SetHandler(typeof(TenantScopedHandler), nameof(TenantScopedHandler.Handle));
        _joinPointMock.Setup(j => j.Arguments).Returns(new object?[] { new TenantScopedRequest { TenantId = "T2" } });
        _userContextMock.Setup(u => u.TenantId).Returns("T1");

        // Act
        Action act = () => _sut.Apply(_joinPointMock.Object);

        // Assert
        act.Should().Throw<UnauthorizedAccessException>();
        _joinPointMock.Verify(j => j.Proceed(), Times.Never);
    }

    // ---------------------------------------------------------------------
    // DEFECTO DE SEGURIDAD (soft fail-open) — GUARDA DE REGRESIÓN (G-102).
    //
    // Un handler VINCULADO a un inquilino (usuario con TenantId) marcado con
    // [TenantValidationAspect] (intención explícita de validar inquilino) cuya
    // petición NO permite determinar el inquilino objetivo (TenantId nulo/vacío,
    // o el tipo de la petición carece de la propiedad TenantId) DEBE DENEGAR
    // (fail-closed): el inquilino no puede confirmarse y dejar pasar sería un
    // cruce indebido de inquilinos.
    //
    // Antes de G-102 el aspecto solo denegaba cuando AMBOS inquilinos estaban
    // presentes y diferían; si el inquilino de la petición no podía determinarse,
    // ejecutaba Proceed() SIN validar (soft fail-open). Esta prueba fija el
    // comportamiento SEGURO (DEBE DENEGAR y NO proceder) y actúa como testigo de
    // no-regresión. NO debe "arreglarse" debilitando la aserción (p. ej. esperando
    // Proceed): eso reintroduciría el fail-open.
    // ---------------------------------------------------------------------

    [Fact]
    public void Apply_TenantBoundCallerButRequestTenantEmpty_DeniesFailClosed()
    {
        // Arrange: usuario con inquilino "T1"; la petición tiene TenantId vacío
        // (inquilino objetivo indeterminado).
        SetHandler(typeof(TenantScopedHandler), nameof(TenantScopedHandler.Handle));
        _joinPointMock.Setup(j => j.Arguments).Returns(new object?[] { new TenantScopedRequest { TenantId = "" } });
        _userContextMock.Setup(u => u.TenantId).Returns("T1");

        // Act
        Action act = () => _sut.Apply(_joinPointMock.Object);

        // Assert (comportamiento SEGURO exigido: DENIEGA fail-closed y NO procede)
        act.Should().Throw<UnauthorizedAccessException>(
            "un handler tenant-scoped cuyo inquilino objetivo no puede confirmarse DEBE denegarse (fail-closed)");
        _joinPointMock.Verify(j => j.Proceed(), Times.Never);
    }

    [Fact]
    public void Apply_TenantBoundCallerButRequestHasNoTenantIdProperty_DeniesFailClosed()
    {
        // Arrange: usuario con inquilino "T1"; el tipo de la petición NO expone
        // la propiedad TenantId → el inquilino objetivo no puede determinarse.
        SetHandler(typeof(TenantScopedHandler), nameof(TenantScopedHandler.Handle));
        _joinPointMock.Setup(j => j.Arguments).Returns(new object?[] { new NoTenantRequest { Name = "x" } });
        _userContextMock.Setup(u => u.TenantId).Returns("T1");

        // Act
        Action act = () => _sut.Apply(_joinPointMock.Object);

        // Assert (fail-closed: no hay inquilino que confirmar → DENIEGA y NO procede)
        act.Should().Throw<UnauthorizedAccessException>(
            "si la petición no expone TenantId, el inquilino no puede confirmarse y DEBE denegarse (fail-closed)");
        _joinPointMock.Verify(j => j.Proceed(), Times.Never);
    }
}

// ---------------------------------------------------------------------------
// Clases simuladas. GetAttribute (AbstractAspect) resuelve el atributo por
// MethodInfo → [TenantValidationAspect] se coloca en el MÉTODO Handle.
// ---------------------------------------------------------------------------

public class TenantScopedRequest
{
    public string? TenantId { get; set; }
}

public class NoTenantRequest
{
    public string Name { get; set; } = string.Empty;
}

// Marcado como tenant-scoped: dispara la validación de inquilino del aspecto.
public class TenantScopedHandler
{
    [TenantValidationAspect]
    public void Handle(TenantScopedRequest request) { }
}

// Sin marcar: el aspecto no valida y procede.
public class UnscopedHandler
{
    public void Handle(TenantScopedRequest request) { }
}

#pragma warning restore S1186
