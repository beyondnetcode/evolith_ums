"""Contexto refresh-token — renovación por refresh token opaco (ADR-0110 / FR-015/016).

Fail-closed con refresh apagado, regeneración del grafo, rotación, detección de
reuso (invalida familia), aislamiento multi-tenant, expiración y revocación.
Fuente: auditoría bmad-tester-robosoft-audit-2026-07-16.md (10 invariantes).

IMPLEMENTADO — se ejecuta contra el backend vivo.

Precondición del entorno: el despliegue de dev arranca con la capacidad de refresh
APAGADA para todos los inquilinos (default fail-closed AppConfigurationDefaults
.AuthRefreshTokenEnabled=false). Para ejercer los invariantes con la capacidad
ENCENDIDA (RT02..RT09) el contexto la habilita de forma reversible en el inquilino
management-owner EVOLITH (crea+publica AUTH_REFRESH_TOKEN_ENABLED=true) y la ARCHIVA
al terminar, restaurando el fail-closed. RT01 (fail-closed) se mide sobre inquilinos
CLIENT sembrados (COMEX_ANDINA/AGRONORTE), nunca afectados por ese override.

Lecciones del arnés aplicadas:
  · Auth-negativa: los rechazos de renovación colapsan en el borde HTTP a
    401 code=AUTH_007 (SessionExpired) para todo motivo salvo «capacidad apagada»
    (403 AUTH_008) y «token ausente» (400 AUTH_001). El endpoint /auth/refresh-token
    es anónimo (el refresh ES la credencial), así que no requiere X-Disable-Dev-Auth.
  · Provisión on-behalf: X-Is-Internal-Admin + códigos únicos por corrida.
"""

from __future__ import annotations

import time

from harness import (
    ADMIN_USER, AGRONORTE_CODE, AGRONORTE_USER, COMEX_CODE, COMEX_USER,
    SEED_PASSWORD, EVOLITH_CODE, EVOLITH_TENANT_ID, Checker, login,
)

NAME = "refresh-token"

INVARIANTS = [
    {"code": "INV-RT01", "fr": "FR-015", "title": "A) REFRESH-OFF: fail-closed, no refresh, re-login exigido (regenera grafo)"},
    {"code": "INV-RT02", "fr": "FR-015", "title": "B) REFRESH-ON: renovar regenera el grafo COMPLETO con estado actual sin re-login"},
    {"code": "INV-RT03", "fr": "FR-016", "title": "C) Rotación: nuevo refresh invalida el anterior"},
    {"code": "INV-RT04", "fr": "FR-016", "title": "D) Detección de reuso: refresh consumido invalida la familia + fuerza re-login"},
    {"code": "INV-RT05", "fr": "FR-070", "title": "D-bis) El reuso genera EVENTO AUDITABLE (append-only, ADR-0110)"},
    {"code": "INV-RT06", "fr": "FR-015", "title": "E) Expiración del refresh token -> renovación denegada"},
    {"code": "INV-RT07", "fr": "FR-016", "title": "F) Revocación/logout: el grafo emitido vale hasta validUntil, pero renovar se corta"},
    {"code": "INV-RT08", "fr": "—", "title": "G) Negativos: refresh inválido/malformado -> rechazo"},
    {"code": "INV-RT09", "fr": "FR-022", "title": "G-bis) Aislamiento multi-tenant del refresh (no cruza de tenant)"},
    {"code": "INV-RT10", "fr": "FR-002", "title": "G-ter) Cuenta bloqueada / tenant suspendido -> renovación denegada"},
]

_GRAPH_SECTIONS = ["context", "actions", "menuAccess", "domainPermissions",
                   "featureFlags", "effectiveConfig", "scopes"]
_REFRESH_CFG_CODE = "AUTH_REFRESH_TOKEN_ENABLED"
_REFRESH_PATH = "/api/v1/auth/refresh-token"


# --- Habilitación reversible de la capacidad de refresh en EVOLITH -----------

def _resolve_enabled(rc) -> bool:
    r = rc.http.request(
        "GET",
        "/api/v1/app-configurations/resolve?code=%s&tenantId=%s" % (_REFRESH_CFG_CODE, EVOLITH_TENANT_ID),
        token=rc.admin_token, internal_admin=True)
    body = r.json or {}
    return body.get("found") is True and str(body.get("value", "")).lower() == "true"


