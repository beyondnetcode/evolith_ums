import { defineConfig } from '@playwright/test';

/**
 * Carril E — autorizacion (Fase 2).
 *
 * Sin navegador a proposito: lo que se prueba es que UMS conceda a cada perfil lo suyo, y eso
 * viaja en el grafo de autorizacion, no en la pantalla. Serializado porque los escenarios
 * cambian el perfil activo de una misma sesion.
 */
export default defineConfig({
  testDir: './escenarios',
  fullyParallel: false,
  workers: 1,
  forbidOnly: !!process.env.CI,
  retries: 0,
  reporter: [['list'], ['html', { outputFolder: 'informe', open: 'never' }]],
  use: { trace: 'retain-on-failure' },
});
