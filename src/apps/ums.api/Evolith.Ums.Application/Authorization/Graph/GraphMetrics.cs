namespace Evolith.Ums.Application.Authorization.Graph;

using System.Diagnostics.Metrics;

/// <summary>
/// Instrumentos del camino de construcción del grafo de autorización.
///
/// El meter `UMS.Application` ya estaba registrado en el exportador OTel, pero sin un solo
/// instrumento: el tablero sabía cuánto tarda `POST /auth/login` y nada más. Sin desglose, decidir
/// dónde optimizar es adivinar — y la recomendación más cara del informe de arquitectura (cachear
/// el catálogo de la suite) depende justamente de saber si la suite domina el coste.
///
/// Dos instrumentos, no más:
///   · <see cref="Duracion"/> — cuánto tarda construir el grafo, aislado del hash de la contraseña
///     y de la serialización. Es lo que responde «¿el desvío sobre el SLO es BCrypt o somos
///     nosotros?».
///   · <see cref="TamanoPayload"/> — cuántos bytes se emiten. Es la métrica que hace visible el
///     crecimiento del catálogo, que es lo que realmente dimensiona el payload: un perfil sin
///     permisos recibe casi lo mismo que un administrador.
///
/// El etiquetado es deliberadamente pobre en cardinalidad: código de sistema y de rol, nunca
/// identificadores de usuario o de inquilino. Una etiqueta por usuario multiplica las series
/// temporales por el número de usuarios y tumba al recolector de métricas.
/// </summary>
public static class GraphMetrics
{
    public const string MeterName = "UMS.Application";

    private static readonly Meter Meter = new(MeterName);

    public static readonly Histogram<double> Duracion = Meter.CreateHistogram<double>(
        "ums.auth_graph.build.duration",
        unit: "ms",
        description: "Tiempo de construcción del grafo de autorización, sin autenticación ni serialización.");

    public static readonly Histogram<long> TamanoPayload = Meter.CreateHistogram<long>(
        "ums.auth_graph.payload.size",
        unit: "By",
        description: "Tamaño del grafo serializado que se entrega al cliente, antes de comprimir.");
}
