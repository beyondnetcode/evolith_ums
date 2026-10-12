using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Evolith.Ums.Sdk.Authorization;
using Evolith.Ums.Sdk.Contracts;

namespace Evolith.Ums.Sdk.Client;

/// <summary>
/// Implementación por defecto de <see cref="IUmsAuthClient"/> sobre <see cref="HttpClient"/>.
///
/// <para>Deserializa el grafo —que viaja como cadena, en el formato que eligió el inquilino— y
/// comprueba su <c>schemaVersion</c> contra la ventana de compatibilidad del SDK; un MAJOR
/// incompatible da <c>AUTH_205</c> sin exponer el contenido.</para>
/// </summary>
public sealed class UmsAuthClient : IUmsAuthClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly HttpClient _http;
    private readonly UmsSdkClientOptions _options;

    public UmsAuthClient(HttpClient http, IOptions<UmsSdkClientOptions> options)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _options = options.Value;
        if (_options.BaseAddress is null && _http.BaseAddress is null)
            throw new InvalidOperationException(
                "UmsSdkClientOptions.BaseAddress must be set, or HttpClient.BaseAddress must be configured upstream.");

        if (_http.BaseAddress is null)
            _http.BaseAddress = _options.BaseAddress;
        _http.Timeout = _options.Timeout;
    }

    public async Task<Result<ClientAuthResult>> AuthenticateAsync(ClientAuthRequest request, CancellationToken ct = default)
    {
        if (request is null) throw new ArgumentNullException(nameof(request));

        HttpResponseMessage response;
        try
        {
            response = await _http.PostAsJsonAsync(_options.AuthenticatePath, request, JsonOptions, ct).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            return Result<ClientAuthResult>.Failure(UmsErrorCodes.IdpCallFailed, ex.Message);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            return Result<ClientAuthResult>.Failure(UmsErrorCodes.IdpCallFailed, $"HTTP timeout after {_options.Timeout}: {ex.Message}");
        }

        if (!response.IsSuccessStatusCode)
        {
            var body = await SafeReadAsync(response, ct).ConfigureAwait(false);
            var code = MapHttpStatusToErrorCode(response.StatusCode);
            return Result<ClientAuthResult>.Failure(code, body);
        }

        ClientAuthEnvelope? sobre;
        try
        {
            sobre = await response.Content.ReadFromJsonAsync<ClientAuthEnvelope>(JsonOptions, ct).ConfigureAwait(false);
        }
        catch (JsonException ex)
        {
            return Result<ClientAuthResult>.Failure(UmsErrorCodes.AuthGraphMalformed, ex.Message);
        }

        if (sobre is null)
            return Result<ClientAuthResult>.Failure(UmsErrorCodes.AuthGraphMalformed, "Response body was null after deserialization.");

        // El grafo viene serializado en el formato que eligió el inquilino. Hay que deserializarlo
        // ANTES de mirar nada dentro: esa era la causa de que el SDK rechazara una respuesta
        // perfectamente válida.
        var formato = (sobre.Format ?? "JSON").ToUpperInvariant();
        if (formato != "JSON")
            return Result<ClientAuthResult>.Failure(UmsErrorCodes.AuthGraphMalformed,
                $"El inquilino sirve el grafo en '{formato}' y este SDK solo interpreta JSON. " +
                "Pide 'JSON' en la petición (campo 'format') o configura el inquilino para emitirlo así.");

        AuthorizationGraph? graph;
        try
        {
            graph = JsonSerializer.Deserialize<AuthorizationGraph>(sobre.Graph, JsonOptions);
        }
        catch (JsonException ex)
        {
            return Result<ClientAuthResult>.Failure(UmsErrorCodes.AuthGraphMalformed,
                $"El campo 'graph' no es JSON deserializable: {ex.Message}");
        }

        if (graph is null)
            return Result<ClientAuthResult>.Failure(UmsErrorCodes.AuthGraphMalformed,
                "El campo 'graph' se deserializó a null.");

        if (string.IsNullOrWhiteSpace(graph.SchemaVersion))
            return Result<ClientAuthResult>.Failure(UmsErrorCodes.AuthGraphSchemaMissing,
                "Server response does not carry a schemaVersion field.");

        if (!SchemaVersion.IsSupported(graph.SchemaVersion))
            return Result<ClientAuthResult>.Failure(UmsErrorCodes.AuthGraphSchemaUnsupported,
                $"Server emitted schemaVersion '{graph.SchemaVersion}' which is outside SDK compatibility " +
                $"({SchemaVersion.CompatibilityMinInclusive} ≤ x < {SchemaVersion.CompatibilityMaxExclusive}).");

        return Result<ClientAuthResult>.Success(new ClientAuthResult(
            sobre.Token, sobre.TokenType, sobre.ExpiresInSeconds, sobre.IssuedAt,
            sobre.Format, graph, sobre.RequestId));
    }

    private static async Task<string> SafeReadAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            var text = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            return string.IsNullOrWhiteSpace(text) ? response.ReasonPhrase ?? "(no body)" : text;
        }
        catch
        {
            return response.ReasonPhrase ?? "(no body)";
        }
    }

    private static string MapHttpStatusToErrorCode(System.Net.HttpStatusCode status) => status switch
    {
        System.Net.HttpStatusCode.BadRequest => UmsErrorCodes.ValidationError,
        System.Net.HttpStatusCode.NotFound => UmsErrorCodes.TenantNotFound,
        System.Net.HttpStatusCode.Unauthorized => UmsErrorCodes.InvalidCredentials,
        System.Net.HttpStatusCode.Forbidden => UmsErrorCodes.UserNotActive,
        System.Net.HttpStatusCode.Locked => UmsErrorCodes.AccountLocked,
        _ => UmsErrorCodes.IdpCallFailed
    };
}
