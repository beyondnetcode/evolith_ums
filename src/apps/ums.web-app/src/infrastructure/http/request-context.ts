/**
 * request-context.ts — Shared request context for the HTTP client
 *
 * M-7: Centralizes request context for the httpClient.
 * L-6: BASE_URL configurable via env vars.
 */

export interface RequestContext {
  userId?: string;
  language?: 'en' | 'es';
  tenantId?: string;
}

export type RequestContextProvider = () => RequestContext;

let provider: RequestContextProvider = () => ({});

export const configureRequestContext = (p: RequestContextProvider) => {
  provider = p;
};

export const getRequestContext = (): RequestContext => provider();

export const BASE_URL = import.meta.env.VITE_API_BASE_PATH || '/api/v1';

// ADR-0071 / FS-26: el tenant por defecto en dev es EVOLITH, el Tenant Raíz / Admin Root.
// Con una sesión iniciada se usa `user.tenantId`; este valor es solo el fallback dev.
export const DEFAULT_TENANT_ID = '5f4e3d2c-1b0a-9f8e-7d6c-543210987654';
