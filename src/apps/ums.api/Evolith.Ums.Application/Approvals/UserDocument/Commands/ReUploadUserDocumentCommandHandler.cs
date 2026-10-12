namespace Evolith.Ums.Application.Approvals.UserDocument.Commands;
using Evolith.Ums.Domain.Approvals;
public sealed class ReUploadUserDocumentCommandHandler : ICommandHandler<ReUploadUserDocumentCommand>
{
    private readonly IUserDocumentRepository _repository;
    private readonly IUserContext _userContext;
    public ReUploadUserDocumentCommandHandler(IUserDocumentRepository repository, IUserContext userContext) { _repository = repository; _userContext = userContext; }
    [AuthorizationAspect(ResourceCode = "manage_approvals", ActionCode = "manage")]
    [AuditTrail]
    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]
    public async Task<Result> Handle(ReUploadUserDocumentCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_userContext.UserId)) return Result.Failure("Authenticated user is required.");
        var entity = await _repository.GetByIdAsync(request.UserDocumentId, cancellationToken);
        if (entity is null) return Result.Failure("User document not found.");
        var result = entity.ReUpload(request.NewIssueDate, request.NewExpirationDate, TextValueObject.Create(request.NewFileStoragePath), request.NewFileChecksum, ActorId.Create(_userContext.UserId));
        if (result.IsFailure) return result;
        await _repository.UpdateAsync(entity, cancellationToken);
        await _repository.UnitOfWork.SaveEntitiesAsync(cancellationToken);
        return Result.Success();
    }
}
