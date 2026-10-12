using Microsoft.AspNetCore.Http;
using Evolith.Ums.Sdk.Contracts;

namespace Evolith.Ums.Sdk.Authorization;

/// <summary>
/// Default <see cref="IAuthGraphAccessor"/> for ASP.NET Core applications.
/// Reads the graph from <c>HttpContext.Items[ItemsKey]</c>, populated upstream by middleware
/// such as <c>UseUmsAuthGraph()</c>.
/// </summary>
public sealed class HttpContextAuthGraphAccessor : IAuthGraphAccessor
{
    /// <summary>HttpContext.Items key under which the graph is stored.</summary>
    public const string ItemsKey = "UmsAuthGraph";

    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpContextAuthGraphAccessor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public AuthorizationGraph? Current
    {
        get
        {
            var ctx = _httpContextAccessor.HttpContext;
            if (ctx is null) return null;
            if (!ctx.Items.TryGetValue(ItemsKey, out var raw)) return null;
            return raw as AuthorizationGraph;
        }
    }
}
