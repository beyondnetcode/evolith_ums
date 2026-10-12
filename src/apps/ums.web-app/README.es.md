# Consola Web de UMS

> Idioma: [English](./README.md) | [Español](./README.es.md)

UMS Web Console es el portal React 18 para la experiencia administrativa de User Management System. Es el frontend principal para operadores y administradores que gestionan tenants, autorizacion, configuracion, aprobaciones y flujos de auditoria.

## Enlaces Rapidos

| Necesidad                    | Abrir esto                                                              |
| ---------------------------- | ----------------------------------------------------------------------- |
| README raiz                  | [Resumen del repositorio](../../../README.md)                           |
| Portal documental en ingles  | [docs/README.md](../../../reference/indices/index.md)                   |
| Portal documental en espanol | [docs/README.es.md](../../../reference/indices/index.md)                |
| Portal de arquitectura       | [docs/architecture/index.es.md](../../../reference/architecture/index.md) |
| Portal de gobernanza         | [docs/governance/index.es.md](../../../reference/gobernanza/index.md)     |

## Vista General

| Area           | Decision                                               |
| -------------- | ------------------------------------------------------ |
| Runtime        | React 18 + Vite + TypeScript                           |
| Estado y datos | Zustand, TanStack Query                                |
| Estilos        | Tailwind CSS                                           |
| Proposito      | Portal administrativo para operadores y tenants de UMS |

## Flujo Local

Todos los comandos tecnicos deben ejecutarse desde `src/`.

```bash
cd src
npm install
npx nx run app-web:dev
```

## Notas

- Este README del modulo debe mantenerse corto y facil de navegar.
- El detalle de producto y arquitectura vive en `docs/`.
- La documentacion bilingue debe mantenerse sincronizada con el espejo en ingles.
