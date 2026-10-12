namespace Evolith.Ums.Presentation.Endpoints.Authorization.Profile;

using BeyondNetCode.Shell.Factory.Interfaces;
using BeyondNetCode.Shell.Factory.Interfaces;
using Evolith.Ums.Application.Authorization.Graph;
using Evolith.Ums.Application.Authorization.Graph.Serializers;
using Evolith.Ums.Application.Authorization.Profile.Commands;
using Evolith.Ums.Application.Authorization.Profile.DTOs;
using Evolith.Ums.Application.Authorization.Profile.Exporters;
using Evolith.Ums.Application.Authorization.Profile.Queries;
using Evolith.Ums.Application.Common;
using Evolith.Ums.Application.Common.Interfaces;

public static class ProfileEndpoints
{
    public static IEndpointRouteBuilder MapProfileEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/profiles")
            .WithTags("Profiles");

        group.MapGet("/", async (
            [FromQuery] int page,
            [FromQuery] int pageSize,
            [FromQuery] string? search,
            [FromQuery] string? criteria,
            [FromQuery] string? status,
            [FromQuery] string? sortBy,
            [FromQuery] string? sortOrder,
            [FromQuery] Guid? tenantId,
            [FromQuery] Guid? userId,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetAllProfilesQuery(
                page <= 0 ? 1 : page,
                pageSize <= 0 ? 20 : pageSize,
                search,
                string.IsNullOrWhiteSpace(criteria) ? "userId" : criteria,
                string.IsNullOrWhiteSpace(status) ? "all" : status,
                string.IsNullOrWhiteSpace(sortBy) ? "userId" : sortBy,
                string.IsNullOrWhiteSpace(sortOrder) ? "asc" : sortOrder,
                tenantId,
                userId), ct);
            return result.ToOk(context);
        })
        .WithName("GetAllProfiles")
        .WithSummary("Get profiles using server-side pagination")
        .Produces<PagedResult<ProfileDto>>(StatusCodes.Status200OK);



        group.MapPost("/", async (CreateProfileCommand command, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(command, ct);
            return result.ToCreated(r => $"/profiles/{r.ProfileId}", context);
        })
        .WithName("CreateProfile")
        .WithSummary("Create a new profile")
        .Produces<CreateProfileResponse>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{profileId:guid}/activate", async (Guid profileId, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(new ActivateProfileCommand(profileId), ct);
            return result.ToNoContent(context);
        })
        .WithName("ActivateProfile")
        .WithSummary("Activate a deactivated profile")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{profileId:guid}/deactivate", async (Guid profileId, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(new DeactivateProfileCommand(profileId), ct);
            return result.ToNoContent(context);
        })
        .WithName("DeactivateProfile")
        .WithSummary("Deactivate an active profile")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{profileId:guid}/templates/{templateId:guid}", async (Guid profileId, Guid templateId, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(new AssignProfileTemplateCommand(profileId, templateId), ct);
            return result.ToNoContent(context);
        })
        .WithName("AssignProfileTemplate")
        .WithSummary("Assign a published permission template to the profile")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        // ADR-UMS-106 D4: desligar es una operacion del modelo, no un efecto colateral de asignar.
        // `DELETE` y no `POST /unassign` porque lo que se retira es el ENLACE, que es el recurso que
        // la ruta nombra; el permiso materializado se desactiva, no se borra.
        group.MapDelete("/{profileId:guid}/templates/{templateId:guid}", async (Guid profileId, Guid templateId, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(new UnassignProfileTemplateCommand(profileId, templateId), ct);
            return result.ToNoContent(context);
        })
        .WithName("UnassignProfileTemplate")
        .WithSummary("Unassign a permission template from the profile (deactivates what it granted)")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{profileId:guid}/permissions/{permissionId:guid}/override", async (Guid profileId, Guid permissionId, [FromQuery] string effect, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(new OverrideProfilePermissionCommand(profileId, permissionId, effect), ct);
            return result.ToNoContent(context);
        })
        .WithName("OverrideProfilePermission")
        .WithSummary("Override a profile permission effect")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{profileId:guid}/permissions/{permissionId:guid}/activate", async (Guid profileId, Guid permissionId, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(new SetProfilePermissionStatusCommand(profileId, permissionId, true), ct);
            return result.ToNoContent(context);
        })
        .WithName("ActivateProfilePermission")
        .WithSummary("Activate a profile permission")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{profileId:guid}/permissions/{permissionId:guid}/deactivate", async (Guid profileId, Guid permissionId, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(new SetProfilePermissionStatusCommand(profileId, permissionId, false), ct);
            return result.ToNoContent(context);
        })
        .WithName("DeactivateProfilePermission")
        .WithSummary("Deactivate a profile permission")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        // ── Auth Graph Preview (same pipeline as POST /client/authenticate) ──────
        group.MapGet("/{profileId:guid}/auth-graph/preview", async (
            Guid          profileId,
            [FromQuery]   string? format,
            IMediator     mediator,
            IUserContext  userContext,
            IAuthGraphFormatProvider formatProvider,
            IFactory      factory,
            HttpContext   context,
            CancellationToken ct) =>
        {
            if (!userContext.IsAuthenticated)
                return Results.Unauthorized();

            var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            var command = new PreviewProfileAuthGraphCommand(
                ProfileId:   profileId,
                AdminUserId: userContext.UserId ?? string.Empty,
                ClientIp:    clientIp);

            var result = await mediator.Send(command, ct);

            if (result.IsFailure)
            {
                var errorId = Guid.NewGuid();
                var isNotFound = result.Error.Contains("not found", StringComparison.OrdinalIgnoreCase);

                return isNotFound
                    ? Results.NotFound(new { error = result.Error, errorId })
                    : Results.BadRequest(new { error = result.Error, errorId });
            }

            var preview = result.Value;
            var graph   = preview.Graph;

            // Allow format override — same logic as ClientAuthEndpoints
            var requestedFormat = format?.ToUpperInvariant();
            var resolvedFormat  = preview.GraphFormat;
            var serialized      = preview.SerializedGraph;

            if (!string.IsNullOrWhiteSpace(requestedFormat) && requestedFormat != resolvedFormat)
            {
                var overrideFormat = await formatProvider.ResolveFormatAsync(
                    preview.TenantId, requestedFormat, ct);

                if (overrideFormat != resolvedFormat)
                {
                    var criteria   = new GraphSerializationCriteria(overrideFormat);
                    var serializer = factory
                        .Create<GraphSerializationCriteria, IAuthorizationGraphSerializer>(criteria)
                        .SingleOrDefault();

                    if (serializer is not null)
                    {
                        serialized     = serializer.Serialize(graph);
                        resolvedFormat = overrideFormat;
                    }
                }
            }

            context.Response.Headers["X-Graph-Format"] = resolvedFormat;
            context.Response.Headers["X-Preview-Mode"] = "internal-preview";
            context.Response.Headers["X-Request-Id"]   = preview.RequestId;

            return Results.Ok(new
            {
                format         = resolvedFormat,
                graph          = serialized,
                requestId      = preview.RequestId,
                previewMode    = "internal-preview",
                profileId      = preview.ProfileId,
                userId         = preview.UserId,
                tenantId       = preview.TenantId,
                tenantCode     = preview.TenantCode,
                authMethodUsed = preview.AuthMethodUsed,
            });
        })
        .WithName("PreviewProfileAuthGraph")
        .WithSummary("Preview the exact auth graph a client system receives from POST /client/authenticate — same pipeline, no credential validation")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound);

        // ── Legacy profile permission-graph (profile-exporter pipeline) ────────
        group.MapGet("/{profileId:guid}/permission-graph", async (
            Guid profileId,
            IMediator mediator,
            ITenantExportConfigurationProvider configProvider,
            HttpContext context,
            CancellationToken ct) =>
        {
            var profileResult = await mediator.Send(new GetProfileByIdQuery(profileId), ct);
            if (profileResult.IsFailure)
            {
                return Results.BadRequest(new { error = profileResult.Error, errorId = Guid.NewGuid() });
            }

            var profile = profileResult.Value;
            var tenantId = profile.TenantId;
            var config = await configProvider.GetConfigurationAsync(tenantId, ct);

            var criteria = new ProfileExportCriteria(config.DefaultFormat);
            var factory = context.RequestServices.GetRequiredService<IFactory>();
            var exporters = factory.Create<ProfileExportCriteria, IProfileExporter>(criteria);

            if (exporters.Length == 0)
            {
                return Results.BadRequest(new { error = $"No exporter found for format '{config.DefaultFormat}'", errorId = Guid.NewGuid() });
            }

            var exporter = exporters[0];
            var content = exporter.Export(profile, config);

            return Results.Text(content, exporter.ContentType, System.Text.Encoding.UTF8);
        })
        .WithName("GetProfilePermissionGraphPreview")
        .WithSummary("Preview the profile authorization graph using tenant-configured default format")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/{profileId:guid}/export", async (
            Guid profileId,
            [FromQuery] string format,
            IMediator mediator,
            ITenantExportConfigurationProvider configProvider,
            IFactory factory,
            HttpContext context,
            CancellationToken ct) =>
        {
            var profileResult = await mediator.Send(new GetProfileByIdQuery(profileId), ct);
            if (profileResult.IsFailure)
            {
                return Results.BadRequest(new { error = profileResult.Error, errorId = Guid.NewGuid() });
            }

            var profile = profileResult.Value;
            var tenantId = profile.TenantId;
            var config = await configProvider.GetConfigurationAsync(tenantId, ct);

            var upperFormat = format.ToUpperInvariant();
            if (!config.AllowedFormats.Contains(upperFormat))
            {
                return Results.BadRequest(new
                {
                    error = $"Format '{format}' is not allowed. Allowed formats: {string.Join(", ", config.AllowedFormats)}",
                    errorId = Guid.NewGuid(),
                    allowedFormats = config.AllowedFormats
                });
            }

            var criteria = new ProfileExportCriteria(upperFormat);
            var exporters = factory.Create<ProfileExportCriteria, IProfileExporter>(criteria);

            if (exporters.Length == 0)
            {
                return Results.BadRequest(new { error = $"Export format '{format}' is not supported.", errorId = Guid.NewGuid() });
            }

            var exporter = exporters[0];
            var content = exporter.Export(profile, config);

            var fileName = $"profile_{profileId}.{exporter.FileExtension}";
            return Results.File(System.Text.Encoding.UTF8.GetBytes(content), exporter.ContentType, fileName);
        })
        .WithName("ExportProfileGraph")
        .WithSummary("Export the materialized profile authorization graph in JSON, XML, YAML, or CSV formats using our corporate factory.")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
