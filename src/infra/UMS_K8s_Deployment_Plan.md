# UMS Kubernetes Deployment Plan

This document captures the full deployment plan for the UMS monorepo to a local Docker Desktop Kubernetes cluster, using PostgreSQL, Redis, and an observability stack (Prometheus, Grafana, Loki, Jaeger).

---

# Deploy UMS to Local Docker Desktop Kubernetes (full stack – PostgreSQL)

## Goal
Deploy the entire UMS monorepo (backend API, frontend web app, and supporting infrastructure) to the local Kubernetes cluster provided by Docker Desktop, ensuring:
- Super‑Admin user is seeded with password `root`
- Observability stack (metrics, logs, tracing) is available
- Caching layer (Redis) is provisioned
- **PostgreSQL** is used as the relational database

## User Review Required
- **Docker image tags** you would like to use (e.g., `latest` or a specific git‑sha).
- **Kubernetes namespace** (recommended: `ums-local`).
- Whether the admin password should be stored in a **Kubernetes Secret** or remain hard‑coded for dev.
- **Ingress host** for the UI (e.g., `ums.local`).
- **TLS** for local dev (self‑signed is fine).
- **Observability components** you want (Prometheus + Grafana, Loki, Jaeger).
- **Redis cache** – single‑node is enough for dev.

## Open Questions
> [!IMPORTANT]
> **Namespace** – target namespace?
>
> > (Recommended) `ums-local`
>
> **Ingress host** – hostname to reach the UI?
>
> > (Recommended) `ums.local`
>
> **TLS** – enable self‑signed TLS?
>
> > (Recommended) `yes`
>
> **Admin password secret** – store in a Secret?
>
> > (Recommended) `ums-admin-secret` with key `ADMIN_PASSWORD`
>
> **Observability stack** – include which tools?
>
> > (Recommended) Prometheus, Grafana, Loki, Jaeger
>
> **Redis cache** – single instance?
>
> > (Recommended) `true`

## Proposed Changes
---
### Core Services
#### Backend (API)
- Add a **Dockerfile** (multi‑stage) that builds the .NET 10 API and runs migrations on startup.
- Expose **Prometheus metrics** endpoint (`/metrics`) via `UsePrometheus()` (already in code).
- Add **OpenTelemetry** instrumentation for tracing (Jaeger exporter).
- Environment variables for observability:
  - `OTEL_EXPORTER_JAEGER_ENDPOINT=http://ums-jaeger:14268/api/traces`
  - `OTEL_METRICS_EXPORTER=prometheus`
- Helm values (`values-backend.yaml`):
  ```yaml
  image:
    repository: ums/backend
    tag: <tag>
  env:
    - name: ASPNETCORE_ENVIRONMENT
      value: Production
    - name: ConnectionStrings__Default
      valueFrom:
        secretKeyRef:
          name: ums-db-secret
          key: CONNECTION_STRING
    - name: ADMIN_PASSWORD
      valueFrom:
        secretKeyRef:
          name: ums-admin-secret
          key: ADMIN_PASSWORD
    - name: REDIS_CONNECTION
      value: redis://ums-redis:6379
    - name: OTEL_EXPORTER_JAEGER_ENDPOINT
      value: http://ums-jaeger:14268/api/traces
    - name: OTEL_METRICS_EXPORTER
      value: prometheus
  service:
    type: ClusterIP
  replicaCount: 1
  resources:
    limits:
      cpu: "500m"
      memory: "512Mi"
    requests:
      cpu: "250m"
      memory: "256Mi"
  livenessProbe:
    httpGet:
      path: /healthz
      port: 80
    initialDelaySeconds: 10
    periodSeconds: 10
  readinessProbe:
    httpGet:
      path: /ready
      port: 80
    initialDelaySeconds: 5
    periodSeconds: 5
  ```

#### Frontend (Web App)
- Ensure production build (`npm run build`).
- Dockerfile using Nginx to serve static files.
- Helm `values-frontend.yaml`:
  ```yaml
  image:
    repository: ums/frontend
    tag: <tag>
  service:
    type: ClusterIP
  ingress:
    enabled: true
    hosts:
      - host: ums.local
        paths:
          - path: /
            pathType: Prefix
    tls:
      - hosts:
          - ums.local
        secretName: ums-tls
  resources:
    limits:
      cpu: "200m"
      memory: "256Mi"
    requests:
      cpu: "100m"
      memory: "128Mi"
  ```

### Supporting Infrastructure
#### PostgreSQL
- Deploy the official `postgres:15-alpine` image.
- Helm values (`values-db.yaml`):
  ```yaml
  image:
    repository: postgres
    tag: 15-alpine
  env:
    - name: POSTGRES_USER
      value: postgres
    - name: POSTGRES_PASSWORD
      valueFrom:
        secretKeyRef:
          name: ums-db-secret
          key: POSTGRES_PASSWORD
    - name: POSTGRES_DB
      value: ums
  persistence:
    enabled: true
    size: 5Gi
  ```
