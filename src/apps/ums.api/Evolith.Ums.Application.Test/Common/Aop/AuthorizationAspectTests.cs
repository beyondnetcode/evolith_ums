#pragma warning disable S1186
using System;
using System.Reflection;
using BeyondNetCode.Shell.Aop;
using Evolith.Ums.Application.Common.Aop;
using Evolith.Ums.Application.Common.Interfaces;
using FluentAssertions;
using Moq;
using Xunit;

namespace Evolith.Ums.Application.Test.Common.Aop;

public class AuthorizationAspectTests
{
    private readonly Mock<IUserContext> _userContextMock;
    private readonly AuthorizationAspect _sut;
    private readonly Mock<IJoinPoint> _joinPointMock;

    public AuthorizationAspectTests()
    {
        _userContextMock = new Mock<IUserContext>();
        _sut = new AuthorizationAspect(_userContextMock.Object);
        _joinPointMock = new Mock<IJoinPoint>();

        var targetType = typeof(CreateUserCommandHandler);
        _joinPointMock.Setup(j => j.TargetType).Returns(targetType);
        _joinPointMock.Setup(j => j.MethodInfo).Returns(targetType.GetMethod(nameof(CreateUserCommandHandler.Handle))!);
    }

    // ---------------------------------------------------------------------
    // Camino permitido (cobertura preexistente)
    // ---------------------------------------------------------------------

    // Antes esta prueba afirmaba que sin atributo se PROCEDE, y ahí estaba la asimetría: la rama de
    // «el atributo está pero el permiso no se pudo determinar» ya denegaba (G-098), y esta dejaba
    // pasar. Si el aspecto llega a ejecutarse es porque el pointcut encontró `[AuthorizationAspect]`
    // en el método --intención explícita de proteger--, así que no poder resolverlo después es un
    // fallo del andamiaje, no un permiso concedido.
    [Fact]
    public void Apply_WhenNoAttribute_DeniesAndDoesNotProceed()
    {
        // Arrange
        var targetType = typeof(UnprotectedCommandHandler);
        _joinPointMock.Setup(j => j.TargetType).Returns(targetType);
        _joinPointMock.Setup(j => j.MethodInfo).Returns(targetType.GetMethod(nameof(UnprotectedCommandHandler.Handle))!);

        // Act
        var acto = () => _sut.Apply(_joinPointMock.Object);

        // Assert
        // G-280: 403, no 401. La excepcion propia es lo que distingue «no puedes» de «no te has identificado».
        acto.Should().Throw<AccesoDenegadoException>();
        _joinPointMock.Verify(j => j.Proceed(), Times.Never);
    }

    [Fact]
    public void Apply_WithAttributeAndPermission_Proceeds()
    {
        // Arrange
        // Simular que Shell.Aop provee el atributo
        _userContextMock.Setup(u => u.HasPermission("user:create")).Returns(true);

        // Act
        _sut.Apply(_joinPointMock.Object);

        // Assert
        _joinPointMock.Verify(j => j.Proceed(), Times.Once);
    }

    // ---------------------------------------------------------------------
    // Ruta de DENEGACIÓN (G-080). El aspecto es el punto de control de
    // acceso en runtime: si falta el permiso requerido debe DENEGAR
    // lanzando AccesoDenegadoException (403) y NO invocar Proceed().
    // Evidencia del mecanismo real de negativa:
    //   AuthorizationAspect.cs → throw new AccesoDenegadoException(...)
    // ---------------------------------------------------------------------

    [Fact]
    public void Apply_WithAttributeAndMissingPermission_DeniesAndDoesNotProceed()
    {
        // Arrange
        // CreateUserCommandHandler lleva [AuthorizationAspect]; el aspecto
        // infiere el permiso "user:create" por convención de nombre.
        // El contexto NO concede ese permiso (HasPermission → false por defecto).
        _userContextMock.Setup(u => u.HasPermission("user:create")).Returns(false);

        // Act
        Action act = () => _sut.Apply(_joinPointMock.Object);

        // Assert: deniega con AccesoDenegadoException y no procede al handler.
        act.Should().Throw<AccesoDenegadoException>()
            .WithMessage("*user:create*");
        _joinPointMock.Verify(j => j.Proceed(), Times.Never);
    }

    [Fact]
    public void Apply_WithGrantForUnrelatedPermission_DeniesRequiredAction()
    {
        // Arrange
        // El modelo de permisos (UserContext.HasPermission) es allow-list puro
        // (grant-only, default-deny): no existe precedencia "deny-wins" porque no
        // hay lista de denegación explícita. La propiedad de seguridad equivalente
        // y verificable aquí es que poseer un permiso NO relacionado ("user:read")
        // jamás autoriza una acción distinta ("user:create"): no hay fuga de
        // privilegios entre permisos.
        _userContextMock.Setup(u => u.HasPermission("user:read")).Returns(true);
        _userContextMock.Setup(u => u.HasPermission("user:create")).Returns(false);

        // Act
        Action act = () => _sut.Apply(_joinPointMock.Object);

        // Assert
        act.Should().Throw<AccesoDenegadoException>()
            .WithMessage("*user:create*");
        _joinPointMock.Verify(j => j.Proceed(), Times.Never);
    }

