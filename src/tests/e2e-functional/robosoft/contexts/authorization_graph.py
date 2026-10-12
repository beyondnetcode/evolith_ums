"""Contexto authorization-graph — resolución de grants y serialización del grafo.

Resolución de grants (menuAccess/scopes/domainPermissions), precedencia Deny>Allow
y Override>Template, fail-closed y serialización en español.

Fuente de invariantes: auditoría bmad-tester-robosoft-audit-2026-07-16.md,
sección «Cobertura por contexto → authorization-graph» (10 invariantes).

IMPLEMENTADO — se ejecuta contra el backend vivo.

Nota de contexto (contra la auditoría 2026-07-16): el binario vivo YA corrige la
regresión H-05: el grafo SÍ concede permisos efectivos vía `scopes` (lista
"option.action"). Un usuario EVOLITH con perfil (agente.aduanas) recibe ~14 scopes.
Además BuildDomainPermissions y BuildMenuAccess son ahora fail-closed
(AuthorizationGraphBuilderService.cs: `var effect = AccessEffect.NotGranted;`
antes del TryGetValue), corrigiendo el fail-open AG-01 que la auditoría reportó.
No repetir el falso hallazgo H-05 (lección del batch 1).
"""

from __future__ import annotations

from harness import (
    COMEX_CODE, COMEX_USER, AGRONORTE_CODE, AGRONORTE_USER, SEED_PASSWORD,
    EVOLITH_CODE, EVOLITH_TENANT_ID, Checker, Provisioner, login,
)

NAME = "authorization-graph"

INVARIANTS = [
    {"code": "INV-AG01", "fr": "FR-034", "title": "Activar opciones del sistema desde el grafo: opción concedida en plantilla resuelve effect=Allow"},
    {"code": "INV-AG02", "fr": "FR-035", "title": "Precedencia Deny>Allow: un permiso Deny para (target,action) domina sobre Allow"},
    {"code": "INV-AG03", "fr": "FR-035", "title": "Precedencia Override>Template: un permiso con IsOverride domina sobre el valor de plantilla"},
    {"code": "INV-AG04", "fr": "FR-035", "title": "Ausencia = no concedido en menuAccess (fail-closed)"},
    {"code": "INV-AG05", "fr": "FR-035", "title": "Ausencia = no concedido en domainPermissions (fail-closed)"},
    {"code": "INV-AG06", "fr": "FR-034", "title": "Resolución de grants refleja los permisos efectivos del perfil"},
    {"code": "INV-AG07", "fr": "FR-034", "title": "H-05: el grafo concede permisos a los usuarios sembrados"},
    {"code": "INV-AG08", "fr": "FR-034", "title": "Cliente externo obtiene su grafo vía POST /client/authenticate con credenciales válidas"},
    {"code": "INV-AG09", "fr": "FR-041", "title": "featureFlags del grafo se evalúan por suite con isEnabled y matchedCriteriaType"},
    {"code": "INV-AG10", "fr": "SD-08", "title": "El grafo se serializa de forma consistente y en español en los endpoints que lo exponen"},
]


