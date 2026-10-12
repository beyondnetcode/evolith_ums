/**
 * securityInterceptor.ts
 *
 * Production-grade security interceptor for API requests.
 * Implements OWASP recommendations:
 *
 * - HTTPS enforcement in production
 * - CSRF token handling
 * - Secure headers (CSP, X-Frame-Options, etc.)
 * - Request/response monitoring for security events
 * - Automatic logout on 401 responses
 * - Rate limiting feedback
 */
import { useAuthStore } from '@app/stores/auth.store';
import { randomUuid } from '@app/utils/security';

export const CSRF_TOKEN_KEY = 'ums_csrf_token' as const;

const DEFAULT_CSP =
  "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; font-src 'self'; connect-src 'self'; frame-ancestors 'none';";

const SECURITY_HEADERS = {
  'X-Content-Type-Options': 'nosniff',
  'X-Frame-Options': 'DENY',
  'X-XSS-Protection': '1; mode=block',
  'Referrer-Policy': 'strict-origin-when-cross-origin',
  'Permissions-Policy': 'geolocation=(), microphone=(), camera=()',
  'Content-Security-Policy': DEFAULT_CSP,
} as const;

export interface SecurityInterceptorConfig {
  baseUrl: string;
  onUnauthorized: () => void;
  onRateLimited: (retryAfter: number) => void;
  onServerError: () => void;
}

class SecurityInterceptor {
  private baseUrl: string;
  private onUnauthorized: () => void;
  private onRateLimited: (retryAfter: number) => void;
  private onServerError: () => void;
  private csrfToken: string | null = null;

  constructor(config: SecurityInterceptorConfig) {
    this.baseUrl = config.baseUrl;
    this.onUnauthorized = config.onUnauthorized;
    this.onRateLimited = config.onRateLimited;
    this.onServerError = config.onServerError;
  }

  private generateCsrfToken(): string {
    const array = new Uint8Array(32);
    crypto.getRandomValues(array);
    return Array.from(array, byte => byte.toString(16).padStart(2, '0')).join('');
  }

  initCsrfToken(): void {
    if (!this.csrfToken) {
      this.csrfToken = this.generateCsrfToken();
      this.storeCsrfToken(this.csrfToken);
    }
  }

  private storeCsrfToken(token: string): void {
    try {
      sessionStorage.setItem(CSRF_TOKEN_KEY, token);
    } catch {}
  }

  private getStoredCsrfToken(): string | null {
    try {
      return sessionStorage.getItem(CSRF_TOKEN_KEY);
    } catch {
      return null;
    }
  }

  getSecureHeaders(customHeaders?: Record<string, string>): HeadersInit {
    const headers: Record<string, string> = {
      'Content-Type': 'application/json',
      Accept: 'application/json',
      'X-Request-ID': randomUuid(),
      ...SECURITY_HEADERS,
      ...customHeaders,
    };

    const storedCsrf = this.getStoredCsrfToken();
    if (storedCsrf) {
      headers['X-CSRF-Token'] = storedCsrf;
    }

    const authState = useAuthStore.getState();
    if (authState.isAuthenticated && authState.user) {
      headers['X-Tenant-Id'] = authState.user.tenantId;
      if (authState.user.sessionTrackingId) {
        headers['X-Session-Tracking-Id'] = authState.user.sessionTrackingId;
      }
    }

    return headers;
  }

  handleResponseError(status: number, headers?: Headers): void {
    switch (status) {
      case 401:
      case 403:
        this.onUnauthorized();
        break;

      case 429:
        const retryAfter = headers?.get('Retry-After');
        const retryMs = retryAfter ? parseInt(retryAfter, 10) * 1000 : 60000;
        this.onRateLimited(retryMs);
        break;

      case 500:
      case 502:
      case 503:
        this.onServerError();
        break;
    }
  }

  validateUrl(url: string): boolean {
    try {
      const parsed = new URL(url, this.baseUrl);

      if (import.meta.env.PROD && parsed.protocol !== 'https:') {
        console.warn('Security: Insecure URL blocked in production');
        return false;
      }

      const allowedOrigins = [this.baseUrl];
      if (!allowedOrigins.some(origin => parsed.origin === origin)) {
        console.warn('Security: Unauthorized origin blocked');
        return false;
      }

      return true;
    } catch {
      console.warn('Security: Invalid URL format rejected');
      return false;
    }
  }
}

let securityInterceptor: SecurityInterceptor | null = null;

export function initSecurityInterceptor(config: SecurityInterceptorConfig): SecurityInterceptor {
  if (!securityInterceptor) {
    securityInterceptor = new SecurityInterceptor(config);
    securityInterceptor.initCsrfToken();
  }
  return securityInterceptor;
}

export function getSecurityInterceptor(): SecurityInterceptor | null {
  return securityInterceptor;
}

export function clearSecurityInterceptor(): void {
  securityInterceptor = null;
}

export { SecurityInterceptor };
export type { SecurityInterceptorConfig } from './securityInterceptor';
