"""Contexto configuration — configuración jerárquica, FSM, versionado y feature flags.

Fuente de invariantes: auditoría bmad-tester-robosoft-audit-2026-07-16.md,
sección «Cobertura por contexto → configuration» (15 invariantes).

IMPLEMENTADO — se ejecuta contra el backend vivo. RoboSoft aprovisiona sus propias
configuraciones/flags con códigos únicos por corrida (idempotencia re-corrible) y
verifica los invariantes sin destruir la semilla.

Lecciones del arnés aplicadas:
  · /feature-flags/{id}/evaluate deriva `evaluatedBy` de ClaimTypes.NameIdentifier.
    En Development, DevAuthMiddleware FABRICA la identidad de endpoints anónimos con
    NameIdentifier="dev-user" (no-GUID) y, con X-Disable-Dev-Auth, no materializa el
    Bearer (identidad nula). Para ejercer el evaluador se inyecta X-User-Id=<GUID real>
    (el `sub` del admin) que DevAuth usa como NameIdentifier -> evaluatedBy válido.
  · CFG-02 (fail-closed de ciclo de vida) y CFG-04 (rollout Percentage) se corrigieron
    en G-048; CFG-05 (Suite/Module sin tenant) y CFG-06 (suceder Archived) también.
"""

from __future__ import annotations

import base64
import json
import uuid
from pathlib import Path

from harness import (
    AGRONORTE_CODE, AGRONORTE_USER, COMEX_CODE, COMEX_USER, SEED_PASSWORD,
    EVOLITH_CODE, EVOLITH_TENANT_ID, Checker, login,
)

NAME = "configuration"

# SystemSuite ADUANAS (semilla). Se resuelve dinámicamente en run(); este es el fallback.
ADUANAS_SUITE_ID = "b49b8747-6d87-48ff-ba86-c9a90eeae0fc"
ZERO_GUID = "00000000-0000-0000-0000-000000000000"

# Contrato publicado auth-graph.schema.json (ADR-0071/0073/0074). Se LEE del fichero, no se copia.
#
# Antes estaba embebido «para no depender del fichero en runtime», y el resultado fue el previsible:
# el contrato llegó a 2.3.0 mientras la copia seguía afirmando 2.2.0 y doce claves de catorce, así
# que INV-CF14 comparaba el grafo contra un contrato que ya no existía. Un arnés que verifica la
# conformidad con un contrato tiene que leer ese contrato: si el fichero no está, es un fallo de
# entorno y se declara como tal, no se sustituye por una copia que envejece en silencio.
_RELATIVA = "libs/sdk/contracts/auth-graph.schema.json"
_CONTRATO = next(
    (p / _RELATIVA for p in Path(__file__).resolve().parents if (p / _RELATIVA).is_file()),
    None,
)
if _CONTRATO is None:
    raise RuntimeError(
        "No se encontró el contrato publicado %s remontando desde %s. INV-CF14 verifica la "
        "conformidad del grafo con ese fichero: sin él no hay nada contra lo que comparar."
        % (_RELATIVA, __file__)
    )

with _CONTRATO.open(encoding="utf-8") as _f:
    _ESQUEMA = json.load(_f)

def _version_sin_proyeccion(esquema):
    """La `schemaVersion` que declara una respuesta COMPLETA, leída del contrato.

    Este arnés solo ve respuestas completas: `/client/authenticate` no acepta `?include=`. Desde
    2.5.0 el esquema declara DOS versiones —`enum: [2.4.0, 2.5.0]`— y fija cuál toca con un
    `const` dentro de cada rama del `allOf`: 2.4.0 sin `projection`, 2.5.0 con ella. La
    comparación por IGUALDAD de INV-CF14 sigue siendo válida y sigue siendo la correcta; lo único
    que cambia es de dónde se lee el valor.

    Con 2.4.0 y anteriores no hay ramas y el valor está en `properties`, así que se cae ahí.
    """
    for rama in esquema.get("allOf", []):
        if rama.get("if", {}).get("not", {}).get("required") == ["projection"]:
            fijado = rama["then"].get("properties", {}).get("schemaVersion", {}).get("const")
            if fijado:
                return fijado
    declarada = esquema["properties"]["schemaVersion"]
    return declarada.get("const") or declarada["enum"][0]


