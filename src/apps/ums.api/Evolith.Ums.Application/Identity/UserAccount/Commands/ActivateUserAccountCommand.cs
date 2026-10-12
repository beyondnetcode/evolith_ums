using Evolith.Ums.Application.Identity.UserAccount.DTOs;

namespace Evolith.Ums.Application.Identity.UserAccount.Commands;

public sealed record ActivateUserAccountCommand(Guid UserAccountId) : ICommand;
