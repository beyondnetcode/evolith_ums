import { describe, it, expect, beforeEach } from 'vitest';
import { useI18nStore, toSupportedLanguage } from './i18n.store';

describe('i18n.store', () => {
  beforeEach(() => {
    useI18nStore.setState({ language: 'es', chosenByUser: false });
    document.documentElement.lang = 'es';
  });

  it('initializes with Spanish as default', () => {
    expect(useI18nStore.getState().language).toBe('es');
  });

  it('changes language to English', () => {
    useI18nStore.getState().setLanguage('en');
    expect(useI18nStore.getState().language).toBe('en');
  });

  it('updates document.documentElement.lang on language change', () => {
    useI18nStore.getState().setLanguage('en');
    expect(document.documentElement.lang).toBe('en');
  });

  it('updates document.documentElement.lang to Spanish', () => {
    useI18nStore.getState().setLanguage('es');
    expect(document.documentElement.lang).toBe('es');
  });

  it('selects language via getState', () => {
    useI18nStore.setState({ language: 'en' });
    const lang = useI18nStore.getState().language;
    expect(lang).toBe('en');
  });

  it('has setLanguage function', () => {
    expect(typeof useI18nStore.getState().setLanguage).toBe('function');
  });

  describe('idioma publicado por el sistema', () => {
    it('lo aplica cuando el usuario no ha elegido', () => {
      useI18nStore.getState().applySystemLanguage('en-US');

      expect(useI18nStore.getState().language).toBe('en');
      expect(document.documentElement.lang).toBe('en');
    });

    it('respeta la elección explícita del usuario', () => {
      // Quien acaba de pulsar el conmutador no debe ver cómo se le revierte al resolverse el grafo.
      useI18nStore.getState().setLanguage('en');
      useI18nStore.getState().applySystemLanguage('es-PE');

      expect(useI18nStore.getState().language).toBe('en');
    });

    it('ignora un idioma que el producto no traduce', () => {
      // Mejor la interfaz entera en español que media traducida a un idioma inexistente.
      useI18nStore.getState().applySystemLanguage('pt-BR');

      expect(useI18nStore.getState().language).toBe('es');
    });

    it('ignora la ausencia de idioma', () => {
      useI18nStore.getState().applySystemLanguage(undefined);

      expect(useI18nStore.getState().language).toBe('es');
    });
  });

  describe('toSupportedLanguage', () => {
    it('se queda con el subtag primario', () => {
      expect(toSupportedLanguage('es-PE')).toBe('es');
      expect(toSupportedLanguage('EN_us')).toBe('en');
      expect(toSupportedLanguage('  es  ')).toBe('es');
    });

    it('devuelve undefined para lo que no se traduce', () => {
      expect(toSupportedLanguage('pt')).toBeUndefined();
      expect(toSupportedLanguage('')).toBeUndefined();
      expect(toSupportedLanguage(undefined)).toBeUndefined();
    });
  });
});
