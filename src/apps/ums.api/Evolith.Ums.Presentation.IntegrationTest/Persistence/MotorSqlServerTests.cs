using Evolith.Ums.Contracts.MasterData;
using Evolith.Ums.Infrastructure.MasterData;
using Evolith.Ums.Infrastructure.Observability;
using Evolith.Ums.Presentation.IntegrationTest.Infrastructure;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Evolith.Ums.Presentation.IntegrationTest.Persistence;

/// <summary>
/// Lo que solo el motor puede responder ([[G-275]]): la secuencia del localizador y el upsert
/// monotónico de la proyección, ejercidos contra un SQL Server de verdad.
///
/// <para>Se OMITEN sin <c>UMS_SQLSERVER_TEST_CONNECTION</c>. Es el precio de que la imagen oficial
/// sea amd64 y no se sostenga en los equipos ARM del equipo; lo que no se paga es el silencio:
/// cuando hay servidor, estas dos piezas —las únicas que no se pueden comprobar sobre el modelo—
/// quedan protegidas contra regresión.</para>
/// </summary>
public sealed class MotorSqlServerTests : IClassFixture<SqlServerFixture>
{
    private readonly SqlServerFixture _sqlServer;

    public MotorSqlServerTests(SqlServerFixture sqlServer) => _sqlServer = sqlServer;

    [Fact]
    public async Task LaSecuenciaDelLocalizador_AvanzaConCadaLlamada()
    {
        if (!_sqlServer.IsAvailable) { Assert.Skip(_sqlServer.MotivoDeOmision); return; }

        // SQL Server no tiene `CREATE SEQUENCE IF NOT EXISTS` ni `nextval`: la existencia se
        // consulta en sys.sequences y el avance es `NEXT VALUE FOR`. Si esa rama se rompe, el
        // localizador TX-AAAA-NNNNNN deja de acuñarse y la transacción funcional pierde su
        // identificador legible —el que se le muestra al usuario cuando algo sale mal—.
        await using var contexto = _sqlServer.CrearContextoDePlataforma();
        var fabrica = new TransactionLocatorFactory(contexto);

        var primero = await fabrica.NextAsync(TestContext.Current.CancellationToken);
        var segundo = await fabrica.NextAsync(TestContext.Current.CancellationToken);

        Assert.Matches(@"^TX-\d{4}-\d{6}$", primero);
        Assert.Matches(@"^TX-\d{4}-\d{6}$", segundo);
        Assert.True(NumeroDe(segundo) > NumeroDe(primero), $"{segundo} debería ser posterior a {primero}");
    }

    [Fact]
    public async Task LaSecuenciaDelLocalizador_NoRetrocedeAunqueLaTransaccionSeRevierta()
    {
        if (!_sqlServer.IsAvailable) { Assert.Skip(_sqlServer.MotivoDeOmision); return; }

        // La propiedad que hace útil al localizador: las secuencias NO son transaccionales, así que
        // un número consumido no se devuelve al revertir. Si volviera, dos transacciones distintas
        // podrían acuñar el MISMO localizador y el identificador dejaría de identificar.
        await using var contexto = _sqlServer.CrearContextoDePlataforma();
        var fabrica = new TransactionLocatorFactory(contexto);
        var ct = TestContext.Current.CancellationToken;

        var antes = await fabrica.NextAsync(ct);

        await using (var transaccion = await contexto.Database.BeginTransactionAsync(ct))
        {
            await fabrica.NextAsync(ct);
            await transaccion.RollbackAsync(ct);
        }

        var despues = await fabrica.NextAsync(ct);

        Assert.True(NumeroDe(despues) > NumeroDe(antes) + 1,
            $"el número consumido dentro de la transacción revertida no debería reutilizarse ({antes} → {despues})");
    }

    [Fact]
    public async Task ElUpsertDeLaProyeccion_DescartaLoRezagadoYAplicaLoNuevo()
    {
        if (!_sqlServer.IsAvailable) { Assert.Skip(_sqlServer.MotivoDeOmision); return; }

        // SQL Server no tiene `ON CONFLICT`: el equivalente atómico es MERGE con WITH (HOLDLOCK) y
        // la guarda de monotonía en el WHEN MATCHED. Sin ella, un evento rezagado —que el bus puede
        // entregar fuera de orden— haría RETROCEDER la proyección de forma permanente.
        var ct = TestContext.Current.CancellationToken;
        var inquilino = Guid.NewGuid();

        await AplicarAsync(inquilino, secuencia: 5, nombre: "Quinto", ct);
        Assert.Equal(5, await VersionAsync(inquilino, ct));

        await AplicarAsync(inquilino, secuencia: 3, nombre: "Tercero — rezagado", ct);
        Assert.Equal(5, await VersionAsync(inquilino, ct));
        Assert.Equal("Quinto", await NombreAsync(inquilino, ct));

        await AplicarAsync(inquilino, secuencia: 7, nombre: "Séptimo", ct);
        Assert.Equal(7, await VersionAsync(inquilino, ct));
        Assert.Equal("Séptimo", await NombreAsync(inquilino, ct));
    }

    private async Task AplicarAsync(Guid inquilino, long secuencia, string nombre, CancellationToken ct)
    {
        await using var proyeccion = _sqlServer.CrearContextoDeProyeccion();
        var consumidor = new TenantProjectionConsumer(
            proyeccion, Microsoft.Extensions.Logging.Abstractions.NullLogger<TenantProjectionConsumer>.Instance);

        var evento = new TenantEvent
        {
            Id = Guid.NewGuid(),
            Type = "tenant.updated",
            Subject = inquilino,
            Time = DateTimeOffset.UtcNow,
            Sequence = secuencia,
            CorrelationId = Guid.NewGuid(),
            CausationId = Guid.NewGuid(),
            ProducedAt = DateTimeOffset.UtcNow,
            Data = new TenantData(inquilino, "PRUEBA", nombre, "Active", secuencia),
        };

        var contexto = new Moq.Mock<ConsumeContext<TenantEvent>>();
        contexto.SetupGet(c => c.Message).Returns(evento);
        contexto.SetupGet(c => c.CancellationToken).Returns(ct);

        await consumidor.Consume(contexto.Object);
    }

    private async Task<long> VersionAsync(Guid inquilino, CancellationToken ct)
    {
        await using var proyeccion = _sqlServer.CrearContextoDeProyeccion();
        var fila = await proyeccion.Tenants.AsNoTracking().SingleAsync(t => t.TenantId == inquilino, ct);
        return fila.Version;
    }

    private async Task<string> NombreAsync(Guid inquilino, CancellationToken ct)
    {
        await using var proyeccion = _sqlServer.CrearContextoDeProyeccion();
        var fila = await proyeccion.Tenants.AsNoTracking().SingleAsync(t => t.TenantId == inquilino, ct);
        return fila.Name;
    }

    private static long NumeroDe(string localizador) => long.Parse(localizador.Split('-')[2]);
}
