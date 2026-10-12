using System.Text.Json.Nodes;
using Evolith.Ums.Infrastructure.Persistence.Seeders;
using Evolith.Ums.Presentation.IntegrationTest.Infrastructure;

namespace Evolith.Ums.Presentation.IntegrationTest.Security;

/// <summary>
/// G-207 — el sobre que devuelve <c>POST /api/v1/client/authenticate</c>, capturado de la API real.
///
/// <para>Los dos SDK de cliente tipaban <c>graph</c> como objeto. La API lo devuelve como
/// <b>cadena serializada</b>, porque el formato lo elige el inquilino (JSON, XML, YAML o CSV) y un
/// objeto no puede transportar XML. Consecuencia: <c>parsed.graph?.schemaVersion</c> daba
/// <c>undefined</c>, el cliente devolvía <c>AUTH_204</c> y <b>cualquier login a través del SDK
/// fallaba</b>. Ningún SDK había ejercido nunca el endpoint real; sus pruebas usaban objetos
/// escritos a mano con la forma que el SDK creía correcta.</para>
///
/// <para>Por eso este fichero no comprueba la forma contra una expectativa escrita a mano: la
/// <b>captura</b> de la API viva y la deja en un fixture que consumen las pruebas de los dos SDK.
/// Si la API cambia el sobre, el fixture deja de cuadrar y esta prueba falla — que es justo lo que
/// no ocurrió cuando el sobre y los SDK divergieron.</para>
/// </summary>
public sealed class ContratoDeClientAuthenticateTests : IClassFixture<UmsApiWebApplicationFactory>
{
    private readonly HttpClient _client;

