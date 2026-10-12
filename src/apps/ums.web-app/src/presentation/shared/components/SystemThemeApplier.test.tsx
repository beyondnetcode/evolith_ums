import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, cleanup } from '@testing-library/react';
import { SystemThemeApplier, hexAHslCanales } from './SystemThemeApplier';
import { useI18nStore } from '@app/stores/i18n.store';
import { useThemeStore } from '@app/stores/theme.store';

let settings: Record<string, Record<string, string>> = {};

vi.mock('@app/stores/auth.store', () => ({
  useAuthStore: (selector: (s: unknown) => unknown) =>
    selector({ user: { authorizationGraph: { settings } } }),
}));

describe('hexAHslCanales', () => {
  it('convierte al formato de los tokens (tripleta HSL sin envoltorio)', () => {
    // Los tokens son `208 75% 23%`, no `#0f3e67`: Tailwind los compone con opacidad.
    // Ese valor es, literalmente, el que `index.css` tiene escrito a mano para --m3-primary:
    // la conversión reproduce el token del producto a partir del hexadecimal corporativo.
    expect(hexAHslCanales('#0f3e67')).toBe('208 75% 23%');
    expect(hexAHslCanales('0f3e67')).toBe('208 75% 23%');
  });

  it('admite la forma corta de tres dígitos', () => {
    expect(hexAHslCanales('#fff')).toBe('0 0% 100%');
  });

  it('devuelve undefined ante un valor inválido', () => {
    // Un color mal configurado en la base debe dejar el tema por defecto, no romper la interfaz.
    expect(hexAHslCanales('azul')).toBeUndefined();
    expect(hexAHslCanales('#12345')).toBeUndefined();
    expect(hexAHslCanales(undefined)).toBeUndefined();
  });
});

