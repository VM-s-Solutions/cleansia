# ADR-0028 — Multi-tenant activation pack: the HOST names the tenant (server-side host→tenant registry, precedence `override > tenant_id claim > host > null`), every anonymous email-keyed account flow then scopes to the resolved tenant — retiring the `IgnoringTenant` compensations — and confirm-family lookups STAY tenant-filtered; single-tenant mode (empty registry) is byte-identical to today

- **Status:** **draft — awaiting owner ratification** (panel consensus reached 2026-07-17 — zero
  blocking challenges; the ADR carries owner OPEN items O-1…O-3 which gate *activation*, not the
  decision shape. On ratification flips to `accepted` and becomes immutable — supersede, never edit.)
- **Date:** 2026-07-17
- **Supersedes:** — (composes with ADR-0017, which locked tenancy = app-level / claim-driven / no
  region in the filter; this ADR fills the one gap ADR-0017 left open: how an ANONYMOUS request —
  which has no claim — acquires its tenant. The tenancy query filter itself is **byte-unchanged**.)
- **Superseded by:** —
- **Applies to:** backend shared Core + `Cleansia.Config` (one small gate middleware + a
  `TenantProvider` precedence extension + the anonymous auth-flow read-switch) | all four API hosts
  equally (no host coupling) | **no schema migration** (the registry is config; no new columns) |
  **no NSwag / no client change** (web tenant selection rides DNS; mobile is deferred, O-2)
- **Ticket:** T-0365 (`security_touching: true`; "blocks multi-tenant go-live; single-tenant
  behavior is unaffected today") · related: T-0361 (the employee-read `IgnoringTenant` residual —
  the compensation family this ADR retires the *root cause* of), the `e406584f` anonymous-read fix
  class, the S8 composite `(TenantId, Email)` index decision recorded in
  `UserEntityConfiguration.cs:99-107`

> **One decision:** *how an unauthenticated request acquires its tenant.* Everything else in this
> pack is a corollary of that one rule. Today the tenant is resolved ONLY from the `tenant_id` JWT
> claim (`TenantProvider.cs:12-20`), so anonymous requests have **no tenant context at all** — and
> the auth flows compensated with `IgnoringTenant` reads ("the email is the scope"). That
> compensation is correct exactly as long as an email names at most one account platform-wide — the
> invariant the composite `(TenantId, Email)` unique index deliberately **removed**. The moment a
> second tenant holds an email the first tenant also holds: login resolves an arbitrary row,
> `RecordFailedLoginAsync` charges **all** rows, and a reset/confirm code issued to one tenant's
> account is verified against whichever row `FirstOrDefaultAsync` returns. This ADR decides:
> **(D1)** account identity is `(TenantId, Email)` and the request's **resolved tenant** scopes
> every anonymous account flow — no chooser, no fallback, no cross-tenant search; **(D2)** the
> resolved tenant comes from a **server-side host→tenant registry** (gate middleware + a lazy
> `TenantProvider` precedence chain `override > claim > host > null`), fail-mode: empty registry →
> null (single-tenant mode, byte-identical), non-empty registry → unmapped host **421**; **(D3)**
> confirm-family lookups **stay tenant-filtered** — the "filtered + wrong tenant = codes never
> match" failure is converted from a silent per-user dead end into a loud config error by D2's
> fail-closed rule, not by widening reads across tenants.

---

## Context (all citations verified in the working tree, 2026-07-17)

### The machinery that exists

- **The global tenant filter** — `CleansiaDbContext.ApplyTenantQueryFilters`
  (`src/Cleansia.Infra.Database/CleansiaDbContext.cs:200-269`); body:
  `tenantProvider == null || (currentTenantId == null && e.TenantId == null) || e.TenantId == currentTenantId`.
  The middle clause **is** single-tenant mode (CLAUDE.md compatibility law: `null` TenantId =
  single-tenant, must keep working unchanged).
- **Tenant resolution is claim-only** — `TenantProvider.GetCurrentTenantId()` returns the explicit
  override else the `tenant_id` claim else null (`TenantProvider.cs:12-20`). **An anonymous request
  therefore always resolves null.**
- **Writes are stamped from the same provider** — `CommitAsync` stamps a new `ITenantEntity`'s
  `TenantId` from `GetCurrentTenantId()` (`CleansiaDbContext.cs:88-91`). Anonymous registration can
  therefore only ever create **null-tenant** accounts today — a second tenant has no way to acquire
  a self-registered user.
- **Authenticated tenancy is user-row truth** — the JWT mints `tenant_id` from `user.TenantId`
  (`AuthExtensions.cs:30`). Post-login, tenancy is already correct and deterministic. **The gap is
  exclusively pre-auth.**
- **Email identity is per-tenant by decision** — the unique index is `(TenantId, Email)`, not
  `(Email)`, with the S8 rationale recorded in-code (`UserEntityConfiguration.cs:99-107`): a global
  index was a cross-tenant existence oracle (23505 → 500) and barred one person from being a
  customer in two tenants.

### The inventory of what breaks when an email exists in two tenants (the T-0365 findings, re-verified)

