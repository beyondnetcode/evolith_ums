.PHONY: build helm-up helm-down compose-up compose-down clean

# Build de imágenes con los nombres que espera el chart canónico ums-helm
# (ums/backend, ums/frontend). El backend usa el Dockerfile canónico
# apps/ums.api/Dockerfile (contexto src/); el feed privado de NuGet se autentica
# por BuildKit secret (G-017): exporta GH_TOKEN con un PAT read:packages.
#   GH_TOKEN=$(gh auth token) make build
build:
	DOCKER_BUILDKIT=1 docker build --secret id=gh_token,env=GH_TOKEN \
	  -t ums/backend:latest -f ../apps/ums.api/Dockerfile ..
	DOCKER_BUILDKIT=1 docker build \
	  -t ums/frontend:latest -f ../apps/ums.web-app/Dockerfile ..

# Despliegue con el ÚNICO chart canónico: ums-helm (PostgreSQL + Redis +
# observabilidad), coherente con el código (Persistence.Provider=PostgreSql,
# D-008). Los secretos los plantilla el propio chart (G-019); ya no se crean a
# mano con `kubectl create secret`. El chart de SQL Server fue retirado (G-018).
helm-up:
	helm upgrade --install ums ./ums-helm \
	  -f ums-helm/values/backend.yaml \
	  -f ums-helm/values/frontend.yaml

helm-down:
	helm uninstall ums || true

compose-up:
	docker compose -f local/compose/docker-compose.yml up -d

compose-down:
	docker compose -f local/compose/docker-compose.yml down -v

clean: helm-down compose-down
	docker images | grep ums | awk '{print $$3}' | xargs -r docker rmi -f
