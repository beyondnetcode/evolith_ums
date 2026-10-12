using Evolith.Ums.Application.Approvals.DocumentType.DTOs;
using Evolith.Ums.Domain.Approvals;
using Evolith.Ums.Domain.Approvals.DocumentType;

namespace Evolith.Ums.Application.Approvals.DocumentType.Queries;

public sealed class GetDocumentTypeByIdQueryHandler : IQueryHandler<GetDocumentTypeByIdQuery, DocumentTypeDto>
{
    private readonly IDocumentTypeRepository _repository;

    public GetDocumentTypeByIdQueryHandler(IDocumentTypeRepository repository) => _repository = repository;

    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]

    [AuthorizationAspect(ResourceCode = "view_approval_rules", ActionCode = "view")]
    public async Task<Result<DocumentTypeDto>> Handle(GetDocumentTypeByIdQuery request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(request.DocumentTypeId, cancellationToken);
        if (entity is null) return Result<DocumentTypeDto>.Failure("Document type not found.");

        return Result<DocumentTypeDto>.Success(new DocumentTypeDto(
            entity.Props.Id.GetValue(), entity.Props.TenantId.GetValue(), entity.Props.Code.GetValue(),
            entity.Props.Name.GetValue(), entity.Props.Description.GetValue(), entity.Props.Criticity.ToString()));
    }
}
