import { createPublicKey, type KeyObject } from 'node:crypto';

/**
 * Caché del JWKS del emisor: las claves públicas con las que se verifica un portador RS256.
 *
 * Existe por dos razones que tiran en direcciones opuestas y hay que conciliar:
 *
 * 1. **No se puede ir a la red en cada verificación.** La verificación está en la ruta caliente de
 *    cada petición de cada satélite; un GET al JWKS por token convertiría al emisor en el cuello de
 *    botella de todo el ecosistema, y en su punto único de fallo.
 * 2. **Las claves rotan.** Si la caché fuera eterna, una rotación dejaría a todos los satélites
 *    rechazando tokens legítimos hasta que alguien los reiniciara. ADR-0157 §4.4 permite el solape
 *    justamente para que la rotación no sea un corte, pero el solape solo sirve si el consumidor
 *    va a buscar la clave nueva cuando la ve.
 *
 * La conciliación es traer el JWKS cuando aparece un `kid` que no se conoce. Y ahí está el riesgo
 * que este archivo existe para acotar: **ese refresco lo dispara un desconocido**, porque el `kid`
 * viaja en la cabecera de un token sin verificar. Sin límite, basta con mandar tokens con `kid`
 * inventados —uno distinto cada vez— para que el satélite bombardee al emisor. Es un amplificador
 * de denegación de servicio construido con nuestras propias manos.
 *
 * Por eso el refresco está acotado por tres mecanismos que se complementan:
 *
 * - **Cadencia mínima entre refrescos** (`esperaEntreRefrescosMs`): como mucho una petición al JWKS
 *   por ventana, contando TODOS los `kid` desconocidos juntos. Diez mil `kid` inventados no
 *   producen diez mil peticiones: producen una por ventana, y el resto se rechaza sin tocar la red.
 * - **Memoria de lo ya descartado** (`kidsDescartados`): un `kid` que ya se buscó y no estaba se
 *   recuerda, y el siguiente token que lo traiga se rechaza sin consumir siquiera la cadencia. Sin
 *   esto, un atacante que repitiera un único `kid` falso se comería el refresco de cada ventana y
 *   dejaría sin él a una rotación legítima.
 * - **Petición en vuelo compartida** (`enVuelo`): N verificaciones concurrentes con el mismo `kid`
 *   nuevo hacen UNA petición, no N. Sin esto, la ráfaga de arranque de un satélite con tráfico
 *   —justo cuando la caché está vacía— sale entera a la red a la vez.
 */

/** Una clave del JWKS, ya convertida a material utilizable. */
interface ClavePublica {
  readonly clave: KeyObject;
  /** El algoritmo que declara la PROPIA clave, no el que declare el token. */
  readonly algoritmo: string;
}

/** El resultado distingue «no está» de «no pude mirar»: llevan a rechazos distintos. */
export type ResultadoDeClave =
  | { readonly estado: 'OK'; readonly clave: KeyObject; readonly algoritmo: string }
  | { readonly estado: 'DESCONOCIDA' }
  | { readonly estado: 'INACCESIBLE' };

export interface OpcionesDeJwks {
  /** URI absoluta del JWKS del emisor. Ej.: `https://ums/.well-known/jwks.json`. */
  readonly uri: string;
  /** Implementación de `fetch`. Se inyecta para poder probar sin red. */
  readonly fetchImpl?: typeof fetch;
  /** Cuánto se considera fresca la copia. Por defecto 10 min, el `max-age` que publica UMS. */
  readonly ttlMs?: number;
  /** Cadencia mínima entre refrescos disparados por un `kid` desconocido. Por defecto 60 s. */
  readonly esperaEntreRefrescosMs?: number;
  /** Cuánto se recuerda un `kid` que no estaba en el JWKS. Por defecto 10 min. */
  readonly memoriaDeDescartadosMs?: number;
  /** Tiempo máximo de la petición al JWKS. Por defecto 5 s. */
  readonly tiempoDeEsperaMs?: number;
  /** Tamaño máximo aceptado del documento. Por defecto 512 KiB. */
  readonly maxBytes?: number;
  /** Reloj inyectable, para que las pruebas no dependan del paso real del tiempo. */
  readonly reloj?: () => number;
}

