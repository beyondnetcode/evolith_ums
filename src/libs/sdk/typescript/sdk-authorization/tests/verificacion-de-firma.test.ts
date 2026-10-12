import { describe, expect, it } from 'vitest';
import { createHmac, generateKeyPairSync, sign, constants, type KeyObject } from 'node:crypto';
import { CacheDeJwks } from '../src/jwks.js';
import { verificarFirma, verificarFirmaHs256 } from '../src/verificacion-de-firma.js';

/**
 * ADR-0157: UMS firma en RS256 y publica su clave pública. El verificador exigía HS256 de forma
 * literal —correcto contra la confusión de algoritmo (G-217), pero rechazaba los portadores buenos—.
 *
 * Estas pruebas fijan las dos mitades a la vez: que RS256 se acepte, y que aceptarlo no haya
 * abierto ninguna de las puertas que la exigencia original cerraba.
 */

const SECRETO = 'secreto-de-prueba-de-al-menos-32-caracteres-1234'; // gitleaks:allow -- deterministic signing key used only by tests
const b64 = (o: unknown) => Buffer.from(JSON.stringify(o)).toString('base64url');
const CUERPO = { sub: 'admin@evolith.example', graph: { schemaVersion: '2.4.0' } };

function parDeClaves(kid: string) {
  const { publicKey, privateKey } = generateKeyPairSync('rsa', { modulusLength: 2048 });
  const jwk = publicKey.export({ format: 'jwk' }) as Record<string, string>;
  return {
    privada: privateKey,
    jwk: { kty: 'RSA', use: 'sig', alg: 'RS256', kid, n: jwk['n'], e: jwk['e'] },
  };
}

function tokenRs256(privada: KeyObject, kid: string, cuerpo: unknown = CUERPO): string {
  const cabecera = b64({ alg: 'RS256', typ: 'JWT', kid });
  const c = b64(cuerpo);
  const firma = sign(
    'sha256',
    Buffer.from(`${cabecera}.${c}`, 'ascii'),
    { key: privada, padding: constants.RSA_PKCS1_PADDING }
  ).toString('base64url');
  return `${cabecera}.${c}.${firma}`;
}

function tokenHs256(secreto = SECRETO, cuerpo: unknown = CUERPO): string {
  const cabecera = b64({ alg: 'HS256', typ: 'JWT' });
  const c = b64(cuerpo);
  const firma = createHmac('sha256', secreto).update(`${cabecera}.${c}`).digest('base64url');
  return `${cabecera}.${c}.${firma}`;
}

/** JWKS servido en memoria, contando las peticiones para poder afirmar cuántas hubo. */
function servidorDeJwks(claves: unknown[]) {
  const estado = { peticiones: 0, cuerpo: { keys: claves } as unknown };
  const fetchImpl = (async () => {
    estado.peticiones += 1;
    return {
      ok: true,
      text: async () => JSON.stringify(estado.cuerpo),
    } as unknown as Response;
  }) as unknown as typeof fetch;
  return { estado, fetchImpl };
}

