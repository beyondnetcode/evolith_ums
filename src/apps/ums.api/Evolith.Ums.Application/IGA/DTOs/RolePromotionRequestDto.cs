namespace Evolith.Ums.Application.IGA.DTOs;

/// <summary>Proyección de lectura de una solicitud de promoción de rol (IGA, ADR-UMS-093).</summary>
public sealed record RolePromotionRequestDto(
    Guid Id,
    Guid TenantId,
    Guid TargetUserId,
    Guid RequesterId,
    Guid CurrentRoleId,
    Guid TargetRoleId,
    string Status,
    int? RiskScore,
    Guid? ApproverId,
    Guid? SecurityReviewerId,
    Guid? ExecutorId,
    Guid? VerifierId,
    string? DecisionReason);
