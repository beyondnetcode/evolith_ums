# @evolith-ums/ums-sdk-contracts

> Part of the [UMS SDK](https://github.com/beyondnetcode/ums/tree/main/docs/sdk) — the official client integration surface for UMS.

TypeScript types for the `AuthorizationGraph` payload, `UmsErrorCodes` constants and `SchemaVersion` helpers.

Ships dual `.js` / `.d.ts` — usable from plain JavaScript with full functionality, only without compile-time typing.

## Install

```bash
npm install @evolith-ums/ums-sdk-contracts
```

## Use

```ts
import type { AuthorizationGraph } from '@evolith-ums/ums-sdk-contracts';
import { UmsErrorCodes, SchemaVersion, isSchemaVersionSupported } from '@evolith-ums/ums-sdk-contracts';

const graph: AuthorizationGraph = JSON.parse(payload);
console.log(graph.schemaVersion);                          // "1.0.0"
console.log(isSchemaVersionSupported(graph.schemaVersion)); // true
if (err.code === UmsErrorCodes.ScopeNotGranted) { ... }
```

## See also

- [TypeScript SDK guide](https://github.com/beyondnetcode/ums/blob/main/docs/sdk/typescript/README.md)
- [Schema overview](https://github.com/beyondnetcode/ums/blob/main/docs/sdk/contracts/schema-overview.md)

## License

MIT — see [LICENSE](https://github.com/beyondnetcode/ums/blob/main/src/libs/sdk/LICENSE).
