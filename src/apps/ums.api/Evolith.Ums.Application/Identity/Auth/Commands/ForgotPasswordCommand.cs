namespace Evolith.Ums.Application.Identity.Auth.Commands;

public sealed record ForgotPasswordCommand(string TenantCode, string Email)
    : ICommand<ForgotPasswordResponse>;

/// <summary>
/// Respuesta del flujo anónimo. Lleva un ÚNICO campo, y siempre con el mismo valor: cualquier
/// dato que dependa de si la cuenta existe convierte el endpoint en un oráculo de enumeración.
/// El secreto de restablecimiento no viaja aquí en ningún entorno; sale solo por el buzón.
/// </summary>
public sealed record ForgotPasswordResponse(string Message);
