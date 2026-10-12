namespace Evolith.Ums.Domain.Authorization.SystemSuite.MenuNode;

/// <summary>
/// Rol de un nodo de la topología de navegación (ADR-0090). Clasifica el nodo
/// sin fijar la profundidad: un <see cref="Menu"/> o <see cref="SubMenu"/> es
/// un nodo rama; una <see cref="Option"/> es un nodo hoja.
/// </summary>
public enum NodeKind
{
    Menu = 1,
    SubMenu = 2,
    Option = 3,
}
