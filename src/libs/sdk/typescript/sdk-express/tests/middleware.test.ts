import { describe, it, expect } from 'vitest';
import { createHmac } from 'node:crypto';
import { AsyncLocalAuthGraphAccessor, MemoryAuthGraphAccessor } from '@evolith-ums/ums-sdk-authorization';
import { AuthGraphBuilder } from '@evolith-ums/ums-sdk-testing';
import type { AuthorizationGraph } from '@evolith-ums/ums-sdk-contracts';
import { umsAuthGraph } from '../src/index.js';

/**
 * Secreto de la corrida. Desde G-217 el middleware verifica la firma, así que estas pruebas ya no
 * pueden fabricar un token `alg: none` y esperar que se acepte — eso es exactamente lo que ahora
 * se rechaza, y lo cubre `firma.test.ts`.
 */
const SECRETO_DE_PRUEBA = 'secreto-de-prueba-de-al-menos-32-caracteres-1234'; // gitleaks:allow -- deterministic signing key used only by tests

/** Un portador FIRMADO de verdad, que es lo que emite UMS. */
function fakeJwt(graph: AuthorizationGraph): string {
  const header = base64Url(Buffer.from('{"alg":"HS256","typ":"JWT"}'));
  const payload = base64Url(Buffer.from(JSON.stringify({ graph })));
  const firma = createHmac('sha256', SECRETO_DE_PRUEBA)
    .update(`${header}.${payload}`)
    .digest('base64url');
  return `${header}.${payload}.${firma}`;
}

function base64Url(buffer: Buffer): string {
  return buffer.toString('base64').replace(/=+$/, '').replace(/\+/g, '-').replace(/\//g, '_');
}

function makeRes() {
  const body: { status?: number; payload?: unknown } = {};
  return {
    status(code: number) {
      body.status = code;
      return {
        json(p: unknown) {
          body.payload = p;
        }
      };
    },
    body
  };
}

/**
 * Espera a que el middleware termine, por donde termine.
 *
 * Desde que la verificación admite RS256 el middleware es asíncrono —consultar el JWKS lo obliga—,
 * así que leer `res.body` justo después de invocarlo lee el estado de antes de que pasara nada.
 * Este arnés espera a lo que ocurra primero: la llamada a `next()` o el rechazo escrito en la
 * respuesta.
 */
function ejecutar(
  mw: ReturnType<typeof umsAuthGraph>,
  headers: Record<string, string | string[] | undefined>
): Promise<{ res: ReturnType<typeof makeRes>; siguiente: boolean }> {
  return new Promise((resolve) => {
    const res = makeRes();
    const original = res.status.bind(res);
    res.status = (code: number) => {
      const devuelto = original(code);
      return {
        json(p: unknown) {
          devuelto.json(p);
          resolve({ res, siguiente: false });
        }
      };
    };
    mw({ headers }, res as never, () => resolve({ res, siguiente: true }));
  });
}

describe('@evolith-ums/ums-sdk-express middleware', () => {
  it('binds graph to MemoryAuthGraphAccessor and calls next', async () => {
    const graph = AuthGraphBuilder.forTenant('LOGISTICS_CORE').withScope('PURCHASE_ORDER.VIEW').build();
    const accessor = new MemoryAuthGraphAccessor();
    const mw = umsAuthGraph({ accessor, signingSecret: SECRETO_DE_PRUEBA });

    await new Promise<void>((resolve) => {
      mw({ headers: { authorization: `Bearer ${fakeJwt(graph)}` } }, makeRes() as never, () => resolve());
    });

    const bound = accessor.current();
    expect(bound).not.toBeNull();
    expect(bound!.context.tenant.code).toBe('LOGISTICS_CORE');
  });

  it('binds graph inside AsyncLocalAuthGraphAccessor scope', async () => {
    const graph = AuthGraphBuilder.forTenant('LOGISTICS_CORE').build();
    const accessor = new AsyncLocalAuthGraphAccessor();
    const mw = umsAuthGraph({ accessor, signingSecret: SECRETO_DE_PRUEBA });

    let observed: AuthorizationGraph | null = null;
    await new Promise<void>((resolve) => {
      mw({ headers: { authorization: `Bearer ${fakeJwt(graph)}` } }, makeRes() as never, () => {
        observed = accessor.current();
        resolve();
      });
    });
    expect(observed).not.toBeNull();
    expect(observed!.context.tenant.code).toBe('LOGISTICS_CORE');
  });

  it('rejects with 401 AUTH_205 on unsupported MAJOR', async () => {
    const graph = AuthGraphBuilder.forTenant('LOGISTICS_CORE').withSchemaVersion('3.0.0').build();
    const accessor = new MemoryAuthGraphAccessor();
    const mw = umsAuthGraph({ accessor, signingSecret: SECRETO_DE_PRUEBA });

    const { res, siguiente } = await ejecutar(mw, { authorization: `Bearer ${fakeJwt(graph)}` });

    expect(siguiente).toBe(false);
    expect(res.body.status).toBe(401);
    expect((res.body.payload as { code: string }).code).toBe('AUTH_205');
  });

  it('rejects expired graph when rejectExpired is true', async () => {
    const graph = AuthGraphBuilder.forTenant('LOGISTICS_CORE').buildExpired();
    const accessor = new MemoryAuthGraphAccessor();
    const mw = umsAuthGraph({ accessor, signingSecret: SECRETO_DE_PRUEBA, rejectExpired: true });

    const { res, siguiente } = await ejecutar(mw, { authorization: `Bearer ${fakeJwt(graph)}` });

    expect(siguiente).toBe(false);
    expect(res.body.status).toBe(401);
    expect((res.body.payload as { code: string }).code).toBe('AUTH_201');
  });

  it('passes through when no bearer is provided', async () => {
    const accessor = new MemoryAuthGraphAccessor();
    const mw = umsAuthGraph({ accessor, signingSecret: SECRETO_DE_PRUEBA });
    let called = false;
    await new Promise<void>((resolve) => {
      mw({ headers: {} }, makeRes() as never, () => { called = true; resolve(); });
    });
    expect(called).toBe(true);
    expect(accessor.current()).toBeNull();
  });
});
