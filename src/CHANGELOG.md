## Unreleased (2026-06-03)

### Features

- **authorization:** DDD domain resource hierarchy — adds `DomainMethod` as a third `DomainResourceType` variant (`DomainMethod = 3`) with `ParentResourceId` nullable column; hierarchy invariants enforced in `SystemSuite.AddDomainResource`; auth graph builder includes DomainMethod nodes as addressable permission targets ([b4294be](https://github.com/beyondnetcode/ums/commit/b4294be))
- **authorization:** consistent state-change and deletion dependency guard policy — application command handlers check active dependencies before executing guarded operations and return HTTP 409 with structured `BlockedOperationResponse` payload including `errorCode`, `message`, `brokenRule`, and `blockingDependencies[]` ([e22629c](https://github.com/beyondnetcode/ums/commit/e22629c))

### Thank You

- Alberto Arroyo Raygada @nestjslatam

---

## 0.0.9 (2026-05-10)

### Features

- **devops:** enforce 70% coverage gate for ADR 0018 ([7f957ec](https://github.com/beyondnetcode/ums/commit/7f957ec))

### Thank You

- Alberto Arroyo Raygada @nestjslatam

## 0.0.8 (2026-05-10)

### Features

- **devops:** provide FF dependencies for ADR 0017 ([d7eaef7](https://github.com/beyondnetcode/ums/commit/d7eaef7))

### Thank You

- Alberto Arroyo Raygada @nestjslatam

## 0.0.7 (2026-05-10)

### Features

- **devops:** provide context storage dependency for ADR 0016 ([c2a7d8b](https://github.com/beyondnetcode/ums/commit/c2a7d8b))

### Thank You

- Alberto Arroyo Raygada @nestjslatam

## 0.0.6 (2026-05-10)

### Features

- **devops:** provide EDA dependencies for ADR 0015 ([320f33e](https://github.com/beyondnetcode/ums/commit/320f33e))

### Thank You

- Alberto Arroyo Raygada @nestjslatam

## 0.0.5 (2026-05-10)

### Features

- **devops:** provide caching dependencies for ADR 0014 ([3d97238](https://github.com/beyondnetcode/ums/commit/3d97238))

### Thank You

- Alberto Arroyo Raygada @nestjslatam

## 0.0.4 (2026-05-10)

### Features

- **devops:** provide authentication dependencies for ADR 0012 ([0da5161](https://github.com/beyondnetcode/ums/commit/0da5161))

### Thank You

- Alberto Arroyo Raygada @nestjslatam

## 0.0.3 (2026-05-10)

### Features

- **bmad:** introduce Configuration & Feature Management Platform - Multi-IdP, System Config, Feature Flags, ADR-0024, bounded context update ([6f0161f](https://github.com/beyondnetcode/ums/commit/6f0161f))
- **bmad:** enhance Feature Flag framework with pluggable IFeatureFlagPort - ADR-0025, LaunchDarkly/Unleash/ConfigCat/Azure adapters, provider selector strategy ([07d41fb](https://github.com/beyondnetcode/ums/commit/07d41fb))
- **bmad:** integrate hierarchical config resolution & C4 updates - UC-09, overrides strategy, business-context alignment ([a7d036b](https://github.com/beyondnetcode/ums/commit/a7d036b))
- **bmad:** update Strategic OKRs & Product Vision - optional external IdPs/FFs design mandate, native fallback core ([6330316](https://github.com/beyondnetcode/ums/commit/6330316))
- **bmad:** decouple UMS from Product Planner - declare as 100% abstract, API/Message Bus driven standalone security kernel ([2202272](https://github.com/beyondnetcode/ums/commit/2202272))
- **bmad:** update business-context diagram with pluggable auth/config/FF architecture ([00628d8](https://github.com/beyondnetcode/ums/commit/00628d8))
- **bmad:** create master audit alignment and enterprise architecture spec - unified B-M-A-D pillars ([dfd76b4](https://github.com/beyondnetcode/ums/commit/dfd76b4))
- **bmad:** introduce Customizable Hosted Login Page feature - UC-10, schema properties, scope, and alignment ([2388cf4](https://github.com/beyondnetcode/ums/commit/2388cf4))
- **core:** add ADR 0008 for progressive multi-module evolution, API Gateway, and BFF patterns ([630a05c](https://github.com/beyondnetcode/ums/commit/630a05c))
- **core:** implement ADR 0009 strict dependency pinning and CI vulnerability shield ([a813c68](https://github.com/beyondnetcode/ums/commit/a813c68))
- **core:** implement Result pattern (ADR 0019) and update tracking matrix ([e5a4ee3](https://github.com/beyondnetcode/ums/commit/e5a4ee3))
- **devops:** provide production-grade docker files and complete agnostic docker-compose stack ([4df1bb1](https://github.com/beyondnetcode/ums/commit/4df1bb1))
- **devops:** integrate LGTM stack and circuit breaker patterns ([ef760b0](https://github.com/beyondnetcode/ums/commit/ef760b0))

### Fixes

- **bmad:** quote Mermaid link labels in business-context to resolve parser error ([500dce0](https://github.com/beyondnetcode/ums/commit/500dce0))
- **bmad:** simplify Mermaid arrow labels to alphanumeric/underscore to ensure 100% engine compatibility ([86ae67e](https://github.com/beyondnetcode/ums/commit/86ae67e))
- **bmad:** remove sequence note over tags from flowchart and simplify cylinder shapes to fix parser error completely ([d668c0d](https://github.com/beyondnetcode/ums/commit/d668c0d))

### Thank You

- Alberto Arroyo Raygada @nestjslatam

## 0.0.2 (2026-05-08)

### Features

- **core:** initial enterprise architecture and bmad setup ([c277f8b](https://github.com/beyondnetcode/ums/commit/c277f8b))

### Fixes

- **docs:** restore utf-8 encoding for emojis in readme ([c855866](https://github.com/beyondnetcode/ums/commit/c855866))

### Thank You

- Alberto Arroyo Raygada @nestjslatam