describe('RS256 por JWKS (ADR-0157)', () => {
  it('(a) acepta un token firmado con la clave que publica el JWKS', async () => {
    const { privada, jwk } = parDeClaves('kid-bueno');
    const { fetchImpl } = servidorDeJwks([jwk]);
    const jwks = new CacheDeJwks({ uri: 'https://ums/.well-known/jwks.json', fetchImpl });

    const r = await verificarFirma(tokenRs256(privada, 'kid-bueno'), { jwks });

    expect(r.valido).toBe(true);
    if (r.valido) expect(r.cuerpo['sub']).toBe('admin@evolith.example');
  });

  it('(b) RECHAZA un token firmado con OTRA clave, aunque declare un `kid` que sí existe', async () => {
    // El ataque realista: el `kid` es válido y el token está bien formado; lo único que falla es
    // que quien firmó no tenía la clave privada del emisor.
    const { jwk } = parDeClaves('kid-bueno');
    const ajena = generateKeyPairSync('rsa', { modulusLength: 2048 }).privateKey;
    const { fetchImpl } = servidorDeJwks([jwk]);
    const jwks = new CacheDeJwks({ uri: 'https://ums/.well-known/jwks.json', fetchImpl });

    const r = await verificarFirma(tokenRs256(ajena, 'kid-bueno'), { jwks });

    expect(r.valido).toBe(false);
    if (!r.valido) expect(r.motivo).toBe('FIRMA');
  });

  it('(c) RECHAZA `alg: none`, que es como se desactiva la firma sin tocarla', async () => {
    const { jwk } = parDeClaves('kid-bueno');
    const { fetchImpl } = servidorDeJwks([jwk]);
    const jwks = new CacheDeJwks({ uri: 'https://ums/.well-known/jwks.json', fetchImpl });

    const token = `${b64({ alg: 'none', typ: 'JWT' })}.${b64(CUERPO)}.`;
    const r = await verificarFirma(token, { jwks, secretoHs256: SECRETO });

    expect(r.valido).toBe(false);
    if (!r.valido) expect(r.motivo).toBe('ALGORITMO');
  });

  it('RECHAZA un token sin `kid`: no se prueba con «alguna» clave', async () => {
    const { privada, jwk } = parDeClaves('kid-bueno');
    const { fetchImpl } = servidorDeJwks([jwk]);
    const jwks = new CacheDeJwks({ uri: 'https://ums/.well-known/jwks.json', fetchImpl });

    const cabecera = b64({ alg: 'RS256', typ: 'JWT' }); // sin kid
    const c = b64(CUERPO);
    const firma = sign('sha256', Buffer.from(`${cabecera}.${c}`, 'ascii'),
      { key: privada, padding: constants.RSA_PKCS1_PADDING }).toString('base64url');

    const r = await verificarFirma(`${cabecera}.${c}.${firma}`, { jwks });

    expect(r.valido).toBe(false);
    if (!r.valido) expect(r.motivo).toBe('CLAVE_DESCONOCIDA');
  });

  it('trae el JWKS UNA vez para muchas verificaciones: la caché evita el GET por token', async () => {
    const { privada, jwk } = parDeClaves('kid-bueno');
    const { estado, fetchImpl } = servidorDeJwks([jwk]);
    const jwks = new CacheDeJwks({ uri: 'https://ums/.well-known/jwks.json', fetchImpl });

    for (let i = 0; i < 25; i += 1) {
      const r = await verificarFirma(tokenRs256(privada, 'kid-bueno'), { jwks });
      expect(r.valido).toBe(true);
    }
    expect(estado.peticiones).toBe(1);
  });

  it('verificaciones concurrentes con la caché fría hacen UNA petición, no una por token', async () => {
    const { privada, jwk } = parDeClaves('kid-bueno');
    const { estado, fetchImpl } = servidorDeJwks([jwk]);
    const jwks = new CacheDeJwks({ uri: 'https://ums/.well-known/jwks.json', fetchImpl });

    const resultados = await Promise.all(
      Array.from({ length: 20 }, () => verificarFirma(tokenRs256(privada, 'kid-bueno'), { jwks }))
    );

    expect(resultados.every((r) => r.valido)).toBe(true);
    expect(estado.peticiones).toBe(1);
  });

  it('una rotación se recoge sola: un `kid` nuevo dispara un refresco y el token pasa', async () => {
    const vieja = parDeClaves('kid-v1');
    const { estado, fetchImpl } = servidorDeJwks([vieja.jwk]);
    const jwks = new CacheDeJwks({ uri: 'https://ums/.well-known/jwks.json', fetchImpl });

    expect((await verificarFirma(tokenRs256(vieja.privada, 'kid-v1'), { jwks })).valido).toBe(true);

    // UMS rota: publica una clave nueva con otro `kid`.
    const nueva = parDeClaves('kid-v2');
    estado.cuerpo = { keys: [vieja.jwk, nueva.jwk] };

    const r = await verificarFirma(tokenRs256(nueva.privada, 'kid-v2'), { jwks });
    expect(r.valido).toBe(true);
    expect(estado.peticiones).toBe(2);
  });
});

