namespace Evolith.Ums.Application.Test.Observability;

using Evolith.Ums.Application.Common.Interfaces;
using Evolith.Ums.Infrastructure.Observability;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

/// <summary>
/// Pruebas del modelo de transacción funcional (ADR-0096 §2.3; ADR-UMS-085). Verifican el
/// invariante del <b>desenlace</b>, la acuñación perezosa e idempotente del localizador legible
/// (ADR-UMS-084) y la reconciliación del estado con las etapas.
/// </summary>
public sealed class FunctionalTransactionTests
{
    private sealed class StubLocatorFactory : ITransactionLocatorFactory
    {
        public int Calls { get; private set; }

        public Task<string> NextAsync(CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult($"TX-2026-{Calls:D6}");
        }
    }

    private static FunctionalTransaction NewTransaction(out StubLocatorFactory factory)
    {
        factory = new StubLocatorFactory();
        return new FunctionalTransaction(
            NullLogger<FunctionalTransaction>.Instance,
            factory);
    }

    [Fact]
    public void El_localizador_no_existe_hasta_acuñarlo()
    {
        var tx = NewTransaction(out _);
        tx.Open("POST /x", actor: "ana");

        Assert.Null(tx.Locator);
    }

    [Fact]
    public async Task GetOrMintLocator_es_idempotente_y_acuña_una_sola_vez()
    {
        var tx = NewTransaction(out var factory);
        tx.Open("POST /x", actor: "ana");

        var first = await tx.GetOrMintLocatorAsync();
        var second = await tx.GetOrMintLocatorAsync();

        Assert.Equal(first, second);
        Assert.Equal("TX-2026-000001", first);
        Assert.Equal(1, factory.Calls);
        Assert.Equal(first, tx.Locator);
    }

    [Fact]
    public async Task Un_fallo_acuña_el_localizador_para_mostrarlo_al_usuario()
    {
        var tx = NewTransaction(out var factory);
        tx.Open("GET /x", actor: "ana");

        await tx.CompleteAsync(TransactionState.Failed, statusCode: 500);

        Assert.Equal(TransactionState.Failed, tx.State);
        Assert.NotNull(tx.Locator);
        Assert.Equal(1, factory.Calls);
    }

    [Fact]
    public async Task Una_lectura_exitosa_no_acuña_localizador()
    {
        var tx = NewTransaction(out var factory);
        tx.Open("GET /x", actor: "ana");

        await tx.CompleteAsync(TransactionState.Completed, statusCode: 200);

        Assert.Equal(TransactionState.Completed, tx.State);
        Assert.Null(tx.Locator);
        Assert.Equal(0, factory.Calls);
    }

    [Fact]
    public async Task Exito_nominal_con_etapa_fallida_se_reconcilia_como_parcial()
    {
        var tx = NewTransaction(out _);
        tx.Open("POST /x", actor: "ana");
        tx.RecordStage("validar", detail: "ok");
        tx.RecordStage("publicar-evento", detail: "timeout del bus", failed: true);

        await tx.CompleteAsync(TransactionState.Completed, statusCode: 200);

        Assert.Equal(TransactionState.PartiallyCompleted, tx.State);
        Assert.NotNull(tx.Locator); // parcial es no-exitoso → se acuña
    }

    [Fact]
    public async Task Exito_nominal_con_todas_las_etapas_fallidas_se_reconcilia_como_fallo()
    {
        var tx = NewTransaction(out _);
        tx.Open("POST /x", actor: "ana");
        tx.RecordStage("publicar-evento", failed: true);

        await tx.CompleteAsync(TransactionState.Completed, statusCode: 200);

        Assert.Equal(TransactionState.Failed, tx.State);
    }

    [Fact]
    public async Task CompleteAsync_es_idempotente()
    {
        var tx = NewTransaction(out var factory);
        tx.Open("GET /x", actor: "ana");

        await tx.CompleteAsync(TransactionState.Failed, statusCode: 500);
        var locatorAfterFirst = tx.Locator;
        await tx.CompleteAsync(TransactionState.Completed, statusCode: 200);

        // El segundo desenlace no altera el estado ni vuelve a acuñar.
        Assert.Equal(TransactionState.Failed, tx.State);
        Assert.Equal(locatorAfterFirst, tx.Locator);
        Assert.Equal(1, factory.Calls);
    }

    [Fact]
    public void RecordEffect_y_RecordDecision_no_lanzan()
    {
        var tx = NewTransaction(out _);
        tx.Open("POST /x", actor: "ana");

        var effect = Record.Exception(() =>
            tx.RecordEffect("message.publish", "outbox", "evt-1", EffectReversibility.PendingCompensation));
        var decision = Record.Exception(() =>
            tx.RecordDecision("aprobar", "el monto está bajo el umbral"));

        Assert.Null(effect);
        Assert.Null(decision);
    }
}
