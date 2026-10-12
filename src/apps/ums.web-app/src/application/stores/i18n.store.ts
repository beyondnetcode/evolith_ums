/**
 * i18n.store.ts — Independent internationalization store.
 *
 * Manages the active language state separately from devTools.
 * In development, devTools can sync with this store, but production
 * code should only depend on this store for language selection.
 */
import { create } from 'zustand';

export type SupportedLanguage = 'en' | 'es';

interface I18nState {
  /** Active UI language */
  language: SupportedLanguage;
  /**
   * El usuario eligió idioma explícitamente en esta sesión del navegador.
   *
   * Existe para que el idioma que publica el sistema (`settings.locale.language`) actúe como
   * DEFECTO y no como imposición: quien acaba de pulsar el conmutador no debería ver cómo se le
   * revierte al resolverse el grafo.
   */
  chosenByUser: boolean;
  /** Set the active language and update document.documentElement.lang */
  setLanguage: (lang: SupportedLanguage) => void;
  /** Aplica el idioma publicado por el sistema, salvo que el usuario ya haya elegido. */
  applySystemLanguage: (tag: string | undefined) => void;
}

/**
 * `es-PE` → `es`. Solo se aceptan los idiomas que el producto traduce: un `pt-BR` publicado por
 * error debe dejar la interfaz como está, no a medio traducir.
 */
export function toSupportedLanguage(tag: string | undefined): SupportedLanguage | undefined {
  const primario = tag?.trim().toLowerCase().split(/[-_]/)[0];
  return primario === 'es' || primario === 'en' ? primario : undefined;
}

export const useI18nStore = create<I18nState>((set, get) => ({
  language: 'es',
  chosenByUser: false,
  setLanguage: lang => {
    document.documentElement.lang = lang;
    set({ language: lang, chosenByUser: true });
  },
  applySystemLanguage: tag => {
    if (get().chosenByUser) return;

    const lang = toSupportedLanguage(tag);
    if (!lang || lang === get().language) return;

    document.documentElement.lang = lang;
    set({ language: lang });
  },
}));