| Flow | Read today | Failure under a duplicate email |
|---|---|---|
| Login (all 5 variants) | `GetByEmailIgnoringTenantAsync` (`Login.cs:48`, `LoginValidator.cs:64,79,92,116`, `AdminLogin.cs:53`, `PartnerLogin.cs:49`, `MobileLogin.cs:61`, `MobilePartnerLogin.cs:62`) | `FirstOrDefaultAsync`, no ordering → **arbitrary row wins**; the other tenant's user cannot log in (their password fails against the picked row) |
| Failed-login lockout | `RecordFailedLoginAsync` — `IgnoreQueryFilters().Where(u => u.Email == email)` `ExecuteUpdateAsync` (`UserRepository.cs:132-147`) | **every row for the email is charged**: a guessing run against tenant A locks tenant B's same-email account (cross-tenant lockout griefing) |
| Forgot / reset | issue: `RequestPasswordChange.cs:42`; verify+complete: `ChangePassword.cs:60,73,84,104` — all `IgnoringTenant` | code is emailed to the arbitrary pick; verification re-resolves (possibly a different pick) → **a legitimate code is refused**; budget charged to whichever account got picked |
| Confirm-email (OTP branch) | `GetByEmailIgnoringTenantAsync` (`ConfirmUserEmail.cs:147` — its own comment concedes "the hash compare disambiguates *in practice*") | same arbitrary-pick class; budget charge (`TryChargeConfirmationCodeAttemptAsync`) lands on the picked account |
| Confirm-email (legacy 128-bit link) | `GetByConfirmationCodeAsync` — **tenant-FILTERED** (`UserRepository.cs:80-93`) | anonymous request → filter narrows to `TenantId == null` → **a tenant-stamped user's emailed link can never match** (the S8 silent-zero-rows trap, already broken today, no duplicate needed) |
| Resend confirmation | tenant-FILTERED `ExistsWithEmailAsync`/`GetByEmailAsync` (`ResendConfirmationEmail.cs:28,41,54`) | anonymous → null scope → a tenant-stamped unconfirmed user gets `NotExistingUserWithEmail` (**already broken today**) |
| Register | tenant-FILTERED pre-check (`Register.cs:49,76`) + null stamp at commit | per-tenant duplicate check is *correct* per the index — but the stamp means only null-tenant accounts can ever be created anonymously |
| Social auth | `GetByEmailIgnoringTenantAsync` (`GoogleAuth.cs:65`, `AppleAuth.cs:68`) | same arbitrary-pick class, keyed by provider-asserted email |
| Token mint / refresh | `GetByUserEmailIgnoringTenantAsync` (T-0361: `TokenService.cs:60-63`, `RefreshToken.cs:115`); `GetByIdIgnoringTenantAsync` (`RefreshToken.cs:84`) | the T-0361 compensation family — correct today, and the pattern this ADR gives a principled retirement path for on email-keyed sites |

The `IgnoringTenant` reads were **compensation**, added deliberately (comments at
`UserRepository.cs:59-63,129-131`, `LoginValidator.cs:16-17`) for "anonymous request = no tenant
context". They are correct in single-tenant reality and become nondeterministic the day the
composite index is actually *used*. T-0365 exists because the tenant-fix lane consciously deferred
the real decision; this ADR is that decision.

### What is locked around this decision

- **ADR-0017 (accepted):** tenancy is APP-level, row-scoped, claim-driven, "no tenant header"; the
  filter is never modified for region; a region clause in the filter is a conflation finding. This
  ADR keeps all of that: resolution feeds the **provider**, never the filter.
