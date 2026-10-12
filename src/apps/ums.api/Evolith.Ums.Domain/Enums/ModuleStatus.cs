namespace Evolith.Ums.Domain.Enums;

public class ModuleStatus : DomainEnumeration
{
    public static readonly ModuleStatus Active = new(1, nameof(Active));
    public static readonly ModuleStatus Inactive = new(2, nameof(Inactive));

    private ModuleStatus(int id, string name) : base(id, name) { }
}