describe('(d) un `kid` desconocido no cuelga ni abre una tormenta contra el JWKS', () => {
  it('mil `kid` inventados producen UNA petición, no mil', async () => {
    // Sin acotar, el refresco por `kid` desconocido es un amplificador de denegación de servicio:
    // lo dispara un desconocido, porque el `kid` viaja en un token todavía sin verificar.
    const { privada, jwk } = parDeClaves('kid-bueno');
    const { estado, fetchImpl } = servidorDeJwks([jwk]);
    const jwks = new CacheDeJwks({
      uri: 'https://ums/.well-known/jwks.json',
      fetchImpl,
      esperaEntreRefrescosMs: 60_000,
    });

    for (let i = 0; i < 1000; i += 1) {
      const r = await verificarFirma(tokenRs256(privada, `inventado-${i}`), { jwks });
      expect(r.valido).toBe(false);
      if (!r.valido) expect(r.motivo).toBe('CLAVE_DESCONOCIDA');
    }

    // Dos: la de arranque —la caché estaba vacía— y una única sonda por si fuera una rotación.
    // Las otras 998 no tocan la red. Lo que importa no es el número exacto sino que esté acotado
    // por la ventana de tiempo y no por el número de tokens que mande el atacante.
    expect(estado.peticiones).toBe(2);
  });

  it('repetir el MISMO `kid` falso no consume el refresco que necesita una rotación', async () => {
    // Sin memoria de descartados, un único `kid` falso repetido se comería el refresco de cada
    // ventana y una rotación legítima no llegaría a verse nunca.
    const reloj = { t: 1_000_000 };
    const vieja = parDeClaves('kid-v1');
    const { estado, fetchImpl } = servidorDeJwks([vieja.jwk]);
    const jwks = new CacheDeJwks({
      uri: 'https://ums/.well-known/jwks.json',
      fetchImpl,
      esperaEntreRefrescosMs: 60_000,
      memoriaDeDescartadosMs: 3_600_000,
      reloj: () => reloj.t,
    });

    await verificarFirma(tokenRs256(vieja.privada, 'kid-v1'), { jwks }); // 1ª petición: arranque
    expect(estado.peticiones).toBe(1);

    // El atacante machaca con el mismo `kid` falso. El reloj avanza poco, para que el TTL no
    // caduque y quede aislado lo que se mide: el coste de repetir un `kid` que ya se descartó.
    for (let i = 0; i < 10; i += 1) {
      reloj.t += 10_000;
      await verificarFirma(tokenRs256(vieja.privada, 'kid-falso'), { jwks });
    }
    // Solo la primera vez se miró de verdad; a partir de ahí se recuerda que no estaba.
    expect(estado.peticiones).toBe(2);

    // Y cuando llega la rotación de verdad, el refresco sigue disponible.
    const nueva = parDeClaves('kid-v2');
    estado.cuerpo = { keys: [nueva.jwk] };
    reloj.t += 120_000;

    const r = await verificarFirma(tokenRs256(nueva.privada, 'kid-v2'), { jwks });
    expect(r.valido).toBe(true);
  });

  it('si el JWKS no responde, se rechaza — no se cuelga ni se deja pasar', async () => {
    const fetchImpl = (async () => {
      throw new Error('ECONNREFUSED');
    }) as unknown as typeof fetch;
    const jwks = new CacheDeJwks({ uri: 'https://ums/.well-known/jwks.json', fetchImpl });
    const { privada } = parDeClaves('kid-bueno');

    const r = await verificarFirma(tokenRs256(privada, 'kid-bueno'), { jwks });

    expect(r.valido).toBe(false);
    if (!r.valido) expect(r.motivo).toBe('JWKS_INACCESIBLE');
  });

  it('si en la URI del JWKS hay HTML —enrutado equivocado— se rechaza, no se interpreta', async () => {
    // Ocurre de verdad: si el JWKS queda detrás del comodín del frontal, la respuesta es la página
    // del SPA con un 200 alegre. Un 200 no es un JWKS.
    const fetchImpl = (async () => ({
      ok: true,
      text: async () => '<!doctype html><html><body>SPA</body></html>',
    } as unknown as Response)) as unknown as typeof fetch;
    const jwks = new CacheDeJwks({ uri: 'https://ums/.well-known/jwks.json', fetchImpl });
    const { privada } = parDeClaves('kid-bueno');

    const r = await verificarFirma(tokenRs256(privada, 'kid-bueno'), { jwks });

    expect(r.valido).toBe(false);
    if (!r.valido) expect(r.motivo).toBe('JWKS_INACCESIBLE');
  });
});

