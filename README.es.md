# Evolith UMS

> Navegación bilingüe: [English](./README.md)

Evolith UMS es un monorepo de gestión de usuarios con una aplicación web React
y Vite y una API REST en .NET 10. La configuración del espacio Nx y el código
de las aplicaciones están en [`src/`](./src/).

## Requisitos

- Node.js 22 o posterior
- .NET SDK 10
- Docker Desktop u otro motor Docker para las pruebas de integración que usan contenedores

## Inicio Rápido

Instala las dependencias e inicia la aplicación web:

```bash
npm ci
npm ci --prefix src
npm run dev
```

La aplicación web se sirve con Vite. Para iniciar la API, restaura su solución
y ejecútala mediante Nx:

```bash
dotnet restore src/apps/ums.api/Evolith.Ums.sln
cd src
npx nx run app-api-dotnet:run
```

## Proyectos Nx

```bash
cd src
npx nx show projects
```

Actualmente, el espacio de trabajo detecta `app-web` y `app-api-dotnet`.

## Build, Lint y Pruebas

Desde la raíz del repositorio:

```bash
npm run build
npm run lint
npm test
npm run validate
```

El build compila la aplicación web y la solución .NET. Las pruebas web usan
Vitest; el objetivo de pruebas de la API incluye pruebas de integración que
pueden requerir Docker.
