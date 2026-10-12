import { createHmac, timingSafeEqual, verify as verificarAsimetrica, constants } from 'node:crypto';
import { CacheDeJwks, uriDeJwks, type OpcionesDeJwks } from './jwks.js';

/**
 * Verificación de la firma del portador emitido por UMS.
 *
 * Existe porque los SDK NO la hacían. `sdk-express` y el de .NET partían el token por los puntos,
 * descodificaban el trozo del medio y usaban su contenido —incluido el grafo de autorización— sin
 * mirar la firma. Cualquiera que supiera fabricar un base64url con la forma esperada obtenía el
 * grafo que quisiera: ni siquiera hacía falta conocer el secreto (G-217).
 *
 * Un SDK existe precisamente para que un equipo no tenga que resolver esto por su cuenta. Cuando el
 * SDK no verifica, cada satélite que lo instala hereda el agujero sin haber hecho nada mal.
 *
 * Vive aquí y no en cada SDK a propósito: repetida cuatro veces, la comprobación se desincroniza, y
 * la copia que se quede atrás será la que nadie mire.
 *
 * ── RS256 ────────────────────────────────────────────────────────────────────────────────────────
 * UMS pasó a firmar con clave privada RSA y a publicar la pública en su JWKS (ADR-0157). La
 * diferencia con HS256 no es de robustez del algoritmo sino de **quién puede emitir**: con un
 * secreto compartido, verificar y firmar son la misma capacidad, así que todo satélite que valida
 * es también un emisor y ninguna firma identifica su origen.
 *
 * Este verificador nació exigiendo HS256 de forma literal, y esa exigencia —correcta contra la
 * confusión de algoritmo— es la que empezó a rechazar los portadores legítimos en RS256. La salida
 * NO es aceptar lo que el token declare: es que el consumidor diga qué acepta y que la clave, no el
 * token, determine con qué se comprueba.
 */

/** El resultado dice POR QUÉ se rechaza. Un booleano obligaría a cada SDK a inventarse el motivo. */
export type ResultadoDeVerificacion =
  | { readonly valido: true; readonly cuerpo: Record<string, unknown> }
  | { readonly valido: false; readonly motivo: MotivoDeRechazo };

export type MotivoDeRechazo =
  | 'FORMATO'            // no son tres segmentos separados por punto
  | 'ALGORITMO'          // el algoritmo del token no está entre los que el consumidor acepta
  | 'FIRMA'              // la firma no corresponde al contenido
  | 'CLAVE_DESCONOCIDA'  // el `kid` no está en el JWKS del emisor
  | 'JWKS_INACCESIBLE'   // no se pudo consultar el JWKS y no hay copia previa
  | 'CUERPO_ILEGIBLE';   // el segmento del medio no es JSON

const decodificar = (segmento: string): Buffer => {
  const normalizado = segmento.replace(/-/g, '+').replace(/_/g, '/');
  const relleno = normalizado.length % 4;
  if (relleno === 1) throw new Error('longitud base64url inválida');
  return Buffer.from(normalizado + '='.repeat(relleno ? 4 - relleno : 0), 'base64');
};

interface CabeceraJwt {
  readonly alg?: unknown;
  readonly kid?: unknown;
}

/**
 * Qué acepta el consumidor. Es configuración suya, nunca del token.
 *
 * Declarar solo `secretoHs256` acepta solo HS256; declarar solo `jwks` acepta solo RS256; declarar
 * ambos acepta ambos, y existe únicamente para que un despliegue pueda cruzar de uno a otro sin
 * corte. Lo que no puede pasar es que aceptar los dos permita DEGRADAR: ver el apunte sobre
 * `algoritmosAceptados` más abajo.
 */
export interface OpcionesDeVerificacion {
  /** Secreto compartido para HS256. Su sola presencia habilita ese algoritmo. */
  readonly secretoHs256?: string;
  /** Caché del JWKS del emisor. Su sola presencia habilita RS256. */
  readonly jwks?: CacheDeJwks;
}

