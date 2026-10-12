namespace Evolith.Ums.Presentation.Endpoints.Approvals.AccessEnforcementPolicy.Queries;

using Evolith.Ums.Application.Approvals.AccessEnforcementPolicy.DTOs;
using Evolith.Ums.Application.Approvals.AccessEnforcementPolicy.Queries;

public static class AccessEnforcementPolicyQueryEndpoints
{
    public static IEndpointRouteBuilder MapAccessEnforcementPolicyQueryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/access-enforcement-policies").WithTags("AccessEnforcementPolicies - Queries");

        group.MapGet("/{id:guid}", async (Guid id, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetAccessEnforcementPolicyByIdQuery(id), ct);
            return result.ToOk(context);
        }).WithName("GetAccessEnforcementPolicyById").Produces<AccessEnforcementPolicyDto>(StatusCodes.Status200OK).ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
