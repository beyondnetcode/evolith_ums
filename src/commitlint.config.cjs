// Conventional Commits — validación LOCAL del formato del mensaje (hook commit-msg).
// Vive en src/ para que `extends` resuelva desde src/node_modules.
module.exports = { extends: ['@commitlint/config-conventional'] };
