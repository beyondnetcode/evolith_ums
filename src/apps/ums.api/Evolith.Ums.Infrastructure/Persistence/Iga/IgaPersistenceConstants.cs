namespace Evolith.Ums.Infrastructure.Persistence.Iga;

/// <summary>
/// Constantes de persistencia del contexto acotado IGA (ADR-UMS-093).
/// Las tablas de gobierno de identidad viven en su propio esquema, acotado por inquilino.
/// </summary>
internal static class IgaPersistenceConstants
{
    public const string Schema = "iga";
}