    [Fact]
    public void Apply_WithExplicitResourceActionAndMissingPermission_Denies()
    {
        // Arrange
        // Handler con permiso EXPLÍCITO [AuthorizationAspect("branch", "delete")]
        // (ejercita la rama de códigos explícitos de AuthorizationAspect.cs:41-42,
        // distinta de la inferencia por convención). Simula además el aislamiento
        // de scope: el contexto posee un permiso de OTRO recurso/rama ("user:create")
        // pero eso no autoriza "branch:delete".
        var targetType = typeof(CloseBranchCommandHandler);
        _joinPointMock.Setup(j => j.TargetType).Returns(targetType);
        _joinPointMock.Setup(j => j.MethodInfo).Returns(targetType.GetMethod(nameof(CloseBranchCommandHandler.Handle))!);

        _userContextMock.Setup(u => u.HasPermission("user:create")).Returns(true);   // permiso de otro recurso/rama
        _userContextMock.Setup(u => u.HasPermission("branch:delete")).Returns(false); // el requerido: no concedido

        // Act
        Action act = () => _sut.Apply(_joinPointMock.Object);

        // Assert
        act.Should().Throw<AccesoDenegadoException>()
            .WithMessage("*branch:delete*");
        _joinPointMock.Verify(j => j.Proceed(), Times.Never);
    }

    // ---------------------------------------------------------------------
    // DEFECTO DE SEGURIDAD (fail-open) — GUARDA DE REGRESIÓN (G-098, CORREGIDO).
    //
    // Si un handler lleva [AuthorizationAspect] (intención explícita de
    // protegerlo) pero el aspecto NO puede resolver el permiso —el nombre no
    // casa la convención Create/Update/Delete/Get/List y no se pasaron
    // ResourceCode/ActionCode— la implementación DEBE caer en el `else` de
    // AuthorizationAspect y DENEGAR (fail-closed): un método marcado como
    // protegido no puede ejecutarse sin control de acceso.
    //
    // Antes de G-098 el aspecto ejecutaba Proceed() SIN comprobar permiso
    // (fail-open), una vía real de escalada de privilegios. Esta prueba fija el
    // comportamiento SEGURO (debe DENEGAR y NO proceder) y actúa como testigo de
    // no-regresión. NO debe "arreglarse" debilitando la aserción.
    // ---------------------------------------------------------------------

    [Fact]
    public void Apply_WithAttributeButUninferrablePermission_ShouldDenyButFailsOpen()
    {
        // Arrange
        // ApproveBranchCommandHandler: lleva [AuthorizationAspect] sin códigos y
        // el verbo "Approve" no casa la convención → el permiso queda indeterminado.
        var targetType = typeof(ApproveBranchCommandHandler);
        _joinPointMock.Setup(j => j.TargetType).Returns(targetType);
        _joinPointMock.Setup(j => j.MethodInfo).Returns(targetType.GetMethod(nameof(ApproveBranchCommandHandler.Handle))!);

        // Act
        Action act = () => _sut.Apply(_joinPointMock.Object);

        // Assert (comportamiento SEGURO exigido: DENIEGA fail-closed y NO procede)
        act.Should().Throw<UnauthorizedAccessException>(
            "un handler explícitamente protegido cuyo permiso no puede resolverse DEBE denegarse (fail-closed)");
        _joinPointMock.Verify(j => j.Proceed(), Times.Never);
    }
}

// Clases simuladas para el test
[AuthorizationAspect]
public class CreateUserCommandHandler
{
    public void Handle() { }
}

public class UnprotectedCommandHandler
{
    public void Handle() { }
}

// Permiso explícito (recurso/acción declarados en el atributo). Es un DOBLE local del aspecto, no
// el manejador real; se renombra a «Close» para no dejar vivo el vocabulario del borrado físico que
// ADR-0164 retiró, aunque el permiso siga llamándose "branch:delete" (el verbo HTTP es DELETE).
[AuthorizationAspect("branch", "delete")]
public class CloseBranchCommandHandler
{
    public void Handle() { }
}

// Protegido pero con permiso indeterminable (verbo fuera de convención,
// sin ResourceCode/ActionCode): dispara la rama fail-open del aspecto.
[AuthorizationAspect]
public class ApproveBranchCommandHandler
{
    public void Handle() { }
}

#pragma warning restore S1186
