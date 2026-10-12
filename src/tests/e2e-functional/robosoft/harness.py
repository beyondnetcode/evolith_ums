"""Núcleo del arnés RoboSoft — framework compartido de certificación E2E funcional de UMS.

Sin dependencias externas: solo biblioteca estándar de Python 3 (>=3.8).
Este módulo aporta el cliente HTTP, el modelo de invariantes/resultados, el
provisionamiento idempotente y el reporter. Cada contexto (contexts/*.py)
importa desde aquí y expone `NAME`, `INVARIANTS` y `run(rc)`.

Regla de oro (SD-05): la evidencia precede a la afirmación. Todo resultado
carga el código HTTP y el fragmento de cuerpo que lo justifica.
"""

from __future__ import annotations

import json
import random
import string
import urllib.error
import urllib.request
from dataclasses import dataclass, field
from typing import Any, Optional

# --- Estados de invariante -------------------------------------------------

PASS = "PASS"      # el invariante se cumple contra el binario vivo
FAIL = "FAIL"      # el invariante NO se cumple -> candidato a gap
SKIP = "SKIP"      # no ejercitable en caja negra (p.ej. requiere fuente .NET)
BLOCK = "BLOCK"    # bloqueado por una precondición no satisfecha en runtime
PENDING = "PENDING"  # no hay datos/semilla para ejercerlo en vivo

# Identificadores estables de tenants sembrados (semilla de dev de UMS).
EVOLITH_TENANT_ID = "5f4e3d2c-1b0a-9f8e-7d6c-543210987654"
EVOLITH_CODE = "EVOLITH"
COMEX_CODE = "COMEX_ANDINA"
COMEX_USER = "usuario.impo@comexandina.com.pe"
AGRONORTE_CODE = "AGRONORTE"
AGRONORTE_USER = "usuario.expo@agronorte.com.pe"
SEED_PASSWORD = "Evolith.Dev.2026"
ADMIN_USER = "admin@evolith.example"


# --- HTTP client -----------------------------------------------------------

@dataclass
class Response:
    """Respuesta HTTP normalizada (nunca lanza por 4xx/5xx)."""

    status: int
    headers: dict
    text: str
    cookie: Optional[str] = None

    @property
    def json(self) -> Optional[Any]:
        if not self.text:
            return None
        try:
            return json.loads(self.text)
        except (ValueError, TypeError):
            return None

    def snippet(self, limit: int = 240) -> str:
        """Fragmento de cuerpo para la evidencia."""
        body = self.text.replace("\n", " ").strip()
        return body[:limit]


class Http:
    """Cliente HTTP mínimo sobre urllib. Normaliza la cookie de sesión."""

    def __init__(self, base_url: str, timeout: float = 30.0):
        self.base_url = base_url.rstrip("/")
        self.timeout = timeout

    def request(
        self,
        method: str,
        path: str,
        body: Any = None,
        token: Optional[str] = None,
        cookie: Optional[str] = None,
        internal_admin: bool = False,
        extra_headers: Optional[dict] = None,
    ) -> Response:
        url = self.base_url + path
        data = None
        headers = {"Accept": "application/json"}
        if body is not None:
            data = json.dumps(body).encode("utf-8")
            headers["Content-Type"] = "application/json"
        if token:
            headers["Authorization"] = "Bearer " + token
        if cookie:
            headers["Cookie"] = cookie
        if internal_admin:
            headers["X-Is-Internal-Admin"] = "true"
        if extra_headers:
            headers.update(extra_headers)

        req = urllib.request.Request(url, data=data, headers=headers, method=method)
        try:
            with urllib.request.urlopen(req, timeout=self.timeout) as resp:
                return self._to_response(resp.status, resp.headers, resp.read())
        except urllib.error.HTTPError as err:
            return self._to_response(err.code, err.headers, err.read())
        except urllib.error.URLError as err:
            return Response(status=0, headers={}, text="URLError: %s" % err.reason)

    @staticmethod
    def _to_response(status, headers, raw) -> Response:
        text = raw.decode("utf-8", errors="replace") if raw else ""
        set_cookie = None
        try:
            raw_cookie = headers.get("Set-Cookie")
            if raw_cookie:
                set_cookie = raw_cookie.split(";", 1)[0]
        except Exception:  # noqa: BLE001 - defensivo
            set_cookie = None
        return Response(
            status=status,
            headers={k.lower(): v for k, v in dict(headers).items()},
            text=text,
            cookie=set_cookie,
        )