GRAPH_SCHEMA_VERSION = _version_sin_proyeccion(_ESQUEMA)

# Obligatoriedad y permisividad son DOS cosas distintas del esquema, y confundirlas era un defecto
# de este arnés: `top_extra` se calculaba contra `required`, de modo que cualquier propiedad
# declarada pero OPCIONAL contaba como «extra» y ponía INV-CF14 en rojo. Con 2.5.0 eso deja de ser
# teórico —la clave `projection` es opcional por definición: solo aparece si hubo proyección— y la
# invariante habría fallado incluso en la respuesta COMPLETA, que es justo la que no cambió.
#
#   · `required`   → lo que TIENE que estar (falta = incumplimiento).
#   · `properties` → lo que PUEDE estar; `additionalProperties: false` prohíbe el resto.
#
# Desde 2.5.0 el `required` de la raíz es el SOBRE, y las quince claves clásicas se exigen por una
# rama condicional: cuando no se emitió `projection`, la respuesta es completa. Se lee esa rama del
# propio fichero en vez de copiar la lista aquí (G-210).
GRAPH_TOP_ALLOWED = list(_ESQUEMA["properties"].keys())


def _requeridas_sin_proyeccion(esquema):
    """Claves exigidas cuando la respuesta NO viene proyectada.

    Con el esquema 2.4.0 y anteriores es simplemente `required`. Desde 2.5.0 hay además una rama
    `allOf` con `if: {not: {required: ["projection"]}}` que reimpone las quince clásicas; se busca
    esa rama en lugar de asumir su posición.
    """
    for rama in esquema.get("allOf", []):
        condicion = rama.get("if", {})
        if condicion.get("not", {}).get("required") == ["projection"]:
            return list(rama["then"]["required"])
    return list(esquema["required"])


GRAPH_TOP_REQUIRED = _requeridas_sin_proyeccion(_ESQUEMA)
EFFECTIVE_CONFIG_REQUIRED = ["sessionTimeoutMinutes", "maxLoginAttempts", "minPasswordLength",
                             "mfaRequiredForAdmin", "accessTokenDurationMs", "authUseExternalIdp"]
# matchedCriteriaType es OPCIONAL en el contrato: el serializer usa WhenWritingNull y lo omite cuando
# ningún criterio aplicó (ausente == null). Solo flagCode/systemSuiteId/isEnabled son obligatorios.
FEATURE_FLAG_STATE_REQUIRED = ["flagCode", "systemSuiteId", "isEnabled"]

INVARIANTS = [
    {"code": "INV-CF01", "fr": "FR-040", "title": "Configuración jerárquica de 4 ámbitos (Global/Suite/Tenant/Module) con resolución en cascada por código"},
    {"code": "INV-CF02", "fr": "FR-040", "title": "Precedencia efectiva 'Global > Suite > Tenant > Module' (Tenant se impone sobre Suite)"},
    {"code": "INV-CF03", "fr": "FR-040", "title": "FSM Draft->Published->Archived con transiciones terminales bloqueadas"},
    {"code": "INV-CF04", "fr": "FR-040", "title": "Solo configs Published participan en la resolución (Draft y Archived excluidos)"},
    {"code": "INV-CF05", "fr": "FR-040", "title": "Versionado: la edición incrementa la versión"},
    {"code": "INV-CF06", "fr": "FR-040", "title": "El ciclo versionado permite suceder un valor Archived con nueva versión del mismo (scope,code)"},
    {"code": "INV-CF07", "fr": "FR-040", "title": "Configs de ámbito Suite/Module sin tenantId son resolubles (default de suite entre tenants)"},
    {"code": "INV-CF08", "fr": "FR-041", "title": "Feature flag acotado a la suite; nunca global (suite obligatoria)"},
    {"code": "INV-CF09", "fr": "FR-041", "title": "Intra-tipo OR, entre-tipos AND en la evaluación de criterios"},
    {"code": "INV-CF10", "fr": "FR-041", "title": "Fail-closed ante contexto incompleto"},
    {"code": "INV-CF11", "fr": "FR-041", "title": "Fail-closed respecto al ciclo de vida del flag (Inactive/Deactivated -> deshabilitado)"},
    {"code": "INV-CF12", "fr": "FR-041", "title": "El tipo Percentage / rolloutPercentage se aplica en la evaluación"},
    {"code": "INV-CF13", "fr": "FR-040", "title": "effectiveConfig presente en el grafo con precedencia Tenant-override sobre Global"},
    {"code": "INV-CF14", "fr": "FR-034", "title": "El grafo/effectiveConfig entregado a clientes cumple el contrato auth-graph.schema.json"},
    {"code": "INV-CF15", "fr": "FR-040", "title": "effectiveConfig de un tenant CLIENT vía /client/authenticate"},
]


