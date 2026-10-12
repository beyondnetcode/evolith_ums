import http from 'k6/http';
import { check, sleep } from 'k6';

// Configuración de la prueba de rendimiento
export const options = {
  stages: [
    { duration: '10s', target: 5 }, // Rampa de subida a 5 usuarios concurrentes
    { duration: '20s', target: 5 }, // Mantener 5 usuarios
    { duration: '10s', target: 0 }, // Rampa de bajada
  ],
  thresholds: {
    http_req_duration: ['p(95)<1000'], // El 95% de las peticiones debe ser menor a 1s
    http_req_failed: ['rate<0.01'],   // Menos del 1% de fallos permitidos
  },
};

const BASE_URL = 'http://localhost:5293/api/v1'; // Ajustado al puerto por defecto del Backend API si corre en http

export default function () {
  // 1. Prueba de Login Interno (Auth Endpoint)
  const loginPayload = JSON.stringify({
    tenantCode: 'INTERNAL_ADMIN',
    username: 'admin@ums.local',
    password: 'Admin@123',
    rememberMe: false
  });

  const loginParams = {
    headers: {
      'Content-Type': 'application/json',
      'X-Disable-Dev-Auth': 'true' // Para asegurar que pasa por el pipeline de auth real
    },
  };

  const loginRes = http.post(`${BASE_URL}/auth/login`, loginPayload, loginParams);

  if (loginRes.status !== 200) {
    console.error(`Login failed with status ${loginRes.status}: ${loginRes.body}`);
  }

  check(loginRes, {
    'login status is 200': (r) => r.status === 200,
    'login has session cookie': (r) => r.headers['Set-Cookie'] !== undefined,
  });

  // 2. Prueba de Autenticación de Cliente Externo (Graph)
  // Esta prueba es más pesada porque devuelve el Grafo de Autorización serializado
  const clientAuthPayload = JSON.stringify({
    tenantCode: 'INTERNAL_ADMIN',
    username: 'admin@ums.local',
    password: 'Admin@123',
    format: 'JSON'
  });

  const clientAuthRes = http.post(`${BASE_URL}/client/authenticate`, clientAuthPayload, loginParams);

  if (clientAuthRes.status !== 200) {
    console.error(`Client auth failed with status ${clientAuthRes.status}: ${clientAuthRes.body}`);
  }

  check(clientAuthRes, {
    'client auth status is 200': (r) => r.status === 200,
    'client auth returns graph': (r) => r.body && r.body.includes('graph'),
  });

  // 3. Prueba de Refresh Token
  // Usamos la cookie obtenida en el login interno para solicitar un refresh
  const cookies = loginRes.headers['Set-Cookie'];
  let refreshParams = { ...loginParams };

  if (cookies) {
    refreshParams.headers['Cookie'] = typeof cookies === 'string' ? cookies : cookies.join('; ');

    // Obtenemos el xsrf token si viene en las cookies (como simplificación enviamos vacío si no lo parseamos)
    refreshParams.headers['X-XSRF-TOKEN'] = 'dummy-for-load-test';

    const refreshRes = http.post(`${BASE_URL}/auth/refresh`, null, refreshParams);

    if (refreshRes.status !== 200) {
      console.error(`Refresh failed with status ${refreshRes.status}: ${refreshRes.body}`);
    }

    check(refreshRes, {
      'refresh status is 200': (r) => r.status === 200,
      'refresh sets new cookie': (r) => r.headers['Set-Cookie'] !== undefined,
    });
  }

  // 4. Prueba de carga sobre la Web App (Frontend Vite Server)
  // Aunque es un dev server local, verificamos que sirva el index.html rápidamente
  const webRes = http.get('http://[::1]:5173/login?tenantCode=INTERNAL_ADMIN');

  if (webRes.status !== 200) {
    console.error(`Web app failed with status ${webRes.status}: ${webRes.error || webRes.body}`);
  }

  check(webRes, {
    'web app responds': (r) => r.status === 200,
  });

  // Pausa entre iteraciones
  sleep(1);
}
