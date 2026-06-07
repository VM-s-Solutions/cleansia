# ADR-0007 — Soft-delete policy: Deactivate vs Remove, the S10 read obligation, null-FK handling, and the GDPR-anonymization boundary

- **Status:** accepted   <!-- proposed | accepted | superseded | rejected -->
- **Date:** 2026-06-06
- **Supersedes:** —
- **Superseded by:** —
- **Applies to:** backend | cross-cutting
- **Ratifies:** consistency rule **B6** (`agents/knowledge/consistency.md:67-72,158`) as platform policy
- **Ticket:** T-0152 (soft-delete ADR) · **Consumers:** T-0153 (SavedAddress), T-0154 (Device), T-0191 (CC-02/03/04/06, Wave-2)

> This ADR is **ADR-SOFT-DELETE**. It ratifies the B6 Architect judgment call as binding platform policy:
> when an entity is **soft-deleted** (`Deactivate`) vs **physically removed** (`Remove`), how reads treat
> deactivated rows, how null-FK band-aids are handled, and — explicitly — that **GDPR anonymization is a
> different operation from soft-delete**. It ships **no code**: SavedAddress is T-0153, Device is T-0154.
> Once `accepted` it is immutable — supersede, never edit.

---

## Context

The codebase deletes inconsistently. Consistency rule **B6** already canonicalizes deletion on
**soft-delete via `repo.Deactivate(entity)`** for user/business-facing entities, reserving
`repo.Remove(entity)` for true join/scratch rows (`agents/knowledge/consistency.md:67-72`), and flags
B6 as an **Architect-owned judgment call** whose platform-wide adoption "must be ratified as an ADR, not
ad-hoc" (`:158`). This ADR is that ratification. All facts verified (2026-06-06).

**The primitives already exist** — this ADR adopts a *policy*, it builds no primitive:
- `IRepository.Deactivate(entity)` / `DeactivateRange(...)` (`IRepository.cs:45,47`).
- `Auditable.Deactivated(by, on)` (`Auditable.cs:35-42`) sets `DeactivatedBy`, `DeactivatedOn`, and
  `IsActive = false` — a single auditable soft-delete primitive on every `Auditable` entity.
- `IRepository.Remove(entity)` / `RemoveRange(...)` (`IRepository.cs:41,43`) — hard delete.

**S10 is the load-bearing read rule** (`agents/knowledge/security-rules.md:161-168`):
`BaseEntity.IsActive` is the soft-delete flag and there is **NO global query filter** for it —
*intentional*, so admins can see all rows. **Therefore every read that should hide deactivated rows must
filter `Where(e => e.IsActive)` itself.** Soft-delete without the read filter is a silent bug: the row is
"deleted" but still resurfaces in the user's list. S10 also warns of the **`IsActive` overload** on
recurring templates, where `IsActive` is the user's *pause/resume* flag, not soft-delete — the two must
not be conflated.

**The four verified instances this ADR adjudicates (from T-0142):**
- **DA-10 — SavedAddress hard-deletes.** `DeleteSavedAddress.Handler` calls
  `savedAddressRepository.Remove(saved)` (`DeleteSavedAddress.cs:56`) on a user-facing entity — destroys
  the row and its history.
- **D-09 — SavedAddress repo has no soft-delete read filter.**
  `SavedAddressRepository.GetByUserAsync` (`SavedAddressRepository.cs:10-19`) returns **all** of a user's
  rows with no `IsActive` filter — so if SavedAddress moved to soft-delete today, deactivated addresses
  would still list (the exact S10 miss).
- **DA-15 — null-FK band-aid.** `GetSavedAddresses.Handler` filters `.Where(s => s.Address != null)`
  (`GetSavedAddresses.cs:22`) — silently hiding rows whose shared `Address` FK is null, masking a data
  defect rather than surfacing it.
- **IA-13 — Device hard-deletes.** `UnregisterDevice.Handler` calls `deviceRepository.Remove(device)`
  (`UnregisterDevice.cs:34`) — a push-token registration row.

