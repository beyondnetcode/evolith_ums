using System.Threading;
using System.Threading.Tasks;
using Evolith.Ums.Domain.Authorization;
using Evolith.Ums.Domain.Authorization.SystemSuite;
using Evolith.Ums.Domain.Kernel;
using Evolith.Ums.Domain.Kernel.ValueObjects;
using Evolith.Ums.Application.Common;

namespace Evolith.Ums.Application.Authorization.SystemSuite.Commands;

public sealed class UpdateDomainResourceCommandHandler(ISystemSuiteRepository repository, IUserContext userContext)
    : ICommandHandler<UpdateDomainResourceCommand>
{
    [AuthorizationAspect(ResourceCode = "manage_suites", ActionCode = "manage")]
    public async Task<Result> Handle(UpdateDomainResourceCommand request, CancellationToken cancellationToken)
    {
        var suite = await repository.GetByIdAsync(request.SystemSuiteId, cancellationToken);
        if (suite is null)
        {
            return Result.Failure(DomainErrors.Common.NotFound);
        }

        var result = suite.UpdateDomainResource(
            IdValueObject.Load(request.DomainResourceId),
            Name.Create(request.Name),
            Description.Create(request.Description),
            ActorId.Create(userContext.UserId ?? string.Empty));

        if (result.IsFailure)
        {
            return result;
        }

        await repository.UpdateAsync(suite, cancellationToken);
        await repository.UnitOfWork.SaveEntitiesAsync(cancellationToken);

        return Result.Success();
    }
}
