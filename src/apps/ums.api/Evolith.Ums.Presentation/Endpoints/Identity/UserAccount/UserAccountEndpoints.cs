namespace Evolith.Ums.Presentation.Endpoints.Identity.UserAccount;

#pragma warning disable S125

using Evolith.Ums.Application.Common;
using Evolith.Ums.Application.Identity.UserAccount.Commands;
using Evolith.Ums.Application.Identity.UserAccount.DTOs;
using Evolith.Ums.Application.Identity.UserAccount.Queries;
using Evolith.Ums.Presentation.Endpoints.Common;

public static class UserAccountEndpoints
{
    public static IEndpointRouteBuilder MapUserAccountEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/user-accounts")
            .WithTags("UserAccounts");

        group.MapGet("/", async (
            [AsParameters] TenantPagedQueryParameters query,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetAllUserAccountsQuery(
                query.Page <= 0 ? 1 : query.Page,
                query.PageSize <= 0 ? 20 : query.PageSize,
                query.Search,
                string.IsNullOrWhiteSpace(query.Criteria) ? "email" : query.Criteria,
                string.IsNullOrWhiteSpace(query.Status) ? "all" : query.Status,
                string.IsNullOrWhiteSpace(query.SortBy) ? "email" : query.SortBy,
                string.IsNullOrWhiteSpace(query.SortOrder) ? "asc" : query.SortOrder,
                query.TenantId), ct);
            return result.ToOk(context);
        })
        .WithName("GetAllUserAccounts")
        .WithSummary("Get user accounts using server-side pagination")
        // G-041 (SEGURIDAD): el listado expone PII; exige autenticación.
        .RequireAuthorization()
        .Produces<PagedResult<UserAccountDto>>(StatusCodes.Status200OK);



        group.MapPost("/", async (CreateUserAccountCommand command, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(command, ct);
            return result.ToCreated(r => $"/user-accounts/{r.UserAccountId}", context);
        })
        .WithName("CreateUserAccount")
        .WithSummary("Create a new user account")
        .Produces<CreateUserAccountResponse>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{userAccountId:guid}/activate", async (Guid userAccountId, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(new ActivateUserAccountCommand(userAccountId), ct);
            return result.ToNoContent(context);
        })
        .WithName("ActivateUserAccount")
        .WithSummary("Activate a pending user account")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        // Sub-recurso propio y no un PUT general de la cuenta, por el mismo criterio que /activate,
        // /block y /validity: un PUT general obligaría a reenviar todos los campos, y el cliente que
        // se dejara uno lo borraría sin pedirlo.
        group.MapPost("/{userAccountId:guid}/display-name", async (
            Guid userAccountId,
            SetUserAccountDisplayNameCommand command,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            // El id de la ruta manda sobre el del cuerpo: la ruta es la que identifica el recurso.
            var result = await mediator.Send(command with { UserAccountId = userAccountId }, ct);
            return result.ToNoContent(context);
        })
        .WithName("SetUserAccountDisplayName")
        .WithSummary("Set or clear the display name of a user account")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{userAccountId:guid}/block", async (Guid userAccountId, string reason, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(new BlockUserAccountCommand(userAccountId, reason), ct);
            return result.ToNoContent(context);
        })
        .WithName("BlockUserAccount")
        .WithSummary("Block an active user account")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{userAccountId:guid}/restore", async (Guid userAccountId, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(new RestoreUserAccountCommand(userAccountId), ct);
            return result.ToNoContent(context);
        })
        .WithName("RestoreUserAccount")
        .WithSummary("Restore a blocked user account")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{userAccountId:guid}/deny-signup", async (
            Guid userAccountId,
            DenyUserSignupRequest body,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var result = await mediator.Send(new DenyUserSignupCommand(userAccountId, body.Reason), ct);
            return result.ToNoContent(context);
        })
        .WithName("DenyUserSignup")
        .WithSummary("Deny a pending user signup request. Sends a denial notification to the applicant.")
        .RequireAuthorization()
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        // REC-16: Soft-delete + GDPR anonymization. Irreversible — raises UserDeletedEvent
        // which also revokes any active tokens via HARDENING-03 TokenRevocationMiddleware.
        group.MapDelete("/{userAccountId:guid}", async (
            Guid userAccountId, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(new DeleteUserAccountCommand(userAccountId), ct);
            return result.ToNoContent(context);
        })
        .WithName("DeleteUserAccount")
        .WithSummary("Soft-delete a user account and anonymize PII (GDPR). Action is irreversible.")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{userAccountId:guid}/passwords", async (
            Guid userAccountId,
            AddUserAccountPasswordCommand command,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var result = await mediator.Send(command with { UserAccountId = userAccountId }, ct);
            return result.ToCreated(r => $"/user-accounts/{userAccountId}/passwords/{r.CredentialId}", context);
        })
        .WithName("AddUserAccountPassword")
        .WithSummary("Set or rotate a local password credential; hashing is performed by the API")
        .Produces<AddUserAccountPasswordResponse>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{userAccountId:guid}/passwords/force-reset", async (
            Guid userAccountId,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var result = await mediator.Send(new ForcePasswordResetCommand(userAccountId), ct);
            return result.ToOk(context);
        })
        .WithName("ForcePasswordReset")
        .WithSummary("Force a password reset: generates and sets a temporary password that the admin shares with the user")
        .Produces<ForcePasswordResetResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        // Historic credential reactivation and physical deletion remain intentionally
        // unavailable: password rotation retains inactive entries for security audit.

        group.MapPost("/{userAccountId:guid}/mfa-enrollments", async (Guid userAccountId, EnrollUserAccountMfaCommand command, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(command with { UserAccountId = userAccountId }, ct);
            return result.ToCreated(r => $"/user-accounts/{userAccountId}/mfa-enrollments/{r.EnrollmentId}", context);
        })
        .WithName("EnrollUserAccountMfa")
        .WithSummary("Enroll an MFA method for the user account")
        .Produces<EnrollUserAccountMfaResponse>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{userAccountId:guid}/mfa-enrollments/{enrollmentId:guid}/verify", async (Guid userAccountId, Guid enrollmentId, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(new VerifyUserAccountMfaCommand(userAccountId, enrollmentId), ct);
            return result.ToNoContent(context);
        })
        .WithName("VerifyUserAccountMfa")
        .WithSummary("Verify a pending MFA enrollment")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapDelete("/{userAccountId:guid}/mfa-enrollments/{enrollmentId:guid}", async (Guid userAccountId, Guid enrollmentId, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(new RevokeUserAccountMfaCommand(userAccountId, enrollmentId), ct);
            return result.ToNoContent(context);
        })
        .WithName("RevokeUserAccountMfaEnrollment")
        .WithSummary("Revoke an MFA enrollment for the user account")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPatch("/{userAccountId:guid}/validity", async (
            Guid userAccountId,
            ModifyValidityPeriodRequest body,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var result = await mediator.Send(
                new ModifyUserValidityPeriodCommand(userAccountId, body.ExpiresAt, body.Reason), ct);
            return result.ToNoContent(context);
        })
        .WithName("ModifyUserValidityPeriod")
        .WithSummary("Set or extend the validity period for a user account. Validates against MAX_VALIDITY_PERIOD_DAYS.")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{userAccountId:guid}/authentication-attempts", async (
            Guid userAccountId,
            RecordAuthenticationAttemptCommand command,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var result = await mediator.Send(command with { UserAccountId = userAccountId }, ct);
            return result.ToNoContent(context);
        })
        .WithName("RecordAuthenticationAttempt")
        .WithSummary("Record an authentication attempt (success or failure) for audit and rate-limiting purposes")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}

public sealed record ModifyValidityPeriodRequest(DateTimeOffset ExpiresAt, string? Reason = null);

public sealed record DenyUserSignupRequest(string? Reason = null);
