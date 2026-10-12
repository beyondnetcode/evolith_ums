import { test, expect } from '@playwright/test';
import { PaginaDeAcceso } from '../paginas/acceso.pagina';
import { TABLERO, USUARIOS, claveDe, exigirIdentidadConfigurada } from '../fixtures/entorno';

test.beforeAll(exigirIdentidadConfigurada);

test.describe('05 · Dónde vive el token', () => {
  test('E-15 · el token de UMS NO llega al almacenamiento del navegador', async ({ page }) => {
    const acceso = new PaginaDeAcceso(page);
    await acceso.ir();
    await acceso.entrar(USUARIOS.valido, claveDe(USUARIOS.valido));
    await expect(acceso.titulo()).toBeHidden({ timeout: 15000 });

    const almacenado = await page.evaluate(() => {
      const volcar = (s: Storage) => Object.keys(s).map((k) => `${k}=${s.getItem(k)}`).join('|');
      return `${volcar(localStorage)}||${volcar(sessionStorage)}`;
    });

    // Un JWT empieza por `eyJ`. En `localStorage` sería legible por cualquier script de la página:
    // por eso la sesión vive en una cookie HttpOnly y el token no cruza al cliente.
    expect(almacenado).not.toContain('eyJ');
  });

  test('E-16 · la cookie de sesión es HttpOnly', async ({ request }) => {
    const r = await request.post(`${TABLERO}/api/auth/login`, {
      data: { username: USUARIOS.valido, password: claveDe(USUARIOS.valido) },
    });
    expect(r.status()).toBe(200);

    const cookies = r.headersArray().filter((h) => h.name.toLowerCase() === 'set-cookie');
    expect(cookies.length).toBeGreaterThan(0);
    for (const c of cookies) expect(c.value).toMatch(/HttpOnly/i);
  });

  test('E-17 · el token tampoco aparece en el HTML servido', async ({ page }) => {
    const acceso = new PaginaDeAcceso(page);
    await acceso.ir();
    await acceso.entrar(USUARIOS.valido, claveDe(USUARIOS.valido));
    await expect(acceso.titulo()).toBeHidden({ timeout: 15000 });

    // Incrustarlo en el documento lo dejaría en el historial, en la caché y en cualquier captura.
    expect(await page.content()).not.toContain('eyJ');
  });
});
