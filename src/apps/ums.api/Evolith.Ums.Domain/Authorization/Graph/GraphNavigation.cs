namespace Evolith.Ums.Domain.Authorization.Graph;

/// <summary>
/// Recorrido del árbol de navegación, en un solo sitio.
///
/// El árbol admite cualquier profundidad (ADR-0090). Antes cada consumidor lo recorría con tres
/// bucles anidados escritos a mano —el constructor, el emisor de claims, los cuatro serializadores,
/// los endpoints— y todos compartían el mismo defecto: perdían en silencio lo que no encajara en
/// tres niveles (G-171). Un recorrido único evita que la próxima corrección haya que aplicarla
/// siete veces.
/// </summary>
public static class GraphNavigation
{
    /// <summary>Aplana el árbol en profundidad, en el orden en que se pinta.</summary>
    public static IEnumerable<GraphNavigationNode> Flatten(IEnumerable<GraphNavigationNode> nodes)
    {
        foreach (var node in nodes)
        {
            yield return node;
            foreach (var child in Flatten(node.Children)) yield return child;
        }
    }

    /// <summary>Todos los nodos de navegación del grafo, de todos sus módulos.</summary>
    public static IEnumerable<GraphNavigationNode> AllNodes(AuthorizationGraph graph)
        => graph.MenuAccess.SelectMany(m => Flatten(m.Nodes));

    /// <summary>
    /// Pares (código de nodo, código de acción) con efecto <c>Allow</c>. Es la forma en que el
    /// resto del sistema expresa «lo que este perfil puede hacer».
    /// </summary>
    public static IEnumerable<(string Code, string ActionCode)> AllowedPairs(AuthorizationGraph graph)
        => AllNodes(graph)
            .SelectMany(n => n.Actions.Where(a => a.Effect == AccessEffect.Allow),
                        (n, a) => (n.Code, a.ActionCode));
}
