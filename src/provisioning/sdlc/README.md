# Alta del Tablero SDLC en UMS — inventario y mapeo

> **Estado:** Borrador · **Tenant:** `EVOLITH` · **Sistema:** `SDLC` · **Última extracción:** 2026-08-01

Este directorio contiene la **especificación declarativa** que da de alta el Tablero de Gobierno SDLC como sistema configurado en UMS: navegación, acciones, objetos de dominio, roles y plantillas de permiso.

* Spec: [`sdlc-suite.json`](./sdlc-suite.json)
* Origen de los datos: `evolith_arch` → `reference/governance/tablero-ejecutivo/app`
* Deuda asociada: **G-166** en [`GAPS.md`](../../../GAPS.md) — hoy la extracción es artesanal; debería ser una capacidad de importación de UMS.

---

## 1. Qué se migra

| Origen (Tablero SDLC) | Destino (modelo de UMS) | Volumen |
| :--- | :--- | ---: |
| Aplicación completa | `SystemSuite` (`code = SDLC`) | 1 |
| Áreas funcionales de la navegación | `Module` | 6 |
| Entradas de menú | `MenuNode` (`Kind = Menu`) | 12 |
| Agrupadores / pestañas | `MenuNode` (`Kind = SubMenu`) | 21 |
| Pantallas y paneles operables | `MenuNode` (`Kind = Option`) | 36 |
| Verbos de operación | `Action` de la suite | 13 |
| Vínculos opción ↔ acción (N:M, ADR-0090) | `MenuNode ↔ Action` | 85 |
| Tablas de la base del Tablero | `DomainResource` | 1 (ver §7.1) |
| Parámetros de operación | `AppSetting` | 4 |
| Cargos que operan el tablero | `Role` | 7 |
| Matrices de permiso por rol | `PermissionTemplate` | 7 |
| Personas del padrón × sucursal | `Profile` | 12 (ver §8) |

## 2. Trazabilidad de la extracción (SD-05)

Las tres fuentes de evidencia y su cobertura verificada:

| Fuente | Extraído de | Cobertura |
| :--- | :--- | :--- |
| **96 rutas de API** | `app/server/src/**` (`app.get\|post\|put\|patch\|delete`) | 92/96 mapeadas a una opción; las 4 restantes (`/health`, `/ready`, `/api/health`, `/metrics`) se declaran **no navegables** en `origen.rutasNoNavegables`: son endpoints de infraestructura, no pantallas |
| **22 tablas** | `CREATE TABLE` en `app/server/src/db/**` | 22/22 mapeadas a un `DomainResource` |
| **7 vistas de navegación** | `app/web/src/App.jsx`, `components/common.jsx` (Sidebar), `CommandBar.jsx` | Descompuestas en 6 módulos (la vista `detalle` se abre en 4 menús: planificación, artefactos, equipo/riesgos, evolución) |

Reproducir la verificación de cobertura:

```bash
node -e 'const s=require("./src/provisioning/sdlc/sdlc-suite.json");const r=new Set();for(const m of s.modules)for(const me of m.menus)for(const sm of me.submenus)for(const o of sm.options)(o.routes||[]).forEach(x=>r.add(x));console.log(r.size+" rutas mapeadas")'
```

## 3. Mapa de módulos

| Módulo | Nombre | Origen en el Tablero |
| :--- | :--- | :--- |
| `GOB` | Gobierno de Satélites | vista `satelites` (+ `SistemasPanel`, `EquipoSistemaPanel`, `SyncBar`) |
| `PORT` | Portafolio Ejecutivo | vista `portafolio` (`PortfolioView`) |
| `PRD` | Iniciativas (PRD) | vistas `prds` y `detalle` (`Planificacion`, `KanbanFases`, `Gantt`, `FaseDetalle`, `GateModal`, `EquipoPanel`, `RiesgosPanel`, `EvolucionPlan`, `Bitacora`) |
| `DEM` | Demandas inter-sistema | vista `demandas` (`DemandasBoard`, `KpisDemanda`, `EjecucionEditor`) |
| `PER` | Personas y capacidad | vista `personas` (`PersonaDialog`, asignaciones) |
| `CFG` | Configuración del Tablero | vista `config` (`JornadaConfig`, `ArtefactosConfig`) |

