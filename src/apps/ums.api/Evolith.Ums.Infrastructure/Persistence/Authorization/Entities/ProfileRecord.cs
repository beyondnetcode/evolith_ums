using Evolith.Ums.Infrastructure.Persistence;

namespace Evolith.Ums.Infrastructure.Persistence.Authorization.Entities;

public sealed class ProfileRecord : IAuditableRecord
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public Guid RoleId { get; set; }
    public Guid? BranchId { get; set; }
    public int ScopeId { get; set; }
    public bool IsActive { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public string AuditTimeSpan { get; set; } = string.Empty;
    public byte[] RowVersion { get; set; } = [];

    public List<ProfilePermissionRecord> Permissions { get; set; } = [];
}
