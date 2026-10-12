# Transactional Reliability & Operational Risk Assessment

**API:** UMS (User Management System) — .NET 10 / EF Core / SQL Server 2022
**Date:** 2026-05-23
**Scope:** Backend / API layer — transactions, atomicity, idempotency, concurrency, resilience, isolation, auditability, observability, security failure points

---

## Executive summary

The UMS API has a **solid DDD foundation** (aggregate roots, value objects, Result pattern, validation pipeline, clean architecture) but is operating at **maturity level 2–3 out of 5** on transactional reliability. Five production-blocking gaps exist; four represent silent data-loss or corruption paths that will manifest under normal operating conditions — not just edge cases.

| Area | Maturity | Verdict |
|---|---|---|
| Domain integrity (state machines, invariants) | ★★★★★ | Excellent |
| Transaction atomicity (single aggregate) | ★★★★☆ | Good — with one critical bug |
| Error handling & HTTP classification | ★★★★☆ | Good |
| Input validation pipeline | ★★★★☆ | Good |
| Rate limiting | ★★★☆☆ | Adequate |
| Audit stamping | ★★★☆☆ | Adequate — manual, no interceptor |
| Outbox infrastructure | ★★★☆☆ | Captured but **never delivered** |
| Tenant/context isolation | ★★☆☆☆ | Manual only — no EF global filter |
| Idempotency | ★☆☆☆☆ | **Not implemented** |
| Optimistic concurrency | ★☆☆☆☆ | **Not implemented** |
| Cross-aggregate transactions | ★☆☆☆☆ | **Not scoped** |
| Outbox dispatcher | ★☆☆☆☆ | **Not implemented** |
| Observability | ★★☆☆☆ | Basic correlation ID only |

---

## 1. Risks enumerated

### CRITICAL — silent data loss or corruption

#### RISK-01: Events marked committed before `SaveChangesAsync` succeeds

**File:** `SqlServerTenantRepository.cs` (and all SQL Server repositories) — `SaveEntitiesAsync`

```csharp
foreach (var aggregate in _trackedAggregates)
{
    dbContext.OutboxMessages.AddRange(OutboxMessageFactory.CreateFromAggregate(aggregate));
    aggregate.DomainEvents.MarkChangesAsCommitted();  // ← events cleared HERE
}
_trackedAggregates.Clear();                           // ← tracking cleared HERE
await dbContext.SaveChangesAsync(cancellationToken);  // ← if this throws → events GONE
```

**Scenario:** Database is unavailable for 1 second. EF Core's built-in retry fires. But `MarkChangesAsCommitted()` already cleared the aggregate's event collection. The retry attempt calls `SaveChangesAsync()` again — but the OutboxMessages are still in EF's change tracker (not yet persisted), so the DB write retries correctly. _However_, if the operation ultimately fails after 5 retries, the events are permanently gone from memory. The aggregate appears to have succeeded (no error thrown from domain), but neither the DB state nor the outbox has the changes.

**Real-world trigger:** Transient connection drop during a high-traffic period. Expected frequency: 1–5 times per day in production.

**Fix:** Move `MarkChangesAsCommitted()` to _after_ `SaveChangesAsync` succeeds.

---

#### RISK-02: No optimistic concurrency control — lost update

**Files:** All `*Record.cs` entity classes — no `[ConcurrencyToken]` or `RowVersion` property.

**Scenario:**
1. Request A reads `AppConfiguration` ID=X (version 1.0.0).
2. Request B reads the same `AppConfiguration` ID=X (version 1.0.0).
3. Request A calls `Update()` → version bumps to 1.1.0, persists.
4. Request B calls `Update()` → version bumps to 1.1.0 again (started from 1.0.0), overwrites A's changes silently.

Result: Request A's update is permanently lost. No error, no log.

**Affects:** Every mutable aggregate — tenants, user accounts, configurations, feature flags, profiles, delegations.

**Fix:** Add `RowVersion byte[]` to all record entities; configure EF Core with `.IsRowVersion()`; catch `DbUpdateConcurrencyException` in repositories and return `Result.Failure("Concurrency conflict")`.

---

#### RISK-03: No outbox dispatcher — domain events never delivered

**Files:** `OutboxMessage.cs`, `OutboxMessageFactory.cs`, `PersistenceOptions.cs` (`EnableOutbox = true`).

The outbox infrastructure is fully implemented: the SQL table exists, messages are captured atomically with aggregate changes, the `IX_OutboxMessages_Dispatch` index is optimized for polling.

