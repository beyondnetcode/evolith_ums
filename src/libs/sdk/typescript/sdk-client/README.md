# @evolith-ums/ums-sdk-client

> Part of the [UMS SDK](https://github.com/beyondnetcode/ums/tree/main/docs/sdk).

Typed fetch-based HTTP client for `POST /api/v1/client/authenticate`. Validates the server's `schemaVersion` and returns a typed `Result`.

## Install

```bash
npm install @evolith-ums/ums-sdk-client @evolith-ums/ums-sdk-contracts @evolith-ums/ums-sdk-authorization
```

## Use

```ts
import { UmsAuthClient } from '@evolith-ums/ums-sdk-client';

const client = new UmsAuthClient({ baseUrl: 'https://ums.example.com' });

const result = await client.authenticate({
  tenantCode: 'LOGISTICS_CORE',
  username: 'ana.flores',
  password: '...'
});

if (!result.ok) console.error(result.error.code, result.error.message);
else accessor.set(result.value.graph);
```

## License

MIT — see [LICENSE](https://github.com/beyondnetcode/ums/blob/main/src/libs/sdk/LICENSE).
