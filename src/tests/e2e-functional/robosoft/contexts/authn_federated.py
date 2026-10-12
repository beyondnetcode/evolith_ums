"""Contexto authn-federated — federación de identidad por inquilino (FR-011/012/042).

Registro de IdP por inquilino con estrategia inmutable, reglas de resolución por
prioridad/suite/dominio, fallback encadenado, usuario federado sin password local.
Fuente: auditoría bmad-tester-robosoft-audit-2026-07-16.md (9 invariantes).

IMPLEMENTADO — se ejecuta contra el backend vivo.

Lecciones del arnés aplicadas:
  · Contexto de inquilino en lecturas: las operaciones por id (activate/GetById/
    resolve-de-activos/passwords) se filtran por el inquilino AMBIENTE del token
    (EVOLITH). Provisionar en un inquilino desechable crea (201) pero luego esas
    lecturas dan 404. Por eso TODA la provisión federada se hace en EVOLITH.
  · Aislamiento entre corridas: las idp-configurations de prueba se crean en una
    SUITE dedicada (TMS) de EVOLITH y se DESACTIVAN al terminar, para no contaminar
    la resolución de futuras corridas.
  · Provisión on-behalf: X-Is-Internal-Admin + códigos únicos por corrida.
"""

from __future__ import annotations

from harness import EVOLITH_TENANT_ID, Checker

NAME = "authn-federated"

# SystemSuite dedicada (semilla) para las idp-configurations de prueba: raramente usada
# para federación, minimiza interferencia de resolución. TMS y ADUANAS son de la semilla.
_TMS_SUITE = "c60eb614-c5f3-4cc2-8c24-41dc0a82676c"
_ADUANAS_SUITE = "b49b8747-6d87-48ff-ba86-c9a90eeae0fc"

INVARIANTS = [
    {"code": "INV-AF01", "fr": "FR-011", "title": "Registrar IdP por tenant con estrategia inmutable (OIDC/SAML2/WS-Fed)"},
    {"code": "INV-AF02", "fr": "FR-042", "title": "Reglas de resolución por tenant+suite con PRIORIDAD"},
    {"code": "INV-AF03", "fr": "FR-042", "title": "Resolución por dominio (domainHints)"},
    {"code": "INV-AF04", "fr": "FR-042", "title": "Fallback ENCADENADO (chained fallback)"},
    {"code": "INV-AF05", "fr": "FR-042", "title": "Resolución dinámica en LOGIN usa las reglas (prioridad/suite/fallback)"},
    {"code": "INV-AF06", "fr": "FR-012", "title": "Usuario federado NO puede tener contraseña local activa"},
    {"code": "INV-AF07", "fr": "FR-012", "title": "La contraseña se desactiva AL VINCULAR (transición local->federado)"},
    {"code": "INV-AF08", "fr": "FR-011", "title": "Autenticación federada REAL contra Keycloak (OIDC) detrás de IdpAdapter"},
    {"code": "INV-AF09", "fr": "—", "title": "Patrón Result (sin excepciones) en registro de IdP"},
]


# --- Fachada de provisión federada (on-behalf EVOLITH) -----------------------

