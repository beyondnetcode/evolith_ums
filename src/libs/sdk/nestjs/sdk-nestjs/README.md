# @evolith-ums/ums-sdk-nestjs

> Part of the [UMS SDK](https://github.com/beyondnetcode/ums/tree/main/docs/sdk) — the official client integration surface for UMS.

NestJS distribution: `UmsSdkModule`, `UmsAuthGuard`, decorators (`@RequiresScope`, `@RequiresMenuOption`, `@RequiresDomainAccess`, `@RequiresFeatureFlag`), `AuthorizationDeniedFilter` and `AuthGraphMiddleware` — all on top of [`@evolith-ums/ums-sdk-authorization`](https://www.npmjs.com/package/@evolith-ums/ums-sdk-authorization).

## Install

```bash
npm install @evolith-ums/ums-sdk-nestjs @evolith-ums/ums-sdk-authorization @evolith-ums/ums-sdk-contracts
```

## Quickstart

```ts
import { Module, MiddlewareConsumer, NestModule } from '@nestjs/common';
import { APP_GUARD, APP_FILTER } from '@nestjs/core';
import { UmsSdkModule, UmsAuthGuard, AuthGraphMiddleware,
         AuthorizationDeniedFilter } from '@evolith-ums/ums-sdk-nestjs';

@Module({
  imports: [UmsSdkModule.forRoot({ mode: 'enforce', signingSecret: process.env.UMS_JWT_SECRET! })],
  providers: [
    { provide: APP_GUARD,  useClass: UmsAuthGuard },
    { provide: APP_FILTER, useClass: AuthorizationDeniedFilter }
  ]
})
export class AppModule implements NestModule {
  configure(consumer: MiddlewareConsumer): void {
    consumer.apply(AuthGraphMiddleware).forRoutes('*');
  }
}
```

```ts
import { Controller, Post, Param } from '@nestjs/common';
import { RequiresScope } from '@evolith-ums/ums-sdk-nestjs';

@Controller('orders')
export class OrdersController {
  @Post(':id/approve')
  @RequiresScope('PURCHASE_ORDER.APPROVE')
  approve(@Param('id') id: string) { return { ok: true, id }; }
}
```

Missing scope → HTTP 403 with structured body:

```json
{
  "statusCode": 403,
  "error": "Forbidden",
  "code": "AUTH_101",
  "message": "Scope 'PURCHASE_ORDER.APPROVE' is not present in the authorization graph.",
  "primitive": "RequiresScope",
  "target": "PURCHASE_ORDER.APPROVE"
}
```

## See also

- [NestJS SDK guide](https://github.com/beyondnetcode/ums/blob/main/docs/sdk/nestjs/README.md)
- [NestJS Quickstart](https://github.com/beyondnetcode/ums/blob/main/docs/sdk/nestjs/quickstart.md)

## License

MIT — see [LICENSE](https://github.com/beyondnetcode/ums/blob/main/src/libs/sdk/LICENSE).
