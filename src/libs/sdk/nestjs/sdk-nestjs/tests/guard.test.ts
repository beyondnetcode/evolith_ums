import 'reflect-metadata';
import { createHmac } from 'node:crypto';
import { describe, it, expect, beforeAll, afterAll } from 'vitest';
import { Test, type TestingModule } from '@nestjs/testing';
import {
  Controller,
  Get,
  type INestApplication,
  type MiddlewareConsumer,
  Module,
  type NestModule,
  Param,
  Post,
  UseGuards
} from '@nestjs/common';
import { APP_FILTER, APP_GUARD } from '@nestjs/core';
import request from 'supertest';
import type { AuthorizationGraph } from '@evolith-ums/ums-sdk-contracts';
import { MemoryAuthGraphAccessor } from '@evolith-ums/ums-sdk-authorization';
import { AuthGraphBuilder } from '@evolith-ums/ums-sdk-testing';
import {
  AuthGraphMiddleware,
  AuthorizationDeniedFilter,
  RequiresFeatureFlag,
  RequiresScope,
  UmsAuthGuard,
  UmsSdkModule
} from '../src/index.js';

@Controller('orders')
class OrdersController {
  @Post(':id/approve')
  @RequiresScope('PURCHASE_ORDER.APPROVE')
  approve(@Param('id') id: string): { ok: true; id: string } {
    return { ok: true, id };
  }

  @Post(':id/pick')
  @RequiresScope('PURCHASE_ORDER.APPROVE')
  @RequiresFeatureFlag('WMS_NEW_PICKING_UI')
  pick(@Param('id') id: string): { ok: true; id: string } {
    return { ok: true, id };
  }

  @Get(':id/view')
  view(@Param('id') id: string): { id: string } {
    // No decorator — must always pass through.
    return { id };
  }
}

const SECRETO_DE_PRUEBA = 'secreto-de-prueba-de-al-menos-32-bytes-para-hs256';

const b64url = (b: Buffer): string => b.toString('base64url');

/**
 * Emite un portador firmado como el de UMS.
 *
 * Antes estas pruebas escribían el grafo directamente en `req.umsAuthGraph` y pasaban: el
 * middleware se creía lo que hubiera ahí. Ahora el grafo entra por donde entra en producción —un
 * portador firmado— y la prueba ejercita la verificación en vez de saltársela (G-217).
 */
function firmar(graph: AuthorizationGraph | null, secreto = SECRETO_DE_PRUEBA, alg = 'HS256'): string {
  const cabecera = b64url(Buffer.from(JSON.stringify({ alg, typ: 'JWT' })));
  const cuerpo = b64url(Buffer.from(JSON.stringify({ graph })));
  if (alg === 'none') return `${cabecera}.${cuerpo}.`;
  const firma = createHmac('sha256', secreto).update(`${cabecera}.${cuerpo}`).digest('base64url');
  return `${cabecera}.${cuerpo}.${firma}`;
}

/** Middleware de prueba: traduce la cabecera `x-graph` en un portador firmado de verdad. */
function buildTestModule(accessor: MemoryAuthGraphAccessor, graphs: Map<string, AuthorizationGraph | null>): Promise<TestingModule> {
  @Module({
    imports: [
      UmsSdkModule.forRoot({
        mode: 'enforce',
        accessor,
        signingSecret: SECRETO_DE_PRUEBA
      })
    ],
    controllers: [OrdersController],
    providers: [
      { provide: APP_GUARD, useClass: UmsAuthGuard },
      { provide: APP_FILTER, useClass: AuthorizationDeniedFilter }
    ]
  })
  class TestAppModule implements NestModule {
    configure(consumer: MiddlewareConsumer): void {
      consumer
        .apply((req: { headers: Record<string, string | string[] | undefined> }, _res: unknown, next: (err?: unknown) => void) => {
          const key = (req.headers['x-graph'] as string | undefined) ?? '';
          const graph = graphs.get(key);
          if (graph) req.headers['authorization'] = `Bearer ${firmar(graph)}`;
          next();
        }, AuthGraphMiddleware)
        .forRoutes('*');
    }
  }
  return Test.createTestingModule({ imports: [TestAppModule] }).compile();
}

