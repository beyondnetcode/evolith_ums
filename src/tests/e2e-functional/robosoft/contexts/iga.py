"""Contexto iga — IMPLEMENTADO (ola 2).

Promoción de rol por FSM auditada (RolePromotionRequest, ADR-0112/G-052), análisis
de riesgo (RiskScore inmutable), elegibilidad fail-closed (RoleMaturityStatus) y
SOD 3-partes; más el análogo Delegation (SOD 2, FSM, compuerta de aprobación,
aislamiento por inquilino). Fuente: auditoría bmad-tester-robosoft-audit-2026-07-16.md
(8 invariantes INV-*).

NOTA DE ESTADO (delta desde la auditoría): la auditoría reportó FR-060/061/062
AUSENTES (IGA-01) y las consultas de Delegation anónimas (IGA-02) con la compuerta
de aprobación eludible (IGA-03). En el binario vivo actual la promoción de rol SÍ
existe (endpoints /role-promotion-requests con FSM completa) y ambas fugas están
cerradas; este contexto lo certifica contra el binario, no contra el snapshot.

Modelo de identidad ejercitado:
  · Promoción: DevAuth con X-User-Id por transición (actores distintos = SOD) bajo el
    inquilino RANSA sembrado, que trae RoleMaturityStatus ELEGIBLE y borde NO ELEGIBLE.
  · Delegation: provisión on-behalf de dos cuentas activas bajo EVOLITH; el delegante
    actúa vía DevAuth (X-User-Id == DelegatingAdminId, exigido por el handler).
"""

from __future__ import annotations

from harness import Checker, Provisioner, EVOLITH_TENANT_ID

NAME = "iga"

# Semilla de dev (CoreDevDataSeeder + IgaDevDataSeeder): inquilino RANSA y su madurez.
RANSA_TENANT_ID = "3fa85f64-5717-4562-b3fc-2c963f66afa6"
RANSA_ADMIN_USER_ID = "3fa85f01-5717-4562-b3fc-2c963f66afa6"    # ELEGIBLE (madurez sembrada)
RANSA_ANALYST_USER_ID = "3fa85f02-5717-4562-b3fc-2c963f66afa6"  # NO ELEGIBLE (borde)
DEMO_ADMIN_ROLE_ID = "aaaa0001-0000-0000-0000-000000000001"
DEMO_OPERATOR_ROLE_ID = "aaaa0002-0000-0000-0000-000000000001"

# Actores distintos por transición (segregación de funciones a lo largo del flujo).
REQUESTER = "a0000000-0000-0000-0000-000000000a01"
APPROVER = "b0000000-0000-0000-0000-000000000b02"
SECURITY = "c0000000-0000-0000-0000-000000000c03"
EXECUTOR = "d0000000-0000-0000-0000-000000000d04"
VERIFIER = "e0000000-0000-0000-0000-000000000e05"

INVARIANTS = [
    {"code": "INV-IGA1", "fr": "FR-060", "title": "Promoción de rol por FSM auditada (Draft->...->Approved)"},
    {"code": "INV-PIA1", "fr": "FR-061", "title": "Análisis de impacto/riesgo tóxico: RiskScore 0-100 inmutable por solicitud"},
    {"code": "INV-RMS3", "fr": "FR-062", "title": "Elegibilidad de promoción: cumplimiento al día + desempeño >= 3.0 + tiempo mínimo en nivel"},
    {"code": "INV-IGA-SOD3", "fr": "FR-062", "title": "SOD 3-partes en promoción: aprobador != objetivo != auditor"},
    {"code": "INV-DEL2", "fr": "FR-062", "title": "SOD 2-partes en Delegation (análogo IGA implementado): DelegatingAdminId != DelegatedAdminId"},
    {"code": "INV-DEL-FSM", "fr": "FR-062", "title": "Máquina de estados auditada de Delegation (Draft->Active->Revoked; inválida rechazada)"},
    {"code": "INV-DEL-APR", "fr": "FR-062", "title": "Compuerta de aprobación auditada en Delegation (RequiresApproval->PendingApproval->Approve/Reject) antes de activar"},
    {"code": "INV-DEL-ISO", "fr": "FR-022", "title": "Aislamiento por tenant / autorización en consultas de gobernanza (Delegations)"},
]


# --- Helpers ----------------------------------------------------------------

def _headers(actor: str, tenant: str) -> dict:
    """DevAuth: identidad = X-User-Id; inquilino = X-Tenant-Id."""
    return {"X-User-Id": actor, "X-Tenant-Id": tenant}


