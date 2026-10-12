namespace Evolith.Ums.Presentation.Endpoints.Authorization.Profile.Queries;

using Evolith.Ums.Application.Authorization.Profile.DTOs;
using Evolith.Ums.Application.Authorization.Profile.Queries;

public static class ProfileQueryEndpoints
{
    public static IEndpointRouteBuilder MapProfileQueryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/profiles")
            .WithTags("Profiles - Queries");

        group.MapGet("/{profileId:guid}", async (Guid profileId, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetProfileByIdQuery(profileId), ct);
            return result.ToOk(context);
        })
        .WithName("GetProfileById")
        .WithSummary("Get profile by ID")
        .Produces<ProfileDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
