# @evolith-ums/ums-sdk-express

> Part of the [UMS SDK](https://github.com/beyondnetcode/ums/tree/main/docs/sdk).

Express middleware that decodes a bearer JWT's `graph` claim into an `AuthorizationGraph` and binds it to the configured `@evolith-ums/ums-sdk-authorization` accessor for the request lifetime.

## Install

```bash
npm install @evolith-ums/ums-sdk-express @evolith-ums/ums-sdk-authorization @evolith-ums/ums-sdk-contracts
```

## Use

```ts
import express from 'express';
import {
  AsyncLocalAuthGraphAccessor, AuthorizationValidator, configureAuthorization
} from '@evolith-ums/ums-sdk-authorization';
import { umsAuthGraph } from '@evolith-ums/ums-sdk-express';

const accessor = new AsyncLocalAuthGraphAccessor();
configureAuthorization({ accessor, validator: new AuthorizationValidator() });

const app = express();
// `signingSecret` es obligatorio: es el secreto con el que UMS firma sus portadores. Sin él, el
// middleware no puede distinguir un grafo emitido por UMS de uno escrito por quien llama (G-217).
app.use(umsAuthGraph({ accessor, rejectExpired: true, signingSecret: process.env.UMS_JWT_SECRET! }));
```

After the middleware runs, any handler that calls `requireScope(...)` or any of the
HOFs/decorators from `@evolith-ums/ums-sdk-authorization` can read the current graph through the
accessor without further wiring.

## License

MIT — see [LICENSE](https://github.com/beyondnetcode/ums/blob/main/src/libs/sdk/LICENSE).
