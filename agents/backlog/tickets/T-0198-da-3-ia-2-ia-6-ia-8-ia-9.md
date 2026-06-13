---
id: T-0198
title: De-triplicate Dispute/SavedAddress/Auth controllers + login/forgot-password facades; unify email/password rules
status: ready            # draft | ready | in_progress | in_review | qa | done | blocked
size: M                  # S | M | L  (L must be split before going ready)
owner: —                 # the agent currently working it (pm sets this)
created: 2026-06-01
updated: 2026-06-01
depends_on: []           # ticket ids that must be done first
blocks: []               # tickets waiting on this one
stories: []              # US-<persona>-NNNN ids this satisfies
adrs: []                 # ADR numbers in force
layers: [backend, frontend]
security_touching: false # true → Security gate mandatory
manual_steps: []         # owner-only: ef-migration, nswag-regen, db-seed, xcode-project, docs-build
sprint: 3
source: theme 6 (cross-app triplication); findings DA-3/IA-2/6/8/9
---

## Context

Wave-3 consistency cleanup against `consistency.md` theme 6 (the same operation written N ways across
hosts/apps). Five audit findings describe one tangled cluster of cross-app/cross-host copy-paste in
the auth + dispute + saved-address surfaces:

- **DA-3** — `Web.Customer/Controllers/DisputeController.cs` and
  `Web.Mobile.Customer/Controllers/DisputeController.cs` are **byte-for-byte identical** (95 lines
  each); `Web.Partner/Controllers/DisputeController.cs` is the same body plus 2 extra (admin-only)
  actions. `SavedAddressController.cs` is identically triplicated (Customer + Mobile.Customer copies
  are identical). The copies already **drift** (Partner carries actions the others don't), so a fix
  to one (e.g. the inline `"File is required."` string, DA-7) silently misses the others.
- **IA-2** — `Features/Auth/Login.cs`, `PartnerLogin.cs`, `AdminLogin.cs` share a byte-for-byte
  identical ~45-line validator and the same handler skeleton; the only real difference is a single
  `if (user.Profile != X)` profile gate. The five host AuthControllers
  (`Web.Customer`, `Web.Partner`, `Web.Admin/AdminAuthController`, `Web.Mobile.Customer`,
  `Web.Mobile.Partner`) copy-paste the `HandleTokenIssuingResult` helper and the cookie-first
  Refresh/Logout bodies verbatim. *(IA-2's cross-host Login.Command unification is the textbook
  "Architect-owned because it spans the host boundary" call — see Implementation notes / Out of
  scope; this ticket does the safe consolidation, not a wire-shape redesign.)*
- **IA-6** — the **same** email-max-length and password rules are encoded in ~5 places with **3
  different password regexes and 2 different email lengths**: `BaseAuthValidator.AddEmailRules`
  `MaximumLength(50)` vs `CreateAdminUser` `MaximumLength(150)`; backend password
  `^(?=.*[a-zA-Z])(?=.*\d).{8,}$` (BaseAuthValidator + `ChangePassword.cs`) vs `CreateAdminUser`
  min-length-8-only (materially weaker) vs customer FE `forgot-password.facade.ts`
  `^(?=.*[a-zA-Z])(?=.*\d).{8,}$` vs partner FE `^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).{8,}$` (stricter —
  the partner FE rejects passwords the backend accepts).
- **IA-8** — `cleansia-customer-features/forgot-password/.../forgot-password.facade.ts` and
  `cleansia-partner-features/.../forgot-password.facade.ts` are ~95% identical; both **swallow API
  errors** (no `error` handler → C4 violation), skip the C3 pipe, use raw `new FormGroup/FormControl`
  (D2), use mutable public booleans instead of signals (C2/D1), implement the resend cooldown two
  ways with inline magic numbers (`30`, `30_000`), and the customer facade imports its commands from
  `@cleansia/partner-services` (wrong client package).
- **IA-9** — `cleansia-partner-features/login/.../login.facade.ts` subscribe has **no `error`
  handler** (a failed partner login shows the user nothing — a real bug), the customer
  `login.facade.ts` imports `JwtTokenResponse` from `@cleansia/partner-services` (wrong package), and
  both use raw `new FormControl/FormGroup` (D2) and skip the C3 pipe.

This is a **refactor**: behavior must stay identical (except the two named functional bugs that are
in-scope to fix — see AC4/AC5). The win is one canonical home per operation so the next auth/dispute
fix lands once, not five times.

## Acceptance criteria

> Refactor pattern (Wave-3 default): **characterization test first** (pins current behavior), then the
> cleanup, behavior identical, smell removed, `check-consistency.mjs` clean for the touched area.

- [ ] **AC1 (characterization-first)** — Given the current Dispute/SavedAddress controllers, the three
  Login handlers/validators, and the login/forgot-password facades, When the change starts, Then
  characterization tests pinning the *current* observable behavior exist and pass **before** any
  refactor: backend integration tests for the dispute + saved-address routes on each host (status +
  body, incl. the auth posture each host currently has) and for `Login`/`PartnerLogin`/`AdminLogin`
  (success + the profile-gate rejection `Error.Code` each currently returns); frontend Jest facade
  tests for the four login/forgot-password facades (state transitions + the three data states). The
  status log records "red → green" and the tests appear before the refactor in the diff.

- [ ] **AC2 (DA-3 controller de-triplication)** — Given the duplicated Dispute and SavedAddress
  controllers, When refactored to a shared base/partial controller (or a thin shared controller in a
  common Web library) with each host declaring only its host-specific actions + attributes, Then the
  Customer and Mobile.Customer copies are no longer independent byte-for-byte duplicates, the
  Partner-only dispute actions remain Partner-only, and **every route on every host returns the
  identical status + body it returned before** (AC1 characterization tests stay green). No route
  path, verb, request DTO, or response shape changes.

- [ ] **AC3 (IA-2 login/auth consolidation, no wire-shape change)** — Given the three near-identical
  login validators/handlers and the verbatim `HandleTokenIssuingResult` + Refresh/Logout cookie
  bodies, When the shared validator is composed once (shared rule extension per B3) and the
  token-issuing/cookie helper is extracted to one shared base/helper consumed by all five hosts, Then
  there is one copy of the validator rules and one copy of the helper, the per-host profile gate is
  preserved exactly, and the login/refresh/logout routes return identical results (AC1 stays green).
  *(A single unified `Login.Command` carrying an allowed-profiles set is **architect-gated and out of
  scope here** — see Out of scope.)*

- [ ] **AC4 (IA-6 single source of truth for email/password rules)** — Given email-length and
  password rules duplicated in ~5 places with divergent regexes/lengths, When a single backend
  password-rule extension (composed via `.SetValidator`/a rule extension, B3) and a single email-rule
  with one chosen `MaximumLength` are the only definitions, and a single exported FE regex/validator
  constant is consumed by both customer and partner apps, Then: backend password complexity is
  identical everywhere (admin aligned to the same complexity as customer/partner — closing the weak
  admin-password divergence), the FE regex matches the backend regex exactly (the partner FE no longer
  rejects a backend-valid password), and adding a future rule means editing **one** file per side. A
  unit test asserts the shared validator fires the expected `BusinessErrorMessage` for an invalid
  password and passes a valid one.

- [ ] **AC5 (IA-8/IA-9 facade unification + the two real bugs)** — Given the two forgot-password
  facades (~95% identical) and the two login facades, When unified onto one shared facade/base
  extending `UnsubscribeControlDirective` with signal state (C2/D1), the **C3 pipe**
  (`takeUntil(this.destroyed$) → catchError(() => of(null)) → finalize(...)`), `fb.nonNullable.group`
  (D2), and a single named cooldown constant, Then: (a) the **partner login facade now surfaces login
  errors via `SnackbarService.showApiError`** (was silently swallowed — IA-9 bug fixed); (b) **both
  forgot-password facades surface request/change-password errors via `showApiError`** (was swallowed —
  IA-8 bug fixed); (c) every command/DTO is imported from the **correct per-app client** (customer
  forgot-password + customer login no longer import from `@cleansia/partner-services`); and the Jest
  facade tests assert error→snackbar mapping and the three data states.

- [ ] **AC6 (smell removed + consistency clean)** — Given the touched backend and frontend areas,
  When the refactor is complete, Then `node agents/tools/check-consistency.mjs` reports **no new
  violations** for the touched files and clears the B1/B3 + C/D hits attributable to this cluster
  (the 11 B1/B3 hits called out in the IA slice for the auth area, and the C3/C4/D2 hits on the four
  facades), and no behavior-change is introduced beyond AC4/AC5's two named bug fixes.

## Out of scope

- **Unified `Login.Command` wire-shape redesign** (one command with an allowed-profiles / `LoginContext`
  enum replacing the three commands) — IA-2 flags this as **Architect-owned (spans the host boundary)**;
  it would change generated clients (`manual_step: nswag-regen`). This ticket does the *safe*
  consolidation (shared validator + shared token/cookie helper, profile gate preserved) and leaves the
  command-merge to a separate architect-gated ticket.
- **Mobile password-reset surface gap (IA-3)** — adding the missing `ChangePassword` route to the
  mobile auth controllers and harmonizing the reset verbs/controllers across hosts is a GAP needing a
  user story; tracked separately, not here.
- **Refresh-token profile pinning decision (IA-5)** — the partner/customer-refresh-not-profile-locked
  question is Security-Reviewer-owned; do not change the refresh guard posture in this ticket (keep it
  byte-identical per AC1/AC3).
- **Dispute/SavedAddress *handler* refactors** — DA-4 (AddDisputeMessage null-guard), DA-8
  (AddSavedAddress god-method + the triplicated `SavedAddressDto` projection), DA-5/DA-6/DA-7
  (customer disputes facade/table/i18n) are their own tickets; this one is the **controller** layer +
  the login/forgot facades only.
- **IA-1 / IA-7 / IA-12** (double-hash, Register/RegisterEmployee dup + defects, password-converter
  redesign) — separate tickets.
- No new endpoints, no DB changes, no migration, no nswag-regen (controller routes/DTOs are unchanged).

## Implementation notes

- **Canonical patterns (`consistency.md`):** backend validators → **B3** (`AbstractValidator<Command>`,
  compose shared rules via `.SetValidator`/a rule extension — *not* custom bases; `.Cascade(Stop)`;
  every rule → `.WithMessage(BusinessErrorMessage.X)`); failure construction → **B5**. The IA-6 single
  source of truth = one shared password-rule extension + one email-rule, exactly the B3 "compose
  shared rules" prescription. Frontend facades → **C1** (`UnsubscribeControlDirective`, provided on the
  component), **C2/D1** (signal state, `loading`/`saving`), **C3** (the exact pipe), **C4**
  (`SnackbarService` for errors — this is what the two swallowed-error bugs violate), **D2**
  (`fb.nonNullable.group` + `route.snapshot.data['mode']`), **D3** (`cleansia-*` + `ErrorPipe`).
  DA-3's controller de-dup is the `conventions.md` Duplication rule ("same operation written N ways").