## 4. Catálogo de acciones

`VIEW`, `CREATE`, `UPDATE`, `DELETE`, `SEARCH`, `APPROVE`, `EXECUTE`, `SYNC`, `IMPORT`, `EXPORT`, `CERTIFY`, `UPLOAD`, `ASSIST`.

Las seis primeras son las estándar de la suite; las siete restantes existen porque el Tablero tiene operaciones que no son CRUD y que **deben** poder concederse por separado: sincronizar con la fuente, importar un PRD, ejecutar un proceso de plan (regularizar fechas, restaurar versión, snapshot de KPIs), aprobar el cierre de fase, certificar un artefacto, adjuntar archivos e invocar la asistencia IA (ADR-0145).

## 5. Roles

Son **7**, y llevan el nombre de los cargos de Evolith y no el de las funciones del ciclo de vida
(`bc89b1a`). Los ocho anteriores —`PMO`, `ARQUITECTO`, `PRODUCT_OWNER`, `TECH_LEAD`, `EQUIPO`,
`DIRECTORIO`— ya no existen.

| Rol | Alcance |
| :--- | :--- |
| `ADMIN_SDLC` | Todo el sistema |
| `GERENTE` | Lectura ejecutiva del portafolio; **concentra** `APPROVE` y `CERTIFY` |
| `AUDITOR` | Solo lectura completa (`VIEW`/`SEARCH`/`EXPORT`); ninguna escritura |
| `COORDINADOR` | Cronograma, capacidad y demanda; único no-administrador que escribe en `GOB` |
| `JEFE` | Ejecución del plan de fases; equipo, riesgos y artefactos |
| `ANALISTA` | Análisis y redacción sobre el PRD; no ejecuta fases ni cierra gates |
| `DESARROLLADOR` | Borradores, revisión y riesgos; lectura del resto |

Lo que sostiene el modelo no es el vocabulario sino la **diferenciación**: quien lleva el cronograma
(`COORDINADOR`) **no** tiene `CERTIFY`, que vive en `GERENTE`. Verificado por simulación sobre la
spec: ninguna pareja de los 7 comparte el mismo conjunto de ítems.

Cada rol tiene su `PermissionTemplate` anclada (`PLT_<ROL>`), expresada en la spec como `grants` con granularidad de suite, módulo, menú, submenú u opción, y con `effect: "deny"` cuando hay que recortar una concesión más amplia.

## 6. Vista de consumo

[`render-auth-graph.mjs`](../render-auth-graph.mjs) proyecta la spec en la respuesta de `POST /api/v1/auth/login` — un archivo por rol en [`auth-graph/`](./auth-graph). La forma se tomó del código de la API (`Domain/Authorization/Graph/*.cs`, `AuthorizationGraphBuilderService`, `LoginSuccessResponse`), **no** del contrato publicado en `libs/sdk/contracts/`, que diverge: ver **G-167** en [`GAPS.md`](../../../GAPS.md).

Rasgos que conviene recordar al leer esos JSON:

* Dentro de `authorizationGraph` no viaja **ningún `id`** — los `Guid` están marcados `[JsonIgnore]`. Los ids que ve el cliente son los del envoltorio (`userId`, `tenantId`, `profileId`).
* Todo nombre o etiqueta se serializa como **`value`**.
* `menuAccess` incluye **los 6 módulos siempre**, aunque el rol no tenga una sola opción concedida; las opciones `NotGranted` también viajan. El cliente decide si oculta o deshabilita.
* `domainPermissions` enumera **las 13 acciones por cada uno de los 22 recursos** (286 filas), resueltas *fail-closed*: sin item de plantilla, `NotGranted` (G-039).
* `scopes` va en minúsculas, deduplicado y ordenado.

## 7. Criterios de recorte del árbol

Cuatro decisiones que separan la spec de una transcripción literal de la UI:

