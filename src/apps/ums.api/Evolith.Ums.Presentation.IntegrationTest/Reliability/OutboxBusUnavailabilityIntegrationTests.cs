using MassTransit;
using MassTransit.EntityFrameworkCoreIntegration;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Evolith.Ums.Application.Common.Interfaces;
using Evolith.Ums.Domain.Events;
using Evolith.Ums.Infrastructure.Persistence;
using Evolith.Ums.Infrastructure.Persistence.Configuration.Entities;
using Evolith.Ums.Presentation.IntegrationTest.Infrastructure;

namespace Evolith.Ums.Presentation.IntegrationTest.Reliability;

/// <summary>
/// G-003 (sub-caso «corte del bus / backpressure»): prueba de FALLO del Transactional Outbox de
/// MassTransit cuando el bróker / servicio de entrega está INDISPONIBLE.
///
/// Verifica el invariante de resiliencia del outbox transaccional (ADR-0098 D4/D7, G-066):
///   1. Con el bus CAÍDO, publicar un evento de integración dentro de la transacción del agregado
///      ESTACIONA el mensaje en el outbox (tabla <c>OutboxMessage</c>) y NO se pierde.
///   2. El cambio del agregado quedó CONSISTENTE (commit atómico: agregado + fila de outbox).
///   3. Cuando el bus VUELVE, el servicio de entrega (<c>BusOutboxDeliveryService</c>) drena el
///      outbox: el mensaje se entrega EXACTAMENTE una vez al consumidor y la fila del outbox
///      desaparece.
///
/// A diferencia de <c>IntegrationEventOutboxDispatchTests</c> (dobles en memoria, nivel aplicación),
/// esta prueba ejercita el MECANISMO REAL: EF Core bus-outbox (<c>AddEntityFrameworkOutbox</c> +
/// <c>UseBusOutbox</c>) sobre un PostgreSQL de Testcontainers, con las tablas
/// <c>OutboxMessage</c>/<c>OutboxState</c> reales. La «indisponibilidad del bus» se modela NO
/// arrancando el host (el transporte y el servicio de entrega quedan detenidos); la «vuelta del bus»
/// se modela arrancándolo.
/// </summary>
[Collection("PostgreSql")]
public sealed class OutboxBusUnavailabilityIntegrationTests
{
    private static readonly Guid SeededTenantId = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6");

    private readonly PostgreSqlContainerFixture _fixture;

    public OutboxBusUnavailabilityIntegrationTests(PostgreSqlContainerFixture fixture)
        => _fixture = fixture;

    [Fact]
    public async Task BusDown_OutboxRetainsMessage_AndDeliversWhenBusReturns()
    {
        if (!_fixture.IsAvailable)
        {
            Assert.Skip("Docker required.");
            return;
        }

        var ct      = TestContext.Current.CancellationToken;
        var capture = new IntegrationEventCapture();

        using var host = BuildHost(_fixture.ConnectionString, capture);

        // Línea base limpia del outbox (la colección PostgreSql es secuencial; otras pruebas truncan
        // al crear su host, así que partimos de una pizarra conocida para este contenedor).
        await CleanOutboxAsync(host.Services, ct);

        var configId = Guid.NewGuid();
        var evt = new RolePromotionExecutedIntegrationEvent(
            SeededTenantId, RequestId: Guid.NewGuid(), TargetUserId: Guid.NewGuid(),
            CurrentRoleId: Guid.NewGuid(), TargetRoleId: Guid.NewGuid(), ExecutorId: Guid.NewGuid());

        // ── Fase 1: BUS CAÍDO (host sin arrancar → sin transporte ni servicio de entrega) ───────────
        // Publicar dentro de la transacción del agregado: el evento se estaciona en el outbox junto al
        // cambio del agregado y ambos se confirman atómicamente al guardar.
        using (var scope = host.Services.CreateScope())
        {
            var db              = scope.ServiceProvider.GetRequiredService<UmsPlatformDbContext>();
            var publishEndpoint = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();

            db.Set<AppConfigurationRecord>().Add(NewConfigRecord(configId));
            await publishEndpoint.Publish(evt, ct);
            await db.SaveChangesAsync(ct);
        }

        // (1) El mensaje quedó RETENIDO en el outbox (no se perdió pese a no haber bus).
        // (2) El agregado quedó CONSISTENTE (commit atómico agregado + fila de outbox).
        using (var scope = host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<UmsPlatformDbContext>();

            (await db.Set<OutboxMessage>().CountAsync(ct)).Should().BeGreaterThan(0,
                "con el bus caído el evento debe permanecer estacionado en el outbox — no se pierde");
            (await db.Set<AppConfigurationRecord>().AnyAsync(r => r.Id == configId, ct)).Should().BeTrue(
                "el cambio del agregado se confirmó atómicamente con la fila del outbox (consistencia)");
        }

        // (3) Con el bus caído no puede haberse entregado nada al consumidor.
        capture.Count.Should().Be(0, "con el bus caído no puede haberse entregado nada al consumidor");

        // ── Fase 2: EL BUS VUELVE (se arranca el host → transporte + BusOutboxDeliveryService) ───────
        await host.StartAsync(ct);
        try
        {
            // El servicio de entrega drena el outbox y publica al transporte; el consumidor lo recibe.
            await WaitUntilAsync(
                async () => capture.Count >= 1 && await OutboxMessageCountAsync(host.Services, ct) == 0,
                timeout: TimeSpan.FromSeconds(60), ct);

            capture.Count.Should().Be(1,
                "al volver el bus, el outbox entrega el evento EXACTAMENTE una vez (sin pérdida ni duplicado)");
            capture.Last.Should().NotBeNull();
            capture.Last!.RequestId.Should().Be(evt.RequestId,
                "el evento entregado es el mismo que se estacionó con el cambio del agregado");

            (await OutboxMessageCountAsync(host.Services, ct)).Should().Be(0,
                "tras la entrega el outbox queda drenado");

            // El agregado sigue presente e íntegro tras la entrega.
            using var scope = host.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<UmsPlatformDbContext>();
            (await db.Set<AppConfigurationRecord>().AnyAsync(r => r.Id == configId, ct)).Should().BeTrue(
                "la entrega del evento no altera el estado del agregado ya confirmado");
        }
        finally
        {
            await host.StopAsync(ct);
        }
    }

