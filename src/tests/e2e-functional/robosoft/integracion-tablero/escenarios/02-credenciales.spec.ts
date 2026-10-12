import { test, expect } from '@playwright/test';
import { PaginaDeAcceso } from '../paginas/acceso.pagina';
import { TABLERO, USUARIOS, claveDe, exigirIdentidadConfigurada } from '../fixtures/entorno';

test.beforeAll(exigirIdentidadConfigurada);

test.describe('02 · Credenciales', () => {
  test('E-04 · contraseña incorrecta no entra y el mensaje es accionable', async ({ page }) => {
    const acceso = new PaginaDeAcceso(page);
    await acceso.ir();
    const respuesta = await acceso.entrar(USUARIOS.valido, 'ClaveQueNoEs.2026!');

    expect(respuesta.status()).toBe(401);
    await expect(acceso.titulo()).toBeVisible();

    // Capa de experiencia: dice qué hacer y no filtra detalle interno.
    const texto = (await acceso.error().textContent()) ?? '';
    expect(texto.length).toBeGreaterThan(0);
    expect(texto).not.toMatch(/stack|SQL|Exception|UMS_|http:\/\//i);
  });

  test('E-05 · usuario inexistente es INDISTINGUIBLE de contraseña incorrecta', async ({ request }) => {
    // El corazón del escenario, y la razón de que sea una prueba y no una revisión: si el estado o
    // el cuerpo difieren, el formulario se convierte en un detector de cuentas para cualquiera.
    const mala = await request.post(`${TABLERO}/api/auth/login`, {
      data: { username: USUARIOS.valido, password: 'ClaveQueNoEs.2026!' },
    });
    const inexistente = await request.post(`${TABLERO}/api/auth/login`, {
      data: { username: USUARIOS.inexistente, password: 'ClaveQueNoEs.2026!' },
    });

    expect(inexistente.status()).toBe(mala.status());
    expect(await inexistente.text()).toBe(await mala.text());
  });

  test('E-06 · una cuenta recién solicitada no entra hasta que la aprueben', async ({ request }) => {
    const marca = Date.now().toString(36);
    const correo = `alta.fase1.${marca}@evolith.example`;
    const clave = `Alta.${marca}.2026!`;

    const alta = await request.post(`${TABLERO}/api/auth/signup`, {
      data: { displayName: 'Alta de Fase 1', email: correo, password: clave },
    });
    expect(alta.status()).toBe(202);

    const intento = await request.post(`${TABLERO}/api/auth/login`, {
      data: { username: correo, password: clave },
    });
    expect(intento.status()).not.toBe(200);
  });

  test('E-07 · sin usuario o sin contraseña se rechaza en el Tablero', async ({ request }) => {
    for (const cuerpo of [{}, { username: USUARIOS.valido }, { password: 'x' }]) {
      const r = await request.post(`${TABLERO}/api/auth/login`, { data: cuerpo });
      expect(r.status()).toBe(400);
    }
  });
});
