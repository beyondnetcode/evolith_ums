#pragma warning disable S1144, S1186
namespace Evolith.Ums.Domain.Authorization.AssignmentRule.Events;

using Evolith.Ums.Domain.Events;

public sealed class TemplateAssignmentRuleDomainEventsManager : DomainEventsManager
{
    public TemplateAssignmentRuleDomainEventsManager(IAggregateRoot aggregateRoot) : base(aggregateRoot) { }

    private void Apply(AssignmentRuleCreatedEvent @event) { }
    private void Apply(AssignmentRuleDeactivatedEvent @event) { }
    private void Apply(AssignmentRuleReactivatedEvent @event) { }
}

#pragma warning restore S1144, S1186
