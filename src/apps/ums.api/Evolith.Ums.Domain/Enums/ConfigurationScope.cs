namespace Evolith.Ums.Domain.Enums;

public class ConfigurationScope : DomainEnumeration
{
    public static readonly ConfigurationScope Global = new(1, nameof(Global));
    public static readonly ConfigurationScope Tenant = new(2, nameof(Tenant));
    public static readonly ConfigurationScope User   = new(3, nameof(User));
    public static readonly ConfigurationScope Suite  = new(4, nameof(Suite));
    public static readonly ConfigurationScope Module = new(5, nameof(Module));

    private ConfigurationScope(int id, string name) : base(id, name) { }
}
