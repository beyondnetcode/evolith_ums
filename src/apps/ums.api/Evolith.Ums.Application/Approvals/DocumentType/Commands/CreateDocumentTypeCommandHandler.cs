using Evolith.Ums.Application.Approvals.DocumentType.DTOs;

namespace Evolith.Ums.Application.Approvals.DocumentType.Commands;

using Evolith.Ums.Domain;
using Evolith.Ums.Domain.Approvals;
using Evolith.Ums.Domain.Approvals.DocumentType;
using Evolith.Ums.Domain.Enums;

public sealed class CreateDocumentTypeCommandHandler : ICommandHandler<CreateDocumentTypeCommand, CreateDocumentTypeResponse>
{
    private readonly IDocumentTypeRepository _repository;
    private readonly IUserContext _userContext;

    public CreateDocumentTypeCommandHandler(IDocumentTypeRepository repository, IUserContext userContext)
    {
        _repository = repository;
        _userContext = userContext;
    }

    [AuthorizationAspect(ResourceCode = "manage_approval_rules", ActionCode = "manage")]
    [AuditTrail]
    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]
    public async Task<Result<CreateDocumentTypeResponse>> Handle(CreateDocumentTypeCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_userContext.UserId))
            return Result<CreateDocumentTypeResponse>.Failure("Authenticated user is required.");

        var criticity = DomainEnumerationParser.FromName<DocumentCriticity>(request.Criticity)!;

        var result = DocumentType.Create(
            TenantId.Load(request.TenantId),
            Code.Create(request.Code),
            Name.Create(request.Name),
            Description.Create(request.Description),
            criticity,
            ActorId.Create(_userContext.UserId));

        if (result.IsFailure) return Result<CreateDocumentTypeResponse>.Failure(result.Error);

        await _repository.AddAsync(result.Value, cancellationToken);
        await _repository.UnitOfWork.SaveEntitiesAsync(cancellationToken);

        return Result<CreateDocumentTypeResponse>.Success(new CreateDocumentTypeResponse(result.Value.Props.Id.GetValue()));
    }
}
