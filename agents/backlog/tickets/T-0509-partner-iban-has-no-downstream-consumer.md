---
id: T-0509
title: The cleaner's bank account is a T-0470-class value — sweep its exposure in logs, exports and list DTOs
status: ready
size: S
owner: security
created: 2026-08-02
updated: 2026-08-02
depends_on: []
blocks: []
stories: []
adrs: []
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

**Read first:** `agents/knowledge/security-rules.md` (S1–S10), sprint-14's **T-0457** and **T-0470**,
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

## Review
