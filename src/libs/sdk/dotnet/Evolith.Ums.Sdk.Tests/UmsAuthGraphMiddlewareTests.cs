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

public sealed class UmsAuthGraphMiddlewareTests
{
    [Fact]
    public async Task Middleware_Decodes_Graph_From_Bearer_And_Stores_It_On_HttpContext()
    {
        var graph = AuthGraphBuilder.ForTenant("LOGISTICS_CORE").WithScope("PURCHASE_ORDER.VIEW").Build();
        var jwt = BuildSignedJwt(graph);
        AuthorizationGraph? observed = null;

        using var host = await BuildHost(observe => observed = observe);
        using var client = host.GetTestServer().CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", jwt);

        var response = await client.GetAsync("/probe");
        response.IsSuccessStatusCode.Should().BeTrue();

        observed.Should().NotBeNull();
        observed!.Context.Tenant.Code.Should().Be("LOGISTICS_CORE");
        observed.SchemaVersion.Should().Be(SchemaVersion.Current);
    }

    [Fact]
    public async Task Middleware_Without_Bearer_Leaves_Context_Empty()
    {
        AuthorizationGraph? observed = null;
        using var host = await BuildHost(observe => observed = observe);
        using var client = host.GetTestServer().CreateClient();

        var response = await client.GetAsync("/probe");
        response.IsSuccessStatusCode.Should().BeTrue();
        observed.Should().BeNull();
    }

    [Fact]
    public async Task Middleware_RejectsExpiredGraphs_With_401_AUTH_201()
    {
        var graph = AuthGraphBuilder.ForTenant("LOGISTICS_CORE").BuildExpired();
        var jwt = BuildSignedJwt(graph);

        using var host = await BuildHost(_ => { }, opt => opt.RejectExpiredGraphs = true);
        using var client = host.GetTestServer().CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", jwt);

        var response = await client.GetAsync("/probe");
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Unauthorized);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("\"AUTH_201\"");
    }

    [Fact]
    public async Task Middleware_RejectsIncompatibleSchemaVersion_With_401_AUTH_205()
    {
        var graph = AuthGraphBuilder.ForTenant("LOGISTICS_CORE")
            .WithSchemaVersion("3.0.0")
            .Build();
        var jwt = BuildSignedJwt(graph);

        using var host = await BuildHost(_ => { });
        using var client = host.GetTestServer().CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", jwt);

        var response = await client.GetAsync("/probe");
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Unauthorized);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("\"AUTH_205\"");
    }

    [Fact]
    public async Task Middleware_Descarta_Un_Portador_Fabricado_A_Mano()
    {
        // El ataque que G-217 dejaba pasar: nadie necesita el secreto para escribir un grafo de
        // administrador; le basta con codificarlo en base64 y firmar con lo que sea.
        var falsificado = BuildSignedJwt(
            AuthGraphBuilder.ForTenant("LOGISTICS_CORE").WithScope("PURCHASE_ORDER.DELETE").Build(),
            "no-es-el-secreto-de-ums");

        AuthorizationGraph? observed = null;
        using var host = await BuildHost(observe => observed = observe);
        using var client = host.GetTestServer().CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", falsificado);

        var response = await client.GetAsync("/probe");
        response.IsSuccessStatusCode.Should().BeTrue();
        observed.Should().BeNull("un portador que no firmo UMS no aporta grafo");
    }

    [Fact]
    public async Task Middleware_Descarta_Un_Portador_Con_Alg_None()
    {
        // Confusion de algoritmo: si se acepta el `alg` que declara el propio token, declarar `none`
        // desactiva la comprobacion de firma desde fuera.
        var graph = AuthGraphBuilder.ForTenant("LOGISTICS_CORE").WithScope("PURCHASE_ORDER.DELETE").Build();
        var header = Base64UrlEncode(Encoding.UTF8.GetBytes("{\"alg\":\"none\",\"typ\":\"JWT\"}"));
        var payload = Base64UrlEncode(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { graph })));

        AuthorizationGraph? observed = null;
        using var host = await BuildHost(observe => observed = observe);
        using var client = host.GetTestServer().CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", $"{header}.{payload}.");

        var response = await client.GetAsync("/probe");
        response.IsSuccessStatusCode.Should().BeTrue();
        observed.Should().BeNull("`alg: none` no es una firma valida");
    }

    private static async Task<IHost> BuildHost(
        Action<AuthorizationGraph?> observe,
        Action<UmsAuthGraphMiddlewareOptions>? configure = null)
    {
        var builder = Host.CreateDefaultBuilder()
            .ConfigureWebHost(web => web
                .UseTestServer()
                .ConfigureServices(services =>
                {
                    services.AddUmsAuthGraphMiddleware();
                    services.Configure<UmsAuthGraphMiddlewareOptions>(o => o.SigningSecret = SecretoDePrueba);
                    if (configure is not null) services.Configure(configure);
                    services.AddRouting();
                })
                .Configure(app =>
                {
                    app.UseUmsAuthGraph();
                    app.UseRouting();
                    app.UseEndpoints(e => e.MapGet("/probe", ctx =>
                    {
                        var stored = ctx.Items.TryGetValue("UmsAuthGraph", out var raw)
                            ? raw as AuthorizationGraph
                            : null;
                        observe(stored);
                        ctx.Response.StatusCode = 200;
                        return Task.CompletedTask;
                    }));
                }));

        var host = await builder.StartAsync();
        return host;
    }

    private const string SecretoDePrueba = "secreto-de-prueba-de-al-menos-32-bytes-para-hs256";

    /// <summary>
    /// Portador firmado de verdad, como el que emite UMS.
    ///
    /// <para>Antes de G-217 esto producia un token con <c>alg: none</c> y firma vacia, y las pruebas
    /// pasaban: el middleware no miraba la firma. La prueba reflejaba el agujero en vez de
    /// detectarlo.</para>
    /// </summary>
    private static string BuildSignedJwt(AuthorizationGraph graph, string secreto = SecretoDePrueba)
    {
        var header = Base64UrlEncode(Encoding.UTF8.GetBytes("{\"alg\":\"HS256\",\"typ\":\"JWT\"}"));
        var payload = Base64UrlEncode(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { graph })));

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secreto));
        var firma = Base64UrlEncode(hmac.ComputeHash(Encoding.ASCII.GetBytes($"{header}.{payload}")));

        return $"{header}.{payload}.{firma}";
    }

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
