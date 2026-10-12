namespace Evolith.Ums.Domain.Authorization.SystemSuite.Module;

using Evolith.Ums.Domain.Authorization.SystemSuite.MenuNode;
using MenuNodeEntity = Evolith.Ums.Domain.Authorization.SystemSuite.MenuNode.MenuNode;

public sealed class Module : Entity<Module, ModuleProps>
{
    private readonly List<MenuNodeEntity> _nodes = new();

    private Module(ModuleProps props) : base(props)
    {
    }

    public SystemId SystemId => Props.SystemId;
    public Code Code => Props.Code;
    public Name Name => Props.Name;
    public Description Description => Props.Description;
    public ModuleStatus Status => Props.Status;
    public int SortOrder => Props.SortOrder;

    /// <summary>Icono del módulo (identificador, no recurso). Nulo si no se configuró.</summary>
    public string? Icon => Props.Icon;

    /// <summary>Raíces del árbol de navegación recursivo (ADR-0090). Los hijos anidan dentro de cada nodo.</summary>
    public IReadOnlyCollection<MenuNodeEntity> Nodes => _nodes.AsReadOnly();

    public ModuleId GetId() => ModuleId.Load(Props.Id.GetValue());

    public static Result<Module> Create(
        SystemId systemId,
        Code code,
        Name name,
        Description description,
        int sortOrder,
        ActorId createdBy,
        string? icon = null)
    {
        var props = new ModuleProps(IdValueObject.Create(), systemId, code, name, description, ModuleStatus.Inactive, sortOrder, createdBy, NormalizarIcono(icon));
        var module = new Module(props);

        if (!module.IsValid())
        {
            return Result<Module>.Failure(module.BrokenRules.GetBrokenRulesAsString());
        }

        return Result<Module>.Success(module);
    }

    public Result Update(Name name, Description description, int sortOrder, ActorId updatedBy)
    {
        SetProps(Props.WithName(name).WithDescription(description).WithSortOrder(sortOrder));

        if (!IsValid())
        {
            return Result.Failure(BrokenRules.GetBrokenRulesAsString());
        }

        Props.Audit.Update(updatedBy.GetValue());
        return Result.Success();
    }

    /// <summary>Fija o borra el icono del módulo. Cadena vacía y solo-espacios equivalen a borrarlo.</summary>
    public Result SetIcon(string? icon, ActorId updatedBy)
    {
        SetProps(Props.WithIcon(NormalizarIcono(icon)));
        Props.Audit.Update(updatedBy.GetValue());
        return Result.Success();
    }

    /// <summary>«Sin icono», no «icono ''»: la misma regla que aplica el nodo de navegación.</summary>
    private static string? NormalizarIcono(string? icon)
        => string.IsNullOrWhiteSpace(icon) ? null : icon.Trim();

    public Result Activate(ActorId updatedBy)
    {
        BrokenRules.Clear();

        if (Status == ModuleStatus.Active)
        {
            BrokenRules.Add(new BrokenRule(nameof(Status), DomainErrors.SystemSuite.ModuleAlreadyActive));
        }

        if (!IsValid())
        {
            return Result.Failure(BrokenRules.GetBrokenRulesAsString());
        }

        SetProps(Props.WithStatus(ModuleStatus.Active));
        Props.Audit.Update(updatedBy.GetValue());
        return Result.Success();
    }

    public Result Deactivate(ActorId updatedBy)
    {
        BrokenRules.Clear();

        if (Status == ModuleStatus.Inactive)
        {
            BrokenRules.Add(new BrokenRule(nameof(Status), DomainErrors.SystemSuite.ModuleAlreadyInactive));
        }

        if (!IsValid())
        {
            return Result.Failure(BrokenRules.GetBrokenRulesAsString());
        }

        SetProps(Props.WithStatus(ModuleStatus.Inactive));
        Props.Audit.Update(updatedBy.GetValue());
        return Result.Success();
    }

    // ── Árbol de navegación recursivo (MenuNode, ADR-0090) ─────────────────────