const TTL_POR_DEFECTO = 600_000;
const ESPERA_ENTRE_REFRESCOS_POR_DEFECTO = 60_000;
const MEMORIA_DE_DESCARTADOS_POR_DEFECTO = 600_000;
const TIEMPO_DE_ESPERA_POR_DEFECTO = 5_000;
const MAX_BYTES_POR_DEFECTO = 512 * 1024;

/**
 * Tamaño mínimo de clave RSA aceptado, en bytes del módulo (2048 bits).
 *
 * Es el mismo suelo que UMS se impone al arrancar (ADR-0157 §4.7). Se repite aquí porque el
 * consumidor no puede dar por hecho que el otro extremo lo cumple: una clave corta verifica sin
 * error y sin aviso, y el sistema queda débil sin que nada falle.
 */
const BYTES_MINIMOS_DE_MODULO = 256;

export class CacheDeJwks {
  private readonly uri: string;
  private readonly fetchImpl: typeof fetch;
  private readonly ttlMs: number;
  private readonly esperaEntreRefrescosMs: number;
  private readonly memoriaDeDescartadosMs: number;
  private readonly tiempoDeEsperaMs: number;
  private readonly maxBytes: number;
  private readonly reloj: () => number;

  private claves = new Map<string, ClavePublica>();
  /** Momento en que se completó el último refresco con éxito. 0 = nunca. */
  private traidoEn = 0;
  /**
   * Momento del último refresco disparado por un `kid` desconocido teniendo la copia fresca.
   *
   * Se lleva aparte del refresco por caducidad a propósito, y es la corrección de un error que
   * costó entender: si un único contador gobernara los dos, el primer refresco del arranque
   * —programado, nuestro, inevitable— gastaría el presupuesto del siguiente, y una rotación
   * legítima se quedaría esperando la ventana entera sin motivo. El presupuesto que hay que
   * racionar es solo el que puede disparar un desconocido.
   */
  private ultimoRefrescoPorKid = 0;
  /** Momento del último intento FALLIDO. Impide martillear un emisor caído. */
  private ultimoFallo = 0;
  private readonly kidsDescartados = new Map<string, number>();
  private enVuelo: Promise<boolean> | null = null;

  constructor(opciones: OpcionesDeJwks) {
    if (!opciones.uri) throw new Error('CacheDeJwks exige la URI del JWKS del emisor.');
    this.uri = opciones.uri;
    const fetchElegido = opciones.fetchImpl ?? globalThis.fetch;
    if (!fetchElegido) {
      throw new Error('No hay implementación de fetch disponible — pásala en `fetchImpl`.');
    }
    this.fetchImpl = fetchElegido;
    this.ttlMs = opciones.ttlMs ?? TTL_POR_DEFECTO;
    this.esperaEntreRefrescosMs = opciones.esperaEntreRefrescosMs ?? ESPERA_ENTRE_REFRESCOS_POR_DEFECTO;
    this.memoriaDeDescartadosMs = opciones.memoriaDeDescartadosMs ?? MEMORIA_DE_DESCARTADOS_POR_DEFECTO;
    this.tiempoDeEsperaMs = opciones.tiempoDeEsperaMs ?? TIEMPO_DE_ESPERA_POR_DEFECTO;
    this.maxBytes = opciones.maxBytes ?? MAX_BYTES_POR_DEFECTO;
    this.reloj = opciones.reloj ?? Date.now;
  }

