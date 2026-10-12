namespace Evolith.Ums.Domain.Authorization.SystemSuite.MenuNode;

/// <summary>
/// Cómo se pinta un nodo de navegación: su icono y la ruta a la que lleva.
///
/// Va aparte de <see cref="MenuNodeMetadata"/> a propósito. Esa metadata es de GOBIERNO —quién es
/// el responsable, qué criticidad tiene, a qué trazabilidad SDLC responde— y su público es el
/// auditor. Esto es presentación, y su público es el navegador. Mezclarlas obligaría a exponer al
/// cliente campos de gobierno para poder darle un icono.
///
/// Existe porque el escenario objetivo pide que el cliente construya la navegación sin llamadas
/// adicionales, y con solo código y etiqueta no puede: no sabe qué icono pintar ni a dónde llevar
/// al usuario al pulsar.
/// </summary>
public sealed class MenuNodePresentation : IEquatable<MenuNodePresentation>
{
    /// <summary>
    /// Identificador del icono, no una URL ni un SVG: el catálogo de iconos lo decide el cliente.
    /// Guardar aquí un recurso ataría el servidor a la biblioteca gráfica de un frontend concreto.
    /// </summary>
    public string? Icon { get; }

    /// <summary>
    /// Ruta relativa a la que lleva el nodo (`/portafolio`, `/prds/:id`). Tiene sentido sobre todo
    /// en las hojas, pero un menú puede tener su propia pantalla de aterrizaje, así que no se
    /// restringe por tipo de nodo.
    /// </summary>
    public string? Route { get; }

    private MenuNodePresentation(string? icon, string? route)
    {
        Icon = Normalizar(icon);
        Route = Normalizar(route);
    }

    public static readonly MenuNodePresentation Empty = new(null, null);

    public static MenuNodePresentation Create(string? icon, string? route)
    {
        var presentacion = new MenuNodePresentation(icon, route);
        return presentacion.Icon is null && presentacion.Route is null ? Empty : presentacion;
    }

    /// <summary>Cadena vacía y solo-espacios se tratan como ausencia: «sin icono», no «icono ''».</summary>
    private static string? Normalizar(string? valor)
        => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    public bool Equals(MenuNodePresentation? other)
        => other is not null && Icon == other.Icon && Route == other.Route;

    public override bool Equals(object? obj) => Equals(obj as MenuNodePresentation);

    public override int GetHashCode() => HashCode.Combine(Icon, Route);
}
