namespace Evolith.Ums.Presentation.Endpoints.Authorization.SystemSuite.Queries;

using Evolith.Ums.Application.Authorization.SystemSuite.DTOs;
using Evolith.Ums.Application.Authorization.SystemSuite.Queries;

public static class SystemSuiteQueryEndpoints
{
    public static IEndpointRouteBuilder MapSystemSuiteQueryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/system-suites")
            .WithTags("SystemSuites - Queries");

        group.MapGet("/{systemSuiteId:guid}", async (Guid systemSuiteId, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetSystemSuiteByIdQuery(systemSuiteId), ct);
            return result.ToOk(context);
        })
        .WithName("GetSystemSuiteById")
        .WithSummary("Get system suite by ID")
        .Produces<SystemSuiteDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
