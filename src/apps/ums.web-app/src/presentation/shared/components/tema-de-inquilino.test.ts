import { describe, expect, it } from 'vitest';
import { contraste, cssDelTema, derivarFamilia, hexAHsl, normalizarHex } from './tema-de-inquilino';

/** HSL → hex, solo para componer los casos de esta prueba. */
function hslAHexParaPrueba(h: number, s: number, l: number): string {
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
  const byte = (v: number) => Math.round((v + m) * 255).toString(16).padStart(2, '0');
  return `#${byte(r)}${byte(g)}${byte(b)}`;
}

describe('tema de inquilino', () => {
  it('reproduce los ratios que el ADR declara', () => {
    // Si esta prueba se cae, la fórmula dejó de ser la del estándar y todo lo demás sobra.
    expect(contraste('#0f3e67', '#ffffff')).toBeCloseTo(11.02, 1);
  });

  it('convierte la semilla de marca a los canales del token', () => {
    expect(hexAHsl('#0f3e67')).toEqual({ h: 208, s: 75, l: 23 });
  });

  it('acepta el hexadecimal corto y rechaza lo que no es color', () => {
    expect(normalizarHex('#0F6')).toBe('#00ff66');
    expect(normalizarHex('rojo')).toBeUndefined();
    expect(normalizarHex(undefined)).toBeUndefined();
  });

  it('deriva la familia completa, no solo el rol', () => {
    const { claro, oscuro, rechazos } = derivarFamilia('primary', '#0f3e67');

    expect(rechazos).toEqual([]);
    // Los cuatro roles de la familia, en los dos esquemas: es lo que P-UX-06 exige.
    expect(Object.keys(claro).sort()).toEqual(
      ['on-primary', 'on-primary-container', 'primary', 'primary-container'].sort()
    );
    expect(Object.keys(oscuro).sort()).toEqual(
      ['on-primary', 'on-primary-container', 'primary', 'primary-container'].sort()
    );
  });

  it('el `on-` derivado se lee de verdad sobre su fondo', () => {
    // El defecto que este módulo existe para impedir: un primario claro con `on-primary` blanco.
    const { claro } = derivarFamilia('primary', '#ffe600');
    const aHex = (canal: string) => {
      const [h, s, l] = canal.replace(/%/g, '').split(' ').map(Number);
      return { h, s, l };
    };
    expect(aHex(claro['on-primary']).l).toBeLessThan(20); // negro, no blanco
  });

  it('pase el color que pase, el par (rol, on-rol) alcanza el piso', () => {
    // Esta es la propiedad que importa, y se comprueba sobre una rejilla de colores en vez de sobre
    // un caso simpático: da igual qué publique el inquilino, la etiqueta se lee sobre su fondo.
    const aHex = (canal: string) => {
      const [h, s, l] = canal.replace(/%/g, '').split(' ').map(Number);
      return hslAHexParaPrueba(h, s, l);
    };

    for (let h = 0; h < 360; h += 30) {
      for (const l of [10, 30, 50, 70, 90]) {
        const semilla = hslAHexParaPrueba(h, 80, l);
        const { claro, oscuro, rechazos } = derivarFamilia('primary', semilla);
        expect(rechazos, `${semilla} fue rechazado`).toEqual([]);
        expect(contraste(aHex(claro.primary), aHex(claro['on-primary'])), semilla).toBeGreaterThanOrEqual(4.5);
        expect(contraste(aHex(oscuro.primary), aHex(oscuro['on-primary'])), semilla).toBeGreaterThanOrEqual(4.5);
        expect(
          contraste(aHex(claro['primary-container']), aHex(claro['on-primary-container'])),
          semilla
        ).toBeGreaterThanOrEqual(4.5);
      }
    }
  });

  it('emite las dos capas de token en sincronía', () => {
    const css = cssDelTema([derivarFamilia('primary', '#0f3e67')]);

    // Si solo se escribiera `--m3-*`, la capa alias quedaría con el valor viejo y las dos dejarían
    // de decir lo mismo.
    expect(css).toContain('--m3-primary:');
    expect(css).toContain('--md-sys-color-primary: hsl(');
    // Y el esquema oscuro va por hoja de estilo, no por estilo en línea, o `.dark` lo pisaría.
    expect(css).toContain('.dark');
  });

  it('sin color de inquilino no emite nada', () => {
    expect(cssDelTema([derivarFamilia('primary', undefined)])).toBe('');
  });
});
