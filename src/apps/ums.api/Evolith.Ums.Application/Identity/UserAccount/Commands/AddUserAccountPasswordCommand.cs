using Evolith.Ums.Application.Identity.UserAccount.DTOs;

namespace Evolith.Ums.Application.Identity.UserAccount.Commands;

public sealed record AddUserAccountPasswordCommand(Guid UserAccountId, string Password)
    : ICommand<AddUserAccountPasswordResponse>;
