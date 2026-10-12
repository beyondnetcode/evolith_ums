namespace Evolith.Ums.Domain.Authorization.SystemSuite;

/// <summary>
/// Identidad mínima de un sistema: lo justo para nombrarlo en una lista.
///
/// Existe para no cargar el agregado completo cuando solo hace falta el rótulo — cargarlo cuesta
/// siete consultas por sus módulos, nodos, acciones y recursos de dominio.
/// </summary>
public sealed record SystemSuiteSummary(Guid Id, string Code, string Name);