- Create a **Secret** `ums-db-secret` with keys:
  - `POSTGRES_PASSWORD` – your dev password (e.g., `postgres`).
  - `CONNECTION_STRING` – `Host=ums-postgres;Database=ums;Username=postgres;Password=$(POSTGRES_PASSWORD);`

#### Redis Cache
- Deploy the official `redis:7-alpine` chart (stand‑alone).
- Helm values (`values-redis.yaml`):
  ```yaml
  architecture: standalone
  replicaCount: 1
  auth:
    enabled: false
  ```

#### Observability Stack
- **Prometheus + Grafana**: `kube-prometheus-stack` chart.
- **Loki**: `grafana/loki-stack` chart for log aggregation.
- **Jaeger**: `jaegertracing/jaeger` chart for tracing.
- Provide a `values-observability.yaml` that enables ServiceMonitors for the API and Nginx, and sets scrape intervals.

### Helm Chart Structure
```
ums-helm/
├─ Chart.yaml
├─ templates/
│   ├─ backend-deployment.yaml
│   ├─ backend-service.yaml
│   ├─ frontend-deployment.yaml
│   ├─ frontend-service.yaml
│   ├─ ingress.yaml
│   ├─ postgres-deployment.yaml
│   ├─ redis-deployment.yaml
│   └─ observability/ (sub‑charts imports)
└─ values/
    ├─ backend.yaml
    ├─ frontend.yaml
    ├─ postgres.yaml
    ├─ redis.yaml
    └─ observability.yaml
```
- Each sub‑chart pulls in the appropriate values files.
- Include a **Job** (`ums-seeder`) that runs the seeder commands (`dotnet run --project Evolith.Ums.Api.csproj` with a `--seed` flag) if you need to reseed data.

## Verification Plan
### Automated Tests
1. **Build images** locally:
   ```bash
   docker build -t ums/backend:latest ./src/apps/ums.api
   docker build -t ums/frontend:latest ./src/apps/ums.web-app
   ```
2. **Secrets** — el chart los plantilla (G-019), así que un `helm install` limpio ya los crea (`ums-db-secret`, `ums-admin-secret`) con los defaults de `values.yaml` (`secrets.*`). Para gestionarlos fuera del chart, pon `secrets.create=false` y créalos a mano:
   ```bash
   kubectl create secret generic ums-db-secret \
       --from-literal=POSTGRES_PASSWORD=postgres \
       --from-literal=CONNECTION_STRING="Host=ums-postgres;Database=ums;Username=postgres;Password=postgres;"
   kubectl create secret generic ums-admin-secret --from-literal=ADMIN_PASSWORD=root
   ```
3. **Render manifests** and validate:
   ```bash
   helm template ./ums-helm \
     -f values/backend.yaml \
     -f values/frontend.yaml \
     -f values/postgres.yaml \
     -f values/redis.yaml \
     -f values/observability.yaml | kubeval
   ```
4. **Install to local cluster**:
   ```bash
   helm upgrade --install ums ./ums-helm \
     -n ums-local --create-namespace \
     -f values/backend.yaml \
     -f values/frontend.yaml \
     -f values/postgres.yaml \
     -f values/redis.yaml \
     -f values/observability.yaml
   ```
5. **Port‑forward the UI** and test:
   ```bash
   kubectl port-forward svc/ums-frontend 8080:80 -n ums-local
   ```
   Open `http://ums.local` (or `http://localhost:8080`).
6. **Login** with `admin@ums.local` / `root` and verify the full admin suite appears.
7. **Check observability**:
   - Grafana (`http://ums-grafana:3000`) shows dashboards for API metrics and Nginx logs.
   - Jaeger UI (`http://ums-jaeger:16686`) displays traces for the login flow.
   - Prometheus scrapes `/metrics` from the API.
   - Loki receives Nginx access/error logs.
8. **Validate cache**:
   ```bash
   kubectl exec -it $(kubectl get pod -l app=ums-redis -n ums-local -o jsonpath='{.items[0].metadata.name}') -- redis-cli ping
   ```
   Should return `PONG`.

### Manual Checks
- Force‑password‑reset endpoint works and the flow forces a password change on first login.
- Verify that the PostgreSQL schema was created correctly (tables, constraints, migrations applied).
- Ensure that the seeder job (`ums-seeder`) can be re‑run without errors.

---
**Next steps:**
1. Confirm the open questions above (tags, namespace, secrets, observability choices).
2. Once approved, we will run the Docker builds, create the Kubernetes secrets, render the Helm chart, and apply it to your local Docker Desktop Kubernetes cluster.

*When you’re ready, just provide the values you want to use or any adjustments you need.*
