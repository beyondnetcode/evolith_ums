# Auth Graph Schema Versioning — Operational Summary

> Operational summary inside the source tree. The full architectural decision is in
> [ADR-0074](../../../../reference/architecture/adrs/UMS-074-versionado-esquema-grafo-autorizacion.es.md).
> Developer-facing guide: [`docs/sdk/contracts/versioning.md`](../../../../reference/sdk/contracts/versionado.md).

## Errata 2026-08-01 — el esquema no describía la realidad (G-167)

Hasta esta fecha `auth-graph.schema.json` declaraba una forma que **ningún endpoint emitió jamás**: `id` obligatorios en cada nodo, nombres bajo `name`/`label`, y envoltorios `{ "module": … }` y `{ "resource": … }`. La API emitía otra —`value` para todo nombre, listas planas, ids omitidos— y además no una sino **dos**, porque el login web serializaba el record de dominio y el endpoint de cliente proyectaba su propio modelo.

Se corrigió como **errata sobre `1.0.0`, sin bump**: no hubo ruptura de compatibilidad porque no existía un `1.0.0` funcional que romper — un cliente que hubiera integrado leyendo el contrato publicado recibía `undefined` en cada nombre y en cada id. Lo que cambió es la descripción, no el comportamiento; y donde sí cambió el comportamiento fue para **eliminar la divergencia**: hoy ambos endpoints proyectan con el mismo mapeador (`AuthGraphPayload`), que además corrige tres defectos del emisor de clientes (faltaba `sortOrder`, `resourceCode` se llamaba `code`, y `mfaAllowedMethods` no viajaba).

La regla que evita la recaída: `Evolith.Ums.Application.Test/Authorization/Graph/AuthGraphPayloadTests.cs` compara la proyección real contra las claves del contrato. Si divergen, falla la construcción. Ver [D-025](../../../../DECISIONS.md) y G-167 en [GAPS.md](../../../../GAPS.md).

## Current State

- **Versión del esquema según la FORMA de la respuesta** — son dos, y cuál toca no lo elige el
  servidor:
  - respuesta **COMPLETA** (sin `?include=`, el caso de todos los satélites de hoy): `2.4.0`, sin
    cambios;
  - respuesta **PROYECTADA** (con `?include=`): `2.5.0`.

  El documento publicado es el `2.5.0` y describe las dos: `enum: ["2.4.0", "2.5.0"]` en
  `properties`, con un `const` por rama del `allOf` que fija cuál corresponde. Así un consumidor
  que compare por IGUALDAD sigue teniendo un valor único que comprobar para la respuesta que
  recibe.
- **Rango de compatibilidad de los SDK:** `[2.0.0, 3.0.0)`

  > Deriva conocida, aún sin resolver: los `.csproj` y los `package.json` de los SDK declaran
  > `UmsSchemaCompatibility >=1.0.0 <2.0.0`, mientras el CÓDIGO exige `[2.0.0, 3.0.0)`. Hoy son
  > metadatos muertos —nadie los lee— y por eso nada falla; el día que algo los lea, rechazará
  > todo. Se deja escrito para que el próximo que los mire sepa que están mal a propósito y no
  > los tome por la verdad.
