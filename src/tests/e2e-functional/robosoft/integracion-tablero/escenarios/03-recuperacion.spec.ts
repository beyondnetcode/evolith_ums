import { test, expect } from '@playwright/test';
import { TABLERO, USUARIOS, claveDe, exigirIdentidadConfigurada } from '../fixtures/entorno';

test.beforeAll(exigirIdentidadConfigurada);

test.describe('03 · Recuperación de contraseña', () => {
  const recuperar = (request: any, email: string) =>
    request.post(`${TABLERO}/api/auth/recuperar`, { data: { email } });

  test('E-08 · la respuesta no delata si el correo está registrado', async ({ request }) => {
    const existe = await recuperar(request, USUARIOS.valido);
    const noExiste = await recuperar(request, USUARIOS.inexistente);

    expect(noExiste.status()).toBe(existe.status());
    expect(await noExiste.text()).toBe(await existe.text());
  });

  test('E-09 · la respuesta NO lleva el secreto de restablecimiento', async ({ request }) => {
    const r = await recuperar(request, USUARIOS.valido);
    const texto = await r.text();
    // El token es una cadena larga en base64url. Reenviarlo al navegador convertiría este
    // formulario en una toma de cuentas para cualquiera que sepa un correo.
    expect(texto).not.toMatch(/[A-Za-z0-9_-]{32,}/);
  });

  test('E-10 · solicitar el restablecimiento NO cambia la contraseña vigente', async ({ request }) => {
    await recuperar(request, USUARIOS.auditor);

    const entra = await request.post(`${TABLERO}/api/auth/login`, {
      data: { username: USUARIOS.auditor, password: claveDe(USUARIOS.auditor) },
    });
    expect(entra.status()).toBe(200);
  });
});
