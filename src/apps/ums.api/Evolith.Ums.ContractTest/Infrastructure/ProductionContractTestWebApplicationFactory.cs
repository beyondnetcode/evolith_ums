using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
namespace Evolith.Ums.ContractTest.Infrastructure;

/// <summary>
/// G-101: variante de la factoría que arranca el host en entorno «Production»,
/// reutilizando la misma sustitución de repositorios InMemory de la base.
///
/// Sirve para verificar el blindaje del endpoint de estados de proveedor de Pact:
/// en producción, `POST /_pact/provider-states` NO debe estar registrado (404),
/// mientras que en desarrollo (factoría base) sí lo está para que el proveedor
/// de Pact pueda sembrar estados.
/// </summary>
public sealed class ProductionContractTestWebApplicationFactory : ContractTestWebApplicationFactory
{
    protected override string EnvironmentName => "Production";

    /// <summary>
    /// En «Production» la siembra de desarrollo se APAGA, porque el producto la prohibe ahi.
    ///
    /// <para>La factoria base pide `Persistence:SeedDevData=true` —lo necesita para tener datos—, y
    /// heredarlo tal cual hacia que este host ni siquiera arrancara: el arranque lanza «SeedDevData
    /// esta en true con ASPNETCORE_ENVIRONMENT=Production. El dataset de desarrollo trae credenciales
    /// por defecto y no puede sembrarse en produccion». La guarda del producto tiene razon; lo que
    /// estaba mal era pedirle a un host de produccion que sembrara datos de desarrollo.</para>
    ///
    /// <para>Y esta prueba no necesita datos: comprueba que `POST /_pact/provider-states` NO esta
    /// registrado, o sea que la ruta no existe. Nadie consulta nada.</para>
    ///
    /// <para>Llevaba roto sin que ninguna puerta lo dijera, porque el `pre-push` compilaba estas
    /// suites pero no las ejecutaba ([[G-200]]).</para>
    /// </summary>
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        // Se anade DESPUES de la base, asi que gana sobre su valor y sobre la variable de entorno.
        //
        // El transporte de notificaciones va por la misma razon que SeedDevData (G-270): en
        // Production el arranque RECHAZA `Notifications:Provider=Simulated`, porque ese adaptador
        // no entrega nada y ademas vuelca el cuerpo —tokens, contrasenas temporales— al log. Un
        // host de produccion tiene que declarar su relay, y este lo es aunque nunca envie: aqui se
        // declara uno sintetico, que nadie usa porque esta prueba solo mira si una ruta existe.
        builder.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Persistence:SeedDevData"] = bool.FalseString,
                ["Notifications:Provider"] = "Smtp",
                ["Notifications:Smtp:Host"] = "relay.invalido.test",
                ["Notifications:Smtp:User"] = "apikey",
                ["Notifications:Smtp:Password"] = "no-es-una-clave-real",
                ["Notifications:Smtp:FromAddress"] = "no-responder@evolith.example",
            }));
    }
}
