using Evolith.Ums.Application.Authorization.SystemSuite.DTOs;

namespace Evolith.Ums.Application.Authorization.SystemSuite.Commands;

using Evolith.Ums.Domain.Authorization;
using Evolith.Ums.Domain.Authorization.SystemSuite;

public sealed class SetSystemSuiteStatusCommandHandler : ICommandHandler<SetSystemSuiteStatusCommand>
{
    private readonly ISystemSuiteRepository _systemSuiteRepository;
    private readonly IUserContext _userContext;

    public SetSystemSuiteStatusCommandHandler(
        ISystemSuiteRepository systemSuiteRepository,
        IUserContext userContext)
    {
        _systemSuiteRepository = systemSuiteRepository;
        _userContext = userContext;
    }

    [AuthorizationAspect(ResourceCode = "manage_suites", ActionCode = "manage")]
    [AuditTrail]
    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]
    public async Task<Result> Handle(SetSystemSuiteStatusCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_userContext.UserId))
        {
            return Result.Failure("Authenticated user is required to update system suite status.");
        }

        var systemSuite = await _systemSuiteRepository.GetByIdAsync(request.SystemSuiteId, cancellationToken);
        if (systemSuite is null)
        {
            return Result.Failure("System suite was not found.");
        }

        var status = DomainEnumerationParser.FromName<SystemStatus>(request.Status);
        if (status is null)
        {
            return Result.Failure("Invalid system status.");
        }

        var result = systemSuite.SetStatus(status, ActorId.Create(_userContext.UserId));
        if (result.IsFailure)
        {
            return result;
        }

        await _systemSuiteRepository.UpdateAsync(systemSuite, cancellationToken);
        await _systemSuiteRepository.UnitOfWork.SaveEntitiesAsync(cancellationToken);

        return Result.Success();
    }
}
