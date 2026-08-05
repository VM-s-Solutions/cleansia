---
id: T-0505
title: Partner onboarding validates the email, discards it, and shows a success toast — and no email-change path exists anywhere
status: draft
size: M
owner: backend
created: 2026-08-02
updated: 2026-08-02
depends_on: [T-0504]
blocks: []
stories: []
adrs: []
layers: [backend, frontend, android, ios]
security_touching: true
manual_steps: []
sprint: 15
---

## Context

**Source: the partner-onboarding investigation (2026-08-02).** *"Email is validated then silently
discarded with a success toast; there is no email-change path anywhere, including for admins."*

**Status: RELAYED, NOT re-verified by the PM.** AC1 re-establishes it.

### Why this is the worst-shaped defect in the onboarding set

Three failures compound, and each one alone would be a ticket:

1. **The value is validated.** So the code knows what a valid email is and deliberately checks it.
2. **The value is discarded.** The validation's only effect is to make the user retype until the
   input passes, after which nothing happens to it.
3. **The user is told it worked.** A success toast on a discarded write is the single most expensive
   failure mode in a UI, because it **stops the user from retrying** and stops anyone from noticing.
   The cleaner believes their email is updated. Every downstream email goes to the old address, or to
   none.

**And there is no recovery path.** Not in the partner web app, not in either mobile app, **not in
admin**. So when a cleaner changes their email address in real life, there is currently **no
mechanism in this platform to reflect it** — not self-service, not by support intervention. The only
route is a direct database edit.

**The same class already has an open blocking question on the adjacent surface:** `Q-PROFILE-01`
(`blocking: yes`) — `UpdateCurrentUser` requires a client-supplied `Id` the customer **web** app
cannot obtain, so **every customer-web profile save 400s** and has since 2026-05-16. **Two profile
write paths, both broken, neither noticed, because neither surfaces its failure.** The story
(T-0504) should check whether one fix serves both; this ticket must not assume it does.

### The dependency that determines whether this is `M` or an epic

**T-0504 AC4.** If the email is an **identity/login credential**, then changing it is an auth flow —
verification of the new address, handling the old one, session/token consequences, and the
multi-tenant `tenant_id` claim. **That is not an `M`.** If it is a contact field, `M` is right.
**This ticket must not be dispatched before that ruling**, and AC2 forces a re-file if the answer is
"identity".

## Acceptance criteria

- [ ] **AC1 — RE-ESTABLISH all three failures at file:line before fixing any.** Where is the email
      validated, where is it dropped, and where does the success toast come from? **And confirm the
      negative**: search the partner web app, both mobile apps **and the admin app** for any
      email-change path. Evidence: the four file:line answers plus the negative-search commands.
- [ ] **AC2 — the scope matches T-0504 AC4's ruling.** If "identity credential", **STOP and re-file**
      as an auth epic with the story's design. Do not grow this ticket in place. Evidence: the ruling
      reference, or the re-file.
- [ ] **AC3 — the email is persisted, end to end, and proved by round trip.** Set it, restart the
      client cold, read it back. Evidence: the round-trip recording per client that has the field.
- [ ] **AC4 — the success toast cannot fire on a failed or no-op write. This is the most important
      AC in the ticket.** A rejected or dropped write surfaces the actual error. **Add the assertion
      that would have caught the original defect** — i.e. a test that fails if the handler returns
      success without persisting. Evidence: the test, proved red against the pre-fix code.
- [ ] **AC5 — an email-change path exists for at least one actor, and the others are stated.** Per
      T-0504 decision 2 (partner, admin, or both). Whichever is **not** built is named as a follow-up
      ticket, not silently omitted. Evidence: the built path plus the named gap.
- [ ] **AC6 — uniqueness and collision are handled.** What happens when a partner sets an email
      another account already uses? **If the email is a login credential this is an account-takeover
      vector**, not a validation nicety. State the behaviour and test it. Evidence: the test.