describe('SystemThemeApplier', () => {
  beforeEach(() => {
    settings = {};
    document.documentElement.style.removeProperty('--m3-primary');
    document.title = 'UMS';
    useI18nStore.setState({ language: 'es', chosenByUser: false });
    useThemeStore.setState({ isDarkMode: false, chosenByUser: false });
    delete document.documentElement.dataset.density;
    cleanup();
  });

  it('no toca nada cuando el sistema no publica tema', () => {
    render(<SystemThemeApplier />);
    expect(document.getElementById('tema-de-inquilino')).toBeNull();
  });

  it('aplica el color primario del sistema, con su familia derivada', () => {
    settings = { theme: { primary: '#0f3e67' } };
    render(<SystemThemeApplier />);

    // El tema viaja como HOJA DE ESTILO y no como estilo en línea: `.dark` vive en un elemento
    // descendiente y una declaración más cercana ganaba al `style` del ancestro, así que el color
    // del inquilino desaparecía al conmutar a oscuro.
    const hoja = document.getElementById('tema-de-inquilino');
    expect(hoja?.textContent).toContain('--m3-primary: 208 75% 23%');
    // Y no llega solo: `on-primary` es el par de ESTE color, no el del anterior. Escribir el rol
    // suelto es lo que rompía la garantía de contraste del generador (P-UX-06).
    expect(hoja?.textContent).toContain('--m3-on-primary:');
    expect(hoja?.textContent).toContain('--m3-primary-container:');
    // Las dos capas de token dicen lo mismo.
    expect(hoja?.textContent).toContain('--md-sys-color-primary: hsl(208 75% 23%)');
    // Y el esquema oscuro tiene su propia declaración.
    expect(hoja?.textContent).toContain('.dark');
  });

  it('revierte el tema al desmontarse', () => {
    settings = { theme: { primary: '#0f3e67' } };
    const { unmount } = render(<SystemThemeApplier />);
    unmount();

    // Al cambiar de sistema o cerrar sesión, el tema anterior no debe sobrevivir.
    expect(document.getElementById('tema-de-inquilino')).toBeNull();
  });

  it('usa el nombre comercial del sistema como título del documento', () => {
    settings = { brand: { display_name: 'Tablero de Gobierno SDLC' } };
    const { unmount } = render(<SystemThemeApplier />);
    expect(document.title).toBe('Tablero de Gobierno SDLC');

    unmount();
    expect(document.title).toBe('UMS');
  });

  it('usa el icono del sistema como favicon', () => {
    settings = { brand: { icon_url: '/branding/sdlc/icon.svg' } };
    const enlace = document.createElement('link');
    enlace.rel = 'icon';
    enlace.type = 'image/png';
    enlace.href = '/brand/logo-evolith.png';
    document.head.appendChild(enlace);

    const { unmount } = render(<SystemThemeApplier />);
    expect(enlace.getAttribute('href')).toBe('/branding/sdlc/icon.svg');
    // El type del producto (PNG) no describe un icono SVG: declararlo mal lo descarta.
    expect(enlace.getAttribute('type')).toBeNull();

    unmount();
    expect(enlace.getAttribute('href')).toBe('/brand/logo-evolith.png');
    expect(enlace.getAttribute('type')).toBe('image/png');
    enlace.remove();
  });

  it('no toca el favicon si el sistema solo publica logotipo', () => {
    // El logotipo es un lockup ancho: en 16px sería un borrón. Mejor el icono del producto.
    settings = { brand: { logo_url: '/branding/sdlc/logo.svg' } };
    const enlace = document.createElement('link');
    enlace.rel = 'icon';
    enlace.href = '/brand/logo-evolith.png';
    document.head.appendChild(enlace);

    render(<SystemThemeApplier />);
    expect(enlace.getAttribute('href')).toBe('/brand/logo-evolith.png');
    enlace.remove();
  });

  it('crea el enlace si el documento no lo declara, y lo retira al desmontarse', () => {
    settings = { brand: { icon_url: '/branding/sdlc/icon.svg' } };

    const { unmount } = render(<SystemThemeApplier />);
    expect(document.querySelector('link[rel="icon"]')?.getAttribute('href')).toBe(
      '/branding/sdlc/icon.svg'
    );

    unmount();
    expect(document.querySelector('link[rel="icon"]')).toBeNull();
  });

  it('ignora un color inválido en vez de dejar la interfaz a medias', () => {
    settings = { theme: { primary: 'no-es-un-color' } };
    render(<SystemThemeApplier />);
    expect(document.documentElement.style.getPropertyValue('--m3-primary')).toBe('');
  });

  it('aplica el idioma que publica el sistema', () => {
    settings = { locale: { language: 'en-US' } };
    render(<SystemThemeApplier />);

    expect(useI18nStore.getState().language).toBe('en');
  });

  it('no revierte el idioma al desmontarse', () => {
    // A diferencia del tema o el título: el idioma sobrevive al cierre de sesión, o la pantalla
    // de acceso aparecería en otro idioma que el que el usuario acaba de estar usando.
    settings = { locale: { language: 'en-US' } };
    const { unmount } = render(<SystemThemeApplier />);
    unmount();

    expect(useI18nStore.getState().language).toBe('en');
  });

  it('aplica el modo oscuro que publica el sistema', () => {
    settings = { theme: { mode: 'dark' } };
    render(<SystemThemeApplier />);

    expect(useThemeStore.getState().isDarkMode).toBe(true);
  });

  it('no pisa el modo que el usuario eligió', () => {
    useThemeStore.getState().toggleDarkMode(); // el usuario se pasa a oscuro
    settings = { theme: { mode: 'light' } };
    render(<SystemThemeApplier />);

    expect(useThemeStore.getState().isDarkMode).toBe(true);
  });

  it('con modo `system` sigue al sistema operativo y reacciona a sus cambios', () => {
    const escuchas: Array<(e: { matches: boolean }) => void> = [];
    vi.stubGlobal(
      'matchMedia',
      vi.fn(() => ({
        matches: false,
        addEventListener: (_: string, cb: (e: { matches: boolean }) => void) => escuchas.push(cb),
        removeEventListener: vi.fn(),
      }))
    );

    settings = { theme: { mode: 'system' } };
    render(<SystemThemeApplier />);
    expect(useThemeStore.getState().isDarkMode).toBe(false);

    // Anochece en el sistema operativo: `system` no es un modo fijo, es delegar.
    escuchas.forEach(cb => cb({ matches: true }));
    expect(useThemeStore.getState().isDarkMode).toBe(true);

    vi.unstubAllGlobals();
  });

  it('escribe la densidad en la raíz y la retira al desmontarse', () => {
    settings = { ui: { density: 'compact' } };
    const { unmount } = render(<SystemThemeApplier />);

    expect(document.documentElement.dataset.density).toBe('compact');

    unmount();
    expect(document.documentElement.dataset.density).toBeUndefined();
  });

  it('ignora una densidad que el CSS no define', () => {
    // Un valor desconocido dejaría las filas sin espaciado en vez de descolocar la interfaz.
    settings = { ui: { density: 'holgadisima' } };
    render(<SystemThemeApplier />);

    expect(document.documentElement.dataset.density).toBeUndefined();
  });
});
