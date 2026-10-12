using Evolith.Ums.Application.Approvals.NotificationRule.DTOs;
using Evolith.Ums.Application.Approvals.NotificationRule.Services;

namespace Evolith.Ums.Application.Approvals.NotificationRule.Commands;

using Evolith.Ums.Domain;
using Evolith.Ums.Domain.Approvals;
using Evolith.Ums.Domain.Approvals.NotificationRule;
using Evolith.Ums.Domain.Enums;

public sealed class CreateNotificationRuleCommandHandler : ICommandHandler<CreateNotificationRuleCommand, CreateNotificationRuleResponse>
{
    private readonly INotificationRuleRepository _repository;
    private readonly IUserContext _userContext;
    private readonly INotificationRecipientResolver _recipientResolver;

    public CreateNotificationRuleCommandHandler(
        INotificationRuleRepository repository,
        IUserContext userContext,
        INotificationRecipientResolver recipientResolver)
    {
        _repository = repository;
        _userContext = userContext;
        _recipientResolver = recipientResolver;
    }

    [AuthorizationAspect(ResourceCode = "manage_approval_rules", ActionCode = "manage")]
    [AuditTrail]
    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]
    public async Task<Result<CreateNotificationRuleResponse>> Handle(CreateNotificationRuleCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_userContext.UserId))
            return Result<CreateNotificationRuleResponse>.Failure("Authenticated user is required.");

        var channel = DomainEnumerationParser.FromName<NotificationChannel>(request.Channel);
        if (channel is null)
            return Result<CreateNotificationRuleResponse>.Failure($"Notification channel '{request.Channel}' is not supported.");

        var normalizedRecipient = _recipientResolver.Normalize(channel.Name, request.Recipient);
        if (normalizedRecipient.IsFailure)
            return Result<CreateNotificationRuleResponse>.Failure(normalizedRecipient.Error);

        var isDuplicate = await _repository.ExistsDuplicateAsync(
            request.TenantId, channel.Name, normalizedRecipient.Value, cancellationToken);
        if (isDuplicate)
            return Result<CreateNotificationRuleResponse>.Failure(DomainErrors.Approvals.DuplicateNotificationRule);

        var result = NotificationRule.Create(
            TenantId.Load(request.TenantId),
            channel,
            TextValueObject.Create(normalizedRecipient.Value),
            ActorId.Create(_userContext.UserId));

        if (result.IsFailure) return Result<CreateNotificationRuleResponse>.Failure(result.Error);

        await _repository.AddAsync(result.Value, cancellationToken);
        await _repository.UnitOfWork.SaveEntitiesAsync(cancellationToken);

        return Result<CreateNotificationRuleResponse>.Success(new CreateNotificationRuleResponse(result.Value.Props.Id.GetValue()));
    }
}
