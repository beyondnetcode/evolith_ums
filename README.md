# Evolith UMS

> Bilingual navigation: [Español](./README.es.md)

Evolith UMS is a user-management monorepo with a React and Vite web application
and a .NET 10 REST API. Nx workspace configuration and application source live
in [`src/`](./src/).

## Requirements

- Node.js 22 or newer
- .NET SDK 10
- Docker Desktop or another Docker engine for integration tests that use containers

## Quick Start

Install dependencies and start the web application:

```bash
npm ci
npm ci --prefix src
npm run dev
```

The web app is served by Vite. To run the API, restore its solution and start it
through Nx:

```bash
dotnet restore src/apps/ums.api/Evolith.Ums.sln
cd src
npx nx run app-api-dotnet:run
```

## Nx Projects

```bash
cd src
npx nx show projects
```

The workspace currently discovers `app-web` and `app-api-dotnet`.

## Build, Lint, and Tests

From the repository root:

```bash
npm run build
npm run lint
npm test
npm run validate
```

The build compiles the web app and .NET solution. The web tests run with Vitest;
the API test target includes integration tests that may require Docker.
