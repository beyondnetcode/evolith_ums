using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Evolith.Ums.ContractTest.Infrastructure;

/// <summary>
/// Starts a lightweight Kestrel reverse-proxy on a fixed local port that forwards
/// every request to the in-process WebApplicationFactory TestServer.
///
/// This allows PactNet's native (out-of-process) Rust verifier to reach the API via
/// real TCP while still benefiting from the in-memory service replacements configured
/// in ContractTestWebApplicationFactory.
/// </summary>
public sealed class PactKestrelServer : IDisposable
{
    private readonly IHost _proxyHost;
    private bool _disposed;

    public static PactKestrelServer Start(
        WebApplicationFactory<Program> factory, int port)
    {
        return new PactKestrelServer(factory, port);
    }

    private PactKestrelServer(WebApplicationFactory<Program> factory, int port)
    {
        // Build an HttpClient backed by the TestServer's in-process handler.
        var innerClient = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress       = new Uri("http://localhost"),
        });

        _proxyHost = Host.CreateDefaultBuilder()
            .ConfigureLogging(log => log.ClearProviders()) // silent proxy
            .ConfigureWebHost(web =>
            {
                web.UseKestrel()
                   .UseUrls($"http://127.0.0.1:{port}");

                web.Configure(app =>
                {
                    app.Run(async ctx =>
                    {
                        // Forward the request verbatim to the TestServer.
                        var requestUri = new UriBuilder
                        {
                            Scheme   = "http",
                            Host     = "localhost",
                            Path     = ctx.Request.Path,
                            Query    = ctx.Request.QueryString.ToString(),
                        }.Uri;

                        using var proxyRequest = new HttpRequestMessage(
                            new HttpMethod(ctx.Request.Method), requestUri);

                        // Copy body.
                        if (ctx.Request.ContentLength > 0 || ctx.Request.Headers.ContainsKey("Transfer-Encoding"))
                        {
                            proxyRequest.Content = new StreamContent(ctx.Request.Body);
                        }

                        // Copy headers (skip Host).
                        foreach (var (key, values) in ctx.Request.Headers)
                        {
                            if (key.Equals("Host", StringComparison.OrdinalIgnoreCase)) continue;
                            if (!proxyRequest.Headers.TryAddWithoutValidation(key, (IEnumerable<string>)values))
                                proxyRequest.Content?.Headers.TryAddWithoutValidation(key, (IEnumerable<string>)values);
                        }

                        using var response = await innerClient.SendAsync(
                            proxyRequest, ctx.RequestAborted);

                        ctx.Response.StatusCode = (int)response.StatusCode;
                        foreach (var (key, values) in response.Headers)
                            ctx.Response.Headers[key] = new Microsoft.Extensions.Primitives.StringValues(values.ToArray());
                        foreach (var (key, values) in response.Content.Headers)
                            ctx.Response.Headers[key] = new Microsoft.Extensions.Primitives.StringValues(values.ToArray());

                        await response.Content.CopyToAsync(ctx.Response.Body, ctx.RequestAborted);
                    });
                });
            })
            .Build();

        _proxyHost.Start();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _proxyHost.StopAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
        _proxyHost.Dispose();
    }
}
