using Evolith.Ums.Application.Identity.UserAccount.DTOs;

namespace Evolith.Ums.Application.Identity.UserAccount.Commands;

public sealed record BlockUserAccountCommand(Guid UserAccountId, string Reason) : ICommand;
