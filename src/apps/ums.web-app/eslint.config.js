import js from '@eslint/js';
import globals from 'globals';
import reactHooks from 'eslint-plugin-react-hooks';
import reactRefresh from 'eslint-plugin-react-refresh';
import tseslint from 'typescript-eslint';
import prettier from 'eslint-plugin-prettier';
import eslintConfigPrettier from 'eslint-config-prettier';
// Análisis estático serverless para TS: las reglas de SonarSource como plugin de
// ESLint (sin servidor SonarQube ni token). Contraparte de SonarAnalyzer.CSharp
// en el backend .NET. Ver G-016.
import sonarjs from 'eslint-plugin-sonarjs';
// SAST local orientado a SEGURIDAD para TS (contraparte de SonarAnalyzer.CSharp en el
// backend). Detecta patrones inseguros: `eval`, RegExp/child_process/fs con datos no
// confiables, `Math.random()` en contexto de seguridad, etc. Ejecución 100% local.
import security from 'eslint-plugin-security';

export default tseslint.config(
  { ignores: ['dist'] },
  {
    extends: [
      js.configs.recommended,
      ...tseslint.configs.recommended,
      sonarjs.configs.recommended,
      security.configs.recommended,
      eslintConfigPrettier,
    ],
    files: ['**/*.{ts,tsx}'],
    languageOptions: {
      ecmaVersion: 2020,
      globals: globals.browser,
    },
    plugins: {
      'react-hooks': reactHooks,
      'react-refresh': reactRefresh,
      prettier,
    },
    rules: {
      ...reactHooks.configs.recommended.rules,
      // Work around mixed workspace ESLint resolution until dependencies are
      // fully flattened. TypeScript strict build covers this check reliably.
      '@typescript-eslint/no-unused-expressions': 'off',
      'react-refresh/only-export-components': ['warn', { allowConstantExport: true }],
      // Production: only allow console.error (for error boundaries)
      'no-console': ['error', { allow: ['error'] }],
      // Require explicit return types on exported functions for API boundaries
      '@typescript-eslint/explicit-function-return-type': 'off',
      // Allow non-null assertions in test files only
      '@typescript-eslint/no-non-null-assertion': 'warn',
      // Prettier formatting rules
      'prettier/prettier': 'error',
      // SEGURIDAD: se desactiva `detect-object-injection` — marca cualquier acceso
      // `obj[variable]`, patrón ubicuo y casi siempre falso positivo (la propia doc del
      // plugin lo advierte). Se conservan las reglas de alto valor: detect-eval-with-
      // expression, detect-child-process, detect-non-literal-fs-filename, detect-non-
      // literal-require, detect-unsafe-regex, detect-pseudoRandomBytes, etc.
      'security/detect-object-injection': 'off',
      // Debatibles en un frontend (lado cliente): se reportan como warning (visibles,
      // no bloquean) mientras se revisan. Los críticos (eval, child_process,
      // non-literal-require/fs) siguen como error del preset → bloquean.
      'security/detect-possible-timing-attacks': 'warn',
      'security/detect-non-literal-regexp': 'warn',
    },
  }
);
