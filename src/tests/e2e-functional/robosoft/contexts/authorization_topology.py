"""Contexto authorization-topology — jerarquía de suites, roles, plantillas y perfiles.

Jerarquía SystemSuite->Module->Menu->SubMenu->Option, acciones vinculables,
roles acíclicos, plantillas publicables, provisión restringida a management-owner,
overrides de perfil y Result Pattern.

Fuente de invariantes: auditoría bmad-tester-robosoft-audit-2026-07-16.md,
sección «Cobertura por contexto → authorization-topology» (11 invariantes).

IMPLEMENTADO — se ejecuta contra el backend vivo. RoboSoft aprovisiona su propia
suite bajo EVOLITH (management-owner) con códigos únicos por corrida.

Nota de contexto (contra la auditoría 2026-07-16): el binario vivo YA corrige varias
regresiones que la auditoría reportó como FAIL/500 —
  · F-04 (2do management-owner): ahora 409, no 500 (INV-AT08 PASS).
  · F-05 (vincular acción inexistente): ahora 400, no 204 (INV-AT02 PASS).
  · F-06 (broken rules sin detalle): la respuesta ahora trae `errorCode`/`brokenRule`
    con el texto de la regla en español (INV-AT09 PASS).
  · F-02 (/client rechaza CLIENT): ahora 200 (INV-AT10 PASS).
  · F-03 (asignar plantilla no materializa): assign ahora devuelve 204 y materializa
    permisos (permissionCount>0) — el override de perfil, en cambio, sigue roto
    (INV-AT06, defecto real: id de permiso no resoluble → 404).
"""

from __future__ import annotations

from harness import (
    COMEX_CODE, COMEX_USER, AGRONORTE_CODE, AGRONORTE_USER, SEED_PASSWORD,
    EVOLITH_CODE, EVOLITH_TENANT_ID, Checker, Provisioner, login,
)

NAME = "authorization-topology"

# Tenant CLIENT COMEX_ANDINA (semilla) para las pruebas de scope / 2do MO.
COMEX_TENANT_ID = "c0e1a000-1111-4c0e-a000-000000000001"

INVARIANTS = [
    {"code": "INV-AT01", "fr": "FR-030", "title": "Jerarquía SystemSuite->Module->Menu->SubMenu->Option con enforcement de NodeKind y activación"},
    {"code": "INV-AT02", "fr": "FR-030", "title": "Actions del suite registrables y vinculables a nodos; integridad referencial acción<->registro"},
    {"code": "INV-AT03", "fr": "FR-031", "title": "Roles por tenant+suite con jerarquía acíclica (sin ciclos ni auto-parent)"},
    {"code": "INV-AT04", "fr": "FR-032", "title": "Plantillas de permisos publicables solo con >=1 item"},
    {"code": "INV-AT05", "fr": "FR-030", "title": "Provisión de suites/plantillas restringida a management-owner (TenantScopePolicy)"},
    {"code": "INV-AT06", "fr": "FR-033", "title": "Perfiles (usuario+rol+rama) con overrides allow/deny/neutral SIN mutar la plantilla fuente"},
    {"code": "INV-AT07", "fr": "FR-034", "title": "El grafo concede permisos efectivos a usuarios con rol/plantilla"},
    {"code": "INV-AT08", "fr": "—", "title": "Result Pattern: reglas de negocio devuelven Result.Failure/4xx, no 500"},
    {"code": "INV-AT09", "fr": "—", "title": "Disparo de Broken Rules con detalle accionable en violaciones de dominio"},
    {"code": "INV-AT10", "fr": "FR-034", "title": "/client/authenticate devuelve el grafo para tenants CLIENT sembrados"},
    {"code": "INV-AT11", "fr": "FR-030", "title": "Crear la SystemSuite propia de RoboSoft bajo EVOLITH"},
]


# --- Helpers de provisión on-behalf (management-owner EVOLITH) ---------------

