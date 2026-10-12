/**
 * Deriva la familia de color de un inquilino a partir del color que publica en el grafo, y la
 * RECHAZA si no alcanza el piso de contraste.
 *
 * Por qué existe: el sistema de diseño garantiza sus pares de contraste porque el generador del
 * núcleo los deriva de una semilla y los verifica uno a uno (ADR-0228 §2.9). Escribir el color de un
 * inquilino sobre `--m3-primary` y dejar el resto de la familia en los valores de la semilla rompe
 * esa garantía en silencio: `on-primary` sigue siendo el par del color VIEJO. Medido: un inquilino
 * con un primario claro deja la etiqueta de todos los botones `filled` por debajo de 1.1:1, es decir
 * ilegible, con todos los HTTP en verde.
 *
 * Qué hace en su lugar: trata el color del inquilino como SEMILLA —que es lo que P-UX-06 manda—,
 * deriva los cuatro roles de la familia por posición tonal, y comprueba cada par. Si un par no llega
 * al piso, ese rol NO se aplica y el sistema se queda con el generado. Un color de marca no puede
 * comprar una interfaz ilegible.
 *
 * Lo que NO hace, y conviene decirlo: la derivación es en HSL, no en HCT. El espacio de M3 conserva
 * mejor las relaciones de contraste al mover el tono, así que esto es una aproximación —y por eso la
 * verificación no es opcional aquí, mientras que en el generador es una red de seguridad—.
 */

/** Roles que se derivan de la semilla del inquilino. */
export interface FamiliaDeColor {
  /** Canales HSL (`208 75% 23%`) listos para el token. */
  readonly claro: Record<string, string>;
  readonly oscuro: Record<string, string>;
  /** Roles que NO se aplican porque su par no alcanza el piso, con el motivo. */
  readonly rechazos: readonly string[];
}

const PISO_TEXTO = 4.5;

/**
 * Los dos candidatos a `on-<rol>`: blanco y negro puros.
 *
 * Son los extremos de la rampa tonal de M3, y la elección no es estética sino aritmética: con estos
 * dos, SIEMPRE hay uno que alcanza 4.5:1 sobre cualquier color —que ninguno llegue exigiría a la vez
 * luminancia > 0.183 y < 0.175—. Con el gris oscuro del sistema (`#0b0e13`) en lugar del negro, la
 * garantía se rompe: medido, `#0f8a4d` se quedaba en 4.4:1 por los dos lados y el rol acababa
 * descartado sin necesidad.
 */
const CLARO = '#ffffff';
const OSCURO = '#000000';

