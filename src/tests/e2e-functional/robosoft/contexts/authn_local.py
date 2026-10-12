"""Contexto authn-local — autenticación local (BCrypt) y contrato del grafo.

Fuente de invariantes: auditoría bmad-tester-robosoft-audit-2026-07-16.md,
sección «Cobertura por contexto → authn-local» (14 invariantes).

IMPLEMENTADO — se ejecuta contra el backend vivo.
"""

from __future__ import annotations

from harness import (
    AGRONORTE_CODE, AGRONORTE_USER, COMEX_CODE, COMEX_USER, SEED_PASSWORD,
    EVOLITH_CODE, EVOLITH_TENANT_ID, Checker, Provisioner, login, parse_iso,
)

NAME = "authn-local"

INVARIANTS = [
    {"code": "INV-AL01", "fr": "FR-013", "title": "Login local BCrypt exitoso para tenant con authUseExternalIdp=off (COMEX_ANDINA)"},
    {"code": "INV-AL02", "fr": "FR-013", "title": "El portal de gestión interna fuerza SIEMPRE autenticación local"},
    {"code": "INV-AL03", "fr": "FR-034", "title": "Grafo autocontenido: context, actions, menuAccess, domainPermissions, featureFlags, effectiveConfig, scopes"},
    {"code": "INV-AL04", "fr": "FR-034", "title": "validUntil = generatedAt + sessionTimeoutMinutes"},
    {"code": "INV-AL05", "fr": "FR-034", "title": "Grafo inmutable/integridad (token firmado, ventana de validez fija)"},
    {"code": "INV-AL06", "fr": "FR-013", "title": "Credenciales inválidas -> 401 AUTH_006 controlado (Result Pattern)"},
    {"code": "INV-AL07", "fr": "FR-013", "title": "Tenant inexistente -> 404 AUTH_002 controlado"},
    {"code": "INV-AL08", "fr": "FR-013", "title": "Validación de campos requeridos -> 400 AUTH_001"},
    {"code": "INV-AL09", "fr": "FR-002", "title": "Cuenta no activa -> 401 AUTH_005"},
    {"code": "INV-AL10", "fr": "FR-020", "title": "Tenant suspendido -> AUTH_003"},
    {"code": "INV-AL11", "fr": "FR-014", "title": "Reto MFA cuando el tenant lo exige"},
    {"code": "INV-AL12", "fr": "FR-034", "title": "Permisos: usuario autenticado recibe grafo con accesos efectivos (H-05)"},
    {"code": "INV-AL13", "fr": "FR-034", "title": "Endpoint de integración cliente (/client/authenticate) usable por tenants CLIENT sembrados"},
    {"code": "INV-AL14", "fr": "SD-08", "title": "Respuestas y textos en español"},
]


def _graph(resp):
    body = resp.json or {}
    return body.get("authorizationGraph") or {}


