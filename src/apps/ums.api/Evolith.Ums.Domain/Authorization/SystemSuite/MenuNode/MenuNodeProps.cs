namespace Evolith.Ums.Domain.Authorization.SystemSuite.MenuNode;

/// <summary>
/// Props del nodo recursivo de navegación (ADR-0090). Adjacency list:
/// <see cref="ParentNodeId"/> nulo ⇒ hijo directo del módulo.
/// </summary>
public class MenuNodeProps : IProps
{
    public IdValueObject Id { get; private set; }
    public ModuleId ModuleId { get; private set; }
    public IdValueObject? ParentNodeId { get; private set; }
    public NodeKind Kind { get; private set; }
    public Code Code { get; private set; }
    public Name Label { get; private set; }
    public Description Description { get; private set; }
    public ModuleStatus Status { get; private set; }
    public int SortOrder { get; private set; }
    public MenuNodeMetadata Metadata { get; private set; }

    /// <summary>Icono y ruta del nodo. Nunca null: <c>MenuNodePresentation.Empty</c> representa la ausencia.</summary>
    public MenuNodePresentation Presentation { get; private set; }
    public AuditValueObject Audit { get; private set; }

    public MenuNodeProps(
        IdValueObject id,
        ModuleId moduleId,
        IdValueObject? parentNodeId,
        NodeKind kind,
        Code code,
        Name label,
        Description description,
        ModuleStatus status,
        int sortOrder,
        MenuNodeMetadata metadata,
        ActorId createdBy,
        MenuNodePresentation? presentation = null)
    {
        Id = id;
        ModuleId = moduleId;
        ParentNodeId = parentNodeId;
        Kind = kind;
        Code = code;
        Label = label;
        Description = description;
        Status = status;
        SortOrder = sortOrder;
        Metadata = metadata;
        Presentation = presentation ?? MenuNodePresentation.Empty;
        Audit = AuditValueObject.Create(createdBy.GetValue());
    }

    public MenuNodeProps WithLabel(Name label)
    {
        var clone = (MenuNodeProps)MemberwiseClone();
        clone.Label = label;
        return clone;
    }

    public MenuNodeProps WithDescription(Description description)
    {
        var clone = (MenuNodeProps)MemberwiseClone();
        clone.Description = description;
        return clone;
    }

    public MenuNodeProps WithSortOrder(int sortOrder)
    {
        var clone = (MenuNodeProps)MemberwiseClone();
        clone.SortOrder = sortOrder;
        return clone;
    }

    public MenuNodeProps WithStatus(ModuleStatus status)
    {
        var clone = (MenuNodeProps)MemberwiseClone();
        clone.Status = status;
        return clone;
    }

    public MenuNodeProps WithPresentation(MenuNodePresentation presentation)
    {
        var clone = (MenuNodeProps)MemberwiseClone();
        clone.Presentation = presentation;
        return clone;
    }

    public MenuNodeProps WithMetadata(MenuNodeMetadata metadata)
    {
        var clone = (MenuNodeProps)MemberwiseClone();
        clone.Metadata = metadata;
        return clone;
    }

    public object Clone() => MemberwiseClone();
}
