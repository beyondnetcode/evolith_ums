namespace Evolith.Ums.Domain.Configuration.IdpConfiguration;

using Evolith.Ums.Domain.Configuration.IdpConfiguration.Events;

public sealed class IdpConfiguration : AggregateRoot<IdpConfiguration, IdpConfigurationProps>
{
    public new IdpConfigurationDomainEventsManager DomainEvents { get; }

    private IdpConfiguration(IdpConfigurationProps props) : base(props)
    {
        DomainEvents = new IdpConfigurationDomainEventsManager(this);

        if (TrackingState.IsNew)
        {
            DomainEvents.RaiseEvent(new IdpConfigRegisteredEvent(
                props.Id.GetValue(), props.TenantId.GetValue(), props.ProviderType.Name, props.Version));
        }
    }

    public TenantId TenantId => Props.TenantId;
    public SystemSuiteId SystemSuiteId => Props.SystemSuiteId;
    public ProviderType ProviderType => Props.ProviderType;
    public IdpConfigStatus Status => Props.Status;
    public int ResolutionPriority => Props.ResolutionPriority;
    public int Version => Props.Version;

    public IdpConfigurationId GetId() => IdpConfigurationId.Load(Props.Id.GetValue());

    public static Result<IdpConfiguration> Create(
        TenantId tenantId,
        SystemSuiteId systemSuiteId,
        ProviderType providerType,
        string[] domainHints,
        string configPayload,
        string secretRef,
        int resolutionPriority,
        Guid? fallbackToId,
        ActorId createdBy)
    {
        if (string.IsNullOrWhiteSpace(configPayload))
        {
            return Result<IdpConfiguration>.Failure(DomainErrors.Configuration.IdpConfigPayloadInvalid);
        }

        var props = new IdpConfigurationProps(
            IdValueObject.Create(), tenantId, systemSuiteId, providerType,
            domainHints, configPayload, secretRef, resolutionPriority, fallbackToId, createdBy);

        var config = new IdpConfiguration(props);

        if (!config.IsValid())
        {
            return Result<IdpConfiguration>.Failure(config.BrokenRules.GetBrokenRulesAsString());
        }

        return Result<IdpConfiguration>.Success(config);
    }

    public Result Activate(ActorId updatedBy)
    {
        if (Status == IdpConfigStatus.Active)
        {
            BrokenRules.Add(new BrokenRule(nameof(Status), DomainErrors.Configuration.IdpConfigAlreadyActive));
        }

        if (!IsValid())
        {
            return Result.Failure(BrokenRules.GetBrokenRulesAsString());
        }

        Props.Status = IdpConfigStatus.Active;
        DomainEvents.RaiseEvent(new IdpConfigActivatedEvent(Props.Id.GetValue(), Props.TenantId.GetValue()));
        TrackingState.MarkAsDirty();
        Props.Audit.Update(updatedBy.GetValue());
        return Result.Success();
    }

    public Result Deactivate(ActorId updatedBy)
    {
        if (Status != IdpConfigStatus.Active)
        {
            BrokenRules.Add(new BrokenRule(nameof(Status), DomainErrors.Configuration.IdpConfigAlreadyInactive));
        }

        if (!IsValid())
        {
            return Result.Failure(BrokenRules.GetBrokenRulesAsString());
        }

        Props.Status = IdpConfigStatus.Inactive;
        DomainEvents.RaiseEvent(new IdpConfigDeactivatedEvent(Props.Id.GetValue(), Props.TenantId.GetValue()));
        TrackingState.MarkAsDirty();
        Props.Audit.Update(updatedBy.GetValue());
        return Result.Success();
    }

    public Result Archive(ActorId updatedBy)
    {
        if (Status == IdpConfigStatus.Archived)
        {
            BrokenRules.Add(new BrokenRule(nameof(Status), DomainErrors.Configuration.IdpConfigAlreadyArchived));
        }

        if (Status == IdpConfigStatus.Active)
        {
            BrokenRules.Add(new BrokenRule(nameof(Status), DomainErrors.Configuration.IdpConfigAlreadyActive));
        }

        if (!IsValid())
        {
            return Result.Failure(BrokenRules.GetBrokenRulesAsString());
        }

        Props.Status = IdpConfigStatus.Archived;
        TrackingState.MarkAsDirty();
        Props.Audit.Update(updatedBy.GetValue());
        return Result.Success();
    }

    public Result Update(string configPayload, string secretRef, string[] domainHints, ActorId updatedBy)
    {
        if (Status != IdpConfigStatus.Draft && Status != IdpConfigStatus.Inactive)
        {
            BrokenRules.Add(new BrokenRule(nameof(Status),
                Status == IdpConfigStatus.Archived
                    ? DomainErrors.Configuration.IdpConfigArchivedCannotChange
                    : DomainErrors.Configuration.IdpConfigNotDraft));
        }

        if (string.IsNullOrWhiteSpace(configPayload))
        {
            BrokenRules.Add(new BrokenRule(nameof(configPayload), DomainErrors.Configuration.IdpConfigPayloadInvalid));
        }

        if (!IsValid())
        {
            return Result.Failure(BrokenRules.GetBrokenRulesAsString());
        }

        Props.ConfigPayload = configPayload;
        Props.SecretRef = secretRef;
        Props.DomainHints = domainHints;
        Props.Version++;
        DomainEvents.RaiseEvent(new IdpConfigUpdatedEvent(Props.Id.GetValue(), Props.TenantId.GetValue(), Props.Version));
        TrackingState.MarkAsDirty();
        Props.Audit.Update(updatedBy.GetValue());
        return Result.Success();
    }
}
