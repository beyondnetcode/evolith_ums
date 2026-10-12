# =========================================================
# Phase 1: Dependency Resolution
# =========================================================
FROM node:24-alpine AS base
WORKDIR /usr/src/app
COPY package*.json ./
COPY apps/ums.web-app/package*.json ./apps/ums.web-app/
# G-123: el package-lock.json regenerado en macOS omite los binarios nativos opcionales de rollup
# (@rollup/rollup-linux-*-musl) — bug npm #4828 —, así que `npm ci`/`npm install` con ese lock dejan
# `vite build` sin rollup en alpine. Se borra el lock para forzar resolución fresca que incluya la
# optional-dep del target musl en build-time. El fix de fondo es regenerar el lock con plataformas linux.
RUN rm -f package-lock.json apps/ums.web-app/package-lock.json && npm install --no-audit --no-fund

# =========================================================
# Phase 2: Production Compilation
# =========================================================
FROM base AS builder
COPY . .
RUN npm run build --workspace=apps/ums.web-app

# =========================================================
# Phase 3: Secure Static Server (Nginx)
# =========================================================
FROM nginx:1.25-alpine AS runner

# Copy Vite-compiled static assets to Nginx html directory
COPY --from=builder /usr/src/app/apps/ums.web-app/dist /usr/share/nginx/html

# Nginx configuration as template: the official entrypoint runs envsubst over
# /etc/nginx/templates/*.template so API_UPSTREAM can be set per deployment.
COPY apps/ums.web-app/nginx.conf.template /etc/nginx/templates/default.conf.template
ENV API_UPSTREAM=http://ums-backend:80
# Upstream de Grafana para el proxy /grafana/ (observabilidad en la barra).
ENV GRAFANA_UPSTREAM=http://grafana:3000

EXPOSE 80

HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
  CMD wget --no-verbose --tries=1 --spider http://localhost:80/ || exit 1

CMD ["nginx", "-g", "daemon off;"]
