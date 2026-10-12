using Evolith.Ums.Application.Authorization.Profile.DTOs;

namespace Evolith.Ums.Application.Authorization.Profile.Queries;

public sealed record GetProfileByIdQuery(Guid ProfileId) : IQuery<ProfileDto>;
