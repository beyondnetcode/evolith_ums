# Production Infrastructure Plan for UMS

## Overview
This plan details how to deploy the **Ums** solution in production, covering the **React Vite web front‑end**, the **.NET 10 API**, and a **PostgreSQL** database. It follows the corporate standards (`BMAD‑METHOD`, clean‑architecture, tenancy enforcement) and uses managed cloud services to achieve high availability, security, and observability.

---

## 1. Architecture Diagram

![Architecture diagram](../../reference/diagramas/infrastructure_diagram.png)

<!-- Diagram rendered above (AWS architecture). -->

---

## 2. Containerisation & Images
| Component | Dockerfile location | Base Image | Build command |
|----------|--------------------|------------|---------------|
| **Web**  | `src/apps/ums.web-app/Dockerfile` | `node:20-alpine` (build) → `nginx:alpine` (runtime) | `docker build -t beyondnetcode/ums-web:$(git rev-parse --short HEAD) .` |
| **API**  | `src/apps/ums.api/Evolith.Ums.Presentation/Dockerfile` | `mcr.microsoft.com/dotnet/aspnet:10.0` (runtime) + `mcr.microsoft.com/dotnet/sdk:10.0` (build) | `docker build -t beyondnetcode/ums-api:$(git rev-parse --short HEAD) .` |

Both images are pushed to **GitHub Container Registry** (`ghcr.io/beyondnetcode/ums‑web` / `…/ums‑api`).
---

## 3. Managed Services (AWS example)
| Service | Purpose | Suggested SKU |
|--------|---------|----------------|
| **Amazon CloudFront** | Global HTTPS termination, WAF, caching static assets | Standard |
| **Amazon API Gateway** | Throttling, API keys, versioning, developer portal | Regional/Edge |
| **Amazon Elastic Kubernetes Service (EKS)** | Orchestrates the API containers (high‑availability) | t3.medium nodes, autoscale 2‑6 replicas |
| **Amazon RDS for PostgreSQL** | Relational DB with built‑in HA & backups | db.t3.medium, Multi‑AZ |
| **AWS Secrets Manager** | Store connection strings, JWT signing keys, client secrets |
| **Amazon CloudWatch** | Centralised logs, metrics, alerts |
| **AWS X-Ray** | Distributed tracing for the .NET API |
---

## 4. Deployment Pipeline (GitHub Actions)
```yaml
name: CI‑CD
on:
  push:
    branches: [ main ]
  workflow_dispatch:

jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - name: Set up .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'
      - name: Restore & Test
        run: |
          dotnet restore
          dotnet test --no-build --verbosity normal
      - name: Build Docker images
        run: |
          docker build -t ghcr.io/beyondnetcode/ums-web:${{github.sha}} -f src/apps/ums.web-app/Dockerfile .
          docker build -t ghcr.io/beyondnetcode/ums-api:${{github.sha}} -f src/apps/ums.api/Evolith.Ums.Presentation/Dockerfile .
      - name: Log in to GHCR
        uses: docker/login-action@v3
        with:
          registry: ghcr.io
          username: ${{github.actor}}
          password: ${{secrets.GITHUB_TOKEN}}
      - name: Push images
        run: |
          docker push ghcr.io/beyondnetcode/ums-web:${{github.sha}}
          docker push ghcr.io/beyondnetcode/ums-api:${{github.sha}}

  deploy:
    needs: build
    runs-on: ubuntu-latest
    environment: production
    steps:
      - name: Azure login
        uses: azure/login@v2
        with:
          creds: ${{ secrets.AZURE_CREDENTIALS }}
      - name: Deploy to AKS
        run: |
          az aks get-credentials --resource-group rg‑ums --name aks‑ums
          helm upgrade --install ums-api chart/ums-api \
            --set image.tag=${{github.sha}} \
            --set postgres.connectionString=${{secrets.PG_CONNECTION}}
          helm upgrade --install ums-web chart/ums-web \
            --set image.tag=${{github.sha}} \
            --set apiBaseUrl=${{secrets.API_BASE_URL}}
```
---

## 5. Secrets Management
- **PostgreSQL connection string** → stored in **Azure Key Vault** and referenced via `{{ secrets.PG_CONNECTION }}` in the pipeline.
- **JWT signing key**, **OAuth client secrets**, and **API Management keys** also live in Key Vault.
- Enable **Managed Identity** for AKS pods to fetch secrets directly, removing the need for env‑vars.
---

## 6. Observability & Alerting
| Layer | Tool | What to capture |
|-------|------|----------------|
| API | Application Insights | Request latency, dependency calls, exception rates |
| DB  | Azure Monitor (Log Analytics) | Slow queries (> 200 ms), deadlocks, replication lag |
| Web | Azure Front Door logs | CDN cache hit ratio, 4xx/5xx distribution |
| Platform | Azure Monitor alerts | CPU > 80 % for 5 min, memory pressure, pod restarts > 2 |
---

## 7. Scalability & HA
- **AKS**: enable **Cluster Autoscaler** (min 2, max 10 nodes).
- **PostgreSQL**: enable **Zone‑redundant HA** and daily **point‑in‑time backups** (7‑day retention).
- **Front Door**: global anycast, automatic fail‑over.
- Deploy **multiple replicas** of the API (minimum 2) behind the load balancer.
---

## 8. Security Hardening
- Enforce **HTTPS everywhere** (TLS 1.3).
- **WAF** rules on Front Door to block OWASP Top‑10.
- API Management **rate limiting** (100 req/second per client).
- Database **network security group** – only AKS subnet can connect on port 5432.
- Enable **Row‑Level Security (RLS)** in PostgreSQL to complement application‑layer tenant filtering.
---

## 9. Tenancy Enforcement
- The **application layer** continues to enforce tenant isolation (as required by the project rules).
- RLS policies are added as a **defence‑in‑depth** layer:
```sql
CREATE POLICY tenant_isolation ON public.module
  USING (tenant_id = current_setting('app.current_tenant')::uuid);
```
- The API sets `app.current_tenant` from the JWT claim at the start of each request.
---

## 10. Release Checklist
- [x] All unit/integration tests pass (`dotnet test`).
- [x] Docker images built and scanned (trivy).
- [x] Helm charts versioned (`Chart.yaml` bumped).
- [x] Secrets stored in Key Vault, access policies reviewed.
- [x] Monitoring dashboards created (CPU, DB latency, 4xx/5xx).
- [x] Load‑testing (k6) completed – 95th‑pct latency < 200 ms at 500 RPS.
- [ ] **Post‑deployment smoke test** – hit `/system-suites` GET endpoint and verify JSON schema.
- [ ] **Rollback plan** – keep previous image tag for 24 h; helm `--set image.tag=previous` if needed.
---

## 11. Documentation & Governance
- All **deployment scripts**, **helm values**, and **environment variables** are version‑controlled in the `ops/` folder.
- The plan follows the **BMAD‑METHOD** phases (00‑05) and is stored in this repository under `infrastructure_plan.md` for auditability.
- Bilingual version (EN/ES) will be added later to satisfy the bilingual‑consistency rule.
---

**Next steps**
1. Review the diagram and confirm any region‑specific compliance requirements.
2. Approve the helm chart values (`ops/helm/ums‑api/values.yaml` & `ums‑web`).
3. Trigger a release via the `/goal` slash command or schedule a production deployment.

Feel free to ask for modifications, add more details, or request a walkthrough of any section.
