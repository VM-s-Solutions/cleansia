---
id: T-0558
title: `UploadEmployeeDocument` and `UploadNewDocumentVersion` are dead — no controller, no dispatcher, and they model validation that must never be copied
status: done
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

- [x] **AC1 — deadness is proven, not assumed.** Given both command types, When the sweep is run, Then
      the ticket records the evidence that neither is dispatched: no `Send(new UploadEmployeeDocument…`
      / `UploadNewDocumentVersion…` anywhere, no controller action, and the only same-named symbol is
      `Policy.CanUploadEmployeeDocument`, which is a **different symbol** guarding `SaveMyDocuments`.
      Paste the greps with exit codes.
- [x] **AC2 — they are deleted.** Given AC1 holds, When the files are removed, Then the solution builds
      (`dotnet build Cleansia.Api.sln` from `src/`) and all three suites still pass.
- [x] **AC3 — the policy constant is untouched.** Given `Policy.cs:75`, When the commands are deleted,
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
- 2026-08-05 — done by backend. Both files deleted. AC4 not taken: no revival argued.

### AC1 evidence (at `24af741e`, before deletion)

A substring grep is what made this look inconclusive, so the proof is a **word-boundary** grep, which
excludes `CanUploadEmployeeDocument` by construction:

```
$ git grep -nE '(^|[^a-zA-Z])UploadEmployeeDocument' -- 'src/'
src/…/Features/EmployeeDocuments/UploadEmployeeDocument.cs:13:public class UploadEmployeeDocument
exit=0

$ git grep -nE '(^|[^a-zA-Z])UploadNewDocumentVersion' -- 'src/'
src/…/Features/EmployeeDocuments/UploadNewDocumentVersion.cs:12:public class UploadNewDocumentVersion
exit=0
```

The **only** occurrence of either name in the whole `src/` tree was its own class declaration. C# cannot
bind or dispatch a type without naming it and there is no reflective command discovery (the sole
`typeof(ICommand)` sweep is `EveryCommandHasValidatorTests`, a test), so zero references is a
compile-level proof of deadness — no controller action, no `Mediator.Send`, no direct instantiation.

- **Wire surface: clean.** `git grep -nE 'UploadEmployeeDocument|UploadNewDocumentVersion' -- '*.json'
  '*.yaml' '*.yml'` (exit 0) hit only `agents/archive/2026-08/backlog/audits/*.json` prose. The three committed specs —
  `src/cleansia_android/openapi/{customer-api,customer-mobile-api,partner-mobile-api}.json` — carry
  neither name nor a `…Command`/`…Response` schema derived from it. **Not a wire change.**
- **Tests: none referenced either command**, so nothing went green-by-deletion and no test was removed.
  The two reflective sweeps that could plausibly have covered them enumerate *controller actions*, not
  command types, and both are single `[Fact]`s: `Base64UploadIntakeRosterTests` (keys on `BlobFileDto`,
  which these never carried) and `SaveMyDocumentsRouteCoverageTests`. `EveryCommandHasValidatorTests` is
  also one `[Fact]`, so it silently enumerates two fewer types. **Test counts are unchanged: 3072 / 144 / 120.**
- **AC3 holds.** `Policy.CanUploadEmployeeDocument` and all five of its sites are untouched
  (`Policy.cs:75`, `PolicyBuilder.cs:79`, `FrozenPermissionMapTests.cs:72`,
  `Web.Partner/…/EmployeeController.cs:76`, `Web.Mobile.Partner/…/EmployeeController.cs:130`).

### The inherited claim, confirmed — and it was understated

Confirmed: `FileSizeBytes` is a plain `long` on the command with **no bytes anywhere in the request**
(no `BlobFileDto`, no base64, no stream), capped at `10 * 1024 * 1024`. It is a bound on a number the
caller types, and the handler then persisted that same number as the document's recorded size.

What the report did not say is that **`FilePath` is the more serious half**. Both download handlers feed
the stored value straight in as a blob name against the shared employee-documents container —
`blobClient.DownloadAsync(document!.FilePath, ct)` (`DownloadMyDocument.cs:88`,
`DownloadEmployeeDocument.cs:52`) — and the ownership check upstream is on the *document row*, not on
the blob the row names. A caller-chosen `FilePath` therefore becomes a caller-chosen read out of a
container shared by every employee, through a route that then correctly authorises the row. Contrast the
live path, which derives it server-side and uploads the bytes to it first:
`Constants.VirtualDirectories.EmployeeDocuments` formatted with `employee.Id`, plus
`{employee.Id}_{DocumentType}_{timestamp}_{guid8}{ext}` (`SaveMyDocuments.cs:125-145`).

So AC4's bar stands and is if anything higher than written: a revival may not reuse this shape.

### One orphan created, deliberately left — needs a follow-up

`BusinessErrorMessage.FileSizeExceeded10MB` (`BusinessErrorMessage.cs:367`, `"file.size_exceeded_10mb"`)
now has **zero backend references** — the two dead commands were its only ones. It is redundant with the
live `FileSizeExceeded` (`"file.size_exceeded"`) that `DocumentFileValidator.cs:29` actually uses.

It was **not** removed here, because removing it is a three-app frontend change, not a backend one:
`apps/cleansia.app/src/app/i18n/error-contract-parity.spec.ts:181` lists `file.size_exceeded_10mb` in
`CUSTOMER_SURFACE_ERROR_KEYS`, and that spec asserts *frontend key → BusinessErrorMessage value*, so
deleting the constant alone reddens it. A correct removal also deletes 15 translation rows
(3 apps × 5 locales) together, since a sibling assertion requires the five locale `api.*` key sets to be
identical. Out of this ticket's lane (`Features/EmployeeDocuments/**`) and out of the backend role.
Everything else checked out clean: `EmployeeDocumentItem`, its mapper, `IEmployeeDocumentRepository`,
`GetLatestByFileNameAsync`, `EmployeeDocument.Create`/`CreateNewVersion` and
`BusinessErrorMessage.FileTypeNotAllowed` all keep live callers.

## Review
<!-- reviewer verdict here; PM reconciles before advancing state -->
