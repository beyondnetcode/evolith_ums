namespace Evolith.Ums.Presentation.Endpoints.Identity.Onboarding;

using Evolith.Ums.Application.Approvals.ApprovalRequest.DTOs;
using Evolith.Ums.Application.Approvals.ApprovalRequest.Queries;
using Evolith.Ums.Application.Identity.UserAccount.DTOs;
using Evolith.Ums.Application.Identity.UserAccount.Queries;

public static class OnboardingInboxEndpoints
{
    public static IEndpointRouteBuilder MapOnboardingInboxEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/onboarding/inbox")
            .WithTags("Onboarding Inbox")
            .RequireAuthorization();

        group.MapGet("/user-signups", async (
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetPendingUserSignupRequestsQuery(), ct);
            return result.ToOk(context);
        })
        .WithName("GetPendingUserSignups")
        .WithSummary("Get pending user signup requests for the current tenant.")
        .Produces<IReadOnlyList<PendingUserSignupDto>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapGet("/profile-requests", async (
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetPendingProfileRequestsQuery(), ct);
            return result.ToOk(context);
        })
        .WithName("GetPendingProfileRequests")
        .WithSummary("Get pending profile access requests for the current tenant.")
        .Produces<IReadOnlyList<PendingProfileRequestDto>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        return app;
    }
}
