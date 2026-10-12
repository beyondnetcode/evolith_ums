namespace Evolith.Ums.Application.IGA.Services;

using Evolith.Ums.Domain.Authorization;

/// <summary>
/// Implementación heurística versionada (<c>iga-risk-heuristic-v1</c>) del cálculo del RiskScore
/// de una promoción de rol (FR-061, ADR-UMS-093).
///
/// Fórmula v1 (determinista, acotada a [0, 100]):
///   base (10)
///   + escalación jerárquica  = max(0, nivelObjetivo − nivelActual) · 15   (proxy de permisos nuevos)
///   + salto de orden de promoción = min(3, ordenObjetivo − ordenActual) · 10
///   + sensibilidad del rol objetivo por banda de nivel jerárquico (≥4:30, 3:20, 2:10, resto:5)
///
/// Limitación conocida (registrada como seguimiento de ADR-UMS-093): esta v1 usa metadatos del rol
/// (nivel jerárquico y orden de promoción) como <em>proxy</em>. Aún NO calcula el diferencial real de
/// permisos nuevos/removidos ni las combinaciones tóxicas (conflictos SoD con los perfiles/roles
/// vigentes del usuario), que requieren el grafo de autorización (ADR-UMS-088). La infraestructura puede
/// sustituir <see cref="IRiskScoreCalculator"/> por una implementación respaldada por el grafo.
/// </summary>
public sealed class HeuristicRiskScoreCalculator : IRiskScoreCalculator
{
    public const string ModelVersion = "iga-risk-heuristic-v1";

    private const int BaseWeight = 10;
    private const int EscalationWeightPerLevel = 15;
    private const int PromotionOrderWeightPerStep = 10;
    private const int MaxPromotionOrderSteps = 3;

    private readonly IRoleRepository _roleRepository;

    public HeuristicRiskScoreCalculator(IRoleRepository roleRepository)
    {
        _roleRepository = roleRepository;
    }

    public async Task<Result<RiskAssessment>> CalculateAsync(
        RolePromotionRiskContext context,
        CancellationToken cancellationToken = default)
    {
        var currentRole = await _roleRepository.GetByIdAsync(context.CurrentRoleId, cancellationToken);
        if (currentRole is null)
        {
            return Result<RiskAssessment>.Failure("No se encontró el rol actual para calcular el RiskScore.");
        }

        var targetRole = await _roleRepository.GetByIdAsync(context.TargetRoleId, cancellationToken);
        if (targetRole is null)
        {
            return Result<RiskAssessment>.Failure("No se encontró el rol objetivo para calcular el RiskScore.");
        }

        var factors = new List<RiskFactor>();

        factors.Add(new RiskFactor("base", BaseWeight, "Riesgo base de toda promoción de rol."));

        var escalation = Math.Max(0, targetRole.HierarchyLevel - currentRole.HierarchyLevel);
        var escalationWeight = escalation * EscalationWeightPerLevel;
        factors.Add(new RiskFactor(
            "escalacion_jerarquica",
            escalationWeight,
            $"Escalación de {escalation} nivel(es) jerárquico(s) (proxy de permisos nuevos)."));

        var promotionSteps = Math.Min(MaxPromotionOrderSteps, Math.Max(0, targetRole.PromotionOrder - currentRole.PromotionOrder));
        var promotionWeight = promotionSteps * PromotionOrderWeightPerStep;
        factors.Add(new RiskFactor(
            "salto_orden_promocion",
            promotionWeight,
            $"Salto de {promotionSteps} paso(s) en el orden de promoción."));

        var sensitivityWeight = SensitivityWeightFor(targetRole.HierarchyLevel);
        factors.Add(new RiskFactor(
            "sensibilidad_rol_objetivo",
            sensitivityWeight,
            $"Sensibilidad del rol objetivo por banda de nivel {targetRole.HierarchyLevel}."));

        var raw = factors.Sum(f => f.Weight);
        var score = Math.Clamp(raw, RiskScore.Min, RiskScore.Max);

        return Result<RiskAssessment>.Success(new RiskAssessment(score, ModelVersion, factors));
    }

    private static int SensitivityWeightFor(int hierarchyLevel) => hierarchyLevel switch
    {
        >= 4 => 30,
        3 => 20,
        2 => 10,
        _ => 5,
    };
}
