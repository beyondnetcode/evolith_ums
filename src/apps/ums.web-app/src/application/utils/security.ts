const ALLOWED_PROTOCOLS = ['http:', 'https:'];

const PRIVATE_IP_PATTERNS = [
  /^https?:\/\/(?:127\.|10\.|192\.168\.|172\.(?:1[6-9]|2\d|3[01]))\./,
  /^https?:\/\/localhost(?:[:/]|$)/i,
  /^https?:\/\/(?:0\.0\.0\.0|::1|\[::1\])(?:[:/]|$)/,
  /^https?:\/\/169\.254\./,
];

export function isValidPublicUrl(url: string): boolean {
  try {
    const parsed = new URL(url);
    if (!ALLOWED_PROTOCOLS.includes(parsed.protocol)) return false;
    for (const pattern of PRIVATE_IP_PATTERNS) {
      if (pattern.test(url)) return false;
    }
    return true;
  } catch {
    return false;
  }
}

const DANGEROUS_CHARS = /[<>"'&]/g;
const CHAR_MAP: Record<string, string> = {
  '<': '&lt;',
  '>': '&gt;',
  '"': '&quot;',
  "'": '&#x27;',
  '&': '&amp;',
};

export function sanitizeInput(input: string): string {
  return input.replace(DANGEROUS_CHARS, char => CHAR_MAP[char] ?? char).trim();
}

export function sanitizeCode(input: string): string {
  return input.replace(/[^A-Z0-9_]/g, '').trim();
}

/**
 * Format a raw string into a standard technical system code.
 * Standardizes to uppercase, replaces whitespaces with underscores, and trims.
 */
export function formatSystemCode(input: string): string {
  return input.trim().toUpperCase().replace(/\s+/g, '_');
}

/**
 * Generate a RFC 4122 v4 UUID.
 *
 * `crypto.randomUUID` is only exposed in secure contexts — HTTPS, or HTTP on
 * `localhost`. Over plain HTTP on any other host the property is `undefined`
 * and calling it throws a `TypeError`, so a caller that builds request headers
 * with it fails *before* issuing the request. That is exactly what broke the
 * login form on `http://srvtms:8080` while it worked on `http://localhost:8080`.
 *
 * `crypto.getRandomValues` is NOT gated by secure context, so the fallback
 * keeps the same entropy — only the convenience wrapper is missing.
 */
export function randomUuid(): string {
  if (typeof crypto.randomUUID === 'function') {
    return crypto.randomUUID();
  }

  const bytes = new Uint8Array(16);
  crypto.getRandomValues(bytes);
  bytes[6] = (bytes[6] & 0x0f) | 0x40; // version 4
  bytes[8] = (bytes[8] & 0x3f) | 0x80; // variant 10x

  const hex = Array.from(bytes, byte => byte.toString(16).padStart(2, '0')).join('');
  return `${hex.slice(0, 8)}-${hex.slice(8, 12)}-${hex.slice(12, 16)}-${hex.slice(16, 20)}-${hex.slice(20)}`;
}
