# Arnés RoboSoft — certificación E2E funcional de UMS

Fundación **durable y versionada** de la certificación funcional de extremo a extremo de
UMS (G-112). RoboSoft es un cliente externo simulado que **aprovisiona su propia data**
con códigos únicos por corrida y **verifica invariantes** contra el backend vivo,
reportando PASS/FAIL por invariante con evidencia real (código HTTP + cuerpo).

> Este arnés existe porque su ausencia fue la causa raíz de una pérdida previa: se corría
> de forma efímera y no quedaba versionado. Aquí vive en el repositorio, se alimenta solo
> y crece con el sistema. **No lo borres; extiéndelo.**

- Sin dependencias externas: **solo la biblioteca estándar de Python 3** (`urllib`,
  `http.client`, `json`). Requiere Python **>= 3.8**.
- Idempotente y re-corrible: cada corrida usa un `run_id` con sufijo único, de modo que
  re-ejecutar no colisiona (409) contra data previa. No destruye la semilla.
- Determinista: el orden y la lógica de los checks son estables; con `--seed` se fija el
  `run_id` para reproducir una corrida (útil en depuración, no para re-ejecutar en serie).

## Estructura

```
src/tests/e2e-functional/robosoft/
├── robosoft.py           # runner CLI (punto de entrada)
├── harness.py            # framework: HTTP, invariantes/resultados, provisión, reporter
├── contexts/             # un módulo por contexto acotado (11)
│   ├── authn_local.py            # IMPLEMENTADO (se corre en vivo)
│   ├── robosoft_core.py          # IMPLEMENTADO (se corre en vivo)
│   ├── audit.py                  # IMPLEMENTADO (se corre en vivo)
│   ├── transversal_security.py   # esqueleto (ola 2)
│   ├── authorization_graph.py    # esqueleto (ola 2)
│   ├── authorization_topology.py # esqueleto (ola 2)
│   ├── configuration.py          # esqueleto (ola 2)
│   ├── authn_federated.py        # esqueleto (ola 2)
│   ├── refresh_token.py          # esqueleto (ola 2)
│   ├── approvals_compliance.py   # esqueleto (ola 2)
│   └── iga.py                    # esqueleto (ola 2)
└── README.md             # este archivo
```

Cada módulo de contexto expone tres símbolos:

- `NAME` — nombre del contexto (coincide con la auditoría).
- `INVARIANTS` — catálogo de invariantes `{code, fr, title}`. **Es la fuente de verdad de
  la matriz de certificación** (`reference/qa/e2e-certification-matrix.md`): cada `INV-*`
  mapea a un check ejecutable.
- `run(rc)` — ejecuta los checks y devuelve una lista de `Result`.

## Cómo correrlo

### 1) Backend vivo en kind

```bash
# Port-forward al backend desplegado en el clúster local evolith-cluster-ums:
kubectl --context kind-evolith-cluster-ums -n ums \
    port-forward svc/ums-backend 18080:80 &
# Verifica salud:
curl -s -o /dev/null -w "%{http_code}\n" http://localhost:18080/health   # -> 200
```

### 2) Ejecutar

```bash
cd src/tests/e2e-functional/robosoft

# Los 3 contextos implementados (por defecto):
python3 robosoft.py --base http://localhost:18080

# Un contexto concreto (repetible):
python3 robosoft.py --context audit
python3 robosoft.py --context authn_local --context robosoft_core

# Los 11 contextos (incluye esqueletos, que reportan PENDING):
python3 robosoft.py --all

# Listar contextos y su estado:
python3 robosoft.py --list

# Salida JSON (para CI o auto-alimentar la matriz):
python3 robosoft.py --json > resultado.json

# Reproducir una corrida (fija el run_id):
python3 robosoft.py --seed 42
```

La URL base también se puede fijar con la variable de entorno `ROBOSOFT_BASE`.

### Códigos de salida

| Código | Significado                                             |
|-------:|--------------------------------------------------------|
| `0`    | ningún invariante FAIL                                  |
| `1`    | al menos un invariante FAIL (hallazgo candidato a gap)  |
| `2`    | error de arranque (backend inalcanzable, login fallido) |

### Estados por invariante