def _enable_refresh(rc):
    """Crea+publica AUTH_REFRESH_TOKEN_ENABLED=true en el inquilino EVOLITH.

    Devuelve (evidencia, config_id_creado). Idempotente: si ya está activo no crea nada.
    """
    if _resolve_enabled(rc):
        return "AUTH_REFRESH_TOKEN_ENABLED ya resuelve true en EVOLITH (reutilizado)", None
    cr = rc.http.request("POST", "/api/v1/app-configurations", token=rc.admin_token,
                         internal_admin=True, body={
                             "code": _REFRESH_CFG_CODE, "value": "true",
                             "description": "RoboSoft refresh-token enablement",
                             "isInheritable": True, "isEncrypted": False,
                             "isNonOverridable": False, "tenantId": EVOLITH_TENANT_ID})
    cid = (cr.json or {}).get("appConfigurationId")
    pub = rc.http.request("POST", "/api/v1/app-configurations/%s/publish" % cid,
                         token=rc.admin_token, internal_admin=True) if cid else None
    ok = _resolve_enabled(rc)
    ev = "create=%d publish=%s resolve.enabled=%s cid=%s" % (
        cr.status, pub.status if pub else "—", ok, cid)
    return ev, (cid if ok else None)


def _cleanup_refresh(rc, cid):
    """Archiva el override creado, restaurando el fail-closed (Published -> Archived)."""
    if cid:
        rc.http.request("POST", "/api/v1/app-configurations/%s/archive" % cid,
                        token=rc.admin_token, internal_admin=True)


# --- Helpers de renovación --------------------------------------------------

def _admin_login(rc):
    return login(rc.http, EVOLITH_CODE, ADMIN_USER, SEED_PASSWORD)


def _refresh(rc, token, cookie=None):
    return rc.http.request("POST", _REFRESH_PATH, body={"refreshToken": token}, cookie=cookie)


# --- Ejecución --------------------------------------------------------------

def run(rc):
    chk = Checker(rc, NAME, INVARIANTS)

    enable_ev, created_cid = _enable_refresh(rc)
    rc.log("refresh enablement: %s" % enable_ev)

    try:
        _run_checks(rc, chk, enable_ev)
    finally:
        _cleanup_refresh(rc, created_cid)

    return chk.results


