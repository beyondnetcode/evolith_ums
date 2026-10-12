namespace Evolith.Ums.Application.Authorization.SystemSuite.Commands;

using BeyondNetCode.Shell.Ddd;
using Evolith.Ums.Domain.Authorization;
using Evolith.Ums.Domain.Enums;

public sealed class AddAppSettingCommandHandler : ICommandHandler<AddAppSettingCommand>
{
    private readonly ISystemSuiteRepository _repository;
    private readonly IUserContext _userContext;

    public AddAppSettingCommandHandler(ISystemSuiteRepository repository, IUserContext userContext)
    {
        _repository = repository;
        _userContext = userContext;
    }

    [AuthorizationAspect(ResourceCode = "manage_suites", ActionCode = "manage")]
    [AuditTrail]
    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]
    public async Task<Result> Handle(AddAppSettingCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_userContext.UserId))
            return Result.Failure("Authenticated user is required.");

        var suite = await _repository.GetByIdAsync(request.SystemSuiteId, cancellationToken);
        if (suite is null) return Result.Failure("System suite not found.");

        var scope = DomainEnumeration.FromDisplayName<ConfigurationScope>(request.Scope);
        if (scope is null)
            return Result.Failure($"Invalid scope '{request.Scope}'.");

        var result = suite.AddAppSetting(
            ConfigurationKey.Create(request.Key),
            ConfigurationValue.Create(request.Value),
            scope,
            ActorId.Create(_userContext.UserId));

        if (result.IsFailure) return result;

        await _repository.UpdateAsync(suite, cancellationToken);
        await _repository.UnitOfWork.SaveEntitiesAsync(cancellationToken);
        return Result.Success();
    }
}
