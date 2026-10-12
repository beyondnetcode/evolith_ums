/**
 * csrf.ts — CSRF token management for state-changing requests.
 *
 * Reads the CSRF token from a meta tag (set by the server) or from
 * a cookie. The token is injected into the X-CSRF-Token header for
 * all non-GET requests.
 *
 * Double-submit cookie pattern:
 *   1. Server sets XSRF-TOKEN cookie on initial page load
 *   2. Client reads cookie and sends it as X-CSRF-Token header
 *   3. Server validates header matches cookie value
 */

const CSRF_COOKIE_NAME = 'XSRF-TOKEN';
const CSRF_HEADER_NAME = 'X-CSRF-Token';
const CSRF_META_NAME = 'csrf-token';

let cachedToken: string | null = null;

function getCookie(name: string): string | null {
  const match = document.cookie.match(new RegExp(`(^| )${name}=([^;]+)`));
  return match?.[2] ?? null;
}

function getMetaToken(): string | null {
  const meta = document.querySelector<HTMLMetaElement>(`meta[name="${CSRF_META_NAME}"]`);
  return meta?.content ?? null;
}

export function getCsrfToken(): string | null {
  if (cachedToken) return cachedToken;

  cachedToken = getCookie(CSRF_COOKIE_NAME) ?? getMetaToken();
  return cachedToken;
}

export function refreshCsrfToken(): void {
  cachedToken = null;
  getCsrfToken();
}

export { CSRF_HEADER_NAME };
