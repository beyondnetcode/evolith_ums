"""Contexto approvals-compliance — IMPLEMENTADO (ola 2).

ApprovalRequest (SOD, terminalidad), workflow + checklist de documentos requeridos,
ciclo de vida de UserDocument (FSM), AccessEnforcementPolicy, Result Pattern y
códigos HTTP. Fuente de invariantes: auditoría bmad-tester-robosoft-audit-2026-07-16.md
sección «Cobertura por contexto → approvals-compliance» (22 invariantes INV-*).

Se ejecuta contra el backend vivo. RoboSoft aprovisiona sus propios agregados con
códigos únicos por corrida y no destruye la semilla. Regla de oro (SD-05): cada
resultado carga el código HTTP y el fragmento de dominio que lo justifica.

Modelo de identidad ejercitado:
  · Provisión de cuentas objetivo: on-behalf bajo EVOLITH (Bearer admin + X-Is-Internal-Admin).
  · Solicitante/decisor de aprobación: DevAuth con X-User-Id fijo (createdBy determinista)
    y X-Tenant-Id=EVOLITH (management-owner ⇒ EnsureManagementOwnerScope pasa).
"""

from __future__ import annotations

from harness import Checker, Provisioner, EVOLITH_TENANT_ID

NAME = "approvals-compliance"

# Semilla de dev (CoreDevDataSeeder): sistema y rol reales para poblar la solicitud.
DEMO_SYSTEM_ID = "dddd0001-0000-0000-0000-000000000001"  # DemoSystemSuiteId
DEMO_ROLE_ID = "aaaa0001-0000-0000-0000-000000000001"    # DemoAdminRoleId

# Actores estables (DevAuth X-User-Id): createdBy determinista para observar SoD (G-119).
# El happy-path (AR4) aprueba con un actor DISTINTO al creador; AR3 aprueba con el MISMO (self-approval).
CREATOR_ACTOR = "f0000000-0000-0000-0000-0000000000f1"
APPROVER_ACTOR = "f0000000-0000-0000-0000-0000000000f2"

INVARIANTS = [
    {"code": "INV-AR1", "fr": "FR-050", "title": "ApprovalRequest: Approve/Reject solo desde Pending (irreversible)"},
    {"code": "INV-AR2", "fr": "FR-050", "title": "Decisión terminal: no se puede re-decidir una request Approved/Rejected"},
    {"code": "INV-AR3", "fr": "FR-050", "title": "Segregación de deberes: el solicitante (createdBy) NO puede aprobar su propia request"},
    {"code": "INV-AR4", "fr": "FR-050", "title": "Flujo de aprobación (happy-path) completa Pending->Approved"},
    {"code": "INV-WF1", "fr": "FR-050", "title": "Workflow declara checklist de documentos requeridos (alta)"},
    {"code": "INV-WF2", "fr": "FR-050", "title": "El checklist de documentos requeridos es legible por el cliente"},
    {"code": "INV-WF3", "fr": "FR-050", "title": "Un documento requerido puede eliminarse vía API"},
    {"code": "INV-WF4", "fr": "—", "title": "Alta duplicada de documento requerido se maneja limpiamente (Result Pattern)"},
    {"code": "INV-WF5", "fr": "FR-050", "title": "Documentos requeridos se exigen como precondición al crear/aprobar la request"},
    {"code": "INV-UD1", "fr": "FR-052", "title": "Upload inicia en PendingReview y exige ExpirationDate>IssueDate"},
    {"code": "INV-UD2", "fr": "FR-052", "title": "PendingReview->Valid (validate); Valid terminal para validate/reject"},
    {"code": "INV-UD3", "fr": "FR-052", "title": "PendingReview->Rejected (reject)"},
    {"code": "INV-UD4", "fr": "FR-052", "title": "Expired solo alcanzable desde Valid (FSM)"},
    {"code": "INV-UD5", "fr": "FR-052", "title": "Expire idempotente/terminal (no re-expirar)"},
    {"code": "INV-UD6", "fr": "FR-052", "title": "ReUpload solo desde Expired/Rejected con nuevo checksum -> PendingReview"},
    {"code": "INV-UD7", "fr": "FR-052", "title": "Checksum obligatorio en upload"},
    {"code": "INV-AEP1", "fr": "FR-053", "title": "Política de enforcement exige profileId o roleId"},
    {"code": "INV-AEP2", "fr": "FR-053", "title": "Acción de enforcement = bloqueo/degradación (BlockUser/RestrictProfile/LogOnly)"},
    {"code": "INV-AEP3", "fr": "FR-053", "title": "Política de documentos críticos con periodo de gracia"},
    {"code": "INV-AEP4", "fr": "FR-053", "title": "Deactivate idempotente"},
    {"code": "INV-RP", "fr": "—", "title": "Entradas de enum inválidas producen 4xx limpio (Result Pattern)"},
    {"code": "INV-HTTP", "fr": "—", "title": "Códigos HTTP consistentes con el contrato OpenAPI para conflictos de estado"},
]