- **S1:** never trust `tenantId` from the request body/query. **S8:** `IgnoreQueryFilters()` only
  with an explicit re-pinning predicate (unguessable secret or caller's own id). **S3:** anonymous
  routes must not return tenant-scoped data un-gated.
- **CLAUDE.md law:** `null` TenantId = single-tenant mode, backward compatible, unchanged.

---

## Decision

> **Contract principle: the HOST names the tenant; the EMAIL names the account within it; the
> SECRET proves possession.** Anonymous tenant acquisition is a server-side lookup of the request
> host in a tenant-host registry — never a client-declared value, never derived from the email.

### D1 — Account identity is `(TenantId, Email)`; the resolved tenant scopes every anonymous account flow

1. **Registration.** Registering on host *H* creates the user under `tenant(H)` — via the existing
   `CommitAsync` stamp, because the provider now resolves the host tenant (D2); **no handler
   changes for stamping**. The duplicate pre-check stays tenant-scoped (already correct per the
   index comment). The same email registering on two tenants' hosts is **two unrelated accounts** —
   that is the composite index working as designed, not a conflict to resolve.
2. **Login / forgot-password / reset / confirm / resend / social auth** on host *H* resolve exactly
   the `(tenant(H), email)` account — deterministic by the unique index. **When the email exists in
   another tenant too: nothing.** No account chooser, no cross-tenant fallback search, no "did you
   mean tenant B" — the other tenant's account is a different account and is invisible on this
   host. (A fallback would be a cross-tenant oracle and would reintroduce the nondeterminism this
   ADR exists to remove.)
3. **The `IgnoringTenant` email-keyed variants are retired from anonymous flows.** With ambient
   host resolution in the provider, the plain filtered reads (`GetByEmailAsync`,
   `ExistsWithEmailAsync`, `GetByConfirmationCodeAsync`) do the right thing on every path — the
   compensation's root cause is gone. `RecordFailedLoginAsync` and the two attempt-budget charges
   drop `IgnoreQueryFilters()` (or equivalently carry the resolved-tenant predicate): the lockout
   charge lands on **one** `(tenant, email)` row, so tenant A's guessing run can no longer lock
   tenant B's account — and the S7a atomic-update shape is unchanged.
4. **`IgnoringTenant` remains sanctioned ONLY for secret-keyed and background reads** (the standing
   S8 pattern): refresh-token hash reads, webhook id lookups (`GetByStripePaymentIntentId…`),
   directory polls (ADR-0026/0027 — untouched), and the refresh-path user-by-persisted-id read
   (`RefreshToken.cs:84` — the id comes from the token record, an unguessable chain). The refresh/
   mint employee read (T-0361 sites) is refined to scope by the **resolved user's own
   `user.TenantId`** rather than staying tenant-ignoring — deterministic on both login and refresh,
   independent of host.
5. **The founding tenant's identity is `null`, forever.** Existing rows are never backfilled to a
   real tenant id (a whole-database rewrite for zero functional gain); the filter's middle clause
   *is* the founding tenant's scoping. A "real" second tenant is a new non-null `TenantId`.
6. **Activation invariant (new, enforced at activation):** a non-null `TenantId` on any `User` row
   implies an **activated** tenant — one with host mappings in the registry. The activation runbook
   audits for stray tenant-stamped rows *before* the read-switch lands; a tenant-stamped user
   without a mapped host is exactly the population the retired `IgnoringTenant` reads were
   compensating for, and would be stranded (the `e406584f`/T-0361 bug class returning). See
   Rollout.

### D2 — Resolution mechanism, placement, and fail-mode

1. **A server-side host→tenant registry** — `ITenantHostRegistry` (`Cleansia.Config`), backed by
   configuration (`Tenancy:Hosts`, a `host → tenantId` map where the mapped value may be **null**:
   the founding tenant maps its hosts to null explicitly). Case-insensitive exact host match
   (no wildcard magic in v1). Config-backed now because tenant activation is an owner/deploy
   operation; moving it to the DB (`TenantConfiguration` or a first-class `Tenant` entity) is a
   named escalation gated on self-service onboarding (O-3) — behind the interface, a swap, not a
   redesign.
2. **Two consumption points, one source of truth:**
   - **A small gate middleware** in the shared `CleansiaStartupBase` pipeline, after
     `UseForwardedHeaders` (the host may legitimately arrive via `X-Forwarded-Host` from the
     **trusted proxy only** — the exact S5 trusted-proxy discipline; a client-forged forwarded host
     is stripped there) and **before** authentication. It resolves the host against the registry
     once per request, stashes the result in `HttpContext.Items`, and enforces the fail-mode.
   - **`TenantProvider` gains the last link in its precedence chain:**
     `explicit override > tenant_id claim > stashed host resolution > null`. The **claim always
     wins when present** — it is signed user-row truth (`AuthExtensions.cs:30`); the host may never
     widen, narrow, or reassign an authenticated session's tenancy. A claim-vs-host mismatch is
     logged (warning, ids only — S6) and the claim is honored. Because the provider is evaluated
     lazily at query time, there is no pipeline-order hazard for the precedence itself; the
     middleware exists for the fail-mode gate and the once-per-request resolution.
3. **Fail-mode (the ticket's "no resolvable tenant → ?"):**
   - **Registry empty / section absent → resolve null everywhere.** This is single-tenant mode and
     is **byte-identical to today** (provider chain degenerates to `override > claim > null`) — the
     CLAUDE.md compatibility law, honored structurally, and pinned by test (TC-TENANT-ACT-7).
   - **Registry non-empty → an unmapped host fails closed: `421 Misdirected Request`** before any
     handler runs (health/liveness endpoints exempt). Rationale: once the platform is
     multi-tenant, serving a request whose tenant cannot be named means either silently serving the
     founding tenant's data on a stray CNAME (a leak-shaped default) or silently zero-matching
     (the S8 trap). A loud 421 makes a missing mapping a *config incident within minutes*, not a
     per-user support mystery.
   - **Break-glass:** `Tenancy:UnmappedHostMode = Reject | Null`, default `Reject`, raw-file
     test-pinned like the ADR-0026 bounds — an ops escape hatch for a botched activation (e.g. a
     forgotten apex/www/legacy host), never a steady state. Flipping it to `Null` in config is
     loud (a failing pin test gates the commit path; runtime flip is the incident lever).
4. **What resolution does NOT touch:** `ApplyTenantQueryFilters` is **byte-unchanged** (the
   ADR-0017 conflation check extends to tenancy resolution itself); no handler ever branches on a
   tenant id (the country-code rule extended); `SetTenantOverride`/`ClearTenantOverride` semantics
   for background jobs are unchanged and still outrank everything.

### D3 — Confirm-family lookups STAY tenant-filtered

**The uniform rule: account lookups ride the ambient filter; only unguessable-secret *token
mechanics* may bypass it.**

- The **OTP branches** (confirm-email, reset-code) are email-named by design (`be087ae3`: a 6-digit
  code is never resolved by the bare code) — under D1/D2 they resolve `(resolvedTenant, email)` and
  the hash compare proves possession against exactly one account. They *cannot* meaningfully ignore
  tenant: the email is the resolver, and the email is only unique per tenant.
- The **legacy 128-bit link branch** (`GetByConfirmationCodeAsync`) stays inside the filter. The
  alternative — unfiltering it "because 128 bits can't collide cross-tenant" — is technically safe
  but is rejected: it would be the only email-flow read outside the filter, a permanent S8 audit
  exception carried for a **dying surface** (pre-OTP links; long-expired by the time a second
  tenant activates), bought to avoid a failure mode D2 already converts into a loud 421. Under host
  resolution the filtered lookup is *correct* for tenant-stamped users for the first time (today it
  silently zero-matches them).
- **The ticket's dilemma dissolves:** "filtered + wrong tenant resolution = codes that never match"
  presumes wrong resolution is silent. With D2's fail-closed rule, wrong resolution is an unmapped
  or mis-mapped host — a 421 or an activation-checklist failure — not a quiet per-user dead end.
  "Unfiltered = cross-tenant probing surface" is avoided outright.
- **Attempt budgets improve:** the per-code charge lands on the account the *resolved tenant*
  names, so an attacker on tenant B's host can no longer burn tenant A's user's budget (today the
  arbitrary pick charges whichever tenant's account `FirstOrDefaultAsync` returned).

### Rollout — what changes when the SECOND tenant first activates (the activation runbook)

Nothing in this ADR requires a migration or client change **now**. The pack lands as one backend
ticket (registry + gate + provider chain + read-switch, sequenced internally: resolution must exist
before the read-switch commit), fully inert while `Tenancy:Hosts` is empty. Activation then is:

1. **Owner decides the host scheme (O-1)** and provisions DNS/TLS for the new tenant's serving
   hosts (see the CH-3 discussion for the per-audience API host shapes).
2. **Seed the registry with ALL currently-serving founding hosts → null** (apex, www, the four API
   hosts, legacy domains — checklist-driven; missing one = 421 on that host, caught by the
   post-seed smoke check, recoverable via break-glass).
3. **Audit invariant D1.6:** no `User` row carries a non-null `TenantId` whose tenant lacks host
   mappings; reconcile strays before proceeding.
4. **Add tenant B:** its `TenantId` value + host mappings + its `CountryConfiguration`/catalog
   seed; only then create or invite its users. Onboarding stays "a data + config operation, not a
   deployment" (ADR-0017 D1) — this ADR adds exactly one config artifact to that sentence.
5. Mobile presence for tenant B is **gated on O-2** (deliberately not solved here).

---

## Alternatives considered

- **(a) Server-side host→tenant registry + resolved-tenant scoping.** **CHOSEN** (D1/D2). No
  client-controlled tenant input (S1 intact), zero client changes for web (DNS does the naming),
  deterministic account resolution by the existing unique index, single-tenant mode preserved
  structurally (empty registry ≡ today), and the filter/claim machinery byte-unchanged.
- **(b) Client-declared tenant (an `X-Tenant-Id` header / body field) on anonymous flows.**
  Rejected as the default. Honest analysis: for *anonymous* flows the tenant is a namespace
  selector, not a privilege — an attacker could equally visit the other tenant's real frontend. But
  it (1) violates S1's letter and would need a standing carve-out taught to every future reviewer,
  (2) makes cross-tenant enumeration a header loop against one endpoint instead of per-host
  traffic (the per-IP auth bucket bounds both, but the ergonomics differ), (3) couples every
  client — web, Android, iOS — to tenant plumbing that DNS gives the web for free, and (4) once the
  header exists, the pressure to trust it *somewhere it matters* never goes away. Kept as the
  **named possible extension for mobile only** (O-2): if a second tenant needs mobile before
  per-tenant app distribution exists, a successor ADR may admit a pinned client-config tenant for
  the two mobile hosts — under the hard rule that it can never outrank claim or host.
- **(c) Global email uniqueness (revert to a single-column unique index).** Rejected: re-opens
  everything the composite index closed (`UserEntityConfiguration.cs:99-107`) — the cross-tenant
  existence oracle, the unhandled-23505 500, and "one person cannot be a customer of two tenants,"
  which is a real multi-tenant requirement, not an edge case.
- **(d) Credential-resolved tenant ("try every row holding the email; the matching password/code
  names the account").** The challenger's strongest cheap option — no DNS, no registry. Rejected:
  (1) it turns one login attempt into a credential test against **N** accounts (a cross-tenant
  oracle by construction); (2) the lockout charge becomes unanswerable — charge all rows
  (today's griefing bug, kept) or none (S7a atomic update cannot name the "winner" it didn't
  find); (3) it is **ambiguous exactly for the persona the composite index exists for** — the same
  person in two tenants plausibly reuses the same password, and then two rows match; (4) register,
  resend, and forgot-password have **no secret at resolution time** — the mechanism cannot even
  cover the flow set, so a second mechanism would be needed anyway. A mechanism that only handles
  half the flows and is oracle-shaped on the other half loses to (a) outright.
- **(e) Defer again (decide at second-tenant time).** Rejected: this decision *was* deferred once —
  T-0365 is the deferral note. The pack is cheap and inert now (config-gated, no migration, no
  client change); deciding it under a live second tenant means doing the same work against
  production data with the nondeterminism already shipping.
- **(f) Host outranks the claim (host authoritative for authenticated requests too).** Rejected:
  the claim is signed user-row truth; letting a Host header reassign an authenticated session's
  tenancy inverts the trust order and creates a request-forgery-shaped tenant switch. Mismatch is
  logged, claim honored (D2.2).

---

## Consequences

**Cheaper / safer:**
- Multi-tenant activation becomes possible at all: every anonymous account flow is deterministic
  under duplicate emails, and cross-tenant lockout griefing / budget burning are structurally gone.
- The `IgnoringTenant` **email-keyed** compensation family (the T-0361/e406584f class) is retired
  at the root instead of being patched read-by-read; the surviving `IgnoreQueryFilters` sites are
  exactly the S8-sanctioned secret-keyed/background set — a smaller, auditable exception list.
- Single-tenant deployments are structurally unaffected (empty registry ≡ today, test-pinned), and
  the two broken-today paths (legacy confirm link + resend for tenant-stamped users) become
  *correct* rather than differently broken.
- The filter, the claim mint, ADR-0026/0027 revocation machinery, background overrides, and the
  per-audience host split are all byte-untouched; ADR-0017's region seam composes unchanged
  (host→tenant here is app-config; tenant→region there is infra-config; they meet nowhere).

**More expensive (accepted):**
- **Per-tenant serving hosts are now a hard activation prerequisite** — DNS + TLS per tenant for
  each audience surface that tenant uses (App Service supports N custom domains per site; the SSR
  customer host can alternatively forward the original host via the trusted proxy — implementation
  freedom within the same trusted-proxy discipline). This is real operational cost per tenant,
  chosen over the client-declared alternative's permanent trust carve-out.
- A registry + gate + precedence chain to maintain, and an activation runbook with a checklist step
  that can 421 a forgotten founding host (mitigated: smoke check + break-glass `UnmappedHostMode`).
- The anonymous per-flow enumeration surface (`NotExistingUserWithEmail` on forgot/resend) becomes
  per-tenant-scoped — unchanged in kind, still bounded by the per-IP `auth` bucket; recorded, not
  new.
- Mobile multi-tenancy is explicitly **not** solved (O-2) — a second tenant activates web-first
  under this ADR.

---

## How a reviewer verifies compliance

**Mechanical:**
1. `ApplyTenantQueryFilters` and `AuthExtensions.SetClaims` are **byte-unchanged** (diff-empty on
   this axis). Resolution feeds `TenantProvider`/`HttpContext.Items`, never the filter.
2. Precedence lives in exactly one place (`TenantProvider`): `override > claim > host > null`;
   grep confirms no second reader of `Tenancy:Hosts` outside the registry, and the claim path is
   evaluated before the host path.
3. After the read-switch, **no anonymous email-keyed flow calls a `*IgnoringTenant*` variant**:
   grep the inventory set (`Login`/`PartnerLogin`/`AdminLogin`/`MobileLogin`/`MobilePartnerLogin`/
   `LoginValidator`/`RequestPasswordChange`/`ChangePassword`/`ConfirmUserEmail`/
   `ResendConfirmationEmail`/`Register`/`GoogleAuth`/`AppleAuth`). Surviving `IgnoreQueryFilters`
   sites are secret-keyed/background and keep their S8 justification comments.
4. `RecordFailedLoginAsync` / `TryCharge*AttemptAsync` no longer bypass the filter un-pinned — the
   failed-login update matches at most **one** `(tenant, email)` row.
5. The gate middleware sits in `CleansiaStartupBase` after `UseForwardedHeaders`, before
   authentication, on all four hosts identically; health/liveness routes are exempt from 421.
6. `Tenancy:UnmappedHostMode` defaults to `Reject` and is raw-file test-pinned (the ADR-0026
   TC-REVOKE-NOW-7 mechanism); changing it in checked-in config fails a test absent a superseding
   ADR.
7. No handler branches on a tenant id (the country-code rule, extended — same grep discipline).
8. The T-0361 employee-read sites scope by `user.TenantId` (the resolved user's own row), not by
   host and not tenant-ignoring.

**Test contract (names for the implementation ticket):**
- **TC-TENANT-ACT-1 — duplicate-email login determinism.** Two tenants (null + "B"), same email,
  different passwords, hosts mapped. Login on host A with A's password → 200, `tenant_id` claim =
  A's; with B's password → `InvalidPassword` AND only A's row accrues `FailedLoginAttempts` (B's
  row stays 0 — the cross-tenant griefing pin).
- **TC-TENANT-ACT-2 — lockout isolation.** Lock `(A, email)` by spraying host A → `(B, email)`
  still logs in on host B.
- **TC-TENANT-ACT-3 — reset-code determinism.** Forgot-password on host A issues the code to A's
  account; completion succeeds on host A; the same code on host B is refused; B's budget untouched.
- **TC-TENANT-ACT-4 — confirm-family on the right host.** A tenant-stamped user's OTP **and**
  legacy link both confirm on their tenant's host and are refused on the other host (pins D3:
  filtered lookups now *work* for tenant-stamped users).
- **TC-TENANT-ACT-5 — per-tenant registration.** Register the same email on host A then host B →
  two rows, correctly stamped, the composite index holds; duplicate on the same host →
  `ExistingUserWithEmail`.
- **TC-TENANT-ACT-6 — fail-mode matrix.** Empty registry: unmapped host serves normally (null
  mode). Non-empty registry: unmapped host → 421 before handlers; `/health` still 200;
  `UnmappedHostMode=Null` restores serving (break-glass proven).
- **TC-TENANT-ACT-7 — the single-tenant compatibility pin (the headline).** The entire existing
  auth suite (login/lockout/reset/confirm/resend/refresh/social pins, incl.
  `UserRepositoryTenantLoginLockoutTests` and the T-0361 pin) runs green with an **empty registry**
  and no test edits — empty-registry mode is byte-identical to today.
- **TC-TENANT-ACT-8 — claim precedence.** An authenticated request presented on the "wrong"
  tenant's host scopes by its claim (data unchanged), and the mismatch is logged without PII.

---

## OPEN items — owner / product input required (activation-gating, not decision-gating)

- **O-1 — Host naming scheme + per-tenant surface set.** Which surfaces get per-tenant hosts and
  under what scheme (`{tenant}.cleansia.cz` subdomains vs tenant-owned domains via CNAME; which of
  customer/partner/admin/mobile API surfaces a tenant actually receives). The registry accepts any
  exact host, so this is pure naming/DNS policy — **deliberately not decided here.**
- **O-2 — Mobile tenant acquisition.** Per-tenant app flavors, an in-app tenant picker, or the
  pinned-client-config header extension (Alternatives (b), successor-ADR-gated). Blocks a second
  tenant's *mobile* presence only; web activation proceeds without it.
- **O-3 — Registry backing store escalation.** Config now; move behind `ITenantHostRegistry` to the
  DB when tenant onboarding becomes self-service (trigger: the first tenant the owner does not
  personally activate).

---

## Living docs to update at acceptance (not before — this ADR is a draft)

- `agents/architecture/decisions/multi-tenancy-and-region.md` — add the anonymous-resolution
  section: the precedence chain, the registry, the fail-mode, the activation runbook + invariant,
  and the "IgnoringTenant = secret-keyed/background only" convergence.
- `agents/knowledge/security-rules.md` — S8 gains: *"Anonymous requests acquire their tenant from
  the server-side host registry (ADR-0028); anonymous email-keyed account flows must use the
  ambient filter, never `IgnoringTenant` variants. `IgnoreQueryFilters()` remains sanctioned only
  for secret-keyed / background reads with a re-pinning predicate."* S1 gains the pointer that a
  client-declared tenant is rejected even for anonymous flows (ADR-0028 Alt (b)).
- `agents/knowledge/patterns-backend.md` — the tenancy note gains the precedence chain and the
  "host names the tenant / email names the account / secret proves possession" rule.
- `agents/knowledge/roles/tenant-host-registry.md` — **new CRC card**: resolves serving hosts to
  tenant ids; collaborators: gate middleware, `TenantProvider`; does NOT know: users, claims,
  regions, connection strings (the ADR-0017 resolver is a different role). `TenantProvider`'s card
  gains the host link in its chain.

---

## Challenge

*(Architect panel, challenger mode, 2026-07-17. Citations independently re-verified against the
working tree — the inventory table above was walked file-by-file; the `RecordFailedLoginAsync`
all-rows behavior, the resend/legacy-link already-broken-today findings, and the claim-mint at
`AuthExtensions.cs:30` are confirmed, not taken from the draft.)*

**CH-1 — BLOCKING-CANDIDATE (D1.3): retiring the `IgnoringTenant` reads re-creates the exact
`e406584f`/T-0361 bug class for any tenant-stamped row that exists BEFORE its host is mapped.**
The compensation reads exist because tenant-stamped users could not log in anonymously. The draft
deletes them and waves at a runbook. A runbook is not an enforcement mechanism: if a tenant-stamped
user exists in any environment when the read-switch deploys with an empty registry, that user is
locked out of login, reset, confirm AND resend simultaneously — a worse outage than the bug the
compensations fixed. Demand: the invariant must be *checked by the pack*, not just written in a
runbook, and the sequencing must be inside one deployable unit.

**CH-2 — (D1, alternative dismissed too fast): credential-resolved tenant needs a real answer, not
a paragraph.** It requires zero DNS, zero registry, zero activation runbook — for login and
reset-completion it is *self-disambiguating by the secret the flow already holds*. The draft owes
the precise reason it fails, or it is the cheaper option.

**CH-3 — BLOCKING-CANDIDATE (D2): the Host header the API sees may not distinguish tenants at
all.** The per-audience hosts are per **audience**, not per tenant (`api-cleansia-partner-weu-dev`,
ADR-0015/0017 naming). If tenant B's customer SPA at `b.example` calls the shared customer API
origin, the API's `Host` is the API's own name — the registry resolves the *audience*, not the
tenant. The decision as drafted silently assumes per-tenant **API** custom domains (N tenants × up
to 4 audience sites of DNS+TLS) or a forwarding gateway — neither is stated as a cost, and O-1
reads as if only cosmetic naming were open. Name the operational commitment or the mechanism is
underspecified.

**CH-4 — (D2 fail-mode): 421-on-unmapped-host is a founding-tenant availability grenade.** The
first activation flips the registry non-empty; one forgotten founding host (apex vs www, a legacy
domain, the mobile API host no one remembers is in the serving set) takes real traffic to 421 at
the exact moment of the most delicate deploy. Fail-closed is right in steady state; the *transition*
needs a lever and a proof, not optimism.

**CH-5 — (D2 placement): the draft's provider-lazy resolution has no clean home for the fail-mode.**
If resolution happens only inside `TenantProvider` at query time, an unmapped host is discovered
mid-query — throwing 421 out of a query-filter evaluation path is a layering violation and
untestable. Either the fail-mode moves to a pipeline component or the fail-mode is unreachable.

**CH-6 — (D3): keeping the legacy 128-bit lookup filtered is dogma over threat-model.** 128 bits
cannot be probed cross-tenant; the refresh-token hash read ignores the filter for exactly this
reason (`RefreshTokenRepository.cs:10-23`). Why is one unguessable secret sanctioned and the other
not? If the answer is "uniformity," show the cost of the exception is real.

**CH-7 — (process): this is three decisions wearing one ADR — the charter says split.**
Registration semantics, middleware, and confirm-family filtering each have their own alternatives
tables. Justify the pack or split it.

**Checked and NOT challenged (named per the protocol):** the claim-wins precedence (f) — correct,
the claim is signed row-truth; the null-founding-tenant posture (no backfill) — consistent with the
filter's middle clause and ADR-0017; the S8 survival set (secret-keyed/background reads) — each
surviving site re-verified as pinned by an unguessable predicate; ADR-0026/0027 interaction — the
directories key on userId/deviceId, tenant-independent, untouched; the enumeration surface — not
new, per-IP-bounded, correctly recorded as residue rather than solved.

## Defense

*(Author, same session, responding per the deliberation bar.)*

- **CH-1 — CONCEDE + REVISE (folded).** The challenge is right that a runbook is prose. Folded into
  the artifact: (i) the pack lands as **one deployable ticket** with resolution preceding the
  read-switch *within* it (Rollout intro — no window where the reads are switched and resolution
  absent); (ii) invariant D1.6 gains a **mechanical check**: the implementation ticket must ship a
  startup-time (or health-degrading) assertion — *"if any `User` row carries a non-null `TenantId`
  whose tenant has no registry mapping, log an error naming the tenant"* — plus the activation-audit
  step, so a stranded population is loud in every environment, not discovered by a support ticket.
  TC-TENANT-ACT-7 pins the empty-registry world byte-identical, which covers today's actual data
  (all-null rows) — the residual risk is precisely tenant-stamped strays, and the assertion names
  them. Not a design change; an enforcement addition.
- **CH-2 — REBUT (with the flow inventory).** Credential resolution cannot cover **register,
  resend, or forgot-password** — there is no secret at resolution time in any of them, so a second
  mechanism (host or header) is required regardless; at that point credential-resolution is
  additive complexity, not an alternative. On the flows it *can* cover it is oracle-shaped: one
  login attempt tests the password against every tenant's row (Alternatives (d) point 1), the S7a
  lockout update cannot atomically name a winner it didn't find (point 2), and the same-person-
  same-password-two-tenants case — the *design persona* of the composite index — is genuinely
  ambiguous (point 3). An alternative that fails 3 of 6 flows outright and is ambiguous for the
  target persona on the rest is answered, not dismissed.
- **CH-3 — CONCEDE the cost, REBUT the underspecification claim, REVISE the text.** Conceded and
  now explicit (Consequences bullet 1 + Rollout step 1): **per-tenant serving hosts for the API
  surfaces a tenant uses are a hard activation prerequisite** — real DNS+TLS cost per tenant — OR
  the SSR/trusted-proxy forwards the original host under the exact S5 trusted-proxy discipline
  (`X-Forwarded-Host` handled at `UseForwardedHeaders`, client-forged values stripped). Rebutted:
  this is implementation shape *within* the decided rule ("a server-side host mapping consulted by
  the API"), not a different mechanism — both shapes terminate in the same registry lookup and the
  same tests; which shape, and the naming, is O-1 because it is DNS/product policy the owner must
  ratify anyway. The decision is the rule + fail-mode + precedence; it is not silent about the
  cost anymore.
- **CH-4 — CONCEDE + REVISE (already folded at draft time, sharpened).** The break-glass
  `UnmappedHostMode=Null` (D2.3) + the runbook's post-seed smoke check + the health-route
  exemption are exactly this lever; sharpened per the challenge: the smoke check is an explicit
  runbook step (step 2) run **before** tenant B's users exist, when `Null`-mode fallback is still
  harmless (all rows null). The knob is raw-file pinned so it cannot silently become steady state.
  Fail-closed stays the default: the alternative (unmapped → founding tenant) is a silent
  cross-tenant-serving default forever, traded against a bounded, lever-equipped transition risk.
- **CH-5 — CONCEDE + REVISE (folded).** Correct, and the draft you are reading already carries the
  concession: D2.2 is the **hybrid** — a small gate middleware owns resolution + fail-mode
  (pipeline-testable, pre-handler 421), `TenantProvider` owns precedence consumption via
  `HttpContext.Items`. Provider-only was the author's first shape; the challenge killed it; the
  middleware is not optional decoration but the fail-mode's home.
- **CH-6 — REBUT (with the cost ledger).** The refresh-token hash bypass is load-bearing on an
  **authenticated, permanent, high-traffic** path with no alternative (the row is null-stamped by
  the anonymous issue path — there is no tenant context that would ever make the filtered read
  correct). The legacy confirm-link is the opposite on every axis: anonymous, **dying** (pre-OTP
  links; expired long before any second tenant exists), and under D2 the filtered read is simply
  *correct* — the bypass would buy robustness against a misconfiguration that D2 makes loud
  anyway. An S8 exception is a permanent audit obligation (rule: every `IgnoreQueryFilters` site
  is individually justified forever); paying that forever for a dead surface's edge case is a real,
  named cost, not uniformity dogma. The exception budget stays spent on sites that need it.
- **CH-7 — REBUT (the ADR-0017 precedent).** One decision: *how an anonymous request acquires its
  tenant.* D1 is that rule's consequence for account identity (scope reads by what resolution
  yields); D3 is its consequence for one lookup family (the filter is now correct, so stay inside
  it). Neither D1 nor D3 is decidable without D2, and D2 is unmotivated without D1's breakage
  inventory — the "inseparable facets" test ADR-0017 recorded ("Why this is ONE decision"). Three
  ADRs would tri-plicate the context and invite drift between them.

## Verdict

*(Architect panel lead, 2026-07-17 — adjudicating hat; each ruling re-checked against the cited
code and the revised text before ruling.)*

| Challenge | Ruling | Disposition |
|---|---|---|
| CH-1 stranded tenant-stamped rows | **RESOLVED — concession folded** | One deployable unit + the mechanical stray-row assertion + TC-TENANT-ACT-7. The invariant is enforced, not narrated. |
| CH-2 credential-resolved alternative | **RESOLVED — rebutted** | Fails register/resend/forgot outright (no secret exists at resolution time), oracle-shaped and S7a-incompatible on the rest, ambiguous for the composite index's own design persona. Alternatives (d) records it. |
| CH-3 shared-audience API hosts | **RESOLVED — cost conceded, mechanism stands** | Per-tenant API hosts (or trusted-proxy host forwarding) are now an explicit, costed activation prerequisite; both shapes are the same rule + registry + tests. Naming/shape is genuinely owner policy → O-1. |
| CH-4 421 transition risk | **RESOLVED — lever + proof folded** | Break-glass knob (pinned), pre-user smoke-check sequencing, health exemption. Fail-closed default stands: silent founding-tenant serving on stray hosts is the worse permanent default. |
| CH-5 fail-mode placement | **RESOLVED — concession folded** | The hybrid (gate middleware + provider precedence) is the recorded decision D2.2. |
| CH-6 legacy-link filter bypass | **RESOLVED — rebutted** | The S8 exception budget is a permanent per-site audit cost; spending it on a dying surface to soften a failure D2 makes loud fails the cost/benefit test. The refresh-hash precedent is disanalogous on lifetime, path, and necessity. |
| CH-7 split the ADR | **RESOLVED — rebutted** | One decision (anonymous tenant acquisition) with two corollaries; the ADR-0017 inseparability precedent applies. |

**Consensus: zero blocking challenges remain.** The panel adopts the artifact as revised. Status is
**`draft — awaiting owner ratification`**, NOT `accepted`, because the pack's activation
prerequisites are owner-owned commitments this panel cannot make on the owner's behalf: **O-1**
(the host/subdomain naming scheme and the per-tenant DNS+TLS operational commitment — a lasting
business/ops cost) and **O-2** (the mobile tenant-acquisition product shape). Per the charter's
escalation rule these ride to the owner via `questions/open.md` at PM dispatch; the *technical*
shape (registry + precedence + fail-closed + filtered confirm-family + the read-switch) is
consensus-final and is what the owner ratifies. O-3 is a recorded trigger, non-blocking.

**Why this earns its place (the long-game test).** It removes an entire compensation family instead
of growing it (every future anonymous flow gets tenancy for free from the ambient chain — no more
per-read `IgnoringTenant` judgment calls for developers); it keeps the three seams it touches
byte-unchanged (filter, claim mint, per-audience hosts); single-tenant mode is preserved
structurally rather than by care; and the two future evolutions it can foresee (DB-backed registry,
mobile tenant acquisition) are named triggers behind existing interfaces, not rewrites. The
alternative futures — a client tenant header trusted "just for anonymous," or per-flow heuristics —
each make the *next* tenant more expensive; this makes it a config + DNS operation.

**No code, no INDEX edit, no living-doc edit ships with this draft** (the living-doc/catalog edits
are enumerated above and land at acceptance, per the ADR-0024/0026/0027 precedent).