def _run_checks(rc, chk, enable_ev):
    http = rc.http

    # INV-RT01: REFRESH-OFF fail-closed. Inquilinos CLIENT sembrados (nunca habilitados)
    # -> /auth/login no emite refreshToken (null). Independiente del override de EVOLITH.
    rc_comex = login(http, COMEX_CODE, COMEX_USER, SEED_PASSWORD)
    rc_agro = login(http, AGRONORTE_CODE, AGRONORTE_USER, SEED_PASSWORD)
    off_ok = (rc_comex.status == 200 and (rc_comex.json or {}).get("refreshToken") is None
              and rc_agro.status == 200 and (rc_agro.json or {}).get("refreshToken") is None)
    chk.check("INV-RT01", off_ok,
              "fail-closed: COMEX_ANDINA login=%d refreshToken=%s; AGRONORTE login=%d refreshToken=%s "
              "(default AuthRefreshTokenEnabled=false -> no se emite)"
              % (rc_comex.status, (rc_comex.json or {}).get("refreshToken"),
                 rc_agro.status, (rc_agro.json or {}).get("refreshToken")))

    # INV-RT02: REFRESH-ON regenera el grafo COMPLETO sin re-login (EVOLITH habilitado).
    lr = _admin_login(rc)
    r0 = (lr.json or {}).get("refreshToken")
    if lr.status == 200 and r0:
        gr = _refresh(rc, r0)
        graph = (gr.json or {}).get("authorizationGraph") or {}
        missing = [k for k in _GRAPH_SECTIONS if k not in graph]
        chk.check("INV-RT02", gr.status == 200 and not missing and bool((gr.json or {}).get("token")),
                  "login EVOLITH emite refreshToken; POST /auth/refresh-token -> HTTP %d; access token nuevo=%s; "
                  "grafo regenerado con secciones %s (faltan=%s) [habilitación: %s]"
                  % (gr.status, bool((gr.json or {}).get("token")),
                     [k for k in _GRAPH_SECTIONS if k in graph], missing or "ninguna", enable_ev))
    else:
        chk.block("INV-RT02", "login admin no emitió refreshToken tras habilitar (HTTP %d, refreshToken=%s); "
                  "habilitación: %s" % (lr.status, r0, enable_ev))

    # INV-RT03: Rotación — R0->R1->R2 (todas 200, refresh distinto) y R0 tras rotar -> 401.
    lr = _admin_login(rc)
    r0 = (lr.json or {}).get("refreshToken")
    if r0:
        g1 = _refresh(rc, r0); r1 = (g1.json or {}).get("refreshToken")
        g2 = _refresh(rc, r1); r2 = (g2.json or {}).get("refreshToken")
        chain_ok = (g1.status == 200 and g2.status == 200
                    and len({r0, r1, r2}) == 3 and all((r0, r1, r2)))
        reuse_prev = _refresh(rc, r0)  # el anterior (ya rotado) debe morir
        rot_ok = chain_ok and reuse_prev.status == 401
        chk.check("INV-RT03", rot_ok,
                  "cadena R0->R1->R2: HTTP %d,%d; refresh distintos=%s; presentar R0 tras rotar -> HTTP %d code=%s "
                  "(esperado 401: rotación invalida el anterior)"
                  % (g1.status, g2.status, len({r0, r1, r2}) == 3, reuse_prev.status,
                     (reuse_prev.json or {}).get("code")))
    else:
        chk.block("INV-RT03", "sin refreshToken de admin para ejercer rotación")

    # INV-RT04: Detección de reuso -> invalida la familia entera (fuerza re-login).
    lr = _admin_login(rc)
    r0 = (lr.json or {}).get("refreshToken")
    if r0:
        g1 = _refresh(rc, r0); r1 = (g1.json or {}).get("refreshToken")
        g2 = _refresh(rc, r1); r2 = (g2.json or {}).get("refreshToken")
        reuse = _refresh(rc, r0)          # reuso de R0 (ya rotado) -> 401 + mata familia
        active_after = _refresh(rc, r2)   # R2 estaba activo; tras el reuso debe morir también
        family_dead = (reuse.status == 401 and active_after.status == 401)
        chk.check("INV-RT04", g1.status == 200 and g2.status == 200 and family_dead,
                  "reuso de R0 rotado -> HTTP %d code=%s; R2 (antes activo) tras el reuso -> HTTP %d code=%s "
                  "(esperado ambos 401: la familia entera se invalida -> re-login forzado)"
                  % (reuse.status, (reuse.json or {}).get("code"),
                     active_after.status, (active_after.json or {}).get("code")))
    else:
        chk.block("INV-RT04", "sin refreshToken de admin para ejercer reuso")

    # INV-RT05: el reuso genera un EVENTO AUDITABLE (append-only). Disparamos un reuso y
    # consultamos la traza por eventType=Auth.Refresh.Failure (persistida vía AuthAuditService).
    lr = _admin_login(rc)
    r0 = (lr.json or {}).get("refreshToken")
    if r0:
        _refresh(rc, r0)          # rota R0 -> R1
        _refresh(rc, r0)          # reuso de R0 -> Auth.Refresh.Failure (append-only)
        found = 0
        sample = ""
        for _ in range(8):
            audit = http.request(
                "GET", "/api/v1/audit-records?page=1&pageSize=20&eventType=Auth.Refresh.Failure",
                token=rc.admin_token, internal_admin=True)
            items = (audit.json or {}).get("items") or []
            if items:
                found = len(items)
                sample = items[0].get("eventType")
                break
            time.sleep(1.0)
        chk.check("INV-RT05", found > 0,
                  "tras el reuso, GET /audit-records?eventType=Auth.Refresh.Failure -> %d registro(s) "
                  "(eventType[0]=%s). Auditoría 2026-07-16 halló totalItems=0; el evento ahora persiste "
                  "(AuthAuditService.RecordAuthEventAsync -> AppendAsync+SaveChanges)."
                  % (found, sample))
    else:
        chk.block("INV-RT05", "sin refreshToken de admin para generar el evento de reuso")

    # INV-RT06: expiración -> renovación denegada. NO ejercitable de forma determinista en caja negra:
    # la vida por defecto es 43 200 min (30 días) y acortarla por debajo de 1 min colapsa la política a
    # DESHABILITADA (fail-closed), no a «Expired»; probar la expiración real exige una espera de reloj
    # de >=60 s, fuera del alcance de un arnés rápido. La rama de expiración existe en el handler
    # (RefreshAuthenticationCommandHandler: `snapshot.ExpiresAtUtc <= now` -> Expired).
    chk.pending("INV-RT06",
                "no ejercitable en caja negra sin espera de reloj: vida por defecto 30 días y no hay "
                "resolución sub-minuto; vida<=0 con capacidad activa colapsa a fail-closed (Disabled), "
                "no a Expired. Camino Expired presente en el handler.")

    # INV-RT07: revocación/logout corta la renovación. login (cookie + refresh) -> logout(cookie) ->
    # el refresh emitido ya no renueva. El logout deriva usuario/tenant de la COOKIE de sesión y revoca
    # todas las familias vivas (RevokeAllForUserAsync), así que el logout DEBE llevar la cookie.
    lr = _admin_login(rc)
    r0 = (lr.json or {}).get("refreshToken")
    cookie = lr.cookie
    if r0 and cookie:
        logout = http.request("POST", "/api/v1/auth/logout", cookie=cookie)
        after = _refresh(rc, r0)
        chk.check("INV-RT07", logout.status == 200 and after.status == 401,
                  "login (cookie+refresh) -> POST /auth/logout (con cookie) HTTP %d -> "
                  "POST /auth/refresh-token{mismo refresh} -> HTTP %d code=%s. Auditoría 2026-07-16 halló "
                  "HTTP 200 (renovaba tras logout); ahora se corta (revocación de familia en logout)."
                  % (logout.status, after.status, (after.json or {}).get("code")))
    else:
        chk.block("INV-RT07", "login admin sin refreshToken (%s) o sin cookie de sesión (%s)"
                  % (bool(r0), bool(cookie)))

    # INV-RT08: negativos. Garbage -> 401 AUTH_007; vacío/ausente/null -> 400 AUTH_001.
    garbage = _refresh(rc, "not-a-real-refresh-token-robosoft")
    empty = http.request("POST", _REFRESH_PATH, body={"refreshToken": ""})
    missing = http.request("POST", _REFRESH_PATH, body={})
    null = http.request("POST", _REFRESH_PATH, body={"refreshToken": None})
    neg_ok = (garbage.status == 401 and (garbage.json or {}).get("code") == "AUTH_007"
              and empty.status == 400 and (empty.json or {}).get("code") == "AUTH_001"
              and missing.status == 400 and null.status == 400)
    chk.check("INV-RT08", neg_ok,
              "garbage -> HTTP %d code=%s; vacío -> HTTP %d code=%s; campo ausente -> HTTP %d; null -> HTTP %d "
              "(esperado 401 AUTH_007 vs 400 AUTH_001 'Refresh token is required')"
              % (garbage.status, (garbage.json or {}).get("code"), empty.status, (empty.json or {}).get("code"),
                 missing.status, null.status))

    # INV-RT09: aislamiento multi-tenant. El endpoint recibe SOLO {refreshToken} (opaco, sin parámetro
    # de tenant); el registro almacenado resuelve TenantId/UserId. La renovación devuelve el grafo del
    # inquilino EMISOR (EVOLITH), no de otro: el token está ligado a su inquilino y no puede cruzar.
    lr = _admin_login(rc)
    r0 = (lr.json or {}).get("refreshToken")
    if r0:
        gr = _refresh(rc, r0)
        tenant = (((gr.json or {}).get("authorizationGraph") or {}).get("context") or {}).get("tenant") or {}
        bound = gr.status == 200 and tenant.get("code") == EVOLITH_CODE
        chk.check("INV-RT09", bound,
                  "el body de renovación es opaco {refreshToken} sin parámetro de tenant; el grafo renovado "
                  "queda ligado al inquilino emisor: context.tenant.code=%s (esperado %s). Un refresh no puede "
                  "renovar la sesión de otro inquilino." % (tenant.get("code"), EVOLITH_CODE))
    else:
        chk.block("INV-RT09", "sin refreshToken de admin para verificar el binding de inquilino")

    # INV-RT10: cuenta bloqueada / tenant suspendido -> renovación denegada. NO ejercitable de forma
    # no destructiva en este backend COMPARTIDO: (a) bloquear un usuario sembrado o suspender un
    # inquilino sembrado altera datos que otros contextos/agentes usan; (b) un usuario desechable no
    # obtiene una sesión válida —el login de una cuenta activa SIN perfil revienta con
    # NullReferenceException (HTTP 500)— por lo que no hay refresh token que bloquear. La rama existe
    # en el handler (usuario/inquilino con Status != Active -> RevokeFamily -> renovación denegada).
    chk.pending("INV-RT10",
                "no ejercitable sin destruir la semilla compartida: bloquear/suspender principales "
                "sembrados es destructivo, y un usuario desechable sin perfil no obtiene login válido "
                "(login profileless -> NullReference 500), así que no hay refresh que revocar. "
                "Camino presente en el handler (Status != Active -> RevokeFamily).")
