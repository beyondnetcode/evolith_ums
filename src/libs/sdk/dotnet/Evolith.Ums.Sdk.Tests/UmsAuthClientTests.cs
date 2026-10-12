using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Evolith.Ums.Sdk.Authorization;
using Evolith.Ums.Sdk.Authorization.Testing;
using Evolith.Ums.Sdk.Client;
using Evolith.Ums.Sdk.Contracts;
using Xunit;

namespace Evolith.Ums.Sdk.Tests;

public sealed class UmsAuthClientTests
{
    private static readonly JsonSerializerOptions JsonOpcionesDelSobre = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// El sobre REAL de <c>POST /api/v1/client/authenticate</c>, capturado de la API viva por
    /// <c>ContratoDeClientAuthenticateTests</c> y regenerable desde ahí.
    ///
    /// <para>No está escrito a mano a propósito: las pruebas de este cliente sí lo estaban, con la
    /// forma que el SDK creía correcta, y por eso pasaban mientras cualquier login real fallaba
    /// (G-207). Una prueba escrita contra lo que uno supone no comprueba nada.</para>
    /// </summary>
    private static string SobreReal()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, ".git")))
            dir = dir.Parent;

        if (dir is null) throw new InvalidOperationException("No se encontró la raíz del repositorio.");

        return File.ReadAllText(Path.Combine(dir.FullName, "src", "libs", "sdk", "contracts",
            "fixtures", "client-authenticate.envelope.json"));
    }

    [Fact]
    public async Task AuthenticateAsync_Entiende_El_Sobre_Real_Capturado_De_La_Api()
    {
        var client = CreateClient(new StubHandler(HttpStatusCode.OK, SobreReal()));

        var result = await client.AuthenticateAsync(
            new ClientAuthRequest("COMEX_ANDINA", "usuario.impo@comexandina.com.pe", "p"));

        result.IsSuccess.Should().BeTrue(because: result.ErrorMessage);
        // Se LEE la constante en vez de copiar el número. Al copiarlo, cada versión nueva ponía
        // esto en rojo por el motivo equivocado —el fixture y el SDK habían avanzado a la vez— y
        // ese rojo se lee como una regresión del cliente, que es justo lo que no es.
        result.Value.Graph.SchemaVersion.Should().Be(SchemaVersion.Current);
        result.Value.Graph.Context.Tenant.Code.Should().Be("COMEX_ANDINA");
    }

    [Fact]
    public async Task AuthenticateAsync_Formato_No_Json_Explica_El_Motivo_Real()
    {
        // Antes contestaba «falta schemaVersion», que manda a buscar el problema al sitio
        // equivocado: el grafo está completo, lo que pasa es que viene en otro formato.
        var sobre = JsonSerializer.Serialize(new
        {
            token = "T",
            tokenType = "Bearer",
            expiresIn = 60,
            issuedAt = DateTimeOffset.UtcNow,
            format = "XML",
            graph = "<graph/>",
            requestId = "0HN7QMV8KJLDT:00000001"
        });

        var result = await CreateClient(new StubHandler(HttpStatusCode.OK, sobre))
            .AuthenticateAsync(new ClientAuthRequest("X", "u", "p"));

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(UmsErrorCodes.AuthGraphMalformed);
        result.ErrorMessage.Should().Contain("XML");
    }

    private static UmsAuthClient CreateClient(HttpMessageHandler handler, UmsSdkClientOptions? options = null)
    {
        options ??= new UmsSdkClientOptions { BaseAddress = new Uri("https://ums.example.com") };
        var http = new HttpClient(handler) { BaseAddress = options.BaseAddress };
        return new UmsAuthClient(http, Options.Create(options));
    }

    [Fact]
    public async Task AuthenticateAsync_HappyPath_ReturnsTypedResult()
    {
        var graph = AuthGraphBuilder.ForTenant("LOGISTICS_CORE")
            .WithUser("ana.flores@example.com")
            .WithScope("PURCHASE_ORDER.VIEW")
            .Build();

        var payload = new
        {
            token = "TOKEN.PLACEHOLDER.SIG",
            tokenType = "Bearer",
            expiresIn = 3600,
            issuedAt = DateTimeOffset.UtcNow,
            format = "JSON",
            // Cadena, no objeto; y `requestId` es un identificador de traza, no un GUID. Escritas
            // con la forma que el SDK suponía, estas pruebas pasaban mientras cualquier login real
            // fallaba (G-207).
            graph = JsonSerializer.Serialize(graph, JsonOpcionesDelSobre),
            requestId = "0HN7QMV8KJLDT:00000001"
        };

        var handler = new StubHandler(HttpStatusCode.OK, JsonSerializer.Serialize(payload));
        var client = CreateClient(handler);

        var result = await client.AuthenticateAsync(new ClientAuthRequest("LOGISTICS_CORE", "ana.flores", "p"));

        result.IsSuccess.Should().BeTrue();
        result.Value.Token.Should().Be("TOKEN.PLACEHOLDER.SIG");
        result.Value.Graph.SchemaVersion.Should().Be(SchemaVersion.Current);
        result.Value.Graph.Context.Tenant.Code.Should().Be("LOGISTICS_CORE");
    }

    [Fact]
    public async Task AuthenticateAsync_UnsupportedMajor_ReturnsAuth205()
    {
        var graph = AuthGraphBuilder.ForTenant("LOGISTICS_CORE")
            .WithSchemaVersion("3.0.0")
            .Build();

        var payload = new
        {
            token = "T",
            tokenType = "Bearer",
            expiresIn = 60,
            issuedAt = DateTimeOffset.UtcNow,
            format = "JSON",
            // Cadena, no objeto; y `requestId` es un identificador de traza, no un GUID. Escritas
            // con la forma que el SDK suponía, estas pruebas pasaban mientras cualquier login real
            // fallaba (G-207).
            graph = JsonSerializer.Serialize(graph, JsonOpcionesDelSobre),
            requestId = "0HN7QMV8KJLDT:00000001"
        };

        var handler = new StubHandler(HttpStatusCode.OK, JsonSerializer.Serialize(payload));
        var client = CreateClient(handler);

        var result = await client.AuthenticateAsync(new ClientAuthRequest("LOGISTICS_CORE", "u", "p"));

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(UmsErrorCodes.AuthGraphSchemaUnsupported);
    }

    [Fact]
    public async Task AuthenticateAsync_ServerReturns401_ReturnsInvalidCredentials()
    {
        var handler = new StubHandler(HttpStatusCode.Unauthorized, "{}");
        var client = CreateClient(handler);

        var result = await client.AuthenticateAsync(new ClientAuthRequest("LOGISTICS_CORE", "u", "bad"));

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(UmsErrorCodes.InvalidCredentials);
    }

    [Fact]
    public async Task AuthenticateAsync_ServerReturns404_ReturnsTenantNotFound()
    {
        var handler = new StubHandler(HttpStatusCode.NotFound, "tenant not found");
        var client = CreateClient(handler);

        var result = await client.AuthenticateAsync(new ClientAuthRequest("ZZZ", "u", "p"));

        result.ErrorCode.Should().Be(UmsErrorCodes.TenantNotFound);
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string _body;

        public StubHandler(HttpStatusCode status, string body)
        {
            _status = status;
            _body = body;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(_status)
            {
                Content = new StringContent(_body, System.Text.Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        }
    }
}
