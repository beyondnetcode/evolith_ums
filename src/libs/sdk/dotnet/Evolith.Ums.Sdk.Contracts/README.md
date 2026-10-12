# Evolith.Ums.Sdk.Contracts

> Part of the [UMS SDK](https://github.com/beyondnetcode/ums/tree/main/docs/sdk) — the official client integration surface for UMS.

Typed C# DTOs for the `AuthorizationGraph` payload, error code constants and schema version metadata.

This package has zero runtime dependencies. It is consumed by `Evolith.Ums.Sdk.Authorization`, `Evolith.Ums.Sdk.Authorization.Aop` and `Evolith.Ums.Sdk.Authorization.Testing`, and can be referenced standalone by any application that needs to deserialize or produce graph payloads.

## Install

```bash
dotnet add package Evolith.Ums.Sdk.Contracts
```

## Basics

```csharp
using System.Text.Json;
using Evolith.Ums.Sdk.Contracts;

var graph = JsonSerializer.Deserialize<AuthorizationGraph>(json);
Console.WriteLine(graph!.SchemaVersion);                        // "1.0.0"
Console.WriteLine(graph.Context.Tenant.Code);                   // "LOGISTICS_CORE"
Console.WriteLine(SchemaVersion.IsSupported(graph.SchemaVersion)); // true

// Error code constants (instead of magic strings)
if (errorCode == UmsErrorCodes.ScopeNotGranted) { ... }
```

## See also

- [.NET SDK guide](https://github.com/beyondnetcode/ums/blob/main/docs/sdk/dotnet/README.md)
- [Schema overview](https://github.com/beyondnetcode/ums/blob/main/docs/sdk/contracts/schema-overview.md)
- [Error codes catalog](https://github.com/beyondnetcode/ums/blob/main/docs/sdk/contracts/error-codes.md)
- [Versioning policy](https://github.com/beyondnetcode/ums/blob/main/docs/sdk/contracts/versioning.md)

## License

MIT — see [LICENSE](https://github.com/beyondnetcode/ums/blob/main/src/libs/sdk/LICENSE).
