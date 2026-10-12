# UMS SDK — Changelog

All notable changes to the UMS SDK family (`.NET`, `TypeScript`, `NestJS`) are recorded here.
The schema versioning policy is described in [ADR-0074](../../../reference/architecture/adrs/UMS-074-versionado-esquema-grafo-autorizacion.es.md).

## [1.0.0] — Initial release

### Publicación (GitHub Packages)

El SDK existía, pasaba sus pruebas y no se podía instalar: `npm view` respondía
404 y no había workflow que publicara. `.github/workflows/publicacion.yml`
empaqueta, verifica y publica los seis paquetes en orden de dependencia, a mano
y en simulacro por defecto.

- `sdk-nestjs` declaraba sus dos dependencias como `file:`, que es un mecanismo
  de monorepo y no de distribución: quien instalara el paquete desde el registro
  recibiría una ruta que en su máquina no existe. Ahora las declara por versión.
- La ventana de compatibilidad de esquema del manifiesto decía `>=1.0.0 <2.0.0`
  mientras el código exige `2.0.0 ≤ x < 3.0.0` y el servidor emite `2.4.0`.
  Corregida.


### Contracts (`src/libs/sdk/contracts/`)
- `auth-graph.schema.json` — canonical JSON Schema 2020-12 of the `AuthorizationGraph` payload, schemaVersion `1.0.0`.
- `error-codes.yaml` — canonical catalog of `AUTH_xxx` codes (auth, IDP resolution, SDK authorization, graph lifecycle).
- `fixtures/` — 12 golden JSON fixtures (9 valid, 3 invalid) used as the executable contract across all runtimes.
- `SCHEMA_VERSIONING.md` — operational summary of the versioning policy.

### .NET (`Evolith.Ums.Sdk.*`, NuGet)
- `Evolith.Ums.Sdk.Contracts 1.0.0` — typed AuthorizationGraph DTOs mirroring the schema, `UmsErrorCodes` constants, `SchemaVersion` helpers.
- `Evolith.Ums.Sdk.Authorization 1.0.0` — pure `IAuthorizationValidator` (deny-wins, override, expiry, schema-compat), `IAuthGraphAccessor` port + `HttpContextAuthGraphAccessor` default, `Result/Result<T>`, `AuthorizationDecision`, `AuthorizationDeniedException`, `AuthorizationOptions` (Enforce/AuditOnly), DI extensions.
- `Evolith.Ums.Sdk.Authorization.Aop 1.0.0` — `[RequiresScope]`, `[RequiresMenuOption]`, `[RequiresDomainAccess]`, `[RequiresFeatureFlag]` attributes + `AuthorizationAspect` on top of `BeyondNetCode.Shell.Aop`. Configurable `DenialBehavior.Throw` (default) or `DenialBehavior.ReturnFailure`. Per-attribute audit-only override.
- `Evolith.Ums.Sdk.Authorization.Testing 1.0.0` — fluent `AuthGraphBuilder` and `TestAuthGraphAccessor` for unit testing consumer code without UMS.
- Test suite: 30/30 PASS against the 12 golden fixtures.

### TypeScript (`@evolith-ums/ums-sdk-*`, npm)
- `@evolith-ums/ums-sdk-contracts 1.0.0` — TypeScript types for the AuthorizationGraph, `UmsErrorCodes`, `SchemaVersion` with `isSchemaVersionSupported`/`isMinorAhead`/`isMinorBehind`/`isMajorMatch`.
- `@evolith-ums/ums-sdk-authorization 1.0.0` — `AuthorizationValidator`, `AuthGraphAccessor` port + `AsyncLocalAuthGraphAccessor` (Node) and `MemoryAuthGraphAccessor` (browser), HOF primitives (`requireScope`, `requireMenuOption`, `requireDomainAccess`, `requireFeatureFlag`), TC39 Stage-3 decorators, `Result` discriminated union, audit-only mode, `configureAuthorization`.
- `@evolith-ums/ums-sdk-testing 1.0.0` — fluent `AuthGraphBuilder` and `TestAuthGraphAccessor`.
- Test suite: 33/33 PASS across the three packages using the same golden fixtures.

### NestJS (`@evolith-ums/ums-sdk-nestjs`, npm)
- `@evolith-ums/ums-sdk-nestjs 1.0.0` — `UmsSdkModule.forRoot()` / `.forRootAsync()`, `UmsAuthGuard` (`CanActivate`), Nest-style decorators (`@RequiresScope`, etc.), `AuthorizationDeniedFilter` mapping to HTTP 403, `AuthGraphMiddleware` binding the accessor per request.
- Test suite: 7/7 PASS end-to-end via `@nestjs/testing` + `supertest`.

### Documentation
- ADR-0073 — UMS SDK Multi-Runtime Client Integration Surface.
- ADR-0074 — Auth Graph Schema Versioning Policy.
- `docs/sdk/` and `docs/sdk-es/` — bilingual portal: contracts, three runtime guides, quickstarts.

### Contract parity
- The 12 fixtures in `src/libs/sdk/contracts/fixtures/` are exercised by every SDK runtime and produce identical authorization decisions.

## Compatibility

| Schema → / Runtime ↓ | 1.0.0 |
|---|---|
| `Evolith.Ums.Sdk.* 1.0.x` | ✅ |
| `@evolith-ums/ums-sdk-* 1.0.x` | ✅ |
| `@evolith-ums/ums-sdk-nestjs 1.0.x` | ✅ |

Full matrix at [`docs/sdk/contracts/compatibility-matrix.md`](../../../reference/sdk/contracts/matriz-compatibilidad.md).
