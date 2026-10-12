namespace Evolith.Ums.Application.Test.IGA;

using System;
using System.Threading;
using System.Threading.Tasks;
using Evolith.Ums.Application.Common.Interfaces;
using Evolith.Ums.Application.IGA.RolePromotion.Commands;
using Evolith.Ums.Application.IGA.Services;
using Evolith.Ums.Domain.Enums;
using Evolith.Ums.Domain.Events;
using Evolith.Ums.Domain.IGA;
using Evolith.Ums.Domain.Kernel;
using Evolith.Ums.Domain.Kernel.ValueObjects;
using Moq;
using Xunit;
using RoleMaturityStatusAggregate = Evolith.Ums.Domain.IGA.RoleMaturityStatus.RoleMaturityStatus;
using RolePromotionRequestAggregate = Evolith.Ums.Domain.IGA.RolePromotionRequest.RolePromotionRequest;

/// <summary>
/// Pruebas de los handlers de comandos de promoción de rol (IGA, ADR-UMS-093). Cubren, por handler,
/// el camino feliz y las guardas: estado inválido, no elegible → Rejected (fail-closed),
/// SoD violada → Failure y RiskScore alto → enruta a PendingSecurityReview.
/// </summary>
public class RolePromotionCommandHandlerTests
{
    private readonly Mock<IRolePromotionRequestRepository> _repo = new();
    private readonly Mock<IRoleMaturityStatusRepository> _maturityRepo = new();
    private readonly Mock<IRiskScoreCalculator> _calculator = new();
    private readonly Mock<ITenantScopePolicy> _scope = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IUserContext> _ctx = new();
    // G-094: el ejecutor publica el efecto como evento de integración al outbox; se falsea el puerto.
    private readonly Mock<IIntegrationEventPublisher> _integrationEvents = new();

    private readonly Guid _tenant = Guid.NewGuid();
    private readonly Guid _target = Guid.NewGuid();
    private readonly Guid _requester = Guid.NewGuid();
    private readonly Guid _approver = Guid.NewGuid();
    private readonly Guid _reviewer = Guid.NewGuid();
    private readonly Guid _executor = Guid.NewGuid();
    private readonly Guid _verifier = Guid.NewGuid();
    private readonly Guid _currentRole = Guid.NewGuid();
    private readonly Guid _targetRole = Guid.NewGuid();

    public RolePromotionCommandHandlerTests()
    {
        _repo.Setup(r => r.UnitOfWork).Returns(_uow.Object);
        _maturityRepo.Setup(r => r.UnitOfWork).Returns(_uow.Object);
        _uow.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        // Alcance de inquilino no restrictivo por defecto (administrador interno).
        _scope.Setup(s => s.ResolveQueryScope()).Returns((Guid?)null);
    }

    private void ActingAs(Guid userId) => _ctx.Setup(c => c.UserId).Returns(userId.ToString());

    private RolePromotionRequestAggregate NewDraft() =>
        RolePromotionRequestAggregate.Create(
            TenantId.Load(_tenant),
            UserId.Load(_target),
            UserId.Load(_requester),
            RoleId.Load(_currentRole),
            RoleId.Load(_targetRole),
            ActorId.Create(_requester.ToString())).Value;

    private RolePromotionRequestAggregate InPendingManagerApproval(int riskScore)
    {
        var req = NewDraft();
        req.Submit(RiskScore.Create(riskScore).Value, ActorId.Create(_requester.ToString()));
        req.ConfirmEligibility(true, ActorId.Create("sistema"));
        return req;
    }

    private RolePromotionRequestAggregate InApproved(int riskScore = 30)
    {
        var req = InPendingManagerApproval(riskScore);
        req.ManagerApprove(UserId.Load(_approver), ActorId.Create(_approver.ToString()));
        return req;
    }

    private RolePromotionRequestAggregate InExecuted()
    {
        var req = InApproved();
        req.Execute(UserId.Load(_executor), ActorId.Create(_executor.ToString()));
        return req;
    }

    private RoleMaturityStatusAggregate EligibleMaturity()
    {
        var status = RoleMaturityStatusAggregate.Create(
            TenantId.Load(_tenant),
            UserId.Load(_target),
            RoleId.Load(_currentRole),
            RoleMaturityLevel.Junior,
            DateTime.UtcNow.AddYears(-2),
            ActorId.Create("sistema")).Value;
        status.UpdatePerformanceScore(4.5m, ActorId.Create("sistema"));
        return status;
    }