  /**
   * Devuelve la clave de ese `kid`, trayendo el JWKS solo si hace falta y solo si toca.
   *
   * Nunca devuelve «alguna» clave: sin `kid` que case, se rechaza. Probar el token contra todas las
   * claves disponibles parece inofensivo —al fin y al cabo, o firma o no firma— pero convierte cada
   * rotación en una ventana en la que una clave retirada sigue siendo válida, y borra la única
   * señal que dice con qué clave se emitió realmente.
   */
  async clavePara(kid: string): Promise<ResultadoDeClave> {
    if (!kid) return { estado: 'DESCONOCIDA' };

    const ahora = this.reloj();

    // Lo ya descartado se responde sin red y sin gastar la cadencia. Es lo que impide que un
    // `kid` falso repetido monopolice el refresco que necesita una rotación de verdad.
    const descartadoEn = this.kidsDescartados.get(kid);
    if (descartadoEn !== undefined && ahora - descartadoEn < this.memoriaDeDescartadosMs) {
      return { estado: 'DESCONOCIDA' };
    }

    const enCache = this.claves.get(kid);
    const copiaFresca = this.traidoEn > 0 && ahora - this.traidoEn < this.ttlMs;
    if (enCache && copiaFresca) {
      return { estado: 'OK', clave: enCache.clave, algoritmo: enCache.algoritmo };
    }

    // Hay que ir a la red, y el motivo cambia radicalmente quién está al mando:
    //
    // - Copia fría o caducada: lo dispara el reloj, no quien llama. Como mucho una vez por TTL, y
    //   nadie de fuera puede acelerar ese reloj.
    // - Copia fresca pero `kid` sin conocer: lo dispara la cabecera de un token SIN VERIFICAR. Es
    //   el caso que hay que racionar, y el único.
    const loDisparaUnDesconocido = copiaFresca;
    const refrescado = await this.refrescar(ahora, loDisparaUnDesconocido);

    const trasRefresco = this.claves.get(kid);
    if (trasRefresco) {
      return { estado: 'OK', clave: trasRefresco.clave, algoritmo: trasRefresco.algoritmo };
    }

    if (refrescado) {
      // Se miró de verdad y no estaba: se recuerda, para no volver a mirar por este mismo `kid`.
      this.kidsDescartados.set(kid, this.reloj());
      this.podarDescartados();
      return { estado: 'DESCONOCIDA' };
    }

    // No se pudo mirar: o la cadencia lo frenó, o el emisor no respondió.
    if (enCache) {
      // Copia caducada y refresco frenado: se sirve lo viejo antes que rechazar un token legítimo
      // por un problema de red. La clave sigue siendo del emisor; solo está sin revalidar.
      return { estado: 'OK', clave: enCache.clave, algoritmo: enCache.algoritmo };
    }
    return this.traidoEn > 0 ? { estado: 'DESCONOCIDA' } : { estado: 'INACCESIBLE' };
  }

  /**
   * Trae el JWKS si toca. Devuelve si la copia quedó actualizada.
   *
   * @param loDisparaUnDesconocido cuando es cierto se aplica la cadencia, porque el refresco lo
   * provocó un `kid` que venía en un token sin verificar. Cuando es falso, el refresco es
   * programado —caché fría o TTL cumplido— y solo lo frena el cortafuegos de fallos.
   */
  private async refrescar(ahora: number, loDisparaUnDesconocido: boolean): Promise<boolean> {
    // Una sola petición en vuelo: N verificaciones concurrentes con la caché fría deben producir
    // UNA petición, no N. Es el caso del arranque de un satélite con tráfico encima.
    if (this.enVuelo) return this.enVuelo;

    if (loDisparaUnDesconocido
      && this.ultimoRefrescoPorKid > 0
      && ahora - this.ultimoRefrescoPorKid < this.esperaEntreRefrescosMs) {
      return false;
    }

    // Si el emisor acaba de fallar, no se insiste. Reintentar sin freno contra algo caído es
    // exactamente cuando peor sienta, y convierte una caída ajena en una propia.
    if (this.ultimoFallo > 0 && ahora - this.ultimoFallo < this.esperaEntreRefrescosMs) {
      return false;
    }

    if (loDisparaUnDesconocido) this.ultimoRefrescoPorKid = ahora;

    this.enVuelo = this.traer()
      .then((ok) => {
        if (!ok) this.ultimoFallo = this.reloj();
        return ok;
      })
      .finally(() => {
        this.enVuelo = null;
      });
    return this.enVuelo;
  }

