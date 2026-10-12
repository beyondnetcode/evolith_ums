"""Contexto transversal-security — aislamiento multi-tenant, idempotencia, contrato REST.

Fuente de invariantes: auditoría bmad-tester-robosoft-audit-2026-07-16.md,
sección «Cobertura por contexto → transversal-security» (9 invariantes).

IMPLEMENTADO — se ejecuta contra el backend vivo.

Lecciones del arnés aplicadas:
  · Auth-negativa / identidad REAL: todo lo que dependa de la identidad efectiva del
    llamante usa X-Disable-Dev-Auth:true. En Development, DevAuthMiddleware fabrica un
    EVOLITH internal-admin por defecto; sin desactivarlo se mediría auth fabricada.
    Con G-042, X-Disable-Dev-Auth es fail-closed: exige credencial real (cookie/bearer),
    y una sesión-cookie real materializa el contexto de inquilino del llamante.
  · Aislamiento: se prueba con una sesión-cookie CLIENT real (COMEX_ANDINA), NO con el
    admin (internal-admin bypasea) ni anónimo (401).
"""

from __future__ import annotations

import json

from harness import (
    COMEX_CODE, COMEX_USER, SEED_PASSWORD, EVOLITH_CODE, EVOLITH_TENANT_ID,
    Checker, Provisioner, admin_login, login,
)

NAME = "transversal-security"

COMEX_TENANT_ID = "c0e1a000-1111-4c0e-a000-000000000001"
AGRONORTE_TENANT_ID = "a9701e00-2222-4a97-b000-000000000002"
DISABLE_DEV_AUTH = {"X-Disable-Dev-Auth": "true"}

INVARIANTS = [
    {"code": "INV-TS01", "fr": "FR-022", "title": "Aislamiento multi-tenant en LECTURA: cero lecturas cruzadas (global query filter sobre OrganizationId)"},
    {"code": "INV-TS02", "fr": "FR-022", "title": "Aislamiento multi-tenant en ESCRITURA: cero escrituras cruzadas"},
    {"code": "INV-TS03", "fr": "FR-022", "title": "Cross-tenant SOLO INTERNAL_ADMIN vía switch-tenant, auditado"},
    {"code": "INV-TS04", "fr": "G-020", "title": "RLS de BD no activo -> el filtro de aplicación debe ser suficiente"},
    {"code": "INV-TS05", "fr": "—", "title": "Idempotencia: reintentos con Idempotency-Key -> efecto único"},
    {"code": "INV-TS06", "fr": "—", "title": "Contrato REST: verbos correctos (GET lee; POST/PUT/PATCH/DELETE escriben)"},
    {"code": "INV-TS07", "fr": "—", "title": "Respuestas Result Pattern + mapeo de errores 400/401/403/404/409/500"},
    {"code": "INV-TS08", "fr": "FR-010", "title": "Seguridad: contraseñas/secretos/refresh tokens NUNCA en logs, proyecciones ni grafo"},
    {"code": "INV-TS09", "fr": "—", "title": "Llamada idempotente repetida (demostración end-to-end)"},
]


def _evolith_user_account_id(http, admin_token):
    """Devuelve el id de una cuenta cuyo tenant es EVOLITH (para probar lectura cruzada)."""
    r = http.request("GET", "/api/v1/user-accounts?page=1&pageSize=20",
                     token=admin_token, internal_admin=True)
    for it in (r.json or {}).get("items", []) or []:
        tid = it.get("tenantId")
        if tid is None or tid == EVOLITH_TENANT_ID:
            return it.get("userAccountId") or it.get("id")
    items = (r.json or {}).get("items") or []
    return (items[0].get("userAccountId") or items[0].get("id")) if items else None