class _Api:
    def __init__(self, rc):
        self.rc = rc
        self.http = rc.http
        self.token = rc.admin_token

    def _post(self, path, body=None):
        return self.http.request("POST", path, body=body, token=self.token, internal_admin=True)

    def _get(self, path):
        return self.http.request("GET", path, token=self.token, internal_admin=True)

    # -- IdP de inquilino (FR-011) --
    def register_idp(self, code, strategy="Keycloak"):
        return self._post("/api/v1/tenants/%s/identity-providers" % EVOLITH_TENANT_ID,
                          {"code": code, "name": "RoboSoft %s" % code,
                           "description": "RoboSoft IdP probe", "strategy": strategy})

    def list_idps(self):
        r = self._get("/api/v1/tenants/%s/identity-providers" % EVOLITH_TENANT_ID)
        body = r.json
        return body if isinstance(body, list) else (body or {}).get("items") or []

    # -- idp-configurations (FR-042) --
    def create_config(self, suite, provider_type, priority, domains=None, fallback_to=None):
        body = {"tenantId": EVOLITH_TENANT_ID, "systemSuiteId": suite,
                "providerType": provider_type, "domainHints": domains or [],
                "configPayload": '{"authority":"https://kc.robosoft.local/%s"}' % self.rc.run_id,
                "secretRef": "vault://robosoft/%s" % self.rc.run_id,
                "resolutionPriority": priority}
        if fallback_to:
            body["fallbackToId"] = fallback_to
        r = self._post("/api/v1/idp-configurations", body)
        return r, (r.json or {}).get("idpConfigurationId")

    def activate_config(self, cid):
        return self._post("/api/v1/idp-configurations/%s/activate" % cid)

    def deactivate_config(self, cid):
        return self._post("/api/v1/idp-configurations/%s/deactivate" % cid)

    def resolve(self, suite, email_domain=None):
        q = "/api/v1/idp-configurations/resolve?tenantId=%s&systemSuiteId=%s" % (EVOLITH_TENANT_ID, suite)
        if email_domain:
            q += "&emailDomain=%s" % email_domain
        return self._get(q)

    def list_active(self, suite):
        r = self._get("/api/v1/idp-configurations?page=1&pageSize=100&status=active"
                      "&tenantId=%s&systemSuiteId=%s" % (EVOLITH_TENANT_ID, suite))
        items = (r.json or {}).get("items") or []
        return [it.get("idpConfigurationId") for it in items if it.get("idpConfigurationId")]

    def purge(self, suite):
        """Desactiva toda idp-configuration ACTIVA en (EVOLITH, suite): deja limpia la resolución.

        La suite TMS es dedicada a estas pruebas (la semilla no crea federación ahí), así que solo
        recoge artefactos de corridas previas de RoboSoft. Idempotente/re-corrible.
        """
        for cid in self.list_active(suite):
            self.deactivate_config(cid)

    # -- Usuarios federados (FR-012) --
    def create_federated_user(self, email, identity_ref):
        return self._post("/api/v1/user-accounts",
                          {"email": email, "tenantId": EVOLITH_TENANT_ID, "category": "Internal",
                           "identityReference": identity_ref, "identityReferenceType": "HrId"})

    def get_user(self, uid):
        return self._get("/api/v1/user-accounts/%s" % uid)

    def set_password(self, uid):
        return self._post("/api/v1/user-accounts/%s/passwords" % uid, {"password": "Evolith.Dev.2026!"})


def run(rc):
    chk = Checker(rc, NAME, INVARIANTS)
    api = _Api(rc)
    created_configs = []

    try:
        _run_checks(rc, chk, api, created_configs)
    finally:
        # Aislamiento entre corridas: desactivar las idp-configurations creadas.
        for cid in created_configs:
            api.deactivate_config(cid)

    return chk.results


