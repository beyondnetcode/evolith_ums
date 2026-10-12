import { describe, it, expect, beforeEach, vi } from 'vitest';
import { useThemeStore } from './theme.store';

vi.mock('zustand/middleware', async importOriginal => {
  const actual = await importOriginal<typeof import('zustand/middleware')>();
  return {
    ...actual,
    persist: (creator: unknown) => creator,
  };
});

describe('theme.store', () => {
  beforeEach(() => {
    useThemeStore.setState({ isDarkMode: false, chosenByUser: false });
  });

  it('initializes with light mode enabled (EVOLITH corporate default)', () => {
    expect(useThemeStore.getState().isDarkMode).toBe(false);
  });

  it('toggles dark mode from true to false', () => {
    useThemeStore.setState({ isDarkMode: true });
    useThemeStore.getState().toggleDarkMode();
    expect(useThemeStore.getState().isDarkMode).toBe(false);
  });

  it('toggles dark mode from false to true', () => {
    useThemeStore.setState({ isDarkMode: false });
    useThemeStore.getState().toggleDarkMode();
    expect(useThemeStore.getState().isDarkMode).toBe(true);
  });

  it('toggles multiple times correctly', () => {
    useThemeStore.setState({ isDarkMode: true });
    useThemeStore.getState().toggleDarkMode();
    expect(useThemeStore.getState().isDarkMode).toBe(false);
    useThemeStore.getState().toggleDarkMode();
    expect(useThemeStore.getState().isDarkMode).toBe(true);
    useThemeStore.getState().toggleDarkMode();
    expect(useThemeStore.getState().isDarkMode).toBe(false);
  });

  it('has toggleDarkMode function', () => {
    expect(typeof useThemeStore.getState().toggleDarkMode).toBe('function');
  });

  describe('modo publicado por el sistema', () => {
    it('lo aplica cuando el usuario no ha elegido', () => {
      useThemeStore.getState().applyDefaultDarkMode(true);
      expect(useThemeStore.getState().isDarkMode).toBe(true);
    });

    it('respeta la elección explícita del usuario', () => {
      // La preferencia se persiste; sin esta marca el sistema la revertiría en cada recarga.
      useThemeStore.getState().toggleDarkMode();
      useThemeStore.getState().applyDefaultDarkMode(false);

      expect(useThemeStore.getState().isDarkMode).toBe(true);
    });

    it('el conmutador marca la elección del usuario', () => {
      expect(useThemeStore.getState().chosenByUser).toBe(false);
      useThemeStore.getState().toggleDarkMode();
      expect(useThemeStore.getState().chosenByUser).toBe(true);
    });
  });
});