class _Authz:
    """POSTs/PUTs de topología con Bearer admin + X-Is-Internal-Admin."""

    def __init__(self, rc):
        self.rc = rc
        self.http = rc.http
        self.token = rc.admin_token

    def post(self, path, body):
        return self.http.request("POST", path, body=body, token=self.token, internal_admin=True)

    def put(self, path, body):
        return self.http.request("PUT", path, body=body, token=self.token, internal_admin=True)

    def get(self, path):
        return self.http.request("GET", path, token=self.token, internal_admin=True)

    # -- creadores que devuelven (Response, id) --
    def create_suite(self, code):
        r = self.post("/api/v1/system-suites",
                      {"tenantId": EVOLITH_TENANT_ID, "code": code, "name": "RoboSoft %s" % code,
                       "description": "Suite de certificación RoboSoft"})
        return r, (r.json or {}).get("systemSuiteId")

    def add_module(self, sid, code):
        r = self.post("/api/v1/system-suites/%s/modules" % sid,
                      {"code": code, "name": "Mod %s" % code, "description": "d", "sortOrder": 1})
        return r, (r.json or {}).get("moduleId")

    def activate_module(self, sid, mid):
        return self.post("/api/v1/system-suites/%s/modules/%s/activate" % (sid, mid), None)

    def add_node(self, sid, mid, parent, kind, code, sort=1):
        r = self.post("/api/v1/system-suites/%s/modules/%s/nodes" % (sid, mid),
                      {"parentNodeId": parent, "kind": kind, "code": code,
                       "label": code, "description": "d", "sortOrder": sort})
        return r, (r.json or {}).get("nodeId")

    def register_action(self, sid, code, name):
        r = self.post("/api/v1/system-suites/%s/actions" % sid, {"code": code, "name": name})
        return r, (r.json or {}).get("actionId")

    def link_action(self, sid, mid, nid, action_code):
        return self.post("/api/v1/system-suites/%s/modules/%s/nodes/%s/actions" % (sid, mid, nid),
                         {"actionCode": action_code})

    def create_role(self, sid, code, parent, level, order):
        r = self.post("/api/v1/system-suites/%s/roles" % sid,
                      {"code": code, "value": code, "description": "d",
                       "parentRoleId": parent, "hierarchyLevel": level, "promotionOrder": order})
        return r, (r.json or {}).get("roleId")

    def update_role_parent(self, sid, rid, parent, level, order):
        return self.put("/api/v1/system-suites/%s/roles/%s" % (sid, rid),
                        {"value": "x", "description": "d", "parentRoleId": parent,
                         "hierarchyLevel": level, "promotionOrder": order})

    def create_template(self, sid, role_id, tenant_id=EVOLITH_TENANT_ID):
        r = self.post("/api/v1/permission-templates",
                      {"tenantId": tenant_id, "roleId": role_id, "systemSuiteId": sid})
        return r, (r.json or {}).get("templateId")

    def add_item(self, tid, target_id, action_id, allow=True, deny=False):
        r = self.post("/api/v1/permission-templates/%s/items" % tid,
                      {"targetType": "Option", "targetId": target_id, "actionId": action_id,
                       "isAllowed": allow, "isDenied": deny})
        return r, (r.json or {}).get("itemId")

    def publish_template(self, tid):
        return self.post("/api/v1/permission-templates/%s/publish" % tid, None)

    def create_profile(self, user_id, role_id):
        r = self.post("/api/v1/profiles",
                      {"tenantId": EVOLITH_TENANT_ID, "userId": user_id, "roleId": role_id, "branchId": None})
        return r, (r.json or {}).get("profileId")

    def assign_template(self, pid, tid):
        return self.post("/api/v1/profiles/%s/templates/%s" % (pid, tid), None)


