using MassTransit;

namespace Evolith.Ums.Infrastructure.MasterData;

/// <summary>
/// Binds <see cref="TenantProjectionConsumer"/> to the canonical queue
/// <c>ums.tenant-projection</c> (ADR-0107 topology) with exponential retry before dead-lettering.
/// </summary>
public sealed class TenantProjectionConsumerDefinition : ConsumerDefinition<TenantProjectionConsumer>
{
    public TenantProjectionConsumerDefinition()
    {
        EndpointName = "ums.tenant-projection";
    }

    protected override void ConfigureConsumer(
        IReceiveEndpointConfigurator endpointConfigurator,
        IConsumerConfigurator<TenantProjectionConsumer> consumerConfigurator,
        IRegistrationContext context)
    {
        // DS-04: wire the EF inbox at the endpoint so InboxState actually dedups by messageId and
        // the consume runs in a transaction. The bus-level AddEntityFrameworkOutbox alone does NOT
        // activate consumer-side dedup — without this the InboxState table exists but is never used.
        endpointConfigurator.UseEntityFrameworkOutbox<TenantProjectionDbContext>(context);

        endpointConfigurator.UseMessageRetry(r =>
            r.Exponential(5, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(2)));
    }
}
