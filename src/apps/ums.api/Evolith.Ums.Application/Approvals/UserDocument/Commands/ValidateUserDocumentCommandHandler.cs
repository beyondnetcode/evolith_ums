using Evolith.Ums.Application.Approvals.UserDocument.DTOs;

namespace Evolith.Ums.Application.Approvals.UserDocument.Commands;

using Evolith.Ums.Application.Common.Notifications;
using Evolith.Ums.Domain.Approvals;
using Evolith.Ums.Domain.Identity;

public sealed class ValidateUserDocumentCommandHandler : ICommandHandler<ValidateUserDocumentCommand>
{
    private readonly IUserDocumentRepository _repository;
    private readonly IUserAccountRepository  _userAccountRepository;
    private readonly INotificationService    _notificationService;
    private readonly IUserContext            _userContext;

    public ValidateUserDocumentCommandHandler(
        IUserDocumentRepository repository,
        IUserAccountRepository userAccountRepository,
        INotificationService notificationService,
        IUserContext userContext)
    {
        _repository            = repository;
        _userAccountRepository = userAccountRepository;
        _notificationService   = notificationService;
        _userContext           = userContext;
    }

    [AuthorizationAspect(ResourceCode = "manage_approvals", ActionCode = "manage")]
    [AuditTrail]
    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]
    public async Task<Result> Handle(ValidateUserDocumentCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_userContext.UserId))
            return Result.Failure("Authenticated user is required.");

        var entity = await _repository.GetByIdAsync(request.UserDocumentId, cancellationToken);
        if (entity is null) return Result.Failure("User document not found.");

        var result = entity.Validate(ActorId.Create(_userContext.UserId));
        if (result.IsFailure) return result;

        await _repository.UpdateAsync(entity, cancellationToken);
        await _repository.UnitOfWork.SaveEntitiesAsync(cancellationToken);

        var owner = await _userAccountRepository.GetByIdAsync(entity.UserId.GetValue(), cancellationToken);
        if (owner is not null)
        {
            await _notificationService.SendAsync(
                NotificationTemplates.UserDocumentValidated(
                    owner.Email.GetValue(),
                    owner.DisplayName?.GetValue() ?? owner.Email.GetValue(),
                    entity.DocumentTypeId.GetValue().ToString()),
                cancellationToken);
        }

        return Result.Success();
    }
}
