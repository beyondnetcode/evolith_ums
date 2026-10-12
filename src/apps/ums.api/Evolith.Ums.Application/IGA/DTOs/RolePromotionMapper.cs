namespace Evolith.Ums.Application.IGA.DTOs;

using Evolith.Ums.Domain.IGA.RoleMaturityStatus;
using Evolith.Ums.Domain.IGA.RolePromotionRequest;

/// <summary>Proyecciones de lectura de los agregados IGA a sus DTO (ADR-UMS-093).</summary>
internal static class RolePromotionMapper
{
    public static RolePromotionRequestDto ToDto(RolePromotionRequest request) =>
        new(
            request.GetId().GetValue(),
            request.TenantId.GetValue(),
            request.TargetUserId.GetValue(),
            request.RequesterId.GetValue(),
            request.CurrentRoleId.GetValue(),
            request.TargetRoleId.GetValue(),
            request.Status.Name,
            request.RiskScore?.GetValue(),
            request.ApproverId?.GetValue(),
            request.SecurityReviewerId?.GetValue(),
            request.ExecutorId?.GetValue(),
            request.VerifierId?.GetValue(),
            request.DecisionReason);

    public static RoleMaturityStatusDto ToDto(RoleMaturityStatus status) =>
        new(
            status.GetId().GetValue(),
            status.TenantId.GetValue(),
            status.UserId.GetValue(),
            status.RoleId.GetValue(),
            status.CurrentMaturityLevel.ToString(),
            status.NextEligibleMaturityLevel?.ToString(),
            status.AssignedAt,
            status.CurrentLevelSince,
            status.EligibleForPromotionAt,
            status.CompletedCertificationsCount,
            status.CompletedTrainingsCount,
            status.PerformanceScore,
            status.HasNoComplianceIssues,
            status.BlockingFactor?.GetValue(),
            status.LastReviewedAt);
}
