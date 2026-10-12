# Accesos al Entorno Local (Kubernetes)

Este documento detalla todas las URLs, comandos y credenciales necesarias para acceder a los distintos componentes del proyecto UMS desplegado localmente usando Helm en Docker Desktop.

## 1. Configuración Previa Obligatoria

Para que el *Ingress Controller* local pueda enrutar el tráfico a tus aplicaciones usando nombres de dominio limpios, debes asegurarte de que tu archivo `/etc/hosts` tenga mapeados los dominios a `localhost` (`127.0.0.1`).

Ejecuta en tu terminal:
```bash
sudo sh -c 'echo "127.0.0.1 ums.local grafana.ums.local" >> /etc/hosts'
```

> **IMPORTANTE PARA USUARIOS MAC / DOCKER DESKTOP:**
> A veces Docker Desktop no vincula automáticamente el puerto 80 del Ingress al `localhost` de tu Mac. Si no puedes acceder a la web, debes abrir una terminal y dejar corriendo este comando para forzar el puente:
> ```bash
> sudo kubectl port-forward -n ingress-nginx svc/ingress-nginx-controller 80:80
> ```

---

## 2. Aplicaciones Web (vía Ingress)

Estos servicios están expuestos automáticamente en los puertos 80/443 de tu máquina anfitriona. Solo debes abrir tu navegador en los siguientes enlaces:

| Componente | URL Local | Descripción |
| :--- | :--- | :--- |
| **Portal Web (Frontend)** | [http://ums.local](http://ums.local) | Aplicación principal en React. |
| **API Backend (Swagger)** | [http://ums.local/api/swagger](http://ums.local/api/swagger) | (O la ruta base de Swagger configurada) Documentación interactiva de la API. |
| **Grafana** | [http://grafana.ums.local](http://grafana.ums.local) | Panel de observabilidad (Log in anónimo automático habilitado para modo dev). |

---

## 3. Servicios Internos y Bases de Datos (Port Forwarding)

Los componentes de infraestructura pesada como bases de datos o colectores de métricas corren aislados dentro del clúster (tipo `ClusterIP`). Para conectar clientes externos (como Azure Data Studio, SSMS, o Redis Insight), debes crear un puente o "Port-Forward" abriendo una terminal dedicada.

### PostgreSQL (Base de Datos)
> Persistencia PostgreSQL únicamente (D-008 / ADR-UMS-089). El chart de SQL Server se retiró (G-018).
* **Comando:** `kubectl port-forward svc/ums-postgres 5432:5432`
* **Conexión:** `localhost:5432` / `127.0.0.1:5432`
* **Usuario:** `postgres`
* **Contraseña:** `postgres` (default del chart; ver `ums-helm/values.yaml` → `secrets.db.postgresPassword`)

### Redis (Caché)
* **Comando:** `kubectl port-forward svc/redis 6379:6379`
* **Conexión:** `localhost:6379`

### Prometheus (Métricas Crudas)
* **Comando:** `kubectl port-forward svc/prometheus 9090:9090`
* **Acceso Web:** [http://localhost:9090](http://localhost:9090)

### Loki (Logs)
* **Comando:** `kubectl port-forward svc/loki 3100:3100`
* **Uso:** Generalmente se explora directamente desde Grafana, pero este es el puerto de la API directa de Loki.

### Tempo (Trazas Distribuidas)
* **Comando:** `kubectl port-forward svc/tempo 3200:3200`
* **Uso:** Generalmente explorado a través de Grafana, pero expone interfaces de API directas.

---

## 4. Notas de Administración

Si necesitas gestionar los pods directamente o acceder a la terminal de alguno de ellos, usa las etiquetas del Helm chart:

* **Listar todos los pods:** `kubectl get pods`
* **Ver logs de la API:** `kubectl logs -l app=ums-api -f`
* **Ver logs del Web:** `kubectl logs -l app=ums-web -f`
* **Reiniciar el backend:** `kubectl rollout restart deployment ums-api`
