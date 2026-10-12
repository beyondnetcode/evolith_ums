import { defineConfig } from 'vitest/config';

export default defineConfig({
  test: {
    globals: false,
    environment: 'node',
    include: ['sdk-*/tests/**/*.test.ts'],
    coverage: {
      provider: 'v8',
      include: ['sdk-*/src/**/*.ts'],
      exclude: ['sdk-*/src/index.ts']
    }
  }
});
