#pragma warning disable S1144, S1186
namespace Evolith.Ums.Domain.IGA.RolePromotionRequest.Events;

using Evolith.Ums.Domain.Events;

public class RolePromotionRequestEventsManager : DomainEventsManager
{
    public RolePromotionRequestEventsManager(IAggregateRoot aggregateRoot) : base(aggregateRoot) { }

    private void Apply(RolePromotionRequestedEvent @event) { }
    private void Apply(RolePromotionSubmittedEvent @event) { }
    private void Apply(RolePromotionEligibilityConfirmedEvent @event) { }
    private void Apply(RolePromotionManagerApprovedEvent @event) { }
    private void Apply(RolePromotionSecurityApprovedEvent @event) { }
    private void Apply(RolePromotionExecutedEvent @event) { }
    private void Apply(RolePromotionVerifiedEvent @event) { }
    private void Apply(RolePromotionRejectedEvent @event) { }
    private void Apply(RolePromotionCancelledEvent @event) { }
}

#pragma warning restore S1144, S1186
