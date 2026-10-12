using System.Text.Json.Serialization;
using Evolith.Ums.Sdk.Contracts;

namespace Evolith.Ums.Sdk.Client;

/// <summary>
/// Lo que recibe quien llama: el mismo sobre que devuelve
/// <c>POST /api/v1/client/authenticate</c>, con el grafo ya interpretado.
///
/// <para>La forma del cable vive en <see cref="ClientAuthEnvelope"/>, donde <c>graph</c> es una
/// cadena y <c>requestId</c> no es un GUID (G-207).</para>
/// </summary>
public sealed record ClientAuthResult(
    [property: JsonPropertyName("token")] string Token,
    [property: JsonPropertyName("tokenType")] string TokenType,
    [property: JsonPropertyName("expiresIn")] int ExpiresInSeconds,
    [property: JsonPropertyName("issuedAt")] DateTimeOffset IssuedAt,
    [property: JsonPropertyName("format")] string Format,
    [property: JsonPropertyName("graph")] AuthorizationGraph Graph,
    [property: JsonPropertyName("requestId")] string RequestId);
