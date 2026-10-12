/**
 * El entorno de la Fase 2: autorizacion. A diferencia del carril D, aqui no hay navegador —
 * lo que se prueba es que UMS conceda a cada perfil lo suyo, y eso se ve en el grafo.
 */
export const UMS = (process.env.UMS_BASE_URL ?? 'http://localhost:8080').replace(/\/$/, '');
export const TENANT = process.env.UMS_TENANT ?? 'EVOLITH';
export const SISTEMA = process.env.UMS_SYSTEM ?? 'SDLC';

/** Contrasena determinista, derivada del correo, igual que en el carril D. */
export const claveDe = (correo: string): string =>
  `Sdlc.${correo.split('@')[0].split('.')[0].replace(/^./, (c) => c.toUpperCase())}.2026!`;

export const SUJETOS = {
  /**
   * El unico sujeto con DOS perfiles de rol DISTINTO en el mismo sistema, que es lo que hace
   * demostrable la autorizacion: AUDITOR (solo lectura) y DESARROLLADOR. Sin el, cualquier prueba
   * multi-perfil pasa en verde sin probar nada — es lo que G-209 denunciaba.
   *
   * Antes era AUDITOR + EQUIPO; EQUIPO desaparecio al alinear los roles del Tablero con el
   * organigrama, y DESARROLLADOR es su equivalente. Lo que importa no es el nombre sino que uno
   * escriba y el otro no: dos roles parecidos devolverian el mismo grafo y la prueba pasaria sin
   * demostrar nada.
   */
  multiPerfil: 'auditor.sdlc@evolith.example',
  /** Existe y es valido, pero NO tiene perfil en SDLC. */
  sinPerfilEnSistema: { usuario: 'admin@evolith.example', clave: 'Evolith.Dev.2026' },
} as const;

export type Grafo = {
  accessState: string;
  profiles: Array<{ id: string; role: { code: string }; isCurrent?: boolean }>;
  menuAccess: Array<{ code: string }>;
  scopes: string[];
};

export type Sesion = { token: string; grafo: Grafo };

const grafoDe = (cuerpo: { graph: string | object }): Grafo =>
  (typeof cuerpo.graph === 'string' ? JSON.parse(cuerpo.graph) : cuerpo.graph) as Grafo;

export async function autenticar(usuario: string, clave: string): Promise<Sesion> {
  const r = await fetch(`${UMS}/api/v1/client/authenticate`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ username: usuario, password: clave, tenantCode: TENANT, systemCode: SISTEMA }),
  });
  if (!r.ok) throw new Error(`authenticate ${usuario}: HTTP ${r.status}`);
  const cuerpo = (await r.json()) as { token: string; graph: string };
  return { token: cuerpo.token, grafo: grafoDe(cuerpo) };
}

export async function cambiarPerfil(token: string, profileId: string) {
  const r = await fetch(`${UMS}/api/v1/client/switch-profile`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${token}` },
    body: JSON.stringify({ profileId }),
  });
  const texto = await r.text();
  return { estado: r.status, cuerpo: texto ? JSON.parse(texto) : null };
}

/** Lee los claims del token sin verificar la firma: aqui se comprueba QUE declara, no si es valido. */
export function claims(token: string): Record<string, unknown> {
  const p = token.split('.')[1].replace(/-/g, '+').replace(/_/g, '/');
  return JSON.parse(Buffer.from(p + '='.repeat((4 - (p.length % 4)) % 4), 'base64').toString('utf8'));
}
