---
id: T-0507
title: LEGAL — partner consent is required on web, never persisted, and never asked on mobile
status: draft
size: M
owner: db
created: 2026-08-02
updated: 2026-08-06
depends_on: [T-0504]
blocks: []
stories: []
adrs: [0012, 0041]
layers: [android, ios]
security_touching: true
manual_steps: []
sprint: 15
---

## Context

**Source: the partner-onboarding investigation (2026-08-02).** *"Consent is required on web, never
persisted, never asked on mobile."*

**Status: RELAYED, NOT re-verified by the PM.** AC1 re-establishes it.

### Why this is filed as a legal exposure and not as a defect

**The platform blocks a user from proceeding until they consent, and then keeps no record that they
did.** That is the worst of both worlds:

- **We cannot prove consent.** Under GDPR the controller must be able to **demonstrate** that the
  data subject consented (Art. 7(1)). A checkbox that gates a form and writes nothing demonstrates
  nothing. If a partner disputes it, the platform's evidence is *"our UI would not have let them
  continue"* — which is an argument about our code, not a record about them.
- **A second cohort never consented at all.** Every cleaner who onboarded through **mobile** was never
  asked. So the population splits into "asked, not recorded" and "not asked" — and **the platform
  cannot currently tell which of its partners is in which group**, because neither leaves a trace.

**This is not post-demo polish.** It is a compliance gap that grows monotonically with every new
partner, and it cannot be retro-fixed for the people already onboarded — you cannot generate a
consent record for a consent that was never captured. **Every day this stands, the un-provable
cohort gets larger and permanently so.**

### What makes this a schema change, and therefore owner-gated

T-0504 AC5 specifies the record's shape, and it is deliberately **not a boolean**:

| Field | Why |
|---|---|
| **what** was consented to | terms, privacy policy, marketing — these are separate consents with separate legal bases |
| **which version** | terms change; a consent to v1 does not cover v3 |
| **when** (UTC) | the demonstrable timestamp |
| **from which client / context** | web, Android, iOS — provenance |
| **how it is withdrawn** | GDPR Art. 7(3): withdrawal must be as easy as giving it |

**That is a new entity or a new set of columns → an EF migration → `manual_steps: ef-migration`,
which is owner-only.** The PM flags it and **holds** the dependent client work until the owner
confirms.

## Acceptance criteria

- [ ] **AC1 — RE-ESTABLISH all three claims at file:line.** Where web requires it; where it is
      dropped; and the negative search proving neither mobile app asks. Evidence: three answers plus
      the search commands.
- [ ] **AC2 — the record's shape matches T-0504 AC5 exactly**, including the version field.
      **A boolean column does not pass this AC.** Evidence: the entity/columns plus the mapping to
      AC5's list.
- [ ] **AC3 — the migration is WRITTEN AS A SPEC, and the migration itself is FLAGGED, not run.**
      **No agent runs `dotnet ef migrations add` or `database update`** (`CLAUDE.md`). The ticket
      carries the exact schema the owner must migrate. Evidence: the spec, and the owner's
      confirmation recorded before any dependent work starts.
- [ ] **AC4 — consent is captured on ALL THREE onboarding clients**, or the ones it is not is named
      with a date. Partner web, partner Android, partner iOS. Evidence: three screenshots plus three
      round trips.
- [ ] **AC5 — the existing partner population is ADDRESSED, and "we cannot fix it" is written down if
      that is the answer.** State how many partners exist with no consent record, what the platform
      does about them (re-consent prompt on next login? a flag for legal? nothing?), and — **most
      importantly — state plainly that consents given before this ticket cannot be reconstructed.**
      Evidence: the count, the ruling, and the sentence.
- [ ] **AC6 — withdrawal exists or is named as a follow-up with a date.** GDPR Art. 7(3). If it is
      not built here, it is a **named ticket**, not an omission. Evidence: the path, or the named
      ticket.
- [ ] **AC7 — the consent record is auditable and PII-minimized.** ADR-0012 governs, and
      `Q-AUDIT-01`'s answer set the posture: **ids and changed fields, never raw subject PII**, and
      a GDPR-delete audit legitimately survives the subject's erasure as a legal-basis exception.
      **A consent record is exactly that class** — it must survive an erasure request, and the story
      must say so. Evidence: the retention statement checked against ADR-0012.