**But:** There is no `IHostedService` or background worker that reads from `OutboxMessages WHERE ProcessedOnUtc IS NULL` and dispatches them.

**Scenario:** Tenant is created → `TenantCreatedEvent` captured in outbox → sits in DB forever → downstream services (audit, notifications, IGA onboarding) never triggered → system appears functional but is silently decoupled.

**Impact:** Every business-critical integration (cross-service notifications, audit trails, compliance triggers) is broken at the production level. This is a silent failure — no error is visible to the API caller.

**Fix:** Implement `OutboxDispatcherBackgroundService : BackgroundService` that polls every 2–5 seconds for unprocessed messages, dispatches to `INotificationPublisher` or MediatR `INotification`, marks `ProcessedOnUtc` on success, increments `RetryCount` + sets `LastError` on failure.

---

#### RISK-04: Tenant isolation relies entirely on manual repository code

**Files:** `SqlServerTenantRepository.cs`, `SqlServerAppConfigurationRepository.cs`, etc.

EF Core global query filters (`HasQueryFilter()`) are **not configured** for any entity. There is no automatic tenant scoping in the DbContext. Tenant filtering happens only in specific `GetByTenantIdAsync()` methods; `GetAllAsync()` returns all tenants, `GetByIdAsync(id)` returns any record regardless of tenant.

**Scenario:** A handler calls `_repository.GetAllAsync()` in a multi-tenant context. Without explicit filtering, all tenants' data is returned. If a miscoded endpoint calls this method instead of `GetByTenantIdAsync`, cross-tenant data leaks.

**Scenario 2:** `AppConfigurationRepository.GetByScopeAndCodeAsync(tenantId: null, ...)` — passing null tenant ID intentionally queries global configs but could accidentally return tenant-scoped configs if the `WHERE x.TenantId == null` condition matches wrong records.

**Fix:** Add `HasQueryFilter(e => e.TenantId == null || e.TenantId == _tenantContext.OrganizationId)` for all tenant-scoped entities. The RLS SESSION_CONTEXT is already configured as a failsafe; the EF filter is the primary guard (as documented in `OrganizationDbContextInterceptor.cs` comments).

---

#### RISK-05: No idempotency mechanism — duplicate requests create duplicate state

**Status:** No `IdempotencyKey` header, no request deduplication table, no response caching by request ID.

**Scenario:** Mobile client sends `POST /api/v1/user-accounts` (create user). Network times out after 28 seconds (before server response arrives). Client retries. Server processes the first request (user created), processes the second request (duplicate email check: if first commit completed, returns 409; if first is still in-flight, race condition — potentially two users with same email).

**Scenario 2 (more insidious):** Client uses an HTTP client with automatic retry (Polly, `HttpClient` retry handler). Sends `POST /api/v1/tenants`. Gets a 500 on the first attempt (server succeeded but response serialization failed). Client retries. Two tenants with the same code are created if the duplicate-code check is not atomic.

**Fix:** Implement `IdempotencyMiddleware` that reads `Idempotency-Key` header, stores request hash + response in a `IdempotencyRequests` table (with TTL), returns cached response on duplicate key.

---

### HIGH — operational reliability risks

#### RISK-06: Cross-aggregate partial failure — no distributed transaction scope

**Scenario:** A command handler modifies two aggregates (e.g., create `ApprovalRequest` and update `ApprovalWorkflow` status). Each repository has its own `SaveEntitiesAsync`. If the first save succeeds and the second fails (transient error, constraint violation), the system is left in an inconsistent state: one aggregate persisted, the other not.

**Affected operations:**
- Approve delegation → update delegation status + emit approval event + create audit record
- Publish permission template → update template + notify affected profiles

**Fix:** Implement a `UnitOfWorkScope` or transaction coordinator that wraps multiple repository operations in a single `IDbContextTransaction`. EF Core's `dbContext.Database.BeginTransactionAsync()` supports this.

---

#### RISK-07: AuditRecord, Approval, IGA aggregates are in-memory only

**File:** `UmsPlatformDbContext.cs`, line 32 — `// TODO(api-aggregate-tracker): Add SQL-backed DbSets...`

All data for Approvals, IGA (role promotions, impact analysis), compliance documents, and AuditRecord is stored in `ConcurrentDictionary<Guid, T>` in-process memory. On any restart, pod recycle, or scale-out event, this data is permanently lost.

