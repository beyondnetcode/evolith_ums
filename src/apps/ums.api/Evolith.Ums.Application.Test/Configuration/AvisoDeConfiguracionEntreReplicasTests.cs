using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StackExchange.Redis;
using Evolith.Ums.Infrastructure.Configuration;
using Xunit;

namespace Evolith.Ums.Application.Test.Configuration;

/// <summary>
/// El aviso de cambio de configuración sale SIEMPRE, también cuando este pod no tenía nada
/// guardado de ese inquilino.
///
/// <para>POR QUÉ EXISTE. El aviso estaba condicionado a que la invalidación local hubiera quitado
/// algo, y esa condición no se cumple justo en el caso que importa: un inquilino dado de alta
/// DESPUÉS del arranque no está en la memoria del pod, así que no había nada que quitar y el aviso
/// nunca salía. Los demás pods no se enteraban de su configuración <b>nunca</b>, hasta reiniciar.
/// Medido en vivo con dos réplicas: al publicar una capacidad para un inquilino recién creado, el
/// pod que la publicó la aplicaba y el otro no.</para>
///
/// <para>La prueba mira el CANAL, no el estado local: lo que se rompió no fue el borrado —ese
/// funcionaba— sino el aviso. Comprobar que el diccionario quedó vacío habría pasado en verde con
/// el defecto puesto.</para>
/// </summary>
public sealed class AvisoDeConfiguracionEntreReplicasTests
{
    private static (RedisConfigurationCache Cache, List<string> Avisos) CrearCache()
    {
        var avisos = new List<string>();

        // `PublishAsync` y no `Publish`: el aviso se emite sin esperar respuesta para no bloquear
        // a quien escribe la configuración. Espiar el método equivocado deja la prueba en verde
        // pase lo que pase, porque la lista nunca se llena.
        var suscriptor = new Mock<ISubscriber>();
        suscriptor
            .Setup(s => s.PublishAsync(It.IsAny<RedisChannel>(), It.IsAny<RedisValue>(), It.IsAny<CommandFlags>()))
            .Callback<RedisChannel, RedisValue, CommandFlags>((canal, _, _) => avisos.Add(canal.ToString()))
            .ReturnsAsync(1L);

        var redis = new Mock<IConnectionMultiplexer>();
        redis.Setup(r => r.GetSubscriber(It.IsAny<object>())).Returns(suscriptor.Object);

        var cache = new RedisConfigurationCache(
            redis.Object,
            NullLogger<RedisConfigurationCache>.Instance,
            new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>());

        return (cache, avisos);
    }

    [Fact]
    public void Invalidar_Un_Inquilino_Que_Este_Pod_No_Conoce_Avisa_Igual()
    {
        // Es el inquilino creado sobre la marcha: este pod nunca guardó nada suyo.
        var (cache, avisos) = CrearCache();

        cache.InvalidateTenant(Guid.NewGuid());

        Assert.Contains(avisos, canal => canal.EndsWith(":tenant", StringComparison.Ordinal));
    }

    [Fact]
    public void Invalidar_Un_Inquilino_Conocido_Sigue_Avisando()
    {
        // La mitad que ya funcionaba: no se arregla una rompiendo la otra.
        var (cache, avisos) = CrearCache();
        var inquilino = Guid.NewGuid();
        cache.PopulateTenant(inquilino, []);

        cache.InvalidateTenant(inquilino);

        Assert.Contains(avisos, canal => canal.EndsWith(":tenant", StringComparison.Ordinal));
    }
}
