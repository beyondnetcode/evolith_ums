import { defineConfig, devices } from '@playwright/test';

/**
 * `E2E_BASE_URL` apunta la suite a un despliegue ya levantado (p. ej. el
 * Ingress del clúster kind `evolith-cluster-ums` en http://localhost:8080).
 * Sin ella se conserva el comportamiento por defecto: levantar el dev server
 * de vite. Cuando la app ya está desplegada NO se arranca `webServer`.
 */
const baseURL = process.env.E2E_BASE_URL ?? 'http://localhost:5173';
const usesExternalDeployment = !!process.env.E2E_BASE_URL;

export default defineConfig({
  testDir: './tests',
  // G-112 (certificación): contra un despliegue externo hay UN backend compartido; correr en
  // paralelo produce flakiness por contención/estado entre workers (creación de suites, navegación,
  // drawer). Se serializa (workers=1, sin fullyParallel) para determinismo, igual que el arnés
  // RoboSoft se serializa contra el mismo kind. En dev local (webServer propio) se conserva el paralelo.
  fullyParallel: !usesExternalDeployment,
  forbidOnly: !!process.env.CI,
  /*
   * Un reintento TAMBIEN en local, y no para maquillar: para NOMBRAR.
   *
   * Con `retries: 0`, un fallo intermitente solo puede acabar en dos sitios, y los dos son malos:
   * en rojo —y quien reejecuta lo ve pasar, aprende a reejecutar, y el dia que el fallo sea real
   * nadie lo mira—, o desaparecido, que fue lo que ocurrio el 2026-08-03: el carril A fallo una
   * vez, paso las cinco siguientes, y no quedo constancia de que hubiera fallado.
   *
   * Con `retries: 1`, Playwright distingue tres estados en vez de dos: `passed`, `failed` (fallo
   * las dos veces) y `flaky` (fallo y luego paso). El intermitente deja de ser un fantasma y pasa
   * a ser una linea contada en el informe, con su traza. No se oculta ningun fallo real: uno que
   * falla dos veces sigue saliendo rojo.
   */
  retries: process.env.CI ? 2 : 1,
  workers: usesExternalDeployment || process.env.CI ? 1 : undefined,
  reporter: 'html',
  use: {
    baseURL,
    // `retain-on-failure`, NO `on-first-retry`: se conserva la traza de CUALQUIER fallo, no solo
    // la del reintento. Playwright limpia `test-results/` al arrancar, asi que la evidencia de una
    // corrida no sobrevive a la siguiente: `certify-e2e.sh` la archiva antes de volver a correr.
    //
    // Se pago el 2026-08-03: el carril A salio ROJO una vez en la certificacion y verde en las seis
    // corridas siguientes. Al ir a diagnosticarlo no quedaba ni traza, ni captura, ni video —solo
    // el codigo de salida— y el fallo quedo sin explicar. Una suite que no conserva la prueba de su
    // propio fallo convierte cada rojo raro en una anecdota.
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
    video: 'retain-on-failure',
  },
  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] },
    },
  ],
  ...(usesExternalDeployment
    ? {}
    : {
        webServer: {
          command: 'npm run dev',
          url: baseURL,
          reuseExistingServer: !process.env.CI,
          timeout: 120000,
        },
      }),
});
