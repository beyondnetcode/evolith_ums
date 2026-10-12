export default {
  extends: ['@commitlint/config-conventional'],
  rules: {
    // GIT-08 — Conventional Commits: type(scope): description
    'type-enum': [2, 'always', ["feat","fix","docs","style","refactor","test","chore"]],
  },
};
