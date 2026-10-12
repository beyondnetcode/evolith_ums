namespace Evolith.Ums.Application.IGA.Services;

/// <summary>
/// Contexto de cálculo del RiskScore de una promoción de rol (FR-061, ADR-UMS-093).
/// Identifica el inquilino, el usuario objetivo y el par de roles (actual → objetivo)
/// cuyo salto de privilegio se evalúa.
/// </summary>
public sealed record RolePromotionRiskContext(
    Guid TenantId,
    Guid TargetUserId,
    Guid CurrentRoleId,
    Guid TargetRoleId);

/// <summary>Factor individual que contribuye al RiskScore, con su peso y una descripción trazable.</summary>
public sealed record RiskFactor(string Code, int Weight, string Description);

/// <summary>
/// Evaluación de riesgo congelable de una promoción: la puntuación [0, 100], la versión del modelo
/// (<c>riskModelVersion</c>) para trazar cambios de fórmula y el desglose de factores.
/// </summary>
public sealed record RiskAssessment(int Score, string RiskModelVersion, IReadOnlyList<RiskFactor> Factors);

/// <summary>
/// Puerto de cálculo del RiskScore de impacto tóxico de una promoción de rol (FR-061, ADR-UMS-093).
/// Se invoca una única vez, al hacer <c>Submit</c> (Draft → PendingEligibilityCheck), y su resultado
/// se congela en el agregado (INV-RPR2). La implementación por defecto es una heurística versionada
/// (<see cref="HeuristicRiskScoreCalculator"/>); la infraestructura puede sustituirla por una
/// implementación respaldada por el grafo de autorización (ADR-UMS-088) sin tocar los handlers.
/// </summary>
public interface IRiskScoreCalculator
{
    Task<Result<RiskAssessment>> CalculateAsync(RolePromotionRiskContext context, CancellationToken cancellationToken = default);
}
