namespace Evolith.Ums.Application.Authorization.SystemSuite.Commands;

using Evolith.Ums.Application.Authorization.SystemSuite.DTOs;

/// <param name="Icon">
/// Identificador de icono que el cliente resuelve (nunca un recurso). Opcional y al final del
/// registro para no romper a ningún llamador existente.
///
/// <para>El agregado lo acepta desde siempre —<c>SystemSuite.AddModule(..., icon)</c>, que el
/// sembrador usa— pero el comando no lo exponía y el manejador lo descartaba. Consecuencia real:
/// todo sistema dado de alta por API salía con los módulos a <c>icon: null</c> y la barra lateral
/// perdía los iconos que el sembrador sí pone. Se vio al provisionar UMS desde
/// <c>ums-suite.json</c>, que los declara.</para>
/// </param>
public sealed record AddModuleCommand(
    Guid SystemSuiteId,
    string Code,
    string Name,
    string Description,
    int SortOrder,
    string? Icon = null) : ICommand<AddModuleResponse>;
