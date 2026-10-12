namespace Evolith.Ums.Domain.Approvals.NotificationRule;

public sealed class NotificationRule : AggregateRoot<NotificationRule, NotificationRuleProps>
{
    private NotificationRule(NotificationRuleProps props) : base(props)
    {
    }

    public TenantId TenantId => Props.TenantId;
    public NotificationChannel Channel => Props.Channel;
    public TextValueObject Recipient => Props.Recipient;
    public bool IsActive => Props.IsActive;

    public NotificationRuleId GetId() => NotificationRuleId.Load(Props.Id.GetValue());

    public static Result<NotificationRule> Create(
        TenantId tenantId,
        NotificationChannel channel,
        TextValueObject recipient,
        ActorId createdBy)
    {
        if (string.IsNullOrWhiteSpace(recipient?.GetValue()))
        {
            return Result<NotificationRule>.Failure(DomainErrors.ValueObject.PropertyRequired);
        }

        var props = new NotificationRuleProps(IdValueObject.Create(), tenantId, channel, recipient, true, createdBy);
        var rule = new NotificationRule(props);

        if (!rule.IsValid())
        {
            return Result<NotificationRule>.Failure(rule.BrokenRules.GetBrokenRulesAsString());
        }

        return Result<NotificationRule>.Success(rule);
    }

    public Result Deactivate(ActorId updatedBy)
    {
        if (!IsActive)
        {
            BrokenRules.Add(new BrokenRule(nameof(IsActive), DomainErrors.Approvals.RuleAlreadyInactive));
        }

        if (!IsValid())
        {
            return Result.Failure(BrokenRules.GetBrokenRulesAsString());
        }

        Props.IsActive = false;
        TrackingState.MarkAsDirty();
        Props.Audit.Update(updatedBy.GetValue());
        return Result.Success();
    }

    public Result Reactivate(ActorId updatedBy)
    {
        if (IsActive)
        {
            BrokenRules.Add(new BrokenRule(nameof(IsActive), DomainErrors.Approvals.RuleAlreadyActive));
        }

        if (!IsValid())
        {
            return Result.Failure(BrokenRules.GetBrokenRulesAsString());
        }

        Props.IsActive = true;
        TrackingState.MarkAsDirty();
        Props.Audit.Update(updatedBy.GetValue());
        return Result.Success();
    }

    public Result UpdateChannel(NotificationChannel newChannel, ActorId updatedBy)
    {
        Props.Channel = newChannel;
        TrackingState.MarkAsDirty();
        Props.Audit.Update(updatedBy.GetValue());
        return Result.Success();
    }

    public Result UpdateRecipient(TextValueObject newRecipient, ActorId updatedBy)
    {
        if (string.IsNullOrWhiteSpace(newRecipient?.GetValue()))
        {
            BrokenRules.Add(new BrokenRule(nameof(Recipient), DomainErrors.ValueObject.PropertyRequired));
            return Result.Failure(BrokenRules.GetBrokenRulesAsString());
        }

        Props.Recipient = newRecipient;
        TrackingState.MarkAsDirty();
        Props.Audit.Update(updatedBy.GetValue());
        return Result.Success();
    }
}
