# Evolith.Ums.Sdk.Authorization

> Part of the [UMS SDK](https://github.com/beyondnetcode/ums/tree/main/docs/sdk) — the official client integration surface for UMS.

Pure authorization validator + accessor port + ASP.NET Core integration. Implements the deny-wins, override-takes-precedence, expiry and schema-compatibility rules documented in [ADR-UMS-088](https://github.com/beyondnetcode/ums/blob/main/docs/architecture/adrs/0071-auth-graph-engine.md).

No framework lock-in: usable from ASP.NET Core, workers, CLI, Blazor.

## Install

```bash
dotnet add package Evolith.Ums.Sdk.Authorization
```

## Quickstart

```csharp
using Evolith.Ums.Sdk.Authorization;

builder.Services.AddUmsSdkAuthorization();
builder.Services.AddHttpContextAccessor();        // required for the default accessor
builder.Services.AddHttpContextAuthGraphAccessor();
```

```csharp
public sealed class OrderService
{
    private readonly IAuthorizationValidator _validator;
    private readonly IAuthGraphAccessor _accessor;

    public OrderService(IAuthorizationValidator v, IAuthGraphAccessor a)
    { _validator = v; _accessor = a; }

    public Result Approve(Guid id)
    {
        var d = _validator.RequireScope(_accessor.Current, "PURCHASE_ORDER.APPROVE");
        if (d.IsDenied) return Result.Failure(d.ErrorCode!, d.Reason!);
        // ...
        return Result.Success();
    }
}
```

For declarative authorization with `[RequiresScope]` attributes, see [`Evolith.Ums.Sdk.Authorization.Aop`](https://www.nuget.org/packages/Evolith.Ums.Sdk.Authorization.Aop).

## See also

- [.NET SDK guide](https://github.com/beyondnetcode/ums/blob/main/docs/sdk/dotnet/README.md)
- [.NET Quickstart](https://github.com/beyondnetcode/ums/blob/main/docs/sdk/dotnet/quickstart.md)
- [Authorization Graph](https://github.com/beyondnetcode/ums/blob/main/docs/domain/identity/auth-graph.md)

## License

MIT — see [LICENSE](https://github.com/beyondnetcode/ums/blob/main/src/libs/sdk/LICENSE).
