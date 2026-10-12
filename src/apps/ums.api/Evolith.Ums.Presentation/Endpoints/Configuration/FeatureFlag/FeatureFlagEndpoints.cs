namespace Evolith.Ums.Presentation.Endpoints.Configuration.FeatureFlag;

using Evolith.Ums.Application.Common;
using Evolith.Ums.Application.Configuration.FeatureFlag.Commands;
using Evolith.Ums.Application.Configuration.FeatureFlag.DTOs;
using Evolith.Ums.Application.Configuration.FeatureFlag.Queries;

public static class FeatureFlagEndpoints
{
    public static IEndpointRouteBuilder MapFeatureFlagEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/feature-flags").WithTags("FeatureFlags");

        group.MapGet("/", async (
            [FromQuery] int page,
            [FromQuery] int pageSize,
            [FromQuery] string? search,
            [FromQuery] string? criteria,
            [FromQuery] string? status,
            [FromQuery] string? sortBy,
            [FromQuery] string? sortOrder,
            [FromQuery] string? flagType,
            [FromQuery] string? linkedResourceType,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetAllFeatureFlagsQuery(
                page <= 0 ? 1 : page,
                pageSize <= 0 ? 20 : pageSize,
                search,
                string.IsNullOrWhiteSpace(criteria) ? "flagCode" : criteria,
                string.IsNullOrWhiteSpace(status) ? "all" : status,
                string.IsNullOrWhiteSpace(sortBy) ? "flagCode" : sortBy,
                string.IsNullOrWhiteSpace(sortOrder) ? "asc" : sortOrder,
                flagType,
                linkedResourceType), ct);
            return result.ToOk(context);
        })
        .WithName("GetAllFeatureFlags")
        .Produces<PagedResult<FeatureFlagDto>>(StatusCodes.Status200OK);

        group.MapPost("/", async (CreateFeatureFlagCommand command, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(command, ct);
            return result.ToCreated(r => $"/feature-flags/{r.FeatureFlagId}", context);
        })
        .WithName("CreateFeatureFlag")
        .Produces<CreateFeatureFlagResponse>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/{featureFlagId:guid}", async (Guid featureFlagId, UpdateFeatureFlagRequest request, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(new UpdateFeatureFlagCommand(featureFlagId, request.FlagTargets, request.RolloutPercentage), ct);
            return result.ToNoContent(context);
        })
        .WithName("UpdateFeatureFlag")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{featureFlagId:guid}/activate", async (Guid featureFlagId, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(new ActivateFeatureFlagCommand(featureFlagId), ct);
            return result.ToNoContent(context);
        })
        .WithName("ActivateFeatureFlag")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{featureFlagId:guid}/deactivate", async (Guid featureFlagId, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(new DeactivateFeatureFlagCommand(featureFlagId), ct);
            return result.ToNoContent(context);
        })
        .WithName("DeactivateFeatureFlag")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{featureFlagId:guid}/archive", async (Guid featureFlagId, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(new ArchiveFeatureFlagCommand(featureFlagId), ct);
            return result.ToNoContent(context);
        })
        .WithName("ArchiveFeatureFlag")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{featureFlagId:guid}/evaluate", async (Guid featureFlagId, EvaluateFeatureFlagRequest request, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(new EvaluateFeatureFlagCommand(
                featureFlagId,
                request.TenantId,
                request.BranchId,
                request.ProfileId,
                request.RoleCode,
                request.Environment,
                request.CustomAttributes), ct);
            return result.ToOk(context);
        })
        .WithName("EvaluateFeatureFlag")
        .Produces<EvaluateFeatureFlagResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{featureFlagId:guid}/criteria", async (Guid featureFlagId, AddFeatureFlagCriteriaRequest request, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(new AddFeatureFlagCriteriaCommand(
                featureFlagId,
                request.CriteriaType,
                request.Operator,
                request.Value), ct);
            return result.ToCreated(r => $"/feature-flags/{featureFlagId}/criteria/{r.CriteriaId}", context);
        })
        .WithName("AddFeatureFlagCriteria")
        .Produces<AddFeatureFlagCriteriaResponse>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapDelete("/{featureFlagId:guid}/criteria/{criteriaId:guid}", async (Guid featureFlagId, Guid criteriaId, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(new RemoveFeatureFlagCriteriaCommand(featureFlagId, criteriaId), ct);
            return result.ToNoContent(context);
        })
        .WithName("RemoveFeatureFlagCriteria")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }

    public sealed record UpdateFeatureFlagRequest(string FlagTargets, int? RolloutPercentage);
    public sealed record EvaluateFeatureFlagRequest(
        Guid? TenantId = null,
        Guid? BranchId = null,
        Guid? ProfileId = null,
        string? RoleCode = null,
        string? Environment = null,
        Dictionary<string, string>? CustomAttributes = null);
    public sealed record AddFeatureFlagCriteriaRequest(string CriteriaType, string Operator, string Value);
}
