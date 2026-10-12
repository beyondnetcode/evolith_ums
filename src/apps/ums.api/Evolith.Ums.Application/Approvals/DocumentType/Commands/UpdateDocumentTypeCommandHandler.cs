namespace Evolith.Ums.Application.Approvals.DocumentType.Commands;
using Evolith.Ums.Domain.Approvals;
public sealed class UpdateDocumentTypeCommandHandler : ICommandHandler<UpdateDocumentTypeCommand>
{
    private readonly IDocumentTypeRepository _repository;
    private readonly IUserContext _userContext;
    public UpdateDocumentTypeCommandHandler(IDocumentTypeRepository repository, IUserContext userContext) { _repository = repository; _userContext = userContext; }
    [AuthorizationAspect(ResourceCode = "manage_approval_rules", ActionCode = "manage")]
    [AuditTrail]
    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]
    public async Task<Result> Handle(UpdateDocumentTypeCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_userContext.UserId)) return Result.Failure("Authenticated user is required.");
        var entity = await _repository.GetByIdAsync(request.DocumentTypeId, cancellationToken);
        if (entity is null) return Result.Failure("Document type not found.");
        var result = entity.Update(Name.Create(request.Name), Description.Create(request.Description), ActorId.Create(_userContext.UserId));
        if (result.IsFailure) return result;
        await _repository.UpdateAsync(entity, cancellationToken);
        await _repository.UnitOfWork.SaveEntitiesAsync(cancellationToken);
        return Result.Success();
    }
}