**GDPR is a separate axis (the explicit boundary this ADR must draw).** The domain already has GDPR
*anonymization* — `Dispute.Anonymize()` (`Dispute.cs:110-123`) overwrites PII fields with an
`AnonymizationMarker` while **keeping the row**. Anonymization is **not** soft-delete: soft-delete hides
a row while preserving its data for audit/restore; anonymization keeps the row visible (for referential
and financial integrity) while destroying the *personal data* in it. Conflating them produces either a
GDPR violation (soft-deleting a row but leaving PII intact and exportable) or a data-integrity loss
(hard-deleting a financial row to satisfy a GDPR request). This ADR keeps them orthogonal.

This is **one decision** — "the deletion policy" — because the Deactivate/Remove rule, the S10 read
obligation it *requires*, the null-FK handling, and the GDPR boundary are inseparable: a soft-delete rule
without the read filter is a resurfacing bug; without the GDPR boundary it gets misused as a
GDPR-erasure mechanism (which it must never be).

---

## Decision

> **Policy principle.** Deletion of any **user- or business-facing** entity is **soft** — `Deactivate`
> (sets `IsActive = false` + `DeactivatedBy`/`DeactivatedOn` via `Auditable.Deactivated`). Physical
> `Remove` is reserved for **true join/scratch rows that carry no history and are never referenced**, and
> each such site is **documented at the call site** with the reason. Because there is **no** global
> `IsActive` filter (S10, deliberate), every "list mine"/default-lookup read MUST filter
> `Where(e => e.IsActive)` itself. **Soft-delete is NOT GDPR erasure**: a GDPR request is satisfied by
> *anonymization* (overwrite PII, keep the row for integrity) or by the GDPR delete flow — never by
> flipping `IsActive`.

### D1 — The Deactivate-vs-Remove decision rule (AC2)

A binary, applied per entity and documented per site:

- **`Deactivate` (soft-delete) — the default for any entity that is:**
  - **user- or business-facing** (a customer/employee/admin sees or owns it), OR
  - **referenced** by another row (history, audit, FK), OR
  - **carries history worth keeping** (financial, order, dispute, document, address, membership).
  Soft-delete preserves the row + `DeactivatedBy`/`DeactivatedOn` for audit and restore.