1. **Los paneles de una misma pantalla no son opciones distintas.** El dashboard ejecutivo pinta salud, alertas y KPIs desde una sola llamada (`GET /api/prds`); son una opción, `PORT_DASHBOARD`, con sus paneles anotados. Tres opciones habrían prometido una granularidad de permiso que el Tablero no puede cumplir.
   Al revés también vale, y costó descubrirlo: **dos pantallas distintas no son una opción**. `PORT_DASHBOARD` gobernaba además el backlog del área —actividades, bitácora, contexto y tipos de tarea—, así que recortar el informe ejecutivo a un perfil le apagaba de paso una pantalla operativa que nadie había decidido recortarle. El backlog tiene nodo propio desde el 2026-08-25: `PRD > PRD_PLANIFICACION > PLAN_BACKLOG > PLAN_TAREAS`. El criterio es **quién lee la pantalla**, no de qué recurso cuelga la ruta.
2. **Lo que no se navega no cuelga del menú.** `/health`, `/ready` y `/metrics` son endpoints de operación; quedan fuera del árbol y declarados en `origen.rutasNoNavegables`, para que el inventario siga siendo exhaustivo sin ensuciar la navegación.
3. **Las bajas irreversibles se conceden aparte.** `SAT_BAJA` y `SIS_BAJA` salen de sus listados: hoy solo `ADMIN_SDLC` las tiene. Dar de baja un satélite no puede viajar en el mismo permiso que verlo.
4. **Restaurar no es consultar.** `EVO_RESTAURAR` sale de la pantalla de comparación; la acción más destructiva del módulo deja de estar escondida en una vista de solo lectura. La tienen `ADMIN_SDLC`, `PMO` y `PRODUCT_OWNER`.

Se mantuvo en cambio la profundidad uniforme Menú→Submenú→Opción, aunque algunos submenús no agrupen nada: es la forma que ya tienen los otros seis sistemas del tenant EVOLITH, y romperla haría que el Tablero se viera distinto en la administración de permisos sin ganar nada.

### 7.1 Un objeto de dominio se declara cuando hay código que lo consulta

Esta spec declaró en su día los **22** objetos de dominio del Tablero —agregados y entidades, uno por tabla— y `PLT_ADMIN_SDLC` los concedía todos por comodín. Hoy declara **uno**, `PRD`, y el administrador no los concede por comodín. El criterio, para que nadie los vuelva a añadir por inercia:

> Un `DomainResource` entra en la spec **cuando existe código que lo consulta**. No antes.

Las cuatro razones, medidas el 2026-08-15 y no supuestas:

1. **Nadie los consultaba.** El Tablero autoriza con los 85 scopes del árbol de menú; su inventario (`shared/src/codigos-ums.js`) rechaza cualquier scope fuera de esos 85, y `denegadoEnDominio` no dispara nunca porque UMS poda lo no concedido en vez de emitir `Deny`.
2. **Duplicaban el modelo en dos sistemas.** En DDD la invariante vive en el agregado: «no se cierra un gate sin evidencia» es lógica de dominio, no una fila de permisos. Enumerar `ARTEFACTO_BORRADOR_VERSION × 13 acciones` aquí no añade una regla — añade un inventario que hay que mantener sincronizado con el modelo y que envejece en silencio al renombrar una entidad.
3. **El costo era medible y rompió el login.** 22 objetos × 13 acciones = **286 scopes**, que son 221 de los 407 items de la plantilla del administrador. UMS emite un claim `scope` por permiso, así que el token de `ADMIN_SDLC` llegaba a ~11.500 caracteres contra el límite de **4.096** que los navegadores imponen a una cookie: la sesión no se sostenía y el login respondía 200 sin dejar entrar.
4. **Parecían un control y no lo eran.** Un auditor que lee «ARTEFACTO: create/update/delete concedido a ADMIN» concluye que hay autorización a nivel de objeto. No la había: nada evaluaba esas filas. Un candado dibujado es peor que ninguno.

**`PRD` se conserva a propósito**, como ejemplo ejercitable: mantiene vivo el mecanismo —y las pruebas que lo recorren— sin arrastrar una copia del modelo entero.

**Cuando haga falta uno de verdad** —alcance por sucursal, delegación, una excepción sobre un objeto concreto— se declara ese y solo ese, y preferiblemente como política sobre atributos («ve los PRD de su sucursal») antes que como una fila por objeto × acción, que es la forma que envejece mal.

Los 21 retirados **siguen existiendo en UMS**: se retiran de la declaración, no se borran. Dejar de concederlos no destruye el rastro de quién los tuvo.

