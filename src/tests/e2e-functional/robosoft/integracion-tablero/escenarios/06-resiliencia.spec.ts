import { test, expect } from '@playwright/test';
import { execFileSync } from 'node:child_process';
import { TABLERO, USUARIOS, claveDe, exigirIdentidadConfigurada } from '../fixtures/entorno';

test.beforeAll(exigirIdentidadConfigurada);

/**
 * UMS caído. Se provoca DE VERDAD, escalando el deployment a 0 réplicas, en vez de interceptar: así
 * se prueba también que el Tablero distingue «no responde» de «responde error», que es donde suelen
 * fallar estos clientes.
 *
 * OPT-IN. Estos dos escenarios tumban UMS para el resto del clúster mientras corren, así que solo
 * se ejercen con `E2E_PERMITE_CAIDA=1`. Sin esa variable se OMITEN, y la omisión sale en el informe:
 * un carril que se salta en silencio una propiedad que no puede medir convierte el verde en mentira.
 */
const PERMITE = process.env.E2E_PERMITE_CAIDA === '1';
const CTX = process.env.UMS_CTX ?? 'kind-evolith-cluster-ums';
const NS = process.env.UMS_NS ?? 'ums';

const escalar = (replicas: number) =>
  execFileSync('kubectl', ['--context', CTX, '-n', NS, 'scale', 'deploy/ums-backend',
    `--replicas=${replicas}`], { encoding: 'utf-8' });

const esperarEndpoints = (hayQueHaber: boolean) => {
  for (let i = 0; i < 60; i++) {
    const eps = execFileSync('kubectl', ['--context', CTX, '-n', NS, 'get', 'endpoints',
      'ums-backend', '-o', 'jsonpath={.subsets[*].addresses[*].ip}'], { encoding: 'utf-8' }).trim();
    if (Boolean(eps) === hayQueHaber) return true;
    execFileSync('sleep', ['2']);
  }
  return false;
};

test.describe('06 · Resiliencia', () => {
  test.skip(!PERMITE, 'OMITIDO: exige E2E_PERMITE_CAIDA=1 — tumba UMS para todo el clúster.');

  test.afterAll(() => {
    // Restaurar SIEMPRE, incluso si un escenario revienta: dejar UMS a 0 romperia todo lo demas.
    if (PERMITE) { escalar(1); esperarEndpoints(true); }
  });

  test('E-18 · con UMS caído, el login NO acusa a las credenciales', async ({ request }) => {
    escalar(0);
    expect(esperarEndpoints(false)).toBe(true);

    const r = await request.post(`${TABLERO}/api/auth/login`, {
      data: { username: USUARIOS.valido, password: claveDe(USUARIOS.valido) },
    });

    // Lo que se prueba es la HONESTIDAD del mensaje. Colapsar una indisponibilidad en
    // «credenciales inválidas» mandó a diagnosticar la única pieza que estaba bien y costó horas.
    expect(r.status()).not.toBe(200);
    expect(r.status()).not.toBe(401);
    expect(await r.text()).not.toMatch(/credenciales/i);
  });

  test('E-19 · con UMS caído, una sesión viva NO se expulsa', async ({ request }) => {
    escalar(1);
    expect(esperarEndpoints(true)).toBe(true);
    await request.post(`${TABLERO}/api/auth/login`, {
      data: { username: USUARIOS.valido, password: claveDe(USUARIOS.valido) },
    });

    escalar(0);
    expect(esperarEndpoints(false)).toBe(true);

    // «No puedo revalidar» no es «no estás autenticado». Responder 401 aquí echaría a la calle a
    // quien sí tiene sesión, cada vez que UMS parpadee.
    const sesion = await request.get(`${TABLERO}/api/auth/sesion`);
    expect([200, 503]).toContain(sesion.status());
    if (sesion.status() === 200) {
      expect((await sesion.json()).autenticado).toBe(true);
    }
  });
});
