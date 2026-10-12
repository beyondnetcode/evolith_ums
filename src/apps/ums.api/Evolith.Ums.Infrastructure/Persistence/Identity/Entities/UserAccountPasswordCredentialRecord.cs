namespace Evolith.Ums.Infrastructure.Persistence.Identity.Entities;

public sealed class UserAccountPasswordCredentialRecord
{
    public Guid Id { get; set; }
    public Guid UserAccountId { get; set; }
    public string PasswordHash { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public string AuditTimeSpan { get; set; } = string.Empty;

    public UserAccountRecord UserAccount { get; set; } = default!;
}