# --- Resultado por invariante ---------------------------------------------

@dataclass
class Result:
    context: str
    code: str          # INV-*
    status: str        # PASS/FAIL/SKIP/BLOCK/PENDING
    title: str
    fr: str = ""
    evidence: str = ""


@dataclass
class RunContext:
    """Contexto de ejecución compartido entre contextos e invariantes."""

    http: Http
    admin_token: str
    admin_cookie: Optional[str]
    run_id: str
    seed: int
    verbose: bool = False
    _counter: dict = field(default_factory=dict)

    def unique(self, prefix: str) -> str:
        """Genera un código único y re-corrible: PREFIX_<runid><n>.

        El sufijo es único por corrida (idempotencia/re-ejecución sin 409)
        y determinista dentro de la corrida (secuencial por prefijo).
        """
        n = self._counter.get(prefix, 0) + 1
        self._counter[prefix] = n
        return "%s_%s%02d" % (prefix, self.run_id, n)

    def log(self, message: str) -> None:
        if self.verbose:
            print("    · %s" % message)


# --- Checker: acumula resultados de un contexto ---------------------------

class Checker:
    """Acumulador de resultados atado a la metadata de invariantes del contexto."""

    def __init__(self, rc: RunContext, context_name: str, invariants_meta: list):
        self.rc = rc
        self.context = context_name
        self._meta = {inv["code"]: inv for inv in invariants_meta}
        self.results: list = []

    def _push(self, code: str, status: str, evidence: str) -> None:
        meta = self._meta.get(code, {})
        self.results.append(
            Result(
                context=self.context,
                code=code,
                status=status,
                title=meta.get("title", code),
                fr=meta.get("fr", ""),
                evidence=evidence,
            )
        )

    def check(self, code: str, passed: bool, evidence: str) -> bool:
        self._push(code, PASS if passed else FAIL, evidence)
        return passed

    def skip(self, code: str, evidence: str) -> None:
        self._push(code, SKIP, evidence)

    def block(self, code: str, evidence: str) -> None:
        self._push(code, BLOCK, evidence)

    def pending(self, code: str, evidence: str) -> None:
        self._push(code, PENDING, evidence)


# --- Provisionamiento idempotente -----------------------------------------

class Provisioner:
    """Helpers on-behalf bajo el management-owner EVOLITH (X-Is-Internal-Admin).

    Todos los creates usan códigos con sufijo único por corrida para no colisionar
    (409) al re-ejecutar. No destruye la semilla.
    """

    def __init__(self, rc: RunContext):
        self.rc = rc
        self.http = rc.http
        self.token = rc.admin_token

    def _post(self, path, body, **kw):
        return self.http.request("POST", path, body=body, token=self.token,
                                 internal_admin=True, **kw)

    def create_tenant(self, code: str, ttype: str = "CLIENT",
                      management_owner: bool = False) -> Response:
        body = {
            "code": code,
            "name": "RoboSoft %s" % code,
            "type": ttype,
            "ruc": "20" + "".join(random.choice(string.digits) for _ in range(9)),
        }
        if management_owner:
            body["isManagementOwner"] = True
        return self._post("/api/v1/tenants", body)

    def create_branch(self, tenant_id: str, code: str) -> Response:
        body = {
            "code": code,
            "name": "Sucursal %s" % code,
            "geofencingMetadata": '{"lat":-12.05,"lng":-77.04,"radiusMeters":150}',
        }
        return self._post("/api/v1/tenants/%s/branches" % tenant_id, body)

    def create_user_account(self, tenant_id: str, email: str,
                           category: str = "Internal", display_name: str = "RoboSoft User") -> Response:
        body = {
            "email": email,
            "tenantId": tenant_id,
            "category": category,
            "displayName": display_name,
        }
        return self._post("/api/v1/user-accounts", body)

    def set_password(self, account_id: str, password: str = "Evolith.Dev.2026!") -> Response:
        return self._post("/api/v1/user-accounts/%s/passwords" % account_id,
                         {"password": password})

    def activate_account(self, account_id: str) -> Response:
        return self._post("/api/v1/user-accounts/%s/activate" % account_id, None)

    def block_account(self, account_id: str, reason: str = "robosoft-probe") -> Response:
        return self._post("/api/v1/user-accounts/%s/block?reason=%s" % (account_id, reason), None)


