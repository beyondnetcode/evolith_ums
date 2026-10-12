# @evolith-ums/ums-sdk-authorization

> Part of the [UMS SDK](https://github.com/beyondnetcode/ums/tree/main/docs/sdk) — the official client integration surface for UMS.

Pure authorization validator + accessor abstraction + HOF/decorator authorization primitives for TypeScript and JavaScript. Framework-agnostic.

## Install

```bash
npm install @evolith-ums/ums-sdk-authorization @evolith-ums/ums-sdk-contracts
```

## Quickstart (Node + Express)

```ts
import express from 'express';
import {
  AsyncLocalAuthGraphAccessor,
  AuthorizationValidator,
  configureAuthorization,
  requireScope
} from '@evolith-ums/ums-sdk-authorization';
import { umsAuthGraph } from '@evolith-ums/ums-sdk-express';

const accessor = new AsyncLocalAuthGraphAccessor();
configureAuthorization({ accessor, validator: new AuthorizationValidator() });

const app = express();

// El grafo se toma del portador FIRMADO por UMS, nunca de un campo que ponga otro middleware:
// `req.umsAuthGraph` lo puede escribir cualquiera de la cadena, y eso era el agujero de G-217.
app.use(umsAuthGraph({ accessor, signingSecret: process.env.UMS_JWT_SECRET! }));

app.post(
  '/orders/:id/approve',
  requireScope('PURCHASE_ORDER.APPROVE', async (_req, res) => {
    res.json({ ok: true });
  })
);
```

## Decorators (TS 5.0+)

```ts
import { RequiresScope } from '@evolith-ums/ums-sdk-authorization';

class OrderService {
  @RequiresScope('PURCHASE_ORDER.APPROVE')
  async approve(id: string): Promise<void> { /* ... */ }
}
```

## Browser

```ts
import { MemoryAuthGraphAccessor } from '@evolith-ums/ums-sdk-authorization';
const accessor = new MemoryAuthGraphAccessor();
accessor.set(graph);   // after login
accessor.clear();      // on logout
```

## See also

- [TypeScript SDK guide](https://github.com/beyondnetcode/ums/blob/main/docs/sdk/typescript/README.md)
- [TypeScript Quickstart](https://github.com/beyondnetcode/ums/blob/main/docs/sdk/typescript/quickstart.md)

## License

MIT — see [LICENSE](https://github.com/beyondnetcode/ums/blob/main/src/libs/sdk/LICENSE).
