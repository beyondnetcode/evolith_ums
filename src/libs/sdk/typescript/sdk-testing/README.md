# @evolith-ums/ums-sdk-testing

> Part of the [UMS SDK](https://github.com/beyondnetcode/ums/tree/main/docs/sdk) — the official client integration surface for UMS.

Fluent `AuthGraphBuilder` and `TestAuthGraphAccessor` for unit-testing consumer code without UMS.

## Install

```bash
npm install --save-dev @evolith-ums/ums-sdk-testing
```

## Use

```ts
import { AuthGraphBuilder, TestAuthGraphAccessor } from '@evolith-ums/ums-sdk-testing';
import { AuthorizationValidator } from '@evolith-ums/ums-sdk-authorization';

const graph = AuthGraphBuilder
  .forTenant('LOGISTICS_CORE')
  .withUser('ana.flores@example.com')
  .withScope('PURCHASE_ORDER.VIEW')
  .withDeny('STOCK_DELETE.DELETE')
  .withFeatureFlag('WMS_NEW_PICKING_UI', true, 'BranchId')
  .build();

const accessor = new TestAuthGraphAccessor(graph);
const validator = new AuthorizationValidator();
```

Expired graph helper:

```ts
const expired = AuthGraphBuilder.forTenant('LOGISTICS_CORE').buildExpired();
```

## License

MIT — see [LICENSE](https://github.com/beyondnetcode/ums/blob/main/src/libs/sdk/LICENSE).
