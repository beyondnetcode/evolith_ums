using System.Threading;
using System.Threading.Tasks;
using Evolith.Ums.Application.Common;
using Evolith.Ums.Domain.Authorization;
using Evolith.Ums.Domain.Authorization.SystemSuite;
using Evolith.Ums.Domain.Kernel;
using Evolith.Ums.Domain.Kernel.ValueObjects;

namespace Evolith.Ums.Application.Authorization.SystemSuite.Commands;

public sealed class RemoveDomainResourceCommandHandler(
    ISystemSuiteRepository repository,
    IPermissionTemplateRepository templateRepository,
    IUserContext userContext)
    : ICommandHandler<RemoveDomainResourceCommand>
{
    [AuthorizationAspect(ResourceCode = "manage_suites", ActionCode = "manage")]
    public async Task<Result> Handle(RemoveDomainResourceCommand request, CancellationToken cancellationToken)
    {
        var suite = await repository.GetByIdAsync(request.SystemSuiteId, cancellationToken);
        if (suite is null)
        {
            return Result.Failure(DomainErrors.Common.NotFound);
        }

        // ── Dependency guard: active template items referencing this resource ──
        var templateItemCount = await templateRepository.CountItemsByTargetAsync(
            request.DomainResourceId, cancellationToken);

        if (templateItemCount > 0)
        {
            var deps = new List<BlockingDependency>
            {
                new("PermissionTemplateItem", "Active", templateItemCount),
            };
            return Result.Failure(BlockedOperationError.Encode(
                DomainErrors.Authorization.DomainResourceHasTemplateItems, deps));
        }

        var result = suite.RemoveDomainResource(
            IdValueObject.Load(request.DomainResourceId),
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
