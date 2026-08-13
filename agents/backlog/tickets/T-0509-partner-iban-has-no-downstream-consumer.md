---
id: T-0509
title: The cleaner's bank account is a T-0470-class value — sweep its exposure in logs, exports and list DTOs
status: done
size: S
owner: security
created: 2026-08-02
updated: 2026-08-05
depends_on: []
blocks: []
stories: []
adrs: [0034]
layers: [backend]
security_touching: true
manual_steps: []
sprint: 15
---

> **REWRITTEN 2026-08-02 after the owner's answer.** The original ticket asked *"the IBAN has no
> downstream consumer — wire it or delete it"*, and offered **(b) stop collecting it and delete what is
> held** as a legitimate outcome. **The owner answered decision 3: bank details stay, CZ first, built
> to extend — and decision 4 gives them a consumer, the payout invoice.** Option (b) is dead and the
> "find it a purpose" question is closed. **What survives is the original AC6 — the exposure check —
> and the PM's own grounding found it is bigger than one AC.** `depends_on: [T-0508]` **removed**: the
> sweep never needed the invoice ruling and was always dispatchable.

## Context

**PM-verified first-hand at `master` 2026-08-02.** `Employee.IBAN` is not the write-only field the
onboarding investigation described. It has **four** couplings, three of which are exposure surfaces:

| Coupling | Where | What it means |
|---|---|---|
| Profile-completeness gate | `Employee.cs:283` (`hasEmployeeInfo`), `:313` (`missingFields` → `"profile.fields.iban"`) | a cleaner with no IBAN is **blocked from taking orders** — not an exposure, but it is why "just delete it" was never as free as it looked |
| **GDPR subject-access export** | `GdprExportDto.cs:41` | correct and required — but it must keep working when T-0518 changes the shape |
| **Admin paged LIST DTO** | `EmployeeListItem.cs:52` carries `Iban` | **a full account number on a list response.** Every admin list page ships every cleaner's account details to the browser |
| Anonymization + an audit-log assertion | `Employee.cs:262`, `EmployeeUserAuditCoverageTests.cs:301` (`Assert.DoesNotContain(SubjectIban, json)`) | **someone already thought about this once, for the audit log only** |

**Why it is a T-0470-class value.** Sprint-14 established two things: this platform writes PII into
Information-level request logs on all five hosts (**T-0457**, `ready`, P1), and **a sensitive value
whose field name is not `*Secret*`/`*Token*`/`*Key*`/`*Password*`-shaped is caught by no redaction
list** (**T-0470**). `Iban` / `BankAccountNumber` match none of those tokens. The existing audit-log
assertion proves the concern was recognised **in exactly one place** and nowhere else.

**And the timing matters.** `Q-OBS-01` may turn Sentry on for DEV. **An error tracker that ships log
context would carry whatever is in those logs to a third party** — which is why sprint-15 already
records that T-0457 should land first. This ticket establishes whether account numbers are in that
blast radius.

## Acceptance criteria

- [ ] **AC1 — the request/response log exposure is CHECKED, not assumed.** Do any of the five API
      hosts write a payload containing `Iban` at Information level? Name the endpoints
      (`UpdateBankDetails`, `UpdateEmployee`, `AdminUpdateEmployee`, the employee list/detail queries)
      and state the result per host. Evidence: the check, with the commands run and their output.
- [ ] **AC2 — the finding is cross-noted on T-0457 and T-0470**, whichever way it lands. **A clean
      result is a result and must be recorded** — it is the evidence that stops the question being
      re-asked. Evidence: the cross-notes.
- [ ] **AC3 — the admin list DTO is ruled on.** Does a paged admin list need full account numbers?
      Options: remove it from the list DTO (keep it on the detail), mask it, or keep it with the
      justification written down. **This ticket makes the recommendation; T-0519 AC10 implements it** —
      or this ticket implements it if it is a one-line DTO change and T-0519 has not started.
      Evidence: the ruling plus the diff or the hand-off.
- [ ] **AC4 — the GDPR export is confirmed correct and is flagged to survive T-0518.** An SAR export
      that silently drops the new payout columns is a compliance regression introduced by a feature.
      Evidence: the confirmation plus the note on T-0518 AC-level.
- [ ] **AC5 — the audit-log guard is generalised or its narrowness is recorded.**
      `EmployeeUserAuditCoverageTests.cs:301` asserts the IBAN never reaches audit JSON. **Is there an
      equivalent guard for logs? For the outbox? For emails?** Evidence: the inventory.
- [ ] **AC6 — every claim is a run, not a read.** Greps and their output, not "it appears that".
      Evidence: the commands.
- [ ] **AC7 — the SECURITY gate runs** (this ticket is `security`-owned; the gate is the deliverable).
      Evidence: the verdict.
- [ ] **AC8 — if a code change lands, the suites are green.** `Cleansia.Tests` /
      `Cleansia.IntegrationTests` / `Cleansia.HostTests` **locally**, baselines **2295 / 108 / 75**.
      **If the sweep finds nothing, an empty diff is a successful close.**