# --- Login helpers ---------------------------------------------------------

def login(http: Http, tenant_code: str, username: str, password: str,
          client_endpoint: bool = False) -> Response:
    path = "/api/v1/client/authenticate" if client_endpoint else "/api/v1/auth/login"
    return http.request("POST", path, body={
        "tenantCode": tenant_code,
        "username": username,
        "password": password,
    })


def admin_login(http: Http) -> Response:
    return login(http, EVOLITH_CODE, ADMIN_USER, SEED_PASSWORD)


# --- Utilidades ------------------------------------------------------------

def parse_iso(value: str):
    """Parsea un timestamp ISO-8601 tolerando fracción de segundo de 7 dígitos.

    .NET serializa hasta 7 dígitos de fracción (ticks); datetime.fromisoformat
    en Python <3.11 solo acepta 3 o 6. Truncamos a 6 y normalizamos la 'Z'.
    """
    import datetime as _dt
    import re as _re

    s = (value or "").replace("Z", "+00:00")
    # Recorta la fracción de segundo a 6 dígitos.
    s = _re.sub(r"(\.\d{6})\d+", r"\1", s)
    return _dt.datetime.fromisoformat(s)


# --- Esqueleto de contexto (ola 2) ----------------------------------------

def skeleton_run(rc: RunContext, name: str, invariants_meta: list, note: str) -> list:
    """Devuelve todos los invariantes del contexto como PENDING (módulo esqueleto).

    Los contextos de la siguiente ola declaran aquí su catálogo de invariantes
    (fuente de verdad de la matriz) sin ejecutarlos todavía.
    """
    chk = Checker(rc, name, invariants_meta)
    for inv in invariants_meta:
        chk.pending(inv["code"], note)
    return chk.results


# --- Reporter --------------------------------------------------------------

_ICON = {PASS: "PASS", FAIL: "FAIL", SKIP: "SKIP", BLOCK: "BLOCK", PENDING: "PEND"}


def summarize(all_results: list) -> dict:
    tally = {PASS: 0, FAIL: 0, SKIP: 0, BLOCK: 0, PENDING: 0}
    for r in all_results:
        tally[r.status] = tally.get(r.status, 0) + 1
    return tally


def print_report(all_results: list, run_id: str, base_url: str) -> None:
    print("")
    print("=" * 78)
    print("ARNÉS RoboSoft — certificación E2E funcional de UMS")
    print("run_id=%s · backend=%s" % (run_id, base_url))
    print("=" * 78)

    by_context: dict = {}
    order: list = []
    for r in all_results:
        if r.context not in by_context:
            by_context[r.context] = []
            order.append(r.context)
        by_context[r.context].append(r)

    for ctx_name in order:
        results = by_context[ctx_name]
        t = summarize(results)
        print("")
        print("### %s — PASS %d · FAIL %d · SKIP %d · BLOCK %d · PEND %d" % (
            ctx_name, t[PASS], t[FAIL], t[SKIP], t[BLOCK], t[PENDING]))
        for r in results:
            print("  [%-4s] %-12s %s" % (_ICON.get(r.status, r.status), r.code, r.title))
            if r.evidence:
                print("         -> %s" % r.evidence)

    total = summarize(all_results)
    print("")
    print("-" * 78)
    print("TOTAL: %d invariantes · PASS %d · FAIL %d · SKIP %d · BLOCK %d · PEND %d" % (
        len(all_results), total[PASS], total[FAIL], total[SKIP], total[BLOCK], total[PENDING]))
    if total[FAIL]:
        print("")
        print("HALLAZGOS (FAIL) — candidatos a gap (el orquestador los registra en GAPS.md):")
        for r in all_results:
            if r.status == FAIL:
                print("  · [%s] %s %s" % (r.context, r.code, r.title))
    print("-" * 78)