def _run_checks(rc, chk, api, created_configs):
    # Clean slate: la resolución mira TODAS las configs activas de (EVOLITH, suite). Purgamos las
    # activas de la suite dedicada para que cada sub-test resuelva solo lo suyo (determinismo re-corrible).
    api.purge(_TMS_SUITE)

    # INV-AF01: registrar IdP por inquilino; estrategia inmutable (sin endpoint de edición:
    # solo activate/deactivate/remove). Verificamos 201 + id y que la consulta refleje la estrategia.
    code = rc.unique("RSIDP").upper().replace("_", "")
    reg = api.register_idp(code, strategy="Keycloak")
    idp_id = (reg.json or {}).get("identityProviderId")
    mine = [x for x in api.list_idps() if x.get("code") == code]
    strat = mine[0].get("strategy") if mine else None
    chk.check("INV-AF01", reg.status == 201 and bool(idp_id) and strat == "Keycloak",
              "POST /tenants/{EVOLITH}/identity-providers {strategy:Keycloak} -> HTTP %d id=%s; "
              "consulta refleja strategy=%s. Inmutable: no existe endpoint de edición de estrategia "
              "(solo activate/deactivate/remove)." % (reg.status, idp_id, strat))

    # INV-AF02: reglas de resolución por tenant+suite con PRIORIDAD (número menor gana).
    # Dos configs en (EVOLITH, TMS): A(prio=10) y B(prio=20); resolve devuelve A.
    ra, ca = api.create_config(_TMS_SUITE, "KEYCLOAK", 10)
    rb, cb = api.create_config(_TMS_SUITE, "GENERIC_OIDC", 20)
    for cid in (ca, cb):
        if cid:
            created_configs.append(cid)
            api.activate_config(cid)
    res = api.resolve(_TMS_SUITE)
    winner_prio = (res.json or {}).get("resolutionPriority")
    winner_id = (res.json or {}).get("idpConfigurationId")
    chk.check("INV-AF02",
              ra.status == 201 and rb.status == 201 and res.status == 200
              and winner_prio == 10 and winner_id == ca,
              "creadas A(prio10)+B(prio20) en (EVOLITH,TMS); GET /idp-configurations/resolve -> HTTP %d "
              "gana prio=%s id=%s (esperado A prio=10, %s). Menor prioridad numérica gana."
              % (res.status, winner_prio, winner_id, ca))
    # Aislar el siguiente sub-test: desactivar las configs de AF02.
    for cid in (ca, cb):
        if cid:
            api.deactivate_config(cid)

    # INV-AF03: resolución por dominio (domainHints). Config con domainHints=[dom]; resolve con
    # emailDomain coincidente -> domainMatched:true.
    dom = "rs-%s.robosoft.pe" % rc.run_id
    rd, cd = api.create_config(_TMS_SUITE, "KEYCLOAK", 5, domains=[dom])
    if cd:
        created_configs.append(cd)
        api.activate_config(cd)
    res_dom = api.resolve(_TMS_SUITE, email_domain="usuario@%s" % dom)
    matched = (res_dom.json or {}).get("domainMatched")
    chk.check("INV-AF03", rd.status == 201 and res_dom.status == 200 and matched is True,
              "config con domainHints=[%s]; resolve?emailDomain=usuario@%s -> HTTP %d domainMatched=%s "
              "(esperado true)" % (dom, dom, res_dom.status, matched))
    if cd:
        api.deactivate_config(cd)

    # INV-AF04: fallback ENCADENADO. El fallback es intra-inquilino e intra-suite (el handler rechaza
    # referencias colgantes y cruzadas). Config P(prio1, fallbackToId=B) con B(prio30) activo en la misma
    # suite (única activa): resolve gana P y EXPONE fallbackToId=B; al desactivar P, la resolución cae al
    # objetivo B. Se aisló de AF02/AF03 (desactivadas) para que B sea el único objetivo activo.
    rbase, c_b = api.create_config(_TMS_SUITE, "GENERIC_OIDC", 30)
    if c_b:
        created_configs.append(c_b)
        api.activate_config(c_b)
    rp, c_p = api.create_config(_TMS_SUITE, "KEYCLOAK", 1, fallback_to=c_b)
    fallback_link = None
    resolved_after = None
    if c_p:
        created_configs.append(c_p)
        api.activate_config(c_p)
        res_p = api.resolve(_TMS_SUITE)
        fallback_link = (res_p.json or {}).get("fallbackToId")
        api.deactivate_config(c_p)
        res_fb = api.resolve(_TMS_SUITE)
        resolved_after = (res_fb.json or {}).get("idpConfigurationId")
    if c_b:
        api.deactivate_config(c_b)
    chained_ok = (rp.status == 201 and fallback_link == c_b and resolved_after == c_b)
    chk.check("INV-AF04", chained_ok,
              "P(prio1, fallbackToId=B) + B(prio30) intra-suite: resolve gana P exponiendo fallbackToId=%s "
              "(==B %s); al desactivar P la resolución cae a id=%s (==B %s). El fallback cruzado de suite/"
              "inquilino se rechaza por diseño (referencia colgante). El failover en LOGIN ante IdP real "
              "caído se cubre en integración con Keycloak real (fuera de caja negra)."
              % (fallback_link, fallback_link == c_b, resolved_after, resolved_after == c_b))

    # INV-AF05: resolución dinámica en LOGIN usa las reglas. NO observable en caja negra: exige un login
    # federado real (multi-IdP con prioridad/fallback), que requiere infraestructura IdP real (Keycloak);
    # el StubIdpAuthAdapter solo acepta credenciales 'MOCK-*' y la respuesta de login no expone qué IdP
    # se eligió. La resolución por reglas en login se cubre en integración
    # (Fr042DbBackedRealIdpChainTests, con Keycloak real).
    chk.skip("INV-AF05",
             "no observable en caja negra: el login federado real por reglas exige IdP externo (Keycloak) "
             "no sembrable y la respuesta no revela el IdP elegido. Cubierto en integración "
             "(Fr042DbBackedRealIdpChainTests con Keycloak real).")

    # INV-AF06: usuario federado NO puede tener contraseña local activa. Creado con identityReference,
    # arranca hasActivePassword:false; fijar una contraseña local se rechaza (4xx).
    email = "%s@evolith.example" % rc.unique("rs.fed").lower()
    cu = api.create_federated_user(email, "HR-%s" % rc.unique("REF").upper())
    uid = (cu.json or {}).get("userAccountId")
    if cu.status == 201 and uid:
        acc = api.get_user(uid)
        has_pwd = (acc.json or {}).get("hasActivePassword")
        ident = (acc.json or {}).get("identityReference")
        pwd = api.set_password(uid)
        chk.check("INV-AF06", has_pwd is False and pwd.status >= 400,
                  "usuario federado (identityReference=%s) -> hasActivePassword=%s; "
                  "POST /user-accounts/{id}/passwords -> HTTP %d %s (esperado hasActivePassword=false + rechazo 4xx)"
                  % (ident, has_pwd, pwd.status, (pwd.json or {}).get("detail") or (pwd.json or {}).get("code") or ""))
    else:
        chk.block("INV-AF06", "no se pudo aprovisionar usuario federado: HTTP %d %s"
                  % (cu.status, cu.snippet(120)))

    # INV-AF07: la contraseña se desactiva AL VINCULAR (transición local->federado). NO ejercitable:
    # no existe operación de vinculación en runtime — IdentityReference se asigna solo en la construcción
    # de la cuenta (Create) y es inmutable después; no hay endpoint que convierta una cuenta local en
    # federada. La transición no forma parte de la superficie del API (verificar en fuente evolith_arch).
    chk.pending("INV-AF07",
                "no ejercitable en caja negra: no hay endpoint de vinculación local->federado "
                "(IdentityReference es inmutable tras la creación de la cuenta); la transición no está "
                "expuesta en el API. Coverage gap para producto — verificar en fuente.")

    # INV-AF08: autenticación federada REAL contra Keycloak (OIDC) detrás de IdpAdapter. NO ejercitable:
    # solo existe StubIdpAuthAdapter (acepta 'MOCK-*'); un Keycloak real no es sembrable en caja negra.
    chk.skip("INV-AF08",
             "no ejercitable en caja negra: la autenticación OIDC real requiere un IdP externo (Keycloak) "
             "no sembrable; en dev solo existe StubIdpAuthAdapter (credenciales 'MOCK-*'). "
             "Cubierto en integración con Keycloak real (KeycloakOidcRealHarnessTests).")

    # INV-AF09: patrón Result (sin excepciones) en el registro de IdP. Registrar dos veces el MISMO
    # código -> el 2º intento debe ser un conflicto limpio (409), no una excepción no controlada (500).
    dup_code = rc.unique("RSDUP").upper().replace("_", "")
    first = api.register_idp(dup_code, strategy="Keycloak")
    second = api.register_idp(dup_code, strategy="Keycloak")
    bad = api.register_idp(rc.unique("RSBAD").upper().replace("_", ""), strategy="NotAStrategy")
    chk.check("INV-AF09",
              first.status == 201 and second.status == 409 and bad.status == 400,
              "registro código duplicado: 1º -> HTTP %d, 2º (mismo código) -> HTTP %d (esperado 409 limpio, "
              "no 500); estrategia inválida -> HTTP %d (esperado 400). Auditoría 2026-07-16 halló 500 en el "
              "duplicado; ahora es 409 (Result Pattern)."
              % (first.status, second.status, bad.status))