export function normalizarHex(hex: string | undefined): string | undefined {
  if (!hex) return undefined;
  const limpio = hex.trim().replace(/^#/, '');
  const expandido =
    limpio.length === 3
      ? limpio
          .split('')
          .map(c => c + c)
          .join('')
      : limpio;
  return /^[0-9a-fA-F]{6}$/.test(expandido) ? `#${expandido.toLowerCase()}` : undefined;
}

function canales(hex: string): [number, number, number] {
  const n = hex.replace('#', '');
  return [
    parseInt(n.slice(0, 2), 16),
    parseInt(n.slice(2, 4), 16),
    parseInt(n.slice(4, 6), 16),
  ];
}

/** Luminancia relativa de WCAG 2.x. */
export function luminancia(hex: string): number {
  const [r, g, b] = canales(hex).map(v => {
    const s = v / 255;
    return s <= 0.03928 ? s / 12.92 : ((s + 0.055) / 1.055) ** 2.4;
  });
  return 0.2126 * r + 0.7152 * g + 0.0722 * b;
}

/** Razón de contraste entre dos colores, en el mismo formato que el ADR la declara. */
export function contraste(a: string, b: string): number {
  const la = luminancia(a);
  const lb = luminancia(b);
  const [alto, bajo] = la > lb ? [la, lb] : [lb, la];
  return (alto + 0.05) / (bajo + 0.05);
}

/** Hex → canales HSL sin envoltorio, que es lo que consumen los tokens. */
export function hexAHsl(hex: string): { h: number; s: number; l: number } {
  const [r, g, b] = canales(hex).map(v => v / 255);
  const max = Math.max(r, g, b);
  const min = Math.min(r, g, b);
  const l = (max + min) / 2;
  let h = 0;
  let s = 0;
  if (max !== min) {
    const d = max - min;
    s = l > 0.5 ? d / (2 - max - min) : d / (max + min);
    if (max === r) h = ((g - b) / d + (g < b ? 6 : 0)) / 6;
    else if (max === g) h = ((b - r) / d + 2) / 6;
    else h = ((r - g) / d + 4) / 6;
  }
  return { h: Math.round(h * 360), s: Math.round(s * 100), l: Math.round(l * 100) };
}

function hslAHex({ h, s, l }: { h: number; s: number; l: number }): string {
  const sn = s / 100;
  const ln = l / 100;
  const c = (1 - Math.abs(2 * ln - 1)) * sn;
  const x = c * (1 - Math.abs(((h / 60) % 2) - 1));
  const m = ln - c / 2;
  const [r, g, b] =
    h < 60 ? [c, x, 0]
    : h < 120 ? [x, c, 0]
    : h < 180 ? [0, c, x]
    : h < 240 ? [0, x, c]
    : h < 300 ? [x, 0, c]
    : [c, 0, x];
  const byte = (v: number) =>
    Math.round((v + m) * 255)
      .toString(16)
      .padStart(2, '0');
  return `#${byte(r)}${byte(g)}${byte(b)}`;
}

const canalesDe = (hex: string) => {
  const { h, s, l } = hexAHsl(hex);
  return `${h} ${s}% ${l}%`;
};

/** El `on-<rol>` que de verdad se lee sobre ese fondo, o nada si ninguno alcanza el piso. */
function sobre(fondoHex: string): string | undefined {
  const candidatos = [CLARO, OSCURO]
    .map(c => ({ color: c, ratio: contraste(fondoHex, c) }))
    .sort((a, b) => b.ratio - a.ratio);
  return candidatos[0].ratio >= PISO_TEXTO ? candidatos[0].color : undefined;
}

/**
 * Deriva la familia de un rol (`primary` o `tertiary`) desde la semilla del inquilino.
 *
 * Las posiciones tonales son las de M3: el contenedor claro vive arriba de la rampa y su `on-` abajo;
 * en el esquema oscuro se invierten. No se eligen a ojo — se derivan de la MISMA semilla, que es lo
 * que hace que ajustar el color del inquilino recorra la familia entera.
 */
export function derivarFamilia(rol: string, semillaHex: string | undefined): FamiliaDeColor {
  const semilla = normalizarHex(semillaHex);
  if (!semilla) return { claro: {}, oscuro: {}, rechazos: [] };

  const { h, s } = hexAHsl(semilla);
  const claro: Record<string, string> = {};
  const oscuro: Record<string, string> = {};
  const rechazos: string[] = [];

  const onSemilla = sobre(semilla);
  if (!onSemilla) {
    // Guarda inalcanzable HOY —ver el comentario de los candidatos: la aritmética lo impide— y por
    // eso conviene decirlo antes de que alguien la «simplifique». Se queda porque el día que los
    // candidatos salgan de la rampa del inquilino en vez del blanco y el negro, sí podrá dispararse,
    // y entonces el silencio costaría caro.
    rechazos.push(
      `${rol}: ni blanco ni el gris oscuro del sistema alcanzan ${PISO_TEXTO}:1 sobre ${semilla}; se conserva el color generado`
    );
    return { claro, oscuro, rechazos };
  }

  claro[rol] = canalesDe(semilla);
  claro[`on-${rol}`] = canalesDe(onSemilla);

  // Contenedor claro: mismo tono, saturación contenida, tono alto. Su `on-` es el tono bajo.
  const contenedorClaro = hslAHex({ h, s: Math.min(s, 60), l: 90 });
  const onContenedorClaro = hslAHex({ h, s: Math.min(s, 80), l: 20 });
  if (contraste(contenedorClaro, onContenedorClaro) >= PISO_TEXTO) {
    claro[`${rol}-container`] = canalesDe(contenedorClaro);
    claro[`on-${rol}-container`] = canalesDe(onContenedorClaro);
  } else {
    rechazos.push(`${rol}-container (claro): el par derivado no alcanza ${PISO_TEXTO}:1`);
  }

  // Esquema oscuro: el rol sube de tono para leerse sobre superficie oscura, y su `on-` baja.
  const semillaOscura = hslAHex({ h, s: Math.min(s, 90), l: 80 });
  const onSemillaOscura = sobre(semillaOscura);
  if (onSemillaOscura) {
    oscuro[rol] = canalesDe(semillaOscura);
    oscuro[`on-${rol}`] = canalesDe(onSemillaOscura);
  } else {
    rechazos.push(`${rol} (oscuro): el par derivado no alcanza ${PISO_TEXTO}:1`);
  }

  const contenedorOscuro = hslAHex({ h, s: Math.min(s, 70), l: 26 });
  const onContenedorOscuro = hslAHex({ h, s: Math.min(s, 60), l: 90 });
  if (contraste(contenedorOscuro, onContenedorOscuro) >= PISO_TEXTO) {
    oscuro[`${rol}-container`] = canalesDe(contenedorOscuro);
    oscuro[`on-${rol}-container`] = canalesDe(onContenedorOscuro);
  } else {
    rechazos.push(`${rol}-container (oscuro): el par derivado no alcanza ${PISO_TEXTO}:1`);
  }

  return { claro, oscuro, rechazos };
}

/**
 * El CSS del tema del inquilino, con las dos capas de token en sincronía.
 *
 * Se emite como hoja de estilo y no como estilo en línea sobre `<html>` por una razón medida: el
 * esquema oscuro se declara con `.dark`, que vive en un elemento DESCENDIENTE, y una declaración más
 * cercana gana a un estilo en línea del ancestro — el color del inquilino desaparecía al conmutar a
 * oscuro. Una hoja que llega después, con la misma especificidad, gana en los dos esquemas.
 */
export function cssDelTema(familias: readonly FamiliaDeColor[]): string {
  const junta = (lado: 'claro' | 'oscuro') =>
    familias
      .flatMap(f => Object.entries(f[lado]))
      .map(([rol, canal]) => `  --m3-${rol}: ${canal};\n  --md-sys-color-${rol}: hsl(${canal});`)
      .join('\n');

  const claro = junta('claro');
  const oscuro = junta('oscuro');
  if (!claro && !oscuro) return '';

  return [
    claro && `:root {\n${claro}\n}`,
    oscuro && `.dark,\n:root[data-theme="dark"] {\n${oscuro}\n}`,
  ]
    .filter(Boolean)
    .join('\n\n');
}
