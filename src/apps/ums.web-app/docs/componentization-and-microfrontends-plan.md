# UMS Web App Componentization and Micro-frontends Plan

## Goal

Keep the Progressive Monolith simple while making reusable UI, hooks, infrastructure adapters, and domain-specific features easy to evolve.

## Recommended Order

### 1. Establish Shared UI Foundations

Create a project-wide shared layer for primitives that have no business meaning:

- `presentation/shared/components`: buttons, fields, cards, dialogs, tables, splitters, tooltip, notification center.
- `presentation/shared/layouts`: app shell, route layout, empty/error/loading states.
- `presentation/shared/hooks`: viewport, disclosure, pagination, sorting, drag resize, keyboard shortcuts.
- `presentation/shared/accessibility`: ARIA helpers, focus management, keyboard navigation utilities.

Rules:

- Shared components must not import Identity, Tenant, Auth Provider, Branch, or any business model.
- Shared components receive data through props only.
- Shared hooks must not call business services directly.

### 2. Keep Feature Components Inside Bounded Contexts

Identity-specific components stay under:

- `presentation/identity/components`
- `presentation/identity/screens`
- `application/identity/hooks`
- `infrastructure/identity/services`
- `domain/identity/models`
- `domain/identity/schemas`

Examples:

- `TenantListPanel`
- `TenantDetailPanel`
- `TenantProfileCard`
- `BranchManager`
- `IdpPanel`
- `BrandingPanel`

Rules:

- Feature components may import shared components.
- Feature components may import their own bounded-context hooks and models.
- Feature components must not be moved into `shared` just because they look reusable visually.

### 3. Extract Controller Hooks From Screens

Screens should compose controllers and panels:

- `TenantDashboardScreen` should move state and orchestration into `useTenantDashboardController`.
- The screen should primarily wire layout:
  - left panel
  - splitter
  - right panel
  - modal form

Target:

- Screens: 100-180 lines.
- Panels: 100-300 lines.
- Shared primitives: small and stable.
- Business logic: controller hooks or application hooks.

### 4. Create Shared Data Contracts

Move common transport types to a reusable application area:

- `application/common/pagination.ts`
- `application/common/http-error.ts`
- `application/common/query-params.ts`

Use these for all bounded contexts:

- tenants
- users
- roles
- policies
- workflows
- notifications

### 5. Add Module Boundaries Before Micro-frontends

Before adopting Micro-frontends, enforce boundaries inside the monolith:

- path aliases per layer
- ESLint import boundaries
- one route entry per bounded context
- no cross-context imports except through public API barrels

Recommended public API pattern:

```text
presentation/identity/index.ts
application/identity/index.ts
domain/identity/index.ts
```

### 6. Micro-frontends Decision

Do not start with Micro-frontends for UMS MVP. Use them only when at least two of these are true:

- Multiple independent teams deploy different bounded contexts.
- Identity, Authorization, IGA, Audit, and Compliance have independent release calendars.
- Bundle size or route ownership becomes a real bottleneck.
- Runtime integration with external product modules is required.

Preferred future model:

- Shell app owns authentication, layout, navigation, i18n, observability, and shared design system.
- Bounded-context remotes expose routes or route modules.
- Shared contracts stay versioned and fixed.

Candidate split:

- `ums-shell`
- `ums-identity`
- `ums-authorization`
- `ums-iga`
- `ums-audit`
- `ums-compliance`

Recommended technology when needed:

- Vite Module Federation or Webpack Module Federation.
- Keep React, Router, Query Client, and design system as shared singleton dependencies.

## Current Near-term Backlog

1. Extract `useTenantDashboardController`.
2. Move splitter behavior into `presentation/shared/hooks/useResizablePanel`.
3. Move server pagination contract into `application/common/pagination.ts`.
4. Add import-boundary ESLint rules.
5. Add React Testing Library tests for `TenantListPanel` and `TenantDetailPanel`.
6. Document public API barrels per bounded context.
7. Evaluate Micro-frontends after MVP route boundaries are stable.
