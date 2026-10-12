namespace Evolith.Ums.Domain.Kernel.ValueObjects;

public class RolePromotionRequestId : IdValueObject
{
    private RolePromotionRequestId(Guid value) : base(value) { }
    public static new RolePromotionRequestId Create() => new RolePromotionRequestId(Guid.NewGuid());
    public static new RolePromotionRequestId Load(Guid value) => new RolePromotionRequestId(value);
    public static new RolePromotionRequestId Load(string value) => new RolePromotionRequestId(Guid.Parse(value));
}
