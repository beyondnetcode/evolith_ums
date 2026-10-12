# Evolith.Ums.Sdk.Authorization.Testing

> Part of the [UMS SDK](https://github.com/beyondnetcode/ums/tree/main/docs/sdk) — the official client integration surface for UMS.

Fluent `AuthGraphBuilder` for fabricating valid `AuthorizationGraph` instances in unit tests — no JSON, no HTTP, no UMS.

## Install

```bash
dotnet add package Evolith.Ums.Sdk.Authorization.Testing
```

## Use

```csharp
using Evolith.Ums.Sdk.Authorization.Testing;

var graph = AuthGraphBuilder
    .ForTenant("LOGISTICS_CORE")
    .WithUser("ana.flores@example.com")
    .WithScope("PURCHASE_ORDER.VIEW")
    .WithScope("PURCHASE_ORDER.APPROVE")
    .WithDeny("STOCK_DELETE.DELETE")
    .WithFeatureFlag("WMS_NEW_PICKING_UI", enabled: true)
    .Build();

var accessor = new TestAuthGraphAccessor(graph);
```

For expired-graph scenarios:

```csharp
var expired = AuthGraphBuilder.ForTenant("LOGISTICS_CORE").BuildExpired();
```

## See also

- [.NET SDK guide](https://github.com/beyondnetcode/ums/blob/main/docs/sdk/dotnet/README.md)
- [.NET Quickstart](https://github.com/beyondnetcode/ums/blob/main/docs/sdk/dotnet/quickstart.md)

## License

MIT — see [LICENSE](https://github.com/beyondnetcode/ums/blob/main/src/libs/sdk/LICENSE).
