namespace Evolith.Ums.Application.Authorization.SystemSuite.Commands;

using Evolith.Ums.Application.Authorization.SystemSuite.DTOs;
using Evolith.Ums.Domain.Authorization;

public sealed class RegisterActionCommandHandler : ICommandHandler<RegisterActionCommand, RegisterActionResponse>
{
    private readonly ISystemSuiteRepository _repository;
    private readonly IUserContext _userContext;

    public RegisterActionCommandHandler(ISystemSuiteRepository repository, IUserContext userContext)
    {
        _repository = repository;
        _userContext = userContext;
    }

    [AuthorizationAspect(ResourceCode = "manage_suites", ActionCode = "manage")]
    [AuditTrail]
    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]
    public async Task<Result<RegisterActionResponse>> Handle(RegisterActionCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_userContext.UserId))
            return Result<RegisterActionResponse>.Failure("Authenticated user is required.");

        var suite = await _repository.GetByIdAsync(request.SystemSuiteId, cancellationToken);
        if (suite is null) return Result<RegisterActionResponse>.Failure("System suite not found.");

        var result = suite.RegisterAction(
            ActionCode.Create(request.Code),
            Name.Create(request.Name),
            ActorId.Create(_userContext.UserId));

        if (result.IsFailure) return Result<RegisterActionResponse>.Failure(result.Error);

        await _repository.UpdateAsync(suite, cancellationToken);
        await _repository.UnitOfWork.SaveEntitiesAsync(cancellationToken);
        return Result<RegisterActionResponse>.Success(new RegisterActionResponse(result.Value, request.Code));
    }
}