# --- Helpers ----------------------------------------------------------------

def _actor_headers(actor: str, disable_dev_auth: bool = False) -> dict:
    """Cabeceras DevAuth: identidad = X-User-Id, inquilino = EVOLITH (management-owner)."""
    if disable_dev_auth:
        return {"X-Disable-Dev-Auth": "true"}
    return {
        "X-User-Id": actor,
        "X-Tenant-Id": EVOLITH_TENANT_ID,
        "X-Is-Internal-Admin": "true",
    }


def _err_code(resp) -> str:
    """errorCode/brokenRule del ProblemDetails, o el title si no viaja."""
    d = resp.json or {}
    if isinstance(d, dict):
        return (d.get("errorCode") or d.get("brokenRule") or d.get("title") or "").strip()
    return ""


def _title(resp) -> str:
    d = resp.json or {}
    return d.get("title", "") if isinstance(d, dict) else ""


def _active_target_user(prov: Provisioner, rc, label: str):
    """Provisiona una cuenta Internal activa bajo EVOLITH y devuelve su id."""
    email = "%s@evolith.example" % rc.unique(label).lower()
    r = prov.create_user_account(EVOLITH_TENANT_ID, email)
    uid = (r.json or {}).get("userAccountId")
    if uid:
        prov.set_password(uid)
        prov.activate_account(uid)
    return uid


def _create_manual_workflow(http, rc, actor):
    """Crea un workflow de aprobación manual (RequiresApproval=true) bajo EVOLITH.

    El handler pasa requiredDocumentCount=1 cuando RequiresApproval=true, por lo que
    el workflow se crea sin documentos reales (checklist vacío) salvo que se añadan.
    """
    body = {
        "tenantId": EVOLITH_TENANT_ID,
        "systemSuiteId": DEMO_SYSTEM_ID,
        "code": rc.unique("RS_WF"),
        "name": "RoboSoft Workflow",
        "description": "Workflow de certificación RoboSoft",
        "targetUserCategory": "Internal",
        "requiresApproval": True,
    }
    return http.request("POST", "/api/v1/approval-workflows", body=body,
                        extra_headers=_actor_headers(actor))


def _create_request(http, rc, actor, workflow_id, target_id):
    body = {
        "workflowId": workflow_id,
        "targetUserId": target_id,
        "requestedSystemId": DEMO_SYSTEM_ID,
        "requestedRoleId": DEMO_ROLE_ID,
        "justification": "solicitud de certificación RoboSoft",
    }
    return http.request("POST", "/api/v1/approval-requests", body=body,
                        extra_headers=_actor_headers(actor))


def _create_document_type(http, rc, actor):
    body = {
        "tenantId": EVOLITH_TENANT_ID,
        "code": rc.unique("RS_DT"),
        "name": "Documento RoboSoft",
        "description": "Tipo de documento de certificación",
        "criticity": "High",
    }
    return http.request("POST", "/api/v1/document-types", body=body,
                        extra_headers=_actor_headers(actor))


def _upload_document(http, actor, user_id, doc_type_id,
                     checksum="sha256:robosoft", criticity="High",
                     issue="2026-01-01T00:00:00Z", expiration="2027-01-01T00:00:00Z"):
    body = {
        "userId": user_id,
        "documentTypeId": doc_type_id,
        "issueDate": issue,
        "expirationDate": expiration,
        "criticity": criticity,
        "fileStoragePath": "/robosoft/doc.pdf",
        "fileChecksum": checksum,
    }
    return http.request("POST", "/api/v1/user-documents", body=body,
                        extra_headers=_actor_headers(actor))