class _Authz:
    """Provisión mínima de una suite con opción concedida y opción NO concedida,
    para evidenciar fail-closed y la materialización de grants en `scopes`."""

    def __init__(self, rc):
        self.rc = rc
        self.http = rc.http
        self.token = rc.admin_token

    def post(self, path, body):
        return self.http.request("POST", path, body=body, token=self.token, internal_admin=True)

    def get(self, path):
        return self.http.request("GET", path, token=self.token, internal_admin=True)

    def build_suite_with_grant(self):
        """Devuelve dict con ids y el grafo-preview de un perfil que concede READ
        SOLO en la opción `optG` (optN queda sin conceder) y expone un domain resource
        sin conceder (para fail-closed de domainPermissions).
        Devuelve None si algún paso de provisión falla."""
        rc = self.rc
        prov = Provisioner(rc)

        sid = (self.post("/api/v1/system-suites",
                         {"tenantId": EVOLITH_TENANT_ID, "code": rc.unique("AG_SUITE"),
                          "name": "AG", "description": "d"}).json or {}).get("systemSuiteId")
        if not sid:
            return None
        mid = (self.post("/api/v1/system-suites/%s/modules" % sid,
                         {"code": rc.unique("AGM"), "name": "m", "description": "d",
                          "sortOrder": 1}).json or {}).get("moduleId")
        if not mid:
            return None
        self.post("/api/v1/system-suites/%s/modules/%s/activate" % (sid, mid), None)

        def node(parent, kind, code, sort=1):
            return (self.post("/api/v1/system-suites/%s/modules/%s/nodes" % (sid, mid),
                              {"parentNodeId": parent, "kind": kind, "code": code,
                               "label": code, "description": "d", "sortOrder": sort}).json or {}).get("nodeId")

        menu_id = node(None, "Menu", rc.unique("AGME"))
        sub_id = node(menu_id, "SubMenu", rc.unique("AGSB")) if menu_id else None
        opt_g = node(sub_id, "Option", rc.unique("AGOG"), 1) if sub_id else None
        opt_n = node(sub_id, "Option", rc.unique("AGON"), 2) if sub_id else None
        if not (opt_g and opt_n):
            return None

        read_id = (self.post("/api/v1/system-suites/%s/actions" % sid,
                             {"code": "READ", "name": "Leer"}).json or {}).get("actionId")
        self.post("/api/v1/system-suites/%s/actions" % sid, {"code": "WRITE", "name": "Escribir"})
        if not read_id:
            return None
        # vincular READ a AMBAS opciones (una se concederá, la otra no)
        self.post("/api/v1/system-suites/%s/modules/%s/nodes/%s/actions" % (sid, mid, opt_g),
                  {"actionCode": "READ"})
        self.post("/api/v1/system-suites/%s/modules/%s/nodes/%s/actions" % (sid, mid, opt_n),
                  {"actionCode": "READ"})
        # domain resource sin conceder (para fail-closed de domainPermissions)
        self.post("/api/v1/system-suites/%s/domain-resources" % sid,
                  {"moduleId": None, "parentResourceId": None, "type": "Aggregate",
                   "code": rc.unique("AGRES"), "name": "Res", "description": "d"})

        role_id = (self.post("/api/v1/system-suites/%s/roles" % sid,
                             {"code": rc.unique("AGRO"), "value": "R", "description": "d",
                              "parentRoleId": None, "hierarchyLevel": 0,
                              "promotionOrder": 1}).json or {}).get("roleId")
        if not role_id:
            return None
        tid = (self.post("/api/v1/permission-templates",
                         {"tenantId": EVOLITH_TENANT_ID, "roleId": role_id,
                          "systemSuiteId": sid}).json or {}).get("templateId")
        if not tid:
            return None
        # conceder READ Allow SOLO en optG
        self.post("/api/v1/permission-templates/%s/items" % tid,
                  {"targetType": "Option", "targetId": opt_g, "actionId": read_id,
                   "isAllowed": True, "isDenied": False})
        pub = self.post("/api/v1/permission-templates/%s/publish" % tid, None)
        if pub.status != 204:
            return None

        uemail = "%s@evolith.example" % rc.unique("ag.user").lower()
        uid = (prov.create_user_account(EVOLITH_TENANT_ID, uemail, display_name="AG User").json or {}).get("userAccountId")
        if not uid:
            return None
        pid = (self.post("/api/v1/profiles",
                         {"tenantId": EVOLITH_TENANT_ID, "userId": uid, "roleId": role_id,
                          "branchId": None}).json or {}).get("profileId")
        if not pid:
            return None
        assign = self.post("/api/v1/profiles/%s/templates/%s" % (pid, tid), None)
        if assign.status != 204:
            return None

        prev = self.get("/api/v1/profiles/%s/auth-graph/preview" % pid)
        graph = _parse_preview(prev)
        return {
            "sid": sid, "profile_id": pid, "opt_g_code": _last_code(rc, "AGOG"),
            "graph": graph,
        }


