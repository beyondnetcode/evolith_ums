namespace Evolith.Ums.Domain.Authorization.SystemSuite.DomainResource;

public sealed class DomainResource : Entity<DomainResource, DomainResourceProps>
{
    private DomainResource(DomainResourceProps props) : base(props)
    {
    }

    public SystemSuiteId SystemSuiteId => Props.SystemSuiteId;
    public ModuleId? ModuleId => Props.ModuleId;
    /// <summary>Non-null when this Entity or DomainMethod belongs to a parent Aggregate.</summary>
    public IdValueObject? ParentResourceId => Props.ParentResourceId;
    public DomainResourceType Type => Props.Type;
    public Code Code => Props.Code;
    public Name Name => Props.Name;
    public Description Description => Props.Description;

    public IdValueObject GetId() => Props.Id;

    public static Result<DomainResource> Create(
        SystemSuiteId systemSuiteId,
        ModuleId? moduleId,
        IdValueObject? parentResourceId,
        DomainResourceType type,
        Code code,
        Name name,
        Description description,
        ActorId createdBy)
    {
        var props = new DomainResourceProps(IdValueObject.Create(), systemSuiteId, moduleId, parentResourceId, type, code, name, description, createdBy);
        var domainResource = new DomainResource(props);

        if (!domainResource.IsValid())
        {
            return Result<DomainResource>.Failure(domainResource.BrokenRules.GetBrokenRulesAsString());
        }

        return Result<DomainResource>.Success(domainResource);
    }

    public Result Update(Name name, Description description, ActorId updatedBy)
    {
        SetProps(Props.WithName(name).WithDescription(description));

        if (!IsValid())
        {
            return Result.Failure(BrokenRules.GetBrokenRulesAsString());
        }

        Props.Audit.Update(updatedBy.GetValue());
        return Result.Success();
    }
}
