namespace Evolith.Ums.Presentation.Endpoints.Configuration.FeatureFlag.Queries;

using Evolith.Ums.Application.Configuration.FeatureFlag.DTOs;
using Evolith.Ums.Application.Configuration.FeatureFlag.Queries;

public static class FeatureFlagQueryEndpoints
{
    public static IEndpointRouteBuilder MapFeatureFlagQueryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/feature-flags").WithTags("FeatureFlags - Queries");

        group.MapGet("/{id:guid}", async (Guid id, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetFeatureFlagByIdQuery(id), ct);
            return result.ToOk(context);
        })
        .WithName("GetFeatureFlagById")
        .Produces<FeatureFlagDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapGroup("/system-suites").WithTags("FeatureFlags - Queries")
            .MapGet("/{systemSuiteId:guid}/feature-flags", async (Guid systemSuiteId, IMediator mediator, HttpContext context, CancellationToken ct) =>
            {
                var result = await mediator.Send(new GetFeatureFlagsBySystemSuiteQuery(systemSuiteId), ct);
                return result.ToOk(context);
            })
            .WithName("GetFeatureFlagsBySystemSuite")
            .Produces<IReadOnlyList<FeatureFlagDto>>(StatusCodes.Status200OK);

        return app;
    }
}
