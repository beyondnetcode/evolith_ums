<div align="center">

# UMS: Enterprise User Management System

> **Bilingual Navigation:** [Versión en Español](../reference/indices/index.md)

[![Status](https://img.shields.io/badge/Status-Active-brightgreen?style=for-the-badge)]()
[![Platform](https://img.shields.io/badge/.NET_10_%7C_PostgreSQL_%7C_React_18-informational?style=for-the-badge)]()
[![Architecture](https://img.shields.io/badge/Evolith-Satellite_Product-blueviolet?style=for-the-badge)](https://github.com/beyondnetcode/evolith_arch32)
[![ADRs](https://img.shields.io/badge/ADRs-66_decisions-orange?style=for-the-badge)](../reference/architecture/adrs/)
[![License](https://img.shields.io/badge/License-Proprietary-red?style=for-the-badge)]()

<br/>

<a href="./docs/diagrams/evolith-ums-satellite.png" title="Evolith E2E Architecture - UMS Satellite Product - click to enlarge">
  <img src="./docs/diagrams/evolith-ums-satellite.png"
       alt="Evolith E2E Architecture - UMS Satellite Product"
       width="780"
       style="border-radius: 8px; box-shadow: 0 4px 20px rgba(0,0,0,0.3);" />
</a>

<sub>Evolith E2E Architecture Framework - UMS official satellite product - click to enlarge</sub>

<br/>

**UMS is a modular monolith for identity, authorization, configuration, approvals, compliance, IGA, and audit.**<br/>
Built on **.NET 10, PostgreSQL, EF Core through Npgsql, React 18, TypeScript, and Nx**.<br/>
It specializes the [Evolith](https://github.com/beyondnetcode/evolith_arch32) corporate architecture reference for a product-grade user management system.

> *Inherit the standard, specialize the product.*

</div>

---

## Start Here

<details>
<summary><strong>Primary entry points</strong></summary>

- [Product Vision](../reference/gobernanza/product/vision-producto.md) - strategy, product goals, and business positioning.
- [Architecture Portal](../reference/architecture/index.md) - architectural overview, ADRs, blueprints, and applied reference material.
- [Domain Model](../docs/02-diseno/dominio/index.md) - bounded contexts, aggregates, entities, and domain rules.
- [Functional Stories](../docs/02-diseno/historias-funcionales/index.md) - business-readable product backlog.
- [Master Index](../reference/indices/indice-maestro.md) - complete documentation navigation.
- [Evolith Upstream](https://github.com/beyondnetcode/evolith_arch32) - corporate reference base inherited by UMS.

</details>

<details>
<summary><strong>Getting started by role</strong></summary>

- **Architects:** start with [Architecture Portal](../reference/architecture/index.md), then review [ADR Registry](../reference/architecture/adrs/) and [Traceability Matrix](../reference/architecture/matriz-trazabilidad.md).
- **Backend developers:** start with [API .NET Reference](../reference/architecture/api-dotnet/README.md), then review [Domain Aggregates](../docs/02-diseno/dominio/index.md) and [.NET SDK](../reference/sdk/dotnet/README.md).
- **Frontend developers:** start with [Frontend Clean Architecture ADR](../reference/architecture/adrs/UMS-056-arquitectura-limpia-frontend.es.md), then review [TypeScript SDK](../reference/sdk/typescript/README.md) and [State Management ADR](../reference/architecture/adrs/UMS-057-estado-zustand-tanstack-query.es.md).
- **Product and PM:** start with [Product Vision](../reference/gobernanza/product/vision-producto.md), then review [Scope](../reference/gobernanza/product/alcance.md), [Objectives](../reference/gobernanza/product/objetivos.md), and [Gap Tracker](../reference/gobernanza/project/seguimiento-gaps-historias-funcionales.md).
- **DevOps and SRE:** start with [Infrastructure Plan](./infra/infrastructure_plan.md), then review [Operations Portal](../reference/operaciones/index.md), [Runbooks](../reference/operaciones/runbooks/index.md), and [Metrics](../reference/operaciones/metrics/index.md).
- **AI contributors:** start with [AGENTS.md](./AGENTS.md), then review [Documentation Control Agents](../reference/gobernanza/documentation-control-agents.md) and [ADR Template](../reference/gobernanza/sdlc/plantilla-adr.md).

</details>

## SDLC Navigation

Open the lifecycle area you are working in. Each section groups the documents and repository anchors that support its gate.

<details>
<summary><strong>Phase 00 - Product and Governance</strong></summary>

| Documento | Tipo |
| :--- | :--- |
| [Product Vision](../reference/gobernanza/product/vision-producto.md) | Guía |
| [Business Context](../reference/gobernanza/product/contexto-negocio.md) | Guía |
| [Scope and Boundaries](../reference/gobernanza/product/alcance.md) | Guía |
| [Objectives](../reference/gobernanza/product/objetivos.md) | Guía |
| [Governance Hub](../reference/gobernanza/index.md) | Índice |
| [Stakeholders](../reference/gobernanza/product/partes-interesadas.md) | Registro |

</details>

<details>
<summary><strong>Phase 01 - Requirements</strong></summary>

| Documento | Tipo |
| :--- | :--- |
| [Functional Story Standard](../docs/02-diseno/historias-funcionales/estandar-redaccion-historias-funcionales.md) | Estándar |
| [Requirements Hub](../reference/gobernanza/requirements/index.md) | Índice |
| [Functional Stories](../docs/02-diseno/historias-funcionales/index.md) | Índice |
| [Permission Matrix Example](../reference/gobernanza/requirements/ejemplo-matriz-permisos.md) | Matriz |
| [Conceptual Data Model](../reference/gobernanza/requirements/modelo-datos-conceptual.md) | Referencia |
| [Glossary](../reference/gobernanza/requirements/glosario.md) | Referencia |

</details>

<details>
<summary><strong>Phase 02 - Design and Architecture</strong></summary>

| Documento | Tipo |
| :--- | :--- |
| [Canonical Patterns](../reference/architecture/artifacts/canonical-patterns/index.md) | Guía |
| [Architecture Portal](../reference/architecture/index.md) | Índice |
| [ADR Registry](../reference/architecture/adrs/) | Índice |
| [DDD Design Hub](../reference/gobernanza/construction/ddd-design/index.md) | Índice |
| [Traceability Matrix](../reference/architecture/matriz-trazabilidad.md) | Matriz |
| [Evolith ADR Matrix](https://github.com/beyondnetcode/evolith_arch32/blob/main/reference/architecture/adrs/adr-matrix.md) | Matriz |
| [Architecture Overview](../reference/architecture/vision-general.md) | Referencia |
| [Blueprints](../reference/architecture/blueprints/) | Referencia |

</details>

<details>
<summary><strong>Phase 03 - Construction</strong></summary>

| Documento | Tipo |
| :--- | :--- |
| [Construction Hub](../reference/gobernanza/construction/index.md) | Índice |
| [SDK Portal](../reference/sdk/index.md) | Índice |
| [Bounded Context Map](../reference/gobernanza/construction/ddd-design/01-mapa-contextos-delimitados.md) | Referencia |
| [Cross-Context Flows](../reference/gobernanza/construction/ddd-design/10-flujos-entre-contextos.md) | Referencia |
| [DDD Primitives](../reference/gobernanza/construction/ddd-design/11-primitivas-ddd.md) | Referencia |
| [API .NET Applied Reference](../reference/architecture/api-dotnet/referencia-aplicada-api-dotnet.md) | Referencia |
| [Project Backlog](../reference/gobernanza/project/index.md) | Registro |

</details>

<details>
<summary><strong>Phase 04 - Validation and QA</strong></summary>

| Documento | Tipo |
| :--- | :--- |
| [Performance Testing Plan](../reference/gobernanza/testing/plan-pruebas-rendimiento.md) | Guía |
| [QA Report](../reference/qa/reporte-qa.md) | Registro |
| [Unit Testing Results](../reference/gobernanza/testing/resultados-pruebas-unitarias.md) | Registro |
| [Integration Testing Results](../reference/gobernanza/testing/resultados-pruebas-integracion.md) | Registro |
| [Performance Testing Results](../reference/gobernanza/testing/resultados-pruebas-rendimiento.md) | Registro |
| [QA Evidences](../reference/qa/evidences/) | Registro |

</details>

<details>
<summary><strong>Phase 05 - Delivery and Operations</strong></summary>

| Documento | Tipo |
| :--- | :--- |
| [Runbooks](../reference/operaciones/runbooks/index.md) | Guía |
| [Kubernetes Deployment Plan](./infra/UMS_K8s_Deployment_Plan.md) | Guía |
| [Infrastructure Plan](./infra/infrastructure_plan.md) | Guía |
| [Implementation Plan](./infra/implementation_plan.md) | Guía |
| [Documentation Release Process](../reference/releases/proceso-publicacion-documentacion.md) | Guía |
| [Operations Portal](../reference/operaciones/index.md) | Índice |
| [Metrics](../reference/operaciones/metrics/index.md) | Referencia |

</details>

## Cross-Cutting References

<details>
<summary><strong>Architecture, domain, and product reference</strong></summary>

- [Identity Domain](../docs/02-diseno/dominio/identity/index.md)
- [Authorization Domain](../docs/02-diseno/dominio/authorization/index.md)
- [Configuration Domain](../docs/02-diseno/dominio/configuration/index.md)
- [Approvals Domain](../docs/02-diseno/dominio/approvals/index.md)
- [IGA Domain](../docs/02-diseno/dominio/iga/index.md)
- [Audit Domain](../docs/02-diseno/dominio/audit/index.md)
- [Consistency Rules](../docs/02-diseno/dominio/consistency-rules/index.md)
- [SDK Contracts](../reference/sdk/contracts/resumen-esquema.md)
- [Documentation Standards](../reference/indices/estandares.md)
- [Bilingual Documentation Control](../reference/gobernanza/documentation-control-agents.md)

</details>

<details>
<summary><strong>UMS and Evolith inheritance</strong></summary>

- UMS inherits reusable architecture standards, governance rules, ADR patterns, and documentation practices from [Evolith](https://github.com/beyondnetcode/evolith_arch32).
- UMS keeps product-specific implementation, bounded contexts, schemas, seed strategy, and runtime behavior in this repository.
- Product ADRs may be promoted upstream when UMS provides executable evidence that the decision is reusable across products.
- Multi-tenancy is enforced primarily at the application layer. PostgreSQL policies, constraints, schema ownership, and row-level security are secondary infrastructure failsafes.

</details>

## Tools and Automation

<details>
<summary><strong>Local development commands</strong></summary>

Run technical commands from `src/` unless the command explicitly targets the backend solution.

```bash
# Install frontend dependencies
cd src
npm install

# Frontend: React 18 and Vite
npx nx run app-web:dev

# Backend: .NET 10
cd apps/ums.api
dotnet build
dotnet run

# Backend tests
dotnet test
```

</details>

<details>
<summary><strong>Documentation validation</strong></summary>

```bash
# From the repository root
python3 .bmad-core/scripts/cleanup_markdown_encoding.py

# From src/, when Context7 setup is needed
cd src
npx ctx7 setup
```

Documentation changes must keep English and Spanish artifacts synchronized, preserve UTF-8 integrity, and avoid decorative icons or non-standard Markdown characters.

</details>

---

## Contribution

Before contributing, read:

- [AGENTS.md](./AGENTS.md) - agent rules and repository conventions.
- [Standards](../reference/indices/estandares.md) - engineering and documentation standards.
- [ADR Template](../reference/gobernanza/sdlc/plantilla-adr.md) - how to propose a decision.
- [Child Repository Inheritance Guide](https://github.com/beyondnetcode/evolith_arch32/blob/main/reference/governance/standards/onboarding/child-repository-inheritance-guide.md) - how UMS inherits from Evolith.

---

## License

This repository is proprietary unless a separate license file states otherwise.

---

<div align="center">
  <sub>UMS - Enterprise User Management System | Evolith Satellite Product | .NET 10, React 18, PostgreSQL</sub>
</div>