## Out of scope

- **Deleting the field or stopping collection.** **The owner ruled it stays** (decision 3). That option
  is closed.
- **The new payout schema and its validation** — T-0517 / T-0518 / T-0519. **This ticket sweeps what
  exists today**, so its finding lands before the field set gets wider.
- **Fixing T-0457's PII logging.** That is T-0457, `ready`, P1, already filed. This ticket only
  establishes whether account numbers are inside its blast radius.
- **The invoice** — T-0508 / T-0522.

## Implementation notes

**Dispatchable today, no dependency, ~an hour.** Its value is highest **before** T-0518 widens the
field set, because a sweep of one column is cheaper than a sweep of five.

**Read first:** `docs/architecture/security-rules.md` (S1–S10), sprint-14's **T-0457** and **T-0470**,
`Employee.cs:255-320`, `GdprExportDto.cs`, `EmployeeListItem.cs`,
`EmployeeUserAuditCoverageTests.cs`, and `Q-OBS-01` in `questions/open.md`.

## Status log
- 2026-08-02 — **draft (created by pm from the partner-onboarding investigation)** as *"the IBAN is
  collected and read by nothing — wire it or delete it"*, `depends_on: [T-0508]`.
- 2026-08-02 — **REWRITTEN → `ready`, re-owned to `security`.** The owner's decisions 3 and 4 answer
  the original question (the field stays; its consumer is the payout invoice), so the ticket keeps the
  half that is still true and still unowned: **the exposure sweep.** **The PM re-grounded it and the
  original premise was wrong in a useful way** — the IBAN is *not* read by nothing. It gates
  `hasEmployeeInfo` (`Employee.cs:283`), which decides whether a cleaner may take orders; it is in the
  **GDPR export**; and it is on the **admin paged list DTO** (`EmployeeListItem.cs:52`), which ships
  every cleaner's account number to every admin list page. `depends_on` cleared — the sweep never
  needed the invoice ruling.
- 2026-08-04 — **PM sprint-15 reconciliation — the premise MOVED and the ticket must be re-aimed before it
  runs.** This ticket was written to sweep the exposure of `Employee.Iban`. That field no longer exists on
  the surfaces it named: the payout work (`3092abc1`, `7e1cf7f5`) moved bank details to their own entity
  (`EmployeePayoutDetails`), and the owner's regen (`37440bbc`) **removed `iban` from `EmployeeItem` and
  `UpdateEmployeeCommand`**. Verified at HEAD: `Features/Employees/DTOs/EmployeeListItem.cs` no longer
  carries `Iban`, so the admin **paged list DTO** exposure — the headline finding — is closed as a
  side-effect.
- 2026-08-04 — **the sweep itself was never run and is still worth running, against the NEW target.** Two
  of the three legs are untouched by the move: logs, and the GDPR export (which now has its own
  `GdprExportPayoutDetailsDto`). ADR-0034 decided **plaintext deliberately** — *encrypting a column we
  print, email and GDPR-export is theatre* — and wrote down four reversal triggers; this sweep is what
  checks those triggers have not fired. Add the new single-resource read paths
  (`GetMyPayoutDetails`, `GetEmployeePayoutDetails`, `RevealEmployeePayoutDetails`) to the scope, and note
  that `RevealEmployeePayoutDetails` was deliberately built as a **Command** so it rides audit, rate limit
  and the coverage guard.
- 2026-08-04 — **stays `ready`, size unchanged (`S`).** The re-aim is a scope correction, not new work.