/**
 * Comprueba que el token lo firmó quien dice, y devuelve su cuerpo solo si es así.
 *
 * Es asíncrona porque RS256 puede tener que consultar el JWKS del emisor. No se ha intentado
 * disimularlo tras una fachada síncrona: la única manera de hacerlo sería bloquear el bucle de
 * eventos o verificar contra una caché que todavía no tiene la clave, y las dos son peores que
 * propagar el `await` a los middlewares, que ya viven en un contexto asíncrono.
 */
export async function verificarFirma(
  token: string,
  opciones: OpcionesDeVerificacion
): Promise<ResultadoDeVerificacion> {
  const aceptaHs256 = typeof opciones.secretoHs256 === 'string' && opciones.secretoHs256.length > 0;
  const aceptaRs256 = opciones.jwks !== undefined;
  if (!aceptaHs256 && !aceptaRs256) {
    // Sin nada configurado no se puede verificar nada. Se rechaza en vez de dejar pasar: es
    // justamente la omisión que produjo G-217.
    return { valido: false, motivo: 'FIRMA' };
  }

  const partes = token.split('.');
  if (partes.length !== 3) return { valido: false, motivo: 'FORMATO' };
  const [cabeceraB64, cuerpoB64, firmaB64] = partes as [string, string, string];

  let cabecera: CabeceraJwt;
  try {
    cabecera = JSON.parse(decodificar(cabeceraB64).toString('utf-8')) as CabeceraJwt;
  } catch {
    return { valido: false, motivo: 'FORMATO' };
  }

  const alg = typeof cabecera.alg === 'string' ? cabecera.alg : '';

  // `none` se rechaza aquí, explícitamente y antes que nada. Caería igual por no estar entre los
  // algoritmos aceptados, pero se deja escrito: es LA manera de desactivar la firma sin tocarla, y
  // debe verse en el código que está contemplada, no deducirse de que otra rama la cubre.
  if (alg === 'none' || alg === '') return { valido: false, motivo: 'ALGORITMO' };

  // El algoritmo del token NO decide cómo se comprueba: solo se usa para elegir entre las vías que
  // el consumidor habilitó. Un satélite configurado únicamente para RS256 rechaza un token HS256
  // aunque el token insista en que es HS256 —esa es la degradación que no puede ocurrir, y por eso
  // la condición mira la configuración y no la cabecera—.
  const contenidoFirmado = `${cabeceraB64}.${cuerpoB64}`;

  if (alg === 'RS256') {
    if (!aceptaRs256) return { valido: false, motivo: 'ALGORITMO' };
    const resultado = await comprobarRs256(cabecera, contenidoFirmado, firmaB64, opciones.jwks!);
    if (resultado !== null) return resultado;
  } else if (alg === 'HS256') {
    if (!aceptaHs256) return { valido: false, motivo: 'ALGORITMO' };
    const resultado = comprobarHs256(contenidoFirmado, firmaB64, opciones.secretoHs256!);
    if (resultado !== null) return resultado;
  } else {
    return { valido: false, motivo: 'ALGORITMO' };
  }

  try {
    return {
      valido: true,
      cuerpo: JSON.parse(decodificar(cuerpoB64).toString('utf-8')) as Record<string, unknown>,
    };
  } catch {
    return { valido: false, motivo: 'CUERPO_ILEGIBLE' };
  }
}

/** Devuelve el rechazo, o `null` si la firma es buena. */
async function comprobarRs256(
  cabecera: CabeceraJwt,
  contenidoFirmado: string,
  firmaB64: string,
  jwks: CacheDeJwks
): Promise<ResultadoDeVerificacion | null> {
  // Sin `kid` no se verifica. La alternativa sería probar contra todas las claves publicadas, y eso
  // convierte cada rotación en una ventana donde la clave retirada sigue valiendo.
  const kid = typeof cabecera.kid === 'string' ? cabecera.kid : '';
  if (!kid) return { valido: false, motivo: 'CLAVE_DESCONOCIDA' };

  const buscada = await jwks.clavePara(kid);
  if (buscada.estado === 'DESCONOCIDA') return { valido: false, motivo: 'CLAVE_DESCONOCIDA' };
  if (buscada.estado === 'INACCESIBLE') return { valido: false, motivo: 'JWKS_INACCESIBLE' };

  // Lo que manda es el algoritmo que declara LA CLAVE. Si el JWKS dijera que es una clave para otra
  // cosa, no se verifica «igualmente» con RS256 porque el token lo pida.
  if (buscada.algoritmo !== 'RS256') return { valido: false, motivo: 'ALGORITMO' };

  let firma: Buffer;
  try {
    firma = decodificar(firmaB64);
  } catch {
    return { valido: false, motivo: 'FIRMA' };
  }

  let correcta: boolean;
  try {
    correcta = verificarAsimetrica(
      'sha256',
      Buffer.from(contenidoFirmado, 'ascii'),
      { key: buscada.clave, padding: constants.RSA_PKCS1_PADDING },
      firma
    );
  } catch {
    return { valido: false, motivo: 'FIRMA' };
  }
  return correcta ? null : { valido: false, motivo: 'FIRMA' };
}