| Estado    | Significado                                                             |
|-----------|-------------------------------------------------------------------------|
| `PASS`    | el invariante se cumple contra el binario vivo                          |
| `FAIL`    | no se cumple — **candidato a gap** (evidencia adjunta)                  |
| `SKIP`    | no ejercitable en caja negra (p. ej. requiere fuente .NET de evolith_arch)|
| `BLOCK`   | bloqueado por una precondición no satisfecha en runtime                 |
| `PENDING` | sin data/semilla para ejercerlo en vivo, o contexto esqueleto (ola 2)   |

## Modelo de provisión (verificado contra el binario)

RoboSoft opera **on-behalf** bajo el inquilino **management-owner EVOLITH**. Puntos clave,
confirmados contra `0.1.0-pilot`:

- **Login admin:** `POST /api/v1/auth/login` con
  `{tenantCode: "EVOLITH", username: "admin@evolith.example", password: "Evolith.Dev.2026"}`
  → 200. El campo del token es **`token`** (Bearer, JWT HS256, claim `is_internal_admin: true`).
- **On-behalf:** las operaciones de provisión adjuntan la cabecera **`X-Is-Internal-Admin: true`**
  (además del Bearer). Con ella, un CLIENT tenant **sí** puede provisionarse por API
  (H-04 resuelto, ver ADR-UMS-092).
- **Ids de creación:** `tenants`, `branches` y `user-accounts` **devuelven id**
  (`tenantId` / `userAccountId`). Otros creates de topología (module/action/node/link/item)
  devuelven `{isSuccess}` **sin id** → requieren un GET posterior.
- **Códigos únicos → 409.** RoboSoft genera todos los códigos con sufijo `_<run_id><n>`
  (helper `RunContext.unique`) para ser re-corrible.
- **Ciclos verificados:** tenant `Active→Suspended→Active`; cuenta
  `Pending→Active→Blocked` (la reactivación `Blocked→Active` no es alcanzable en
  `0.1.0-pilot`: no hay `/unblock` y `/activate` desde Blocked responde 400 — es un
  hallazgo, ver la matriz).
- **La semilla no se destruye:** RoboSoft solo crea data propia; nunca borra ni muta la
  semilla de dev.

## Cómo se alimenta al crecer el sistema

El arnés es la **fuente de verdad auto-alimentada** de la certificación. Para hacerlo crecer:

1. **Nuevo invariante en un contexto existente.** Añádelo a la lista `INVARIANTS` del módulo
   (`{code, fr, title}`) e impleméntalo en `run(rc)` como un `chk.check(code, cond, evidencia)`.
   Regenera/actualiza la fila en `reference/qa/e2e-certification-matrix.md`.
2. **Promover un contexto esqueleto (ola 2).** Reemplaza el cuerpo de `run(rc)` (que hoy
   llama a `skeleton_run`) por checks reales usando `Checker`, `Provisioner` y `login` del
   `harness`. Cambia la bandera del registro en `robosoft.py` (`REGISTRY`, columna
   `implemented`) a `True`.
3. **Nuevo contexto acotado.** Crea `contexts/<nuevo>.py` con `NAME`, `INVARIANTS` y
   `run(rc)`; regístralo en `REGISTRY` de `robosoft.py` e impórtalo en `contexts/__init__.py`
   docstring. Añade su bloque a la matriz.
4. **Nueva capacidad del backend.** Si aparece un endpoint nuevo, sondéalo (swagger en
   `/swagger/v1/swagger.json`), añade el invariante y su check. **La evidencia precede a la
   afirmación (SD-05):** todo check adjunta código HTTP + cuerpo relevante.

### Contrato de un invariante

- Un invariante que no se puede convertir en un check ejecutable **no es un invariante**:
  devuélvelo a producto (SD-04). Los `PENDING`/`SKIP` deben documentar por qué no se
  ejercen (falta de semilla, requiere fuente, etc.).
- Un `FAIL` es un **hallazgo candidato a gap**. El arnés lo lista claramente al final; el
  orquestador (Winston) lo registra en `GAPS.md` con evidencia. **El arnés no edita
  `GAPS.md` ni commitea.**

## Trazabilidad

- Fuente de los invariantes: `reference/qa/bmad-tester-robosoft-audit-2026-07-16.md`,
  sección «Cobertura por contexto» (11 contextos, 133 invariantes).
- Matriz FR → invariante → contexto → estado:
  `reference/qa/e2e-certification-matrix.md`.
- Certificación E2E funcional: gap **G-112**.
