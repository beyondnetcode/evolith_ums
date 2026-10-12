namespace Evolith.Ums.Application.Test.Common.Reliability;

using Evolith.Ums.Application.Common.Interfaces;
using Evolith.Ums.Application.IGA.RolePromotion.Commands;
using Evolith.Ums.Domain.IGA;
using Evolith.Ums.Domain.Kernel;
using Evolith.Ums.Domain.Kernel.ValueObjects;
using Xunit;

using RolePromotionRequestAggregate = Evolith.Ums.Domain.IGA.RolePromotionRequest.RolePromotionRequest;

/// <summary>
/// Prueba EXPLÍCITA del despacho POST-commit de eventos de INTEGRACIÓN por el Transactional Outbox
/// (D-016 / ADR-0098 D4/D7). Complementa la cobertura indirecta de
/// <see cref="TransactionalAtomicityTests"/> (atomicidad del change set) y de
/// <c>FunctionalTransactionTests</c> (desenlace de la transacción funcional): aquí se fija, de forma
/// verificable, el invariante «no se publica un evento de integración antes del commit del agregado».
///
/// Mecanismo real que se modela (ver <c>UmsPlatformDbContext</c> + <c>MassTransitIntegrationEventPublisher</c>
/// con <c>UseBusOutbox()</c>): <c>ExecuteRolePromotionCommandHandler</c> publica el evento por el puerto
/// <see cref="IIntegrationEventPublisher"/> ANTES de <c>SaveEntitiesAsync</c>. Bajo kind/prod ese publish
/// solo ESTACIONA el mensaje en las tablas del outbox dentro del mismo change set del agregado; el
/// servicio de entrega lo despacha al consumidor DESPUÉS del commit. Si la transacción del agregado hace
/// rollback, la fila del outbox se revierte con el resto del change set y el evento NUNCA se entrega.
///
/// Limitación del harness (documentada, no oculta): las pruebas de aplicación usan dobles en memoria y
/// no ejercitan el bus-outbox EF real ni el bróker, así que el <em>timing</em> físico de la entrega no
/// es observable aquí. Se cubre el invariante al nivel donde SÍ es determinista: un doble del outbox
/// (<see cref="TransactionalOutboxSpy"/>) que estaciona en el publish y solo entrega al confirmar la
/// transacción del agregado (o descarta al hacer rollback). La entrega física post-commit sobre el
/// bus-outbox real se valida en la suite de integración con Testcontainers.
/// </summary>
public sealed class IntegrationEventOutboxDispatchTests
{
    private readonly Mock<IRolePromotionRequestRepository> _repo  = new();
    private readonly Mock<ITenantScopePolicy>              _scope = new();
    private readonly Mock<IUnitOfWork>                     _uow   = new();
    private readonly Mock<IUserContext>                    _ctx   = new();

    private readonly Guid _tenant      = Guid.NewGuid();
    private readonly Guid _target      = Guid.NewGuid();
    private readonly Guid _requester   = Guid.NewGuid();
    private readonly Guid _approver    = Guid.NewGuid();
    private readonly Guid _executor    = Guid.NewGuid();
    private readonly Guid _currentRole = Guid.NewGuid();
    private readonly Guid _targetRole  = Guid.NewGuid();

    public IntegrationEventOutboxDispatchTests()
    {
        _repo.Setup(r => r.UnitOfWork).Returns(_uow.Object);
        // Alcance de inquilino no restrictivo por defecto (administrador interno).
        _scope.Setup(s => s.ResolveQueryScope()).Returns((Guid?)null);
    }

    // =========================================================================
    // Doble del Transactional Outbox
    // =========================================================================