- **TEST-FIRST (`testing.md`):** this is *changing existing untested code*, so the
  **characterization-test-first** loop applies (AC1): write a test that pins the **current** behavior,
  confirm it passes, then refactor on top with the test staying green — this stops a silent behavior
  break in auth (a money-/access-adjacent surface). The IA-6 shared password rule is **pure logic** →
  strict red-green for the validator unit test. Facade logic (state transitions, error→snackbar
  mapping, three data states) is tested first per the "test the facade/ViewModel" rule; the views are
  QA-verified against AC, not unit-tested for markup. Status log must show "red → green" and AC↔test
  mapping.
- **Serialization / collisions (TICKET-MAP):** the three login handlers, the five AuthControllers, the
  Dispute/SavedAddress controllers (×3), and the four facades are this ticket's exclusive surface — do
  not run concurrently with any other ticket touching those files. Note IA-2 touches auth registration
  conceptually, but the shared `AddCleansiaAuthorization` extraction across the 5 host
  `ServiceExtensions.cs` is **BSP-1**'s surface (ADR-0001 D4) — do **not** touch host auth
  registration here.
- **Serialization (no wire change):** because routes/verbs/DTOs and response shapes are unchanged,
  **no nswag-regen and no EF migration** — the controllers move into a shared base but keep identical
  attributes and signatures. If the reviewer finds any change that alters a generated client's
  surface, **stop and re-scope** (it would make nswag-regen a manual step and is out of scope here).