- [ ] **AC8 — the consent text itself is versioned and its source is named.** A version field with
      nothing to point at is decoration. Where does the terms version come from, and what happens
      when it changes? Evidence: the mechanism.
- [ ] **AC9 — a test that goes red against the pre-fix code (Gate 0.5 leg 1)**: an assertion that a
      completed onboarding produces a consent row. It fails today by construction. Evidence: the red
      run, then green.
- [ ] **AC10 — the SECURITY gate runs.** `security_touching: true`. The gate checks the record's PII
      posture and that consent state cannot be forged or set by a client that should not.
- [ ] **AC11 (Gate 0.5)** — `Cleansia.Tests` / `Cleansia.IntegrationTests` / `Cleansia.HostTests`
      **locally**, baselines **2295 / 108 / 75**; plus the client suites.

## Out of scope

- **The customer apps' consent.** Partner-scoped. **⚠️ If the customer flow has the same defect, that
  is a bigger exposure by population — record it in `## Review` and file it separately, immediately.
  Do not widen this ticket and do not leave it unfiled.**
- **Writing the terms or the privacy policy.** Legal content is the owner's.
- **Running the migration.** AC3.
- **Email** — T-0505. **Language** — T-0506.

## Implementation notes

**No panel of its own — T-0504 is the panel**, and AC5 of that story is this ticket's schema.

**`db` owns this ticket** (`routing.md`: new entity/column/migration → `db`), then `backend`, then the
three clients. **Contract before consumers**, and **manual steps block**: the client legs **hold**
until the owner confirms the migration.

**⚠️ This ticket has the longest owner-gated tail in the sprint** — an `ef-migration` **and**
possibly an `nswag-regen` if the DTO changes. **Start the schema spec early even if the rest waits**,
because the wait is the schedule.

**Read first:** `agents/knowledge/security-rules.md`, **ADR-0012** + `Q-AUDIT-01`'s answer in
`questions/answered.md`, `Cleansia.Infra.Database/EntityConfigurations/`, and the multi-tenancy
`TenantId` convention (a consent record is tenant-scoped).

## Status log
- 2026-08-02 — **draft (created by pm from the partner-onboarding investigation).** Finding marked
  RELAYED; AC1 re-establishes it. **Filed as a legal exposure with `manual_steps: ef-migration`.**
  The framing the PM added and the investigation did not: the population **splits into "asked, not
  recorded" and "never asked"**, the platform **cannot tell which partner is in which**, and
  **consents already given cannot be reconstructed** — so the un-provable cohort grows monotonically
  and permanently. That is why AC5 forces the sentence to be written rather than the problem quietly
  deferred.
- 2026-08-06 — **Gate 0 by `backend`: two of the title's three claims are FALSE.** The consent **is**
  persisted on web and **is** asked on both mobile apps. No `ef-migration` is needed — the home
  already exists and is already migrated. `manual_steps: [ef-migration]` is **withdrawn**; see below.
  Backend leg **closed** with a persistence-boundary test; what survives is a **client** gap and one
  **schema** limb that belongs to ADR-0041's live panel.

## Review

### AC1 — all three claims re-established at file:line