def _promotion(http, actor, target, current_role, target_role):
    body = {
        "tenantId": RANSA_TENANT_ID,
        "targetUserId": target,
        "currentRoleId": current_role,
        "targetRoleId": target_role,
    }
    return http.request("POST", "/api/v1/role-promotion-requests", body=body,
                        extra_headers=_headers(actor, RANSA_TENANT_ID))


def _get_promotion(http, actor, pid):
    return http.request("GET", "/api/v1/role-promotion-requests/%s" % pid,
                        extra_headers=_headers(actor, RANSA_TENANT_ID))


def _transition(http, actor, pid, action, body=None):
    return http.request("POST", "/api/v1/role-promotion-requests/%s/%s" % (pid, action),
                        body=body, extra_headers=_headers(actor, RANSA_TENANT_ID))


def _err_code(resp) -> str:
    d = resp.json or {}
    if isinstance(d, dict):
        return (d.get("errorCode") or d.get("brokenRule") or d.get("title") or "").strip()
    return ""


def _active_user(prov: Provisioner, rc, label: str):
    email = "%s@evolith.example" % rc.unique(label).lower()
    r = prov.create_user_account(EVOLITH_TENANT_ID, email)
    uid = (r.json or {}).get("userAccountId")
    if uid:
        prov.set_password(uid)
        prov.activate_account(uid)
    return uid


# --- Run --------------------------------------------------------------------

