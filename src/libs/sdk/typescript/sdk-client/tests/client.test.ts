import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { describe, it, expect } from 'vitest';
import { AuthGraphBuilder } from '@evolith-ums/ums-sdk-testing';
import { SchemaVersion } from '@evolith-ums/ums-sdk-contracts';
import { UmsAuthClient, type ClientAuthOutcome } from '../src/index.js';

/**
 * El sobre REAL de `POST /api/v1/client/authenticate`, capturado de la API viva por
 * `ContratoDeClientAuthenticateTests` y regenerable desde ahí.
 *
 * No está escrito a mano a propósito. Las pruebas de este cliente sí lo estaban, con la forma que
 * el SDK creía correcta —`graph` como objeto—, y por eso pasaban mientras cualquier login real
 * fallaba con `AUTH_204` (G-207). Una prueba escrita contra lo que uno supone no comprueba nada.
 */
const SOBRE_REAL = JSON.parse(
  readFileSync(
    fileURLToPath(new URL('../../../contracts/fixtures/client-authenticate.envelope.json', import.meta.url)),
    'utf-8'
  )
) as Record<string, unknown>;

function makeFetch(status: number, body: unknown): typeof fetch {
  return async () =>
    new Response(typeof body === 'string' ? body : JSON.stringify(body), {
      status,
      headers: { 'content-type': 'application/json' }
    });
}

describe('UmsAuthClient', () => {
  it('returns Result.success on happy path', async () => {
    const graph = AuthGraphBuilder.forTenant('LOGISTICS_CORE')
      .withScope('PURCHASE_ORDER.VIEW')
      .build();

    const client = new UmsAuthClient({
      baseUrl: 'https://ums.example.com',
      fetchImpl: makeFetch(200, {
        token: 'TOKEN.PLACEHOLDER.SIG',
        tokenType: 'Bearer',
        expiresIn: 3600,
        issuedAt: new Date().toISOString(),
        format: 'JSON',
        // Cadena, no objeto: es como viaja por el cable. Escrito como objeto, esta prueba pasaba
        // mientras cualquier login real fallaba (G-207).
        graph: JSON.stringify(graph),
        requestId: '0HN7QMV8KJLDT:00000001'
      })
    });

    const outcome: ClientAuthOutcome = await client.authenticate({
      tenantCode: 'LOGISTICS_CORE',
      username: 'u',
      password: 'p'
    });

    expect(outcome.ok).toBe(true);
    if (outcome.ok) {
      expect(outcome.value.graph.context.tenant.code).toBe('LOGISTICS_CORE');
    }
  });

  it('returns AUTH_205 when server emits an unsupported MAJOR', async () => {
    const graph = AuthGraphBuilder.forTenant('LOGISTICS_CORE').withSchemaVersion('3.0.0').build();

    const client = new UmsAuthClient({
      baseUrl: 'https://ums.example.com',
      fetchImpl: makeFetch(200, {
        token: 'T', tokenType: 'Bearer', expiresIn: 60,
        issuedAt: new Date().toISOString(), format: 'JSON',
        graph: JSON.stringify(graph), requestId: '0HN7QMV8KJLDT:00000001'
      })
    });

    const outcome = await client.authenticate({ tenantCode: 'LOGISTICS_CORE', username: 'u', password: 'p' });
    expect(outcome.ok).toBe(false);
    if (!outcome.ok) expect(outcome.error.code).toBe('AUTH_205');
  });

  it('entiende el sobre real capturado de la API (G-207)', async () => {
    const client = new UmsAuthClient({
      baseUrl: 'https://ums.example.com',
      fetchImpl: makeFetch(200, SOBRE_REAL)
    });

    const outcome = await client.authenticate({
      tenantCode: 'COMEX_ANDINA',
      username: 'usuario.impo@comexandina.com.pe',
      password: 'p'
    });

    expect(outcome.ok).toBe(true);
    if (outcome.ok) {
      // Se LEE la constante en vez de copiar el número: copiándolo, cada versión nueva pone esto
      // en rojo por el motivo equivocado —el fixture y el SDK avanzaron a la vez— y ese rojo se
      // lee como una regresión del cliente.
      expect(outcome.value.graph.schemaVersion).toBe(SchemaVersion.Current);
      expect(outcome.value.graph.context.tenant.code).toBe('COMEX_ANDINA');
    }
  });

  it('rechaza con un mensaje útil si el inquilino sirve el grafo en XML', async () => {
    // Antes daba «falta schemaVersion», que manda a buscar el problema al sitio equivocado.
    const client = new UmsAuthClient({
      baseUrl: 'https://ums.example.com',
      fetchImpl: makeFetch(200, { ...SOBRE_REAL, format: 'XML', graph: '<graph/>' })
    });

    const outcome = await client.authenticate({ tenantCode: 'X', username: 'u', password: 'p' });
    expect(outcome.ok).toBe(false);
    if (!outcome.ok) {
      expect(outcome.error.code).toBe('AUTH_203');
      expect(outcome.error.message).toContain('XML');
    }
  });

  it('maps HTTP 401 to AUTH_006 (InvalidCredentials)', async () => {
    const client = new UmsAuthClient({
      baseUrl: 'https://ums.example.com',
      fetchImpl: makeFetch(401, 'unauthorized')
    });

    const outcome = await client.authenticate({ tenantCode: 'LOGISTICS_CORE', username: 'u', password: 'bad' });
    expect(outcome.ok).toBe(false);
    if (!outcome.ok) expect(outcome.error.code).toBe('AUTH_006');
  });

  it('maps HTTP 404 to AUTH_002 (TenantNotFound)', async () => {
    const client = new UmsAuthClient({
      baseUrl: 'https://ums.example.com',
      fetchImpl: makeFetch(404, 'not found')
    });

    const outcome = await client.authenticate({ tenantCode: 'ZZZ', username: 'u', password: 'p' });
    expect(outcome.ok).toBe(false);
    if (!outcome.ok) expect(outcome.error.code).toBe('AUTH_002');
  });
});