describe('UmsAuthGuard (e2e with NestJS Testing + supertest)', () => {
  let app: INestApplication;
  let server: ReturnType<INestApplication['getHttpServer']>;

  const accessor = new MemoryAuthGraphAccessor();
  const graphs = new Map<string, AuthorizationGraph | null>();

  beforeAll(async () => {
    const grantApprove = AuthGraphBuilder.forTenant('LOGISTICS_CORE')
      .withUser('ana.flores@example.com')
      .withScope('PURCHASE_ORDER.APPROVE')
      .build();
    const grantApproveAndFlag = AuthGraphBuilder.forTenant('LOGISTICS_CORE')
      .withScope('PURCHASE_ORDER.APPROVE')
      .withFeatureFlag('WMS_NEW_PICKING_UI', true, 'BranchId')
      .build();
    const grantApproveOnly = AuthGraphBuilder.forTenant('LOGISTICS_CORE')
      .withScope('PURCHASE_ORDER.APPROVE')
      .build(); // no flag
    const noScope = AuthGraphBuilder.forTenant('LOGISTICS_CORE').build();

    graphs.set('grant-approve', grantApprove);
    graphs.set('grant-approve-and-flag', grantApproveAndFlag);
    graphs.set('grant-approve-only', grantApproveOnly);
    graphs.set('no-scope', noScope);

    const module = await buildTestModule(accessor, graphs);
    app = module.createNestApplication();
    await app.init();
    server = app.getHttpServer();
  });

  afterAll(async () => {
    await app.close();
  });

  it('denies approve when scope is missing (403 + AUTH_101)', async () => {
    const res = await request(server).post('/orders/abc/approve').set('x-graph', 'no-scope');
    expect(res.status).toBe(403);
    expect(res.body.code).toBe('AUTH_101');
    expect(res.body.primitive).toBe('RequiresScope');
    expect(res.body.target).toBe('PURCHASE_ORDER.APPROVE');
  });

  it('allows approve when scope is present (201)', async () => {
    const res = await request(server).post('/orders/abc/approve').set('x-graph', 'grant-approve');
    expect(res.status).toBe(201);
    expect(res.body).toEqual({ ok: true, id: 'abc' });
  });

  it('denies pick when feature flag is missing (stacked decorators short-circuit on first failure)', async () => {
    const res = await request(server).post('/orders/xyz/pick').set('x-graph', 'grant-approve-only');
    expect(res.status).toBe(403);
    expect(res.body.code).toBe('AUTH_108');
    expect(res.body.primitive).toBe('RequiresFeatureFlag');
  });

  it('allows pick when both scope and flag are granted', async () => {
    const res = await request(server).post('/orders/xyz/pick').set('x-graph', 'grant-approve-and-flag');
    expect(res.status).toBe(201);
  });

  it('decorator-free handler passes through without any graph', async () => {
    const res = await request(server).get('/orders/abc/view');
    expect(res.status).toBe(200);
    expect(res.body).toEqual({ id: 'abc' });
  });

  it('un portador fabricado a mano no otorga nada (G-217)', async () => {
    // El ataque: nadie necesita el secreto para escribir un grafo con el permiso que quiera.
    const falsificado = firmar(
      AuthGraphBuilder.forTenant('LOGISTICS_CORE').withScope('PURCHASE_ORDER.APPROVE').build(),
      'no-es-el-secreto-de-ums'
    );
    const res = await request(server).post('/orders/abc/approve').set('authorization', `Bearer ${falsificado}`);
    expect(res.status).toBe(403);
    expect(res.body.code).toBe('AUTH_202');
  });

  it('un portador con `alg: none` no otorga nada (confusión de algoritmo)', async () => {
    const sinFirma = firmar(
      AuthGraphBuilder.forTenant('LOGISTICS_CORE').withScope('PURCHASE_ORDER.APPROVE').build(),
      SECRETO_DE_PRUEBA,
      'none'
    );
    const res = await request(server).post('/orders/abc/approve').set('authorization', `Bearer ${sinFirma}`);
    expect(res.status).toBe(403);
    expect(res.body.code).toBe('AUTH_202');
  });

  it('el módulo no arranca sin `signingSecret`', () => {
    expect(() => UmsSdkModule.forRoot({ mode: 'enforce' } as never)).toThrow(/signingSecret/);
  });
});

describe('UmsAuthGuard — global guard via UseGuards on a single controller', () => {
  @Controller('admin')
  @UseGuards(UmsAuthGuard)
  class AdminController {
    @Get('reset')
    @RequiresScope('ADMIN.RESET')
    reset(): { reset: true } {
      return { reset: true };
    }
  }

  let app: INestApplication;
  const accessor = new MemoryAuthGraphAccessor();
  const graphs = new Map<string, AuthorizationGraph | null>();

  beforeAll(async () => {
    graphs.set('admin', AuthGraphBuilder.forTenant('LOGISTICS_CORE').withScope('ADMIN.RESET').build());

    @Module({
      imports: [UmsSdkModule.forRoot({ mode: 'enforce', accessor, signingSecret: SECRETO_DE_PRUEBA })],
      controllers: [AdminController],
      providers: [{ provide: APP_FILTER, useClass: AuthorizationDeniedFilter }]
    })
    class TestAppModule implements NestModule {
      configure(consumer: MiddlewareConsumer): void {
        consumer
          .apply((req: { headers: Record<string, string | string[] | undefined> }, _res: unknown, next: (err?: unknown) => void) => {
            const key = (req.headers['x-graph'] as string | undefined) ?? '';
            const graph = graphs.get(key);
            if (graph) req.headers['authorization'] = `Bearer ${firmar(graph)}`;
            next();
          }, AuthGraphMiddleware)
          .forRoutes('*');
      }
    }

    const module = await Test.createTestingModule({ imports: [TestAppModule] }).compile();
    app = module.createNestApplication();
    await app.init();
  });

  afterAll(async () => {
    await app.close();
  });

  it('rejects when missing graph entirely (AUTH_202)', async () => {
    const res = await request(app.getHttpServer()).get('/admin/reset');
    expect(res.status).toBe(403);
    expect(res.body.code).toBe('AUTH_202');
  });

  it('accepts when scope present', async () => {
    const res = await request(app.getHttpServer()).get('/admin/reset').set('x-graph', 'admin');
    expect(res.status).toBe(200);
  });
});