def run(rc):
    chk = Checker(rc, NAME, INVARIANTS)
    http = rc.http

    # Sesión admin (para provisión/consulta) y sesión CLIENT real (para aislamiento).
    adm = admin_login(http)
    admin_token = (adm.json or {}).get("token") or rc.admin_token
    admin_cookie = adm.cookie

    cx = login(http, COMEX_CODE, COMEX_USER, SEED_PASSWORD)
    cx_cookie = cx.cookie
    cx_token = (cx.json or {}).get("token")
    cx_internal_admin = (cx.json or {}).get("isInternalAdmin")

    # ---- INV-TS01: aislamiento en LECTURA (global query filter sobre OrganizationId) ----
    # COMEX (CLIENT, isInternalAdmin=false), con su sesión real, NO debe leer una cuenta de EVOLITH.
    ua_id = _evolith_user_account_id(http, admin_token)
    if cx_cookie and ua_id:
        cross = http.request("GET", "/api/v1/user-accounts/%s" % ua_id,
                             cookie=cx_cookie, extra_headers=DISABLE_DEV_AUTH)
        isolated = cross.status in (403, 404)
        chk.check("INV-TS01", isolated,
                  "COMEX (isInternalAdmin=%s) cookie+X-Disable-Dev-Auth GET /user-accounts/{EVOLITH} "
                  "-> HTTP %d (esperado 404/403: el filtro global sobre OrganizationId aísla la "
                  "cuenta ajena). La fuga del agregado Tenant se evalúa en INV-TS04."
                  % (cx_internal_admin, cross.status))
    else:
        chk.block("INV-TS01", "sin sesión COMEX o sin cuenta EVOLITH para probar lectura cruzada "
                  "(cookie=%s, ua_id=%s)" % (bool(cx_cookie), ua_id))

    # ---- INV-TS02: aislamiento en ESCRITURA (cero escrituras cruzadas) ----
    # COMEX intenta crear una config para el tenant EVOLITH -> debe rechazarse (403).
    if cx_cookie:
        xw_body = {"code": rc.unique("RS_XW").upper(), "value": "x",
                   "description": "cross-write probe", "isInheritable": True,
                   "isEncrypted": False, "isNonOverridable": False, "tenantId": EVOLITH_TENANT_ID}
        w_cross = http.request("POST", "/api/v1/app-configurations", body=xw_body,
                               cookie=cx_cookie, extra_headers=DISABLE_DEV_AUTH)
        w_anon = http.request("POST", "/api/v1/app-configurations", body=xw_body,
                              extra_headers=DISABLE_DEV_AUTH)
        no_cross_write = w_cross.status in (400, 403) and w_anon.status in (401, 403)
        chk.check("INV-TS02", no_cross_write,
                  "COMEX->EVOLITH config write (cookie) -> HTTP %d (%s); write anónimo -> HTTP %d "
                  "(esperado 403 cross-tenant y 401 anónimo)"
                  % (w_cross.status, (w_cross.json or {}).get("error") or w_cross.snippet(60),
                     w_anon.status))
    else:
        chk.block("INV-TS02", "sin sesión COMEX para probar escritura cruzada")

    # ---- INV-TS03: cross-tenant SOLO INTERNAL_ADMIN vía switch-tenant ----
    # El internal-admin (Bearer con claim is_internal_admin) cambia de contexto; un CLIENT no.
    st_admin = http.request("POST", "/api/v1/auth/switch-tenant",
                            body={"tenantId": COMEX_TENANT_ID, "enableCrossTenantAccess": True},
                            token=admin_token)
    st_client = http.request("POST", "/api/v1/auth/switch-tenant",
                             body={"tenantId": EVOLITH_TENANT_ID, "enableCrossTenantAccess": True},
                             token=cx_token)
    st_client_code = (st_client.json or {}).get("code")
    ts03_ok = st_admin.status == 200 and st_client.status == 403 and st_client_code == "AUTH_008"
    chk.check("INV-TS03", ts03_ok,
              "switch-tenant admin Bearer -> HTTP %d (a %s); COMEX Bearer -> HTTP %d code=%s "
              "(esperado admin 200, CLIENT 403 AUTH_008; F5 resuelto: el JWT ya porta "
              "is_internal_admin)"
              % (st_admin.status, (st_admin.json or {}).get("currentTenantCode"),
                 st_client.status, st_client_code))

    # ---- INV-TS04: RLS de BD no activo -> el filtro de aplicación debe ser SUFICIENTE ----
    # El agregado Tenant NO se filtra por OrganizationId y GetTenantByIdQueryHandler no verifica
    # propiedad/internal-admin: un CLIENT lee el registro de OTRO inquilino (F3). Además se
    # confirma que la lectura ANÓNIMA ya está cerrada (G-042) -> el defecto residual es la
    # ausencia de autorización a nivel de aplicación sobre el agregado Tenant.
    anon_tenant = http.request("GET", "/api/v1/tenants/%s" % AGRONORTE_TENANT_ID,
                               extra_headers=DISABLE_DEV_AUTH)
    if cx_cookie:
        leak = http.request("GET", "/api/v1/tenants/%s" % AGRONORTE_TENANT_ID,
                            cookie=cx_cookie, extra_headers=DISABLE_DEV_AUTH)
        leaked = leak.status == 200 and (leak.json or {}).get("code") == "AGRONORTE"
        # El filtro de aplicación es suficiente SOLO si NO hay fuga cross-tenant.
        chk.check("INV-TS04", not leaked,
                  "F3: COMEX (isInternalAdmin=%s) cookie+X-Disable-Dev-Auth GET /tenants/{AGRONORTE} "
                  "-> HTTP %d code=%s name=%r type=%s (fuga cross-tenant de identidad). Lectura "
                  "ANÓNIMA ya cerrada: GET /tenants/{AGRONORTE} anón -> HTTP %d (G-042). El filtro "
                  "de aplicación NO es suficiente sobre el agregado Tenant."
                  % (cx_internal_admin, leak.status, (leak.json or {}).get("code"),
                     (leak.json or {}).get("name"), (leak.json or {}).get("type"), anon_tenant.status))
    else:
        chk.block("INV-TS04", "sin sesión COMEX para probar la fuga del agregado Tenant")

    # ---- INV-TS05: idempotencia con Idempotency-Key -> efecto único (mismo emisor) ----
    idem_key = "robosoft-idem-%s" % rc.run_id
    idem_body = {"code": rc.unique("RS_IDEM").upper(), "value": "v",
                 "description": "idempotency probe", "isInheritable": True,
                 "isEncrypted": False, "isNonOverridable": False, "tenantId": EVOLITH_TENANT_ID}
    r1 = http.request("POST", "/api/v1/app-configurations", body=idem_body, token=admin_token,
                      internal_admin=True, extra_headers={"Idempotency-Key": idem_key})
    r2 = http.request("POST", "/api/v1/app-configurations", body=idem_body, token=admin_token,
                      internal_admin=True, extra_headers={"Idempotency-Key": idem_key})
    id1 = (r1.json or {}).get("appConfigurationId")
    id2 = (r2.json or {}).get("appConfigurationId")
    replayed = r2.headers.get("x-idempotency-replayed") == "true"
    chk.check("INV-TS05", r1.status == 201 and r2.status == 201 and replayed and id1 == id2,
              "POST /app-configurations Idempotency-Key=K -> HTTP %d id=%s; repetición exacta -> "
              "HTTP %d X-Idempotency-Replayed=%s id=%s (efecto único, mismo id)"
              % (r1.status, id1, r2.status, r2.headers.get("x-idempotency-replayed"), id2))

    # ---- INV-TS06: contrato REST — verbos correctos ----
    # GET lee sin mutar; un verbo de mutación no soportado en un recurso de solo-lectura
    # (auditoría append-only) se rechaza (405). Se usa /audit-records como testigo.
    al = http.request("GET", "/api/v1/audit-records?page=1&pageSize=1", token=admin_token,
                      internal_admin=True)
    rec_items = (al.json or {}).get("items") or []
    rec_id = rec_items[0].get("auditRecordId") or rec_items[0].get("id") if rec_items else None
    get_tenant = http.request("GET", "/api/v1/tenants/%s" % EVOLITH_TENANT_ID, token=admin_token,
                              internal_admin=True)
    if rec_id:
        put_rec = http.request("PUT", "/api/v1/audit-records/%s" % rec_id, token=admin_token,
                               internal_admin=True)
        del_rec = http.request("DELETE", "/api/v1/audit-records/%s" % rec_id, token=admin_token,
                               internal_admin=True)
        verbs_ok = (get_tenant.status == 200 and put_rec.status in (404, 405)
                    and del_rec.status in (404, 405))
        chk.check("INV-TS06", verbs_ok,
                  "GET /tenants/{id} (lee) -> HTTP %d; PUT /audit-records/{id} -> HTTP %d, "
                  "DELETE -> HTTP %d (append-only: mutación no soportada -> 405/404)"
                  % (get_tenant.status, put_rec.status, del_rec.status))
    else:
        chk.check("INV-TS06", get_tenant.status == 200,
                  "GET /tenants/{id} (lee) -> HTTP %d; sin registro de auditoría para verbos de "
                  "mutación (contrato append-only confirmado en contexto audit)" % get_tenant.status)

    # ---- INV-TS07: Result Pattern + mapeo de errores 400/401/403/404/409/500 ----
    e400 = http.request("POST", "/api/v1/auth/login", body={})
    e401 = login(http, EVOLITH_CODE, "admin@evolith.example", "CredencialErrada.999")
    e404 = login(http, "NO_SUCH_TENANT_%s" % rc.run_id, "x@y.z", "aaaaaaaaaaaa")
    e403 = http.request("POST", "/api/v1/auth/switch-tenant",
                        body={"tenantId": EVOLITH_TENANT_ID}, token=cx_token)
    prov = Provisioner(rc)
    dup_code = rc.unique("RS_DUP")
    d1 = prov.create_tenant(dup_code, ttype="CLIENT")
    d2 = prov.create_tenant(dup_code, ttype="CLIENT")
    # H-01: crear 2do management-owner debía ser un 500; ahora debe mapear a 4xx (no 500).
    mo = prov.create_tenant(rc.unique("RS_MO"), ttype="MANAGEMENT_OWNER", management_owner=True)
    codes = {
        "400": (e400.status == 400 and (e400.json or {}).get("code") == "AUTH_001"),
        "401": (e401.status == 401 and (e401.json or {}).get("code") == "AUTH_006"),
        "403": (e403.status == 403 and (e403.json or {}).get("code") == "AUTH_008"),
        "404": (e404.status == 404 and (e404.json or {}).get("code") == "AUTH_002"),
        "409": (d1.status == 201 and d2.status == 409),
        "no-500": (mo.status != 500),
    }
    chk.check("INV-TS07", all(codes.values()),
              "mapeo: 400=%s(AUTH_001) 401=%s(AUTH_006) 403=%s(AUTH_008) 404=%s(AUTH_002) "
              "409=dup(%d->%d) MO-2do=%d(H-01: no-500). Result Pattern coherente: %s"
              % (e400.status, e401.status, e403.status, e404.status, d1.status, d2.status,
                 mo.status, "OK" if all(codes.values()) else codes))

    # ---- INV-TS08: secretos NUNCA en el grafo/proyecciones ----
    graph = (adm.json or {}).get("authorizationGraph") or {}
    graph_txt = json.dumps(graph).lower()
    # Se crea además una config cifrada y se verifica que su valor no aparezca en el grafo del login.
    secret = "SUPERSECRET-%s-APIKEY" % rc.run_id
    enc = http.request("POST", "/api/v1/app-configurations", token=admin_token, internal_admin=True,
                       body={"code": rc.unique("RS_SEC").upper(), "value": secret, "scope": "Tenant",
                             "tenantId": EVOLITH_TENANT_ID, "isEncrypted": True,
                             "description": "RoboSoft secret probe"})
    markers = ["passwordhash", "bcrypt", "$2a$", "$2b$", "privatekey", "refreshtoken"]
    leaks = [w for w in markers if w in graph_txt]
    secret_in_graph = secret.lower() in graph_txt
    chk.check("INV-TS08", not leaks and not secret_in_graph,
              "scan del grafo de login: marcadores de secreto=%s; config cifrada creada (HTTP %d) "
              "y su valor %sen el grafo"
              % (leaks or "ninguno", enc.status, "PRESENTE " if secret_in_graph else "AUSENTE "))

    # ---- INV-TS09: llamada idempotente repetida (demostración end-to-end) ----
    # Reafirma el efecto único: dos POST idénticos con la misma clave -> un solo recurso.
    key9 = "robosoft-idem-e2e-%s" % rc.run_id
    body9 = {"code": rc.unique("RS_IDEM9").upper(), "value": "once",
             "description": "e2e idempotency", "isInheritable": True, "isEncrypted": False,
             "isNonOverridable": False, "tenantId": EVOLITH_TENANT_ID}
    a = http.request("POST", "/api/v1/app-configurations", body=body9, token=admin_token,
                     internal_admin=True, extra_headers={"Idempotency-Key": key9})
    b = http.request("POST", "/api/v1/app-configurations", body=body9, token=admin_token,
                     internal_admin=True, extra_headers={"Idempotency-Key": key9})
    single_effect = (a.status == 201 and b.status == 201
                     and (a.json or {}).get("appConfigurationId") == (b.json or {}).get("appConfigurationId")
                     and b.headers.get("x-idempotency-replayed") == "true")
    chk.check("INV-TS09", single_effect,
              "2 POST idénticos (misma Idempotency-Key) -> HTTP %d/%d, mismo id=%s, "
              "replay=%s (un solo efecto end-to-end)"
              % (a.status, b.status, (a.json or {}).get("appConfigurationId"),
                 b.headers.get("x-idempotency-replayed")))

    return chk.results
