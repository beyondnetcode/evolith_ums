# Update System Suite Command Handlers and Endpoints

## Goal
Replace `ToNoContent` usage on POST commands that create resources with appropriate `ToCreated` responses, and ensure command handlers return `Result<T>` with created identifiers where needed. Also verify that duplicate detection returns `409 Conflict` and validation errors return `400 Bad Request` as expected by the integration test suite.

## User Review Required
> [!IMPORTANT]
> The proposed changes will modify the public API contract for several endpoints (e.g., `AddModule`, `AddMenu`, `AddOption`, etc.). Ensure this aligns with any external consumers or documentation.

## Open Questions
> [!QUESTION]
> Do you prefer the location header for created resources to follow the pattern `/system-suites/{systemSuiteId}/modules/{moduleId}` (and similar for menus, options, etc.)?

## Proposed Changes
---
### Command Handlers
- **[MODIFY]** `AddModuleCommandHandler.cs`
  - Change return type to `Result<ModuleId>` and return the newly created module identifier.
- **[MODIFY]** `AddMenuCommandHandler.cs` (similar pattern for menus, submenus, options, app-settings, actions, domain-resources)
  - Update to return the created entity identifier.

---
### Endpoints (`SystemSuiteEndpoints.cs`)
- **[MODIFY]** `AddModule` endpoint
  - Replace `result.ToNoContent(context)` with `result.ToCreated(r => $"/system-suites/{systemSuiteId}/modules/{r.Value.GetId().GetValue()}", context)`.
- Apply analogous changes to other POST endpoints that create resources (menus, submenus, options, app-settings, actions, domain-resources).
- Ensure GET, PUT, DELETE endpoints continue to use `ToNoContent` or appropriate status mappings.

---
### Result Extensions
- No changes needed; `ToCreated` already maps success to `201 Created`.

---
### Verification Plan
- Run `dotnet test` after changes to ensure integration test failures drop from 21 to 0.
- Manually inspect a few endpoint responses using curl or a browser to verify `Location` header and status codes.

---
### Automated Tests
- No new tests required; existing integration tests will validate behavior.

---
### Documentation
- Update API docs (if any) to reflect new response contracts.

---
### Timeline
- Modify handlers and endpoints (estimated 30 min).
- Run tests and address any failures (estimated 15 min).
- Verify and commit (estimated 10 min).

---
## Verification Plan
### Automated Tests
- Execute `dotnet test` in the solution root.

### Manual Verification
- Use `curl -i -X POST /system-suites/{id}/modules` with duplicate code to confirm `409 Conflict`.
- Use `curl -i -X POST /system-suites/{id}/modules` with valid data to confirm `201 Created` and `Location` header.
