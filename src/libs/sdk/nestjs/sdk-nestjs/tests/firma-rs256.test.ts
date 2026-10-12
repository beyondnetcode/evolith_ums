import 'reflect-metadata';
import { constants, createHmac, generateKeyPairSync, sign } from 'node:crypto';
import { describe, it, expect, afterEach } from 'vitest';
import { Test } from '@nestjs/testing';
import {
  Controller,
  Get,
  type INestApplication,
  type MiddlewareConsumer,
  Module,
  type NestModule
} from '@nestjs/common';
import { APP_FILTER, APP_GUARD } from '@nestjs/core';
import request from 'supertest';
import type { AuthorizationGraph } from '@evolith-ums/ums-sdk-contracts';
import { MemoryAuthGraphAccessor } from '@evolith-ums/ums-sdk-authorization';
import { AuthGraphBuilder } from '@evolith-ums/ums-sdk-testing';
import {
  AuthGraphMiddleware,
  AuthorizationDeniedFilter,
  RequiresScope,
  UmsAuthGuard,
  UmsSdkModule
} from '../src/index.js';

/**
 * ADR-0157: UMS firma en RS256 y publica su clave pública en el JWKS. El middleware de este SDK
 * exigía HS256, así que rechazaba todos los portadores buenos.
 *
 * Se comprueba de extremo a extremo —petición HTTP real contra un módulo Nest montado— porque el
 * fallo que se corrige no estaba en la función de verificar sino en cómo la usaba el middleware.
 */

@Controller('pedidos')
class PedidosController {
  @Get('aprobar')
  @RequiresScope('PURCHASE_ORDER.APPROVE')
  aprobar(): { ok: true } {
    return { ok: true };
  }
}

const SECRETO = 'secreto-de-prueba-de-al-menos-32-bytes-para-hs256';
const KID = 'kid-de-prueba';
const b64 = (o: unknown) => Buffer.from(JSON.stringify(o)).toString('base64url');

const { publicKey, privateKey } = generateKeyPairSync('rsa', { modulusLength: 2048 });
const jwkPublica = publicKey.export({ format: 'jwk' }) as Record<string, string>;

/** JWKS servido en memoria: la prueba no debe depender de que haya un UMS levantado. */
const jwksEnMemoria = (async () => ({
  ok: true,
  text: async () => JSON.stringify({
    keys: [{ kty: 'RSA', use: 'sig', alg: 'RS256', kid: KID, n: jwkPublica['n'], e: jwkPublica['e'] }],
  }),
} as unknown as Response)) as unknown as typeof fetch;

function tokenRs256(graph: AuthorizationGraph, clave = privateKey, kid = KID): string {
  const cabecera = b64({ alg: 'RS256', typ: 'JWT', kid });
  const cuerpo = b64({ graph });
  const firma = sign('sha256', Buffer.from(`${cabecera}.${cuerpo}`, 'ascii'),
    { key: clave, padding: constants.RSA_PKCS1_PADDING }).toString('base64url');
  return `${cabecera}.${cuerpo}.${firma}`;
}

function tokenHs256(graph: AuthorizationGraph, secreto = SECRETO): string {
  const cabecera = b64({ alg: 'HS256', typ: 'JWT' });
  const cuerpo = b64({ graph });
  const firma = createHmac('sha256', secreto).update(`${cabecera}.${cuerpo}`).digest('base64url');
  return `${cabecera}.${cuerpo}.${firma}`;
}

const GRAFO = AuthGraphBuilder.forTenant('LOGISTICS_CORE').withScope('PURCHASE_ORDER.APPROVE').build();