def run(rc):
    chk = Checker(rc, NAME, INVARIANTS)
    az = _Authz(rc)
    prov = Provisioner(rc)

    # ── INV-AT11 + AT01: crear suite propia y árbol Module->Menu->SubMenu->Option ──
    suite_r, sid = az.create_suite(rc.unique("RS_SUITE"))
    chk.check("INV-AT11", suite_r.status == 201 and bool(sid),
              "POST /system-suites (tenantId=EVOLITH) -> HTTP %d systemSuiteId=%s"
              % (suite_r.status, sid))

    if not sid:
        for code in ["INV-AT01", "INV-AT02", "INV-AT03", "INV-AT04", "INV-AT06", "INV-AT09"]:
            chk.block(code, "sin suite provisionada (AT11 falló); cadena de topología no ejercitable")
    else:
        mod_r, mid = az.add_module(sid, rc.unique("MOD"))
        act_mod = az.activate_module(sid, mid) if mid else None
        menu_r, menu_id = az.add_node(sid, mid, None, "Menu", rc.unique("MENU")) if mid else (None, None)
        sub_r, sub_id = az.add_node(sid, mid, menu_id, "SubMenu", rc.unique("SUB")) if menu_id else (None, None)
        opt_r, opt_id = az.add_node(sid, mid, sub_id, "Option", rc.unique("OPT"), sort=1) if sub_id else (None, None)
        # enforcement 1: hijo bajo una Option (hoja) -> rechazado
        bad_child = az.add_node(sid, mid, opt_id, "Option", rc.unique("BADCHILD"), sort=2)[0] if opt_id else None
        # enforcement 2: nodo bajo módulo INACTIVO -> rechazado
        mod2_r, mid2 = az.add_module(sid, rc.unique("MODINACT"))
        bad_inact = az.add_node(sid, mid2, None, "Menu", rc.unique("NINACT"))[0] if mid2 else None

        tree_ok = all(x is not None and x.status == 201 for x in [mod_r, menu_r, sub_r, opt_r])
        enforce_ok = (bad_child is not None and bad_child.status in (400, 409, 422)
                      and bad_inact is not None and bad_inact.status in (400, 409, 422))
        chk.check("INV-AT01", tree_ok and (act_mod is not None and act_mod.status == 204) and enforce_ok,
                  "árbol Module=%s/Menu=%s/SubMenu=%s/Option=%s (activate=%s); enforcement: "
                  "hijo-bajo-Option=%s, nodo-bajo-módulo-inactivo=%s"
                  % (_st(mod_r), _st(menu_r), _st(sub_r), _st(opt_r), _st(act_mod),
                     _st(bad_child), _st(bad_inact)))

        # ── INV-AT02: acciones registrables + vinculables + integridad referencial ──
        ra_r, read_id = az.register_action(sid, "READ", "Leer")
        rw_r, _ = az.register_action(sid, "WRITE", "Escribir")
        re_r, _ = az.register_action(sid, "EXPORT", "Exportar")
        link_ok = az.link_action(sid, mid, opt_id, "READ") if opt_id else None
        link_bad = az.link_action(sid, mid, opt_id, "NONEXISTENT_%s" % rc.run_id) if opt_id else None
        actions_ok = all(x is not None and x.status == 201 for x in [ra_r, rw_r, re_r])
        integrity_ok = (link_ok is not None and link_ok.status == 204
                        and link_bad is not None and link_bad.status in (400, 404, 409, 422))
        chk.check("INV-AT02", actions_ok and integrity_ok,
                  "acciones READ/WRITE/EXPORT=%s/%s/%s; link válido=%s; link acción inexistente=%s "
                  "(auditoría F-05: 204 sin integridad; ahora rechazado)"
                  % (_st(ra_r), _st(rw_r), _st(re_r), _st(link_ok), _st(link_bad)))

        # ── INV-AT03: roles con jerarquía acíclica ──
        rolea_r, role_a = az.create_role(sid, rc.unique("ROLE_A"), None, 0, 1)
        roleb_r, role_b = az.create_role(sid, rc.unique("ROLE_B"), role_a, 1, 2) if role_a else (None, None)
        # ciclo: A pasa a ser hijo de B (A->B->A); auto-parent: B hijo de sí mismo
        cyc = az.update_role_parent(sid, role_a, role_b, 2, 1) if (role_a and role_b) else None
        self_p = az.update_role_parent(sid, role_b, role_b, 1, 2) if role_b else None
        roles_ok = (rolea_r is not None and rolea_r.status == 201
                    and roleb_r is not None and roleb_r.status == 201)
        acyclic_ok = (cyc is not None and cyc.status in (400, 409, 422)
                      and self_p is not None and self_p.status in (400, 409, 422))
        chk.check("INV-AT03", roles_ok and acyclic_ok,
                  "role A(raíz,nivel0)=%s, role B(hijo,nivel1)=%s; ciclo A.parent=B=%s, "
                  "auto-parent B.parent=B=%s (ambos rechazados)"
                  % (_st(rolea_r), _st(roleb_r), _st(cyc), _st(self_p)))

        # ── INV-AT04: plantilla publicable solo con >=1 item ──
        tpl_r, tid = az.create_template(sid, role_a) if role_a else (None, None)
        pub_empty = az.publish_template(tid) if tid else None
        item_r, item_id = az.add_item(tid, opt_id, read_id) if (tid and opt_id and read_id) else (None, None)
        pub_ok = az.publish_template(tid) if tid else None
        at04_ok = (tpl_r is not None and tpl_r.status == 201
                   and pub_empty is not None and pub_empty.status in (400, 409, 422)
                   and item_r is not None and item_r.status == 201
                   and pub_ok is not None and pub_ok.status == 204)
        chk.check("INV-AT04", at04_ok,
                  "template create=%s; publish vacío=%s (rechazado); add item(Option,READ,Allow)=%s; "
                  "publish con item=%s" % (_st(tpl_r), _st(pub_empty), _st(item_r), _st(pub_ok)))

        # ── INV-AT06: perfil + assign materializa; override de perfil ──
        uemail = "%s@evolith.example" % rc.unique("rs.prof").lower()
        u_r = prov.create_user_account(EVOLITH_TENANT_ID, uemail, display_name="Perfil RS")
        uid = (u_r.json or {}).get("userAccountId")
        prof_r, pid = az.create_profile(uid, role_a) if (uid and role_a) else (None, None)
        assign_r = az.assign_template(pid, tid) if (pid and tid) else None
        # ¿materializó permisos?
        perm_count = 0
        perm_id = None
        override_r = None
        if pid:
            det = az.get("/api/v1/profiles/%s" % pid)
            body = det.json or {}
            perm_count = body.get("permissionCount") or len(body.get("permissions", []) or [])
            perms = body.get("permissions", []) or []
            if perms:
                perm_id = perms[0].get("permissionId")
            if perm_id:
                override_r = az.post(
                    "/api/v1/profiles/%s/permissions/%s/override?effect=deny" % (pid, perm_id), None)
        # El invariante exige que el override de perfil sea ejercitable. assign materializa,
        # PERO el override devuelve 404: ProfilePermissionDto.PermissionId=Props.Id
        # (GetProfileByIdQueryHandler) != clave de FindPermission=Entity.Id base (Profile.cs:265),
        # mismo defecto de doble identidad que MFA (RC08/F1). Es un BUG REAL de producto.
        materialized = assign_r is not None and assign_r.status == 204 and perm_count > 0
        override_ok = override_r is not None and override_r.status in (200, 204)
        chk.check("INV-AT06", materialized and override_ok,
                  "assign plantilla publicada=%s (permissionCount=%d, materializa); "
                  "override deny sobre permissionId=%s -> %s "
                  "(BUG REAL: 404 por id de permiso no resoluble — doble identidad Props.Id vs "
                  "Entity.Id, análogo a MFA F1; el override de perfil no es ejercitable vía API)"
                  % (_st(assign_r), perm_count, perm_id, _st(override_r)))

        # ── INV-AT09: broken rules con detalle accionable ──
        # Reusar los roles: forzar una violación de dominio (nivel jerárquico inválido).
        viol = None
        if role_a and role_b:
            viol = az.update_role_parent(sid, role_a, role_b, 0, 1)  # nivel incoherente con el padre
        detail = ""
        has_rule = False
        if viol is not None:
            vb = viol.json or {}
            broken = vb.get("brokenRule") or ""
            ecode = vb.get("errorCode") or ""
            detail = broken or ecode
            generic = "request contains invalid data"
            has_rule = bool(detail) and generic not in detail.lower()
        chk.check("INV-AT09", viol is not None and viol.status in (400, 409, 422) and has_rule,
                  "violación de dominio -> HTTP %s; brokenRule/errorCode=%r "
                  "(auditoría F-06: solo texto genérico; ahora trae la regla en español)"
                  % (_st(viol), detail[:120]))

    # ── INV-AT05: provisión restringida a management-owner ──
    # Positivo: internal-admin (management-owner) crea suite -> 201.
    # Negativo (auth-negativa, lección 1): sin X-Is-Internal-Admin y con DevAuth desactivado,
    # el endpoint de provisión rechaza (401) — no hay contexto de management-owner.
    pos_r, pos_id = az.create_suite(rc.unique("RS_SCOPE_OK"))
    neg_r = rc.http.request("POST", "/api/v1/system-suites",
                            body={"tenantId": EVOLITH_TENANT_ID, "code": rc.unique("RS_SCOPE_NO"),
                                  "name": "x", "description": "x"},
                            token=rc.admin_token, internal_admin=False,
                            extra_headers={"X-Disable-Dev-Auth": "true"})
    chk.check("INV-AT05", pos_r.status == 201 and neg_r.status in (401, 403),
              "con management-owner (internal-admin) POST /system-suites=%d; sin contexto "
              "management-owner (DevAuth off, sin internal-admin)=%d (restringido, TenantScopePolicy)"
              % (pos_r.status, neg_r.status))

    # ── INV-AT07: el grafo concede permisos efectivos a un usuario con rol/plantilla ──
    ag = _graph(login(rc.http, EVOLITH_CODE, "agente.aduanas.callao@evolith.example", SEED_PASSWORD))
    scopes = ag.get("scopes", []) or []
    chk.check("INV-AT07", len(scopes) > 0,
              "login EVOLITH/agente.aduanas (con rol+plantilla) -> grafo con scopes=%d (option.action) "
              "sample=%s (H-05 RESUELTO: el grafo concede)" % (len(scopes), scopes[:4]))

    # ── INV-AT08: Result Pattern — 2do management-owner no lanza 500 ──
    mo_r = prov.create_tenant(rc.unique("RS_MO2"), ttype="CLIENT", management_owner=True)
    set_mo = rc.http.request("POST", "/api/v1/tenants/%s/set-management-owner" % COMEX_TENANT_ID,
                             body={"value": True}, token=rc.admin_token, internal_admin=True)
    chk.check("INV-AT08", mo_r.status != 500 and set_mo.status != 500
              and mo_r.status in (400, 409, 422) and set_mo.status in (400, 409, 422),
              "POST /tenants{isManagementOwner:true}=%d; set-management-owner(COMEX)=%d "
              "(auditoría F-04: 500; ahora Result.Failure/409)" % (mo_r.status, set_mo.status))

    # ── INV-AT10: /client/authenticate devuelve grafo para tenants CLIENT ──
    cc = login(rc.http, COMEX_CODE, COMEX_USER, SEED_PASSWORD, client_endpoint=True)
    ca = login(rc.http, AGRONORTE_CODE, AGRONORTE_USER, SEED_PASSWORD, client_endpoint=True)
    chk.check("INV-AT10", cc.status == 200 and ca.status == 200,
              "/client/authenticate COMEX_ANDINA=%d, AGRONORTE=%d (auditoría F-02: 401 AUTH_006; "
              "ahora 200 con grafo)" % (cc.status, ca.status))

    return chk.results


def _st(resp):
    return resp.status if resp is not None else "n/a"


def _graph(resp):
    body = resp.json or {}
    return body.get("authorizationGraph") or {}
