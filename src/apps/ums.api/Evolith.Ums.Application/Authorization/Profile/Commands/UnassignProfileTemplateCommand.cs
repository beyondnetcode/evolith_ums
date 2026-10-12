namespace Evolith.Ums.Application.Authorization.Profile.Commands;

/// <summary>
/// Retira de un perfil lo que una plantilla le concedia (ADR-UMS-106 D4).
///
/// <para>Es la operacion que faltaba: hasta hoy un perfil no se podia devolver a una sola version de
/// su plantilla salvo desactivandolo y creando otro ([[G-272]]).</para>
/// </summary>
public sealed record UnassignProfileTemplateCommand(Guid ProfileId, Guid TemplateId) : ICommand;