async function montar(opciones: Record<string, unknown>): Promise<INestApplication> {
  @Module({
    imports: [
      UmsSdkModule.forRoot({
        mode: 'enforce',
        accessor: new MemoryAuthGraphAccessor(),
        ...opciones
      } as never)
    ],
    controllers: [PedidosController],
    providers: [
      { provide: APP_GUARD, useClass: UmsAuthGuard },
      { provide: APP_FILTER, useClass: AuthorizationDeniedFilter }
    ]
  })
  class AppDePrueba implements NestModule {
    configure(consumer: MiddlewareConsumer): void {
      consumer.apply(AuthGraphMiddleware).forRoutes('*');
    }
  }

  const modulo = await Test.createTestingModule({ imports: [AppDePrueba] }).compile();
  const app = modulo.createNestApplication();
  await app.init();
  return app;
}

let app: INestApplication | null = null;
afterEach(async () => {
  await app?.close();
  app = null;
});

/** Satélite tal como debe quedar tras ADR-0157: verifica sin custodiar material de firma. */
const SOLO_RS256 = { umsBaseUrl: 'https://ums', jwks: { fetchImpl: jwksEnMemoria } };

describe('ADR-0157 · el middleware de NestJS verifica los portadores RS256', () => {
  it('acepta el portador que UMS firma con su clave privada', async () => {
    app = await montar(SOLO_RS256);
    await request(app.getHttpServer())
      .get('/pedidos/aprobar')
      .set('authorization', `Bearer ${tokenRs256(GRAFO)}`)
      .expect(200);
  });

  it('RECHAZA uno firmado con otra clave, aunque declare el `kid` bueno', async () => {
    const ajena = generateKeyPairSync('rsa', { modulusLength: 2048 }).privateKey;
    app = await montar(SOLO_RS256);
    await request(app.getHttpServer())
      .get('/pedidos/aprobar')
      .set('authorization', `Bearer ${tokenRs256(GRAFO, ajena)}`)
      .expect(403);
  });

  it('RECHAZA un `kid` que no está en el JWKS', async () => {
    app = await montar(SOLO_RS256);
    await request(app.getHttpServer())
      .get('/pedidos/aprobar')
      .set('authorization', `Bearer ${tokenRs256(GRAFO, privateKey, 'kid-inventado')}`)
      .expect(403);
  });

  it('RECHAZA `alg: none`', async () => {
    app = await montar(SOLO_RS256);
    const token = `${b64({ alg: 'none', typ: 'JWT', kid: KID })}.${b64({ graph: GRAFO })}.`;
    await request(app.getHttpServer())
      .get('/pedidos/aprobar')
      .set('authorization', `Bearer ${token}`)
      .expect(403);
  });

  it('configurado para RS256, un HS256 NO se cuela', async () => {
    // La degradación que no puede ocurrir: el satélite espera RS256 y le presentan un HS256.
    app = await montar(SOLO_RS256);
    await request(app.getHttpServer())
      .get('/pedidos/aprobar')
      .set('authorization', `Bearer ${tokenHs256(GRAFO)}`)
      .expect(403);
  });

  it('HS256 sigue funcionando donde se configuró así', async () => {
    app = await montar({ signingSecret: SECRETO });
    await request(app.getHttpServer())
      .get('/pedidos/aprobar')
      .set('authorization', `Bearer ${tokenHs256(GRAFO)}`)
      .expect(200);
  });

  it('durante la transición admite las dos vías, y ninguna se relaja', async () => {
    app = await montar({ signingSecret: SECRETO, ...SOLO_RS256 });
    const servidor = app.getHttpServer();

    await request(servidor).get('/pedidos/aprobar')
      .set('authorization', `Bearer ${tokenRs256(GRAFO)}`).expect(200);
    await request(servidor).get('/pedidos/aprobar')
      .set('authorization', `Bearer ${tokenHs256(GRAFO)}`).expect(200);
    await request(servidor).get('/pedidos/aprobar')
      .set('authorization', `Bearer ${tokenHs256(GRAFO, 'otro-secreto-igual-de-largo-pero-ajeno')}`).expect(403);
  });

  it('el módulo falla al construirse si no se le da con qué verificar', () => {
    expect(() => UmsSdkModule.forRoot({} as never)).toThrow(/umsBaseUrl|signingSecret/);
  });
});
