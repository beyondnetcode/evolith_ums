namespace Evolith.Ums.Application.Configuration.AppConfiguration.Commands;

// G-143: borrado duro de una configuración de aplicación (DELETE /app-configurations/{id}).
// A diferencia de Archive (que sólo cambia el estado), este comando ELIMINA la fila.
public sealed record DeleteAppConfigurationCommand(Guid AppConfigurationId) : ICommand;