    // ── Infraestructura del test ────────────────────────────────────────────────────────────────────

    private static IHost BuildHost(string connectionString, IntegrationEventCapture capture) =>
        Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));
                services.AddSingleton(capture);
                services.AddScoped<ITenantContext, SystemTenantContext>();
                services.AddScoped<IPublisher, NoOpPublisher>();

                services.AddDbContext<UmsPlatformDbContext>(o =>
                    o.UseNpgsql(connectionString, sql => sql.EnableRetryOnFailure(3)));

                services.AddMassTransit(x =>
                {
                    x.AddConsumer<CapturingConsumer>();

                    // Bus-outbox EF real: los mensajes publicados por IPublishEndpoint se estacionan en el
                    // outbox con el cambio del agregado y se entregan POST-commit (UseBusOutbox()).
                    x.AddEntityFrameworkOutbox<UmsPlatformDbContext>(o =>
                    {
                        o.UsePostgres();
                        o.QueryDelay = TimeSpan.FromSeconds(1); // barrido rápido del servicio de entrega
                        o.UseBusOutbox();
                    });

                    x.UsingInMemory((context, cfg) => cfg.ConfigureEndpoints(context));
                });
            })
            .Build();

    private static AppConfigurationRecord NewConfigRecord(Guid id) => new()
    {
        Id               = id,
        TenantId         = SeededTenantId,
        SystemSuiteId    = Guid.NewGuid(),
        ModuleId         = Guid.NewGuid(),
        Code             = $"OUTBOX_G003_{id:N}"[..40],
        Value            = "outbox-bus-down",
        Description      = "Agregado sembrado por OutboxBusUnavailabilityIntegrationTests (G-003).",
        ScopeId          = 1,
        IsInheritable    = false,
        IsEncrypted      = false,
        IsNonOverridable = false,
        Version          = "1",
        StatusId         = 1,
        CreatedBy        = "00000000-0000-0000-0000-000000000123",
        CreatedAtUtc     = DateTime.UtcNow,
        AuditTimeSpan    = string.Empty,
        // RowVersion la genera PostgreSQL en el INSERT (gen_random_bytes(8), ValueGenerated.OnAdd).
    };

    private static async Task CleanOutboxAsync(IServiceProvider services, CancellationToken ct)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<UmsPlatformDbContext>();
        // Orden por las FKs: OutboxMessage → OutboxState / InboxState.
        await db.Set<OutboxMessage>().ExecuteDeleteAsync(ct);
        await db.Set<OutboxState>().ExecuteDeleteAsync(ct);
        await db.Set<InboxState>().ExecuteDeleteAsync(ct);
    }

    private static async Task<int> OutboxMessageCountAsync(IServiceProvider services, CancellationToken ct)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<UmsPlatformDbContext>();
        return await db.Set<OutboxMessage>().CountAsync(ct);
    }

    private static async Task WaitUntilAsync(Func<Task<bool>> condition, TimeSpan timeout, CancellationToken ct)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (await condition())
                return;
            await Task.Delay(250, ct);
        }
        // Última evaluación: si expira, falla con el estado real observado.
        (await condition()).Should().BeTrue(
            "el outbox debió entregar el evento y drenarse dentro del tiempo límite tras volver el bus");
    }

    private sealed class IntegrationEventCapture
    {
        private int _count;
        public int Count => Volatile.Read(ref _count);
        public RolePromotionExecutedIntegrationEvent? Last { get; private set; }

        public void Record(RolePromotionExecutedIntegrationEvent evt)
        {
            Last = evt;
            Interlocked.Increment(ref _count);
        }
    }

    private sealed class CapturingConsumer(IntegrationEventCapture capture)
        : IConsumer<RolePromotionExecutedIntegrationEvent>
    {
        public Task Consume(ConsumeContext<RolePromotionExecutedIntegrationEvent> context)
        {
            capture.Record(context.Message);
            return Task.CompletedTask;
        }
    }

    private sealed class NoOpPublisher : IPublisher
    {
        public Task Publish(object notification, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification => Task.CompletedTask;
    }

    private sealed class SystemTenantContext : ITenantContext
    {
        public Guid? OrganizationId  => null;
        public Guid? OriginalTenantId => null;
        public bool  IsInternalAdmin  => true;
        public void Initialize(Guid userTenantId, bool isInternalAdmin) { }
        public void SetOrganizationId(Guid organizationId) { }
        public void EnableCrossTenantAccess() { }
        public void DisableCrossTenantAccess() { }
    }
}
