namespace Evolith.Ums.Application.Identity.Auth.Commands;

/// <summary>
/// Canje del secreto emitido por <see cref="ForgotPasswordCommand"/>.
///
/// <para>No lleva código de inquilino ni correo a propósito: el token ya identifica al usuario y
/// a su inquilino. Pedir además el correo no añadiría seguridad —el token es el secreto— y sí
/// abriría un segundo punto donde una respuesta distinta podría delatar qué cuentas existen.</para>
/// </summary>
public sealed record ResetPasswordCommand(string Token, string NewPassword)
    : ICommand<ResetPasswordResponse>;

public sealed record ResetPasswordResponse(string Message);
