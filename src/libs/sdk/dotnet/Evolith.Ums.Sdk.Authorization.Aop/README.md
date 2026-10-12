# Evolith.Ums.Sdk.Authorization.Aop

> Part of the [UMS SDK](https://github.com/beyondnetcode/ums/tree/main/docs/sdk) — the official client integration surface for UMS.

Declarative authorization attributes for .NET, built on top of `BeyondNetCode.Shell.Aop` (DispatchProxy aspects).

## Install

```bash
dotnet add package Evolith.Ums.Sdk.Authorization.Aop
```

## Quickstart

```csharp
using BeyondNetCode.Shell.DI;
using Evolith.Ums.Sdk.Authorization;
using Evolith.Ums.Sdk.Authorization.Aop;

builder.Services.AddUmsSdkAuthorizationWithAop();    // one-call setup
builder.Services.AddAopProxy<IOrderService, OrderService>();
```

```csharp
public interface IOrderService
{
    [RequiresScope("PURCHASE_ORDER.APPROVE")]
    Task<Result> ApproveAsync(Guid id);

    [RequiresMenuOption("STOCK_ADJUST")]
    Task<Result> AdjustStockAsync(StockAdjustment a);

    [RequiresDomainAccess("PURCHASE_ORDER", "VIEW")]
    Task<Order> GetAsync(Guid id);

    [RequiresFeatureFlag("WMS_NEW_PICKING_UI")]
    Task<PickList> BuildPickListAsync(Guid id);
}
```

Combine `Throw` (default) with `ReturnFailure` per attribute:

```csharp
[RequiresScope("X.Y", OnDenied = DenialBehavior.ReturnFailure)]
Task<Result> ApproveAsync(Guid id);
```

Roll out progressively:

```csharp
builder.Services.Configure<AuthorizationOptions>(o => o.Mode = AuthorizationMode.AuditOnly);
```

## See also

- [.NET SDK guide](https://github.com/beyondnetcode/ums/blob/main/docs/sdk/dotnet/README.md)
- [.NET Quickstart](https://github.com/beyondnetcode/ums/blob/main/docs/sdk/dotnet/quickstart.md)

## License

MIT — see [LICENSE](https://github.com/beyondnetcode/ums/blob/main/src/libs/sdk/LICENSE).