# --- Run --------------------------------------------------------------------

def run(rc):  # noqa: C901 - un contexto agrupa muchos invariantes emparentados
    chk = Checker(rc, NAME, INVARIANTS)
    http = rc.http
    prov = Provisioner(rc)
    actor = CREATOR_ACTOR

    # ======================================================================
    # ApprovalRequest — SOD, terminalidad, happy-path
    # ======================================================================

    # INV-AR4: happy-path Pending -> Approved (workflow sin documentos requeridos).
    tgt_happy = _active_target_user(prov, rc, "rs.ar.tgt")
    wf_happy = _create_manual_workflow(http, rc, actor)
    wf_happy_id = (wf_happy.json or {}).get("approvalWorkflowId")
    if tgt_happy and wf_happy_id:
        cr = _create_request(http, rc, actor, wf_happy_id, tgt_happy)
        rid = (cr.json or {}).get("approvalRequestId")
        if rid:
            # INV-AR4: happy-path con actor APROBADOR distinto del creador (SoD-compatible).
            ap = http.request("POST", "/api/v1/approval-requests/%s/approve" % rid,
                             body={"grantedRoleId": DEMO_ROLE_ID, "decisionReason": "ok"},
                             extra_headers=_actor_headers(APPROVER_ACTOR))
            g2 = http.request("GET", "/api/v1/approval-requests/%s" % rid,
                             extra_headers=_actor_headers(actor))
            final_status = (g2.json or {}).get("status")
            approved = ap.status == 204 and final_status == "Approved"
            chk.check("INV-AR4", approved,
                      "request Pending(id=%s) -> approve por APROBADOR distinto HTTP %d (title=%r), "
                      "status final=%s (G-117: tx vía ExecutionStrategy corrige el 400 previo)"
                      % (rid, ap.status, _title(ap), final_status))

            # INV-AR3 (G-119, SoD): el CREADOR no puede aprobar su propia request. Se crea una segunda
            # request con CREATOR_ACTOR y se intenta aprobar con CREATOR_ACTOR (mismo actor) → 400
            # self_approval_not_allowed; la request queda Pending.
            cr2 = _create_request(http, rc, CREATOR_ACTOR, wf_happy_id,
                                  _active_target_user(prov, rc, "rs.ar.tgt2"))
            rid2 = (cr2.json or {}).get("approvalRequestId")
            if rid2:
                self_ap = http.request("POST", "/api/v1/approval-requests/%s/approve" % rid2,
                                       body={"grantedRoleId": DEMO_ROLE_ID, "decisionReason": "self"},
                                       extra_headers=_actor_headers(CREATOR_ACTOR))
                g3 = http.request("GET", "/api/v1/approval-requests/%s" % rid2,
                                 extra_headers=_actor_headers(CREATOR_ACTOR))
                blocked = self_ap.status == 400 and (g3.json or {}).get("status") == "Pending" \
                    and "self_approval" in (self_ap.text or "").lower()
                chk.check("INV-AR3", blocked,
                          "self-approval (createdBy==approver) -> HTTP %d %s; status=%s (esperado 400 "
                          "self_approval_not_allowed + Pending; G-119 SoD enforcada)"
                          % (self_ap.status, _err_code(self_ap), (g3.json or {}).get("status")))
            else:
                chk.check("INV-AR3", False, "no se pudo crear la 2ª request para ejercer SoD")
        else:
            chk.check("INV-AR4", False,
                      "no se pudo crear la solicitud: POST /approval-requests HTTP %d %s"
                      % (cr.status, cr.snippet(120)))
            chk.block("INV-AR3", "sin solicitud para ejercer el approve (SOD inobservable)")
    else:
        chk.block("INV-AR4", "no se pudo provisionar target/workflow")
        chk.block("INV-AR3", "no se pudo provisionar target/workflow")

    # INV-AR1 / INV-AR2: terminalidad e irreversibilidad vía reject (approve roto por AR4).
    tgt_term = _active_target_user(prov, rc, "rs.ar.term")
    wf_term = _create_manual_workflow(http, rc, actor)
    wf_term_id = (wf_term.json or {}).get("approvalWorkflowId")
    if tgt_term and wf_term_id:
        cr = _create_request(http, rc, actor, wf_term_id, tgt_term)
        rid = (cr.json or {}).get("approvalRequestId")
        if rid:
            rej1 = http.request("POST", "/api/v1/approval-requests/%s/reject" % rid,
                               body={"decisionReason": "no"}, extra_headers=_actor_headers(actor))
            g = http.request("GET", "/api/v1/approval-requests/%s" % rid,
                            extra_headers=_actor_headers(actor))
            after = (g.json or {}).get("status")
            rej2 = http.request("POST", "/api/v1/approval-requests/%s/reject" % rid,
                               body={"decisionReason": "otra vez"}, extra_headers=_actor_headers(actor))
            # Aprobar con un actor DISTINTO del creador (G-119): así el guard de terminalidad
            # (request_not_pending) es el que responde, no el de SoD (self_approval).
            ap_term = http.request("POST", "/api/v1/approval-requests/%s/approve" % rid,
                                  body={"grantedRoleId": DEMO_ROLE_ID}, extra_headers=_actor_headers(APPROVER_ACTOR))
            # AR1: reject sólo desde Pending; el estado terminal es irreversible.
            chk.check("INV-AR1",
                      rej1.status == 204 and after in ("Rejected", "Denied")
                      and rej2.status in (400, 409),
                      "reject desde Pending -> HTTP %d (status=%s); reject de nuevo -> HTTP %d (%s)"
                      % (rej1.status, after, rej2.status, _err_code(rej2)))
            # AR2: no re-decidir una request terminal (ni approve ni reject).
            clean_guard = (
                ap_term.status in (400, 409) and "request_not_pending" in _err_code(ap_term).lower()
                and rej2.status in (400, 409) and "request_not_pending" in _err_code(rej2).lower()
            )
            chk.check("INV-AR2", clean_guard,
                      "sobre terminal: approve -> HTTP %d (%s); reject -> HTTP %d (%s) "
                      "(guard RequestNotPending, Result Pattern limpio antes de la tx)"
                      % (ap_term.status, _err_code(ap_term), rej2.status, _err_code(rej2)))
        else:
            chk.check("INV-AR1", False, "no se pudo crear la solicitud terminal: HTTP %d" % cr.status)
            chk.check("INV-AR2", False, "no se pudo crear la solicitud terminal: HTTP %d" % cr.status)
    else:
        chk.block("INV-AR1", "no se pudo provisionar target/workflow")
        chk.block("INV-AR2", "no se pudo provisionar target/workflow")

    # ======================================================================
    # ApprovalWorkflow — checklist de documentos requeridos
    # ======================================================================

    dt = _create_document_type(http, rc, actor)
    dt_id = (dt.json or {}).get("documentTypeId")
    wf_doc = _create_manual_workflow(http, rc, actor)
    wf_doc_id = (wf_doc.json or {}).get("approvalWorkflowId")

    if dt_id and wf_doc_id:
        add1 = http.request("POST", "/api/v1/approval-workflows/%s/required-documents" % wf_doc_id,
                           body={"documentTypeId": dt_id, "isMandatory": True},
                           extra_headers=_actor_headers(actor))
        # INV-WF1: el workflow declara el documento requerido.
        chk.check("INV-WF1", add1.status == 204,
                  "POST /approval-workflows/%s/required-documents -> HTTP %d" % (wf_doc_id, add1.status))

        # INV-WF4: alta duplicada limpia (Result Pattern) — F6 corregido (era 500).
        add2 = http.request("POST", "/api/v1/approval-workflows/%s/required-documents" % wf_doc_id,
                           body={"documentTypeId": dt_id, "isMandatory": True},
                           extra_headers=_actor_headers(actor))
        chk.check("INV-WF4", add2.status in (400, 409),
                  "alta duplicada del mismo documentTypeId -> HTTP %d (%s); esperado conflicto "
                  "limpio, no 500" % (add2.status, _err_code(add2)))

        # Segundo tipo de documento: el workflow requiere aprobación, así que el dominio (correctamente)
        # no permite quedar en 0 documentos requeridos. Con 2 documentos, remover uno deja >=1 y WF3 es
        # ejercitable sin violar RequiresDocumentsIfApprovalRequired.
        dt2 = _create_document_type(http, rc, actor)
        dt2_id = (dt2.json or {}).get("documentTypeId")
        if dt2_id:
            http.request("POST", "/api/v1/approval-workflows/%s/required-documents" % wf_doc_id,
                         body={"documentTypeId": dt2_id, "isMandatory": False},
                         extra_headers=_actor_headers(actor))

        # INV-WF2: el checklist debe ser legible por el cliente (G-118: RequiredDocuments proyectado).
        gw = http.request("GET", "/api/v1/approval-workflows/%s" % wf_doc_id,
                         extra_headers=_actor_headers(actor))
        body = gw.json if isinstance(gw.json, dict) else {}
        req_docs = body.get("requiredDocuments") or []
        chk.check("INV-WF2", len(req_docs) > 0 and all("requiredDocumentId" in d for d in req_docs),
                  "GET /approval-workflows/{id}.requiredDocuments=%d, cada uno con requiredDocumentId/"
                  "documentTypeId/isMandatory (G-118: checklist legible; antes write-only F4) sample=%s"
                  % (len(req_docs), req_docs[:1]))

        # INV-WF3: eliminar el documento requerido vía API con el id que ahora expone el read model.
        # G-118 expone requiredDocumentId; G-116 hace que FindRequiredDocument resuelva por Props.Id.
        rd_id = next((d.get("requiredDocumentId") for d in req_docs if d.get("documentTypeId") == dt_id),
                     req_docs[0].get("requiredDocumentId") if req_docs else None)
        if rd_id:
            dele = http.request("DELETE", "/api/v1/approval-workflows/%s/required-documents/%s"
                               % (wf_doc_id, rd_id), extra_headers=_actor_headers(actor))
            chk.check("INV-WF3", dele.status == 204,
                      "DELETE /required-documents/{requiredDocumentId=%s} -> HTTP %d (G-118 expone el id, "
                      "G-116 lo resuelve por Props.Id; antes 404 indeleteable F4)" % (rd_id, dele.status))
        else:
            chk.check("INV-WF3", False, "no se pudo obtener requiredDocumentId del read model (WF2)")
    else:
        for code in ("INV-WF1", "INV-WF2", "INV-WF3", "INV-WF4"):
            chk.block(code, "no se pudo provisionar document-type/workflow")

    # INV-WF5: los documentos requeridos se exigen como precondición de la aprobación (fail-closed).
    tgt_wf5 = _active_target_user(prov, rc, "rs.wf5.tgt")
    wf5 = _create_manual_workflow(http, rc, actor)
    wf5_id = (wf5.json or {}).get("approvalWorkflowId")
    if dt_id and wf5_id and tgt_wf5:
        http.request("POST", "/api/v1/approval-workflows/%s/required-documents" % wf5_id,
                    body={"documentTypeId": dt_id, "isMandatory": True},
                    extra_headers=_actor_headers(actor))
        cr = _create_request(http, rc, actor, wf5_id, tgt_wf5)
        rid = (cr.json or {}).get("approvalRequestId")
        if rid:
            # Aprobar con actor DISTINTO del creador (G-119): así responde el guard de documentos
            # requeridos (required_documents_incomplete), no el de SoD (self_approval).
            ap = http.request("POST", "/api/v1/approval-requests/%s/approve" % rid,
                             body={"grantedRoleId": DEMO_ROLE_ID}, extra_headers=_actor_headers(APPROVER_ACTOR))
            enforced = ap.status in (400, 409) and "required_documents" in _err_code(ap).lower()
            chk.check("INV-WF5", enforced,
                      "target sin documento obligatorio Valid -> approve HTTP %d (%s); la aprobación "
                      "se corta fail-closed antes de transicionar (G-051 F4)"
                      % (ap.status, _err_code(ap)))
        else:
            chk.check("INV-WF5", False, "no se pudo crear la solicitud: HTTP %d" % cr.status)
    else:
        chk.block("INV-WF5", "no se pudo provisionar document-type/workflow/target")

    # ======================================================================
    # UserDocument — máquina de estados (FSM)
    # ======================================================================

    ud_user = _active_target_user(prov, rc, "rs.ud.user")
    ud_dt = _create_document_type(http, rc, actor)
    ud_dt_id = (ud_dt.json or {}).get("documentTypeId")
    d3 = None  # doc Rejected reutilizado por INV-UD4

    if ud_user and ud_dt_id:
        # INV-UD1: upload -> PendingReview; expiration<=issue -> rechazo.
        up = _upload_document(http, actor, ud_user, ud_dt_id)
        doc_id = (up.json or {}).get("userDocumentId")
        g = http.request("GET", "/api/v1/user-documents/%s" % doc_id,
                        extra_headers=_actor_headers(actor)) if doc_id else None
        st0 = (g.json or {}).get("status") if g else None
        bad = _upload_document(http, actor, ud_user, ud_dt_id, expiration="2025-01-01T00:00:00Z")
        chk.check("INV-UD1",
                  up.status == 201 and st0 == "PENDING_REVIEW" and bad.status in (400, 422),
                  "upload -> HTTP %d (status=%s); expiration<=issue -> HTTP %d (%s)"
                  % (up.status, st0, bad.status, _err_code(bad)))

        # INV-UD7: checksum obligatorio.
        noc = _upload_document(http, actor, ud_user, ud_dt_id, checksum="")
        chk.check("INV-UD7", noc.status in (400, 422),
                  "upload con fileChecksum='' -> HTTP %d (%s)" % (noc.status, _title(noc)))

        if doc_id:
            # INV-UD2: PendingReview->Valid; Valid terminal para validate/reject.
            v1 = http.request("POST", "/api/v1/user-documents/%s/validate" % doc_id,
                             extra_headers=_actor_headers(actor))
            gv = http.request("GET", "/api/v1/user-documents/%s" % doc_id,
                             extra_headers=_actor_headers(actor))
            st_valid = (gv.json or {}).get("status")
            v2 = http.request("POST", "/api/v1/user-documents/%s/validate" % doc_id,
                             extra_headers=_actor_headers(actor))
            rj = http.request("POST", "/api/v1/user-documents/%s/reject" % doc_id,
                             body={"rejectionReason": "x"}, extra_headers=_actor_headers(actor))
            chk.check("INV-UD2",
                      v1.status == 204 and st_valid == "Valid"
                      and v2.status in (400, 409) and rj.status in (400, 409),
                      "validate -> HTTP %d (status=%s); validate de nuevo -> HTTP %d; reject sobre "
                      "Valid -> HTTP %d" % (v1.status, st_valid, v2.status, rj.status))

            # INV-UD5: expire desde Valid; expire idempotente/terminal.
            e1 = http.request("POST", "/api/v1/user-documents/%s/expire" % doc_id,
                             extra_headers=_actor_headers(actor))
            ge = http.request("GET", "/api/v1/user-documents/%s" % doc_id,
                             extra_headers=_actor_headers(actor))
            st_exp = (ge.json or {}).get("status")
            e2 = http.request("POST", "/api/v1/user-documents/%s/expire" % doc_id,
                             extra_headers=_actor_headers(actor))
            chk.check("INV-UD5",
                      e1.status == 204 and st_exp == "Expired" and e2.status in (400, 409),
                      "expire(Valid) -> HTTP %d (status=%s); expire de nuevo -> HTTP %d (%s)"
                      % (e1.status, st_exp, e2.status, _err_code(e2)))

            # INV-UD6: reupload desde Expired con nuevo checksum -> PendingReview.
            ru = http.request("POST", "/api/v1/user-documents/%s/re-upload" % doc_id,
                             body={"newIssueDate": "2026-02-01T00:00:00Z",
                                   "newExpirationDate": "2027-02-01T00:00:00Z",
                                   "newFileStoragePath": "/robosoft/re.pdf",
                                   "newFileChecksum": "sha256:reupload"},
                             extra_headers=_actor_headers(actor))
            gr = http.request("GET", "/api/v1/user-documents/%s" % doc_id,
                             extra_headers=_actor_headers(actor))
            st_re = (gr.json or {}).get("status")
            chk.check("INV-UD6", ru.status == 204 and st_re == "PENDING_REVIEW",
                      "re-upload(Expired) -> HTTP %d (status=%s)" % (ru.status, st_re))
        else:
            for code in ("INV-UD2", "INV-UD5", "INV-UD6"):
                chk.block(code, "sin documento base")

        # INV-UD3: PendingReview->Rejected.
        up3 = _upload_document(http, actor, ud_user, ud_dt_id)
        d3 = (up3.json or {}).get("userDocumentId")
        rj3 = http.request("POST", "/api/v1/user-documents/%s/reject" % d3,
                          body={"rejectionReason": "no cumple"},
                          extra_headers=_actor_headers(actor)) if d3 else None
        g3 = http.request("GET", "/api/v1/user-documents/%s" % d3,
                         extra_headers=_actor_headers(actor)) if d3 else None
        st3 = (g3.json or {}).get("status") if g3 else None
        chk.check("INV-UD3", bool(rj3) and rj3.status == 204 and st3 == "Rejected",
                  "upload fresco -> reject -> HTTP %s (status=%s)"
                  % (rj3.status if rj3 else "n/a", st3))

        # INV-UD4: Expired sólo alcanzable desde Valid (F3 corregido).
        up4 = _upload_document(http, actor, ud_user, ud_dt_id)
        d4 = (up4.json or {}).get("userDocumentId")
        ep_pending = http.request("POST", "/api/v1/user-documents/%s/expire" % d4,
                                 extra_headers=_actor_headers(actor)) if d4 else None
        ep_rejected = http.request("POST", "/api/v1/user-documents/%s/expire" % d3,
                                  extra_headers=_actor_headers(actor)) if d3 else None
        ud4_ok = (
            bool(ep_pending) and ep_pending.status in (400, 409)
            and bool(ep_rejected) and ep_rejected.status in (400, 409)
        )
        chk.check("INV-UD4", ud4_ok,
                  "expire desde PendingReview -> HTTP %s (%s); expire desde Rejected -> HTTP %s (%s); "
                  "ambos rechazan el salto de FSM"
                  % (ep_pending.status if ep_pending else "n/a",
                     _err_code(ep_pending) if ep_pending else "",
                     ep_rejected.status if ep_rejected else "n/a",
                     _err_code(ep_rejected) if ep_rejected else ""))
    else:
        for code in ("INV-UD1", "INV-UD2", "INV-UD3", "INV-UD4", "INV-UD5", "INV-UD6", "INV-UD7"):
            chk.block(code, "no se pudo provisionar usuario/document-type")

    # ======================================================================
    # AccessEnforcementPolicy
    # ======================================================================

    # INV-AEP1: exige profileId o roleId.
    neither = http.request("POST", "/api/v1/access-enforcement-policies",
                          body={"tenantId": EVOLITH_TENANT_ID, "profileId": None, "roleId": None,
                                "enforcementAction": "BlockUser"}, extra_headers=_actor_headers(actor))
    chk.check("INV-AEP1", neither.status in (400, 422),
              "policy sin profileId ni roleId -> HTTP %d (%s)" % (neither.status, _title(neither)))

    # INV-AEP2: acciones de bloqueo/degradación (BlockUser/RestrictProfile/LogOnly).
    actions_status = {}
    policy_for_deactivate = None
    for act in ("BlockUser", "RestrictProfile", "LogOnly"):
        r = http.request("POST", "/api/v1/access-enforcement-policies",
                        body={"tenantId": EVOLITH_TENANT_ID, "roleId": DEMO_ROLE_ID,
                              "enforcementAction": act}, extra_headers=_actor_headers(actor))
        actions_status[act] = r.status
        if act == "RestrictProfile":
            policy_for_deactivate = (r.json or {}).get("accessEnforcementPolicyId")
    chk.check("INV-AEP2", all(s == 201 for s in actions_status.values()),
              "creación por acción: %s (todas deben aceptarse)" % actions_status)

    # INV-AEP3 (G-120): la política de enforcement de documentos críticos soporta periodo de gracia.
    # Se crea una política con gracePeriodDays=7 y se verifica que el DTO lo expone y round-trips.
    grace_create = http.request("POST", "/api/v1/access-enforcement-policies",
                                body={"tenantId": EVOLITH_TENANT_ID, "profileId": None,
                                      "roleId": DEMO_ROLE_ID, "enforcementAction": "RestrictProfile",
                                      "gracePeriodDays": 7}, extra_headers=_actor_headers(actor))
    grace_id = (grace_create.json or {}).get("accessEnforcementPolicyId")
    grace_dto = {}
    if grace_id:
        gg = http.request("GET", "/api/v1/access-enforcement-policies/%s" % grace_id,
                         extra_headers=_actor_headers(actor))
        grace_dto = gg.json if isinstance(gg.json, dict) else {}
    chk.check("INV-AEP3",
              grace_create.status in (200, 201) and grace_dto.get("gracePeriodDays") == 7,
              "crear política con gracePeriodDays=7 -> HTTP %d; DTO.gracePeriodDays=%s (G-120: dimensión "
              "temporal presente y round-trips; 0=enforcement inmediato)"
              % (grace_create.status, grace_dto.get("gracePeriodDays")))

    # INV-AEP4: deactivate idempotente/terminal.
    if policy_for_deactivate:
        d1 = http.request("POST", "/api/v1/access-enforcement-policies/%s/deactivate" % policy_for_deactivate,
                         extra_headers=_actor_headers(actor))
        d2 = http.request("POST", "/api/v1/access-enforcement-policies/%s/deactivate" % policy_for_deactivate,
                         extra_headers=_actor_headers(actor))
        chk.check("INV-AEP4", d1.status == 204 and d2.status in (204, 400, 409),
                  "deactivate -> HTTP %d; deactivate de nuevo -> HTTP %d (%s) — sin doble efecto"
                  % (d1.status, d2.status, _err_code(d2)))
    else:
        chk.block("INV-AEP4", "no se creó una política para desactivar")

    # ======================================================================
    # Result Pattern y contrato HTTP
    # ======================================================================

    # INV-RP: enums inválidos -> 4xx limpio (G-045 corrigió upload/policy/document-type).
    rp = {}
    if ud_user and ud_dt_id:
        rp["upload"] = _upload_document(http, actor, ud_user, ud_dt_id, criticity="Bogus").status
    rp["policy"] = http.request("POST", "/api/v1/access-enforcement-policies",
                               body={"tenantId": EVOLITH_TENANT_ID, "roleId": DEMO_ROLE_ID,
                                     "enforcementAction": "Foo"},
                               extra_headers=_actor_headers(actor)).status
    rp["document-type"] = http.request("POST", "/api/v1/document-types",
                                      body={"tenantId": EVOLITH_TENANT_ID, "code": rc.unique("RS_RP"),
                                            "name": "X", "description": "d", "criticity": "Foo"},
                                      extra_headers=_actor_headers(actor)).status
    rp_ok = bool(rp) and all(s in (400, 422) for s in rp.values())
    chk.check("INV-RP", rp_ok,
              "enum inválido por endpoint: %s (todos deben ser 400/422, no 500)" % rp)

    # INV-HTTP (G-121): consistencia de códigos para conflictos de TRANSICIÓN de estado. Los endpoints
    # anuncian ProducesProblem(409); tras G-121 los conflictos de ciclo de vida (already_expired,
    # not_pending_review, cannot_transition, request_not_pending) resuelven todos 409.
    ht_user = _active_target_user(prov, rc, "rs.http.user")
    ht_codes = {}
    if ud_dt_id and ht_user:
        u = _upload_document(http, actor, ht_user, ud_dt_id)
        hid = (u.json or {}).get("userDocumentId")
        if hid:
            # already-expired -> observado 409
            http.request("POST", "/api/v1/user-documents/%s/validate" % hid, extra_headers=_actor_headers(actor))
            http.request("POST", "/api/v1/user-documents/%s/expire" % hid, extra_headers=_actor_headers(actor))
            ht_codes["already_expired"] = http.request(
                "POST", "/api/v1/user-documents/%s/expire" % hid, extra_headers=_actor_headers(actor)).status
        u2 = _upload_document(http, actor, ht_user, ud_dt_id)
        hid2 = (u2.json or {}).get("userDocumentId")
        if hid2:
            # not_pending_review (validate un doc ya Valid) -> observado 400
            http.request("POST", "/api/v1/user-documents/%s/validate" % hid2, extra_headers=_actor_headers(actor))
            ht_codes["not_pending_review"] = http.request(
                "POST", "/api/v1/user-documents/%s/validate" % hid2, extra_headers=_actor_headers(actor)).status
    # Consistente sólo si TODOS los conflictos de transición de estado usan el mismo código (409).
    distinct = set(ht_codes.values())
    consistent = bool(ht_codes) and len(distinct) <= 1 and distinct.issubset({409})
    chk.check("INV-HTTP", consistent,
              "conflictos de transición de estado -> %s (G-121: alineados a 409, coherente con "
              "ProducesProblem(409); antes mezclaban 400/409)" % ht_codes)

    return chk.results
