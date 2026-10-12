import { useMemo } from 'react';
import { useAuthStore } from '@app/stores/auth.store';

/**
 * Ajustes del sistema que el grafo publica para inicializar la aplicación (G-178).
 *
 * El servidor los entrega agrupados por espacio de nombres —`settings.brand.logo_url`,
 * `settings.theme.primary`, `settings.ui.home_route`…— y solo incluye los marcados como visibles.
 *
 * Este hook NO valida ni completa lo que falte con datos inventados: devuelve `undefined` y deja
 * que cada consumidor decida su respaldo. Un sistema sin branding configurado debe verse como el
 * producto por defecto, no como un producto a medio pintar.
 */
export interface SystemSettings {
  brand: {
    displayName?: string;
    shortName?: string;
    /** Descriptor bajo el nombre comercial; sustituye al subtítulo del producto. */
    tagline?: string;
    logoUrl?: string;
    iconUrl?: string;
  };
  theme: {
    primary?: string;
    accent?: string;
    mode?: 'light' | 'dark' | 'system';
  };
  ui: {
    layout?: string;
    homeRoute?: string;
    density?: string;
  };
  locale: {
    language?: string;
    timezone?: string;
    currency?: string;
  };
  /** Acceso crudo para espacios de nombres que este hook aún no tipa. */
  raw: Record<string, Record<string, string>>;
}

const VACIO: Record<string, Record<string, string>> = {};

export function useSystemSettings(): SystemSettings {
  const settings = useAuthStore(
    state =>
      (state.user?.authorizationGraph?.settings as Record<string, Record<string, string>>) ?? VACIO
  );

  return useMemo(() => {
    const grupo = (nombre: string) => settings[nombre] ?? {};
    const brand = grupo('brand');
    const theme = grupo('theme');
    const ui = grupo('ui');
    const locale = grupo('locale');

    const modo = theme.mode;

    return {
      brand: {
        displayName: brand.display_name,
        shortName: brand.short_name,
        tagline: brand.tagline,
        logoUrl: brand.logo_url,
        iconUrl: brand.icon_url,
      },
      theme: {
        primary: theme.primary,
        accent: theme.accent,
        // Cualquier otro valor se ignora: un modo desconocido no debe dejar la interfaz a medias.
        mode: modo === 'light' || modo === 'dark' || modo === 'system' ? modo : undefined,
      },
      ui: {
        layout: ui.layout,
        homeRoute: ui.home_route,
        density: ui.density,
      },
      locale: {
        language: locale.language,
        timezone: locale.timezone,
        currency: locale.currency,
      },
      raw: settings,
    };
  }, [settings]);
}
