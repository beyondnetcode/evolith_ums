using System.Text.Json.Serialization;

namespace Evolith.Ums.Sdk.Client;

/// <summary>
/// El sobre TAL COMO VIAJA por el cable, devuelto por <c>POST /api/v1/client/authenticate</c>.
///
/// <para><c>Graph</c> es una <b>cadena serializada</b>, no un objeto: el formato lo elige el
/// inquilino (JSON, XML, YAML o CSV) y un objeto JSON no puede transportar XML. El SDK lo tipaba
/// como <c>AuthorizationGraph</c> y nunca había ejercido el endpoint real, así que
/// <c>parsed.Graph.SchemaVersion</c> reventaba y cualquier login a través del SDK fallaba
/// (G-207).</para>
///
/// <para><c>RequestId</c> es el identificador de traza de ASP.NET Core
/// (<c>0HN7QMV8KJLDT:00000001</c>), <b>no</b> un GUID. Tiparlo como <see cref="Guid"/> hacía que la
/// deserialización lanzara antes siquiera de llegar al grafo: dos defectos distintos que se
/// manifestaban como el mismo fallo.</para>
///
/// <para>Se distingue de <see cref="ClientAuthResult"/> a propósito: este es el cable, aquel es el
/// grafo ya interpretado. Mezclarlos fue lo que permitió que la forma real y la esperada
/// divergieran sin que nada fallara.</para>
/// </summary>
public sealed record ClientAuthEnvelope(
    [property: JsonPropertyName("token")] string Token,
    [property: JsonPropertyName("tokenType")] string TokenType,
    [property: JsonPropertyName("expiresIn")] int ExpiresInSeconds,
    [property: JsonPropertyName("issuedAt")] DateTimeOffset IssuedAt,
    [property: JsonPropertyName("format")] string Format,
    [property: JsonPropertyName("graph")] string Graph,
    [property: JsonPropertyName("requestId")] string RequestId);
