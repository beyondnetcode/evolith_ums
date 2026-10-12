namespace Evolith.Ums.Presentation.Endpoints.Authorization.Role;

using Evolith.Ums.Application.Authorization.Role.Commands;
using Evolith.Ums.Application.Authorization.Role.DTOs;
using Evolith.Ums.Application.Authorization.Role.Queries;

public static class RoleEndpoints
{
    public static IEndpointRouteBuilder MapRoleEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/system-suites/{systemSuiteId:guid}/roles")
            .WithTags("SystemSuite Roles");

        group.MapGet("/", async (Guid systemSuiteId, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetRolesBySystemSuiteQuery(systemSuiteId), ct);
            return result.ToOk(context);
        })
        .WithName("GetRolesBySystemSuite")
        .WithSummary("Get roles maintained by a system suite")
        .Produces<IReadOnlyList<RoleDto>>(StatusCodes.Status200OK);

        group.MapPost("/", async (Guid systemSuiteId, CreateRoleCommand command, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var request = command with { SystemSuiteId = systemSuiteId };
            var result = await mediator.Send(request, ct);
            return result.ToCreated(x => $"/system-suites/{systemSuiteId}/roles/{x.RoleId}", context);
        })
        .WithName("CreateRole")
        .WithSummary("Register a role for a system suite")
        .Produces<CreateRoleResponse>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/{roleId:guid}", async (Guid roleId, UpdateRoleCommand command, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(command with { RoleId = roleId }, ct);
            return result.ToNoContent(context);
        })
        .WithName("UpdateRole")
        .WithSummary("Update role descriptive and hierarchy data")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{roleId:guid}/activate", async (Guid roleId, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(new SetRoleStatusCommand(roleId, true), ct);
            return result.ToNoContent(context);
        })
        .WithName("ActivateRole")
        .WithSummary("Activate a role")
        .Produces(StatusCodes.Status204NoContent);

        group.MapPost("/{roleId:guid}/deactivate", async (Guid roleId, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(new SetRoleStatusCommand(roleId, false), ct);
            return result.ToNoContent(context);
        })
        .WithName("DeactivateRole")
        .WithSummary("Deactivate a role")
        .Produces(StatusCodes.Status204NoContent);

        return app;
    }
}
