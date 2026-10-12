namespace Evolith.Ums.Presentation.Endpoints.Approvals.AccessEnforcementPolicy;

using Evolith.Ums.Application.Approvals.AccessEnforcementPolicy.Commands;
using Evolith.Ums.Application.Approvals.AccessEnforcementPolicy.DTOs;
using Evolith.Ums.Application.Approvals.AccessEnforcementPolicy.Queries;
using Evolith.Ums.Application.Common;
using Evolith.Ums.Presentation.Endpoints.Common;

public static class AccessEnforcementPolicyEndpoints
{
    public static IEndpointRouteBuilder MapAccessEnforcementPolicyEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/access-enforcement-policies").WithTags("AccessEnforcementPolicies");

        group.MapGet("/", async (
            [AsParameters] TenantPagedQueryParameters query,
            IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetAllAccessEnforcementPoliciesQuery(
                query.Page <= 0 ? 1 : query.Page, query.PageSize <= 0 ? 20 : query.PageSize, query.Search,
                string.IsNullOrWhiteSpace(query.Criteria) ? "enforcementAction" : query.Criteria,
                string.IsNullOrWhiteSpace(query.Status) ? "all" : query.Status,
                string.IsNullOrWhiteSpace(query.SortBy) ? "enforcementAction" : query.SortBy,
                string.IsNullOrWhiteSpace(query.SortOrder) ? "asc" : query.SortOrder, query.TenantId), ct);
            return result.ToOk(context);
        }).WithName("GetAllAccessEnforcementPolicies").Produces<PagedResult<AccessEnforcementPolicyDto>>(StatusCodes.Status200OK);

        group.MapPost("/", async (CreateAccessEnforcementPolicyCommand command, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(command, ct);
            return result.ToCreated(r => $"/access-enforcement-policies/{r.AccessEnforcementPolicyId}", context);
        }).WithName("CreateAccessEnforcementPolicy").Produces<CreateAccessEnforcementPolicyResponse>(StatusCodes.Status201Created).ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapPost("/{id:guid}/deactivate", async (Guid id, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(new DeactivateAccessEnforcementPolicyCommand(id), ct);
            return result.ToNoContent(context);
        }).WithName("DeactivateAccessEnforcementPolicy").Produces(StatusCodes.Status204NoContent).ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/{policyId:guid}/action", async (Guid policyId, UpdateAccessEnforcementActionCommand command, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(command with { PolicyId = policyId }, ct);
            return result.ToNoContent(context);
        }).WithName("UpdateAccessEnforcementAction")
          .WithSummary("Change the enforcement action (BlockUser, RestrictProfile, LogOnly)")
          .Produces(StatusCodes.Status204NoContent)
          .ProducesProblem(StatusCodes.Status400BadRequest)
          .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
