/**
 * El entorno bajo prueba, resuelto por variable y verificado antes de afirmar nada.
 */
/**
 * El origen de la INTERFAZ, que es tambien el que hace proxy de `/api`. No es el de la API: son dos
 * servicios distintos —`tablero-web` y `tablero-app`— y apuntar el navegador al segundo devuelve
 * JSON donde el escenario espera una pantalla.
 */
export const TABLERO = (process.env.TABLERO_WEB_URL ?? 'http://localhost:8317').replace(/\/$/, '');

/**
 * Cuentas sembradas por `src/provisioning/cargar-en-ums.mjs`. La contrasena es DETERMINISTA
 * por usuario —se deriva del correo— para que la prueba no dependa de que nadie la anote en ningun
 * sitio, y para que no viaje en el repositorio.
 */
export const claveDe = (correo: string): string =>
  `Sdlc.${correo.split('@')[0].split('.')[0].replace(/^./, (c) => c.toUpperCase())}.2026!`;

export const USUARIOS = {
  valido: 'admin.sdlc.sdlc@evolith.example',
  auditor: 'auditor.sdlc@evolith.example',
  inexistente: 'no.existe.jamas@evolith.example',
} as const;

/**
 * La comprobacion sin la cual TODO lo demas es un falso positivo.
 *
 * Si el Tablero corre en modo dev-abierto —sin identidad configurada— no hay gate, no hay login y
 * los diecinueve escenarios pasarian en verde sin haber autenticado nada. Es el peor resultado
 * posible: una certificacion que afirma exactamente lo contrario de lo que ocurre. Por eso se exige
 * `configurado: true` antes del primer escenario, y no se «avisa»: se aborta.
 */
export async function exigirIdentidadConfigurada(): Promise<void> {
  const r = await fetch(`${TABLERO}/api/auth/estado`);
  const cuerpo = (await r.json()) as { configurado?: boolean };
  if (cuerpo.configurado !== true) {
    throw new Error(
      `El Tablero en ${TABLERO} responde {"configurado":false}: corre en modo dev-abierto, sin gate `
      + 'de identidad. Los 19 escenarios pasarian en verde sin autenticar nada. Configura '
      + '`UMS_BASE_URL` y `UMS_JWT_SECRET` antes de certificar.',
    );
  }
}

/** Entra por API y devuelve la cabecera `Cookie` de la sesion, sin pasar por la interfaz. */
export async function sesionPorApi(correo: string): Promise<string> {
  const r = await fetch(`${TABLERO}/api/auth/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ username: correo, password: claveDe(correo) }),
  });
  if (r.status !== 200) throw new Error(`login por API de ${correo}: HTTP ${r.status}`);
  return (r.headers.getSetCookie?.() ?? [])
    .map((c) => c.split(';')[0])
    .join('; ');
}