**Real-world trigger:** Daily Kubernetes pod restart for certificate rotation → all approvals-in-flight are lost → orphan records in dependent systems.

---

#### RISK-08: Audit stamping is manual — prone to omission

**Pattern in every repository:**
```csharp
// Apply() method in SqlServerTenantRepository.cs
target.CreatedBy = replacement.CreatedBy;
target.CreatedAtUtc = replacement.CreatedAtUtc;
target.UpdatedBy = replacement.UpdatedBy;
target.UpdatedAtUtc = replacement.UpdatedAtUtc;
```

This copy is done manually in each repository's `Apply()` method. If a new repository is added without this block, audit fields will be null or stale — silently, with no compile-time or runtime warning.

**Fix:** Implement `AuditInterceptor : SaveChangesInterceptor` that detects `Added`/`Modified` states and stamps `CreatedAtUtc`, `UpdatedAtUtc`, `CreatedBy`, `UpdatedBy` automatically from `IUserContext`.

---

#### RISK-09: Non-idempotent retry within EF Core retry policy

**File:** `DependencyInjection.cs`, line 55: `sqlServer.EnableRetryOnFailure(5);`

EF Core's built-in retry is safe for _read_ operations and _single-statement writes_ but is **not safe** for multi-statement transactions. When `SaveChangesAsync` is retried after a transient failure, the entire change set (aggregate + outbox messages) is re-executed. This is correct for atomicity but means the `OutboxMessages.AddRange()` call happens inside the retry scope — which it does, correctly.

However, because `MarkChangesAsCommitted()` fires _before_ the retry loop (see RISK-01), a failed-then-retried save will attempt to persist OutboxMessages for events that no longer exist in the aggregate's event collection. The `GetUncommittedChanges()` call in `OutboxMessageFactory` will return an empty collection on retry, resulting in the aggregate being saved without its outbox messages.

**Compound risk:** RISK-01 + RISK-09 together mean: transient failure on first attempt → retry saves aggregate but no outbox messages → events permanently lost, no error surfaced.

---

#### RISK-10: Validation error message concatenation leaks internals

**File:** `ValidationBehavior.cs`:
```csharp
var errors = validationResults
    .SelectMany(result => result.Errors)
    .Select(error => $"{error.PropertyName}: {error.ErrorMessage}")
    .Distinct()
    .ToArray();

return CreateValidationResult<TResponse>(string.Join("; ", errors));
```

FluentValidation `PropertyName` values include full dotted path (e.g., `Command.TenantId.Value.RootId`). The concatenated error string is then passed directly to `DomainErrorStatusMapper.Map()` which does substring matching. A property path containing "not found" as a substring would be misclassified as a 404.

**Fix:** Use structured validation errors (list of `ValidationError { Field, Message }`) rather than a concatenated string.

---

### MEDIUM — correctness and observability risks

#### RISK-11: OutboxMessageFactory uses reflection to extract TenantId

**File:** `OutboxMessageFactory.cs`:
```csharp
private static Guid? ExtractTenantId(IDomainEvent domainEvent)
{
    var property = domainEvent.GetType().GetProperty("TenantId");
    if (property?.PropertyType == typeof(Guid))
        return (Guid?)property.GetValue(domainEvent);
    return null;
}
```

If an event has `TenantId` as `Nullable<Guid>` (most do: `Guid?`), `property.PropertyType == typeof(Guid)` returns `false`, so `TenantId` is never extracted. The outbox message's `TenantId` will always be `null`, breaking tenant-aware outbox dispatch.

---

#### RISK-12: Rate limiting is per-IP only — shared NAT blindspot

**File:** `Program.cs` — `FixedWindowRateLimiter` per `httpContext.Connection.RemoteIpAddress`.

Corporate users behind NAT (common in enterprise B2B scenarios) share the same IP. 100 users hitting the API simultaneously from the same NAT will trigger the rate limiter at 100 requests/minute total, not per user.

---

#### RISK-13: No distributed tracing — correlation ID is local only

