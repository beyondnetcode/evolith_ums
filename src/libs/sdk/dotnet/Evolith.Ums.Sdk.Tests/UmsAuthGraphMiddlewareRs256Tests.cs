using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Evolith.Ums.Sdk.Authorization;
using Evolith.Ums.Sdk.Authorization.AspNetCore;
using Evolith.Ums.Sdk.Authorization.Testing;
using Evolith.Ums.Sdk.Contracts;
using Xunit;

namespace Evolith.Ums.Sdk.Tests;

/// <summary>
/// ADR-0157: UMS firma en RS256 y publica su clave publica en el JWKS. Este middleware exigia HS256
/// de forma literal —correcto contra la confusion de algoritmo (G-217), pero rechazaba todos los
/// portadores buenos—.
/// </summary>
public sealed class UmsAuthGraphMiddlewareRs256Tests
{
    private const string SecretoDePrueba = "secreto-de-prueba-de-al-menos-32-bytes-para-hs256";
    private const string Kid = "kid-de-prueba";
    private const string UriDelJwks = "https://ums.local/.well-known/jwks.json";

    [Fact]
    public async Task Acepta_El_Portador_Que_Ums_Firma_Con_Su_Clave_Privada()
    {
        using var rsa = RSA.Create(2048);
        var jwks = ServidorDeJwks(JwkDe(rsa, Kid));
        var jwt = TokenRs256(rsa, Kid);

        AuthorizationGraph? observado = null;
        using var host = await ConstruirHost(g => observado = g, SoloRs256(jwks));
        var respuesta = await Pedir(host, jwt);

        respuesta.IsSuccessStatusCode.Should().BeTrue();
        observado.Should().NotBeNull();
        observado!.Context.Tenant.Code.Should().Be("LOGISTICS_CORE");
    }

    [Fact]
    public async Task Rechaza_Uno_Firmado_Con_Otra_Clave_Aunque_Declare_El_Kid_Bueno()
    {
        using var buena = RSA.Create(2048);
        using var ajena = RSA.Create(2048);
        var jwks = ServidorDeJwks(JwkDe(buena, Kid));
        var jwt = TokenRs256(ajena, Kid);

        AuthorizationGraph? observado = null;
        using var host = await ConstruirHost(g => observado = g, SoloRs256(jwks));
        _ = await Pedir(host, jwt);

        observado.Should().BeNull("la firma no la hizo la clave que publica el emisor");
    }

    [Fact]
    public async Task Rechaza_Un_Kid_Que_No_Esta_En_El_Jwks()
    {
        using var rsa = RSA.Create(2048);
        var jwks = ServidorDeJwks(JwkDe(rsa, Kid));
        var jwt = TokenRs256(rsa, "kid-inventado");

        AuthorizationGraph? observado = null;
        using var host = await ConstruirHost(g => observado = g, SoloRs256(jwks));
        _ = await Pedir(host, jwt);

        observado.Should().BeNull("no se prueba con «alguna» clave: sin `kid` que case, se rechaza");
    }

