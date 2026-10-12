import { useEffect } from 'react';
import { cssDelTema, derivarFamilia } from './tema-de-inquilino';
import { useSystemSettings } from '@app/authorization/hooks/use-system-settings';
import { useI18nStore } from '@app/stores/i18n.store';
import { useThemeStore } from '@app/stores/theme.store';

/**
 * Aplica al documento el tema, el título, el favicon, el idioma, el modo claro/oscuro y la
 * densidad que publica el sistema en el grafo (G-178).
 *
 * No pinta nada: es un efecto. Vive como componente para colgar del ciclo de vida de React y
 * revertirse solo cuando el usuario cambia de sistema o cierra sesión — un `useEffect` suelto en
 * un layout se quedaría sin limpiar.
 *
 * Los tokens del tema son tripletas HSL sin envoltorio (`208 75% 23%`), no hexadecimales: Tailwind
 * las compone con opacidad (`hsl(var(--m3-primary) / 0.3)`). Por eso el color que publica el
 * sistema se convierte antes de escribirlo; inyectar el hexadecimal tal cual dejaría la mitad de
 * la interfaz sin color, y solo en los sitios que usan transparencia.
 */
/** Densidades que el CSS del producto traduce a espaciado. Cualquier otra se ignora. */
const DENSIDADES = new Set(['compact', 'comfortable', 'spacious']);

export const SystemThemeApplier: React.FC = () => {
  const { brand, theme, locale, ui } = useSystemSettings();
  const applySystemLanguage = useI18nStore(s => s.applySystemLanguage);
  const applyDefaultDarkMode = useThemeStore(s => s.applyDefaultDarkMode);

  useEffect(() => {
    // El color del inquilino se trata como SEMILLA y se deriva su familia entera, verificando cada
    // par (P-UX-06 de ADR-0110). Antes se escribía `--m3-primary` suelto y `on-primary` seguía
    // siendo el par del color VIEJO: la garantía de contraste del generador —cincuenta pares
    // verificados— dejaba de valer en cuanto un inquilino publicaba su marca, y un primario claro
    // dejaba la etiqueta de los botones `filled` por debajo de 1.1:1.
    //
    // Un rol cuyo par derivado no alcanza el piso NO se aplica: se conserva el generado. Un color de
    // marca no compra una interfaz ilegible.
    const familias = [derivarFamilia('primary', theme.primary), derivarFamilia('tertiary', theme.accent)];
    const rechazos = familias.flatMap(f => f.rechazos);
    if (rechazos.length > 0) {
      // Se dice en voz alta: un tema descartado en silencio se ve igual que uno que nunca llegó.
      console.warn('[tema de inquilino] roles descartados por contraste:\n  ' + rechazos.join('\n  '));
    }

    const css = cssDelTema(familias);
    if (!css) return;

    // Hoja de estilo y no estilo en línea: `.dark` vive en un elemento descendiente y una
    // declaración más cercana gana al `style` del ancestro — así el color del inquilino desaparecía
    // al conmutar a oscuro. Una hoja posterior con la misma especificidad gana en los dos esquemas.
    const hoja = document.createElement('style');
    hoja.id = 'tema-de-inquilino';
    hoja.textContent = css;
    document.head.appendChild(hoja);

    return () => {
      // Al cambiar de sistema —o de perfil— el tema anterior no debe sobrevivir.
      hoja.remove();
    };
  }, [theme.primary, theme.accent]);

  useEffect(() => {
    if (!brand.displayName) return;

    const anterior = document.title;
    document.title = brand.displayName;
    return () => {
      document.title = anterior;
    };
  }, [brand.displayName]);

  useEffect(() => {
    // Solo el icono, nunca el logotipo: la pestaña lo pinta en un cuadrado de ~16px y un lockup
    // ancho ahí es un borrón. Sin icono configurado se queda el del producto, que es correcto.
    if (!brand.iconUrl) return;

    const creado = !document.querySelector('link[rel="icon"]');
    const enlace =
      document.querySelector<HTMLLinkElement>('link[rel="icon"]') ??
      document.head.appendChild(Object.assign(document.createElement('link'), { rel: 'icon' }));

    const hrefAnterior = enlace.getAttribute('href');
    const tipoAnterior = enlace.getAttribute('type');

    enlace.setAttribute('href', brand.iconUrl);
    // El `type` declarado describe el recurso del producto (PNG) y el del sistema puede ser otro
    // (SVG). Declarar un tipo que no corresponde hace que el navegador descarte el icono; sin el
    // atributo lo deduce del recurso.
    enlace.removeAttribute('type');

    return () => {
      if (creado) {
        enlace.remove();
        return;
      }
      // Al cambiar de sistema o cerrar sesión vuelve el icono del producto.
      if (hrefAnterior) enlace.setAttribute('href', hrefAnterior);
      if (tipoAnterior) enlace.setAttribute('type', tipoAnterior);
    };
  }, [brand.iconUrl]);

  useEffect(() => {
    // Defecto, no imposición: el store ignora esto si el usuario ya eligió idioma. Y no se revierte
    // al desmontar —a diferencia del tema o el título— porque el idioma de la interfaz sobrevive al
    // cierre de sesión: devolverlo al salir dejaría la pantalla de acceso en otro idioma.
    applySystemLanguage(locale.language);
  }, [locale.language, applySystemLanguage]);

  useEffect(() => {
    if (!theme.mode) return;

    // `system` no es un modo: es delegar en el sistema operativo, y por eso hay que seguir
    // escuchándolo. Fijarlo una vez dejaría la interfaz en claro toda la noche.
    if (theme.mode !== 'system') {
      applyDefaultDarkMode(theme.mode === 'dark');
      return;
    }

    const consulta = window.matchMedia('(prefers-color-scheme: dark)');
    applyDefaultDarkMode(consulta.matches);

    const alCambiar = (e: MediaQueryListEvent) => applyDefaultDarkMode(e.matches);
    consulta.addEventListener('change', alCambiar);
    return () => consulta.removeEventListener('change', alCambiar);
  }, [theme.mode, applyDefaultDarkMode]);

  useEffect(() => {
    // La densidad viaja como atributo y el CSS la traduce a espaciado: un valor desconocido no
    // debe descolocar la interfaz, así que solo se aceptan los tres que el producto define.
    if (!ui.density || !DENSIDADES.has(ui.density)) return;

    const raiz = document.documentElement;
    const anterior = raiz.dataset.density;
    raiz.dataset.density = ui.density;

    return () => {
      if (anterior) raiz.dataset.density = anterior;
      else delete raiz.dataset.density;
    };
  }, [ui.density]);

  return null;
};

