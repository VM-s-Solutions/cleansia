# Sprint 10 — WAVE 8: Pre-iOS Cleanup

**Status:** PLANNED (backlog only — no code, no commits)
**Created:** 2026-06-22
**Source:** `agents/backlog/audits/AUDIT-2026-06-22-pre-ios-cleanup.md` (13 findings) + owner points P1–P4.
**Goal:** clear the carried structural/contract debt the audit + owner surfaced **before iOS development
starts**, so the iOS port builds against a clean, deduplicated, canonical contract surface — not a fork.

> The audit-driven program (Waves 0–7) is **closed and merged**. Wave 8 is a discrete cleanup wave, not
> a continuation of the audit program. 10 tickets (**T-0272…T-0281**), next free ids after T-0271.

---

## 1. Reconciliation against current master (what's genuine, what was refuted/folded)

Every audit finding + owner point was re-traced against `master` (tip `8aa7bcc1`) and run through
`check-consistency.mjs`. Outcomes:

| Audit # / owner pt | Claim | Verdict | Disposition |
|---|---|---|---|
| **P1** | `trustedDeviceToken` web-shared but mobile-only | **VERIFIED** — web overwrites from cookie (`Web.Customer/AuthController.cs:40`); mobile passes body through | **T-0272** |
| **#9** | `RefreshToken.Command` leaks `RequiredProfile`/`RequiredAudience` | **VERIFIED** — all 5 refresh controllers enrich + overwrite; both init-able + on the wire | **T-0272** (same shrink) |
| **#1** | FE error-resolver re-impl in 8 facades | **VERIFIED** — `resolveErrorKey` confirmed in `admin-order-ops.facade.ts:190-207`; canonical in `snackbar.service.ts:117-165` | **T-0274** |
| **#2** | partner-app formatters dup + diverge from `:core` | **HELD** (audit traced file:line) | **T-0277** |
| **#3** | push-token cluster dup across both apps | **HELD** (audit traced; `partner PushTokenRepository.kt:30` admits the mirror) | **T-0278** |
| **#4** | GetMyReferrals non-canonical | **VERIFIED** (tool A1+A5) | **T-0273** |
| **#5** | Loyalty activity pair non-canonical | **VERIFIED** (tool A1+A5 ×2) | **T-0273** |
| **#6** | GetPromoCodeRedemptions non-canonical | **VERIFIED** (tool A1+A5) | **T-0273** |
| **#7** | GetAllEmployees dead paged dup | **VERIFIED** — only self-reference, zero dispatch | **T-0275** |
| **#8** | GetUserByEmail dead CQRS feature | **VERIFIED** — never `Send`-ed (refs = own file + 2 comments) | **T-0275** |
| **#10** | sitewide-push: no facade + raw `http.post` | **VERIFIED** (`sitewide-push-form.component.ts:131-155`) | **T-0276** |
| **#11** | admin-pay-config.service hand-rolls client | **VERIFIED** — but **blocked on IMP-3 regen** | **T-0279** (blocked) |
| **#12** | GetEmployeeDocuments A5 (hand-built PagedData) | **VERIFIED** (tool A5) | **folded into T-0273** |
| **#13** | LOW drift cluster (GetPagedInvoices A6, AdminLogin B5, dead validator helper) | **HELD** (minor) | **folded into T-0275** |
| **P2** | comment-noise cleanup | accepted | **T-0280** (after T-0272) |
| **P3** | expand T-0271 E2E to partner/admin | accepted | T-0271 (customer) kept + **T-0281** (siblings) |
| **P4** | paged canonicalization + "add an A* rule" | accepted, **with a correction** | **T-0273** + meta-finding |

### Refutations / corrections (honesty — where the audit or the owner's quick-classify did NOT hold)
1. **`GetPagedDisputes` is NOT an offender — REFUTED.** The owner's earlier quick-classify flagged it,
   but `GetPagedDisputes.cs` is **canonical A1–A8** (`DataRangeRequest` + `DisputeFilter.MapToDomain()`
   spec + `GetCountAsync` + `GetPagedSort<DisputeSort>` + `AsNoTracking` + `items.MapToDto(total,
   request)`). The audit's coverage note was right. **Not touched by T-0273.**
