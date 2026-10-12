namespace Evolith.Ums.Domain.Enums;

public class PermissionState : DomainEnumeration
{
    public static readonly PermissionState Enabled = new(1, nameof(Enabled));
    public static readonly PermissionState Disabled = new(2, nameof(Disabled));

    private PermissionState(int id, string name) : base(id, name) { }
}
