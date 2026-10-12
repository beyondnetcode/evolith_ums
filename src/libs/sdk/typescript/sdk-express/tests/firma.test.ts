import { describe, expect, it } from 'vitest';
import { constants, createHmac, generateKeyPairSync, sign } from 'node:crypto';
import { umsAuthGraph } from '../src/middleware.js';
import { MemoryAuthGraphAccessor } from '@evolith-ums/ums-sdk-authorization';

/**
 * G-217: el middleware descodificaba el token y se creía su contenido sin mirar la firma.
 * Cualquiera podía fabricarse un portador con el grafo que quisiera —incluido uno de
 * administrador— sin conocer el secreto. Estas pruebas existen para que eso no pueda volver.
 */

const SECRETO = 'secreto-de-prueba-de-al-menos-32-caracteres-1234'; // gitleaks:allow -- deterministic signing key used only by tests
const b64 = (o: unknown) =>
  Buffer.from(JSON.stringify(o)).toString('base64url');

/** Fabrica un token firmado de verdad. */
const tokenFirmado = (cuerpo: unknown, secreto = SECRETO): string => {
  const cabecera = b64({ alg: 'HS256', typ: 'JWT' });
  const c = b64(cuerpo);
  const firma = createHmac('sha256', secreto).update(`${cabecera}.${c}`).digest('base64url');
  return `${cabecera}.${c}.${firma}`;
};

const GRAFO = { schemaVersion: '2.4.0', scopes: ['todo.borrar'], profiles: [] };

/**
 * Invoca el middleware y espera a que termine.
 *
 * El `await` no es adorno: desde que la verificación admite RS256 (ADR-0157) el middleware es
 * asíncrono, porque resolver la clave puede exigir consultar el JWKS del emisor. Leer el resultado
 * sin esperar leería el estado anterior a la verificación, y la prueba pasaría sin comprobar nada.
 */
const invocar = async (token: string | null, opciones: Record<string, unknown> = {}) => {
  const accessor = new MemoryAuthGraphAccessor();
  const mw = umsAuthGraph({ accessor, signingSecret: SECRETO, ...opciones } as never);
  let estado = 0; let cuerpo: unknown = null; let siguiente = false;

  await new Promise<void>((resolve) => {
    const res = {
      status(c: number) { estado = c; return this; },
      json(b: unknown) { cuerpo = b; resolve(); return this; },
    } as never;
    mw(
      { headers: token ? { authorization: `Bearer ${token}` } : {} } as never,
      res,
      () => { siguiente = true; resolve(); }
    );
  });

  return { estado, cuerpo, siguiente, grafo: accessor.current() };
};

describe('G-217 · el portador se verifica antes de creerse su contenido', () => {
  it('acepta un token firmado con el secreto correcto', async () => {
    const r = await invocar(tokenFirmado({ graph: GRAFO }));
    expect(r.siguiente).toBe(true);
    expect(r.grafo).not.toBeNull();
  });

  it('RECHAZA un token fabricado a mano, sin firma válida', async () => {
    // El ataque exacto que G-217 describe: se escribe el grafo que se quiera y se le pega
    // cualquier cosa como firma. Antes, esto pasaba y el satélite concedía esos permisos.
    const cabecera = b64({ alg: 'HS256', typ: 'JWT' });
    const cuerpo = b64({ graph: { ...GRAFO, scopes: ['todo.borrar', 'admin.total'] } });
    const falsificado = `${cabecera}.${cuerpo}.firma-inventada`;

    const r = await invocar(falsificado);
    expect(r.estado).toBe(401);
    expect(r.siguiente).toBe(false);
    expect(r.grafo).toBeNull();
  });

  it('RECHAZA un token firmado con OTRO secreto', async () => {
    const r = await invocar(tokenFirmado({ graph: GRAFO }, 'otro-secreto-igual-de-largo-pero-ajeno-12'));
    expect(r.estado).toBe(401);
    expect(r.grafo).toBeNull();
  });

  it('RECHAZA `alg: none`, que es como se desactiva la firma sin tocarla', async () => {
    const cabecera = b64({ alg: 'none', typ: 'JWT' });
    const cuerpo = b64({ graph: GRAFO });
    const r = await invocar(`${cabecera}.${cuerpo}.`);
    expect(r.estado).toBe(401);
    expect(r.grafo).toBeNull();
  });

  it('exige material de verificación al construirse, no al recibir la primera petición', () => {
    expect(() => umsAuthGraph({ accessor: new MemoryAuthGraphAccessor() }))
      .toThrow(/umsBaseUrl|signingSecret/);
  });
});

describe('ADR-0157 · el mismo middleware verifica los portadores RS256 de UMS', () => {
  const { publicKey, privateKey } = generateKeyPairSync('rsa', { modulusLength: 2048 });
  const jwk = publicKey.export({ format: 'jwk' }) as Record<string, string>;
  const KID = 'kid-de-prueba';

  const jwksEnMemoria = (async () => ({
    ok: true,
    text: async () => JSON.stringify({
      keys: [{ kty: 'RSA', use: 'sig', alg: 'RS256', kid: KID, n: jwk['n'], e: jwk['e'] }],
    }),
  } as unknown as Response)) as unknown as typeof fetch;

  const tokenRs256 = (cuerpo: unknown, clave = privateKey) => {
    const cabecera = b64({ alg: 'RS256', typ: 'JWT', kid: KID });
    const c = b64(cuerpo);
    const firma = sign('sha256', Buffer.from(`${cabecera}.${c}`, 'ascii'),
      { key: clave, padding: constants.RSA_PKCS1_PADDING }).toString('base64url');
    return `${cabecera}.${c}.${firma}`;
  };

  /** Configurado SOLO para RS256: sin secreto, como debe quedar un satélite tras ADR-0157. */
  const soloRs256 = { signingSecret: undefined, umsBaseUrl: 'https://ums', jwks: { fetchImpl: jwksEnMemoria } };

  it('acepta el portador que UMS firma con su clave privada', async () => {
    const r = await invocar(tokenRs256({ graph: GRAFO }), soloRs256);
    expect(r.siguiente).toBe(true);
    expect(r.grafo).not.toBeNull();
  });

  it('RECHAZA uno firmado con otra clave, aunque declare el `kid` bueno', async () => {
    const ajena = generateKeyPairSync('rsa', { modulusLength: 2048 }).privateKey;
    const r = await invocar(tokenRs256({ graph: GRAFO }, ajena), soloRs256);
    expect(r.estado).toBe(401);
    expect(r.grafo).toBeNull();
  });

  it('configurado para RS256, un HS256 NO se cuela aunque el secreto exista en otro sitio', async () => {
    const r = await invocar(tokenFirmado({ graph: GRAFO }), soloRs256);
    expect(r.estado).toBe(401);
    expect(r.grafo).toBeNull();
  });
});
