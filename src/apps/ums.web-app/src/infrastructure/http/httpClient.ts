/**
 * httpClient.ts — Infrastructure HTTP abstraction
 *
 * Production-grade HTTP client with OWASP security recommendations:
 *
 * - Request/response interception for auth handling
 * - Automatic 401 redirection to login
 * - CSRF token injection for state-changing operations
 * - Tenant context propagation
 * - Security headers
 * - Timeout configuration
 * - Error normalization
 */
import axios, { type AxiosInstance, type InternalAxiosRequestConfig, type AxiosError } from 'axios';
import { BASE_URL, getRequestContext, DEFAULT_TENANT_ID } from './request-context';
import { getCsrfToken, CSRF_HEADER_NAME } from './csrf';
import { useAuthStore } from '@app/stores/auth.store';
import { randomUuid } from '@app/utils/security';

const REQUEST_TIMEOUT = 30000;
const RETRY_DELAY = 1000;

function createHttpClient(): AxiosInstance {
  const client = axios.create({
    baseURL: BASE_URL,
    headers: {
      'Content-Type': 'application/json',
      Accept: 'application/json',
      'X-Content-Type-Options': 'nosniff',
      'X-Frame-Options': 'DENY',
    },
    timeout: REQUEST_TIMEOUT,
    withCredentials: true,
  });

  client.interceptors.request.use(
    (config: InternalAxiosRequestConfig) => {
      const { userId, language, tenantId } = getRequestContext();

      if (userId) {
        config.headers.set('X-User-Id', userId);
      }

      if (language) {
        config.headers.set('X-Language', language);
      }

      // ADR-0076 D2: Send browser timezone so server-side operations (reports, emails) use it
      const sessionTimezone = useAuthStore.getState().user?.sessionParameters?.defaultTimezone;
      if (sessionTimezone) {
        config.headers.set('X-Timezone', sessionTimezone);
      }

      config.headers.set('X-Tenant-Id', tenantId || DEFAULT_TENANT_ID);
      config.headers.set('X-Request-ID', randomUuid());

      const csrfToken = getCsrfToken();
      const isSafeMethod =
        config.method && ['get', 'head', 'options', 'trace'].includes(config.method);
      if (csrfToken && !isSafeMethod) {
        config.headers.set(CSRF_HEADER_NAME, csrfToken);
      }

      return config;
    },
    error => {
      return Promise.reject(error);
    }
  );

  client.interceptors.response.use(
    response => response,
    async (error: AxiosError) => {
      const status: number = error.response?.status ?? 0;
      const originalRequest = error.config as InternalAxiosRequestConfig & { _retry?: boolean };

      switch (status) {
        case 401:
        case 403:
          if (!originalRequest._retry) {
            originalRequest._retry = true;

            const authState = useAuthStore.getState();
            if (authState.isAuthenticated) {
              authState.logout();

              if (typeof window !== 'undefined') {
                // Igual que en el cierre de sesión: navegación de página completa, así que
                // la ruta necesita el prefijo público del bundle (BASE_URL) para no salir
                // de la aplicación cuando se sirve bajo un subpath.
                window.location.href = `${import.meta.env.BASE_URL}login?showSessionExpired=true`;
              }
            }
          }
          return Promise.reject(
            Object.assign(error, {
              normalised: {
                message: 'Sesión expirada o no autorizada',
                status,
                code: 'AUTH_ERROR',
              },
            })
          );

        case 429:
          const retryAfter = error.response?.headers?.['retry-after'];
          const delay = retryAfter ? parseInt(retryAfter, 10) * 1000 : RETRY_DELAY * 5;

          return Promise.reject(
            Object.assign(error, {
              normalised: {
                message: `Demasiadas solicitudes. Intente en ${Math.ceil(delay / 1000)} segundos`,
                status,
                code: 'RATE_LIMITED',
              },
            })
          );

        case 500:
        case 502:
        case 503:
          return Promise.reject(
            Object.assign(error, {
              normalised: {
                message: 'Error del servidor. Intente más tarde',
                status,
                code: 'SERVER_ERROR',
              },
            })
          );

        default:
          const data = error.response?.data;
          const errorMessage =
            data && typeof data === 'object'
              ? 'message' in data
                ? (data as { message: string }).message
                : 'detail' in data
                  ? (data as { detail: string }).detail
                  : 'title' in data
                    ? (data as { title: string }).title
                    : JSON.stringify(data).slice(0, 200)
              : 'Error de comunicación';

          return Promise.reject(
            Object.assign(error, {
              normalised: {
                message: errorMessage,
                status,
                code: 'REQUEST_ERROR',
              },
            })
          );
      }
    }
  );

  return client;
}

export const httpClient = createHttpClient();

export function invalidateAndRetry(
  client: AxiosInstance,
  originalConfig: InternalAxiosRequestConfig
): Promise<unknown> {
  return new Promise((resolve, reject) => {
    setTimeout(() => {
      if (originalConfig._retry) {
        reject(new Error('Max retries exceeded'));
        return;
      }

      originalConfig._retry = true;
      client.request(originalConfig).then(resolve).catch(reject);
    }, RETRY_DELAY);
  });
}