    /// <summary>
    /// Doble de prueba que reproduce la semántica del Transactional Outbox de MassTransit
    /// (<c>UseBusOutbox()</c>): <see cref="PublishAsync"/> solo ESTACIONA el evento dentro de la
    /// transacción del agregado; la entrega al consumidor ocurre en <see cref="CommitTransaction"/>
    /// (post-commit). Un <see cref="RollbackTransaction"/> revierte lo estacionado — nunca se entrega.
    /// El <paramref name="timeline"/> registra el orden para verificar que el publish precede al commit.
    /// </summary>
    private sealed class TransactionalOutboxSpy(IList<string> timeline) : IIntegrationEventPublisher
    {
        private readonly List<IIntegrationEvent> _staged    = [];
        private readonly List<IIntegrationEvent> _delivered = [];

        /// <summary>Eventos ya ENTREGADOS al consumidor/bróker (post-commit).</summary>
        public IReadOnlyList<IIntegrationEvent> Delivered => _delivered;

        /// <summary>Eventos aún estacionados en el outbox (pendientes de commit).</summary>
        public int StagedCount => _staged.Count;

        /// <summary>Nº de eventos ya entregados en el instante del último publish (debe ser 0: nada pre-commit).</summary>
        public int DeliveredCountAtLastPublish { get; private set; } = -1;

        public Task PublishAsync(IIntegrationEvent integrationEvent, CancellationToken cancellationToken = default)
        {
            // El outbox NO entrega aquí: estaciona el mensaje en el change set del agregado.
            DeliveredCountAtLastPublish = _delivered.Count;
            timeline.Add("estacionar");
            _staged.Add(integrationEvent);
            return Task.CompletedTask;
        }

        /// <summary>La transacción del agregado CONFIRMÓ: el servicio de entrega despacha lo estacionado.</summary>
        public void CommitTransaction()
        {
            timeline.Add("commit");
            _delivered.AddRange(_staged);
            _staged.Clear();
        }

        /// <summary>La transacción del agregado hizo ROLLBACK: las filas del outbox se revierten.</summary>
        public void RollbackTransaction()
        {
            timeline.Add("rollback");
            _staged.Clear();
        }
    }

    // =========================================================================
    // O01 — Éxito: el evento se estaciona en el outbox y se entrega SOLO tras el commit
    // =========================================================================