- [ ] **AC7 — the change is auditable.** ADR-0012's admin-action audit log exists. An email change —
      especially an admin-initiated one — should be in it. State whether it is, per the ADR's
      PII-minimized posture (`Q-AUDIT-01`'s answer: ids and changed fields, never raw subject PII).
      Evidence: the audit entry or the argued exclusion.
- [ ] **AC8 — the error key reaches every client.** New `BusinessErrorMessage` keys get `errors.*`
      translations in all three web apps × 5 locales and are mapped on mobile. **Different clients use
      different key namespaces, and NSwag throws ProblemDetails bare** — reading `.result` alone
      resolves nothing. Evidence: the parity spec plus the mobile mapping.
- [ ] **AC9 — DTO changes are flagged, not performed.** If the contract changes, this carries
      **`manual_steps: nswag-regen`** + `mobile-spec-redump` — the **owner's** bundle. The PM holds
      dependent client work until the owner confirms. **Sprint-14 has a demonstrated failure history
      immediately after a regen** (T-0438, PR #166). Evidence: the flag on the ticket before the
      client legs start.
- [ ] **AC10 (Gate 0.5)** — `Cleansia.Tests` / `Cleansia.IntegrationTests` / `Cleansia.HostTests`
      **locally**, baselines **2295 / 108 / 75**; plus the client suites for whichever clients change.

## Out of scope

- **`Q-PROFILE-01`'s customer-web `Id` defect.** Adjacent, already escalated, `blocking: yes`, needs
  a **backend shape decision** (a/b/c). **Named here so a reviewer does not read its absence as an
  oversight.** If T-0504 finds one fix serves both, that is a scope amendment recorded explicitly —
  not a silent widening.
- **Language** — T-0506. **Consent** — T-0507. Same flow, different fields, different tickets.
- **Rewriting the onboarding command** — T-0510.
- **Building the actor path AC5 does not build.** Named, not built.

## Implementation notes

**No panel of its own — T-0504 is the panel**, and its AC4 is this ticket's sizing gate.

**Contract before consumers** (`routing.md` rule 1): `backend` locks the shape; **only then** do
`frontend` / `android` / `ios` start — and only after the owner's regen if AC9 fires.

**Gate 6.5 applies** if AC2 lands on "identity credential" — auth decisions are an enumerated class.

**⚠️ `security_touching: true`** — the security gate is mandatory. AC6's collision case is the reason.

## Status log
- 2026-08-02 — **draft (created by pm from the partner-onboarding investigation).** Finding marked
  RELAYED; AC1 re-establishes it including the **negative search across all four clients**.
  **`depends_on: [T-0504]` is a sizing gate, not a formality** — if the email is an identity
  credential this is an auth epic and AC2 forces a re-file rather than letting an `M` become one in
  flight. The **success toast on a discarded write (AC4)** is called out as the most important half:
  it is what stopped anyone noticing for as long as this has been shipping.
- 2026-08-05 — **AC1 re-established by backend: the premise is NO LONGER TRUE, and AC2 fires.** The
  validate-then-discard defect was fixed in `2012b014` (#189, merged the same day this was filed) by
  deleting the field rather than persisting it. What is left of this ticket is AC5, and the AC2 gate
  now has its answer: **the email IS an identity credential**, so AC5 must be re-filed as an auth
  epic, not built here. See `## Review`.

## Review

### AC1 — re-established. Two of the three failures no longer exist; the negative still holds.

| Claim | Verdict | Evidence |
|---|---|---|
| Email is **validated** | **GONE** | `2012b014` deleted `RuleFor(c => c.Email).ValidateUserEmail()` from `UpdateEmployee.Validator` and `AddEmailRules(c => c.Email)` from `UpdatePersonalInfo.Validator` |
| Email is **discarded** | **GONE — the field no longer exists** | `UpdateEmployee.Command` (`UpdateEmployee.cs:203-227`) and `UpdatePersonalInfo.Command` (`UpdatePersonalInfo.cs:52-60`) expose no `Email` member; pinned by `Cleansia.Tests/Features/Employees/EmailIsNotAnOnboardingInputTests.cs` (reflection, both types) |
| **Success toast** on a discarded write | **GONE, and the client was always innocent** | `profile.facade.ts:209-219` fires the toast inside `next:` guarded by `if (result)` — i.e. only when the API returned a body. It said "saved" because the server said "saved". Removing the field removed the lie at its source |
| **No email-change path anywhere** | **STILL TRUE** | `User.Email` has exactly four writers: the three factories (`User.cs:134`, `:149`, `:161`) and `Anonymize()` (`:417`). No `UpdateEmail`/`ChangeEmail` command exists on partner, mobile, or admin. `AdminUpdateEmployee` and the `AdminUsers` feature carry `Email` on **create** only |

The web form field is gone too — `profile.facade.ts:52-54` keeps the address as a display-only signal
with the reason written next to it.

### AC2 — the sizing gate: the email IS an identity credential. STOP and re-file.

`IUserSessionProvider.GetUserEmail()` returns the JWT `ClaimTypes.Email`
(`Infra.Database/UserSessionProvider.cs:24-27`), and **the partner surface resolves its subject row
from it, not from the user id**: `employeeRepository.GetByUserEmailAsync(userSessionProvider.GetUserEmail())`
appears in **14** feature files under `Features/Employees/` and `Features/EmployeeDocuments/`, plus
`Cleansia.Config/Filters/RequireCompleteProfileAttribute.cs:18-25` — the completeness gate on the whole
partner surface. Every login path also looks the user up by email
(`Login.cs:48`, `PartnerLogin.cs:49`, `MobilePartnerLogin.cs:62`, `AdminLogin.cs:53`).

So a live email change with the token unchanged 403s the cleaner off their own account until they
re-authenticate. That is an auth flow — verification of the new address, token re-issue, session
handling — **not an `M` profile edit**. Per AC2 this ticket does not grow in place: AC5 is re-filed.

**Recommended disposition:** close T-0505 (AC1–AC4 satisfied by `2012b014` + the reflection pin);
re-file AC5/AC6/AC7 as an auth epic carrying the 14-call-site finding as its premise.

### Not done here, and why
AC3 (round trip) and AC8/AC9 (error keys, regen) are **moot** — there is no email field on the wire
to round-trip and no contract change. AC5/AC6/AC7 move to the re-file.