2. **`GetPagedMembershipPlans` IS an offender the AUDIT MISSED.** The owner named it as an example; the
   audit's 5-query list omitted it, but `check-consistency.mjs` flags it A1+A5 (`:20,38`) and it's live
   (`AdminMembershipController.cs:31`). **Added to T-0273.** Net genuine paged offenders = **7 live**
   (audit's 5 + GetEmployeeDocuments A5 + GetPagedMembershipPlans), **not 6**.
3. **P4 "add a check-consistency A* rule" — already satisfied, no new rule.** The A1/A5 rules **already
   catch** every one of the 7 offenders (verified). Adding a rule would duplicate them. The real gap is
   that these were **never ticketed** and `consistency-violations.md` claimed the sweep complete — the
   **meta-finding**. T-0273 AC4 = tool-clean-after + de-stale the doc; **no new rule**. (The
   `consistency-violations.md` F1b entry is already updated to record this.)
4. **Nothing else refuted** — the audit's Gate-0 discipline held; #11's IMP-3 dependency is the only
   "can't run this wave" item.

---

## 2. Wave-8 ticket table

| ID | Title | Size | Layers | sec | qa | manual_steps | Lane / batch |
|----|-------|------|--------|-----|----|--------------|--------------|
| **T-0272** | Auth wire-contract shrink (`trustedDeviceToken` mobile-only + drop RefreshToken server fields) | M | architect, backend | **yes** | yes | **nswag-regen** (all web + mobile clients) | **8A — FIRST/ALONE** |
| **T-0273** | Canonicalize 7 bespoke paged queries → DataRangeRequest+Spec+Sort+PagedData | M | backend | no | yes | — | 8B |
| **T-0274** | Dedup API error-key extractor across 8 facades → shared `@cleansia/services` helper | M | frontend | no | yes | — | 8B |
| **T-0275** | Delete dead paged dups (GetAllEmployees, GetUserByEmail) + LOW drift cluster | S | backend | no | yes | — | 8B |
| **T-0276** | Extract `SitewidePushFormFacade` → generated client + `UnsubscribeControlDirective` | S | frontend | no | yes | — (conditional regen, AC5) | 8B |
| **T-0277** | Hoist partner-app order formatters onto `:core` (delete divergent dup) | S | android | no | yes | — | 8B (`:core` lane) |
| **T-0278** | Hoist push-token cluster into `:core` behind `DeviceRegistrationClient` | M | android | no | yes | — | 8B (`:core` lane) |
| **T-0279** | admin-pay-config.service → generated `AdminPayConfigClient` | S | frontend | no | yes | **nswag-regen** (rides IMP-3) | **BLOCKED (IMP-3)** |
| **T-0280** | Strip comment noise (FE auth services + audit pockets) | S | frontend, backend | no | yes | — | 8C (after T-0272 regen) |
| **T-0281** | E2E sibling smokes — partner accept-job + admin login-and-land | M | frontend, backend | no | yes | — | 8C (after T-0271) |

**Reviewer-per-developer on every ticket.** Security gate on **T-0272 only**. No optimizer gate is
mandatory (T-0273 read paths stay query-plan-equivalent — advisory only). QA on all (suite-green +
AC↔evidence; ≥3-run determinism on the E2E tickets).

**Sizes:** every ticket is **S or M** — no `L`. (T-0273, T-0274, T-0281 are M but split-ready: each
status log names the "if it grows past M at dispatch, stop and split" trigger.)

---

## 3. Dependency-ordered batch plan (the regen ripple decides the order)

```
8A (lands FIRST, ALONE)        →   OWNER REGEN BUNDLE   →   8C
  T-0272 auth-contract shrink                                T-0280 comment cleanup (needs T-0272 + regen)
        │ (security-gated)                                   T-0281 E2E siblings (needs T-0271 done)
        │
8B (fan out, concurrent with 8A — disjoint from the auth files)
  T-0273  paged canonicalization (backend; 5 disjoint feature groups, fan out)
  T-0274  FE error-resolver dedup (frontend; 8 files, 1–N devs)
  T-0275  dead-code + LOW drift (backend; 1 dev, serial)
  T-0276  sitewide-push facade (frontend; 1 dev)
  T-0277  Android formatters hoist  ─┐ SERIALIZE on :core
  T-0278  Android push-token hoist  ─┘ (never both editing :core at once)

BLOCKED (not in any runnable batch this wave):
  T-0279  admin-pay-config client — held on the IMP-3 admin nswag-regen (owner step, not yet done)
```

### Why T-0272 lands FIRST / alone
T-0272 **shrinks the wire contract** — `trustedDeviceToken` leaves the 3 web clients;
`RequiredProfile`/`RequiredAudience` leave all the refresh-bearing clients. After the owner regenerates,
the regen **ripples to client consumers other tickets touch** (the FE auth services — which T-0280 then
cleans, and which per the "build all three apps after regen" rule must be re-verified). Running T-0272's
regen in the **same** owner handoff as any other regen, and *before* T-0280, avoids a half-shrunk client
surface. T-0272 also touches the shared `LoginCommand` + `RefreshToken.Command` + 5 controllers + the
shared `LoginValidator` — the **most cross-cutting** auth surface in the wave — so it runs **alone in its
lane** (no other ticket edits `Features/Auth/*` concurrently).

### Why 8B fans out concurrently with 8A
The 8B tickets are **disjoint** from `Features/Auth/*` and from each other (per-feature backend
conversions, per-feature FE facades, Android `:core`). They have **no regen dependency** (T-0273 keeps
the `PagedData<T>` envelope; T-0274/T-0276 consume existing clients; T-0277/T-0278 are Android-internal).
So they run while T-0272 is in flight. The **only intra-8B serialization** is `:core` (T-0277 ↔ T-0278).

### Why 8C waits
- **T-0280** edits the FE auth services where the `trustedDeviceToken` explainers live — those comments
  describe a field T-0272 removes, so the cleanup must land **after** T-0272 + its regen.
- **T-0281** reuses T-0271's CI Playwright job + deterministic seed/boot foundation, so it needs
  **T-0271 `done`** first.

---

## 4. ⚠️ OWNER MANUAL-STEPS BUNDLE (run ONCE, after 8A — do NOT interleave)

Per `quality-gates.md` "Batch the owner-only handoffs": the regen-bearing tickets collect into **one fat
handoff**, not many thin ones. The PM holds dependent consumers until the owner confirms the **whole**
bundle, then re-verifies the merged tree once.

**BUNDLE B1 — after T-0272 lands (the only regen this wave):**
1. **nswag-regen — admin client** (`generate-admin-client`) — `trustedDeviceToken` leaves
   `admin-client.ts`; `RequiredProfile`/`RequiredAudience` leave the admin refresh command.
2. **nswag-regen — partner client** (`generate-partner-client`) — same fields leave `partner-client.ts`.
3. **nswag-regen — customer client** (`generate-customer-client`) — same fields leave `customer-client.ts`.
4. **nswag-regen — mobile clients** — IF the mobile-login contract change (T-0272 AC3) alters the
   generated mobile surface, regenerate the mobile-customer + mobile-partner clients too (the dev
   confirms the surface delta at review; the PM adds them to the bundle only if real).
5. **After the regen: run all three web prod builds** (`build:cleansia-{customer,partner,admin}`) and fix
   any stale `new LoginCommand({ trustedDeviceToken })` / refresh-command consumers **before** pushing
   (the recurring client-drift shape; the blocking prod-build CI catches it, but catch it locally).

**Then, and only then:** the PM releases **T-0280** (FE auth-service comment cleanup) and re-verifies.

**NOT in this bundle (separate, owner-owned, NOT a Wave-8 step):**
- **IMP-3 admin nswag-regen** — gates **T-0279** (held `blocked`). When the owner runs IMP-3's regen
  (or confirms IMP-3 won't touch the pay-config client surface), the PM unblocks T-0279 → `ready`.
- No EF migrations this wave. No schema change in any Wave-8 ticket.

**Standing owner items carried from prior waves (unchanged, for context — NOT Wave-8):** Mapbox key
rotation + Functions restart; the queued Wave-6 ef-migrations (T-0261/T-0237 PROD `CONCURRENTLY`); open
PRs to `master`; IMP-1 (Google OAuth, needs a Google Cloud project) + BUG-22 (email-badge CSS). Full
list: `sprint-9.md` §close-out.

---

## 5. Doc-tiering applied (per the adopted rule)

- **Load-bearing tickets get the full record:** **T-0272** (auth contract — the only `security_touching`
  ticket; full Context/AC/seam-options/manual-step record) and **T-0273** (the broad paged sweep + the
  reconciliation table + meta-finding). **T-0281** gets a fuller record because it expands an E2E layer.
- **Mechanical tickets get Context + Scope + AC only:** **T-0275** (dead-code delete), **T-0280**
  (comment-only), **T-0276**/**T-0277**/**T-0278** (pattern-alignment / dedup). Each carries a one-line
  **no-decision note** and skips the deliberation panel.
- **No deliberation panel convened** anywhere in Wave 8 — every finding is a mechanical canonicalization
  against an already-ratified rule/pattern, or a decision the owner already took (T-0272). No finding
  hid a real story/ADR decision. (ADR-0001 is in force for T-0272 and is **not** reopened — only the wire
  projection shrinks.)

---

## 6. Question triage

**No new blocking questions.** Wave 8 opens **zero** `pre-prod` blocking questions. Two latent flags the
implementer may raise (and stop, not guess):
- T-0272 / T-0281: if a CI secret (Stripe test-mode key) or a real owner-only step surfaces → flag
  `manual_steps`, do not self-provision.
- T-0278: if the Firebase-project migration constant is genuinely ambiguous (AC4) → the PM raises a
  question to the owner rather than the dev picking a project id.

`questions/open.md` pre-prod blocking index is unchanged (none open for the CZ/SK/PL launch).

---

## 7. Definition of Wave-8 done
All 9 runnable tickets (T-0272…T-0278, T-0280, T-0281) `done` with reviewer + (T-0272) security + qa
gates green and mechanical checks green on the merged tree; the owner regen bundle B1 confirmed + the
tree re-verified; `consistency-violations.md` F1b marked resolved (T-0273 tool-clean); INDEX + this doc
match reality. **T-0279 remains `blocked`** until the owner's IMP-3 regen — it does **not** gate Wave-8
close (it's an IMP-3-dependent follow-up that happens to have been found here).
