namespace Evolith.Ums.Application.IGA.DTOs;

/// <summary>Proyección de lectura del estado de madurez de un usuario en un rol (IGA, ADR-UMS-093).</summary>
public sealed record RoleMaturityStatusDto(
    Guid Id,
    Guid TenantId,
    Guid UserId,
    Guid RoleId,
    string CurrentMaturityLevel,
    string? NextEligibleMaturityLevel,
    DateTime AssignedAt,
    DateTime CurrentLevelSince,
    DateTime? EligibleForPromotionAt,
    int CompletedCertificationsCount,
    int CompletedTrainingsCount,
    decimal PerformanceScore,
    bool HasNoComplianceIssues,
    string? BlockingFactor,
    DateTime? LastReviewedAt);
