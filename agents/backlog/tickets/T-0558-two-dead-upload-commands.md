---
id: T-0558
title: `UploadEmployeeDocument` and `UploadNewDocumentVersion` are dead — no controller, no dispatcher, and they model validation that must never be copied
status: ready
size: XS
owner: backend
created: 2026-08-05
updated: 2026-08-05
depends_on: []
blocks: []
stories: []
adrs: []
layers: [backend]
security_touching: false
manual_steps: []
sprint: 15
source: found by the backend lane during the T-0548 upload sweep (`97bb7265`). Filed by the PM
  2026-08-05, low priority
---

## Context

Two command files in `src/Cleansia.Core.AppServices/Features/EmployeeDocuments/` have **no controller
action and no dispatcher**:

- `UploadEmployeeDocument.cs`
- `UploadNewDocumentVersion.cs`

PM-verified at HEAD: the only remaining hits on those names outside the command files themselves are an
**identically-named policy constant** — `Policy.CanUploadEmployeeDocument`
(`src/Cleansia.Core.AppServices/Authentication/Policy.cs:75`), applied at
`src/Cleansia.Web.Partner/Controllers/EmployeeController.cs:76` and
`src/Cleansia.Web.Mobile.Partner/Controllers/EmployeeController.cs:130` — plus
`src/Cleansia.Tests/Authentication/FrozenPermissionMapTests.cs`, which pins the permission map. **The
policy guards the live `SaveMyDocuments` route; it does not dispatch these commands.** A name-grep alone
reads as "in use", which is why this needed checking rather than assuming.

**Why file it at all, given it is dead code.** These two commands cap a **client-declared**
`FileSizeBytes` alongside a **client-supplied** `FilePath`. Both are attacker-controlled values being
treated as facts. Left in the tree they are a working-looking example of exactly the validation
approach the platform must not use — and the next person adding an upload endpoint will find them by
searching for "upload", not by reading `SaveMyDocuments`. **Dead code that looks like a correct
validation example is worse than dead code.**

## Acceptance criteria

- [ ] **AC1 — deadness is proven, not assumed.** Given both command types, When the sweep is run, Then
      the ticket records the evidence that neither is dispatched: no `Send(new UploadEmployeeDocument…`
      / `UploadNewDocumentVersion…` anywhere, no controller action, and the only same-named symbol is
      `Policy.CanUploadEmployeeDocument`, which is a **different symbol** guarding `SaveMyDocuments`.
      Paste the greps with exit codes.
- [ ] **AC2 — they are deleted.** Given AC1 holds, When the files are removed, Then the solution builds
      (`dotnet build Cleansia.Api.sln` from `src/`) and all three suites still pass.
- [ ] **AC3 — the policy constant is untouched.** Given `Policy.cs:75`, When the commands are deleted,
      Then `CanUploadEmployeeDocument` **remains**, because it guards the live route — and
      `FrozenPermissionMapTests` stays green. **Deleting the constant along with the commands would 403
      the working document-upload route on two hosts.** This AC exists because the names collide.
- [ ] **AC4 — if either is kept instead of deleted, the reason and the redesign are recorded.** Given a
      decision to revive rather than delete, When that is chosen, Then the ticket states that a revival
      may **not** reuse the current shape: a client-declared `FileSizeBytes` is not a size check and a
      client-supplied `FilePath` is not a storage location. The revived form must derive both
      server-side, per whatever T-0556 lands.

## Out of scope

- **`SaveMyDocuments`, the live path** — **T-0556**. It has real defects; these two files do not, because
  nothing runs them.
- The host-level body limit — **T-0557**.
- Any other dead-code sweep. This ticket is scoped to two named files.

## Implementation notes

**Files this ticket touches:**
- `src/Cleansia.Core.AppServices/Features/EmployeeDocuments/UploadEmployeeDocument.cs` (delete)
- `src/Cleansia.Core.AppServices/Features/EmployeeDocuments/UploadNewDocumentVersion.cs` (delete)
- **Read-only, must not change:** `src/Cleansia.Core.AppServices/Authentication/Policy.cs:75`,
  `src/Cleansia.Web.Partner/Controllers/EmployeeController.cs:76`,
  `src/Cleansia.Web.Mobile.Partner/Controllers/EmployeeController.cs:130`,
  `src/Cleansia.Tests/Authentication/FrozenPermissionMapTests.cs`

Check for orphaned DTOs/mappers/validators that existed only for these two commands, and for
`BusinessErrorMessage` keys referenced nowhere else — deleting a command that leaves its error key
behind creates the mirror-image orphan (`api.*` translations for a key nothing emits).

### Staleness detectability (sprint-15 §D3)

Names **product paths under `src/`**, so the candidate-3 path rule covers it: if either command file is
committed after this ticket's `updated:` date, it flags. Manual check at dispatch is one command:
`ls src/Cleansia.Core.AppServices/Features/EmployeeDocuments/`.

**No-decision note:** removal of unreachable code; AC4 is the escape hatch that turns it into a routed
decision if the lane argues for revival instead.

## Status log
- 2026-08-05 — created `ready` by pm, low priority. The name collision with `Policy.CanUploadEmployeeDocument`
  is called out in AC3 specifically because a careless "delete every hit" would take down the live
  document-upload route on two hosts.

## Review
<!-- reviewer verdict here; PM reconciles before advancing state -->