  private async traer(): Promise<boolean> {
    const control = new AbortController();
    const temporizador = setTimeout(() => control.abort(), this.tiempoDeEsperaMs);
    try {
      const respuesta = await this.fetchImpl(this.uri, {
        headers: { Accept: 'application/json' },
        signal: control.signal,
      });
      if (!respuesta.ok) return false;

      const texto = await respuesta.text();
      // Un JWKS legítimo son unos cientos de bytes. Un documento enorme aquí es un error de
      // enrutado o algo peor, y no se gana nada intentando interpretarlo.
      if (texto.length > this.maxBytes) return false;

      const documento = JSON.parse(texto) as { keys?: unknown };
      if (!Array.isArray(documento.keys)) return false;

      const nuevas = new Map<string, ClavePublica>();
      for (const jwk of documento.keys) {
        const convertida = convertir(jwk);
        if (convertida) nuevas.set(convertida.kid, convertida.valor);
      }
      // Solo se reemplaza si se obtuvo algo utilizable: un JWKS vacío o ilegible no debe borrar
      // las claves buenas que ya se tenían.
      if (nuevas.size === 0) return false;

      this.claves = nuevas;
      this.traidoEn = this.reloj();
      // Una rotación puede reintroducir un `kid` que antes no estaba; la memoria de descartados
      // dejaría de decir la verdad, así que se limpia junto con la copia.
      this.kidsDescartados.clear();
      return true;
    } catch {
      // Cualquier fallo —red, tiempo agotado, JSON ilegible, HTML del enrutador equivocado— es un
      // refresco que no ocurrió. Nunca una excepción hacia arriba: quien verifica decide qué hacer
      // con la ausencia de clave, y lo hace con un rechazo, no con un error de servidor.
      return false;
    } finally {
      clearTimeout(temporizador);
    }
  }

  private podarDescartados(): void {
    const ahora = this.reloj();
    for (const [kid, momento] of this.kidsDescartados) {
      if (ahora - momento >= this.memoriaDeDescartadosMs) this.kidsDescartados.delete(kid);
    }
  }
}

/**
 * Convierte un JWK a material de verificación, descartando lo que no sirva.
 *
 * Solo pasan claves RSA de firma: un JWKS no debería traer nada más, y aceptar una clave simétrica
 * desde un documento público sería exactamente la degradación que todo esto quiere evitar —el
 * emisor publicaría, sin querer, con qué firmar—.
 */
function convertir(jwk: unknown): { kid: string; valor: ClavePublica } | null {
  if (!jwk || typeof jwk !== 'object') return null;
  const k = jwk as Record<string, unknown>;

  if (k['kty'] !== 'RSA') return null;
  if (k['use'] !== undefined && k['use'] !== 'sig') return null;
  if (typeof k['kid'] !== 'string' || k['kid'].length === 0) return null;
  if (typeof k['n'] !== 'string' || typeof k['e'] !== 'string') return null;

  // Si la clave declara algoritmo, es el que manda. Si no lo declara, RS256 es lo único que UMS
  // emite (ADR-0157) y lo único que este verificador sabe comprobar.
  const algoritmo = typeof k['alg'] === 'string' ? k['alg'] : 'RS256';
  if (algoritmo !== 'RS256') return null;

  const modulo = Buffer.from(k['n'].replace(/-/g, '+').replace(/_/g, '/'), 'base64');
  if (modulo.length < BYTES_MINIMOS_DE_MODULO) return null;

  try {
    const clave = createPublicKey({
      key: { kty: 'RSA', n: k['n'], e: k['e'] } as never,
      format: 'jwk',
    });
    return { kid: k['kid'], valor: { clave, algoritmo } };
  } catch {
    return null;
  }
}

/** Construye la URI del JWKS a partir de la base de UMS. */
export function uriDeJwks(baseUrl: string): string {
  return `${baseUrl.replace(/\/$/, '')}/.well-known/jwks.json`;
}
