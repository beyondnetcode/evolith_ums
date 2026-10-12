namespace Evolith.Ums.Presentation.Endpoints.Identity.Tenant.Queries;

using Evolith.Ums.Application.Identity.Tenant.IdentityProvider.DTOs;
using Evolith.Ums.Application.Identity.Tenant.IdentityProvider.Queries;

public static class IdentityProviderQueryEndpoints
{
    public static IEndpointRouteBuilder MapIdentityProviderQueryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/tenants/{tenantId:guid}/identity-providers")
            .WithTags("Identity Providers - Queries");

        group.MapGet("/", async (Guid tenantId, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetIdentityProvidersByTenantIdQuery(tenantId), ct);
            return result.ToOk(context);
        })
        .WithName("GetIdentityProvidersByTenantId")
        .WithSummary("Get all identity providers for a tenant")
        .Produces<IReadOnlyList<IdentityProviderDto>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