    /// <summary>O01: en el camino feliz, <c>ExecuteRolePromotionCommandHandler</c> publica el evento de
    /// integración (estacionado) ANTES de <c>SaveEntitiesAsync</c>, y la entrega al consumidor ocurre
    /// únicamente al confirmar la transacción del agregado. En el instante del publish —dentro de la
    /// tx— no se había entregado nada: no hay despacho a mitad de la transacción del agregado.</summary>
    [Fact]
    public async Task El_evento_de_integracion_se_estaciona_y_se_entrega_solo_tras_el_commit()
    {
        var timeline = new List<string>();
        var outbox   = new TransactionalOutboxSpy(timeline);

        ActingAs(_executor);
        var req = InApproved();
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(req);

        // El commit de la transacción del agregado (SaveEntitiesAsync) confirma el change set —agregado
        // + fila del outbox— y habilita la entrega POST-commit.
        _uow.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()))
            .Callback(() => outbox.CommitTransaction())
            .ReturnsAsync(true);

        var handler = new ExecuteRolePromotionCommandHandler(_repo.Object, _scope.Object, _ctx.Object, outbox);
        var result  = await handler.Handle(new ExecuteRolePromotionCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Executed", req.Status.Name);

        // 1) El evento se ESTACIONA antes del commit y se ENTREGA en el commit (nunca a mitad de la tx).
        Assert.Equal(["estacionar", "commit"], timeline);
        // 2) En el instante del publish (dentro de la tx) NADA se había entregado aún.
        Assert.Equal(0, outbox.DeliveredCountAtLastPublish);
        // 3) Tras el commit, el evento se entregó exactamente una vez y el outbox quedó vacío.
        Assert.Single(outbox.Delivered);
        Assert.IsType<RolePromotionExecutedIntegrationEvent>(outbox.Delivered[0]);
        Assert.Equal(0, outbox.StagedCount);
    }

    // =========================================================================
    // O02 — Rollback: si el commit del agregado falla, el evento NO se entrega
    // =========================================================================

    /// <summary>O02: si <c>SaveEntitiesAsync</c> falla (la transacción del agregado hace rollback), el
    /// evento —ya estacionado en el outbox dentro de la tx— se revierte con el resto del change set y
    /// NUNCA llega al consumidor. Es el invariante clave: no hay evento de integración sin commit del
    /// agregado.</summary>
    [Fact]
    public async Task Si_el_commit_del_agregado_falla_el_evento_de_integracion_no_se_entrega()
    {
        var timeline = new List<string>();
        var outbox   = new TransactionalOutboxSpy(timeline);

        ActingAs(_executor);
        var req = InApproved();
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(req);

        // El commit falla → rollback: las filas del outbox se revierten con el change set del agregado
        // (atomicidad del outbox transaccional, ADR-0098 D4/D7).
        _uow.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()))
            .Callback(() => outbox.RollbackTransaction())
            .ThrowsAsync(new InvalidOperationException("Fallo de infraestructura al confirmar la transacción del agregado."));

        var handler = new ExecuteRolePromotionCommandHandler(_repo.Object, _scope.Object, _ctx.Object, outbox);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new ExecuteRolePromotionCommand(Guid.NewGuid()), CancellationToken.None));

        // El evento se estacionó dentro de la tx, pero al fallar el commit NUNCA se entrega.
        Assert.Equal(["estacionar", "rollback"], timeline);
        Assert.Empty(outbox.Delivered);
        Assert.Equal(0, outbox.StagedCount);
    }

    // =========================================================================
    // O03 — Rechazo de dominio: nada se estaciona ni se entrega, ni se intenta commit
    // =========================================================================

    /// <summary>O03: cuando la operación del agregado se rechaza antes de publicar (guarda SoD de
    /// ADR-UMS-096: el ejecutor coincide con el aprobador), el handler retorna temprano — no se estaciona
    /// evento alguno en el outbox, no se entrega nada y no se intenta el commit. Confirma que el evento
    /// de integración depende de una transición de estado válida del agregado.</summary>
    [Fact]
    public async Task Si_la_operacion_del_agregado_se_rechaza_no_se_estaciona_ni_se_entrega_ningun_evento()
    {
        var timeline = new List<string>();
        var outbox   = new TransactionalOutboxSpy(timeline);

        // El ejecutor coincide con el aprobador → viola SoD (INV-RPR3 endurecida, ADR-UMS-096).
        ActingAs(_approver);
        var req = InApproved();
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(req);

        var handler = new ExecuteRolePromotionCommandHandler(_repo.Object, _scope.Object, _ctx.Object, outbox);
        var result  = await handler.Handle(new ExecuteRolePromotionCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("segregación de funciones", result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Approved", req.Status.Name); // no transiciona

        // Ni se estaciona ni se entrega evento alguno, y no se intenta commit.
        Assert.Empty(timeline);
        Assert.Empty(outbox.Delivered);
        Assert.Equal(0, outbox.StagedCount);
        _uow.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private void ActingAs(Guid userId) => _ctx.Setup(c => c.UserId).Returns(userId.ToString());

    private RolePromotionRequestAggregate NewDraft() =>
        RolePromotionRequestAggregate.Create(
            TenantId.Load(_tenant),
            UserId.Load(_target),
            UserId.Load(_requester),
            RoleId.Load(_currentRole),
            RoleId.Load(_targetRole),
            ActorId.Create(_requester.ToString())).Value;

    private RolePromotionRequestAggregate InApproved(int riskScore = 30)
    {
        var req = NewDraft();
        req.Submit(RiskScore.Create(riskScore).Value, ActorId.Create(_requester.ToString()));
        req.ConfirmEligibility(true, ActorId.Create("sistema"));
        req.ManagerApprove(UserId.Load(_approver), ActorId.Create(_approver.ToString()));
        return req;
    }
}
