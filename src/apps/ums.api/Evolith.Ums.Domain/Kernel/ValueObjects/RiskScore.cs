namespace Evolith.Ums.Domain.Kernel.ValueObjects;

using BeyondNetCode.Shell.Ddd.ValueObjects.Common;

/// <summary>
/// Objeto de valor inmutable que representa el RiskScore de impacto tóxico de una
/// promoción de rol (FR-061, ADR-UMS-093). Su rango válido es [0, 100]. Una vez congelado
/// dentro de <c>RolePromotionRequest</c> al salir de <c>Draft</c>, no puede recalcularse:
/// recalcular exige una nueva solicitud (inmutabilidad por solicitud, INV-RPR2).
/// </summary>
public sealed class RiskScore : IntValueObject
{
    public const int Min = 0;
    public const int Max = 100;

    private RiskScore(int value) : base(value) { }

    /// <summary>
    /// Crea un <see cref="RiskScore"/> validando el rango [0, 100]. Fuera de rango
    /// devuelve <c>Result.Failure</c> con <see cref="DomainErrors.IGA.RiskScoreOutOfRange"/>
    /// (nunca excepción).
    /// </summary>
    public static Result<RiskScore> Create(int value)
    {
        if (value < Min || value > Max)
        {
            return Result<RiskScore>.Failure(DomainErrors.IGA.RiskScoreOutOfRange);
        }

        return Result<RiskScore>.Success(new RiskScore(value));
    }

    /// <summary>Rehidrata un valor ya persistido (se asume dentro de rango).</summary>
    public static RiskScore Load(int value) => new RiskScore(value);
}
