using Evolith.Ums.Application.Authorization.SystemSuite.DTOs;

namespace Evolith.Ums.Application.Authorization.SystemSuite.Queries;

public sealed record GetSystemSuiteByIdQuery(Guid SystemSuiteId) : IQuery<SystemSuiteDto>;
