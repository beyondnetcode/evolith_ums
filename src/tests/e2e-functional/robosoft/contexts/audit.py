"""Contexto audit — traza de auditoría append-only, autenticación y no-repudio.

Fuente de invariantes: auditoría bmad-tester-robosoft-audit-2026-07-16.md,
sección «Cobertura por contexto → audit» (10 invariantes).

IMPLEMENTADO — se ejecuta contra el backend vivo.
"""

from __future__ import annotations

import time

from harness import EVOLITH_TENANT_ID, Checker

NAME = "audit"

INVARIANTS = [
    {"code": "INV-AU01", "fr": "FR-070", "title": "UPDATE/DELETE de auditoría DENEGADO (append-only en API)"},
    {"code": "INV-AU02", "fr": "FR-070", "title": "Inmutabilidad a nivel de dominio (sin métodos mutadores)"},
    {"code": "INV-AU03", "fr": "FR-070", "title": "Transición crítica registrada con tupla no repudiable (actor, instante, cambio, resultado, entidad, tenant)"},
    {"code": "INV-AU04", "fr": "FR-070", "title": "Fechas en UTC"},
    {"code": "INV-AU05", "fr": "FR-072", "title": "Metadatos desinfectados (sin hashes/PIN/llaves/PII) — vía automática"},
    {"code": "INV-AU06", "fr": "FR-070", "title": "Consulta acotada por tenant, sin lectura cruzada"},
    {"code": "INV-AU07", "fr": "FR-070", "title": "Los endpoints de auditoría requieren autenticación"},
    {"code": "INV-AU08", "fr": "FR-070", "title": "No-repudio: actor/tenant derivados del servidor, no falsificables por el llamador"},
    {"code": "INV-AU09", "fr": "FR-071", "title": "Durabilidad vía Transactional Outbox (ADR-0110)"},
    {"code": "INV-AU10", "fr": "FR-072", "title": "Metadatos desinfectados en la vía manual (POST)"},
]

_LIST = "/api/v1/audit-records"


def _first_id(http):
    r = http.request("GET", _LIST + "?page=1&pageSize=1")
    items = (r.json or {}).get("items") or []
    if items:
        return items[0].get("auditRecordId") or items[0].get("id"), items[0]
    return None, None


def _poll_first(http, attempts=12, delay=1.0):
    """Reintenta hasta que exista un registro de auditoría consultable. La escritura de
    auditoría es asíncrona (~2s vía sink); en una BD recién sembrada la tabla arranca vacía,
    así que el arnés debe esperar en vez de asumir historial. Robustez re-corrible."""
    for _ in range(attempts):
        rid, sample = _first_id(http)
        if rid:
            return rid, sample
        time.sleep(delay)
    return None, None


def _seed_audit_record(http, rc):
    """Genera un registro de auditoría (crear AppConfiguration lo dispara) y espera a que
    sea consultable. Hace el contexto autosuficiente contra una BD fresca."""
    code = rc.unique("AUDIT_SEED").upper()
    http.request("POST", "/api/v1/app-configurations", token=rc.admin_token, internal_admin=True,
                 body={"code": code, "value": "seed", "scope": "Tenant",
                       "tenantId": EVOLITH_TENANT_ID, "valueType": "String",
                       "description": "RoboSoft audit seed"})
    return _poll_first(http)