    private RoleMaturityStatusAggregate NotEligibleMaturity()
    {
        // Desempeño 0 (por defecto) < 3.0 ⇒ no elegible.
        return RoleMaturityStatusAggregate.Create(
            TenantId.Load(_tenant),
            UserId.Load(_target),
            RoleId.Load(_currentRole),
            RoleMaturityLevel.Junior,
            DateTime.UtcNow.AddYears(-2),
            ActorId.Create("sistema")).Value;
    }

    // ── Create ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_ConSolicitanteDistintoDelObjetivo_DevuelveExito()
    {
        ActingAs(_requester);
        var handler = new CreateRolePromotionRequestCommandHandler(_repo.Object, _scope.Object, _ctx.Object);

        var result = await handler.Handle(
            new CreateRolePromotionRequestCommand(_tenant, _target, _currentRole, _targetRole),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotEqual(Guid.Empty, result.Value.RolePromotionRequestId);
        _repo.Verify(r => r.AddAsync(It.IsAny<RolePromotionRequestAggregate>(), It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Create_CuandoSolicitanteEsElObjetivo_ViolaSoD()
    {
        ActingAs(_target); // el actor (solicitante) es el objetivo
        var handler = new CreateRolePromotionRequestCommandHandler(_repo.Object, _scope.Object, _ctx.Object);

        var result = await handler.Handle(
            new CreateRolePromotionRequestCommand(_tenant, _target, _currentRole, _targetRole),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("segregación de funciones", result.Error, StringComparison.OrdinalIgnoreCase);
        _repo.Verify(r => r.AddAsync(It.IsAny<RolePromotionRequestAggregate>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Create_SinUsuarioAutenticado_DevuelveFallo()
    {
        _ctx.Setup(c => c.UserId).Returns(string.Empty);
        var handler = new CreateRolePromotionRequestCommandHandler(_repo.Object, _scope.Object, _ctx.Object);

        var result = await handler.Handle(
            new CreateRolePromotionRequestCommand(_tenant, _target, _currentRole, _targetRole),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("usuario autenticado", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    // ── Submit ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Submit_CongelaRiskScoreYAvanza()
    {
        ActingAs(_requester);
        var req = NewDraft();
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(req);
        _calculator
            .Setup(c => c.CalculateAsync(It.IsAny<RolePromotionRiskContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<RiskAssessment>.Success(new RiskAssessment(42, "iga-risk-heuristic-v1", Array.Empty<RiskFactor>())));

        var handler = new SubmitRolePromotionCommandHandler(_repo.Object, _calculator.Object, _scope.Object, _ctx.Object);
        var result = await handler.Handle(new SubmitRolePromotionCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(42, req.RiskScore!.GetValue());
        Assert.Equal("PendingEligibilityCheck", req.Status.Name);
        _repo.Verify(r => r.UpdateAsync(req, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Submit_CuandoActorEsElObjetivo_ViolaSoD()
    {
        ActingAs(_target);
        var req = NewDraft();
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(req);

        var handler = new SubmitRolePromotionCommandHandler(_repo.Object, _calculator.Object, _scope.Object, _ctx.Object);
        var result = await handler.Handle(new SubmitRolePromotionCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("segregación de funciones", result.Error, StringComparison.OrdinalIgnoreCase);
        _calculator.Verify(c => c.CalculateAsync(It.IsAny<RolePromotionRiskContext>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Submit_CuandoNoSeEncuentraLaSolicitud_DevuelveFallo()
    {
        ActingAs(_requester);
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RolePromotionRequestAggregate?)null);

        var handler = new SubmitRolePromotionCommandHandler(_repo.Object, _calculator.Object, _scope.Object, _ctx.Object);
        var result = await handler.Handle(new SubmitRolePromotionCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        // G-100: el handler ahora devuelve un código estable e idioma-agnóstico (no el texto en español)
        // para que el mapeador HTTP lo clasifique como 404 con independencia del idioma del mensaje.
        Assert.Equal(DomainErrors.IGA.RolePromotionRequestNotFound, result.Error);
    }

    // ── ConfirmEligibility (fail-closed) ─────────────────────────────────────────

    [Fact]
    public async Task ConfirmEligibility_CuandoElegible_AvanzaAAprobacionGerente()
    {
        ActingAs(_requester);
        var req = NewDraft();
        req.Submit(RiskScore.Create(30).Value, ActorId.Create(_requester.ToString()));
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(req);
        _maturityRepo
            .Setup(r => r.GetByUserAndRoleAsync(_tenant, _target, _currentRole, It.IsAny<CancellationToken>()))
            .ReturnsAsync(EligibleMaturity());

        var handler = new ConfirmRolePromotionEligibilityCommandHandler(_repo.Object, _maturityRepo.Object, _scope.Object, _ctx.Object);
        var result = await handler.Handle(new ConfirmRolePromotionEligibilityCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("PendingManagerApproval", req.Status.Name);
    }

    [Fact]
    public async Task ConfirmEligibility_CuandoNoElegible_RechazaFailClosed()
    {
        ActingAs(_requester);
        var req = NewDraft();
        req.Submit(RiskScore.Create(30).Value, ActorId.Create(_requester.ToString()));
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(req);
        _maturityRepo
            .Setup(r => r.GetByUserAndRoleAsync(_tenant, _target, _currentRole, It.IsAny<CancellationToken>()))
            .ReturnsAsync(NotEligibleMaturity());

        var handler = new ConfirmRolePromotionEligibilityCommandHandler(_repo.Object, _maturityRepo.Object, _scope.Object, _ctx.Object);
        var result = await handler.Handle(new ConfirmRolePromotionEligibilityCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsSuccess); // la transición a Rejected es un resultado válido
        Assert.Equal("Rejected", req.Status.Name);
        _maturityRepo.Verify(r => r.UpdateAsync(It.IsAny<RoleMaturityStatusAggregate>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ConfirmEligibility_CuandoNoHayEstadoDeMadurez_RechazaFailClosed()
    {
        ActingAs(_requester);
        var req = NewDraft();
        req.Submit(RiskScore.Create(30).Value, ActorId.Create(_requester.ToString()));
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(req);
        _maturityRepo
            .Setup(r => r.GetByUserAndRoleAsync(_tenant, _target, _currentRole, It.IsAny<CancellationToken>()))
            .ReturnsAsync((RoleMaturityStatusAggregate?)null);

        var handler = new ConfirmRolePromotionEligibilityCommandHandler(_repo.Object, _maturityRepo.Object, _scope.Object, _ctx.Object);
        var result = await handler.Handle(new ConfirmRolePromotionEligibilityCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Rejected", req.Status.Name);
    }

    // ── ManagerApprove (enrutamiento por RiskScore) ──────────────────────────────

    [Fact]
    public async Task ManagerApprove_ConRiesgoBajo_Aprueba()
    {
        ActingAs(_approver);
        var req = InPendingManagerApproval(riskScore: 30);
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(req);

        var handler = new ManagerApproveRolePromotionCommandHandler(_repo.Object, _scope.Object, _ctx.Object);
        var result = await handler.Handle(new ManagerApproveRolePromotionCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Approved", req.Status.Name);
    }

    [Fact]
    public async Task ManagerApprove_ConRiesgoAlto_EnrutaARevisionSeguridad()
    {
        ActingAs(_approver);
        var req = InPendingManagerApproval(riskScore: 80);
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(req);

        var handler = new ManagerApproveRolePromotionCommandHandler(_repo.Object, _scope.Object, _ctx.Object);
        var result = await handler.Handle(new ManagerApproveRolePromotionCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("PendingSecurityReview", req.Status.Name);
    }

    [Fact]
    public async Task ManagerApprove_CuandoAprobadorEsElSolicitante_ViolaSoD()
    {
        ActingAs(_requester); // el aprobador coincide con el solicitante
        var req = InPendingManagerApproval(riskScore: 30);
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(req);

        var handler = new ManagerApproveRolePromotionCommandHandler(_repo.Object, _scope.Object, _ctx.Object);
        var result = await handler.Handle(new ManagerApproveRolePromotionCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("segregación de funciones", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ManagerApprove_EnEstadoInvalido_DevuelveFallo()
    {
        ActingAs(_approver);
        var req = NewDraft(); // sigue en Draft, no en PendingManagerApproval
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(req);

        var handler = new ManagerApproveRolePromotionCommandHandler(_repo.Object, _scope.Object, _ctx.Object);
        var result = await handler.Handle(new ManagerApproveRolePromotionCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Draft", req.Status.Name);
    }

    // ── SecurityApprove ──────────────────────────────────────────────────────────

    [Fact]
    public async Task SecurityApprove_DesdeRevisionSeguridad_Aprueba()
    {
        ActingAs(_reviewer);
        var req = InPendingManagerApproval(riskScore: 80);
        req.ManagerApprove(UserId.Load(_approver), ActorId.Create(_approver.ToString())); // → PendingSecurityReview
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(req);

        var handler = new SecurityApproveRolePromotionCommandHandler(_repo.Object, _scope.Object, _ctx.Object);
        var result = await handler.Handle(new SecurityApproveRolePromotionCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Approved", req.Status.Name);
    }

    [Fact]
    public async Task SecurityApprove_CuandoRevisorEsElAprobador_ViolaSoD()
    {
        ActingAs(_approver); // el revisor coincide con el aprobador
        var req = InPendingManagerApproval(riskScore: 80);
        req.ManagerApprove(UserId.Load(_approver), ActorId.Create(_approver.ToString()));
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(req);

        var handler = new SecurityApproveRolePromotionCommandHandler(_repo.Object, _scope.Object, _ctx.Object);
        var result = await handler.Handle(new SecurityApproveRolePromotionCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("segregación de funciones", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    // ── Execute / Verify ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Execute_DesdeAprobado_Ejecuta()
    {
        ActingAs(_executor);
        var req = InApproved();
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(req);

        var handler = new ExecuteRolePromotionCommandHandler(_repo.Object, _scope.Object, _ctx.Object, _integrationEvents.Object);
        var result = await handler.Handle(new ExecuteRolePromotionCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Executed", req.Status.Name);
        // El efecto se delega al outbox: se publica exactamente un evento de integración de ejecución.
        _integrationEvents.Verify(
            p => p.PublishAsync(It.IsAny<RolePromotionExecutedIntegrationEvent>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Execute_CuandoEjecutorEsElAprobador_ViolaSoD()
    {
        // ADR-UMS-096 (INV-RPR3 endurecida): el ejecutor no puede ser el aprobador.
        ActingAs(_approver); // el ejecutor coincide con el aprobador
        var req = InApproved();
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(req);

        var handler = new ExecuteRolePromotionCommandHandler(_repo.Object, _scope.Object, _ctx.Object, _integrationEvents.Object);
        var result = await handler.Handle(new ExecuteRolePromotionCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("segregación de funciones", result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Approved", req.Status.Name); // no transiciona
        // No se ejecutó: no debe publicarse ningún efecto al outbox.
        _integrationEvents.Verify(
            p => p.PublishAsync(It.IsAny<RolePromotionExecutedIntegrationEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Verify_DesdeEjecutado_Verifica()
    {
        ActingAs(_verifier);
        var req = InExecuted();
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(req);

        var handler = new VerifyRolePromotionCommandHandler(_repo.Object, _scope.Object, _ctx.Object);
        var result = await handler.Handle(new VerifyRolePromotionCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Verified", req.Status.Name);
    }

    [Fact]
    public async Task Verify_CuandoVerificadorEsElEjecutor_ViolaSoD()
    {
        ActingAs(_executor); // el verificador coincide con el ejecutor
        var req = InExecuted();
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(req);

        var handler = new VerifyRolePromotionCommandHandler(_repo.Object, _scope.Object, _ctx.Object);
        var result = await handler.Handle(new VerifyRolePromotionCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("segregación de funciones", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    // ── Cancel ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Cancel_PorElSolicitante_Cancela()
    {
        ActingAs(_requester);
        var req = NewDraft();
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(req);

        var handler = new CancelRolePromotionCommandHandler(_repo.Object, _scope.Object, _ctx.Object);
        var result = await handler.Handle(new CancelRolePromotionCommand(Guid.NewGuid(), "Ya no aplica"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Cancelled", req.Status.Name);
    }

    [Fact]
    public async Task Cancel_PorUnTercero_DevuelveFallo()
    {
        ActingAs(_approver); // no es el solicitante
        var req = NewDraft();
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(req);

        var handler = new CancelRolePromotionCommandHandler(_repo.Object, _scope.Object, _ctx.Object);
        var result = await handler.Handle(new CancelRolePromotionCommand(Guid.NewGuid(), "Motivo"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("solicitante", result.Error, StringComparison.OrdinalIgnoreCase);
    }
}