    [Fact]
    public async Task Rechaza_Alg_None_Tambien_En_La_Via_Rs256()
    {
        using var rsa = RSA.Create(2048);
        var jwks = ServidorDeJwks(JwkDe(rsa, Kid));
        var graph = AuthGraphBuilder.ForTenant("LOGISTICS_CORE").Build();
        var cabecera = Base64Url(Encoding.UTF8.GetBytes($"{{\"alg\":\"none\",\"typ\":\"JWT\",\"kid\":\"{Kid}\"}}"));
        var cuerpo = Base64Url(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { graph })));

        AuthorizationGraph? observado = null;
        using var host = await ConstruirHost(g => observado = g, SoloRs256(jwks));
        _ = await Pedir(host, $"{cabecera}.{cuerpo}.");

        observado.Should().BeNull("`alg: none` no es una firma valida");
    }

    [Fact]
    public async Task Configurado_Para_Rs256_Un_Hs256_No_Se_Cuela()
    {
        // La degradacion que no puede ocurrir: el satelite espera RS256 y le presentan un HS256.
        using var rsa = RSA.Create(2048);
        var jwks = ServidorDeJwks(JwkDe(rsa, Kid));

        AuthorizationGraph? observado = null;
        using var host = await ConstruirHost(g => observado = g, SoloRs256(jwks));
        _ = await Pedir(host, TokenHs256(SecretoDePrueba));

        observado.Should().BeNull("configurado para RS256, un HS256 no debe aceptarse");
    }

    [Fact]
    public async Task Hs256_Sigue_Funcionando_Donde_Se_Configuro_Asi()
    {
        AuthorizationGraph? observado = null;
        using var host = await ConstruirHost(g => observado = g, o => o.SigningSecret = SecretoDePrueba);
        _ = await Pedir(host, TokenHs256(SecretoDePrueba));

        observado.Should().NotBeNull("el corte no puede ser brusco: hay despliegues que aun usan HS256");
    }

    [Fact]
    public async Task Durante_La_Transicion_Admite_Las_Dos_Vias_Y_Ninguna_Se_Relaja()
    {
        using var rsa = RSA.Create(2048);
        var jwks = ServidorDeJwks(JwkDe(rsa, Kid));

        AuthorizationGraph? observado = null;
        using var host = await ConstruirHost(g => observado = g, o =>
        {
            o.SigningSecret = SecretoDePrueba;
            o.JwksUri = UriDelJwks;
            o.Jwks.HttpClient = jwks;
        });

        _ = await Pedir(host, TokenRs256(rsa, Kid));
        observado.Should().NotBeNull("el RS256 legitimo pasa");

        observado = null;
        _ = await Pedir(host, TokenHs256(SecretoDePrueba));
        observado.Should().NotBeNull("el HS256 legitimo tambien, durante la transicion");

        observado = null;
        _ = await Pedir(host, TokenHs256("otro-secreto-igual-de-largo-pero-ajeno"));
        observado.Should().BeNull("y los rechazos siguen siendo rechazos");
    }

    [Fact]
    public async Task Sin_Material_De_Verificacion_No_Se_Acepta_Nada()
    {
        // La omision no es una via de escape: es exactamente como llego G-217.
        AuthorizationGraph? observado = null;
        using var host = await ConstruirHost(g => observado = g, _ => { });
        _ = await Pedir(host, TokenHs256(SecretoDePrueba));

        observado.Should().BeNull();
    }

    // ── La caceria del `kid` desconocido ────────────────────────────────────────────────────────

    [Fact]
    public async Task Mil_Kid_Inventados_No_Producen_Mil_Peticiones_Al_Jwks()
    {
        // Sin acotar, el refresco por `kid` desconocido es un amplificador de denegacion de
        // servicio: lo dispara un desconocido, porque el `kid` viaja en un token sin verificar.
        using var rsa = RSA.Create(2048);
        var contador = new Contador();
        using var http = new HttpClient(new ManejadorDeJwks(JwkDe(rsa, Kid), contador));
        using var cache = new CacheDeJwks(new OpcionesDeJwks
        {
            Uri = UriDelJwks,
            HttpClient = http,
            EsperaEntreRefrescos = TimeSpan.FromSeconds(60),
        });

        for (var i = 0; i < 1000; i++)
        {
            var resultado = await cache.ClaveParaAsync($"inventado-{i}");
            resultado.Estado.Should().Be(EstadoDeClave.Desconocida);
        }

        // Dos: la de arranque —la cache estaba vacia— y una unica sonda por si fuera una rotacion.
        // Lo que importa no es el numero exacto sino que este acotado por la ventana de tiempo y
        // no por el numero de tokens que mande el atacante.
        contador.Peticiones.Should().Be(2);
    }

    [Fact]
    public async Task Una_Rotacion_Se_Recoge_Sola()
    {
        using var vieja = RSA.Create(2048);
        using var nueva = RSA.Create(2048);
        var contador = new Contador();
        var manejador = new ManejadorDeJwks(JwkDe(vieja, "kid-v1"), contador);
        using var http = new HttpClient(manejador);
        using var cache = new CacheDeJwks(new OpcionesDeJwks { Uri = UriDelJwks, HttpClient = http });

        (await cache.ClaveParaAsync("kid-v1")).Estado.Should().Be(EstadoDeClave.Ok);

        // UMS rota: publica una clave nueva con otro `kid`.
        manejador.Claves = [JwkDe(vieja, "kid-v1"), JwkDe(nueva, "kid-v2")];

        (await cache.ClaveParaAsync("kid-v2")).Estado.Should().Be(EstadoDeClave.Ok);
        contador.Peticiones.Should().Be(2);
    }

    [Fact]
    public async Task La_Cache_Evita_Una_Peticion_Por_Verificacion()
    {
        using var rsa = RSA.Create(2048);
        var contador = new Contador();
        using var http = new HttpClient(new ManejadorDeJwks(JwkDe(rsa, Kid), contador));
        using var cache = new CacheDeJwks(new OpcionesDeJwks { Uri = UriDelJwks, HttpClient = http });

        for (var i = 0; i < 25; i++)
        {
            (await cache.ClaveParaAsync(Kid)).Estado.Should().Be(EstadoDeClave.Ok);
        }

        contador.Peticiones.Should().Be(1);
    }

    [Fact]
    public async Task Si_En_La_Uri_Del_Jwks_Hay_Html_Se_Rechaza_En_Vez_De_Interpretarlo()
    {
        // Ocurre de verdad: si el JWKS queda detras del comodin del frontal, la respuesta es la
        // pagina del SPA con un 200 alegre. Un 200 no es un JWKS.
        using var http = new HttpClient(new ManejadorDeHtml());
        using var cache = new CacheDeJwks(new OpcionesDeJwks { Uri = UriDelJwks, HttpClient = http });

        (await cache.ClaveParaAsync(Kid)).Estado.Should().Be(EstadoDeClave.Inaccesible);
    }

    [Fact]
    public async Task Descarta_Una_Clave_Rsa_Por_Debajo_De_2048_Bits()
    {
        // Una clave corta verifica sin error y sin aviso: si no se descarta, deja el sistema debil
        // sin que nada falle.
        using var corta = RSA.Create(1024);
        using var buena = RSA.Create(2048);
        var contador = new Contador();
        using var http = new HttpClient(new ManejadorDeJwks(JwkDe(corta, "corta"), contador)
        {
            Claves = [JwkDe(corta, "corta"), JwkDe(buena, Kid)],
        });
        using var cache = new CacheDeJwks(new OpcionesDeJwks { Uri = UriDelJwks, HttpClient = http });

        (await cache.ClaveParaAsync("corta")).Estado.Should().Be(EstadoDeClave.Desconocida);
        (await cache.ClaveParaAsync(Kid)).Estado.Should().Be(EstadoDeClave.Ok, "la buena sigue sirviendo");
    }

    // ── Arnes ───────────────────────────────────────────────────────────────────────────────────

    private static Action<UmsAuthGraphMiddlewareOptions> SoloRs256(HttpClient jwks) => o =>
    {
        // Satelite tal como debe quedar tras ADR-0157: verifica sin custodiar material de firma.
        o.JwksUri = UriDelJwks;
        o.Jwks.HttpClient = jwks;
    };

    private static HttpClient ServidorDeJwks(string jwk) =>
        new(new ManejadorDeJwks(jwk, new Contador()));

    private static async Task<HttpResponseMessage> Pedir(IHost host, string jwt)
    {
        using var client = host.GetTestServer().CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", jwt);
        return await client.GetAsync(new Uri("/probe", UriKind.Relative));
    }

    private static string JwkDe(RSA rsa, string kid)
    {
        var p = rsa.ExportParameters(includePrivateParameters: false);
        return JsonSerializer.Serialize(new
        {
            kty = "RSA",
            use = "sig",
            alg = "RS256",
            kid,
            n = Base64Url(p.Modulus!),
            e = Base64Url(p.Exponent!),
        });
    }

    private static string TokenRs256(RSA rsa, string kid)
    {
        var graph = AuthGraphBuilder.ForTenant("LOGISTICS_CORE").WithScope("PURCHASE_ORDER.VIEW").Build();
        var cabecera = Base64Url(Encoding.UTF8.GetBytes($"{{\"alg\":\"RS256\",\"typ\":\"JWT\",\"kid\":\"{kid}\"}}"));
        var cuerpo = Base64Url(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { graph })));
        var firma = rsa.SignData(
            Encoding.ASCII.GetBytes($"{cabecera}.{cuerpo}"),
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        return $"{cabecera}.{cuerpo}.{Base64Url(firma)}";
    }

    private static string TokenHs256(string secreto)
    {
        var graph = AuthGraphBuilder.ForTenant("LOGISTICS_CORE").WithScope("PURCHASE_ORDER.VIEW").Build();
        var cabecera = Base64Url(Encoding.UTF8.GetBytes("{\"alg\":\"HS256\",\"typ\":\"JWT\"}"));
        var cuerpo = Base64Url(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { graph })));
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secreto));
        return $"{cabecera}.{cuerpo}.{Base64Url(hmac.ComputeHash(Encoding.ASCII.GetBytes($"{cabecera}.{cuerpo}")))}";
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static async Task<IHost> ConstruirHost(
        Action<AuthorizationGraph?> observar,
        Action<UmsAuthGraphMiddlewareOptions> configurar)
    {
        var builder = Host.CreateDefaultBuilder()
            .ConfigureWebHost(web => web
                .UseTestServer()
                .ConfigureServices(services =>
                {
                    _ = services.AddUmsAuthGraphMiddleware();
                    _ = services.Configure(configurar);
                    _ = services.AddRouting();
                })
                .Configure(app =>
                {
                    _ = app.UseUmsAuthGraph();
                    _ = app.UseRouting();
                    _ = app.UseEndpoints(e => e.MapGet("/probe", ctx =>
                    {
                        observar(ctx.Items.TryGetValue("UmsAuthGraph", out var raw)
                            ? raw as AuthorizationGraph
                            : null);
                        ctx.Response.StatusCode = 200;
                        return Task.CompletedTask;
                    }));
                }));

        return await builder.StartAsync();
    }

    private sealed class Contador
    {
        public int Peticiones { get; set; }
    }

    /// <summary>JWKS servido en memoria, contando las peticiones para poder afirmar cuantas hubo.</summary>
    private sealed class ManejadorDeJwks(string jwk, Contador contador) : HttpMessageHandler
    {
        public IReadOnlyList<string> Claves { get; set; } = [jwk];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            contador.Peticiones++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    $"{{\"keys\":[{string.Join(',', Claves)}]}}", Encoding.UTF8, "application/json"),
            });
        }
    }

    private sealed class ManejadorDeHtml : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("<!doctype html><html><body>SPA</body></html>", Encoding.UTF8, "text/html"),
            });
    }
}
