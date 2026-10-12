using Evolith.Ums.Application.Authorization.SystemSuite.DTOs;
using Evolith.Ums.Domain.Authorization;
using Evolith.Ums.Domain.Authorization.SystemSuite;

namespace Evolith.Ums.Application.Authorization.SystemSuite.Queries;

public sealed class GetSystemSuiteByIdQueryHandler : IQueryHandler<GetSystemSuiteByIdQuery, SystemSuiteDto>
{
    private readonly ISystemSuiteRepository _systemSuiteRepository;

    public GetSystemSuiteByIdQueryHandler(ISystemSuiteRepository systemSuiteRepository)
    {
        _systemSuiteRepository = systemSuiteRepository;
    }

    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]

    [AuthorizationAspect(ResourceCode = "view_suites", ActionCode = "view")]
    public async Task<Result<SystemSuiteDto>> Handle(
        GetSystemSuiteByIdQuery request,
        CancellationToken cancellationToken)
    {
        var systemSuite = await _systemSuiteRepository.GetByIdAsync(request.SystemSuiteId, cancellationToken);

        if (systemSuite is null)
        {
            return Result<SystemSuiteDto>.Failure("System suite not found.");
        }

        return Result<SystemSuiteDto>.Success(SystemSuiteDto.Map(systemSuite));
    }
}
