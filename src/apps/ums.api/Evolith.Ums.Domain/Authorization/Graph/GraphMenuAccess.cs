using System.Text.Json.Serialization;

namespace Evolith.Ums.Domain.Authorization.Graph;

/// <summary>
/// Navegación concedida de un módulo, como árbol recursivo.
///
/// SUSTITUYE a la cadena rígida Módulo→Menú→Submenú→Opción que se proyectaba antes. Aquella
/// recorría exactamente tres niveles literales, de modo que un nodo colgado a otra profundidad
/// —topología que el modelo admite (ADR-0090) y que la API aceptaba— se persistía, se veía en la
/// administración y NO llegaba nunca al usuario: desaparecía del grafo sin error ni traza (G-171).
///
/// Dos cambios de fondo respecto de la forma anterior:
///
///  1. **Profundidad libre.** El cliente recorre <see cref="GraphNavigationNode.Children"/> hasta
///     agotarlo, sin presuponer tres niveles.
///  2. **Una opción, una entrada.** Antes una opción con cinco acciones producía cinco filas
///     idénticas salvo el <c>actionCode</c>. Ahora es un nodo con su lista de acciones.
/// </summary>
public sealed record GraphMenuModule(
    [property: JsonIgnore] Guid Id,
    string                              Code,
    [property: JsonPropertyName("value")] string Name,
    int                                 SortOrder,
    string                              Status,
    // Identificador de icono del módulo (no un recurso). Nulo si no se configuró.
    string?                             Icon,
    IReadOnlyList<GraphNavigationNode>  Nodes);

/// <summary>
/// Nodo del árbol de navegación. <see cref="Kind"/> clasifica su papel (Menu, SubMenu, Option) sin
/// fijar su profundidad.
///
/// Solo viajan los nodos ALCANZABLES: una hoja sin ninguna acción concedida se omite, y una rama
/// que se queda sin hojas se omite con ella. La ausencia significa «no concedido», que es la misma
/// semántica fail-closed que ya regía (G-039); enviar decenas de filas <c>NotGranted</c> era
/// repetir en cada login lo que el contrato ya dice una vez.
/// </summary>
public sealed record GraphNavigationNode(
    [property: JsonIgnore] Guid Id,
    string                              Code,
    [property: JsonPropertyName("value")] string Name,
    string                              Kind,
    int                                 SortOrder,
    /// <summary>Identificador de icono, no un recurso: el catálogo gráfico lo decide el cliente.</summary>
    string?                             Icon,
    /// <summary>Ruta destino. Sin ella el cliente sabe qué pintar pero no a dónde llevar.</summary>
    string?                             Route,
    IReadOnlyList<GraphNodeAction>      Actions,
    IReadOnlyList<GraphNavigationNode>  Children);

/// <summary>
/// Una acción concedida —o denegada explícitamente— sobre un nodo hoja.
///
/// <c>Deny</c> viaja porque no es lo mismo que la ausencia: una denegación explícita gana sobre
/// cualquier concesión heredada, y el cliente necesita poder mostrarla como bloqueada en vez de
/// como inexistente.
/// </summary>
public sealed record GraphNodeAction(
    string           ActionCode,
    AccessEffect     Effect,
    PermissionSource Source);
