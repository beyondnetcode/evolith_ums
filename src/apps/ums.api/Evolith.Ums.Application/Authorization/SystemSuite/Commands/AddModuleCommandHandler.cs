namespace Evolith.Ums.Application.Authorization.SystemSuite.Commands;

using Evolith.Ums.Application.Authorization.SystemSuite.DTOs;
using Evolith.Ums.Domain.Authorization;

public sealed class AddModuleCommandHandler : ICommandHandler<AddModuleCommand, AddModuleResponse>
{
    private readonly ISystemSuiteRepository _repository;
    private readonly IUserContext _userContext;

    public AddModuleCommandHandler(ISystemSuiteRepository repository, IUserContext userContext)
    {
        _repository = repository;
        _userContext = userContext;
    }

    [AuthorizationAspect(ResourceCode = "manage_suites", ActionCode = "manage")]
    [AuditTrail]
    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]
    public async Task<Result<AddModuleResponse>> Handle(AddModuleCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_userContext.UserId))
            return Result<AddModuleResponse>.Failure("Authenticated user is required.");

        var suite = await _repository.GetByIdAsync(request.SystemSuiteId, cancellationToken);
        if (suite is null) return Result<AddModuleResponse>.Failure("System suite not found.");

        var result = suite.AddModule(
            Code.Create(request.Code),
            Name.Create(request.Name),
            Description.Create(request.Description),
            request.SortOrder,
            ActorId.Create(_userContext.UserId),
            request.Icon);

        if (result.IsFailure) return Result<AddModuleResponse>.Failure(result.Error);

        await _repository.UpdateAsync(suite, cancellationToken);
        await _repository.UnitOfWork.SaveEntitiesAsync(cancellationToken);
        return Result<AddModuleResponse>.Success(new AddModuleResponse(result.Value));
    }
}
