import { test, expect } from '@playwright/test';
import { PaginaDeAcceso } from '../paginas/acceso.pagina';
import { TABLERO, USUARIOS, claveDe, exigirIdentidadConfigurada } from '../fixtures/entorno';

test.beforeAll(exigirIdentidadConfigurada);

test.describe('01 · Acceso', () => {
  test('E-01 · credenciales válidas abren la aplicación', async ({ page }) => {
    const acceso = new PaginaDeAcceso(page);
    await acceso.ir();
    await expect(acceso.titulo()).toBeVisible();

    const respuesta = await acceso.entrar(USUARIOS.valido, claveDe(USUARIOS.valido));

    // Capa HTTP: no basta con que la pantalla cambie. El contrato es 200 y el token NO en el cuerpo.
    expect(respuesta.status()).toBe(200);
    const cuerpo = await respuesta.json();
    expect(JSON.stringify(cuerpo)).not.toContain('eyJ');

    // Capa visual: la pantalla de acceso desaparece.
    await expect(acceso.titulo()).toBeHidden({ timeout: 15000 });
  });

  test('E-02 · la sesión sobrevive a recargar la página', async ({ page }) => {
    const acceso = new PaginaDeAcceso(page);
    await acceso.ir();
    await acceso.entrar(USUARIOS.valido, claveDe(USUARIOS.valido));
    await expect(acceso.titulo()).toBeHidden({ timeout: 15000 });

    // Es la prueba de que la sesión vive en la cookie y no en memoria del cliente. Cuando el
    // servidor leía el grafo del token —que ya no lo lleva— el usuario entraba y en la petición
    // siguiente estaba fuera: verde en el login, roto al recargar.
    await page.reload();
    await expect(acceso.titulo()).toBeHidden({ timeout: 15000 });
  });

  test('E-03 · la sesión resuelve con grafo y perfil, no solo con un booleano', async ({ request }) => {
    const login = await request.post(`${TABLERO}/api/auth/login`, {
      data: { username: USUARIOS.valido, password: claveDe(USUARIOS.valido) },
    });
    expect(login.status()).toBe(200);

    const sesion = await (await request.get(`${TABLERO}/api/auth/sesion`)).json();
    expect(sesion.autenticado).toBe(true);
    expect(sesion.rol?.code).toBeTruthy();
    // Sin scopes el gating no puede fallar, y una sesión así hace verde cualquier prueba de
    // permisos sobre una interfaz vacía.
    expect(sesion.graph?.scopes?.length ?? 0).toBeGreaterThan(0);
  });
});
