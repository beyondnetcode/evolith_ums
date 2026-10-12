import { defineConfig } from 'vitest/config';
import swc from 'unplugin-swc';

export default defineConfig({
  esbuild: false,
  oxc: false,
  plugins: [
    // SWC plugin compiles TypeScript with decorators + emitDecoratorMetadata,
    // which Vitest's default esbuild transformer does not support.
    swc.vite({
      module: { type: 'es6' },
      jsc: {
        target: 'es2022',
        parser: { syntax: 'typescript', decorators: true },
        transform: { legacyDecorator: true, decoratorMetadata: true }
      }
    })
  ],
  test: {
    globals: false,
    environment: 'node',
    include: ['sdk-*/tests/**/*.test.ts']
  }
});
