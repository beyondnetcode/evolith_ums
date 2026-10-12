using System.Text.Json;
using System.Text.Json.Serialization;
using Evolith.Ums.Application.Authorization.Graph;
using Evolith.Ums.Application.Authorization.Graph.Serializers;
using Evolith.Ums.Domain.Authorization.Graph;

namespace Evolith.Ums.Infrastructure.Authorization.Graph;

/// <summary>
/// Serializa el <see cref="AuthorizationGraph"/> a JSON (formato por defecto).
///
/// La FORMA la define <see cref="AuthGraphPayload"/>, compartida con el login
/// web: este serializador solo elige la representación. Antes proyectaba su
/// propio modelo y por eso el mismo grafo salía distinto según el endpoint
/// (G-167).
///
/// No se ignoran los nulos: <c>branch</c>, <c>systemSuite</c>, <c>provider</c> o
/// <c>matchedCriteriaType</c> viajan explícitamente como <c>null</c>. Los
/// campos verdaderamente opcionales —los identificadores técnicos— los omite el
/// mapeador, no el serializador, para que un cliente pueda distinguir «este
/// inquilino no publica ids» de «este objeto no tiene valor».
/// </summary>
public sealed class JsonAuthorizationGraphSerializer : IAuthorizationGraphSerializer
{
    public string ContentType   => "application/json";
    public string FileExtension => "json";

    private static readonly JsonSerializerOptions _options = new()
    {
        WriteIndented = true,
        Converters    = { new JsonStringEnumConverter() },
    };

    private static readonly JsonSerializerOptions _compact = new()
    {
        WriteIndented = false,
        Converters    = { new JsonStringEnumConverter() },
    };

    public string Serialize(AuthorizationGraph graph, GraphSerializationOptions? options = null)
    {
        var opts  = options ?? GraphSerializationOptions.Default;
        var model = AuthGraphPayload.Build(graph, opts);
        var json  = JsonSerializer.Serialize(model, opts.PrettyPrint ? _options : _compact);

        // Tamaño ANTES de comprimir: es el que crece con el catálogo y el que hay que vigilar.
        // Lo que viaja por el cable lo mide la capa HTTP, y la diferencia entre ambos es
        // justamente el ahorro de la compresión.
        GraphMetrics.TamanoPayload.Record(
            System.Text.Encoding.UTF8.GetByteCount(json),
            new KeyValuePair<string, object?>("system", graph.Context.SystemSuite?.Code ?? "n/a"),
            new KeyValuePair<string, object?>("role", graph.Context.Role?.Code ?? "n/a"));

        return json;
    }
}
