using Evolith.Ums.Application.Authorization.Profile.DTOs;

namespace Evolith.Ums.Application.Authorization.Profile.Commands;

public sealed record ActivateProfileCommand(Guid ProfileId) : ICommand;
