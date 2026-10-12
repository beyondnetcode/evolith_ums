const js = require('@eslint/js');

module.exports = [
  {
    ignores: ['node_modules/**'],
  },
  {
    files: ['**/*.js'],
    languageOptions: {
      ecmaVersion: 2022,
      sourceType: 'commonjs',
      globals: {
        ...require('globals').node,
      },
    },
    rules: js.configs.recommended.rules,
  },
];
