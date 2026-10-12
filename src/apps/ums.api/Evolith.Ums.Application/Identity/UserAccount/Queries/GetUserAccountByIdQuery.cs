using Evolith.Ums.Application.Identity.UserAccount.DTOs;

namespace Evolith.Ums.Application.Identity.UserAccount.Queries;

public sealed record GetUserAccountByIdQuery(Guid UserAccountId) : IQuery<UserAccountDto>;
