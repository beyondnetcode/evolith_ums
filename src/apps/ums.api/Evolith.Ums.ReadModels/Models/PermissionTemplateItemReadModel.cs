namespace Evolith.Ums.ReadModels.Models;

public sealed class PermissionTemplateItemReadModel
{
    public Guid Id { get; set; }
    public Guid TemplateId { get; set; }
    public int TargetTypeId { get; set; }
    public Guid TargetId { get; set; }
    public Guid ActionId { get; set; }
    public bool IsAllowed { get; set; }
    public bool IsDenied { get; set; }
    public bool IsActive { get; set; }
}