- **`Remove` (hard-delete) — ONLY for a row that is ALL of:**
  - a **true join/scratch row** (a pure many-to-many link, a transient cache/staging row, a one-shot
    token with no audit value), AND
  - **carries no history** anyone would ever need, AND
  - **is never referenced** by another row (no inbound FK), AND
  - has **no GDPR/audit obligation** attached.
  Each `Remove` call site carries a **one-line comment** stating which of these it satisfies (so a
  reviewer can confirm, not guess). An undocumented `Remove` on an `Auditable`/business entity is a
  blocking finding (verification #1).

This **ratifies** consistency rule **B6** verbatim and makes it ADR-binding: new deletes use
`Deactivate`; existing hard-deletes are reviewed case-by-case (this ADR ratifies the *policy* + the
*four named instances*; the program-wide sweep F9 is out of scope, per the ticket).

### D2 — The S10 read obligation (AC3)

Because there is **no** global `IsActive` query filter (S10 — intentional, admins see all):

- **Every "list mine" / default-lookup / catalog read that should hide deactivated rows MUST apply
  `Where(e => e.IsActive)` in the query.** A soft-delete that is not paired with the read filter is
  **incomplete** — the row stays "deleted" but resurfaces. This is the D-09 gap: moving SavedAddress to
  soft-delete *requires* adding `.Where(s => s.IsActive)` to `GetByUserAsync` and `GetDefaultForUserAsync`
  in the **same** ticket (T-0153).
- **Admin/oversight reads deliberately omit the filter** (the reason there is no global filter) — and say
  so with a one-line comment, so the omission reads as intentional, not a miss.
- **Do not add a global `IsActive` query filter** — it would break the admin-sees-all invariant S10
  documents and the existing admin list queries that rely on seeing deactivated rows. The obligation is
  per-read, by design.
- **Never conflate `IsActive`-as-soft-delete with `IsActive`-as-pause/resume** (recurring templates,
  S10). If an entity needs *both* a pause flag and a soft-delete, it gets a **separate** column (e.g.
  `DeactivatedOn`-driven soft-delete distinct from a domain `IsPaused`); the soft-delete is keyed on
  `DeactivatedOn`/the `Deactivated(...)` primitive, not on a reused domain flag.

### D3 — Null-FK classification (AC4 — DA-15) — a binding decision T-0153 implements

The `GetSavedAddresses.cs:22` `.Where(s => s.Address != null)` band-aid is **classified: SURFACE + LOG,
not silently retained.**

- A `SavedAddress` with a null `Address` is a **data defect** (a saved address pointing at a deleted/
  missing shared `Address` row — related to the orphaned-`Address` concern DA-16). Silently filtering it
  hides the defect from both the user (their address vanishes with no signal) and ops.
- **T-0153 binding:** replace the silent `.Where(s => s.Address != null)` with one of, in priority order:
  1. **Make it impossible** — if `Address` is a required relationship for `SavedAddress`, enforce the FK
     so a null cannot exist (preferred; eliminates the band-aid). If a `SavedAddress` is soft-deleted
     together with logic that should also handle its `Address`, that linkage is defined in T-0153.
  2. **If a null can legitimately occur**, **log a Warning** ("SavedAddress {Id} has null Address —
     orphaned, hidden from list") and keep it out of the user list — so the defect is **observable**, not
     swallowed. The `.Where` stays only behind a logged, justified comment, not as a bare filter.
- DA-16 (orphaned shared `Address` rows) is the upstream fix and is **out of scope** here (separate
  ticket per T-0152); this ADR only forbids the *silent* band-aid and requires surfacing.

### D4 — The GDPR-anonymization boundary (explicit — soft-delete ≠ erasure)

Three **distinct** operations; this ADR keeps them orthogonal and names which applies when:

| Operation | What it does | When | Row after |
|---|---|---|---|
| **Soft-delete (`Deactivate`)** | `IsActive = false` + `Deactivated(by, on)`; data **intact** | user "deletes" their address/device; admin deactivates a catalog item | hidden from filtered reads, restorable, fully audit-visible |
| **GDPR anonymization (`Anonymize()`)** | overwrite **PII fields** with `AnonymizationMarker`; row + non-PII **kept** | GDPR erasure request on a row with **financial/referential integrity** to preserve (orders, disputes, receipts) | visible, but personal data destroyed (irreversible) |
| **Hard-delete (`Remove`)** | row physically gone | true join/scratch only (D1) | gone |

Rules:
- **A GDPR "delete my account" request is satisfied by anonymization + the GDPR delete flow, NOT by
  `Deactivate`.** Soft-deleting leaves PII intact and still exportable — a GDPR violation. The GDPR flow
  (already present via `Anonymize()` on disputes, and the `CanDeleteOwnAccount`/`CanAdminDeleteUserAccount`
  permissions in ADR-0001) is the erasure mechanism; soft-delete is an *operational* hide/restore, not a
  *legal* erasure.
- **Conversely, a GDPR request must NOT hard-`Remove` a row carrying financial/fiscal/referential
  obligation** (an order, receipt, invoice, dispute) — those are anonymized, not deleted, so the
  financial record and its links survive (the `Anonymize()` pattern). This is the integrity half of the
  boundary.
- **`AnonymizationMarker`-bearing entities** (e.g. `Dispute.Anonymize()`) are the reference shape; new
  PII-bearing entities that may receive a GDPR request implement an `Anonymize()` that overwrites PII and
  keeps the row, **separately** from any soft-delete.
- **Where the two meet:** a user "deletes" their `SavedAddress` (soft-delete, restorable); a user
  "deletes their account" (GDPR — anonymize PII-bearing financial rows, hard-delete pure scratch like
  device tokens, and run the GDPR delete flow). The two flows are **not** interchangeable.

### D5 — Per-entity verdicts (AC5)

- **`SavedAddress` → `Deactivate` (soft-delete).** It is user-facing, owned, and history-worthy (a user
  may want it back; it links to a shared `Address`). **T-0153 surface:** change `DeleteSavedAddress.cs:56`
  `Remove` → `Deactivate`; add `.Where(s => s.IsActive)` to `SavedAddressRepository.GetByUserAsync`
  (`:10-19`) and `GetDefaultForUserAsync` (`:21-27`) (D2); fix the DA-15 null-FK band-aid (D3);
  `manual_step: ef-migration` only if a new column is needed (the `Auditable.DeactivatedOn` columns
  already exist on `SavedAddress` if it derives `Auditable` — T-0153 confirms and flags the migration if
  not). On GDPR account-deletion, a `SavedAddress` (PII: street/address) is anonymized or hard-deleted as
  part of the GDPR flow, **not** via this soft-delete path (D4).
- **`Device` → `Deactivate` (soft-delete), NOT scratch-row `Remove`.** A `Device` is a push-token
  registration tied to a user. **Verdict: soft-delete.** Reasoning: although a stale push token looks
  "scratch-like," (a) it is **referenced** conceptually by the notification path, (b) `DeactivatedOn`
  gives ops a record of *when* a device unregistered (useful for "why did pushes stop"), and (c)
  re-registration should *reactivate* rather than create churn. A hard `Remove` throws away that signal.
  **T-0154 surface:** change `UnregisterDevice.cs:34` `Remove` → `Deactivate`; ensure the
  device-lookup-for-send and registration reads filter `IsActive` (D2) so a deactivated token is not
  pushed to and a re-register reactivates. *(If T-0154's deeper investigation finds `Device` is genuinely
  a throwaway with a unique `(UserId, DeviceId)` re-register that always overwrites and no ops value in
  the unregister timestamp, the documented scratch-row `Remove` carve-out (D1) is the fallback — but the
  default verdict is `Deactivate`, and choosing `Remove` requires the D1 per-site justification comment.)*

### D6 — Scope guard

This ADR ratifies the policy + the **four named instances** only. The program-wide hard-delete sweep
(F9), DA-16 (orphaned shared `Address`), and DA-17 (web saved-address UI) are **out of scope** (per the
ticket) — other `Remove` sites are reviewed case-by-case later against D1.

---

## Alternatives considered

- **Keep hard-delete as the default (status quo).** Rejected: it destroys audit trails and history a
  production platform needs, and it makes GDPR-traceable deletion (prove what was erased and when)
  impossible. B6 already named soft-delete the long-term-correct default; this ratifies it.
- **Add a global `IsActive` query filter (like the tenant filter) so reads auto-hide deactivated rows.**
  Rejected: it breaks the **admin-sees-all** invariant S10 documents — admin/oversight lists depend on
  seeing deactivated rows, and a global filter would silently hide them (or force `IgnoreQueryFilters()`
  everywhere admin reads, the opposite mess). The per-read `Where(IsActive)` obligation (D2) is the
  deliberate trade-off: explicit at each read, admin reads omit it on purpose.
- **Use soft-delete (`Deactivate`) to satisfy GDPR erasure.** Rejected as a category error: soft-delete
  keeps PII intact and exportable — a GDPR violation. D4 keeps anonymization (PII overwrite, row kept) and
  soft-delete (row hidden, data kept) as separate operations.
- **Hard-`Remove` PII-bearing financial rows on a GDPR request.** Rejected: destroys financial/fiscal
  referential integrity. D4 anonymizes those rows instead (the existing `Anonymize()` pattern).
- **Silently keep the `.Where(s => s.Address != null)` band-aid (DA-15).** Rejected: it masks a data
  defect from both user and ops. D3 requires making it impossible or surfacing it with a logged Warning.
- **`Device` → hard-`Remove` (treat as pure scratch).** Considered (the AC5 alternative). Rejected as the
  default: the unregister timestamp and reactivate-on-re-register behavior have ops value; `Remove` throws
  the signal away. Left as a documented carve-out only if T-0154 proves no value (D5).

---

## Consequences

**Cheaper / safer:**
- Deletions preserve audit trails and are restorable; "who deleted what, when" is on every row
  (`DeactivatedBy`/`DeactivatedOn`).
- GDPR erasure and operational delete are **separate, correct** mechanisms — neither can be misused as
  the other (no PII-left-behind, no integrity-destroyed).
- Data defects (null FKs) become observable instead of silently swallowed.

**More expensive (new obligations on developers):**
- Every new delete uses `Deactivate`; a `Remove` requires the D1 per-site justification comment.
- Every "list mine"/default read on a soft-deletable entity MUST carry `.Where(e => e.IsActive)`; an
  admin read that omits it MUST say so (D2). A missing filter is a blocking finding.
- A null-FK filter MUST be justified + logged, never a bare silent `.Where(... != null)` (D3).
- PII-bearing entities subject to GDPR implement `Anonymize()`; soft-delete is never the GDPR answer (D4).
- **`manual_step: ef-migration` (owner-only)** if a soft-delete adds columns (most `Auditable` entities
  already have `DeactivatedOn`/`DeactivatedBy`; T-0153/T-0154 confirm and flag if not). **No NSwag change**
  (delete endpoints' DTO contracts are unchanged — they still return success).

**Rollout (consumers, each test-first):**
- **T-0153 (SavedAddress):** `Remove`→`Deactivate`; read filters; null-FK fix; migration if needed.
- **T-0154 (Device):** `Remove`→`Deactivate` (or documented carve-out); read filters.
- **T-0191 (CC-02/03/04/06, Wave-2):** references this ADR for its own delete/read decisions.

---

## How a reviewer verifies compliance

**Mechanical (the gate; candidates for `agents/tools/check-consistency.mjs`):**
1. **No undocumented `Remove` on a business/`Auditable` entity.** Grep `repo.Remove(` / `.Remove(` in
   `Features/**` handlers → each match is either (a) on a documented true-scratch row (D1 comment present)
   or (b) a blocking finding. `DeleteSavedAddress.cs:56` and `UnregisterDevice.cs:34` must become
   `Deactivate` after T-0153/T-0154.
2. **Soft-deletable reads filter `IsActive`.** For each repo read that returns user/business rows
   (`GetByUserAsync`, default lookups, catalog lists), assert `Where(... IsActive)` is present **or** a
   one-line "admin sees all — intentional" comment. `SavedAddressRepository.GetByUserAsync` must gain it.
3. **No global `IsActive` query filter** was added (it would break admin-sees-all) — assert none exists.
4. **No silent null-FK band-aid.** A `.Where(x => x.SomeFk != null)` in a handler must carry a logged
   Warning + justification comment (D3); a bare silent filter is a finding.
5. **GDPR ≠ soft-delete.** The account-deletion / GDPR flow does not call `Deactivate` as its erasure
   step; PII-bearing financial rows are `Anonymize()`d, not `Remove`d (D4).

**Test contract (consumer tickets, red first):**
6. **TC-SOFTDELETE-0.** Deleting a `SavedAddress`/`Device` sets `IsActive = false` (row survives,
   `DeactivatedOn` set); a subsequent "list mine" does **not** return it; an admin/oversight read still
   can (where applicable).
7. **TC-NULLFK-0.** A `SavedAddress` with a null `Address` is logged + excluded (not silently dropped).
8. **TC-GDPR-BOUNDARY-0.** A GDPR erasure anonymizes PII-bearing rows (row kept, PII gone) and does not
   merely flip `IsActive`.

---

## Roles affected

Role files in `agents/knowledge/roles/`:
- **`soft-delete-policy.md`** (new, cross-cutting CRC) — *responsibility:* define, once for the platform,
  when a delete is `Deactivate` vs `Remove`, and require the paired `IsActive` read filter.
  *Collaborators:* `IRepository.Deactivate/Remove`, `Auditable.Deactivated`. *Does NOT know:* GDPR erasure
  semantics (that is the GDPR/anonymization role), the pause/resume domain flag (a different concept), or
  any specific entity's business rules.
- **`gdpr-anonymizer.md`** (new/clarified) — *responsibility:* overwrite a row's PII with
  `AnonymizationMarker` while preserving the row for referential/financial integrity. *Collaborators:*
  the PII-bearing entity's `Anonymize()`, the GDPR delete flow. *Does NOT know:* soft-delete `IsActive`
  state (orthogonal — an anonymized row may be active or inactive independently).

Catalog edit (same change): `agents/knowledge/consistency.md §B6` gains "ratified by ADR-0007" and the
GDPR-boundary clarification; `agents/knowledge/security-rules.md §S10` cross-references ADR-0007 (the
per-read filter obligation and the no-global-filter rationale) and the GDPR-≠-soft-delete boundary.

---

## Challenge / Defense / Verdict trail (condensed)

Author drafted; challengers (security/GDPR, pragmatic, data-integrity) attacked; the Lead re-verified
every citation against the real code and adjudicated. **Verdict: all challenges RESOLVED; zero blocking;
consensus reached.**

| # | Challenge (severity) | Disposition | Where |
|---|---|---|---|
| CH-1 (GDPR) | Soft-delete used as GDPR erasure leaves PII intact + exportable → GDPR violation; the ADR must forbid it explicitly (CRITICAL) | CONCEDE + REVISE | D4 boundary table + rules; soft-delete is never the GDPR answer |
| CH-2 (integrity) | A GDPR request hard-deleting a financial/fiscal row destroys referential integrity → use anonymization (MAJOR) | CONCEDE + REVISE | D4 anonymize PII-bearing financial rows (the existing `Anonymize()` pattern) |
| CH-3 (S10) | Soft-delete without the read filter is a *resurfacing bug* — the move must ship the `Where(IsActive)` in the same ticket (MAJOR) | CONCEDE + REVISE | D2 read obligation; D5 binds T-0153 to add the filters; D-09 named |
| CH-4 | Proposing a global `IsActive` filter would break admin-sees-all (S10's reason for no global filter) (MAJOR) | DEFEND + REVISE | D2 explicitly forbids a global filter; per-read obligation is the deliberate trade-off |
| CH-5 (overload) | `IsActive` is *pause/resume* on recurring templates, not soft-delete — conflating them corrupts both (MAJOR) | CONCEDE + REVISE | D2 separate-column rule; soft-delete keyed on `Deactivated(...)`/`DeactivatedOn`, not a reused domain flag |
| CH-6 | Silently retaining `.Where(s => s.Address != null)` masks a real data defect (MODERATE) | CONCEDE + REVISE | D3 surface+log, make-impossible-or-justify |
| CH-7 | `Device` looks like scratch — is soft-delete over-engineering? (MODERATE) | DEFEND | D5 soft-delete default (unregister-timestamp + reactivate value), documented `Remove` carve-out only if T-0154 proves no value |

**Affirmed unchallenged:** B6's Deactivate-default; `Remove` reserved for true scratch with a per-site
comment; the primitives (`Deactivate`/`Auditable.Deactivated`) are sufficient (no new primitive needed);
SavedAddress → soft-delete.

**Lead re-verification (against current code):** `IRepository.cs:41,43,45,47` Remove/Deactivate present;
`Auditable.cs:35-42` `Deactivated` sets `IsActive=false`+timestamps; `security-rules.md:161-168` S10 no
global `IsActive` filter + recurring-template overload; `DeleteSavedAddress.cs:56` `Remove` (DA-10);
`SavedAddressRepository.cs:10-19` no `IsActive` filter (D-09); `GetSavedAddresses.cs:22` null-FY band-aid
(DA-15); `UnregisterDevice.cs:34` `Remove` (IA-13); `Dispute.Anonymize()` (`Dispute.cs:110-123`)
overwrite-PII-keep-row pattern.

**Escalations to the owner:** none. The Deactivate/Remove rule and the GDPR boundary are
architecture/compliance-pattern decisions within the Architect's mandate; no product/legal window is
invented here (GDPR *mechanism* is fixed; GDPR *retention windows*, if ever needed, are a separate owner
question, not raised by this ADR).
