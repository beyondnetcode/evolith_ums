namespace Evolith.Ums.Application.Identity.Auth.Commands;

public sealed record SignupUserCommand(
    string TenantCode,
    string DisplayName,
    string Email,
    string Password) : ICommand<UserSignupResponse>;

public sealed record UserSignupResponse(
    Guid UserAccountId,
    string Message);
