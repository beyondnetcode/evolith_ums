using Evolith.Ums.Application.Identity.UserAccount.DTOs;

namespace Evolith.Ums.Application.Identity.UserAccount.Commands;

public sealed record EnrollUserAccountMfaCommand(Guid UserAccountId, string Method)
    : ICommand<EnrollUserAccountMfaResponse>;
