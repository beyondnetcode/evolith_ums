namespace Evolith.Ums.Application.Authorization.SystemSuite.Commands;

/// <summary>
/// Elimina LÓGICAMENTE un sistema ya archivado y sin referencias vivas (G-246).
///
/// El archivado previo se hace con <see cref="SetSystemSuiteStatusCommand"/> → <c>Deprecated</c>.
/// La fila permanece en la base —el catálogo se consulta sobre datos antiguos—; lo que cambia es que
/// deja de aparecer en cualquier lectura.
/// </summary>
public sealed record DeleteSystemSuiteCommand(Guid SystemSuiteId) : ICommand;