def run(rc):
    chk = Checker(rc, NAME, INVARIANTS)
    az = _Authz(rc)

    # Usuario EVOLITH CON perfil/rol/plantilla — fuente de verdad de "el grafo concede".
    agente = _graph(login(rc.http, EVOLITH_CODE, "agente.aduanas.callao@evolith.example", SEED_PASSWORD))
    ag_scopes = agente.get("scopes", []) or []

    # ── INV-AG01: opción concedida en plantilla resuelve Allow (vía scopes) ──
    chk.check("INV-AG01", len(ag_scopes) > 0,
              "login EVOLITH/agente.aduanas -> grafo con scopes=%d (option.action Allow) sample=%s"
              % (len(ag_scopes), ag_scopes[:4]))

    # ── INV-AG07: H-05 el grafo concede a los usuarios sembrados ──
    chk.check("INV-AG07", len(ag_scopes) > 0,
              "H-05 RESUELTO: agente.aduanas recibe %d scopes concedidos por el grafo "
              "(la auditoría lo marcaba como 'grafo no concede NADA'; ya no aplica)" % len(ag_scopes))

    # ── Provisión controlada: opción concedida (optG) vs NO concedida (optN) ──
    built = az.build_suite_with_grant()

    # ── INV-AG04: fail-closed en menuAccess (opción sin grant => NO concedida) ──
    if built and built.get("graph") is not None:
        pv_scopes = built["graph"].get("scopes", []) or []
        granted = [s for s in pv_scopes if ".read" in s.lower()]
        # optG concede => aparece en scopes; optN NO concede => NO aparece.
        # (los códigos de opción se serializan en minúsculas dentro del scope)
        og_present = any(s for s in pv_scopes)  # al menos la opción concedida
        # buscamos que exista exactamente 1 scope .read (el de optG), no 2.
        chk.check("INV-AG04", og_present and len(granted) == 1,
                  "perfil con 2 opciones (1 concedida READ, 1 sin conceder), ambas con acción "
                  "READ vinculada -> scopes=%s: solo la opción concedida aparece (fail-closed; "
                  "la no concedida resuelve NotGranted y NO emite scope)" % (pv_scopes,))
    else:
        chk.block("INV-AG04", "no se pudo provisionar suite/perfil de control para fail-closed de menú")

    # ── INV-AG05: fail-closed en domainPermissions (recurso sin grant => NotGranted) ──
    if built and built.get("graph") is not None:
        dperms = built["graph"].get("domainPermissions", []) or []
        allow_on_miss = []
        for res in dperms:
            for act in (res.get("actions") or res.get("Actions") or []):
                eff = act.get("effect") if isinstance(act, dict) else None
                if eff in (0, "Allow"):
                    allow_on_miss.append((res.get("resourceCode") or res.get("code"),
                                          act.get("actionCode") or act.get("code")))
        chk.check("INV-AG05", len(allow_on_miss) == 0,
                  "domain resource sin permiso explícito -> domainPermissions con %d entradas, "
                  "0 en Allow-por-miss (fail-closed; corrige el fail-open AG-01). Allow espurios=%s"
                  % (len(dperms), allow_on_miss or "ninguno"))
    else:
        chk.block("INV-AG05", "no se pudo provisionar domain resource de control")

    # ── INV-AG06: la resolución de grants refleja los permisos efectivos del perfil ──
    if built and built.get("graph") is not None:
        pv_scopes = built["graph"].get("scopes", []) or []
        reads = [s for s in pv_scopes if ".read" in s.lower()]
        # El perfil concede exactamente 1 permiso (READ en optG) => exactamente 1 scope .read.
        chk.check("INV-AG06", len(reads) == 1 and len(ag_scopes) > 0,
                  "perfil de control con 1 grant (READ en optG) -> scopes .read=%d (refleja "
                  "exactamente el perfil); agente sembrado -> %d scopes (perfil distinto, "
                  "grants distintos)" % (len(reads), len(ag_scopes)))
    else:
        chk.block("INV-AG06", "no se pudo provisionar perfil de control")

    # ── INV-AG02 / INV-AG03: precedencia Deny>Allow y Override>Template ──
    # Con AT06 corregido (override de perfil resoluble por Props.Id), estas precedencias YA son
    # OBSERVABLES en caja negra: materializamos un Deny que sobrescribe el Allow del template y
    # observamos (a) que el grant desaparece del grafo (Deny domina Allow) y (b) que el permiso
    # queda IsOverride divergiendo del template original Allow (Override domina Template).
    ag23_done = False
    if built and built.get("profile_id") and built.get("graph") is not None:
        pid = built["profile_id"]
        base_reads = [s for s in (built["graph"].get("scopes") or []) if ".read" in s.lower()]
        prof = az.get("/api/v1/profiles/%s" % pid)
        perms = (prof.json or {}).get("permissions") or []
        allow_perm = next((p for p in perms if p.get("isAllowed") and not p.get("isDenied")), None)
        if allow_perm and base_reads:
            perm_id = allow_perm.get("permissionId")
            orig = allow_perm.get("originalFromTemplate") or {}
            template_was_allow = bool(orig.get("isAllowed"))
            ov = az.post("/api/v1/profiles/%s/permissions/%s/override?effect=deny" % (pid, perm_id), None)

            prev2 = az.get("/api/v1/profiles/%s/auth-graph/preview" % pid)
            reads_after = [s for s in (_parse_preview(prev2).get("scopes") or []) if ".read" in s.lower()]

            # INV-AG02: Deny domina sobre Allow -> el scope concedido por el template desaparece.
            chk.check("INV-AG02", ov.status == 204 and len(reads_after) < len(base_reads),
                      "override Deny sobre permiso Allow del template -> HTTP %d; scopes .read %d->%d "
                      "(Deny domina Allow: el grant desaparece del grafo; deny-wins OBSERVABLE tras "
                      "corregir AT06)" % (ov.status, len(base_reads), len(reads_after)))

            # INV-AG03: el permiso IsOverride diverge del template original (Allow) y domina.
            perm2 = next((p for p in ((az.get("/api/v1/profiles/%s" % pid).json or {}).get("permissions") or [])
                          if p.get("permissionId") == perm_id), {})
            chk.check("INV-AG03",
                      ov.status == 204 and perm2.get("isOverride") is True
                      and perm2.get("isDenied") is True and template_was_allow,
                      "permiso overrideado -> isOverride=%s isDenied=%s; template original isAllowed=%s "
                      "(el override diverge del template y DOMINA: override-wins OBSERVABLE)"
                      % (perm2.get("isOverride"), perm2.get("isDenied"), template_was_allow))
            ag23_done = True
    if not ag23_done:
        chk.pending("INV-AG02", "no se pudo provisionar el perfil de control para materializar Deny>Allow")
        chk.pending("INV-AG03", "no se pudo provisionar el perfil de control para materializar Override>Template")

    # ── INV-AG08: cliente externo obtiene su grafo vía /client/authenticate ──
    cc = login(rc.http, COMEX_CODE, COMEX_USER, SEED_PASSWORD, client_endpoint=True)
    ca = login(rc.http, AGRONORTE_CODE, AGRONORTE_USER, SEED_PASSWORD, client_endpoint=True)
    cc_graph = _client_graph(cc)
    chk.check("INV-AG08", cc.status == 200 and ca.status == 200 and bool(cc_graph),
              "/client/authenticate COMEX_ANDINA=%d, AGRONORTE=%d; grafo COMEX top-keys=%s "
              "(auditoría AG-03: 401 AUTH_006; ahora concede grafo)"
              % (cc.status, ca.status, list((cc_graph or {}).keys())[:8]))

    # ── INV-AG09: featureFlags con isEnabled y matchedCriteriaType ──
    flags = agente.get("featureFlags", []) or []
    well_formed = [f for f in flags if isinstance(f, dict)
                   and "isEnabled" in f and "matchedCriteriaType" in f]
    chk.check("INV-AG09", len(flags) > 0 and len(well_formed) == len(flags),
              "grafo agente.aduanas -> featureFlags=%d, todos con isEnabled+matchedCriteriaType "
              "(%d bien formados) sample=%s" % (len(flags), len(well_formed), flags[:1]))

    # ── INV-AG10: serialización en español (SD-08) ──
    # Auth-negativa (lección 1): DevAuth desactivado para forzar el 401 real.
    bad = rc.http.request("POST", "/api/v1/client/authenticate",
                          body={"tenantCode": COMEX_CODE, "username": COMEX_USER,
                                "password": "Credencial.Errada.999"},
                          extra_headers={"X-Disable-Dev-Auth": "true"})
    msg = (bad.json or {}).get("message", "") or ""
    spanish = any(w in msg.lower() for w in
                  ["verifique", "credenciales", "autenticar", "sesión", "inténtelo", "no pudimos"])
    chk.check("INV-AG10", bad.status == 401 and spanish,
              "/client/authenticate credencial errada -> HTTP %d message=%r (español; auditoría "
              "AG-07: mensaje en inglés). Nota: persiste divergencia de enums int(/auth/login) vs "
              "string(/client) — AG-06, contrato, no SD-08" % (bad.status, msg[:80]))

    return chk.results


# --- utilidades ------------------------------------------------------------

def _graph(resp):
    body = resp.json or {}
    return body.get("authorizationGraph") or {}


def _client_graph(resp):
    """El /client/authenticate devuelve el grafo como STRING JSON en `graph`."""
    import json as _json
    body = resp.json or {}
    g = body.get("graph")
    if isinstance(g, str):
        try:
            return _json.loads(g)
        except (ValueError, TypeError):
            return {}
    return g or {}


def _parse_preview(resp):
    """El preview devuelve {graph: '<json string>'}."""
    import json as _json
    body = resp.json or {}
    g = body.get("graph")
    if isinstance(g, str):
        try:
            return _json.loads(g)
        except (ValueError, TypeError):
            return None
    return g


def _last_code(rc, prefix):
    n = rc._counter.get(prefix, 0)
    return "%s_%s%02d" % (prefix, rc.run_id, n)
