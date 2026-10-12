#pragma warning disable S1144, S1186
namespace Evolith.Ums.Domain.Configuration.FeatureFlag.Events;

using BeyondNetCode.Shell.Ddd.Services.Impl;

public class FeatureFlagDomainEventsManager : DomainEventsManager
{
    public FeatureFlagDomainEventsManager(IAggregateRoot aggregateRoot) : base(aggregateRoot) { }

    private void Apply(FeatureFlagCreatedEvent @event) { }
    private void Apply(FeatureFlagActivatedEvent @event) { }
    private void Apply(FeatureFlagDeactivatedEvent @event) { }
    private void Apply(FeatureFlagArchivedEvent @event) { }
    private void Apply(FeatureFlagStateChangedEvent @event) { }
    private void Apply(FlagEvaluatedEvent @event) { }
    private void Apply(FeatureFlagTargetingRulesUpdatedEvent @event) { }
    private void Apply(FeatureFlagCriteriaAddedEvent @event) { }
    private void Apply(FeatureFlagCriteriaRemovedEvent @event) { }
}

#pragma warning restore S1144, S1186
