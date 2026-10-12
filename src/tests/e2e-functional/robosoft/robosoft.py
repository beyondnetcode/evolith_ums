#!/usr/bin/env python3
"""Runner del arnés RoboSoft — certificación E2E funcional de UMS (G-112).

Fundación durable versionada. Sin dependencias externas: solo la biblioteca
estándar de Python 3 (>=3.8). Corre invariantes por contexto contra el backend
UMS vivo y reporta PASS/FAIL/SKIP/BLOCK/PENDING por invariante con evidencia
(código HTTP + cuerpo relevante). Los FAIL son candidatos a gap (SD-05/SD-07).

Uso:
    # 1) Port-forward al backend en kind (en otra terminal o en background):
    kubectl --context kind-evolith-cluster-ums -n ums \\
        port-forward svc/ums-backend 18080:80 &

    # 2) Correr los 3 contextos implementados:
    python3 robosoft.py --base http://localhost:18080

    # Correr un contexto concreto:
    python3 robosoft.py --context audit

    # Correr TODOS (implementados + esqueletos pendientes):
    python3 robosoft.py --all

    # Listar contextos y su estado:
    python3 robosoft.py --list

    # Salida JSON (para CI / auto-alimentar la matriz):
    python3 robosoft.py --json > resultado.json

Códigos de salida:
    0  ningún FAIL
    1  al menos un invariante FAIL (hallazgo candidato a gap)
    2  error de arranque (backend inalcanzable, login fallido, etc.)
"""

from __future__ import annotations

import argparse
import json as _json
import os
import random
import string
import sys
import time

# Permite `python3 robosoft.py` desde cualquier cwd.
_HERE = os.path.dirname(os.path.abspath(__file__))
if _HERE not in sys.path:
    sys.path.insert(0, _HERE)

import harness  # noqa: E402
from contexts import (  # noqa: E402
    approvals_compliance, audit, authn_federated, authn_local,
    authorization_graph, authorization_topology, configuration, iga,
    refresh_token, robosoft_core, transversal_security,
)

# Registro canónico de los 11 contextos. `implemented=True` => se corre en vivo.
REGISTRY = [
    ("authn_local", authn_local, True),
    ("robosoft_core", robosoft_core, True),
    ("audit", audit, True),
    ("transversal_security", transversal_security, True),
    ("authorization_graph", authorization_graph, True),
    ("authorization_topology", authorization_topology, True),
    ("configuration", configuration, True),
    ("authn_federated", authn_federated, True),
    ("refresh_token", refresh_token, True),
    ("approvals_compliance", approvals_compliance, True),
    ("iga", iga, True),
]

DEFAULT_IMPLEMENTED = [key for key, _, impl in REGISTRY if impl]


def _make_run_id(seed):
    rng = random.Random(seed)
    return "".join(rng.choice(string.ascii_lowercase + string.digits) for _ in range(5))


def _resolve_contexts(args):
    by_key = {key: (mod, impl) for key, mod, impl in REGISTRY}
    if args.all:
        return [(k, m, i) for k, m, i in REGISTRY]
    if args.context:
        selected = []
        for name in args.context:
            if name not in by_key:
                print("Contexto desconocido: %s" % name, file=sys.stderr)
                print("Disponibles: %s" % ", ".join(by_key), file=sys.stderr)
                sys.exit(2)
            mod, impl = by_key[name]
            selected.append((name, mod, impl))
        return selected
    # por defecto: los 3 contextos implementados
    return [(k, m, i) for k, m, i in REGISTRY if k in DEFAULT_IMPLEMENTED]


def _print_list():
    print("Contextos del arnés RoboSoft (11):")
    for key, mod, impl in REGISTRY:
        estado = "IMPLEMENTADO" if impl else "esqueleto (ola 2)"
        print("  %-24s %-3d inv  %s" % (key, len(mod.INVARIANTS), estado))


def main():
    parser = argparse.ArgumentParser(description="Arnés RoboSoft — certificación E2E de UMS")
    parser.add_argument("--base", default=os.environ.get("ROBOSOFT_BASE", "http://localhost:18080"),
                        help="URL base del backend (def: http://localhost:18080 o $ROBOSOFT_BASE)")
    parser.add_argument("--context", action="append",
                        help="corre un contexto concreto (repetible). Def: los 3 implementados")
    parser.add_argument("--all", action="store_true", help="corre los 11 contextos (incl. esqueletos)")
    parser.add_argument("--list", action="store_true", help="lista contextos y sale")
    parser.add_argument("--seed", type=int, default=None,
                        help="semilla del run_id (determinismo reproducible). Def: reloj")
    parser.add_argument("--json", action="store_true", help="emite resultados en JSON")
    parser.add_argument("--verbose", action="store_true", help="traza de pasos")
    args = parser.parse_args()

    if args.list:
        _print_list()
        return 0

    seed = args.seed if args.seed is not None else int(time.time())
    run_id = _make_run_id(seed)
    http = harness.Http(args.base)

    # Salud + login admin (management-owner EVOLITH).
    health = http.request("GET", "/health")
    if health.status != 200:
        print("Backend no saludable en %s -> /health HTTP %d" % (args.base, health.status),
              file=sys.stderr)
        return 2
    login_resp = harness.admin_login(http)
    if login_resp.status != 200:
        print("Login admin falló: HTTP %d %s" % (login_resp.status, login_resp.snippet(160)),
              file=sys.stderr)
        return 2
    body = login_resp.json or {}
    admin_token = body.get("token")
    if not admin_token:
        print("Login admin sin token en la respuesta", file=sys.stderr)
        return 2

    rc = harness.RunContext(
        http=http,
        admin_token=admin_token,
        admin_cookie=login_resp.cookie,
        run_id=run_id,
        seed=seed,
        verbose=args.verbose,
    )

    contexts = _resolve_contexts(args)
    all_results = []
    for key, mod, _impl in contexts:
        try:
            all_results.extend(mod.run(rc))
        except Exception as exc:  # noqa: BLE001 - un contexto no debe tumbar al arnés
            all_results.append(harness.Result(
                context=getattr(mod, "NAME", key),
                code="INV-RUNNER",
                status=harness.FAIL,
                title="El contexto lanzó una excepción no controlada",
                evidence="%s: %s" % (type(exc).__name__, exc),
            ))

    if args.json:
        payload = {
            "run_id": run_id,
            "seed": seed,
            "base_url": args.base,
            "summary": harness.summarize(all_results),
            "results": [r.__dict__ for r in all_results],
        }
        print(_json.dumps(payload, ensure_ascii=False, indent=2))
    else:
        print_header(args.base, run_id, seed)
        harness.print_report(all_results, run_id, args.base)

    total = harness.summarize(all_results)
    return 1 if total[harness.FAIL] else 0


def print_header(base, run_id, seed):
    print("Arnés RoboSoft — backend=%s · run_id=%s · seed=%d" % (base, run_id, seed))


if __name__ == "__main__":
    sys.exit(main())
