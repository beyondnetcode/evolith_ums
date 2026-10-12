namespace Evolith.Ums.Domain.Authorization.SystemSuite.MenuNode;

/// <summary>
/// Nodo recursivo de la topología de navegación (ADR-0090). Reemplaza la cadena
/// rígida Menú→SubMenú→Opción por un árbol adjacency-list de profundidad
/// arbitraria: un módulo puede tener opciones directas o jerarquías más
/// profundas, y el submenú es opcional por naturaleza. Una <see cref="NodeKind.Option"/>
/// es un nodo hoja que vincula funcionalidades (acciones) N:M.
/// </summary>
public sealed class MenuNode : Entity<MenuNode, MenuNodeProps>
{
    private readonly List<MenuNode> _children = new();
    private readonly List<ActionCode> _actionCodes = new();

    private MenuNode(MenuNodeProps props) : base(props)
    {
    }

    public ModuleId ModuleId => Props.ModuleId;
    public IdValueObject? ParentNodeId => Props.ParentNodeId;
    public NodeKind Kind => Props.Kind;
    public Code Code => Props.Code;
    public Name Label => Props.Label;
    public Description Description => Props.Description;
    public ModuleStatus Status => Props.Status;
    public int SortOrder => Props.SortOrder;
    public MenuNodeMetadata Metadata => Props.Metadata;

    public IReadOnlyCollection<MenuNode> Children => _children.AsReadOnly();
    public IReadOnlyCollection<ActionCode> ActionCodes => _actionCodes.AsReadOnly();

    public IdValueObject GetId() => Props.Id;

    public static Result<MenuNode> Create(
        ModuleId moduleId,
        IdValueObject? parentNodeId,
        NodeKind kind,
        Code code,
        Name label,
        Description description,
        int sortOrder,
        ActorId createdBy,
        MenuNodeMetadata? metadata = null,
        MenuNodePresentation? presentation = null)
    {
        var props = new MenuNodeProps(
            IdValueObject.Create(),
            moduleId,
            parentNodeId,
            kind,
            code,
            label,
            description,
            ModuleStatus.Active,
            sortOrder,
            metadata ?? MenuNodeMetadata.Empty,
            createdBy,
            presentation ?? MenuNodePresentation.Empty);

        var node = new MenuNode(props);
        if (!node.IsValid())
        {
            return Result<MenuNode>.Failure(node.BrokenRules.GetBrokenRulesAsString());
        }

        return Result<MenuNode>.Success(node);
    }

    public Result Update(Name label, Description description, int sortOrder, ActorId updatedBy)
    {
        SetProps(Props.WithLabel(label).WithDescription(description).WithSortOrder(sortOrder));

        if (!IsValid())
        {
            return Result.Failure(BrokenRules.GetBrokenRulesAsString());
        }

        Props.Audit.Update(updatedBy.GetValue());
        return Result.Success();
    }

    public Result Activate(ActorId updatedBy)
    {
        SetProps(Props.WithStatus(ModuleStatus.Active));
        Props.Audit.Update(updatedBy.GetValue());
        return Result.Success();
    }

    public Result Deactivate(ActorId updatedBy)
    {
        SetProps(Props.WithStatus(ModuleStatus.Inactive));
        Props.Audit.Update(updatedBy.GetValue());
        return Result.Success();
    }

    public Result SetMetadata(MenuNodeMetadata metadata, ActorId updatedBy)
    {
        SetProps(Props.WithMetadata(metadata ?? MenuNodeMetadata.Empty));
        Props.Audit.Update(updatedBy.GetValue());
        return Result.Success();
    }

    /// <summary>Añade un hijo. Regla: una opción es hoja y no admite hijos; el código es único entre hermanos.</summary>
    public Result<Guid> AddChild(
        NodeKind kind,
        Code code,
        Name label,
        Description description,
        int sortOrder,
        ActorId createdBy,
        MenuNodeMetadata? metadata = null,
        MenuNodePresentation? presentation = null)
    {
        BrokenRules.Clear();

        if (Kind == NodeKind.Option)
        {
            BrokenRules.Add(new BrokenRule(nameof(Children), "Una opción es un nodo hoja y no admite hijos."));
        }

        if (_children.Any(c => c.Code.GetValue() == code.GetValue()))
        {
            BrokenRules.Add(new BrokenRule(nameof(Children), DomainErrors.Common.Duplicate));
        }

        if (!IsValid())
        {
            return Result<Guid>.Failure(BrokenRules.GetBrokenRulesAsString());
        }

        var childResult = Create(ModuleId, Props.Id, kind, code, label, description, sortOrder, createdBy, metadata, presentation);
        if (childResult.IsFailure)
        {
            return Result<Guid>.Failure(childResult.Error);
        }

        _children.Add(childResult.Value);
        Props.Audit.Update(createdBy.GetValue());
        return Result<Guid>.Success(childResult.Value.GetId().GetValue());
    }

    public Result RemoveChild(IdValueObject childId, ActorId updatedBy)
    {
        var child = _children.FirstOrDefault(c => c.Props.Id.GetValue() == childId.GetValue());
        if (child is null)
        {
            return Result.Failure(DomainErrors.Common.NotFound);
        }

        _children.Remove(child);
        Props.Audit.Update(updatedBy.GetValue());
        return Result.Success();
    }

    /// <summary>Vincula una funcionalidad (acción) N:M. Solo nodos hoja (Opción).</summary>
    public Result LinkAction(ActionCode actionCode, ActorId updatedBy)
    {
        BrokenRules.Clear();

        if (Kind != NodeKind.Option)
        {
            BrokenRules.Add(new BrokenRule(nameof(ActionCodes), "Solo una opción puede vincular funcionalidades."));
        }

        if (_actionCodes.Any(a => a.GetValue() == actionCode.GetValue()))
        {
            BrokenRules.Add(new BrokenRule(nameof(ActionCodes), DomainErrors.Common.Duplicate));
        }

        if (!IsValid())
        {
            return Result.Failure(BrokenRules.GetBrokenRulesAsString());
        }

        _actionCodes.Add(actionCode);
        Props.Audit.Update(updatedBy.GetValue());
        return Result.Success();
    }

    public Result UnlinkAction(ActionCode actionCode, ActorId updatedBy)
    {
        var existing = _actionCodes.FirstOrDefault(a => a.GetValue() == actionCode.GetValue());
        if (existing is null)
        {
            return Result.Failure(DomainErrors.Common.NotFound);
        }

        _actionCodes.Remove(existing);
        Props.Audit.Update(updatedBy.GetValue());
        return Result.Success();
    }

    /// <summary>Busca este nodo o cualquier descendiente por id (recorrido en profundidad).</summary>
    public MenuNode? Find(IdValueObject id)
    {
        if (Props.Id.GetValue() == id.GetValue())
        {
            return this;
        }

        foreach (var child in _children)
        {
            var found = child.Find(id);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    /// <summary>Devuelve el hijo directo cuyo id coincide, o null.</summary>
    public MenuNode? FindDirectChild(IdValueObject id)
        => _children.FirstOrDefault(c => c.Props.Id.GetValue() == id.GetValue());
}
