# UMS Coverage Strategy

**Scope:** `Evolith.Ums.Domain` + `Evolith.Ums.Application` — business-critical layers only.
**Tooling:** `coverlet.collector` (instrument) + `dotnet-reportgenerator-globaltool` (report).
**Runner:** `./coverage.sh` (local) · `./coverage.sh --ci` (enforces thresholds, exits non-zero on miss).

---

## 1. What to cover

| Layer | Why it matters | Target threshold |
|---|---|---|
| `Evolith.Ums.Domain` — aggregates, state machines | Every `if` guards business invariants; bugs here are silent data corruption | **≥ 85% line** |
| `Evolith.Ums.Application` — command handlers | Auth guards, not-found guards, domain delegation; bugs here bypass security | **≥ 75% line** |
| Combined | Composite gate in CI | **≥ 80% line** |

These thresholds are practical minimums, not aspirational targets. The goal is to ensure every **decision branch in production-reachable code** has at least one test exercise path.

---

## 2. What to exclude

Coverage of the following is intentionally excluded because they add noise without surfacing real risk:

| Pattern | Reason to exclude |
|---|---|
| `*Props` — record property bags | Pure structural, no conditional logic |
| `*Event` — domain event POCOs | Data carriers with no behaviour |
| `*DomainErrors` — static error constants | Constant strings, no branches |
| `*.DTOs.*` — request/response records | Structural shapes, validated by FluentValidation separately |
| `*Validator` — FluentValidation classes | Tested via `ValidationBehaviorTests`; rule-chains have own test class |
| `Evolith.Ums.Infrastructure.*` — EF Core mappings, DbContext, bootstrapper | Integration tests (Testcontainers) own this layer |
| `Evolith.Ums.Presentation.*` — controllers/endpoints | API integration tests own this layer |
| `Evolith.Ums.Shell.*` — DDD kernel (base classes, Result, Entity) | Tested implicitly via domain tests; library code |
| `Evolith.Ums.Globalization.*` — resource strings | Static data, no logic |

---

## 3. Include / exclude filter syntax (Coverlet)

```
Include:  [Evolith.Ums.Domain]*,[Evolith.Ums.Application]*
Exclude:  [Evolith.Ums.Domain]*.Props,
          [Evolith.Ums.Domain]*Event,
          [Evolith.Ums.Domain]*DomainErrors,
          [Evolith.Ums.Application]*.DTOs.*,
          [Evolith.Ums.Application]*Validator,
          [Evolith.Ums.Infrastructure]*,
          [Evolith.Ums.Presentation]*,
          [Evolith.Ums.Shell.*]*,
          [Evolith.Ums.Globalization]*
```

These filters are wired into `coverage.sh` — no manual configuration needed.

---

## 4. What the tests cover (test pyramid)

### Domain layer (`Evolith.Ums.Domain.Test`)
Focus: **state machine correctness, invariants, event contracts**.

| Aggregate | Key scenarios tested |
|---|---|
| `FeatureFlag` | Create (Boolean/Percentage/Variant), boundary percentages (0/100), Activate/Deactivate/Archive + lifecycle cycles, EvaluationLog accumulation, archived guard, event pairs (Activated+StateChanged, etc.) |
| `AppConfiguration` | All 4 scope resolution paths (Global/Tenant/Suite/Module), full Draft→Published→Archived lifecycle, all illegal transitions, Update version bump sequence, metadata flags (IsInheritable, IsEncrypted), event contract per operation |
| `ApprovalRequest` | Pending→Approved/Rejected, terminal state matrix (all 4 cross-transitions), immutability of WorkflowId/TargetUserId/ProfileId, null profile path, no-events contract |

### Application layer (`Evolith.Ums.Application.Test`)
Focus: **auth guards, not-found guards, domain failure surfacing, repository interaction**.

| Handler group | Key scenarios tested |
|---|---|
| `FeatureFlag` commands | Create (success, duplicate code, invalid FlagType, invalid LinkedResourceType, percentage without rollout), Activate/Deactivate/Archive (success, not-found, domain failure), Evaluate (active→true, inactive→false, archived, non-GUID userId, no auth) |
| `AppConfiguration` commands | Create (success, duplicate scope+code, global scope, no auth), Publish (success, not-found, not-draft), Archive (success, not-found, draft→fails, already-archived), Update (success, not-found, published→fails, archived→fails) |
| `ApprovalRequest` commands | Create (success, null profileId, no auth), Approve (success, not-found, already-approved, rejected→fails, no auth), Reject (success, not-found, already-rejected, approved→fails, no auth) |

---

## 5. What is explicitly not tested here

- **Query handlers** — read-only projections with no branching logic; covered by API integration tests
- **FluentValidation rules** — each `*CommandValidator` has a dedicated `*CommandValidatorTests` class
- **Repository implementations** — Testcontainers integration tests in `Evolith.Ums.Presentation.IntegrationTest`
- **Infrastructure (EF Core, SQL Server, outbox)** — integration tests own this
- **Frontend** — Vitest unit tests + Playwright E2E in `ums.web-app`

---

## 6. Running the report locally

```bash
cd src/apps/ums.api

# First time: restore tools
dotnet tool restore

# Run and open report
./coverage.sh
open coverage/report/index.htm   # macOS
xdg-open coverage/report/index.htm  # Linux
```

CI usage (fails build if thresholds are not met):

```bash
./coverage.sh --ci
```

---

## 7. CI integration (GitHub Actions)

Add to `.github/workflows/ci.yml`:

```yaml
- name: Restore dotnet tools
  run: dotnet tool restore
  working-directory: src/apps/ums.api

- name: Run tests with coverage
  run: ./coverage.sh --ci
  working-directory: src/apps/ums.api

- name: Upload coverage report
  uses: actions/upload-artifact@v4
  with:
    name: coverage-report
    path: src/apps/ums.api/coverage/report/
    retention-days: 14
```

---

## 8. Evolving the thresholds

| Phase | Recommended thresholds |
|---|---|
| MVP (current) | Domain ≥ 85%, Application ≥ 75%, Combined ≥ 80% |
| Post-MVP (EP-06/07/08) | Domain ≥ 88%, Application ≥ 80%, Combined ≥ 85% |
| Production-stable | Domain ≥ 90%, Application ≥ 85%, Combined ≥ 88% |

Ratchet up thresholds as each épica ships — never lower them.