## 8. El padrón: quién entra y con qué

Un `Profile` de UMS liga *usuario ↔ rol ↔ **sucursal***, y su unicidad es esa terna completa
([`CreateProfileCommandHandler`](../../apps/ums.api/Evolith.Ums.Application/Authorization/Profile/Commands/CreateProfileCommandHandler.cs)).
Dos consecuencias que no son evidentes y que gobiernan la forma del bloque `profiles` de la spec:

* **Una persona en dos sucursales son dos perfiles**, no un perfil con dos sedes. Las seis personas
  del padrón, en Callao y Paita, son **12 perfiles** en SDLC y otros 12 en UMS.
* **Una persona tiene una sola cuenta y una sola contraseña.** UMS y el Tablero son dos suites del
  mismo inquilino; lo que cambia entre sistemas es el perfil, no la credencial.

### 8.1 Las sucursales llevan el código que ya tenían

`UNI_CALLAO` («Operaciones Callao») y `UNI_PAITA` («Sucursal Paita»), que es como las siembra
[`IdentityDevDataSeeder`](../../apps/ums.api/Evolith.Ums.Infrastructure/Persistence/Seeders/IdentityDevDataSeeder.cs)
por FS-25. Declarar `CALLAO` y `PAITA` a secas no las habría corregido: habría creado dos más.

Y no habría vuelta atrás. La baja de una sucursal es un **cierre lógico** (ADR-0164): la fila
permanece y **el código queda ocupado para siempre**. Un código equivocado no se corrige, se
arrastra. Por eso el cargador lee el catálogo antes de crear nada, aborta si no puede leerlo, y en
`--dry-run` anuncia el alta en vez de ejecutarla.

### 8.2 Cómo se aplica

```bash
UMS_URL=... UMS_USER=... UMS_PASS=... UMS_PAUSA_MS=700 \
  node src/provisioning/cargar-en-ums.mjs --spec src/provisioning/sdlc/sdlc-suite.json --dry-run
```

Dos banderas gobiernan el padrón, y son distintas porque el riesgo lo es:

| Bandera | Qué hace | Qué pisa |
| :--- | :--- | :--- |
| `--crear-cuentas` | Da de alta a quien falte, la activa y le pone su **primera** contraseña | Nada |
| `--fijar-claves` | **Rota** la contraseña de quien **ya existía** | La contraseña de una persona |

Una cuenta recién creada recibe credencial **siempre**, sin depender de `--fijar-claves`: nace
`Pending` y sin contraseña, así que crearla y no dársela produce una cuenta con perfil, con
plantilla y sin poder entrar — con todos los HTTP en verde.

`--fijar-claves` sí va detrás de una bandera porque el cargador se reejecuta a menudo —cada
corrección de permisos es una pasada más— y una rotación por omisión dejaría fuera de su cuenta a
gente que no lo pidió. La tabla final distingue ambos casos en su columna `CUENTA`.

En el servidor de UAT las tres pasadas están envueltas en
[`scripts/cargar-padron-uat.ps1`](../../../scripts/cargar-padron-uat.ps1).

Es **para UAT**. Una contraseña derivable del correo es reproducible, que es justo lo que se quiere
para repartir accesos de prueba y justo lo que no se quiere en producción: ver **G-271** en
[`GAPS.md`](../../../GAPS.md).

## 9. Decisiones pendientes

1. **Sigla `SDLC` fuera del catálogo de sistemas de la suite.** El Tablero no figura en [`catalogo-sistemas-suite.es.md`](https://github.com/beyondnetcode/evolith_arch32/blob/main/reference/architecture/catalogo-sistemas-suite.es.md), cuyo patrón es `?MS`. Usarlo en configuración exige ADR en `evolith_arch` (S-06) o el alta formal en el catálogo.
2. **Quién debería dar de alta a las personas.** Desde ahora el cargador puede hacerlo con `--crear-cuentas`, y es lo que se usa en UAT. Sigue abierto si esa es la vía correcta en producción o si el alta debe venir de la mesa de accesos o del IdP: un guion de configuración que crea personas es cómodo y es exactamente el tipo de atajo que luego nadie recuerda haber tomado. Sin la bandera, una cuenta ausente vuelve a ser un fallo.