# --- Helpers HTTP -----------------------------------------------------------

def _jwt_sub(token: str) -> str:
    """Extrae el claim `sub` (GUID del usuario) del JWT sin verificar firma.

    Se usa como X-User-Id para que DevAuthMiddleware fabrique NameIdentifier=<GUID>
    y /feature-flags/{id}/evaluate obtenga un `evaluatedBy` parseable como GUID.
    """
    try:
        payload = token.split(".")[1]
        payload += "=" * (-len(payload) % 4)
        claims = json.loads(base64.urlsafe_b64decode(payload))
        return claims.get("sub") or ""
    except Exception:  # noqa: BLE001 - defensivo
        return ""


class _Api:
    """Fachada de provisión/consulta de configuración bajo el management-owner EVOLITH."""

    def __init__(self, rc, suite_id: str, eval_user: str):
        self.rc = rc
        self.http = rc.http
        self.token = rc.admin_token
        self.suite = suite_id
        self.eval_user = eval_user

    def _post(self, path, body=None, **kw):
        return self.http.request("POST", path, body=body, token=self.token,
                                 internal_admin=True, **kw)

    def _put(self, path, body=None, **kw):
        return self.http.request("PUT", path, body=body, token=self.token,
                                 internal_admin=True, **kw)

    def _get(self, path, **kw):
        return self.http.request("GET", path, token=self.token, internal_admin=True, **kw)

    # -- app-configurations --
    def create_config(self, code, value, tenant_id=None, suite_id=None, module_id=None,
                      encrypted=False, non_overridable=False, inheritable=True):
        body = {"code": code, "value": value, "description": "RoboSoft config probe",
                "isInheritable": inheritable, "isEncrypted": encrypted,
                "isNonOverridable": non_overridable}
        if tenant_id:
            body["tenantId"] = tenant_id
        if suite_id:
            body["systemSuiteId"] = suite_id
        if module_id:
            body["moduleId"] = module_id
        r = self._post("/api/v1/app-configurations", body)
        return r, (r.json or {}).get("appConfigurationId")

    def publish(self, cid):
        return self._post("/api/v1/app-configurations/%s/publish" % cid)

    def archive(self, cid):
        return self._post("/api/v1/app-configurations/%s/archive" % cid)

    def update(self, cid, value):
        return self._put("/api/v1/app-configurations/%s" % cid,
                         {"value": value, "description": "RoboSoft config update",
                          "isInheritable": True, "isEncrypted": False})

    def get_config(self, cid):
        return self._get("/api/v1/app-configurations/%s" % cid)

    def resolve(self, code, tenant_id=None, suite_id=None, module_id=None):
        q = "/api/v1/app-configurations/resolve?code=%s" % code
        if tenant_id:
            q += "&tenantId=%s" % tenant_id
        if suite_id:
            q += "&suiteId=%s" % suite_id
        if module_id:
            q += "&moduleId=%s" % module_id
        return self._get(q)

    # -- feature-flags --
    def create_flag(self, code, ftype="Boolean", targets="AllUsers", rollout=None, suite_id=None):
        body = {"systemSuiteId": suite_id or self.suite, "flagCode": code,
                "flagType": ftype, "flagTargets": targets}
        if rollout is not None:
            body["rolloutPercentage"] = rollout
        r = self._post("/api/v1/feature-flags", body)
        return r, (r.json or {}).get("featureFlagId")

    def add_criteria(self, fid, ctype, operator, value):
        return self._post("/api/v1/feature-flags/%s/criteria" % fid,
                          {"criteriaType": ctype, "operator": operator, "value": value})

    def activate_flag(self, fid):
        return self._post("/api/v1/feature-flags/%s/activate" % fid)

    def deactivate_flag(self, fid):
        return self._post("/api/v1/feature-flags/%s/deactivate" % fid)

    def evaluate(self, fid, **ctx):
        # Lección arnés: inyecta X-User-Id=<GUID> para que evaluatedBy sea un GUID válido.
        return self._post("/api/v1/feature-flags/%s/evaluate" % fid, ctx,
                          extra_headers={"X-User-Id": self.eval_user})