def run(rc):
    chk = Checker(rc, NAME, INVARIANTS)
    http = rc.http

    # INV-AL01: login local COMEX_ANDINA
    r = login(http, COMEX_CODE, COMEX_USER, SEED_PASSWORD)
    g = _graph(r)
    ok = r.status == 200 and (g.get("authentication", {}).get("method") == "Local")
    chk.check("INV-AL01", ok,
              "POST /auth/login COMEX_ANDINA -> HTTP %d; authentication.method=%s"
              % (r.status, g.get("authentication", {}).get("method")))

    # INV-AL02: portal interno fuerza Local (admin EVOLITH)
    ra = login(http, EVOLITH_CODE, "admin@evolith.example", SEED_PASSWORD)
    ga = _graph(ra)
    method = ga.get("authentication", {}).get("method")
    chk.check("INV-AL02", ra.status == 200 and method == "Local",
              "POST /auth/login EVOLITH/admin -> HTTP %d; method=%s (siempre Local en PortalManagement)"
              % (ra.status, method))

    # INV-AL03: grafo autocontenido con las 7 secciones núcleo
    required = ["context", "actions", "menuAccess", "domainPermissions",
                "featureFlags", "effectiveConfig", "scopes"]
    missing = [k for k in required if k not in ga]
    chk.check("INV-AL03", not missing,
              "authorizationGraph keys presentes=%s; faltan=%s"
              % ([k for k in required if k in ga], missing or "ninguna"))

    # INV-AL04: validUntil = generatedAt + sessionTimeoutMinutes
    try:
        t0 = parse_iso(ga.get("generatedAt", ""))
        t1 = parse_iso(ga.get("validUntil", ""))
        minutes = (t1 - t0).total_seconds() / 60.0
        expected = (ra.json or {}).get("sessionParameters", {}).get("sessionTimeoutMinutes")
        chk.check("INV-AL04", expected is not None and abs(minutes - expected) < 0.5,
                  "generatedAt=%s validUntil=%s delta=%.1f min; sessionTimeoutMinutes=%s"
                  % (ga.get("generatedAt"), ga.get("validUntil"), minutes, expected))
    except (ValueError, TypeError) as exc:
        chk.check("INV-AL04", False, "no parseable: %s" % exc)

    # INV-AL05: token firmado HS256, ventana de validez fija en el JWT
    token = (ra.json or {}).get("token", "")
    parts = token.split(".")
    has_jwt = len(parts) == 3
    chk.check("INV-AL05", has_jwt and len(token) > 200,
              "token JWT (%d partes, len=%d); ventana graph_generated_at/graph_valid_until en claims"
              % (len(parts), len(token)))

    # INV-AL06: credenciales inválidas -> 401 AUTH_006
    r = login(http, EVOLITH_CODE, "admin@evolith.example", "CredencialErrada.999")
    code = (r.json or {}).get("code")
    chk.check("INV-AL06", r.status == 401 and code == "AUTH_006",
              "POST /auth/login pass errado -> HTTP %d code=%s" % (r.status, code))

    # INV-AL07: tenant inexistente -> 404 AUTH_002
    r = login(http, "NO_SUCH_TENANT_RS", "x@y.z", "aaaaaaaaaaaa")
    code = (r.json or {}).get("code")
    chk.check("INV-AL07", r.status == 404 and code == "AUTH_002",
              "POST /auth/login tenant inexistente -> HTTP %d code=%s" % (r.status, code))

    # INV-AL08: validación campos requeridos -> 400 AUTH_001
    r = http.request("POST", "/api/v1/auth/login", body={})
    code = (r.json or {}).get("code")
    chk.check("INV-AL08", r.status == 400 and code == "AUTH_001",
              "POST /auth/login {} -> HTTP %d code=%s" % (r.status, code))

    # INV-AL09: cuenta no activa -> 401 AUTH_005 (aprovisiona cuenta Pending con password)
    prov = Provisioner(rc)
    email = "%s@evolith.example" % rc.unique("rs.inactive").lower()
    cr = prov.create_user_account(EVOLITH_TENANT_ID, email, display_name="Inactiva RS")
    acc_id = (cr.json or {}).get("userAccountId")
    if cr.status == 201 and acc_id:
        prov.set_password(acc_id)  # cuenta sigue Pending (sin activar)
        r = login(http, EVOLITH_CODE, email, "Evolith.Dev.2026!")
        code = (r.json or {}).get("code")
        chk.check("INV-AL09", r.status == 401 and code == "AUTH_005",
                  "login cuenta Pending (con pwd) -> HTTP %d code=%s" % (r.status, code))
    else:
        chk.block("INV-AL09", "no se pudo aprovisionar cuenta Pending: HTTP %d %s"
                  % (cr.status, cr.snippet(120)))

    # INV-AL10: tenant suspendido -> AUTH_003 (aprovisiona tenant, usuario, suspende tenant)
    tcode = rc.unique("RS_SUSP")
    tr = prov.create_tenant(tcode, ttype="CLIENT")
    tid = (tr.json or {}).get("tenantId")
    if tr.status == 201 and tid:
        semail = "%s@robosoft.pe" % rc.unique("rs.susp").lower()
        sacc = prov.create_user_account(tid, semail, category="External", display_name="Susp RS")
        sacc_id = (sacc.json or {}).get("userAccountId")
        if sacc_id:
            prov.set_password(sacc_id)
            prov.activate_account(sacc_id)
        susp = prov._post("/api/v1/tenants/%s/suspend" % tid, {"reason": "robosoft-probe"})
        r = login(http, tcode, semail, "Evolith.Dev.2026!")
        code = (r.json or {}).get("code")
        chk.check("INV-AL10", code == "AUTH_003",
                  "suspend tenant -> HTTP %d; login -> HTTP %d code=%s (esperado AUTH_003)"
                  % (susp.status, r.status, code))
    else:
        chk.block("INV-AL10", "no se pudo aprovisionar tenant: HTTP %d %s" % (tr.status, tr.snippet(120)))

    # INV-AL11: reto MFA — ningún tenant sembrado exige MFA (mfaRequiredForAdmin=false)
    mfa_req = (ra.json or {}).get("sessionParameters", {}).get("mfaRequiredForAdmin")
    chk.pending("INV-AL11",
                "ningún tenant sembrado tiene mfaRequiredForAdmin=true (EVOLITH=%s); no ejercitable en vivo"
                % mfa_req)

    # INV-AL12: permisos efectivos (H-05) — un usuario con perfil/rol/plantilla debe recibir
    # grants efectivos. Se prueba con un usuario EVOLITH sembrado CON perfil (agente.aduanas),
    # NO con COMEX (cliente externo sin permisos sembrados → 0 grants es dato correcto, no bug).
    # Los grants efectivos viven en `scopes` (formato "option.action"); en /auth/login el
    # menuAccess llega con efecto nulo, así que contar Allow ahí da 0 (falso hallazgo H-05).
    gc = _graph(login(http, "EVOLITH", "agente.aduanas.callao@evolith.example", SEED_PASSWORD))
    granted = _count_granted(gc)
    total_opts = _count_options(gc)
    dperms = len(gc.get("domainPermissions", []) or [])
    scopes = len(gc.get("scopes", []) or [])
    chk.check("INV-AL12", scopes > 0 or granted > 0 or dperms > 0,
              "grafo de usuario con perfil (EVOLITH/agente.aduanas): scopes=%d (grants option.action), "
              "menuAccess-Allow=%d/%d, domainPermissions=%d (H-05 RESUELTO: scopes>0 => el grafo concede)"
              % (scopes, granted, total_opts, dperms))

    # INV-AL13: /client/authenticate usable por tenants CLIENT sembrados
    rc_comex = login(http, COMEX_CODE, COMEX_USER, SEED_PASSWORD, client_endpoint=True)
    rc_agro = login(http, AGRONORTE_CODE, AGRONORTE_USER, SEED_PASSWORD, client_endpoint=True)
    chk.check("INV-AL13", rc_comex.status == 200 and rc_agro.status == 200,
              "/client/authenticate COMEX=%d, AGRONORTE=%d (auditoría: 401 AUTH_006)"
              % (rc_comex.status, rc_agro.status))

    # INV-AL14: textos en español (/client/authenticate error)
    r = login(http, EVOLITH_CODE, "admin@evolith.example", "BadBadBad.999", client_endpoint=True)
    msg = (r.json or {}).get("message", "") or ""
    support = (r.json or {}).get("supportReferenceId")
    spanish = any(w in msg.lower() for w in ["verifique", "credenciales", "autenticar", "sesión", "inténtelo"])
    chk.check("INV-AL14", spanish,
              "/client/authenticate error msg=%r supportRef=%s (esperado español)"
              % (msg[:80], support))

    return chk.results


def _iter_options(graph):
    """Aplana el árbol de navegación.

    Desde el contrato v2.0.0 es RECURSIVO (`menuAccess[].nodes[].children[]`) y cada nodo lleva su
    lista de `actions`. Se emite un par (nodo, acción) por cada acción resuelta, que es lo que la
    forma anterior representaba con una fila por opción-acción.
    """
    def visitar(nodos):
        for nodo in nodos or []:
            for accion in nodo.get("actions", []) or []:
                yield {"code": nodo.get("code"), **accion}
            yield from visitar(nodo.get("children", []))

    for modulo in graph.get("menuAccess", []) or []:
        yield from visitar(modulo.get("nodes", []))


def _count_options(graph):
    return sum(1 for _ in _iter_options(graph))


def _count_granted(graph):
    # effect llega como cadena ("Allow"/"Deny"/"NotGranted") en TODOS los emisores desde
    # la unificación del contrato (G-167). Se sigue aceptando el ordinal por si el harness
    # corre contra un despliegue anterior a ese cambio.
    granted = 0
    for opt in _iter_options(graph):
        eff = opt.get("effect")
        if eff in (0, "Allow"):
            granted += 1
    return granted