describe('(e) HS256 y RS256 conviven sin que aceptar los dos permita degradar', () => {
  it('con solo `secretoHs256`, un token HS256 legítimo sigue pasando', async () => {
    const r = await verificarFirma(tokenHs256(), { secretoHs256: SECRETO });
    expect(r.valido).toBe(true);
  });

  it('con solo JWKS configurado, un token HS256 NO se cuela', async () => {
    // La degradación clásica: el consumidor espera RS256 y alguien le presenta un HS256. Se
    // rechaza por configuración, sin llegar siquiera a mirar la firma.
    const { jwk } = parDeClaves('kid-bueno');
    const { estado, fetchImpl } = servidorDeJwks([jwk]);
    const jwks = new CacheDeJwks({ uri: 'https://ums/.well-known/jwks.json', fetchImpl });

    const r = await verificarFirma(tokenHs256(), { jwks });

    expect(r.valido).toBe(false);
    if (!r.valido) expect(r.motivo).toBe('ALGORITMO');
    expect(estado.peticiones).toBe(0);
  });

  it('con solo JWKS, un HS256 firmado con la clave PÚBLICA como secreto tampoco se cuela', async () => {
    // La confusión de algoritmo canónica: la clave pública es conocida, así que si el verificador
    // dejara que el token eligiera el algoritmo, cualquiera podría usarla como secreto HMAC.
    const { jwk } = parDeClaves('kid-bueno');
    const { fetchImpl } = servidorDeJwks([jwk]);
    const jwks = new CacheDeJwks({ uri: 'https://ums/.well-known/jwks.json', fetchImpl });

    const r = await verificarFirma(tokenHs256(jwk.n as string), { jwks });

    expect(r.valido).toBe(false);
    if (!r.valido) expect(r.motivo).toBe('ALGORITMO');
  });

  it('con solo `secretoHs256`, un RS256 no se acepta aunque su firma sea buena', async () => {
    const { privada } = parDeClaves('kid-bueno');
    const r = await verificarFirma(tokenRs256(privada, 'kid-bueno'), { secretoHs256: SECRETO });

    expect(r.valido).toBe(false);
    if (!r.valido) expect(r.motivo).toBe('ALGORITMO');
  });

  it('durante la transición, con ambos configurados, cada token va por su vía y ninguna se relaja', async () => {
    const { privada, jwk } = parDeClaves('kid-bueno');
    const { fetchImpl } = servidorDeJwks([jwk]);
    const jwks = new CacheDeJwks({ uri: 'https://ums/.well-known/jwks.json', fetchImpl });
    const ambos = { jwks, secretoHs256: SECRETO };

    expect((await verificarFirma(tokenRs256(privada, 'kid-bueno'), ambos)).valido).toBe(true);
    expect((await verificarFirma(tokenHs256(), ambos)).valido).toBe(true);

    // Y los rechazos siguen siendo rechazos: ni el HS256 con otro secreto, ni el RS256 con otra clave.
    expect((await verificarFirma(tokenHs256('otro-secreto-igual-de-largo-pero-ajeno'), ambos)).valido).toBe(false);
    const ajena = generateKeyPairSync('rsa', { modulusLength: 2048 }).privateKey;
    expect((await verificarFirma(tokenRs256(ajena, 'kid-bueno'), ambos)).valido).toBe(false);
  });

  it('sin material de verificación no se acepta nada: la omisión no es una vía de escape', async () => {
    const r = await verificarFirma(tokenHs256(), {});
    expect(r.valido).toBe(false);
  });

  it('RECHAZA un algoritmo que nadie habilitó (ES256), aunque el token insista', async () => {
    const { fetchImpl } = servidorDeJwks([parDeClaves('kid-bueno').jwk]);
    const jwks = new CacheDeJwks({ uri: 'https://ums/.well-known/jwks.json', fetchImpl });
    const token = `${b64({ alg: 'ES256', typ: 'JWT', kid: 'kid-bueno' })}.${b64(CUERPO)}.firma`;

    const r = await verificarFirma(token, { jwks, secretoHs256: SECRETO });

    expect(r.valido).toBe(false);
    if (!r.valido) expect(r.motivo).toBe('ALGORITMO');
  });
});