- **Sequence:** (1) characterization tests on all five surfaces (AC1) → (2) backend: shared
  dispute/saved-address base controller (AC2), then shared login validator + token/cookie helper
  (AC3), then shared email/password rule extension + align admin complexity (AC4) → (3) frontend:
  unified forgot-password facade then unified login facade, fixing the swallowed-error bugs + wrong
  client imports (AC5) → (4) `check-consistency.mjs` clean for the touched area (AC6). Spawn a reviewer
  per developer instance (backend dev ∥ reviewer; frontend dev ∥ reviewer).

## Status log
- 2026-06-01 — draft (created by pm)
- 2026-06-13 — **ready** (PM, Wave-5 intake / Batch **5E**). No deps. DoR met: AC1–AC6 observable,
  M, **`security_touching: false`** but this touches the **auth surface** (Login/forgot-password
  de-triplication + the IA-6 single source of truth for password rules incl. **aligning the weaker
  admin password complexity** to the customer/partner rule) — the PM routes a **Security advisory pass**
  alongside the reviewer (auth-adjacent, two real bug fixes: partner login + forgot-password swallowed
  errors). No nswag-regen / no migration (routes/verbs/DTOs unchanged — reviewer stops and re-scopes if
  any generated-client surface changes). **Exclusive surface** (3 login handlers, 5 AuthControllers,
  Dispute/SavedAddress controllers ×3, 4 facades) — do NOT run concurrently with any ticket touching
  those files; in particular **must not touch host auth registration** (BSP-1/T-0100 surface) or
  `disputes.facade.ts` (T-0202). Runs in its own lane in 5E. sprint re-tagged 5.

## Review
<!-- reviewer / security / optimizer write verdicts here; PM reconciles before advancing state -->
