import { defineConfig, devices } from '@playwright/test';

/**
 * Carril D — los 19 escenarios de autenticacion de la Fase 1, del Tablero SDLC contra UMS.
 *
 * VIVE AQUI, dentro del arnes, y no en un `tests/e2e/` propio. El plan original proponia ese arbol
 * nuevo y una comparativa de herramienta; evolith_arch#G-221 lo declaro trabajo duplicado —ADR-0109 y
 * este arnes ya resolvieron las dos cosas— y advirtio que un plan vigente que describe un problema
 * inexistente cuesta trabajo real. Construir el arbol paralelo habria sido exactamente el error que
 * ese gap documenta.
 *
 * QUE DEL PLAN NO SE IMPLEMENTA, y por que. Sus §3.1 y §8 tratan CORS, la CSP de UMS y
 * `SameSite=None` como riesgos a mitigar: NINGUNO aplica. El navegador nunca habla con UMS —la CSP
 * del Tablero fija `connect-src 'self'` y la llamada la hace `fetch` de Node, servidor a servidor—,
 * de modo que no hay escenario multi-origen que probar. Anadir el origen del Tablero a los origenes
 * permitidos de UMS abriria una superficie que la arquitectura cierra a proposito.
 *
 * SERIALIZADO. Hay UN backend compartido y varios escenarios tocan estado de sesion; en paralelo se
 * estorbarian, que es la misma razon por la que el carril A se serializa contra el mismo kind.
 */
/*
 * DOS ORIGENES, y confundirlos cuesta seis escenarios rojos —lo costo—. `tablero-app` sirve la API
 * en su NodePort; `tablero-web` sirve la SPA en el suyo y hace de proxy de `/api` hacia la primera.
 * El NAVEGADOR tiene que ir a la web: apuntarlo a la API le devuelve JSON donde espera HTML, y todo
 * escenario con pantalla falla por una razon que no es la que prueba.
 *
 * Las llamadas directas por `request` pueden ir a cualquiera de las dos; se usa la web para que
 * ambos caminos ejerzan el mismo proxy que usa una persona.
 */
const base = process.env.TABLERO_WEB_URL ?? 'http://localhost:8317';

export default defineConfig({
  testDir: './escenarios',
  fullyParallel: false,
  workers: 1,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 2 : 0,
  reporter: [['line'], ['html', { outputFolder: 'informe', open: 'never' }]],
  use: {
    baseURL: base,
    // `retain-on-failure`, no `on-first-retry`: en local `retries` es 0, asi que «al primer
    // reintento» significa nunca y un rojo raro se queda sin explicacion. Lo aprendio el carril A
    // (G-236) perdiendo la evidencia de su unico fallo.
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
    video: 'retain-on-failure',
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
});
