namespace Evolith.Ums.Domain.IGA.RolePromotionRequest;

/// <summary>
/// Estados de la máquina de promoción de rol (FR-060, ADR-UMS-093).
/// Flujo feliz: Draft → PendingEligibilityCheck → PendingManagerApproval →
/// (PendingSecurityReview) → Approved → Executed → Verified.
/// Estados terminales de corte: Rejected, Cancelled.
/// </summary>
public class RolePromotionStatus : DomainEnumeration
{
    public static readonly RolePromotionStatus Draft                   = new(1, nameof(Draft));
    public static readonly RolePromotionStatus PendingEligibilityCheck = new(2, nameof(PendingEligibilityCheck));
    public static readonly RolePromotionStatus PendingManagerApproval  = new(3, nameof(PendingManagerApproval));
    public static readonly RolePromotionStatus PendingSecurityReview   = new(4, nameof(PendingSecurityReview));
    public static readonly RolePromotionStatus Approved                = new(5, nameof(Approved));
    public static readonly RolePromotionStatus Executed                = new(6, nameof(Executed));
    public static readonly RolePromotionStatus Verified                = new(7, nameof(Verified));
    public static readonly RolePromotionStatus Rejected                = new(8, nameof(Rejected));
    public static readonly RolePromotionStatus Cancelled               = new(9, nameof(Cancelled));

    private RolePromotionStatus(int id, string name) : base(id, name) { }
}