/** Devuelve el rechazo, o `null` si la firma es buena. */
function comprobarHs256(
  contenidoFirmado: string,
  firmaB64: string,
  secreto: string
): ResultadoDeVerificacion | null {
  const esperada = createHmac('sha256', secreto).update(contenidoFirmado).digest();
  let recibida: Buffer;
  try {
    recibida = decodificar(firmaB64);
  } catch {
    return { valido: false, motivo: 'FIRMA' };
  }
  // La comparación es en tiempo constante: hacerla con `===` filtra, por el tiempo que tarda en
  // fallar, cuántos bytes iniciales acertó quien lo intenta.
  if (recibida.length !== esperada.length) return { valido: false, motivo: 'FIRMA' };
  if (!timingSafeEqual(recibida, esperada)) return { valido: false, motivo: 'FIRMA' };
  return null;
}

/**
 * Verificación HS256 síncrona.
 *
 * Se mantiene porque hay despliegues y arneses de prueba que todavía firman con secreto compartido
 * y no pueden cruzar a RS256 el mismo día. No admite RS256 —no puede: consultar el JWKS es
 * asíncrono— así que un consumidor que la use seguirá rechazando los portadores nuevos. Para
 * verificar los dos, `verificarFirma`.
 *
 * @deprecated Usa `verificarFirma`, que cubre RS256 (ADR-0157) además de HS256.
 */
export function verificarFirmaHs256(token: string, secreto: string): ResultadoDeVerificacion {
  if (!secreto) return { valido: false, motivo: 'FIRMA' };

  const partes = token.split('.');
  if (partes.length !== 3) return { valido: false, motivo: 'FORMATO' };
  const [cabeceraB64, cuerpoB64, firmaB64] = partes as [string, string, string];

  let cabecera: CabeceraJwt;
  try {
    cabecera = JSON.parse(decodificar(cabeceraB64).toString('utf-8')) as CabeceraJwt;
  } catch {
    return { valido: false, motivo: 'FORMATO' };
  }
  if (cabecera.alg !== 'HS256') return { valido: false, motivo: 'ALGORITMO' };

  const rechazo = comprobarHs256(`${cabeceraB64}.${cuerpoB64}`, firmaB64, secreto);
  if (rechazo !== null) return rechazo;

  try {
    return {
      valido: true,
      cuerpo: JSON.parse(decodificar(cuerpoB64).toString('utf-8')) as Record<string, unknown>,
    };
  } catch {
    return { valido: false, motivo: 'CUERPO_ILEGIBLE' };
  }
}

/**
 * Arma las opciones de verificación a partir de lo que declara un consumidor.
 *
 * Concentra aquí la regla de convivencia para que los tres SDK no la reimplementen cada uno a su
 * manera —que es como una acaba siendo más laxa que las otras—.
 */
export function opcionesDeVerificacion(config: {
  readonly signingSecret?: string;
  readonly umsBaseUrl?: string;
  readonly jwksUri?: string;
  readonly jwks?: Partial<OpcionesDeJwks>;
}): OpcionesDeVerificacion {
  const uri = config.jwksUri ?? (config.umsBaseUrl ? uriDeJwks(config.umsBaseUrl) : undefined);
  return {
    secretoHs256: config.signingSecret,
    jwks: uri ? new CacheDeJwks({ ...config.jwks, uri }) : undefined,
  };
}