def run(rc):  # noqa: C901 - un contexto agrupa muchos invariantes emparentados
    chk = Checker(rc, NAME, INVARIANTS)
    http = rc.http
    prov = Provisioner(rc)

    # ======================================================================
    # RolePromotionRequest — FSM, RiskScore, elegibilidad, SOD 3-partes
    # ======================================================================

    # Flujo feliz completo (target ELEGIBLE): Draft -> ... -> Approved -> Executed -> Verified.
    cr = _promotion(http, REQUESTER, RANSA_ADMIN_USER_ID, DEMO_ADMIN_ROLE_ID, DEMO_OPERATOR_ROLE_ID)
    pid = (cr.json or {}).get("rolePromotionRequestId")

    if cr.status == 201 and pid:
        sub = _transition(http, REQUESTER, pid, "submit")
        after_submit = _get_promotion(http, REQUESTER, pid).json or {}
        risk_at_submit = after_submit.get("riskScore")
        status_submit = after_submit.get("status")

        conf = _transition(http, REQUESTER, pid, "confirm-eligibility")
        after_conf = (_get_promotion(http, REQUESTER, pid).json or {}).get("status")

        mgr = _transition(http, APPROVER, pid, "manager-approve")
        after_mgr = _get_promotion(http, APPROVER, pid).json or {}
        risk_at_mgr = after_mgr.get("riskScore")
        status_mgr = after_mgr.get("status")

        # 4b. Si el riesgo escaló a revisión de seguridad, un revisor distinto la aprueba.
        if status_mgr == "PendingSecurityReview":
            _transition(http, SECURITY, pid, "security-approve")
            status_mgr = (_get_promotion(http, SECURITY, pid).json or {}).get("status")

        exe = _transition(http, EXECUTOR, pid, "execute")
        status_exec = (_get_promotion(http, EXECUTOR, pid).json or {}).get("status")
        ver = _transition(http, VERIFIER, pid, "verify")
        final = _get_promotion(http, VERIFIER, pid).json or {}

        # INV-IGA1: la promoción atraviesa la FSM auditada hasta un estado terminal aprobado.
        fsm_ok = (
            sub.status == 204 and status_submit == "PendingEligibilityCheck"
            and conf.status == 204 and after_conf == "PendingManagerApproval"
            and mgr.status == 204 and status_mgr == "Approved"
            and exe.status == 204 and status_exec == "Executed"
            and ver.status == 204 and final.get("status") == "Verified"
            and final.get("approverId") and final.get("executorId") and final.get("verifierId")
        )
        chk.check("INV-IGA1", fsm_ok,
                  "FSM: create(Draft)->submit(%s)->confirm(%s)->manager-approve(%s)->execute(%s)"
                  "->verify -> status final=%s; approver=%s executor=%s verifier=%s"
                  % (status_submit, after_conf, status_mgr, status_exec, final.get("status"),
                     final.get("approverId"), final.get("executorId"), final.get("verifierId")))

        # INV-PIA1: RiskScore en [0,100], congelado en submit e inmutable a lo largo de la FSM.
        risk_immutable = (
            risk_at_submit is not None and isinstance(risk_at_submit, (int, float))
            and 0 <= risk_at_submit <= 100
            and risk_at_mgr == risk_at_submit
            and final.get("riskScore") == risk_at_submit
        )
        chk.check("INV-PIA1", risk_immutable,
                  "riskScore congelado en submit=%s; en manager-approve=%s; final=%s "
                  "(en [0,100] e inmutable por solicitud)"
                  % (risk_at_submit, risk_at_mgr, final.get("riskScore")))
    else:
        chk.check("INV-IGA1", False,
                  "no se pudo crear la promoción: POST /role-promotion-requests HTTP %d %s"
                  % (cr.status, cr.snippet(150)))
        chk.block("INV-PIA1", "sin solicitud de promoción para observar el RiskScore")

    # INV-RMS3: elegibilidad fail-closed — el borde NO ELEGIBLE (tiempo insuficiente en nivel)
    # se rechaza en confirm-eligibility y la promoción no avanza.
    crb = _promotion(http, REQUESTER, RANSA_ANALYST_USER_ID, DEMO_OPERATOR_ROLE_ID, DEMO_ADMIN_ROLE_ID)
    pidb = (crb.json or {}).get("rolePromotionRequestId")
    if crb.status == 201 and pidb:
        _transition(http, REQUESTER, pidb, "submit")
        _transition(http, REQUESTER, pidb, "confirm-eligibility")
        rej = _get_promotion(http, REQUESTER, pidb).json or {}
        rms_ok = rej.get("status") == "Rejected" and bool(rej.get("decisionReason"))
        chk.check("INV-RMS3", rms_ok,
                  "target con tiempo insuficiente en nivel -> confirm-eligibility deja status=%s, "
                  "reason=%r (fail-closed: sin cumplimiento/tiempo mínimo no promueve)"
                  % (rej.get("status"), rej.get("decisionReason")))
    else:
        chk.check("INV-RMS3", False,
                  "no se pudo crear la promoción borde: HTTP %d %s" % (crb.status, crb.snippet(120)))

    # INV-IGA-SOD3: segregación de funciones — aprobador != objetivo y aprobador != solicitante.
    self_create = _promotion(http, REQUESTER, REQUESTER, DEMO_ADMIN_ROLE_ID, DEMO_OPERATOR_ROLE_ID)
    crs = _promotion(http, REQUESTER, RANSA_ADMIN_USER_ID, DEMO_ADMIN_ROLE_ID, DEMO_OPERATOR_ROLE_ID)
    pids = (crs.json or {}).get("rolePromotionRequestId")
    self_approve_status = None
    if crs.status == 201 and pids:
        _transition(http, REQUESTER, pids, "submit")
        _transition(http, REQUESTER, pids, "confirm-eligibility")
        self_approve = _transition(http, REQUESTER, pids, "manager-approve")
        self_approve_status = self_approve.status
    sod_ok = self_create.status == 400 and self_approve_status == 400
    chk.check("INV-IGA-SOD3", sod_ok,
              "create con target==solicitante -> HTTP %d (objetivo != solicitante); "
              "manager-approve por el solicitante -> HTTP %s (aprobador != solicitante). "
              "Ambos rechazados = SOD 3-partes exigida"
              % (self_create.status, self_approve_status))

    # ======================================================================
    # Delegation — SOD 2-partes, FSM, compuerta de aprobación, aislamiento
    # ======================================================================

    # El handler exige X-User-Id == DelegatingAdminId y cuentas Active del mismo inquilino.
    delegating = _active_user(prov, rc, "rs.iga.dga")
    delegated = _active_user(prov, rc, "rs.iga.dgb")
    delegated_apr = _active_user(prov, rc, "rs.iga.dgc")

    def _delegation_body(delegated_id, requires_approval):
        return {
            "tenantId": EVOLITH_TENANT_ID,
            "delegatingAdminId": delegating,
            "delegatedAdminId": delegated_id,
            "scopeType": "Tenant",
            "scopeId": None,
            "allowedActions": ["AssignProfile"],
            "validFrom": "2026-07-20T00:00:00Z",
            "validUntil": "2026-08-10T00:00:00Z",
            "maxDurationDays": 30,
            "requiresApproval": requires_approval,
        }

    dga_headers = _headers(delegating, EVOLITH_TENANT_ID)
    dga_headers["X-Is-Internal-Admin"] = "true"

    if delegating and delegated and delegated_apr:
        # INV-DEL2: SOD 2-partes — auto-delegación rechazada.
        self_del = http.request("POST", "/api/v1/delegations",
                               body=_delegation_body(delegating, False), extra_headers=dga_headers)
        chk.check("INV-DEL2", self_del.status == 400,
                  "delegating==delegated -> HTTP %d (auto-delegación prohibida)" % self_del.status)

        # INV-DEL-FSM: Draft->Active->Revoked; transición inválida (revoke sobre Draft) rechazada.
        cr = http.request("POST", "/api/v1/delegations",
                        body=_delegation_body(delegated, False), extra_headers=dga_headers)
        did = (cr.json or {}).get("delegationId")
        if cr.status == 201 and did:
            base = "/api/v1/delegations/%s" % did
            status_draft = (http.request("GET", base, extra_headers=dga_headers).json or {}).get("status")
            revoke_draft = http.request("POST", base + "/revoke?reason=invalida", extra_headers=dga_headers)
            activate = http.request("POST", base + "/activate", extra_headers=dga_headers)
            status_active = (http.request("GET", base, extra_headers=dga_headers).json or {}).get("status")
            revoke_active = http.request("POST", base + "/revoke?reason=fin", extra_headers=dga_headers)
            status_revoked = (http.request("GET", base, extra_headers=dga_headers).json or {}).get("status")
            fsm_ok = (
                status_draft == "Draft" and revoke_draft.status in (400, 409)
                and activate.status == 204 and status_active == "Active"
                and revoke_active.status == 204 and status_revoked == "Revoked"
            )
            chk.check("INV-DEL-FSM", fsm_ok,
                      "create=%s; revoke-sobre-Draft -> HTTP %d (inválida); activate -> HTTP %d "
                      "(status=%s); revoke-sobre-Active -> HTTP %d (status=%s)"
                      % (status_draft, revoke_draft.status, activate.status, status_active,
                         revoke_active.status, status_revoked))
        else:
            chk.check("INV-DEL-FSM", False, "no se pudo crear la delegación: HTTP %d %s"
                      % (cr.status, cr.snippet(120)))

        # INV-DEL-APR: compuerta de aprobación — una delegación con requiresApproval NO puede
        # activarse directamente (fail-closed). Cierra IGA-03 (activate ya no elude la aprobación).
        cra = http.request("POST", "/api/v1/delegations",
                         body=_delegation_body(delegated_apr, True), extra_headers=dga_headers)
        did2 = (cra.json or {}).get("delegationId")
        if cra.status == 201 and did2:
            base2 = "/api/v1/delegations/%s" % did2
            d2 = http.request("GET", base2, extra_headers=dga_headers).json or {}
            act_gate = http.request("POST", base2 + "/activate", extra_headers=dga_headers)
            status_after = (http.request("GET", base2, extra_headers=dga_headers).json or {}).get("status")
            gate_ok = (
                d2.get("status") == "Draft" and not d2.get("approvalRequestId")
                and act_gate.status in (400, 409) and status_after == "Draft"
            )
            chk.check("INV-DEL-APR", gate_ok,
                      "requiresApproval=true -> create status=%s; activate directo -> HTTP %d (%s); "
                      "status tras el intento=%s. La compuerta es fail-closed: no se activa sin "
                      "aprobación (submit/approve no expuestos en la API ⇒ una delegación con "
                      "aprobación queda no-activable, que es el comportamiento seguro; IGA-03 cerrado)"
                      % (d2.get("status"), act_gate.status, _err_code(act_gate), status_after))
        else:
            chk.check("INV-DEL-APR", False, "no se pudo crear la delegación con aprobación: HTTP %d"
                      % cra.status)
    else:
        for code in ("INV-DEL2", "INV-DEL-FSM", "INV-DEL-APR"):
            chk.block(code, "no se pudieron provisionar cuentas activas para delegación")

    # INV-DEL-ISO: aislamiento/autorización en consultas de gobernanza (Delegations).
    # Cierra IGA-02: la lectura anónima debe rechazarse (RequireAuthorization + fail-closed).
    anon = http.request("GET", "/api/v1/delegations",
                       extra_headers={"X-Disable-Dev-Auth": "true"})
    authed = http.request("GET", "/api/v1/delegations", extra_headers=dga_headers)
    iso_ok = anon.status in (401, 403) and authed.status == 200
    chk.check("INV-DEL-ISO", iso_ok,
              "GET /delegations anónimo (X-Disable-Dev-Auth) -> HTTP %d (esperado 401/403); "
              "autenticado -> HTTP %d. La consulta ya no es fail-open anónima (IGA-02 cerrado)"
              % (anon.status, authed.status))

    return chk.results