/**
 * Convierte `#0f3e67` en `208 75% 23%`, el formato de los tokens.
 *
 * Devuelve `undefined` ante cualquier cosa que no sea un hexadecimal de 3 o 6 dígitos: un valor
 * mal configurado en la base no debe romper la interfaz, solo dejar el color por defecto.
 */
export function hexAHslCanales(hex: string | undefined): string | undefined {
  if (!hex) return undefined;

  const limpio = hex.trim().replace(/^#/, '');
  const expandido =
    limpio.length === 3
      ? limpio
          .split('')
          .map(c => c + c)
          .join('')
      : limpio;

  if (!/^[0-9a-fA-F]{6}$/.test(expandido)) return undefined;

  const r = parseInt(expandido.slice(0, 2), 16) / 255;
  const g = parseInt(expandido.slice(2, 4), 16) / 255;
  const b = parseInt(expandido.slice(4, 6), 16) / 255;

  const max = Math.max(r, g, b);
  const min = Math.min(r, g, b);
  const l = (max + min) / 2;
  const d = max - min;

  let h = 0;
  let s = 0;

  if (d !== 0) {
    s = d / (1 - Math.abs(2 * l - 1));
    switch (max) {
      case r:
        h = ((g - b) / d) % 6;
        break;
      case g:
        h = (b - r) / d + 2;
        break;
      default:
        h = (r - g) / d + 4;
    }
    h *= 60;
    if (h < 0) h += 360;
  }

  return `${Math.round(h)} ${Math.round(s * 100)}% ${Math.round(l * 100)}%`;
}
