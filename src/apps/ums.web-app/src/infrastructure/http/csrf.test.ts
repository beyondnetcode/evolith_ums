import { describe, it, expect, beforeEach } from 'vitest';
import { getCsrfToken, refreshCsrfToken, CSRF_HEADER_NAME } from './csrf';

describe('csrf', () => {
  beforeEach(() => {
    document.cookie = '';
    document.querySelectorAll('meta').forEach(meta => meta.remove());
    refreshCsrfToken();
  });

  it('exports CSRF_HEADER_NAME', () => {
    expect(CSRF_HEADER_NAME).toBe('X-CSRF-Token');
  });

  it('returns null when no token is available', () => {
    expect(getCsrfToken()).toBeNull();
  });

  it('reads token from meta tag', () => {
    const meta = document.createElement('meta');
    meta.name = 'csrf-token';
    meta.content = 'meta-token-123';
    document.head.appendChild(meta);

    expect(getCsrfToken()).toBe('meta-token-123');
  });

  it('refreshes the token cache', () => {
    const meta = document.createElement('meta');
    meta.name = 'csrf-token';
    meta.content = 'old-token';
    document.head.appendChild(meta);

    expect(getCsrfToken()).toBe('old-token');

    meta.content = 'new-token';
    refreshCsrfToken();

    expect(getCsrfToken()).toBe('new-token');
  });
});
