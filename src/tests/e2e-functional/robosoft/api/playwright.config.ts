// Arnés RoboSoft — carril B (nivel API, caja negra) · G-112
//
// Verificador de invariantes del PRD (INV-*) contra el backend VIVO de UMS en kind.
// A diferencia del carril A (Playwright UI, en src/apps/ums.web-app/tests), este proyecto
// NO abre navegador: usa exclusivamente el APIRequestContext de Playwright para ejercer el
// API REST y aseverar reglas de negocio, auto-aprovisionando su propia data.
//
// `E2E_BASE_URL` apunta al despliegue ya levantado (Ingress del clúster kind
// `evolith-cluster-ums`, por defecto http://localhost:8080). Este arnés NO levanta servidores.
import { defineConfig } from '@playwright/test';

const baseURL = process.env.E2E_BASE_URL ?? 'http://localhost:8080';

export default defineConfig({
  testDir: './tests',
  // Backend compartido: un único cluster. Correr en paralelo produce flakiness por
  // contención/estado. Se serializa (workers=1) para determinismo, igual que el carril A.
  fullyParallel: false,
  workers: 1,
  forbidOnly: !!process.env.CI,
  retries: 0,
  timeout: 60_000,
  reporter: [['list'], ['json', { outputFile: 'test-results/robosoft-api.json' }]],
  use: {
    baseURL,
    // Contexto de petición: sin navegador. Cada test resuelve identidad vía DevAuth/login.
    extraHTTPHeaders: {
      'Content-Type': 'application/json',
    },
  },
  projects: [
    {
      name: 'robosoft-api',
      testMatch: /.*\.spec\.ts/,
    },
  ],
});
