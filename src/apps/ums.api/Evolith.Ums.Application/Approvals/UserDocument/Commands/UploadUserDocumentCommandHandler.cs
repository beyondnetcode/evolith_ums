using Evolith.Ums.Application.Approvals.UserDocument.DTOs;

namespace Evolith.Ums.Application.Approvals.UserDocument.Commands;

using Evolith.Ums.Domain;
using Evolith.Ums.Domain.Approvals;
using Evolith.Ums.Domain.Approvals.UserDocument;
using Evolith.Ums.Domain.Enums;

public sealed class UploadUserDocumentCommandHandler : ICommandHandler<UploadUserDocumentCommand, UploadUserDocumentResponse>
{
    private readonly IUserDocumentRepository _repository;
    private readonly IUserContext _userContext;

    public UploadUserDocumentCommandHandler(IUserDocumentRepository repository, IUserContext userContext)
    {
        _repository = repository;
        _userContext = userContext;
    }

    [AuthorizationAspect(ResourceCode = "manage_approvals", ActionCode = "manage")]
    [AuditTrail]
    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]
    public async Task<Result<UploadUserDocumentResponse>> Handle(UploadUserDocumentCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_userContext.UserId))
            return Result<UploadUserDocumentResponse>.Failure("Authenticated user is required.");

        var criticity = DomainEnumerationParser.FromName<DocumentCriticity>(request.Criticity)!;

        var result = UserDocument.Upload(
            UserId.Load(request.UserId),
            DocumentTypeId.Load(request.DocumentTypeId),
            request.IssueDate,
            request.ExpirationDate,
            criticity,
            TextValueObject.Create(request.FileStoragePath),
            request.FileChecksum,
            ActorId.Create(_userContext.UserId));

        if (result.IsFailure) return Result<UploadUserDocumentResponse>.Failure(result.Error);

        await _repository.AddAsync(result.Value, cancellationToken);
        await _repository.UnitOfWork.SaveEntitiesAsync(cancellationToken);

        return Result<UploadUserDocumentResponse>.Success(new UploadUserDocumentResponse(result.Value.Props.Id.GetValue()));
    }
}
