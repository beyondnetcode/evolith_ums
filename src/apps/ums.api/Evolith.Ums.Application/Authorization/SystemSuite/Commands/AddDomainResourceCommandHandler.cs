using System.Threading;
using System.Threading.Tasks;
using Evolith.Ums.Domain.Authorization;
using Evolith.Ums.Domain.Authorization.SystemSuite;
using Evolith.Ums.Domain.Authorization.SystemSuite.DomainResource;
using Evolith.Ums.Domain.Authorization.SystemSuite.Module;
using Evolith.Ums.Domain.Kernel;
using Evolith.Ums.Domain.Kernel.ValueObjects;
using Evolith.Ums.Application.Common;
using Evolith.Ums.Application.Authorization.SystemSuite.DTOs;

namespace Evolith.Ums.Application.Authorization.SystemSuite.Commands;

public sealed class AddDomainResourceCommandHandler(ISystemSuiteRepository repository, IUserContext userContext)
    : ICommandHandler<AddDomainResourceCommand, AddDomainResourceResponse>
{
    [AuthorizationAspect(ResourceCode = "manage_suites", ActionCode = "manage")]
    public async Task<Result<AddDomainResourceResponse>> Handle(AddDomainResourceCommand request, CancellationToken cancellationToken)
    {
        var suite = await repository.GetByIdAsync(request.SystemSuiteId, cancellationToken);
        if (suite is null)
        {
            return Result<AddDomainResourceResponse>.Failure(DomainErrors.Common.NotFound);
        }

        var type = DomainEnumerationParser.FromName<DomainResourceType>(request.Type);
        if (type is null) return Result<AddDomainResourceResponse>.Failure($"Invalid DomainResourceType: {request.Type}");

        var moduleId = request.ModuleId.HasValue ? ModuleId.Load(request.ModuleId.Value) : null;
        var parentId = request.ParentResourceId.HasValue ? IdValueObject.Load(request.ParentResourceId.Value) : null;

        var result = suite.AddDomainResource(
            moduleId,
            parentId,
            type,
            Code.Create(request.Code),
            Name.Create(request.Name),
            Description.Create(request.Description),
            ActorId.Create(userContext.UserId ?? string.Empty));

        if (result.IsFailure)
        {
            return Result<AddDomainResourceResponse>.Failure(result.Error);
        }

        await repository.UpdateAsync(suite, cancellationToken);
        await repository.UnitOfWork.SaveEntitiesAsync(cancellationToken);

        return Result<AddDomainResourceResponse>.Success(new AddDomainResourceResponse(result.Value));
    }
}
