using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace Evolith.Ums.ContractTest.Provider;

internal sealed class PactProviderStateStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
        => app =>
        {
            app.UseMiddleware<PactProviderStateMiddleware>();
            next(app);
        };
}
