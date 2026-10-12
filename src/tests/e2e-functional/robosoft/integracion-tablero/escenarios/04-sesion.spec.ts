import { test, expect } from '@playwright/test';
import { PaginaDeAcceso } from '../paginas/acceso.pagina';
import { TABLERO, USUARIOS, claveDe, exigirIdentidadConfigurada } from '../fixtures/entorno';

test.beforeAll(exigirIdentidadConfigurada);

test.describe('04 · Ciclo de sesión', () => {
  test('E-11 · cerrar sesión devuelve al acceso', async ({ page, request }) => {
    const acceso = new PaginaDeAcceso(page);
    await acceso.ir();
    await acceso.entrar(USUARIOS.valido, claveDe(USUARIOS.valido));
    await expect(acceso.titulo()).toBeHidden({ timeout: 15000 });

    await page.request.post(`${TABLERO}/api/auth/logout`);
    await page.reload();
    await expect(acceso.titulo()).toBeVisible({ timeout: 15000 });
  });

  test('E-12 · tras cerrar sesión, la sesión deja de resolver', async ({ request }) => {
    await request.post(`${TABLERO}/api/auth/login`, {
      data: { username: USUARIOS.valido, password: claveDe(USUARIOS.valido) },
    });
    expect((await (await request.get(`${TABLERO}/api/auth/sesion`)).json()).autenticado).toBe(true);

    await request.post(`${TABLERO}/api/auth/logout`);
    expect((await (await request.get(`${TABLERO}/api/auth/sesion`)).json()).autenticado).toBe(false);
  });

  test('E-13 · sin sesión, una ruta protegida responde 401 y no 200 vacío', async ({ request }) => {
    // Un 200 con lista vacía sería indistinguible de «no tienes nada», y ahí es donde un gate
    // ausente pasa por gate presente.
    const r = await request.get(`${TABLERO}/api/satelites`);
    expect(r.status()).toBe(401);
  });

  test('E-14 · cerrar sesión es idempotente', async ({ request }) => {
    await request.post(`${TABLERO}/api/auth/login`, {
      data: { username: USUARIOS.valido, password: claveDe(USUARIOS.valido) },
    });
    const primero = await request.post(`${TABLERO}/api/auth/logout`);
    const segundo = await request.post(`${TABLERO}/api/auth/logout`);
    expect(primero.status()).toBeLessThan(400);
    expect(segundo.status()).toBeLessThan(400);
  });
});
