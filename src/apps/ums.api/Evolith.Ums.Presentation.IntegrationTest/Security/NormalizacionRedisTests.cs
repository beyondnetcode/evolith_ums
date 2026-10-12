namespace Evolith.Ums.Presentation.IntegrationTest.Security;

using FluentAssertions;
using Evolith.Ums.Infrastructure.Configuration;

/// <summary>
/// La forma del valor de Redis, que tumbó el arranque en el clúster.
///
/// El manifiesto declara `REDIS_CONNECTION` como URI —`redis://ums-redis:6379`, que es la
/// convención razonable— y ese valor se pasaba tal cual a `ConnectionMultiplexer.Connect`, que no
/// entiende el esquema. Medido el 2026-08-02 al desplegar una imagen actual: el proceso no
/// arrancaba, con una excepción que acusaba a Redis —vivo y sano— en vez de a la notación.
///
/// El defecto llevaba latente desde que se escribió el camino de código: el clúster corría una
/// imagen anterior, así que nunca se ejecutaba. Estas pruebas existen para que no vuelva a hacer
/// falta un despliegue para descubrirlo.
/// </summary>
public sealed class NormalizacionRedisTests
{
    [Theory]
    [InlineData("redis://ums-redis:6379", "ums-redis:6379")]
    [InlineData("rediss://ums-redis:6380", "ums-redis:6380")]
    [InlineData("REDIS://UMS-REDIS:6379", "UMS-REDIS:6379")]
    [InlineData("  redis://ums-redis:6379  ", "ums-redis:6379")]
    public void Una_uri_pierde_el_esquema_y_conserva_todo_lo_demas(string entrada, string esperado)
    {
        Evolith.Ums.Infrastructure.Configuration.CadenaDeRedis.Normalizar(entrada).Should().Be(esperado);
    }

    [Theory]
    [InlineData("ums-redis:6379")]
    [InlineData("localhost:6379")]
    // Las opciones van detrás del host y NO se tocan: recortar más de lo necesario cambiaría la
    // conexión en vez de solo su notación.
    [InlineData("ums-redis:6379,abortConnect=false,ssl=true")]
    public void Lo_que_ya_viene_en_la_forma_correcta_no_se_altera(string entrada)
    {
        Evolith.Ums.Infrastructure.Configuration.CadenaDeRedis.Normalizar(entrada).Should().Be(entrada);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Sin_valor_se_devuelve_tal_cual_para_que_el_llamante_caiga_al_anillo_en_memoria(string? entrada)
    {
        Evolith.Ums.Infrastructure.Configuration.CadenaDeRedis.Normalizar(entrada).Should().Be(entrada);
    }
}
