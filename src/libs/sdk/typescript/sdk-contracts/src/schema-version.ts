/**
 * Compile-time constants describing the AuthorizationGraph schema this SDK was built against.
 * See ADR-0074 for the versioning policy.
 */
export const SchemaVersion = {
  /**
   * Versión que declara una respuesta COMPLETA — la que recibe todo el que no envía `?include=`,
   * es decir todos los satélites de hoy.
   *
   * Se queda en 2.4.0 A PROPÓSITO: NO sube con la proyección. Subirla en todas las respuestas
   * parecía inocuo «porque los verificadores conocidos comparan RANGO», y no lo es. Un satélite
   * puede tener el esquema 2.4.0 vendorizado y validar contra su `const`, que compara por
   * IGUALDAD: rechazaría TODAS las respuestas sin haber pedido nunca una proyección. Y «los
   * verificadores conocidos» no es una propiedad del contrato, es un censo, y los censos caducan.
   */
  Current: '2.4.0',
  /**
   * Versión que declara una respuesta PROYECTADA (`?include=`, v2.5.0). Solo la ve quien pide una
   * proyección, y esa respuesta sí tiene una forma distinta de verdad: trae la clave `projection`
   * y le faltan bloques. La versión sube cuando la forma cambia, no cuando se despliega código.
   */
  Projected: '2.5.0',
  /**
   * La versión MÁS ALTA que este SDK entiende, y el techo contra el que comparan `isMajorMatch`,
   * `isMinorAhead` e `isMinorBehind`. Si el techo fuera `Current`, un grafo proyectado —que este
   * SDK lee sin problema— se reportaría como «servidor por delante del SDK», que es falso.
   */
  Latest: '2.5.0',
  CompatibilityMinInclusive: '2.0.0',
  CompatibilityMaxExclusive: '3.0.0'
} as const;

interface Semver {
  readonly major: number;
  readonly minor: number;
  readonly patch: number;
}

function tryParse(input: string | null | undefined): Semver | null {
  if (input === null || input === undefined) return null;
  const trimmed = input.trim();
  if (trimmed.length === 0) return null;
  const parts = trimmed.split('.');
  if (parts.length !== 3) return null;
  const major = Number.parseInt(parts[0]!, 10);
  const minor = Number.parseInt(parts[1]!, 10);
  const patch = Number.parseInt(parts[2]!, 10);
  if (!Number.isFinite(major) || !Number.isFinite(minor) || !Number.isFinite(patch)) return null;
  if (String(major) !== parts[0] || String(minor) !== parts[1] || String(patch) !== parts[2]) return null;
  return { major, minor, patch };
}

function compare(a: Semver, b: Semver): number {
  if (a.major !== b.major) return a.major - b.major;
  if (a.minor !== b.minor) return a.minor - b.minor;
  return a.patch - b.patch;
}

/** Returns true when `version` falls within the SDK's supported range. */
export function isSchemaVersionSupported(version: string | null | undefined): boolean {
  const v = tryParse(version);
  if (v === null) return false;
  const min = tryParse(SchemaVersion.CompatibilityMinInclusive)!;
  const max = tryParse(SchemaVersion.CompatibilityMaxExclusive)!;
  return compare(v, min) >= 0 && compare(v, max) < 0;
}

/** True when the MAJOR of `version` matches the SDK's current MAJOR. */
export function isMajorMatch(version: string | null | undefined): boolean {
  const v = tryParse(version);
  if (v === null) return false;
  const current = tryParse(SchemaVersion.Latest)!;
  return v.major === current.major;
}

/** True when `version` is a MINOR ahead of the current schema (server newer than SDK). */
export function isMinorAhead(version: string | null | undefined): boolean {
  const v = tryParse(version);
  if (v === null) return false;
  const current = tryParse(SchemaVersion.Latest)!;
  if (v.major !== current.major) return false;
  return v.minor > current.minor;
}

/** True when `version` is a MINOR behind the current schema (SDK newer than server). */
export function isMinorBehind(version: string | null | undefined): boolean {
  const v = tryParse(version);
  if (v === null) return false;
  const current = tryParse(SchemaVersion.Latest)!;
  if (v.major !== current.major) return false;
  return v.minor < current.minor;
}
