namespace Evolith.Ums.Domain.Authorization.SystemSuite.MenuNode;

/// <summary>
/// Metadatos de gobernanza SDLC opcionales por nodo (ADR-0090). Todos los
/// campos son nulos por defecto: no obligan estructura donde no aporta.
/// Value object inmutable; se reemplaza entero vía <see cref="MenuNode.SetMetadata"/>.
/// </summary>
public sealed class MenuNodeMetadata : IEquatable<MenuNodeMetadata>
{
    public string? Responsable { get; }
    public string? Criticidad { get; }
    public string? ProductoImpactado { get; }
    public string? ComponenteTecnico { get; }
    public string? Dependencias { get; }
    public string? Evidencias { get; }
    public string? TrazabilidadSdlc { get; }

    private MenuNodeMetadata(
        string? responsable,
        string? criticidad,
        string? productoImpactado,
        string? componenteTecnico,
        string? dependencias,
        string? evidencias,
        string? trazabilidadSdlc)
    {
        Responsable = Normalize(responsable);
        Criticidad = Normalize(criticidad);
        ProductoImpactado = Normalize(productoImpactado);
        ComponenteTecnico = Normalize(componenteTecnico);
        Dependencias = Normalize(dependencias);
        Evidencias = Normalize(evidencias);
        TrazabilidadSdlc = Normalize(trazabilidadSdlc);
    }

    public static MenuNodeMetadata Create(
        string? responsable = null,
        string? criticidad = null,
        string? productoImpactado = null,
        string? componenteTecnico = null,
        string? dependencias = null,
        string? evidencias = null,
        string? trazabilidadSdlc = null)
        => new(responsable, criticidad, productoImpactado, componenteTecnico, dependencias, evidencias, trazabilidadSdlc);

    public static MenuNodeMetadata Empty { get; } = new(null, null, null, null, null, null, null);

    public bool IsEmpty =>
        Responsable is null &&
        Criticidad is null &&
        ProductoImpactado is null &&
        ComponenteTecnico is null &&
        Dependencias is null &&
        Evidencias is null &&
        TrazabilidadSdlc is null;

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    // G-055: value object con igualdad por valor. Antes de esto, dos instancias con el
    // mismo contenido eran distintas (Equals/GetHashCode por referencia), lo que impedía
    // usarlo en HashSet/Dictionary o deduplicar. La igualdad cubre los 7 campos ya
    // normalizados, de modo que Create() sin argumentos equivale a Empty.
    public bool Equals(MenuNodeMetadata? other) =>
        other is not null &&
        Responsable == other.Responsable &&
        Criticidad == other.Criticidad &&
        ProductoImpactado == other.ProductoImpactado &&
        ComponenteTecnico == other.ComponenteTecnico &&
        Dependencias == other.Dependencias &&
        Evidencias == other.Evidencias &&
        TrazabilidadSdlc == other.TrazabilidadSdlc;

    public override bool Equals(object? obj) => Equals(obj as MenuNodeMetadata);

    public override int GetHashCode() => HashCode.Combine(
        Responsable,
        Criticidad,
        ProductoImpactado,
        ComponenteTecnico,
        Dependencias,
        Evidencias,
        TrazabilidadSdlc);
}
