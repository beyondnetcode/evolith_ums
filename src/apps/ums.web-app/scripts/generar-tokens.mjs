#!/usr/bin/env node
// Genera los tokens de diseño de UMS desde el ORIGEN ÚNICO del núcleo (P-UX-04 de ADR-0110).
//
// Los valores no se escriben aquí ni en el CSS: los emite `tools/generar-tokens-evolith.py` de
// `evolith_arch` desde la semilla de marca `#0f3e67` (ADR-0228), que además verifica los cincuenta
// pares de contraste. Este guion solo TRADUCE esa salida a lo que la aplicación consume:
//
//   · conserva la capa alias `--md-sys-*` tal cual, que es la que ADR-0110 §2.2 manda consumir;
//   · añade `--m3-*` con los mismos colores en CANALES HSL, porque Tailwind v3 compone la opacidad
//     (`bg-m3-primary/10`) y no puede hacerlo con un hexadecimal;
//   · reescribe el selector de esquema oscuro al `.dark` que usa esta aplicación.
//
// Ninguna de esas tres cosas inventa un valor: si la semilla cambia, se regenera y el sistema entero
// se mueve con ella. Editar el CSS de salida a mano es exactamente lo que P-UX-04 prohíbe.
//
// Uso:  node scripts/generar-tokens.mjs   (con EVOLITH_ARCH apuntando al clon, o el clon hermano)

import { execFileSync } from 'node:child_process';
import { existsSync, writeFileSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const aqui = dirname(fileURLToPath(import.meta.url));
const salida = resolve(aqui, '..', 'src', 'styles', 'tokens-evolith.generated.css');

const candidatos = [
  process.env.EVOLITH_ARCH && join(process.env.EVOLITH_ARCH, 'tools', 'generar-tokens-evolith.py'),
  resolve(aqui, '..', '..', '..', '..', '..', 'evolith_arch', 'tools', 'generar-tokens-evolith.py'),
].filter(Boolean);

const generador = candidatos.find(ruta => existsSync(ruta));
if (!generador) {
  console.error(
    'No se encontró el generador de tokens del núcleo.\n' +
    'Es el origen único (P-UX-04): sin él no se pueden regenerar los tokens sin transcribir valores.\n' +
    'Declara EVOLITH_ARCH con la ruta al clon de evolith_arch, o clónalo junto a este repositorio.\n' +
    'Buscado en:\n  ' + candidatos.join('\n  '));
  process.exit(1);
}

// La puerta del propio generador: recorre los pares de contraste y falla si alguno baja del piso.
try {
  execFileSync('python3', [generador, '--verificar'], { stdio: 'pipe' });
} catch (error) {
  console.error('El generador del núcleo NO pasa su propia verificación de contraste:\n' +
    String(error.stdout ?? error.message));
  process.exit(1);
}

const crudo = execFileSync('python3', [generador], { encoding: 'utf8' });

/** `#0f3e67` → `208 75% 23%`, que es lo que Tailwind compone con opacidad. */
function aCanalesHsl(hex) {
  const limpio = hex.replace('#', '');
  const r = parseInt(limpio.slice(0, 2), 16) / 255;
  const g = parseInt(limpio.slice(2, 4), 16) / 255;
  const b = parseInt(limpio.slice(4, 6), 16) / 255;
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
  return `${Math.round(h * 360)} ${Math.round(s * 100)}% ${Math.round(l * 100)}%`;
}

// Por cada color alias se emite su gemelo en canales, en la misma línea de bloque.
const conCanales = crudo.replace(
  /^(\s*)--md-sys-color-([a-z-]+):\s*(#[0-9a-fA-F]{6});$/gm,
  (_todo, sangria, rol, hex) =>
    `${sangria}--md-sys-color-${rol}: ${hex};\n${sangria}--m3-${rol}: ${aCanalesHsl(hex)};`);


// El generador del núcleo emite también un bloque `@theme`, que es el último eslabón de la cadena en
// Tailwind v4. Esta aplicación todavía corre v3, y v3 NO conoce `@theme`: PostCSS lo deja pasar tal
// cual y el navegador descarta la at-rule desconocida CON TODO SU BLOQUE. Medido sobre el bundle:
// viajaba literal al cliente, `--color-primary` y `--text-body-large` no existían en runtime y
// ninguna utilidad se generaba desde ellos. Se poda aquí —era peso muerto y, peor, daba la
// impresión de que la cadena v4 estaba viva—. Al migrar a v4, quitar esta poda es lo único que hay
// que hacer para reconectarla.
const sinTema = conCanales.replace(/@theme\s*\{[\s\S]*?\n\}\n?/g, '');

// Esta aplicación conmuta el tema con la clase `.dark` (Tailwind `darkMode: 'class'`), no con
// `data-theme`. Se aceptan las dos para no romper a quien llegue con el atributo del núcleo.
const conSelectores = sinTema
  .replace(':root:not([data-theme="dark"])', ':root:not(.dark):not([data-theme="dark"])')
  .replace(':root:not([data-theme="light"])', ':root:not(.light):not([data-theme="light"])')
  .replace(':root[data-theme="dark"] {', '.dark,\n:root[data-theme="dark"] {');

const cabecera = [
  '/* GENERADO por scripts/generar-tokens.mjs desde el generador de evolith_arch — NO editar a mano.',
  ' *',
  ' * Origen único de tokens (P-UX-04 de ADR-0110): la semilla de marca `#0f3e67` de ADR-0228 y las',
  ' * rampas HCT que de ella derivan. Este fichero es SALIDA; para cambiar un color se cambia la',
  ' * semilla en el núcleo y se regenera con `npm run tokens`.',
  ' *',
  ' * Trae dos capas: `--md-sys-*` es la capa alias que los componentes consumen, y `--m3-*` los',
  ' * mismos colores en canales HSL para que Tailwind pueda componer opacidad.',
  ' */',
  '',
].join('\n');

writeFileSync(salida, cabecera + conSelectores, 'utf8');
console.log(`Tokens generados en ${salida}`);