| Claim | Verdict | Evidence |
|---|---|---|
| "required on web" | **TRUE** | `UpdateEmployee.Validator` `Features/Employees/UpdateEmployee.cs:132-134` — `RuleFor(c => c.Consent).Equal(true)`; the form control is `Validators.requiredTrue` (`profile.models.ts:123`) |
| "never persisted" | **FALSE** | `UpdateEmployee.Handler:263` — `await consentService.TryGrantAsync(employee.UserId, ConsentType.DataProcessing, ct)`. Landed in `2012b014` (PR #189), already pinned by `Cleansia.Tests/Features/Employees/OnboardingConsentTests.cs` (4 tests) |
| "never asked on mobile" | **FALSE as written** | Both partner mobile apps ask — `RegisterScreen.kt:190` and `RegisterView.swift:150`, both rendering the shared `CleansiaConsentCheckbox` and both **gating the submit button** (`RegisterViewModel.kt:85`, `form.isValid`) |

**The true, narrower defect the ticket was reaching for.** The mobile tick is asked at **registration**
and **never leaves the device**: `RegisterEmployee.Command` (`Features/Auth/RegisterEmployee.cs:52-58`)
has no consent member, and `RegisterViewModel.kt:98-104` passes only email/password/names/language.
Mobile **onboarding** — the six section PUTs — neither asks nor grants. Negative searches, both zero:

```
rg -n 'onsent' cleansia_android/partner-app/src/main/java/cz/cleansia/partner/features/profile/ \
               cleansia_ios/CleansiaPartner/Sources/Features/Profile/          # 0
rg -n 'rantConsent' cleansia_android/{partner,customer}-app/src \
                    cleansia_ios/Cleansia{Partner,Customer}/Sources           # 0
```

So the population splits as: **web cleaner → has a `UserConsent(DataProcessing)` row; mobile-only
cleaner → has none, having ticked a box.** That is the manufactured-evidence half of the ticket's
framing, and it is real.

### AC2/AC3 — the record's home ALREADY EXISTS. No migration. `manual_steps: ef-migration` withdrawn.

`UserConsent : Auditable, ITenantEntity` (`Core.Domain/Users/UserConsent.cs`), configured at
`UserConsentEntityConfiguration.cs` and **already in the one committed migration**
(`20260723182623_Initial.cs:1228`, indexes at `:3438`/`:3443`). Against T-0504 AC5's field list:

| AC5 requires | `UserConsent` | |
|---|---|---|
| **what** was consented to | `ConsentType` (4 values) | ✅ |
| **when** (UTC) | `GrantedAt` | ✅ |
| **from which client / context** | `IpAddress` + `UserAgent`, read **server-side** from `IRequestMetadataProvider` (`ConsentService.cs:16-19`) so the client cannot forge them — satisfies **AC10** | ✅ |
| **how it is withdrawn** | `WithdrawnAt` + `WithdrawConsent.Command`, live on four hosts — satisfies **AC6** | ✅ |
| **which version** | — | ❌ **the one unmet limb** |

**AC7 is satisfied and was worth checking:** erasure does **not** delete the row —
`GdprDeletionService.cs:206-208` calls `Withdraw()` on granted consents, which sets `IsGranted=false`
and `WithdrawnAt`, leaving `GrantedAt` intact. The proof of a consent given survives the subject's
erasure, which is what AC7 demands.

### The version limb (AC2 last row, AC8) is ADR-0041's, and this ticket STOPS at that boundary

ADR-0041 rev 3 **F1** is verbatim this finding, and it extends it beyond self-billing: *"No version
column; the unique index makes it one mutable row per type; and `Regrant` overwrites the grant
timestamp and the request metadata … **and the table does not meet it for any of its four current
types either**."* Adding a version column and a history shape to `UserConsent` is therefore a
decision that is **mid-panel** (ADR-0041 is `proposed`, rev 3, awaiting a third panel; `Not buildable`,
`no EF migration may be created`). **Not done here, deliberately.** The precise boundary:

- **This ticket owns** — that a `DataProcessing` grant exists, is unforgeable, is durable, survives
  erasure, and is capturable from every client. All backend-side ✅ today.
- **ADR-0041 owns** — whether a consent record is a *mutable row per type* or an *append-only
  versioned log*. That reshapes `UserConsent` for all four existing types; it is not a partner-consent
  question and must not be pre-empted by this ticket.
- **No overlap in the enum.** ADR-0041 F2 explicitly refuses a fifth `ConsentType` (a commercial B2B
  term must not be withdrawable through the GDPR `WithdrawConsent` endpoint). This ticket adds no enum
  value. The two are cleanly separable **today**; they collide only on the version limb above.

### AC4 — capture on all three clients: the backend leg needs NOTHING; the gap is client-side

`GrantConsent` is already live on the partner mobile host —
`Web.Mobile.Partner/Controllers/GdprController.cs:45-49`, `POST /api/gdpr/consents`,
`[Permission(Policy.CanGrantConsent)]` which maps to `PhysicalPolicy.Authenticated` (**all roles**,
`PolicyBuilder.cs:219`), so a cleaner can reach it. The generated clients already carry
`GrantConsentCommand` + `ConsentType` in both mobile trees. **Neither a backend change nor an
`nswag-regen`/mobile-spec-redump is required for mobile to close this** — the apps must call
`grantConsent(ConsentType.dataProcessing)` when their existing checkbox is ticked. Same shape as
T-0506 (language): wired server-side, unreached by the client.

> ⚠️ **One thing the mobile lane must be told:** `GrantConsent.Handler:38-41` returns **failure**
> (`ConsentAlreadyGranted`) when the consent is already held. It is an explicit-user-action endpoint,
> not an idempotent upsert. A re-install / re-register that calls it again gets a 400, and the client
> must treat `consent.already_granted` as success rather than surfacing it. Do **not** "fix" this by
> softening the endpoint — the GDPR settings screen relies on that answer.

### AC5 — the existing population

Moot by owner ruling (relayed 2026-08-05, recorded in ADR-0041 §Owner rulings 1): *"the database is
being dropped; don't be bothered with existing cleaners."* **There is no cohort to reconstruct**, so
AC5's required sentence is discharged by the ruling rather than by a count. The sentence that still
holds and should not be lost: *a consent that was never captured cannot be generated later* — which is
why the mobile client leg should not wait behind the ADR-0041 panel.

### AC9/AC11 — Gate 0.5

Added `Cleansia.IntegrationTests/Features/Employees/OnboardingConsentPersistenceTests.cs` (3 tests,
real Postgres). AC9's "red against the pre-fix code" is **not available** — the fix shipped in
`2012b014`; the equivalent discipline (`testing.md:60-65`) is *revert the fix, prove red, restore*,
done four times below. The new tests assert at the **persistence boundary** — a fresh scope and fresh
`DbContext` after the act — because the existing unit tests assert on a Moq callback on
`IUserConsentRepository.Add` and are green over a handler whose row is never committed (**M4**).

| # | Mutation | Result | Killed |
|---|---|---|---|
| M1 | `UpdateEmployee.cs:263` grant call → `await Task.CompletedTask` | **RED** 2/3 | `…Writes_A_Granted_DataProcessing_Consent_Row`, `…Does_Not_Move_The_Grant_Timestamp` |
| M2 | `UpdateEmployee.cs:132-134` `.Equal(true)` → `.NotNull()` | **RED** 1/3 | `Onboarding_Refused_For_Missing_Consent_Writes_No_Consent_Row` |
| M3 | `ConsentService.cs` idempotency guard dropped | **RED** 1/3, Postgres `23505` on `IX_UserConsents_UserId_ConsentType` | `…Keeps_One_Row…` |
| M4 | `UnitOfWorkPipelineBehavior.IsNotCommand` → never commits | **RED** 2/3 **while `OnboardingConsentTests` (unit) stays 4/4 GREEN** | the anti-vacuity proof: the row is `Add`ed and never persisted, and only the boundary test sees it |

All four restored **byte-exact, verified by `shasum -a 256 -c`** after each. Suites, all exit 0:
**unit 3229 → 3229**, **integration 144 → 147**, **host 135 → 135**.

### Out-of-scope findings, for the PM to file (recorded, not fixed)

1. **⚠️ The customer signup has the same defect and no downstream catch — the bigger exposure by
   population, as the ticket's own `## Out of scope` predicted.** All three customer clients ask and
   drop: `SignUpScreen.kt:224`, `SignUpView.swift:168`, and the Angular
   `libs/cleansia-customer-features/register/src/lib/register/register.facade.ts:220`
   (`Validators.requiredTrue`) whose `register(...)` call at `:125` sends email/password/names/referral
   and **no consent**. `Register.Command` (`Features/Auth/Register.cs:54`) has no consent member, and
   `GrantConsent`+`UpdateEmployee` are the **only** two `TryGrantAsync` callers in the tree — so a
   customer has **no** consent row until they visit the GDPR settings screen. Unlike a cleaner, a
   customer has no onboarding step to catch it. **Sized larger than this ticket.**
2. **The partner web register tick is also dropped** (`register.facade.ts:69`), harmlessly today
   because onboarding grants — but it means the *register-time* consent is unrecorded on every client
   of every app. Whether registration or onboarding is the legally correct capture point is an
   owner/legal question, not an engineering one.