- **Historial:**
  - `1.0.0` — release inicial.
  - `1.1.0` (2026-08-01) — bloque `profiles` con los perfiles activos del usuario. Aditivo, por eso MINOR.
  - `2.0.0` (2026-08-01) — **MAJOR**. Dos cambios que rompen a cualquier consumidor:
    1. **Árbol de navegación recursivo.** `menuAccess[].menus[].subMenus[].options[]` pasa a
       `menuAccess[].nodes[].children[]`, con las acciones agrupadas en el nodo. La forma anterior
       tenía tres niveles fijos y perdía en silencio los nodos que no encajaban en ella (G-171).
    2. **Solo viaja lo alcanzable.** Las filas `NotGranted` desaparecen del cable, en navegación y
       en objetos de dominio: la ausencia ya significaba denegación (fail-closed, G-039), así que
       enviarlas era repetir en cada login lo que el contrato dice una vez. Medido sobre capturas
       reales: el perfil más restringido baja de 42,0 a 9,5 KB.

    `Deny` sí sigue viajando: no es lo mismo que la ausencia — gana sobre cualquier concesión
    heredada y el cliente debe poder pintarlo como bloqueado, no como inexistente.
  - `2.1.0` (2026-08-01) — bloque `settings`: los ajustes del sistema marcados como visibles para
    el cliente, agrupados por espacio de nombres (`settings.brand.logo_url`, `settings.ui.home_route`,
    `settings.locale.language`…). Aditivo, por eso MINOR.

    **Es el punto de extensión del contrato.** Es el único sitio con `additionalProperties: true`:
    añadir un espacio de nombres nuevo —notificaciones, informes, integraciones— no cambia la forma
    del payload ni obliga a un bump. El resto del esquema sigue cerrado, que es donde la validación
    estricta protege.
  - `2.2.0` (2026-08-01) — `icon` y `route` en cada nodo de navegación. Aditivo, por eso MINOR.
    Sin ellos el cliente sabía qué texto poner en el menú, pero no qué icono pintar ni a dónde
    llevar al usuario: la promesa de «inicializar sin llamadas adicionales» no se sostenía.
    `icon` es un IDENTIFICADOR, no un recurso — el catálogo gráfico lo resuelve el cliente;
    guardar una URL o un SVG ataría el servidor a la biblioteca de iconos de un frontend concreto.
  - `2.3.0` (2026-08-01) — `icon` en el módulo. Aditivo, por eso MINOR. El módulo era el único
    nivel de la navegación sin presentación propia: el cliente sabía cómo se llamaba pero no con
    qué pintarlo, y acababa resolviendo el icono por código, que es la tabla estática que el grafo
    vino a eliminar (G-181, G-182).
  - `2.4.0` (2026-08-02) — `accessState` como discriminador cerrado del estado de acceso,
    `context.requestedSystem` como eco del sistema pedido, y `profiles[].id` siempre presente.
    Aditivo, por eso MINOR. Ver ADR-0156. (Esta entrada faltaba: el esquema llevaba en 2.4.0
    desde entonces y este documento seguía diciendo 2.3.0.)
  - `2.5.0` (2026-08-15) — **proyección opcional del grafo**. `GET /api/v1/client/graph` acepta
    `?include=` con un vocabulario cerrado —`permissions`, `navigation`, `profiles`, `features`,
    `config`, `settings`— y devuelve solo esos bloques; el eco de lo aplicado viaja en la clave
    nueva `projection`. Aditivo, por eso MINOR, con tres matices que conviene no perder:

    1. **Sin `?include=` no cambia nada.** La clave `projection` no se emite y el `required` de las
       quince claves clásicas sigue vigente palabra por palabra, ahora expresado con `if/then`
       sobre la presencia de `projection`. Ningún satélite que no use el parámetro puede observar
       una diferencia de comportamiento.

    2. **Un bloque no pedido se OMITE, jamás llega vacío**, y el esquema lo exige por las dos
       caras: si el token está, sus secciones son obligatorias; si no está, se prohíben. La mitad
       que se olvida es la segunda, y es la que importa: `domainPermissions: []` y «no lo pedí» son
       indistinguibles para el evaluador de deny-wins —ante los dos concluye que no hay ninguna
       denegación explícita y CONCEDE—, así que permitir el array vacío como sustituto de la
       ausencia dejaría la regla declarada y sin ninguna defensa observable.

    3. **`permissions` es atómico**: trae `scopes` y `domainPermissions` juntos y no existe token
       para pedir una sola de las dos mitades. En la RESPUESTA lo verifica el esquema; en la
       PETICIÓN lo garantiza el vocabulario, que es la única defensa real — un esquema no puede
       impedir que alguien pida mal, solo que le respondan mal.

    **Sin excepciones a «no cambia nada», y esta parte se corrigió sobre la marcha.** El borrador
    de este cambio subía el `const` de `schemaVersion` a `2.5.0` TAMBIÉN en las respuestas
    completas, y lo declaraba inevitable e inocuo. Era ninguna de las dos cosas:

    - **No era inevitable.** Un mismo servidor sí puede declarar dos versiones según la petición,
      y un `const` sí puede expresarlo: basta condicionarlo a la presencia de `projection`. Es lo
      que hacen ahora las dos ramas del `allOf` —`const: 2.4.0` en la de respuesta completa,
      `const: 2.5.0` en la proyectada— con `enum: ["2.4.0","2.5.0"]` en `properties` para que las
      dos sean valores legales del documento.
    - **No era inocuo.** El argumento era que «los cuatro verificadores conocidos comprueban RANGO,
      no igualdad». Un satélite con este mismo fichero VENDORIZADO en su versión 2.4.0 valida
      contra su `const`, que compara por igualdad: habría empezado a rechazar TODAS las respuestas
      el día del despliegue, sin haber pedido jamás una proyección. Y «los verificadores
      conocidos» no es una propiedad del contrato sino un censo — en este mismo repositorio hay
      uno que compara por igualdad, `src/tests/e2e-functional/robosoft/contexts/configuration.py`.

    Regla que queda: **la versión sube cuando cambia lo que se EMITE, no cuando se despliega
    código nuevo.** Quien no envía `?include=` recibe hoy exactamente los mismos bytes que ayer,
    token de versión incluido; los fixtures dorados siguen en `2.4.0` y validan sin tocarlos.

    **Orden de despliegue, no negociable:** los SDK con las guardas de nulos —`GraphIndex` en .NET
    y en TS, que hasta ahora reventaban con `NullReferenceException` / `TypeError` ante una sección
    ausente— deben estar publicados y desplegados ANTES de que UMS responda la primera petición
    proyectada. Si se invierte, basta un `curl` con `?include=` contra un entorno compartido para
    tumbar a un satélite.