The `GlobalExceptionHandler.cs` emits a `correlationId` in error responses using `context.TraceIdentifier` (ASP.NET's per-request ID). There is no OpenTelemetry instrumentation, no propagation of `traceparent` / `tracestate` headers, no distributed trace ID that spans the API → outbox → dispatcher → downstream service chain.

---

#### RISK-14: No circuit breaker for DB — connection storm risk

`sqlServer.EnableRetryOnFailure(5)` provides transient retry but no circuit breaker. Under sustained DB pressure, every request will retry 5 times before failing, multiplying load on an already-stressed DB by 5×. A 1-second DB hiccup becomes 5 seconds of saturated thread pool.

---

## 2. Maturity assessment by area

### Transactions & Atomicity  ★★★☆☆
**What works:** Single-aggregate operations are atomic — aggregate changes and outbox messages are written in one `SaveChangesAsync` call. EF Core's transaction scope wraps both.

**Gaps:**
- RISK-01: Events cleared before commit (silent loss on failure)
- RISK-06: No cross-aggregate transaction scope
- RISK-09: Retry policy interacts badly with RISK-01

### Consistency  ★★★☆☆
**What works:** Domain state machines enforce valid transitions. Value objects enforce type invariants. Validation pipeline catches bad inputs early.

**Gaps:**
- RISK-02: No optimistic concurrency (last write wins)
- RISK-06: Cross-aggregate inconsistency on partial failure
- RISK-07: In-memory aggregates lose state on restart

### Idempotency  ★☆☆☆☆
**What works:** Duplicate-code guards in handlers prevent obvious duplicate creates — but only if the first request has already committed.

**Gaps:**
- RISK-05: No request-level idempotency key
- In-flight race window where two identical requests both pass the duplicate check

### Concurrency  ★☆☆☆☆
**What works:** Nothing.

**Gaps:**
- RISK-02: No RowVersion / ConcurrencyToken on any entity
- No `DbUpdateConcurrencyException` handling
- Concurrent updates silently overwrite each other

### Event Publishing Consistency  ★★★☆☆
**What works:** Outbox pattern correctly captures events in the same transaction as aggregate changes. The infrastructure (table, index, factory) is well-designed.

**Gaps:**
- RISK-01: Events cleared before save — compound failure path
- RISK-03: No dispatcher — events sit in DB forever
- RISK-11: TenantId never extracted into outbox messages (reflection type mismatch)

### Error Handling  ★★★★☆
**What works:** Result<T> pattern prevents exceptions from propagating. `DomainErrorStatusMapper` maps error strings to precise HTTP status codes. `GlobalExceptionHandler` catches everything with RFC 7807 ProblemDetails. `ValidationBehavior` runs FluentValidation before handlers.

**Gaps:**
- RISK-10: Error message concatenation can cause misclassification
- Validation errors return concatenated string, not structured list

### Tenant / Context Isolation  ★★☆☆☆
**What works:** `ITenantContext` prevents mid-request context hijacking. SQL Server RLS SESSION_CONTEXT is set via `OrganizationDbContextInterceptor`. RLS predicates are in SQL schema.

**Gaps:**
- RISK-04: No EF global query filter — all `GetAllAsync()` methods return all tenants' data
- InMemory repositories (used in tests) have no tenant filtering at all
- `GetByIdAsync(id)` makes no tenant check

### Audit Trail  ★★★☆☆
**What works:** All SQL-backed entities have 5 audit columns. ActorId is threaded through all command handlers and stored.

**Gaps:**
- RISK-08: Manual stamping in `Apply()` methods — omission-prone
- RISK-07: `AuditRecord` aggregate is in-memory only — not persisted
- No immutable audit log table (entity records can be overwritten)

### Observability  ★★☆☆☆
**What works:** `correlationId` in error responses. Request logging via ASP.NET middleware. Development-mode stacktraces.

**Gaps:**
- RISK-13: No distributed tracing (no OpenTelemetry)
- No metrics (no Prometheus counters for failed commands, outbox lag, retry count)
- No health check for outbox backlog
- No alerting for `LastError` accumulation in outbox
- Structured logging exists but no log correlation across services

### Security Failure Points  ★★★☆☆
**What works:** `X-User-Id` / `X-User-Name` headers drive authentication. Handlers check `IsNullOrWhiteSpace(_userContext.UserId)`. Rate limiting prevents brute force.

**Gaps:**
- `X-User-Id` header is trusted without cryptographic verification (no JWT validation visible)
- No per-user rate limiting — only per-IP
- Unauthorized access returns string error matching that could be pattern-matched by attackers

---

## 3. Prioritized recommendations

| # | Risk | Impact | Fix complexity | Priority |
|---|---|---|---|---|
| FIX-01 | RISK-01: Move `MarkChangesAsCommitted` after SaveChanges | Critical — silent event loss | Low (1 line per repo) | **P0** |
| FIX-02 | RISK-03: Implement OutboxDispatcherBackgroundService | Critical — events never delivered | Medium (2–3 days) | **P0** |
| FIX-11 | RISK-11: Fix TenantId reflection type mismatch in OutboxMessageFactory | Critical — outbox tenant context wrong | Low (10 min) | **P0** |
| FIX-03 | RISK-02: Add RowVersion to all entities + handle DbUpdateConcurrencyException | High — lost updates | Medium (1 day) | **P1** |
| FIX-04 | RISK-07: Migrate Approvals, IGA, AuditRecord to SQL-backed repos | High — data loss on restart | High (2–3 weeks) | **P1** |
| FIX-05 | RISK-04: Add EF HasQueryFilter for tenant-scoped entities | High — data leakage | Low (2 hours) | **P1** |
| FIX-06 | RISK-05: Implement IdempotencyMiddleware + IdempotencyRequests table | High — duplicate state | Medium (3 days) | **P2** |
| FIX-07 | RISK-06: Implement UnitOfWorkScope for cross-aggregate transactions | High — partial failure | Medium (1 day) | **P2** |
| FIX-08 | RISK-08: Implement AuditInterceptor (SaveChangesInterceptor) | Medium — audit reliability | Low (4 hours) | **P2** |
| FIX-09 | RISK-10: Structured validation errors instead of string concat | Medium — misclassification | Low (2 hours) | **P3** |
| FIX-10 | RISK-13: Add OpenTelemetry instrumentation | Medium — blind operations | High (2 days) | **P3** |
| FIX-12 | RISK-14: Add Polly circuit breaker for DB | Medium — connection storm | Low (1 hour) | **P3** |

---

## 4. Concrete backend / API test plan

### Layer 1 — Application unit tests (mock repositories)

These tests verify handler and domain behavior without infrastructure.

| Test | Validates |
|---|---|
| `T01_AtomicSave_AggregateAndOutbox_SameTransaction` | `SaveEntitiesAsync` persists aggregate + outbox in one call |
| `T02_SaveEntities_WhenSaveChangesFails_EventsAreAlreadyLost` | **Documents RISK-01** — events cleared before commit |
| `T03_Handler_WhenDomainFails_SaveEntitiesNotCalled` | No persistence on domain error |
| `T04_Handler_WhenValidationFails_SaveEntitiesNotCalled` | No persistence on validation error |
| `T05_DuplicateCreateCommand_WithSameCode_SecondReturnsConflict` | Duplicate detection works |
| `T06_InvalidStateTransition_Handler_Returns422` | State machine guard surfaced via handler |
| `T07_ConcurrencyGap_TwoUpdates_LastWriteWins` | **Documents RISK-02** — no optimistic lock |
| `T08_IdempotencyGap_TwoIdenticalCreates_BothSucceed` | **Documents RISK-05** — no deduplication |
| `T09_ErrorClassification_NotFound_Maps404` | HTTP status mapping |
| `T10_ErrorClassification_AlreadyExists_Maps409` | HTTP status mapping |
| `T11_ErrorClassification_AuthRequired_Maps401` | HTTP status mapping |
| `T12_AuditFields_OnCreate_ArePopulated` | Audit trail |

### Layer 2 — Integration tests (in-memory repositories + full HTTP stack)

| Test | Validates |
|---|---|
| `T13_Create_ThenGet_ReturnsCreatedResource` | Atomic commit visible to subsequent reads |
| `T14_DuplicateCreate_SameCode_Returns409` | Duplicate guard end-to-end |
| `T15_InvalidTransition_ViaHttp_Returns422` | State machine via HTTP |
| `T16_Unauthenticated_NoUserId_Returns401` | Auth guard |
| `T17_StateLifecycle_CreatePublishArchive_Success` | Full lifecycle atomicity |
| `T18_DuplicateRequest_Rapid_CreatesTwo` | **Documents RISK-05** — shows the gap |
| `T19_ConcurrentUpdates_LastWriteWins` | **Documents RISK-02** — shows the gap |
| `T20_TenantIsolation_GetAllReturnsAllTenants` | **Documents RISK-04** — shows the gap |

---

## 5. Automated test implementation

See companion test files:
- `Evolith.Ums.Application.Test/Common/Reliability/TransactionalAtomicityTests.cs` — T01–T12
- `Evolith.Ums.Presentation.IntegrationTest/Reliability/ReliabilityIntegrationTests.cs` — T13–T20