describe('la vía HS256 síncrona se mantiene para quien aún no ha cruzado', () => {
  it('sigue aceptando lo bueno y rechazando lo fabricado', () => {
    expect(verificarFirmaHs256(tokenHs256(), SECRETO).valido).toBe(true);
    expect(verificarFirmaHs256(`${b64({ alg: 'HS256' })}.${b64(CUERPO)}.inventada`, SECRETO).valido).toBe(false);
    expect(verificarFirmaHs256(`${b64({ alg: 'none' })}.${b64(CUERPO)}.`, SECRETO).valido).toBe(false);
  });

  it('no acepta RS256: quien la use seguirá rechazando los portadores nuevos', () => {
    const { privada } = parDeClaves('kid-bueno');
    const r = verificarFirmaHs256(tokenRs256(privada, 'kid-bueno'), SECRETO);
    expect(r.valido).toBe(false);
    if (!r.valido) expect(r.motivo).toBe('ALGORITMO');
  });
});

describe('el JWKS no se cree cualquier cosa', () => {
  it('descarta una clave simétrica colada en el JWKS', async () => {
    // Un JWKS con una clave simétrica dentro sería el emisor publicando con qué firmar.
    const { fetchImpl } = servidorDeJwks([{ kty: 'oct', kid: 'kid-bueno', k: 'AAAA', alg: 'HS256' }]);
    const jwks = new CacheDeJwks({ uri: 'https://ums/.well-known/jwks.json', fetchImpl });

    const r = await verificarFirma(tokenHs256(), { jwks });
    expect(r.valido).toBe(false);
  });

  it('descarta una clave RSA por debajo de 2048 bits y conserva las demás', async () => {
    // Una clave corta verifica sin error y sin aviso: si no se descarta aquí, deja el sistema
    // débil sin que nada falle. Se publica junto a una buena para comprobar que se descarta ella
    // sola, no el JWKS entero.
    const buena = parDeClaves('kid-bueno');
    const { publicKey, privateKey } = generateKeyPairSync('rsa', { modulusLength: 1024 });
    const jwk = publicKey.export({ format: 'jwk' }) as Record<string, string>;
    const { fetchImpl } = servidorDeJwks([
      { kty: 'RSA', use: 'sig', alg: 'RS256', kid: 'corta', n: jwk['n'], e: jwk['e'] },
      buena.jwk,
    ]);
    const jwks = new CacheDeJwks({ uri: 'https://ums/.well-known/jwks.json', fetchImpl });

    const r = await verificarFirma(tokenRs256(privateKey, 'corta'), { jwks });
    expect(r.valido).toBe(false);
    if (!r.valido) expect(r.motivo).toBe('CLAVE_DESCONOCIDA');

    // La buena sigue sirviendo.
    expect((await verificarFirma(tokenRs256(buena.privada, 'kid-bueno'), { jwks })).valido).toBe(true);
  });
});