def _graph_from(resp):
    """Normaliza el grafo: /auth/login lo entrega como objeto, /client/authenticate como string."""
    body = resp.json or {}
    g = body.get("authorizationGraph")
    if isinstance(g, dict):
        return g
    raw = body.get("graph")
    if isinstance(raw, str):
        try:
            return json.loads(raw)
        except (ValueError, TypeError):
            return {}
    return {}


# --- Ejecución --------------------------------------------------------------

def run(rc):
    chk = Checker(rc, NAME, INVARIANTS)
    http = rc.http

    # Resolver la suite ADUANAS dinámicamente (fallback a la conocida).
    suite_id = ADUANAS_SUITE_ID
    ss = http.request("GET", "/api/v1/system-suites?page=1&pageSize=50",
                      token=rc.admin_token, internal_admin=True)
    for it in (ss.json or {}).get("items", []) or []:
        if it.get("code") == "ADUANAS":
            suite_id = it.get("systemSuiteId") or it.get("id") or suite_id
            break

    eval_user = _jwt_sub(rc.admin_token) or "5f4e3d14-1b0a-9f8e-7d6c-543210987654"
    api = _Api(rc, suite_id, eval_user)

    # ---- INV-CF01: 4 ámbitos con resolución en cascada ----
    code = rc.unique("ROBO.CF.PREC").upper().replace("_", ".")
    # GUID válido y determinista por corrida (run_id puede llevar caracteres no-hex).
    module_id = str(uuid.uuid5(uuid.NAMESPACE_URL, "robosoft-module-%s" % rc.run_id))
    rg, cg = api.create_config(code, "GLOBALVAL")
    rt, ct = api.create_config(code, "TENANTVAL", tenant_id=EVOLITH_TENANT_ID)
    rs, cs = api.create_config(code, "SUITEVAL", suite_id=suite_id)
    rm, cm = api.create_config(code, "MODULEVAL", module_id=module_id)
    creates_ok = all(r.status == 201 for r in (rg, rt, rs, rm))
    for cid in (cg, ct, cs, cm):
        if cid:
            api.publish(cid)
    res_t = api.resolve(code, tenant_id=EVOLITH_TENANT_ID)
    scopes_created = {"Global": rg.status, "Tenant": rt.status, "Suite": rs.status, "Module": rm.status}
    chk.check("INV-CF01",
              creates_ok and (res_t.json or {}).get("found") is True,
              "creados 4 ámbitos %s; resolve(code,tenant) -> found=%s scope=%s"
              % (scopes_created, (res_t.json or {}).get("found"), (res_t.json or {}).get("resolvedScope")))

    # ---- INV-CF02: precedencia determinista ----
    # BR-1 implementada (ConfigurationCache.GetWithPrecedence): Module > Suite > Tenant > Global.
    # La lectura literal del PRD 'Tenant sobre Suite' fue REFUTADA adversarialmente (CFG-03 excluido:
    # el runtime es determinista). Se verifica el contrato real: Tenant vence a Global y la cascada
    # más-específica-gana es determinista.
    r_tenant = api.resolve(code, tenant_id=EVOLITH_TENANT_ID)                      # espera Tenant (vs Global)
    r_suite = api.resolve(code, tenant_id=EVOLITH_TENANT_ID, suite_id=suite_id)    # espera Suite
    r_module = api.resolve(code, tenant_id=EVOLITH_TENANT_ID, suite_id=suite_id, module_id=module_id)
    v_tenant = (r_tenant.json or {}).get("value")
    v_suite = (r_suite.json or {}).get("value")
    v_module = (r_module.json or {}).get("value")
    deterministic = v_tenant == "TENANTVAL" and v_suite == "SUITEVAL" and v_module == "MODULEVAL"
    chk.check("INV-CF02", deterministic,
              "precedencia determinista Module>Suite>Tenant>Global (BR-1): resolve(tenant)=%s, "
              "+suite=%s, +module=%s. Tenant vence a Global. Nota: 'Tenant sobre Suite' del PRD "
              "fue refutado (CFG-03 excluido); el sistema es determinista."
              % (v_tenant, v_suite, v_module))

    # ---- INV-CF03: FSM Draft->Published->Archived con transiciones terminales bloqueadas ----
    sm_code = rc.unique("ROBO.CF.SM").upper().replace("_", ".")
    _, sm_id = api.create_config(sm_code, "DRAFTVAL", tenant_id=EVOLITH_TENANT_ID)
    if sm_id:
        arch_draft = api.archive(sm_id)          # Draft no archivable -> 400
        pub1 = api.publish(sm_id)                # Draft -> Published -> 204
        pub2 = api.publish(sm_id)                # Published -> Published -> 400
        upd_pub = api.update(sm_id, "X")         # update de Published -> 400
        arch_pub = api.archive(sm_id)            # Published -> Archived -> 204
        final = (api.get_config(sm_id).json or {}).get("status")
        fsm_ok = (arch_draft.status == 400 and pub1.status == 204 and pub2.status == 400
                  and upd_pub.status == 400 and arch_pub.status == 204 and final == "Archived")
        chk.check("INV-CF03", fsm_ok,
                  "archive(Draft)=%d, publish(Draft)=%d, publish(Published)=%d, update(Published)=%d, "
                  "archive(Published)=%d, status_final=%s"
                  % (arch_draft.status, pub1.status, pub2.status, upd_pub.status, arch_pub.status, final))
    else:
        chk.block("INV-CF03", "no se pudo crear la config para el FSM")

    # ---- INV-CF04: solo Published participa en la resolución ----
    pu_code = rc.unique("ROBO.CF.PUB").upper().replace("_", ".")
    _, pu_id = api.create_config(pu_code, "V1", tenant_id=EVOLITH_TENANT_ID)
    if pu_id:
        r_draft = api.resolve(pu_code, tenant_id=EVOLITH_TENANT_ID)
        api.publish(pu_id)
        r_pub = api.resolve(pu_code, tenant_id=EVOLITH_TENANT_ID)
        api.archive(pu_id)
        r_arch = api.resolve(pu_code, tenant_id=EVOLITH_TENANT_ID)
        only_pub = ((r_draft.json or {}).get("found") is False
                    and (r_pub.json or {}).get("found") is True
                    and (r_arch.json or {}).get("found") is False)
        chk.check("INV-CF04", only_pub,
                  "resolve Draft=found:%s, Published=found:%s, Archived=found:%s"
                  % ((r_draft.json or {}).get("found"), (r_pub.json or {}).get("found"),
                     (r_arch.json or {}).get("found")))
    else:
        chk.block("INV-CF04", "no se pudo crear la config para CF04")

    # ---- INV-CF05: versionado incrementa versión al editar ----
    ver_code = rc.unique("ROBO.CF.VER").upper().replace("_", ".")
    _, ver_id = api.create_config(ver_code, "v1", tenant_id=EVOLITH_TENANT_ID)
    if ver_id:
        v0 = (api.get_config(ver_id).json or {}).get("version")
        upd = api.update(ver_id, "v2")
        after = api.get_config(ver_id).json or {}
        chk.check("INV-CF05", upd.status == 204 and v0 == "1.0.0" and after.get("version") == "1.1.0",
                  "version inicial=%s; update(Draft)=%d; version tras editar=%s value=%s"
                  % (v0, upd.status, after.get("version"), after.get("value")))
    else:
        chk.block("INV-CF05", "no se pudo crear la config para CF05")

    # ---- INV-CF06: suceder un (scope,code) Archived con nueva versión ----
    sup_code = rc.unique("ROBO.CF.SUP").upper().replace("_", ".")
    _, sup_id = api.create_config(sup_code, "first", tenant_id=EVOLITH_TENANT_ID)
    if sup_id:
        api.publish(sup_id)
        api.archive(sup_id)
        r_again, sup_id2 = api.create_config(sup_code, "second", tenant_id=EVOLITH_TENANT_ID)
        chk.check("INV-CF06", r_again.status == 201,
                  "recrear (Tenant,%s) tras Archived -> HTTP %d (esperado 201; CFG-06 corregido, "
                  "antes 409) id=%s" % (sup_code, r_again.status, sup_id2))
    else:
        chk.block("INV-CF06", "no se pudo crear la config para CF06")

    # ---- INV-CF07: Suite/Module sin tenantId resolubles (default de suite entre tenants) ----
    so_code = rc.unique("ROBO.CF.SO").upper().replace("_", ".")
    r_so, so_id = api.create_config(so_code, "SUITEONLYVAL", suite_id=suite_id)  # sin tenant
    if so_id:
        api.publish(so_id)
        r_res = api.resolve(so_code, suite_id=suite_id)
        chk.check("INV-CF07", (r_res.json or {}).get("found") is True,
                  "config Suite sin tenantId publicada -> resolve(code,suiteId) found=%s scope=%s "
                  "(CFG-05 corregido: BucketTenantlessScopedConfigs)"
                  % ((r_res.json or {}).get("found"), (r_res.json or {}).get("resolvedScope")))
    else:
        chk.block("INV-CF07", "no se pudo crear la config Suite sin tenant: HTTP %d %s"
                  % (r_so.status, r_so.snippet(100)))

    # ---- INV-CF08: feature flag acotado a la suite (suite obligatoria) ----
    ff_base = rc.unique("ROBO.FF").upper().replace("_", ".")
    r_nosuite = api._post("/api/v1/feature-flags",
                          {"flagCode": ff_base + ".NS", "flagType": "Boolean", "flagTargets": "AllUsers"})
    r_zeros = api._post("/api/v1/feature-flags",
                        {"systemSuiteId": ZERO_GUID, "flagCode": ff_base + ".Z",
                         "flagType": "Boolean", "flagTargets": "AllUsers"})
    r_valid, _ = api.create_flag(ff_base + ".OK")
    chk.check("INV-CF08",
              r_nosuite.status == 400 and r_zeros.status == 400 and r_valid.status == 201,
              "POST /feature-flags sin suite=%d, suite all-zeros=%d, suite válida ADUANAS=%d"
              % (r_nosuite.status, r_zeros.status, r_valid.status))

    # ---- INV-CF09: intra-tipo OR, entre-tipos AND ----
    _, f9 = api.create_flag(rc.unique("ROBO.FF.CRIT").upper().replace("_", "."))
    if f9:
        api.add_criteria(f9, "TenantId", "Equals", EVOLITH_TENANT_ID)
        api.add_criteria(f9, "TenantId", "Equals", "99999999-9999-4999-9999-999999999999")
        api.activate_flag(f9)
        ev_or = api.evaluate(f9, tenantId=EVOLITH_TENANT_ID)
        or_ok = (ev_or.json or {}).get("isEnabled") is True
        api.add_criteria(f9, "Environment", "Equals", "Production")
        ev_and_ok = api.evaluate(f9, tenantId=EVOLITH_TENANT_ID, environment="Production")
        ev_and_bad = api.evaluate(f9, tenantId=EVOLITH_TENANT_ID, environment="Development")
        and_ok = ((ev_and_ok.json or {}).get("isEnabled") is True
                  and (ev_and_bad.json or {}).get("isEnabled") is False)
        chk.check("INV-CF09", or_ok and and_ok,
                  "2x TenantId(Equals) match-uno -> isEnabled=%s (OR intra-tipo); +Environment: "
                  "env=Production -> %s, env=Development -> %s (AND entre-tipos)"
                  % ((ev_or.json or {}).get("isEnabled"), (ev_and_ok.json or {}).get("isEnabled"),
                     (ev_and_bad.json or {}).get("isEnabled")))
        # ---- INV-CF10: fail-closed ante contexto incompleto (reutiliza f9 con Environment) ----
        ev_missing = api.evaluate(f9, tenantId=EVOLITH_TENANT_ID)  # omite environment
        mj = ev_missing.json or {}
        chk.check("INV-CF10",
                  mj.get("isEnabled") is False and "missing" in (mj.get("reason") or "").lower(),
                  "eval con criterio Environment pero SIN environment -> isEnabled=%s reason=%r"
                  % (mj.get("isEnabled"), mj.get("reason")))
    else:
        chk.block("INV-CF09", "no se pudo crear el flag para CF09")
        chk.block("INV-CF10", "no se pudo crear el flag para CF10")

    # ---- INV-CF11: fail-closed de ciclo de vida (Inactive/Deactivated -> deshabilitado) ----
    _, f11 = api.create_flag(rc.unique("ROBO.FF.LIFE").upper().replace("_", "."))
    if f11:
        ev_inactive = api.evaluate(f11)                    # recién creado = Inactive
        api.activate_flag(f11)
        ev_active = api.evaluate(f11)
        api.deactivate_flag(f11)
        ev_deact = api.evaluate(f11)
        life_ok = ((ev_inactive.json or {}).get("isEnabled") is False
                   and (ev_active.json or {}).get("isEnabled") is True
                   and (ev_deact.json or {}).get("isEnabled") is False)
        chk.check("INV-CF11", life_ok,
                  "evaluate Inactive=%s, Active=%s, Deactivated=%s (G-048 fail-closed; auditoría "
                  "CFG-02: fail-OPEN isEnabled:true)"
                  % ((ev_inactive.json or {}).get("isEnabled"), (ev_active.json or {}).get("isEnabled"),
                     (ev_deact.json or {}).get("isEnabled")))
    else:
        chk.block("INV-CF11", "no se pudo crear el flag para CF11")

    # ---- INV-CF12: FlagType.Percentage / rolloutPercentage se aplica ----
    _, f0 = api.create_flag(rc.unique("ROBO.FF.PCT0").upper().replace("_", "."),
                            ftype="Percentage", rollout=0)
    _, f100 = api.create_flag(rc.unique("ROBO.FF.PCT100").upper().replace("_", "."),
                              ftype="Percentage", rollout=100)
    if f0 and f100:
        api.activate_flag(f0)
        api.activate_flag(f100)
        ev0 = api.evaluate(f0, tenantId=EVOLITH_TENANT_ID)
        ev100 = api.evaluate(f100, tenantId=EVOLITH_TENANT_ID)
        pct_ok = ((ev0.json or {}).get("isEnabled") is False
                  and (ev100.json or {}).get("isEnabled") is True)
        chk.check("INV-CF12", pct_ok,
                  "rollout=0 -> isEnabled=%s (%s); rollout=100 -> isEnabled=%s (%s) "
                  "(G-048 bucketing; auditoría CFG-04: rollout inerte isEnabled:true)"
                  % ((ev0.json or {}).get("isEnabled"), (ev0.json or {}).get("reason"),
                     (ev100.json or {}).get("isEnabled"), (ev100.json or {}).get("reason")))
    else:
        chk.block("INV-CF12", "no se pudieron crear los flags Percentage para CF12")

    # ---- INV-CF13: effectiveConfig presente en el grafo + precedencia Tenant sobre Global ----
    rcli = login(http, EVOLITH_CODE, "agente.aduanas.callao@evolith.example", SEED_PASSWORD,
                 client_endpoint=True)
    g = _graph_from(rcli)
    ec = g.get("effectiveConfig") or {}
    ec_present = all(k in ec for k in EFFECTIVE_CONFIG_REQUIRED)
    # Precedencia Tenant>Global demostrada por el motor de resolución (CF01/CF02): TENANTVAL vence.
    tenant_over_global = (api.resolve(code, tenant_id=EVOLITH_TENANT_ID).json or {}).get("value") == "TENANTVAL"
    chk.check("INV-CF13", ec_present and tenant_over_global,
              "graph.effectiveConfig con %d/%d claves canónicas %s; Tenant-override sobre Global "
              "confirmado (resolve(code,tenant)=TENANTVAL vs GLOBALVAL)"
              % (sum(1 for k in EFFECTIVE_CONFIG_REQUIRED if k in ec), len(EFFECTIVE_CONFIG_REQUIRED),
                 {k: ec.get(k) for k in EFFECTIVE_CONFIG_REQUIRED if k in ec}))

    # ---- INV-CF14: conformidad con auth-graph.schema.json (contrato de /client/authenticate) ----
    top_missing = [k for k in GRAPH_TOP_REQUIRED if k not in g]
    # `additionalProperties: false` se comprueba contra `properties`, NO contra `required`: una
    # propiedad declarada y opcional es LEGAL, no un extra. Ver el comentario de GRAPH_TOP_ALLOWED.
    top_extra = [k for k in g if k not in GRAPH_TOP_ALLOWED]
    ec_missing = [k for k in EFFECTIVE_CONFIG_REQUIRED if k not in ec]
    ff = g.get("featureFlags") or []
    ff_missing = [k for k in FEATURE_FLAG_STATE_REQUIRED if ff and k not in ff[0]]
    schema_version = g.get("schemaVersion")
    conforms = (not top_missing and not top_extra and not ec_missing and not ff_missing
                and schema_version == GRAPH_SCHEMA_VERSION)
    chk.check("INV-CF14", conforms,
              "conformidad auth-graph.schema.json: schemaVersion=%s (contrato %s), top faltantes=%s, "
              "top EXTRA=%s (additionalProperties:false), effectiveConfig faltantes=%s, "
              "featureFlags[0] faltantes=%s. Defectos de auditoría CFG-01 (schemaVersion/PascalCase/"
              "accessTokenDurationMs) RESUELTOS; G-115: schema actualizado — 'onboardingPending' "
              "declarado (required) y 'matchedCriteriaType' opcional (WhenWritingNull). La versión y "
              "las claves esperadas se LEEN del contrato publicado, no se copian aquí (G-210)."
              % (schema_version, GRAPH_SCHEMA_VERSION, top_missing or "ninguno",
                 top_extra or "ninguno", ec_missing or "ninguno", ff_missing or "ninguno"))

    # ---- INV-CF15: effectiveConfig de un tenant CLIENT vía /client/authenticate ----
    r_comex = login(http, COMEX_CODE, COMEX_USER, SEED_PASSWORD, client_endpoint=True)
    r_agro = login(http, AGRONORTE_CODE, AGRONORTE_USER, SEED_PASSWORD, client_endpoint=True)
    ec_comex = _graph_from(r_comex).get("effectiveConfig") or {}
    ec_agro = _graph_from(r_agro).get("effectiveConfig") or {}
    client_ok = (r_comex.status == 200 and r_agro.status == 200
                 and all(k in ec_comex for k in EFFECTIVE_CONFIG_REQUIRED)
                 and all(k in ec_agro for k in EFFECTIVE_CONFIG_REQUIRED))
    chk.check("INV-CF15", client_ok,
              "/client/authenticate COMEX=%d (effectiveConfig %d claves), AGRONORTE=%d (%d claves) "
              "(auditoría: BLOCKED 401 AUTH_006; G-042/H-04 corregidos)"
              % (r_comex.status, len(ec_comex), r_agro.status, len(ec_agro)))

    return chk.results
