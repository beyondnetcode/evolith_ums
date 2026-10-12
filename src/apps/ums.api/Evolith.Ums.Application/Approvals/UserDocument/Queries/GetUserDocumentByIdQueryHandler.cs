using Evolith.Ums.Application.Approvals.UserDocument.DTOs;
using Evolith.Ums.Domain.Approvals;
using Evolith.Ums.Domain.Approvals.UserDocument;

namespace Evolith.Ums.Application.Approvals.UserDocument.Queries;

public sealed class GetUserDocumentByIdQueryHandler : IQueryHandler<GetUserDocumentByIdQuery, UserDocumentDto>
{
    private readonly IUserDocumentRepository _repository;

    public GetUserDocumentByIdQueryHandler(IUserDocumentRepository repository) => _repository = repository;

    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]

    [AuthorizationAspect(ResourceCode = "view_approvals", ActionCode = "view")]
    public async Task<Result<UserDocumentDto>> Handle(GetUserDocumentByIdQuery request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(request.UserDocumentId, cancellationToken);
        if (entity is null) return Result<UserDocumentDto>.Failure("User document not found.");

        return Result<UserDocumentDto>.Success(new UserDocumentDto(
            entity.Props.Id.GetValue(), entity.Props.UserId.GetValue(), entity.Props.DocumentTypeId.GetValue(),
            entity.Props.IssueDate, entity.Props.ExpirationDate, entity.Props.Status.ToString(),
            entity.Props.Criticity.ToString(), entity.Props.FileStoragePath.GetValue(),
            entity.Props.FileChecksum, entity.Props.NotificationStep));
    }
}
