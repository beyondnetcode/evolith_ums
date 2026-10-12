namespace Evolith.Ums.Presentation.Middleware;

using System.Security.Claims;
using Evolith.Ums.Application.Common.Interfaces;

public sealed class TenantContextMiddleware
{
    private readonly RequestDelegate _next;

    public TenantContextMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ITenantContext tenantContext)
    {
        var tenantIdClaim = context.User.FindFirstValue("tenant_id")
                         ?? context.User.FindFirstValue("org_id");

        var isInternalAdminClaim = context.User.FindFirstValue("is_internal_admin")?.ToLower() == "true";

        if (Guid.TryParse(tenantIdClaim, out var tenantId))
        {
            try
            {
                tenantContext.Initialize(tenantId, isInternalAdminClaim);
            }
            catch (InvalidOperationException)
            {
                // Already initialized - this is fine
            }
        }

        await _next(context);
    }
}

public static class TenantContextMiddlewareExtensions
{
    public static IApplicationBuilder UseTenantContext(this IApplicationBuilder app)
        => app.UseMiddleware<TenantContextMiddleware>();
}