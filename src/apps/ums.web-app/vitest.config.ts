import { defineConfig } from 'vitest/config';
import react from '@vitejs/plugin-react';
import path from 'path';

export default defineConfig({
  plugins: [react()],
  resolve: {
    alias: {
      '@app': path.resolve(__dirname, 'src/application'),
      '@domain': path.resolve(__dirname, 'src/domain'),
      '@infra': path.resolve(__dirname, 'src/infrastructure'),
      '@infrastructure': path.resolve(__dirname, 'src/infrastructure'),
      '@presentation': path.resolve(__dirname, 'src/presentation'),
      '@shared': path.resolve(__dirname, 'src/presentation/shared'),
    },
  },
  test: {
    environment: 'jsdom',
    globals: true,
    setupFiles: ['./src/test-setup.ts'],
    exclude: ['**/e2e/**', '**/tests/*.spec.ts', '**/node_modules/**'],
    coverage: {
      provider: 'v8',
      reporter: ['text', 'lcov'],
      // Piso de cobertura (G-083). Medido sobre la suite completa el 2026-07-21 con
      // 1494 tests: global real = 34.97% stmts / 27.69% branches / 32.04% funcs /
      // 36.16% lines (denominador incluyendo parameter-catalog, 0% cubierto). El piso
      // se fija ~1 punto por debajo de lo medido: buffer estrecho pero no frágil (la
      // cobertura v8 es determinista sobre este set de tests) para que el gate FALLE
      // ante regresión real —código nuevo sin cubrir o borrado de módulos cubiertos—.
      // Objetivo pendiente (report-first, escalonado): seguir subiendo +5 pts/sprint
      // hasta 40–50% líneas. No subir por encima de lo medido sin volver a medir.
      // Ver GAPS.md (G-083).
      thresholds: {
        branches: 27,
        functions: 31,
        lines: 35,
        statements: 34,
      },
      include: ['src/**/*.ts', 'src/**/*.tsx'],
      exclude: ['src/**/*.test.ts', 'src/**/*.test.tsx', 'src/test-setup.ts'],
    },
  },
});