- 2026-08-04 — **swept (backend). Most of it was already satisfied — and the one leg that was not is the
  biggest single PII surface on the platform.**

  **AC1 — request/response logs, CHECKED. One real hole, now closed.**
  `IsSensitivePath` covered `/updatebankdetails`, `/getmypayoutdetails`, `payout-details`
  (which catches both the masked read and `/reveal`) and `/gdpr` on all five hosts. But the admin export
  is routed `/api/v{version}/AdminGdpr/export/{userId}`, and `pathValue.Contains("/gdpr")` **never
  matched it** — there is no slash before "gdpr" inside "AdminGdpr". Identical shape to the `/auth/`
  vs `/api/AdminAuth/…` miss T-0446 found. That route returns another user's entire `GdprExportDto`:
  profile PII, `GdprExportEmployeeDto.IBAN`, and the full `GdprExportPayoutDetailsDto`
  (account number, prefix, bank code, IBAN, SWIFT, holder name). Verified by simulation before the fix:

  ```
  /api/v1/Gdpr/export             -> ['/gdpr']
  /api/v1/AdminGdpr/export/user-1 -> []
  ```

  Fixed by matching the **trailing** slash — `pathValue.Contains("gdpr/")` — which covers both routes and
  any future `*Gdpr` controller, on all five hosts.

  **The interaction with T-0457 is the part worth flagging.** T-0457 adds contact-identity redaction,
  which frees window in the export body. Had the two landed separately, redacting the profile block on an
  **unsuppressed** admin export would have pulled the payout block *further into* the 500-byte window.
  They were done together for that reason.

  **A second finding the guard produced:** Cleansia's own `CompanyInfoDetailDto.{Iban,BankAccountNumber,
  Swift}` and the create/update commands behind them. Not a cleaner's payout destination and not
  confidential (it prints on every customer receipt), but suppressed anyway via `/admincompany/` — the
  alternative was an exception entry reading "this bank account is fine to log", which is the sentence
  that gets copied onto the next one.

  **AC2 — cross-noted** on T-0457 (implemented jointly; the `gdpr/` fix is recorded in both) and on
  T-0470 (see its status log: the payout family is now covered by a derived guard, so it is out of that
  ticket's residue).

  **AC3 — nothing to rule on. Confirmed closed at HEAD:** `Features/Employees/DTOs/EmployeeListItem.cs`
  and `UpdateEmployee.Command` carry no `Iban` (grep clean). The admin paged-list exposure the ticket was
  written around no longer exists.

  **AC4 — GDPR export confirmed correct.** `GdprExportService:43-53` reads the real
  `EmployeePayoutDetails` row through `IEmployeePayoutDetailsRepository.GetByEmployeeIdAsync` and exports
  every column including `LastRevealedAt`/`RevealCount`. It is a subject-access right, not a leak; the
  route is now suppressed on **both** the self and admin paths.

  **AC5 — the audit guard is generalised, plus an inventory of the other sinks.**
  `EmployeeUserAuditCoverageTests:301` asserted one IBAN against the handlers somebody thought to drive.
  New `src/Cleansia.Tests/Features/Auditing/AuditSnapshotSensitiveMemberGuardTests.cs` walks the whole
  `*Snapshot` family (13 types) and fails on any payout identifier or contact-identity member —
  `AuditContext.RecordChange` takes `object` and serializes it whole, so the discipline has to live on the
  types. The rest of the inventory, all **runs**:
  - **Logs** — `grep -rn "Iban\|AccountNumber\|HolderName\|Swift" | grep -i "log\|_logger\|Exception("`
    returns nothing. `UpdateBankDetails`' duplicate-destination warning logs `{EmployeeId}` only.
  - **Audit rows** — `AuditEntryFactory` never serializes the request; `BeforeJson`/`AfterJson` come only
    from a handler's `RecordChange`. `RevealEmployeePayoutDetails` pushes an ids-only `RevealSnapshot`;
    `UpdateBankDetails` pushes nothing. `AuditResourceResolver` reads `*Id` string properties only.
  - **Outbox / queue / email** — no message type in `Cleansia.Core.Queue.Abstractions` or
    `Cleansia.Functions` mentions a payout field.
  - **Validation echo** — `ValidationPipelineBehavior` maps to `new Error(ErrorCode, ErrorMessage)` and
    every payout rule uses a `BusinessErrorMessage` dot-key. `grep -rn "PropertyValue"` (FluentValidation's
    value placeholder) returns **nothing** repo-wide, so no failure path echoes the value back.
  - **Documents** — the payout invoice PDF prints it, which is ADR-0034's intended consumer.

  **What replaces the hand-written route list.** `RequestLogPayoutPathSuppressionTests` gained
  `EveryRouteCarryingAPayoutIdentifier_IsSuppressedOnEveryHost`, derived from the wire surface: a new
  route returning a payout identifier joins the rule by existing. That is what found the AdminGdpr hole;
  the `[InlineData]` list — written when payout shipped — could not have, and it is kept only for
  legibility. ADR-0034 D6's plaintext trade holds only while the value stays out of every other sink, and
  a log is a sink with different retention, different access control, and — once `Q-OBS-01` ships Sentry
  log context — a different company.

  **AC8 — `Cleansia.Tests` 3017/3017, `Cleansia.IntegrationTests` 132/132, `Cleansia.HostTests` 120/120**,
  all local. Not an empty diff after all.

- 2026-08-05 — **`ready` → `done` (PM reconciliation pass 4).** **Verified at HEAD.** The sweep ran and
  the live hole it found was **not** the one the ticket predicted: `IsSensitivePath`'s `Contains("/gdpr")`
  never matched `/api/v1/AdminGdpr/export/{userId}`, because no slash precedes "gdpr" inside "AdminGdpr" —
  the same shape as `/auth/` missing `/api/AdminAuth/…`. All five hosts now test `Contains("gdpr/")`, which
  matches both routes. `Cleansia.Tests/Logging/RequestLogPayoutPathSuppressionTests.cs:19-22` records the
  finding and `EveryRouteCarryingAPayoutIdentifier_IsSuppressedOnEveryHost` **derives** the route list from
  the wire surface, so a new payout route joins the rule by existing — that derived guard is what found the
  hole; the hand-written `[InlineData]` list could not have. AC3 also landed: `EmployeeListItem` no longer
  carries `Iban` (grep for `Iban` under `Features/Employees/` returns only `UpdateBankDetails` and
  `PayoutDetailsDtos`). Shipped in `b9753e85`.

## Review