    public ContratoDeClientAuthenticateTests(UmsApiWebApplicationFactory factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
        });
    }

    /// <summary>
    /// Campos que cambian en cada ejecución, con el valor fijo que los sustituye.
    ///
    /// <para>Sin fijarlos, el fixture cambiaría en cada corrida y la comprobación de deriva no
    /// diría nada. El sustituto conserva el TIPO —una fecha sigue siendo una fecha— porque el
    /// fixture no es una foto para mirar: lo deserializan los dos SDK, y un marcador en prosa donde
    /// va una fecha los haría fallar por el fixture, no por el contrato.</para>
    ///
    /// <para>El token se sustituye además por higiene: un portador real firmado no tiene por qué
    /// quedar publicado en el repositorio.</para>
    /// </summary>
    private static readonly Dictionary<string, string> CamposVolatiles = new()
    {
        ["token"]     = "PORTADOR.FIJADO.EN.EL.FIXTURE",
        ["issuedAt"]  = "2026-01-01T00:00:00.0000000+00:00",
        ["requestId"] = "0HN7QMV8KJLDT:00000001",
    };

    [Fact]
    public async Task El_Sobre_De_Client_Authenticate_Coincide_Con_El_Fixture_Capturado()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await _client.PostAsJsonAsync("/api/v1/client/authenticate", new
        {
            tenantCode = "COMEX_ANDINA",
            username = "usuario.impo@comexandina.com.pe",
            password = CoreDevDataSeeder.EffectiveEvolithDevPassword,
        }, ct);

        var cuerpo = await response.Content.ReadAsStringAsync(ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK, because: $"Cuerpo: {cuerpo}");

        var capturado = Normalizar(cuerpo);
        var ruta = RutaDelFixture();

        if (!File.Exists(ruta))
        {
            await File.WriteAllTextAsync(ruta, capturado, ct);
            Assert.Fail($"No existía el fixture del sobre; se ha generado en {ruta}. Revísalo y publícalo.");
        }

        var comprometido = await File.ReadAllTextAsync(ruta, ct);
        capturado.Should().Be(comprometido,
            because: "el sobre de /client/authenticate cambió respecto al fixture que consumen los SDK. " +
                     "Si el cambio es intencionado, borra el fichero y vuelve a ejecutar esta prueba para regenerarlo, " +
                     "y comprueba que los clientes de los dos SDK lo siguen entendiendo (G-207).");
    }

    /// <summary>
    /// Comprobación explícita del hecho que rompía a los SDK, aparte de la comparación con el
    /// fixture: un cambio de tipo aquí debe leerse en el nombre de la prueba que falla, no
    /// deducirse de un diff de 10 000 caracteres.
    /// </summary>
    [Fact]
    public async Task El_Grafo_Viaja_Como_Cadena_Serializada_No_Como_Objeto()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await _client.PostAsJsonAsync("/api/v1/client/authenticate", new
        {
            tenantCode = "COMEX_ANDINA",
            username = "usuario.impo@comexandina.com.pe",
            password = CoreDevDataSeeder.EffectiveEvolithDevPassword,
        }, ct);

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var grafo = payload.RootElement.GetProperty("graph");

        grafo.ValueKind.Should().Be(JsonValueKind.String,
            because: "el formato lo elige el inquilino (JSON/XML/YAML/CSV) y un objeto no puede transportar XML");

        // `requestId` es el TraceIdentifier de ASP.NET Core (`0HN...:00000001`), no un GUID. El
        // cliente .NET lo tipaba como Guid y reventaba al deserializar antes siquiera de mirar el
        // grafo: dos defectos distintos que se manifestaban como el mismo fallo.
        payload.RootElement.GetProperty("requestId").ValueKind.Should().Be(JsonValueKind.String);
        Guid.TryParse(payload.RootElement.GetProperty("requestId").GetString(), out _)
            .Should().BeFalse(because: "es un identificador de traza, no un GUID; tiparlo como Guid rompe el cliente");
    }

    /// <summary>
    /// Lo volátil de DENTRO del grafo, que viaja serializado en el campo `graph`.
    ///
    /// <para>Normalizar solo el sobre no basta: el grafo lleva cuatro marcas de tiempo y el
    /// identificador del perfil, que cambian en cada ejecución (y el id, además, en cada
    /// recreación de la base sembrada). Sin fijarlos, el fixture derivaba siempre y la
    /// comprobación no distinguía un cambio de contrato de un simple reloj distinto.</para>
    /// </summary>
    private static readonly Dictionary<string, string> CamposVolatilesDelGrafo = new()
    {
        ["generatedAt"]       = "2026-01-01T00:00:00.0000000Z",
        // Lejano a propósito: un `validUntil` fijo en el pasado haría que cualquier consumidor del
        // fixture lo rechazara por caducado, y el fixture existe para probar el contrato, no la
        // caducidad — para eso está `expired-graph.json`.
        ["validUntil"]        = "2099-12-31T23:59:59.0000000Z",
        ["issuedAt"]          = "2026-01-01T00:00:00.0000000Z",
        ["sessionExpiresAt"]  = "2099-12-31T23:59:59.0000000Z",
        ["id"]                = "00000000-0000-0000-0000-000000000001",
    };

    /// <summary>Recorre el grafo y fija lo volátil, conservando el resto intacto.</summary>
    private static string NormalizarGrafo(string json)
    {
        var raiz = JsonNode.Parse(json);
        Recorrer(raiz);

        return raiz!.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });

        static void Recorrer(JsonNode? nodo)
        {
            switch (nodo)
            {
                case JsonObject obj:
                    foreach (var clave in obj.Select(par => par.Key).ToList())
                    {
                        if (CamposVolatilesDelGrafo.TryGetValue(clave, out var fijo) &&
                            obj[clave] is JsonValue)
                        {
                            obj[clave] = fijo;
                        }
                        else
                        {
                            Recorrer(obj[clave]);
                        }
                    }
                    break;

                case JsonArray arr:
                    foreach (var hijo in arr) Recorrer(hijo);
                    break;
            }
        }
    }

    /// <summary>Sustituye lo volátil por un marcador fijo y reindenta, para que el diff sea legible.</summary>
    private static string Normalizar(string cuerpo)
    {
        using var doc = JsonDocument.Parse(cuerpo);
        var campos = new Dictionary<string, object?>();

        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            if (CamposVolatiles.TryGetValue(prop.Name, out var fijo))
            {
                campos[prop.Name] = fijo;
            }
            else if (prop.Name == "graph")
            {
                campos[prop.Name] = NormalizarGrafo(prop.Value.GetString()!);
            }
            else
            {
                campos[prop.Name] = prop.Value.ValueKind switch
                {
                    JsonValueKind.String => prop.Value.GetString(),
                    JsonValueKind.Number => prop.Value.GetInt64(),
                    _ => prop.Value.ToString()
                };
            }
        }

        return JsonSerializer.Serialize(campos, new JsonSerializerOptions
        {
            WriteIndented = true,
            // Sin esto cada tilde y cada comilla del grafo salen como \uXXXX y el fixture es
            // ilegible: un fixture que nadie puede leer no se revisa, se acepta.
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });
    }

    private static string RutaDelFixture()
    {
        // `.git` se comprueba como directorio O como fichero: en un WORKTREE de git es un fichero
        // con `gitdir: ...` dentro. Mirando solo el directorio, el bucle sube hasta la cima del
        // disco y lanza. Es el gemelo de `ContratoDeProyeccionTests`, que se corrigió sin esta
        // copia (G-317): el mismo bucle, duplicado, se arregló en un sitio y no en el otro.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null
               && !Directory.Exists(Path.Combine(dir.FullName, ".git"))
               && !File.Exists(Path.Combine(dir.FullName, ".git")))
            dir = dir.Parent;

        if (dir is null) throw new InvalidOperationException("No se encontró la raíz del repositorio.");

        return Path.Combine(dir.FullName, "src", "libs", "sdk", "contracts", "fixtures",
            "client-authenticate.envelope.json");
    }
}