    /// <summary>Añade un nodo raíz (hijo directo del módulo). Código único entre raíces.</summary>
    public Result<Guid> AddRootNode(NodeKind kind, Code code, Name label, Description description, int sortOrder, ActorId createdBy, MenuNodeMetadata? metadata = null, MenuNodePresentation? presentation = null)
    {
        BrokenRules.Clear();

        if (Status == ModuleStatus.Inactive)
        {
            BrokenRules.Add(new BrokenRule(nameof(Nodes), DomainErrors.SystemSuite.ModuleInactiveCannotAddMenu));
        }

        if (_nodes.Any(n => n.Code.GetValue() == code.GetValue()))
        {
            BrokenRules.Add(new BrokenRule(nameof(Nodes), DomainErrors.SystemSuite.MenuCodeNotUnique));
        }

        if (!IsValid())
        {
            return Result<Guid>.Failure(BrokenRules.GetBrokenRulesAsString());
        }

        var nodeResult = MenuNodeEntity.Create(GetId(), null, kind, code, label, description, sortOrder, createdBy, metadata, presentation);
        if (nodeResult.IsFailure)
        {
            return Result<Guid>.Failure(nodeResult.Error);
        }

        _nodes.Add(nodeResult.Value);
        Props.Audit.Update(createdBy.GetValue());
        return Result<Guid>.Success(nodeResult.Value.GetId().GetValue());
    }

    /// <summary>Añade un nodo hijo bajo un nodo existente (a cualquier profundidad).</summary>
    public Result<Guid> AddChildNode(IdValueObject parentNodeId, NodeKind kind, Code code, Name label, Description description, int sortOrder, ActorId createdBy, MenuNodeMetadata? metadata = null, MenuNodePresentation? presentation = null)
    {
        var parent = FindNode(parentNodeId);
        if (parent is null)
        {
            return Result<Guid>.Failure(DomainErrors.Common.NotFound);
        }

        var result = parent.AddChild(kind, code, label, description, sortOrder, createdBy, metadata, presentation);
        if (result.IsSuccess)
        {
            Props.Audit.Update(createdBy.GetValue());
        }

        return result;
    }

    public Result UpdateNode(IdValueObject nodeId, Name label, Description description, int sortOrder, ActorId updatedBy)
        => DelegateToNode(nodeId, node => node.Update(label, description, sortOrder, updatedBy), updatedBy);

    public Result ActivateNode(IdValueObject nodeId, ActorId updatedBy)
        => DelegateToNode(nodeId, node => node.Activate(updatedBy), updatedBy);

    public Result DeactivateNode(IdValueObject nodeId, ActorId updatedBy)
        => DelegateToNode(nodeId, node => node.Deactivate(updatedBy), updatedBy);

    public Result LinkNodeAction(IdValueObject nodeId, ActionCode actionCode, ActorId updatedBy)
        => DelegateToNode(nodeId, node => node.LinkAction(actionCode, updatedBy), updatedBy);

    public Result UnlinkNodeAction(IdValueObject nodeId, ActionCode actionCode, ActorId updatedBy)
        => DelegateToNode(nodeId, node => node.UnlinkAction(actionCode, updatedBy), updatedBy);

    public Result SetNodeMetadata(IdValueObject nodeId, MenuNodeMetadata metadata, ActorId updatedBy)
        => DelegateToNode(nodeId, node => node.SetMetadata(metadata, updatedBy), updatedBy);

    /// <summary>Elimina un nodo (raíz o anidado) y todo su subárbol.</summary>
    public Result RemoveNode(IdValueObject nodeId, ActorId updatedBy)
    {
        var node = FindNode(nodeId);
        if (node is null)
        {
            return Result.Failure(DomainErrors.Common.NotFound);
        }

        if (node.ParentNodeId is null)
        {
            _nodes.Remove(node);
            Props.Audit.Update(updatedBy.GetValue());
            return Result.Success();
        }

        var parent = FindNode(node.ParentNodeId);
        if (parent is null)
        {
            return Result.Failure(DomainErrors.Common.NotFound);
        }

        var result = parent.RemoveChild(nodeId, updatedBy);
        if (result.IsSuccess)
        {
            Props.Audit.Update(updatedBy.GetValue());
        }

        return result;
    }

    private Result DelegateToNode(IdValueObject nodeId, Func<MenuNodeEntity, Result> action, ActorId updatedBy)
    {
        var node = FindNode(nodeId);
        if (node is null)
        {
            return Result.Failure(DomainErrors.Common.NotFound);
        }

        var result = action(node);
        if (result.IsSuccess)
        {
            Props.Audit.Update(updatedBy.GetValue());
        }

        return result;
    }

    private MenuNodeEntity? FindNode(IdValueObject id)
    {
        foreach (var root in _nodes)
        {
            var found = root.Find(id);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }
}
