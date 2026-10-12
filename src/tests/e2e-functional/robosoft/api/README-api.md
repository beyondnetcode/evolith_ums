# Arnés RoboSoft — carril B (nivel API, caja negra)

Fundación **durable y versionada** de la certificación funcional E2E de UMS a **nivel API**
(G-112, carril B). Complementa al carril A (Playwright UI, en `src/apps/ums.web-app/tests`) y
al arnés Python 0.1.0-pilot (en el directorio padre `robosoft/`).

Este arnés es un **cliente externo simulado** que:

- Ejerce el API REST del backend **vivo** en kind (por defecto `http://localhost:8080`) usando
  exclusivamente el `APIRequestContext` de Playwright — **sin navegador**.
- **Auto-aprovisiona** su propia data con identificadores/códigos **únicos por corrida** →
  determinista, idempotente y re-corrible (no colisiona 409 contra data previa, no destruye la
  semilla).
- Verifica **invariantes del PRD** (`INV-*`) como reglas de negocio de caja negra, con evidencia
  runtime (código HTTP + `errorCode` de dominio).

## Estructura

```
src/tests/e2e-functional/robosoft/api/
├── playwright.config.ts               # proyecto "robosoft-api": request context, sin navegador
├── helpers/
│   ├── auth.ts                        # login real + cabeceras DevAuth (personificación)
│   ├── provision.ts                   # alta idempotente de data vía API (ids únicos por corrida)
│   └── invariant.ts                   # declarar/aseverar una invariante INV-* con su evidencia
├── tests/
│   └── approvals-compliance.spec.ts   # tramo 1: 20 invariantes del contexto approvals-compliance
└── README-api.md                      # este archivo
```

## Identidad (Development)

En `Development` el backend expone dos caminos, ambos usados por este arnés:

1. **Login real** (`POST /api/v1/auth/login`) → devuelve un JWT. Ver `helpers/auth.ts:login`.
2. **DevAuthMiddleware**: acepta identidad por cabeceras `X-User-Id` / `X-Tenant-Id` /
   `X-Is-Internal-Admin` para toda petición fuera de `/api/v1/auth` y `/api/v1/client`. Permite
   **personificar** a distintos usuarios/inquilinos — imprescindible para verificar invariantes
   como la segregación de deberes (`INV-AR3`): el solicitante y el aprobador son identidades
   distintas. Ver `helpers/auth.ts:devAuthHeaders`.

## Cómo correrlo

Requiere el backend vivo (health 200). El clúster local es `evolith-cluster-ums`.

```bash
# Directo (solo carril B):
cd src/tests/e2e-functional/robosoft/api
E2E_BASE_URL=http://localhost:8080 npx playwright test

# Un solo contexto / invariante:
E2E_BASE_URL=http://localhost:8080 npx playwright test -g "INV-AR3"

# Vía runner de certificación (ambos carriles, resumen verde/rojo):
scripts/certify-e2e.sh                # carril A (UI) + carril B (API)
scripts/certify-e2e.sh --carril b     # solo carril B
```

`E2E_BASE_URL` por defecto es `http://localhost:8080` (Ingress de kind). El arnés **no levanta**
servidores ni toca el clúster.

## Tramo implementado (fundación + tramo 1)

Contexto **`approvals-compliance`** (FR-050/052/053): **20 invariantes**, todas VERDE contra el
binario actual (2026-07-23). Es la **fundación** del carril B, no el 100%: el resto de los 11
contextos queda listado como pendiente en
[`reference/qa/e2e-certification-matrix.md`](../../../../../reference/qa/e2e-certification-matrix.md),
fuente de verdad para extender de forma incremental.

## Cómo extenderlo

1. Añade un `tests/<contexto>.spec.ts` nuevo.
2. Reutiliza/añade helpers de `provision.ts` (alta idempotente con ids únicos).
3. Declara cada invariante con `invariante({ id, contexto, descripcion, referencia }, cuerpo)`.
   Si re-verificas un BUG REAL aún presente, usa `invarianteFixme({ ..., hallazgo }, cuerpo)`
   con `archivo:línea` + evidencia (no se corrige producción desde el arnés).
4. Actualiza la matriz de certificación.

**No lo borres; extiéndelo.** Su ausencia fue la causa raíz de una pérdida previa de cobertura.
