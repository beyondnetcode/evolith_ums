using Evolith.Ums.Application.Authorization.Profile.DTOs;

namespace Evolith.Ums.Application.Authorization.Profile.Commands;

public sealed record DeactivateProfileCommand(Guid ProfileId) : ICommand;
