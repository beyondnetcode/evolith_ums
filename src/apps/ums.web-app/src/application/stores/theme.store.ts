/**
 * theme.store.ts — Application store for theme preferences
 *
 * M-1: Separated from auth.store.ts to respect SRP.
 * C-2: No DOM manipulation here — presentation layer handles document.body.
 */
import { create } from 'zustand';
import { persist } from 'zustand/middleware';

interface ThemeState {
  isDarkMode: boolean;
  /**
   * El usuario eligió modo explícitamente.
   *
   * Se persiste con la preferencia porque la preferencia también se persiste: sin esta marca, el
   * modo que publica el sistema (`settings.theme.mode`) volvería a imponerse en cada recarga sobre
   * lo que el usuario ya había decidido.
   */
  chosenByUser: boolean;
  toggleDarkMode: () => void;
  /** Aplica el modo publicado por el sistema, salvo que el usuario ya haya elegido. */
  applyDefaultDarkMode: (isDarkMode: boolean) => void;
}

export const useThemeStore = create<ThemeState>()(
  persist(
    (set, get) => ({
      isDarkMode: false,
      chosenByUser: false,
      toggleDarkMode: () => set(state => ({ isDarkMode: !state.isDarkMode, chosenByUser: true })),
      applyDefaultDarkMode: isDarkMode => {
        if (get().chosenByUser || get().isDarkMode === isDarkMode) return;
        set({ isDarkMode });
      },
    }),
    { name: 'ums-theme' }
  )
);