- **Schema file:** [`auth-graph.schema.json`](./auth-graph.schema.json)
- **Error codes catalog:** [`error-codes.yaml`](./error-codes.yaml)
- **Golden fixtures:** [`fixtures/`](./fixtures/)

## Bump Decision Cheatsheet

| Change | Bump | Example |
|---|---|---|
| Add optional field | MINOR | New `context.user.locale` optional string |
| Add new top-level section | MINOR | New `delegations` array |
| Add new enum value to open enumeration | MINOR | New `IdpStrategyHint` value |
| Remove required field | **MAJOR** | Drop `effectiveConfig.minPasswordLength` |
| Rename field | **MAJOR** | `tenant.name` → `tenant.legalName` |
| Narrow type | **MAJOR** | `string` → `enum` for an existing field |
| Change semantics | **MAJOR** | Flip Allow/Deny precedence rule |
| Documentation-only edit | PATCH | Clarify a `description` |

When in doubt, bump higher.

## Per-Change Workflow

1. Update `auth-graph.schema.json` — bump the `schemaVersion` `const` value to match the change category.
2. Update `error-codes.yaml` if new codes are introduced.
3. Add or update fixtures under `fixtures/` covering the new behavior.
4. Add an entry to `docs/sdk/contracts/compatibility-matrix.md`.
5. For MAJOR or removals: open an ADR amendment documenting the migration path.
6. Run CI locally: contract validation, round-trip through all SDKs.

## Deprecation Cycle

1. **MINOR N**: Add replacement field, mark old as `"deprecated": true` in schema, add target removal version via `"x-deprecation-removed-in"`.
2. **MINOR N+1**: SDKs emit `DeprecatedFieldUsageEvent` on read.
3. **MINOR N+2+**: Continue emitting events. Minimum window: 2 MINORs / 6 months.
4. **MAJOR N+1**: Remove. Servers stop emitting. SDK majors required to upgrade.