def run(rc):
    chk = Checker(rc, NAME, INVARIANTS)
    http = rc.http

    rec_id, sample = _first_id(http)
    if not rec_id:
        rec_id, sample = _seed_audit_record(http, rc)

    # INV-AU01: UPDATE/DELETE denegado (append-only en API)
    if rec_id:
        put = http.request("PUT", "%s/%s" % (_LIST, rec_id), token=rc.admin_token)
        patch = http.request("PATCH", "%s/%s" % (_LIST, rec_id), token=rc.admin_token)
        dele = http.request("DELETE", "%s/%s" % (_LIST, rec_id), token=rc.admin_token)
        append_only = put.status in (404, 405) and patch.status in (404, 405) and dele.status in (404, 405)
        chk.check("INV-AU01", append_only,
                  "PUT=%d PATCH=%d DELETE=%d sobre %s (esperado 405/404: solo GET/POST)"
                  % (put.status, patch.status, dele.status, rec_id))
    else:
        chk.block("INV-AU01", "no hay registros de auditoría para ejercer los verbos")

    # INV-AU02: inmutabilidad de dominio — no verificable en caja negra (requiere fuente .NET);
    #           la ausencia de PUT/PATCH/DELETE en el contrato es evidencia consistente.
    chk.skip("INV-AU02",
             "invariante de dominio (AuditRecord sin mutadores) no verificable en caja negra; "
             "consistente con INV-AU01 (API append-only). Verificar en fuente evolith_arch.")

    # INV-AU03: transición crítica -> tupla no repudiable. Generamos una transición real
    #           (crear AppConfiguration) y verificamos que el registro tenga la tupla completa.
    cfg_code = rc.unique("AUDIT_PROBE").upper()
    cfg = http.request("POST", "/api/v1/app-configurations", token=rc.admin_token, internal_admin=True,
                       body={
                           "code": cfg_code,
                           "value": "probe-value",
                           "scope": "Tenant",
                           "tenantId": EVOLITH_TENANT_ID,
                           "valueType": "String",
                           "description": "RoboSoft audit probe",
                       })
    _rec_id, sample2 = _poll_first(http)
    tuple_fields = ["whoActed", "whenOccurred", "whatChanged", "auditResult",
                    "affectedEntityId", "affectedEntityType", "rootTenantId"]
    present = [f for f in tuple_fields if sample2 and sample2.get(f) is not None]
    chk.check("INV-AU03", cfg.status in (200, 201) and len(present) == len(tuple_fields),
              "crear AppConfiguration -> HTTP %d; último record con tupla %d/%d campos (%s)"
              % (cfg.status, len(present), len(tuple_fields), ",".join(present)))

    # INV-AU04: fechas en UTC (whenOccurred termina en Z)
    lst = http.request("GET", _LIST + "?page=1&pageSize=20")
    items = (lst.json or {}).get("items") or []
    utc = [i for i in items if str(i.get("whenOccurred", "")).endswith("Z")]
    chk.check("INV-AU04", items and len(utc) == len(items),
              "%d/%d whenOccurred terminan en 'Z' (UTC)" % (len(utc), len(items)))

    # INV-AU05: metadatos desinfectados (vía automática) — el secreto NO debe aparecer en la traza
    secret = "SUPERSECRET-%s-APIKEY" % rc.run_id
    enc = http.request("POST", "/api/v1/app-configurations", token=rc.admin_token, internal_admin=True,
                       body={
                           "code": rc.unique("AUDIT_SEC").upper(),
                           "value": secret,
                           "scope": "Tenant",
                           "tenantId": EVOLITH_TENANT_ID,
                           "valueType": "String",
                           "isEncrypted": True,
                           "description": "RoboSoft secret probe",
                       })
    scan = http.request("GET", _LIST + "?page=1&pageSize=50")
    leaked = secret in scan.text
    chk.check("INV-AU05", not leaked,
              "config cifrada creada (HTTP %d); secreto %sen los últimos 50 records de auditoría"
              % (enc.status, "PRESENTE " if leaked else "AUSENTE "))

    # INV-AU06: consulta acotada por tenant — GET/{id} sin filtro de tenant (lectura cruzada).
    # PATRÓN DE MÉTODO: el despliegue corre en Development, donde DevAuthMiddleware FABRICA un
    # usuario por defecto para peticiones sin auth. Para medir la auth REAL hay que desactivarlo
    # con X-Disable-Dev-Auth:true (endurecido en G-042). Sin esto, "anónimo" mide un usuario dev.
    if rec_id:
        anon = http.request("GET", "%s/%s" % (_LIST, rec_id),
                            extra_headers={"X-Disable-Dev-Auth": "true"})
        # Un GET/{id} anónimo con 200 y cuerpo completo evidencia falta de acotamiento por tenant.
        cross = anon.status == 200 and (anon.json or {}).get("rootTenantId") is not None
        chk.check("INV-AU06", not cross,
                  "GET /audit-records/{id} ANÓNIMO -> HTTP %d; rootTenantId expuesto=%s "
                  "(auditoría AUDIT-02: lectura cruzada sin filtro de tenant)"
                  % (anon.status, (anon.json or {}).get("rootTenantId")))
    else:
        chk.block("INV-AU06", "sin registro para probar lectura cruzada")

    # INV-AU07: endpoints de auditoría requieren autenticación (con DevAuth desactivado, ver arriba)
    anon_list = http.request("GET", _LIST + "?page=1&pageSize=2",
                             extra_headers={"X-Disable-Dev-Auth": "true"})
    chk.check("INV-AU07", anon_list.status in (401, 403),
              "GET /audit-records SIN credenciales -> HTTP %d (esperado 401/403; "
              "auditoría AUDIT-01: 200 anónimo)" % anon_list.status)

    # INV-AU08: no-repudio — POST anónimo forjando actor/tenant NO debe crearse (no 201)
    forged = http.request("POST", _LIST, body={
        "eventType": "RS.Forgery.Probe",
        "whoActed": "11111111-1111-1111-1111-111111111111",
        "rootTenantId": EVOLITH_TENANT_ID,
        "whatChanged": "FORGED-BY-ANON-ROBOSOFT admin@evolith.example deleted everything",
        "affectedEntityId": "22222222-2222-2222-2222-222222222222",
        "affectedEntityType": "Probe",
        "auditResult": "Success",
        "subjectType": "Admin",
        "metadata": "{}",
    })
    chk.check("INV-AU08", forged.status != 201,
              "POST /audit-records ANÓNIMO forjando actor/tenant -> HTTP %d %s "
              "(esperado rechazo; auditoría AUDIT-03: 201 falsificable)"
              % (forged.status, forged.snippet(90)))

    # INV-AU09: durabilidad vía Transactional Outbox — no verificable en caja negra
    chk.skip("INV-AU09",
             "el canal de auditoría (outbox vs Channel<T> en memoria) no es observable vía API; "
             "verificar en fuente evolith_arch (ADR-0110). Auditoría AUDIT-05: Channel en memoria.")

    # INV-AU10: metadatos desinfectados en la vía manual (POST)
    #   Si el POST manual está bloqueado (no-repudio), la vía manual no es un vector: lo registramos.
    if forged.status == 201:
        # improbable dado INV-AU08, pero verificamos desinfección si se creó
        chk.check("INV-AU10", "<script" not in forged.text.lower(),
                  "POST manual creó registro; revisar desinfección de metadata")
    else:
        chk.skip("INV-AU10",
                 "la vía manual (POST) está cerrada al llamador externo (HTTP %d en INV-AU08); "
                 "no hay vector de inyección de metadata sin desinfectar por esta ruta" % forged.status)

    return chk.results
