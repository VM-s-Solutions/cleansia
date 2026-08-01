# Open Questions — escalation inbox

Any agent appends a question here when it needs an owner decision. The PM surfaces `blocking: yes`
entries at the next checkpoint. When the owner answers, the entry moves to `answered.md` and the
decision is locked into the relevant artifact (ADR / story / charter) so it's never re-asked.

## Triage discipline (no question stays open without an owner AND a deadline)

`blocking: yes/no` alone is not enough — a "no" question with no deadline drifts silently and becomes a
late surprise (this happened: a question sat open weeks past the wave it belonged to). So **every** open
question carries:
- **Owner** — who decides: `owner` for a business/legal/product/money call; an **agent** for a technical
  default the owner only ratifies.
- **Resolve-by** — a deadline bucket: **`pre-prod`** (must be answered before going to production) |
  **`post-prod`** (a v1.x refinement) | **`backlog`** (nice-to-have). A question with no Resolve-by may
  not stay in this file.

The **Pre-prod blocking index** below lists *only* the `pre-prod` questions so nothing go-live-critical
hides in a long file. The PM re-surfaces every still-open `pre-prod` question at every checkpoint, and
each gets a line on the pre-PROD readiness checklist.

### Pre-prod blocking index (the only questions that block go-live)
<!-- PM keeps this list in sync: one line per OPEN question whose Resolve-by is `pre-prod`. -->
- _(Q-REFUND-01 is `pre-prod` but scoped to DE/AT/ES go-live only, not the CZ/SK/PL launch)_
- _(Q-AUDIT-01 — RESOLVED 2026-06-22: owner adopted the append-only / no-auto-delete / PII-minimized
  **default**; moved to `answered.md`. The pre-prod **ratification** of the exact retention window +
  redaction list is now a **pre-PROD readiness-checklist item**, not an open question.)_
- **Q-INFRA-01** (`pre-prod` for PROD only; non-blocking for the DEV provision) — custom domain for the
  environments? Default: no custom domain for dev (stable `*.azurewebsites.net`).
- **Q-INFRA-03** (`pre-prod` for PROD hardening; non-blocking for the DEV provision) — prod VNet/private-endpoint
  + Postgres-MI auth? Default for dev: public-endpoint + firewall + MI-to-KeyVault/Storage.
- **Q-IOS-04** (`pre-submission` — gates only the SIWA iOS ticket T-0326; non-blocking for the rest of the iOS
  plan) — the Sign-in-with-Apple backend integration mechanism (likely a backend `appleauth` endpoint).
- _(**Q-I18N-02 — ANSWERED 2026-08-01**; the owner chose the verb-only label + truncate-don't-wrap.
  Moved to `answered.md`. It was the last `blocking: yes` entry in this file. **T-0450 is now `ready`**
  and T-0448/T-0449 clear when it lands.)_
- **Q-BRAND-01** (`pre-prod`, blocking: no) — Poppins covers **0/98** Cyrillic code points on all three
  platforms (byte-identical binaries). Strategy for `ru`/`uk` headings. **Untouched by the Q-I18N-02
  answer** — a shorter Russian string still renders in a system fallback face. Now carried by its own
  ticket **T-0472** (split out of T-0450 on 2026-08-01); T-0472 fixes the mobile profile hero and its
  architect panel rules on the mechanism, the platform-wide remediation stays open here.
- **Q-PROFILE-01** (`pre-prod`, **blocking: yes**) — `UpdateCurrentUser` requires a client-supplied
  `Command.Id` that the customer **web** app cannot obtain (no `id` on `MyProfileDto`; HttpOnly-cookie
  session, so no JWT to decode as Android/iOS do). Every customer-web profile save 400s with
  `user.not_allowed_to_update`. Pre-existing since `29de7b48`; raised by T-0447, which it blocks for
  AC2/AC3 round-trip evidence. Needs a **backend** decision, not a frontend workaround.

**One `blocking: yes` question is open in this file as of 2026-08-01: Q-PROFILE-01.**

Format:

```
### Q-NNNN — [blocking: yes|no] <short title>
- Raised by: <agent> (<ticket id>)
- Owner: owner | <agent>
- Resolve-by: pre-prod | post-prod | backlog
- Date: YYYY-MM-DD
- Question: <the precise decision needed>
- Why it matters: <the lasting consequence of getting it wrong>
- Default taken (if non-blocking): <the defensible assumption proceeded with>
- Answer: _(owner fills in)_
```

---

_(Q-0001…Q-0005 and Q-RATELIMIT-01/02/03 all answered 2026-06-01; see `answered.md`. Key outcomes:
staff dispute replies = Admin-only (ADR-0001); prod proxy = 1 hop, `ForwardLimit=1`, rate-limit cleared
for prod (ADR-0003); Wave 0 ships rate-limit per-IP-only with BSP-4b as a fast-follow.)_

---

## Wave-1 planning questions (2026-06-05) — see `status/sprint-3.md`

> **All four Wave-1 planning questions (Q-W1-1…Q-W1-4) were answered by the owner on 2026-06-05 and
> moved to `answered.md`.** Outcomes: Q-W1-1 — Wave 0 is CLOSED (T-0230 reconciled to `done`; does NOT
> gate Wave 1). Q-W1-2 — both L-splits authorized (T-0142 → T-0152/T-0153/T-0154; T-0143 →
> T-0155/T-0156/T-0157/T-0158; architect owns the ADR-0002 D1.3 decision in T-0155). Q-W1-3 — BLIND-2
> filed into Wave 1 as **T-0159**. Q-W1-4 — author ADR-REFUND (T-0140) now in Batch 1A.

_No open Wave-1 *planning* questions remain._

---

## Refund money-path questions (2026-06-06) — raised by ADR-0006 (T-0140)

### Q-REFUND-01 — [blocking: yes — for DE/AT/ES go-live only; NOT for CZ/SK/PL launch] Per-country corrective fiscal document on refund/cancellation of a registered sale
- Raised by: architect (T-0140 / ADR-0006 D6)
- Date: 2026-06-06
- Question: When a refund or cancellation reverses a **fiscally-registered** sale, does each
  **BlockingOnline** country (DE TSE / AT RKSV / ES VeriFactu) legally require a **corrective fiscal
  document** (cancellation/credit registration with the tax authority), and in what form? CZ/SK/PL today
  are `None`/`AsyncBackground` with no gapless/corrective requirement, so this does not block their launch.
- Why it matters: A refund that silently skips a legally-required corrective registration is a
  **compliance incident** in a BlockingOnline regime — the same class ADR-0004 guards for receipts. The
  refund seam (ADR-0006) carries a `Refund.ReceiptId` link and is ready to ride ADR-0004's existing fiscal
  retry/reconciliation machinery; only the per-country **rule** is missing.
- Default taken (non-blocking for CZ/SK/PL): the `Refund` row records the corrective obligation but does
  not register a corrective document (no live BlockingOnline country). The TC-FISCAL-CORRECTIVE test is
  gated on this answer.
- Gating: **bound to ADR-0004's existing DE/AT/ES go-live gate** — must be answered + implemented before
  any BlockingOnline country goes live in production.
- Research (2026-06-06, Claude — web sources below): **YES, all three BlockingOnline regimes legally
  require a corrective fiscal registration; a refund/cancellation of a registered sale may NOT silently
  skip it.**
  - **DE (KassenSichV/TSE):** a TSE-signed sale can NOT be voided. A *separate* cancellation receipt
    (Stornobeleg, `BON_STORNO=1`, reverse/voided ReceiptCaseFlag) with reversed amounts must be created
    and independently TSE-signed. (fiskaltrust FAQ.)
  - **ES (VeriFactu / RD 1007/2023, mod. RD 254/2025):** records are immutable + chained; a correction
    requires a new chained billing record — either a *registro de anulación* linked to the original, or a
    *factura rectificativa* (own series, references the original), which explicitly covers refunds
    (devoluciones). Each carries its own SHA-256 hash in the chain. (BOE / AEAT FAQ.)
  - **AT (RKSV):** same model as DE — the signed QR receipt chain means a reversal is itself a signed
    "Storno" receipt linked into the chain; it can NOT be an untracked void. (efsta / RKSV overview.)
  - **Implication for ADR-0006/0004:** the refund seam must, for a BlockingOnline country, register a
    corrective fiscal document via the SAME ADR-0004 claim-before-register + retry/reconciliation
    machinery the receipt flow uses (the `Refund.ReceiptId` link is already in place). The exact document
    *type/series* per country (Storno vs rectificativa vs anulación) is an implementation detail of the
    go-live ticket, not a blocker for CZ/SK/PL (`None`/`AsyncBackground`, no corrective requirement).
  - Sources: fiskaltrust DE-cancellation FAQ; marosavat/getrenn VeriFactu 2026 guides; BOE RD 1007/2023
    (BOE-A-2023-24840) + Orden HAC/1177/2024; AEAT "registros de facturación: anulación" FAQ; efsta RKSV
    overview.
- Answer (owner, 2026-06-06): **CONFIRMED.** Adopt the researched requirement — the refund seam MUST
  register a corrective fiscal document per BlockingOnline country (DE Stornobeleg / ES rectificativa or
  anulación / AT Storno) via the ADR-0004 machinery before DE/AT/ES go-live. No change for CZ/SK/PL.
  Locked into the superseding **ADR-0009** `0009-refund-policy.md` D7 (the next free ADR number was 0009,
  not 0007 — 0001-0008 already exist; 0007=soft-delete, 0008=outbox-table). **RESOLVED.**

### Q-REFUND-02 — [blocking: no] Refund-policy windows (time limit / partial rules / who bears the Stripe fee)
- Raised by: architect (T-0140 / ADR-0006 D6 / Alternatives)
- Date: 2026-06-06
- Question: What are the product refund-policy rules — time window in which a refund may be issued,
  partial-refund eligibility, and whether the platform or the customer bears the non-refunded Stripe
  processing fee on a refund?
- Why it matters: These shape the **amount** a caller computes (`RefundRequest.Amount`) and the admin UX
  (AUD-01). Getting it wrong is a money/CX issue, not a correctness one.
- Default taken (non-blocking): ADR-0006 makes the amount a **caller input** and hard-codes **no** policy;
  the cancel path keeps its existing fee computation (`order.Cancel(...)`). The seam supports any answer,
  so Wave-2 implementation proceeds on the default and tightens when the owner answers.
- Owner direction (2026-06-06): (a) refund window is **14 days** — but VERIFY against code first;
  (b) **partial / per-service refunds wanted** (e.g. cleaner skipped one service → refund that service);
  (c) Stripe-fee bearer = **open**, owner wants a recommendation.
- Codebase findings (2026-06-06, Claude — verified):
  - **No 14-day refund window exists.** `BookingPolicy` (Features/Orders/BookingPolicy.cs) only models
    PRE-cleaning **cancellation** (oops-window, 24h free / 4h 25% / <4h 50%). There is no post-completion
    refund concept anywhere.
  - **`CancelOrder` BLOCKS completed/in-progress orders** (returns OrderAlreadyCompleted /
    OrderInProgressCannotCancel) — so today there is NO path to refund a Completed order at all.
  - **Refund is full-order only:** `refundAmount = TotalPrice * (1 - feeRate)`. No partial / per-service
    refund exists. The Stripe call is the inline un-keyed one ADR-0006 migrates onto IRefundService.
  - ⇒ The owner's scenario ("refund 1 service on a completed order") is NEW functionality the current
    code actively prevents — a post-completion partial refund, distinct from cancellation.
- Status: panel (analyst + architect + PM, 2026-06-06) delivered the recommendation; owner answered all
  four residual decisions. Locked into the superseding **ADR-0009** `0009-refund-policy.md` (ADR-0006 stays
  accepted/immutable; the next free ADR number was 0009, not 0007 — 0001-0008 already exist).
- Answer (owner, 2026-06-06) — **RESOLVED**, all four:
  1. **Window = 14 calendar days**, soft, anchored to `Order.CompletedAt`, admin-overridable with a
     recorded reason, null-anchor closed-by-default, chargeback path exempt.
  2. **Stripe fee:** PLATFORM ABSORBS on service-fault refunds (customer gets the full allocated amount);
     deduct ONLY on pure change-of-mind goodwill. Requires the new `RefundReason.ServiceNotRendered`
     value so fault-vs-goodwill is mechanically decidable.
  3. **Loyalty clawback on partial refunds = YES, proportional** (revoke `floor(refundNet/10)` per refund,
     capped at original earn, anonymous/legacy skipped) — needs a NEW `ILoyaltyService` partial-revoke
     method with a per-refund idempotency key (the existing full-mirror revoke no-ops on a second call).
  4. **FUND per-included-service package pricing** (owner override of the panel's v1 whole-package-only
     limit): a single service bundled inside a package MUST be independently refundable. This is the
     deliberate long-term-win schema/pricing change — packages get a per-included-service price basis so
     the share-of-`TotalPrice` allocator can target one bundled line. Larger schema work; sequenced as
     its own epic that the partial-refund build depends on.
  Allocation formula (share of frozen `Order.TotalPrice`), VAT apportionment, refundable ceiling, cash
  guard, dispute linkage, admin-only issuance, `PaymentStatus.PartiallyRefunded` — all panel-settled.
  **Locked into superseding ADR-0009 `0009-refund-policy.md` (2026-06-06).** (Note: the next free ADR
  number was 0009, not 0007 — 0001-0008 already exist.)

### Q-REFUND-03 — [blocking: no] Per-bundle business weighting of legacy packages for per-included-service pricing
- Raised by: architect (T-0140 / ADR-0009 D5)
- Date: 2026-06-06
- Question: ADR-0009 decision #4 adds `PackageService.PriceWeight` so the bundled `Package.Price` is split
  across a package's included services, giving each a gross the partial-refund allocator can target. The
  data migration backfills **even weights** (every included service = equal share) for existing packages.
  For any specific live bundle where the included services are NOT of equal value, what relative weights
  should they carry?
- Why it matters: the even-split default is mechanically safe and reversible, but a refund of one bundled
  service from a bundle whose services are unequally valued would refund the wrong amount until the weights
  are corrected. This is a pricing/product call, not an architecture one.
- Default taken (non-blocking): ship the even-weight backfill in **T-0231** (AUD-02p1, the db+backend split
  child). The owner sets per-bundle weights via the admin package-pricing UI in **T-0232** (AUD-02p2) after
  T-0231 lands; no per-bundle business weighting is invented in the ADR or the migration.
- Wave-2 status (2026-06-07): this is the **only open Wave-2 question** (Q-REFUND-01/02 resolved in ADR-0009).
  It does **not** block starting Wave 2 — T-0231 ships even-split. The owner should, before any DE/AT/ES or
  high-value-bundle refund goes live, either (a) confirm even-split is acceptable for all current bundles, or
  (b) set real weights via T-0232. AUD-02p is now split: weighting capability = T-0232, schema/backfill = T-0231.
- Answer: _(owner fills in — set per-bundle weights via the admin UI in T-0232 post-T-0231, or confirm
  even-split is acceptable for all current bundles)_

---

## Wave-3 planning questions (2026-06-09) — raised by PM sequencing Wave 3 (`status/sprint-5.md`)

> Wave 3 = the admin-feature block T-0170…T-0195 (26 tickets). The refund seam (T-0161) + seam
> migration (T-0164) that gated T-0170/T-0173 are now `done` (merged 8ff35d49, PR #75), so those two
> are unblocked. One genuine **pre-build owner decision** surfaced; everything else is an architect/PM
> call at contract-lock. The carry-forward owner action items in §3 of sprint-5 are owner *tracking*
> items, not blocking questions, and are listed there.

### Q-W3-1 — [blocking: yes — gates T-0191 sub-(d) CC-06 only; NOT the rest of T-0191 or Wave 3] Default-language policy for catalog translations
- Raised by: pm (T-0191 / finding CC-06, the ticket's own AC7)
- Date: 2026-06-09
- Question: `Language` has only `Code`/`Name` (no `IsDefault`, unlike `Currency`/`Country`), and the
  Service/Package validators require a translation for **all 5** languages (`CreateService.cs:67-74`)
  with no designated fallback. T-0191 AC7 makes this an explicit `blocking: yes` precondition: choose
  **(a)** introduce `Language.IsDefault` + a `SetDefaultLanguage` flow + relax the
  all-languages-required validator to a fallback rule, **or** **(b)** formally document translations as
  mandatory-for-all and define add-a-language behavior (no `Language.IsDefault` column).
- Why it matters: path (a) is a schema change (new column → owner ef-migration) plus a validator
  semantics change; path (b) is a doc/validator-rule change with no migration. Building the wrong one
  is rework on a money-adjacent catalog surface. The other three CC findings in T-0191 (CC-02 in-use
  guard, CC-03 activate/deactivate, CC-04 set-default-currency) are **not** gated on this — only the
  CC-06 sub-ticket (T-0191 split-(d)) is.
- Default taken (non-blocking for the rest of T-0191): the PM holds **only** the CC-06 sub-work
  (T-0191 split-(d)); CC-02/CC-03/CC-04 (splits a/b/c) proceed independently once T-0142's soft-delete
  ADR gate is confirmed (it is — children `done`). No CC-06 schema/code lands before the owner answers.
- Answer: **(b) — translations mandatory for all active languages, no `Language.IsDefault` column, no
  ef-migration** (owner, 2026-06-09). CC-06 documents catalog translations as required for every active
  language; the existing all-languages-required validators (`CreateService.cs:67-74`, package equivalents)
  STAY and are the enforcement. Define add-a-language behavior: when an admin adds a new active language,
  existing catalog items are flagged **incomplete / needs-translation** until a translation is supplied
  (they are not auto-filled and there is no fallback). No `Language.IsDefault`/`SetDefaultLanguage` work.
  CC-02/CC-03/CC-04 were never gated on this and proceed regardless.
- _(superseded answer placeholder removed)_ _(owner fills in — choose (a) Language.IsDefault + fallback, or (b) mandatory-all + documented
  add-a-language behavior)_

### Q-W3-2 — [blocking: no] Currency on the partner "my period pay" summary
- Raised by: frontend (T-0171e)
- Date: 2026-06-10
- Question: `PeriodPaySummaryDto` / `OrderEmployeePayDto` carry no currency code (unlike
  `EmployeeInvoiceDto.currencyCode`). The new partner web "My Pay" screen displays amounts with a
  hardcoded `Kč` suffix, mirroring the existing partner dashboard earnings precedent
  (`dashboard.facade.ts` "… Kč"). Should the backend add a `CurrencyCode` to `PeriodPaySummaryDto`
  (DTO change → nswag-regen) so partner pay surfaces stop hardcoding the currency?
- Why it matters: when a non-CZK tenant/market launches, every partner pay surface that hardcodes
  `Kč` shows wrong currency; fixing it then touches the DTO, three clients (web/Android partner +
  admin), and the screens at once.
- Default taken (non-blocking): hardcoded `Kč`, consistent with the existing partner dashboard
  earnings display.
- Answer: _(owner fills in)_

### Q-W3-3 — [RESOLVED 2026-06-21 — not blocking] PdfGenerationFailed / PdfGenerationError missing from admin invoice DTOs
- Raised by: frontend (T-0171d)
- Date: 2026-06-10
- Question: AC4 requires the admin invoice list/detail to *show* `PdfGenerationFailed` +
  `PdfGenerationError`, but neither `EmployeeInvoiceDto` nor `EmployeeInvoiceDetailDto`
  (`Features/EmployeePayroll/DTOs/*`) exposes those domain fields (`EmployeeInvoice.cs:46-51`), so the
  regenerated admin client cannot carry them. Should the backend add both fields to the two DTOs (+
  mappers) so the UI can render the explicit failed state and error text? Requires backend DTO change
  → **manual_step: nswag-regen** before the frontend can finish the display half of AC4.
- Why it matters: without the flag the UI can only infer "no PDF yet" from an empty `pdfBlobName`; a
  failed generation and a still-pending generation look identical, and the stored `PdfGenerationError`
  is invisible to admins.
- Default taken (non-blocking for the rest of T-0171d): the retry surface shipped — the invoice list
  shows a retry-PDF action on any non-cancelled invoice without a PDF (`!pdfBlobName`), invoking the
  existing `RegenerateInvoicePdf` endpoint; the detail page keeps its regenerate action. The explicit
  failed-flag + error-message display lands as a follow-up once the DTO fields exist.
- Wave-3 close (2026-06-12): converted to ticket **T-0238** (backend DTO fields + admin nswag-regen +
  UI display). Answering here or approving T-0238 are the same decision.
- **RESOLVED 2026-06-21 (PM reconcile): approved-in-substance and SHIPPED.** Both halves landed `done`:
  **T-0238** added `PdfGenerationFailed`/`PdfGenerationError` to `EmployeeInvoiceDto` +
  `EmployeeInvoiceDetailDto` (+ mappers), the owner's admin **nswag-regen** was confirmed, and **T-0263**
  shipped the failed-vs-pending admin render + error text + i18n ×5 (`nx test invoice-management` 34/34,
  `data-protection` 12/12, admin prod build clean). T-0171d AC4 / T-0238 AC3–AC4 fully satisfied. This
  entry is closed (the AC3 PM-closure step that flipped it slipped at the Wave-6 reconcile; reconciled now).
- Answer: **owner approved the DTO addition (= approving T-0238); delivered.**

---

## Wave-5 planning questions (2026-06-13) — raised by PM sequencing Wave 5 (`status/sprint-7.md`)

> Wave 5 = the two folded-front production bugs (T-0245/T-0246) + the consistency/quality sweep
> (T-0196…T-0206) + 3 Wave-4 follow-ups (T-0242/T-0243/T-0244). One genuine pre-build owner product
> decision surfaced (Q-W5-1). It gates **only T-0242** — the rest of Wave 5 proceeds.

> **Q-W5-1 ANSWERED 2026-06-14 (owner) — path (B), Plus = wider free window — moved to `answered.md`.**
> T-0242 unblocked, folded into Wave 6, and `done` (Wave-6 close `b8f89202`). No open Wave-5 questions remain.

---

### Q-W3-4 — [blocking: no] Dispute Resolve when the Stripe refund FAILS — keep "Resolved + Pending Refund row" or defer/surface?
- Raised by: backend (T-0173a); originally recorded as "Q-W3-2" inside the T-0173 ticket file — **re-keyed
  to Q-W3-4 by the PM at Wave-3 close (2026-06-12)** because `Q-W3-2` above (partner-pay currency) already
  held the id; the ticket-file text is the original.
- Date: 2026-06-09 (filed into this inbox 2026-06-12)
- Question: On a dispute Resolve where the Stripe refund FAILS: keep ADR-0006's "mark `Resolved` + return
  Success, leave a Pending `Refund` row for operator re-drive" (current, shipped behavior), OR defer the
  `Resolved` transition until the refund confirms / surface the failure to the admin?
- Why it matters: the admin sees "resolved" while money hasn't moved, and the terminal-state guard then
  blocks a retried Resolve from re-driving the refund (the Pending Refund row is the re-drive path, but it
  is operator-driven, not self-evident in the UI).
- Default taken (non-blocking): keep ADR-0006 behavior. The shipped 173b UX honors it defensively — the
  resolve copy does NOT over-promise ("submitted", not "refunded"; Stripe may-remain-pending disclaimer).
- Status: **owner/security confirmation pending** (carried at Wave-3 close, sprint-5 §8.3 item 5).
- Answer: _(owner fills in)_

---

## Admin action audit log question (2026-06-22) — raised by ADR-0012 (AUD-AUDITLOG)

> **Q-AUDIT-01 RESOLVED 2026-06-22 — moved to `answered.md`.** The owner adopted the **default**
> (append-only / no-auto-delete / PII-minimized: snapshots store ids + changed fields only, never raw
> subject PII; the GDPR-delete audit keeps actor + scope + subject id and legitimately survives the
> subject's erasure as a legal-basis exception). Baked into Wave-9 tickets T-0282 / T-0284 / T-0287. The
> **pre-prod ratification** of the exact retention window + redaction list is a **pre-PROD readiness
> checklist** item (owner/legal), not an open question. No open Wave-9 / audit-log questions remain.

---

## iOS port questions (2026-06-23) — raised by ADR-0013 (IOS-ADR)

> **Q-IOS-01 RESOLVED 2026-06-23 — owner answered iOS 16** (recorded in superseding ADR-0014). Q-IOS-02 /
> Q-IOS-03 remain non-blocking with their defaults; the iOS plan proceeds on them. The **one hard blocker**
> for the iOS feature waves is NOT a question — it is the owner **mobile-spec regen**
> (`manual_step: mobile-spec-regen`, owner-only), a regen of the *existing* contract (not a contract
> change), which gates only the generated-client tickets. The Phase-0 foundation runs without it.

### Q-IOS-01 — [RESOLVED 2026-06-23 — answered: iOS 16] iOS minimum deployment target
- Raised by: architect (IOS-ADR / ADR-0013 D2)
- Owner: owner
- Resolve-by: post-prod
- Date: 2026-06-23
- Question: What is the iOS **minimum deployment target**? The (original) architecture assumed **iOS 17**
  (enables Observation `@Observable` for the state parity and the SwiftUI `Map` for the MapKit default).
- Why it matters: it sets device reach. It does **not** change the architecture — a lower floor only forces
  an `ObservableObject`/`@Published` state mechanism and a `UIViewRepresentable` MapKit variant.
- Default taken (non-blocking, original): **iOS 17** — the modern-platform-floor posture matching Android
  `minSdk 26`.
- **Answer (owner, 2026-06-23): iOS 16.** Rationale = **old-device reach**: iOS 16 runs on **iPhone 8 /
  8 Plus / X (2017+)**, which the iOS-17 floor (XS/XR, 2018+) excluded. Reach was the deciding factor; the
  cost (a more verbose `ObservableObject`/`@Published` state mechanism + the iOS-16 MapKit API variant) is
  accepted. **Recorded in superseding ADR-0014** (`adr/0014-ios-deployment-target-ios16-and-state-mechanism.md`),
  which partially supersedes ADR-0013 D2 + the deployment-target assumption — all other ADR-0013 decisions
  stand. The sealed `UiState`/`ActionState` enums + the facade/state parity are **unchanged** (only the
  observation wrapper changed). sprint-12 tickets updated in parallel. **RESOLVED.**

### Q-IOS-02 — [blocking: no] Hard brand requirement that the iOS map be Mapbox-identical?
- Raised by: architect (IOS-ADR / ADR-0013 D6)
- Owner: owner
- Resolve-by: post-prod
- Date: 2026-06-23
- Question: Is there a **hard brand/design requirement** that the iOS map look pixel-identical to the
  Mapbox-styled Android map (the `MapStyles.kt` custom style)? The default is **MapKit** (Apple-native,
  free, no token), with the Mapbox iOS SDK kept as a **scoped fallback behind the `MapProvider` protocol**.
- Why it matters: a "yes" flips the **default provider** (imports the paid Mapbox SDK + a token to rotate);
  the **seam is unchanged** either way, so this is a provider choice, not an architecture change.
- Default taken (non-blocking): **No** — MapKit by default; Mapbox only if a specific parity gap (custom
  style, service-area polygon overlay, a sheet UX MapKit can't match) forces one surface onto it.
- Answer: _(owner fills in — confirm MapKit default, or require Mapbox-identical → flip the default provider)_

### Q-IOS-03 — [blocking: no] Add trusted-device to the mobile clients (iOS + Android)?
- Raised by: architect (IOS-ADR / ADR-0013 D10)
- Owner: owner
- Resolve-by: post-prod
- Date: 2026-06-23
- Question: Should the **trusted-device** flow (`trustedDeviceToken`, currently optional/null on
  `MobileLogin`/`MobilePartnerLogin` and **not sent by Android**) be built for the mobile clients?
- Why it matters: it is net-new with **no Android reference**; an iOS-only build creates a security-path
  **divergence** with no cross-client contract to anchor it. The ADR-0011 posture is "one contract, all
  clients" — if wanted, design it once and ship Android + iOS together.
- Default taken (non-blocking): **omit from iOS v1 to match Android** (the field is optional, so omitting
  it is fully supported).
- Answer: _(owner fills in — omit to match Android, or commission a one-design trusted-device flow for both
  mobile clients)_

---

## Azure DEV deployment questions (2026-06-23) — raised by ADR-0015 (INFRA-ADR)

> All three are **non-blocking for the DEV provision** (sprint-13 proceeds on the defaults). Q-INFRA-01 and
> Q-INFRA-03 are `pre-prod` *for the prod side only* — they do not gate dev. The DEV provision's real
> prerequisites are **owner manual steps** (GitHub Environments + reviewers, secret migration, Key Vault value
> population, running/approving the first `az deployment group create`), not these questions.

### Q-INFRA-01 — [blocking: no — pre-prod for PROD only] Custom domain for the environments?
- Raised by: architect (INFRA-ADR / ADR-0015 D6)
- Owner: owner
- Resolve-by: pre-prod (prod side); non-blocking for dev
- Date: 2026-06-23
- Question: Should the environments use a **custom domain** (`*.cleansia.cz`) instead of the default Azure
  hostnames (`*.azurewebsites.net` / `*.azurestaticapps.net`)? This needs an owner DNS step + TLS binding.
- Why it matters: the iOS apps + the SPAs point at host URLs; a custom domain is a branding/prod concern. Getting
  it wrong is cosmetic for dev but a real prod readiness item.
- Default taken (non-blocking for dev): **No for dev** — the default `*.azurewebsites.net` hostnames are stable +
  TLS-terminated, sufficient for the Mac-points-at-dev goal. The iOS base-URL is env-switched **config**, so
  adding a custom domain later is config, not code.
- Answer: _(owner fills in — confirm default hostnames for dev; decide custom domain for prod before go-live)_

### Q-INFRA-02 — [blocking: no] Two subscriptions, or one subscription + two resource groups?
- Raised by: architect (INFRA-ADR / ADR-0015 D1)
- Owner: owner
- Resolve-by: post-prod
- Date: 2026-06-23
- Question: Should dev and prod live in **separate Azure subscriptions** (hard billing/policy isolation) or in
  **one subscription with two resource groups** (`rg-cleansia-dev` / `rg-cleansia-prod`)?
- Why it matters: subscription split gives the strongest billing/governance isolation but doubles
  subscription-level overhead. RG split gives clean blast-radius isolation (a `dev` apply cannot touch prod; the
  protected `prod` Environment gates prod deploys) at far less overhead.
- Default taken (non-blocking): **one subscription, two RGs.** The Bicep is RG-scoped, so a later move to two
  subscriptions is a parameter change, not a rewrite — the seam is preserved.
- Answer: _(owner fills in — confirm one-sub/two-RGs, or require separate subscriptions)_

### Q-INFRA-03 — [blocking: no — pre-prod for PROD hardening] Prod network/auth hardening: VNet + private endpoints + Postgres-MI auth?
- Raised by: architect (INFRA-ADR / ADR-0015 D3/D4)
- Owner: owner
- Resolve-by: pre-prod (prod side); non-blocking for dev
- Date: 2026-06-23
- Question: For **prod**, should Postgres/Storage be reached over **VNet + private endpoints** (no public
  endpoint) and should Postgres use **AAD/managed-identity auth** instead of a connection-string secret?
- Why it matters: the system holds PII + payment data; prod should be hardened beyond the dev-pragmatic
  public-endpoint + firewall posture. Postgres-MI auth removes the connection-string secret entirely but needs
  Npgsql token plumbing (a code change).
- Default taken (non-blocking for dev): **dev = public-endpoint + firewall (Azure-services + admin IP) + TLS +
  MI-to-KeyVault/Storage + connection-string-in-Key-Vault for Postgres.** The prod Bicep leaves the seam (a
  module flag) to flip VNet/private-endpoint + Postgres-MI on before prod go-live.
- Answer: _(owner fills in — confirm dev posture; decide prod VNet/private-endpoint + Postgres-MI before prod)_

---

## Apple App Review / iOS compliance questions (2026-06-23) — raised by ADR-0016 (IOS-COMPLIANCE-ADR)

> **Framing recorded in ADR-0016:** there is NO "AI-written-code detector" and App Review cannot brick hardware
> — both FALSE. The real risk is rejection vs the published guidelines. The only owner question is the SIWA
> backend mechanism; it gates **only** the SIWA ticket, not the iOS plan.

### Q-IOS-04 — [blocking: no — gates only the SIWA ticket T-0326] Sign-in-with-Apple backend integration mechanism
- Raised by: architect (IOS-COMPLIANCE-ADR / ADR-0016 D2/AR-ACCT-2)
- Owner: owner (+ architect for the technical shape)
- Resolve-by: pre-submission
- Date: 2026-06-23
- Question: The **Sign-in-with-Apple obligation is CONFIRMED** (the customer app offers Google Sign-In, so
  Guideline 4.8 requires SIWA on the customer app). **How** should SIWA authenticate against the backend — a new
  backend **`appleauth`** anon endpoint (analogous to the existing `googleauth`: validate the Apple identity
  token → issue the mobile JWT, a backend ticket + a spec-regen), or an existing token-exchange path?
- Why it matters: it touches the **auth contract** (a new anon endpoint + the allow-list + a spec-regen feeding
  all three clients), so it is owner-ratified, not a unilateral technical default. The **obligation** is not in
  question — only the mechanism.
- Default taken (non-blocking, to keep planning moving): **assume a backend `appleauth` endpoint is needed** (the
  safe, `googleauth`-mirroring assumption), sized as a backend + iOS pair, gated on the owner confirming the
  backend appetite. The rest of the iOS plan does not wait on this — only T-0326 (the SIWA ticket) does.
- Answer (RELAYED 2026-06-23 — proceed on the default; NOT yet owner-direct-confirmed): the coordinator relayed
  that the owner chose **SIWA via a backend `appleauth` endpoint** — i.e. the default above. The planning
  proceeds on this (T-0326 sized as a backend `appleauth` endpoint + spec-regen + the iOS SIWA UI). **Caveat:**
  this is a coordinator-relayed answer, not a direct owner message, so it carries no user authority — the owner
  should confirm directly before T-0326 (and the backend `appleauth` ticket) advances to `done`; the auth-contract
  change + spec-regen remain owner-gated regardless. Treated as RESOLVED-for-planning, open-for-direct-ratification.

---

## Multi-region expansion questions (2026-06-23) — raised by ADR-0017 (INFRA-REGION-ADR)

> All three are **non-blocking for the single-region West-Europe dev build** (sprint-13 ships single-region +
> the minimal seam). They are gated on a **second region** actually being on the table, which this pass does not
> build. ADR-0017 recommends the **lightest model** (one shared region + DB now; tenancy already separates
> tenants logically) with the seam left clean — these questions decide when/how the heavier region-pinned model
> is adopted.

### Q-REGION-01 — [blocking: no — gated on a second region] The residency trigger (which market, if any, is residency-regulated)
- Raised by: architect (INFRA-REGION-ADR / ADR-0017 D1/D6)
- Owner: owner
- Resolve-by: post-prod (becomes pre-prod for the specific market if a residency-regulated one is launched)
- Date: 2026-06-23
- Question: Is any planned market **residency-regulated** such that its data must **physically stay in-region**
  (a legal requirement, not just presence/latency)? This is the named **trigger** that flips the model from
  one-shared-DB to **region-pinned DBs**.
- Why it matters: the owner's stated driver is **market expansion, not residency**, so the shared model ships.
  But launching a residency-regulated market on a shared DB would be a **compliance incident** — the trigger must
  be caught **before** that market goes live, not after.
- Default taken (non-blocking): **none yet** — the current EU-centric markets (CZ/SK/PL/…) keep data in **West
  Europe**, which is *in* the EU (GDPR's cross-border concern is transfers *out* of the EU; a single EU region
  does not trigger it). A residency-regulated or non-EU market is the trigger to revisit (a new ADR for the
  region-pinned model).
- Answer: _(owner fills in — confirm no residency requirement for the planned markets, or name the market(s)
  that force region-pinned DBs and when they launch)_

### Q-REGION-02 — [blocking: no — gated on a second region] Tenant→region assignment + reassignment policy
- Raised by: architect (INFRA-REGION-ADR / ADR-0017 D3)
- Owner: owner
- Resolve-by: post-prod
- Date: 2026-06-23
- Question: Confirm **country→region** as the assignment granularity (a tenant inherits its country's home
  region; a tenant has exactly **one** home region and never spans regions). And: if a tenant is ever
  **reassigned** to a new region, what is the data-migration story?
- Why it matters: the granularity decides the shape of the future tenant→region map (one row per country vs per
  tenant) and the `CountryConfiguration.HomeRegion` field. Reassignment is a data-migration concern only relevant
  once a second region exists.
- Default taken (non-blocking): **country-driven, one home region per tenant, no reassignment story built** until
  a second region is real. A multi-market legal entity = **two tenants** (one per region), not one tenant
  spanning two.
- Answer: _(owner fills in — confirm country→region granularity; defer reassignment until a second region)_

### Q-REGION-03 — [blocking: no] Per-region subscriptions, or one subscription with region in RG/naming?
- Raised by: architect (INFRA-REGION-ADR / ADR-0017 D6)
- Owner: owner
- Resolve-by: post-prod
- Date: 2026-06-23
- Question: When a second region is added, should each region get its **own Azure subscription**, or stay **one
  subscription** with the region carried in the RG + resource naming (`rg-cleansia-<region>-<stage>`)?
- Why it matters: Azure regions are a resource *location*, not a subscription boundary — one subscription holds
  many regions' RGs. A per-region subscription adds governance/billing overhead and is only warranted by a real
  trigger (a subscription-level quota hit, a billing/legal boundary, or a blast-radius/compliance requirement).
- Default taken (non-blocking): **one subscription** (region in RG/naming) until a quota / billing-legal /
  blast-radius trigger fires. The Bicep is RG-scoped, so a later per-region subscription is a deployment-target
  parameter, not a rewrite.
- Answer: _(owner fills in — confirm one subscription until a trigger, or require per-region subscriptions)_

### Q-FEED-01 — [blocking: no] Do sitewide promo pushes appear in the customer notifications feed?
- Raised by: analyst (T-0393 — feed design panel, D2)
- Owner: owner
- Resolve-by: post-prod
- Date: 2026-07-17
- Question: When the notifications inbox ships, should `promo.new_sitewide` (admin-authored marketing
  pushes) also appear as feed rows in the customer inbox — or does the feed stay transactional-only?
- Why it matters: promo is the only event carrying literal server-authored text (no client template —
  the feed row would have to freeze the rendered `title`/`body`, unlike every other event), its Promo
  category defaults to **off** (marketing consent), and iOS promo *push* display is already its own
  deferred marketing ticket (ADR-0025 verdict, CH-1). Putting marketing into the inbox is a product
  stance, not a technical one.
- Default taken (non-blocking): **excluded from feed v1**; revisit together with the ADR-0025 promo
  iOS-display follow-up ticket so marketing surfaces are decided once, coherently.
- Answer: _(owner fills in)_

---

## CI / branch-policy question (2026-07-30) — raised by ADR-0031 (T-0439), challenge CH-1

### Q-CI-01 — [blocking: no — does NOT gate ADR-0031 or T-0439] Require PRs for `master` (branch protection), instead of / in addition to placing checks?
- Raised by: architect (T-0439 / ADR-0031 D4, panel challenge CH-1)
- Owner: **owner** (this constrains the owner's own git workflow — no agent may decide it)
- Resolve-by: post-prod
- Date: 2026-07-30
- Question: Should `master` be **branch-protected** — no direct pushes, changes land via PR, with the
  existing `frontend-ci` / `backend-ci` checks required to pass before merge?
- Why it matters: **the evidence is unusually direct.** Of the last **25** first-parent commits on
  `master`, every one carries a `(#NNN)` PR number **except two** — `bbcf5b24` and `2ce848cb`. Those two
  are exactly the commits that broke the frontend build for all three web apps (repaired by `7c82cd2e` /
  PR #171 and `ccca1496` / PR #166). **The only two un-PR'd commits in recent history are the two that
  caused the defect ADR-0031 exists to guard.** Under branch protection each would have been a PR, and the
  *already-existing, already-correct* PR-triggered build would have gone red **before** merge — with zero
  new machinery. Every option ADR-0031 enumerated (A–D) placed a *check*; this is the only one on the
  "who may make `master` red" axis, and it is the only invariant-shaped one (it does not depend on anyone
  remembering to run a command, or on a check being placed at the right point).
- The costs only the owner can weigh: a solo operator can lock themselves out of their own repo unless
  `enforce_admins` stays off / self-approval is allowed; every trivial docs push becomes a PR; the hotfix
  path lengthens; CI minutes rise on pushes that today are free.
- **Not a go-live blocker:** prod deploys are `workflow_dispatch`-only behind the `prod-weu` GitHub
  Environment's required reviewers (`deploy-pro.yml:19-29`), so an unbuilt `master` push cannot ship
  itself. This is a velocity/attribution question, not a release-safety one — hence `post-prod`.
- Default taken (non-blocking): **no branch protection changed.** ADR-0031 ships its two mechanisms and is
  **not conditional** on this answer — the regen-time typecheck fires earlier than any branch-protection
  rule can (before a commit exists), and the `master` push build is either the safety net (this question
  answered "no") or harmless redundancy (answered "yes"). The two compose; neither substitutes.
- Answer: _(owner fills in — confirm direct pushes stay allowed, or protect `master` and name the
  self-approval / admin-bypass posture)_

---

### Q-FEED-02 — [blocking: no] Partner-targeted notification events (job assignment / customer cancellation / invoice ready)?
- Raised by: analyst (T-0393 — feed design panel, D2)
- Owner: owner
- Resolve-by: post-prod
- Date: 2026-07-17
- Question: Should the platform add partner-targeted notification events — e.g. `order.assigned`
  (admin assigns a job), a customer/admin cancellation of a job the cleaner already accepted, and
  `invoice.generated` (pay-period invoice ready)? Today the ONLY partner-targeted dispatch is the
  `order.new_available` 30-min digest; every `order.*`/`dispute.reply` producer targets the order's
  customer (the partner Android app documents this gap in a TODO —
  `partner-app/.../CleansiaFirebaseMessagingService.kt:33-42`, with templates already pre-wired).
- Why it matters: a cleaner whose accepted job is cancelled by the customer currently learns nothing
  until they look at their schedule — an operational gap, not just a nicety. Each new event needs a
  producer + `NotificationCategory` + client templates ×2 platforms ×5 locales, so it is real scoped
  work, and which events partners get is a product call.
- Default taken (non-blocking): **not invented inside T-0393** — the feed v1 shows only events that
  exist. Recommended: a dedicated follow-up ticket (the T-0393 notify seam gives any new producer a
  feed row for free; the cancellation-of-accepted-job event is the highest-impact candidate).
- Answer: _(owner fills in)_

---

## Sprint-14 questions (2026-07-30) — see `status/sprint-14.md`

### Q-I18N-02 — [**ANSWERED 2026-08-01 — moved to `answered.md`**] Shorter `ru`/`uk` wording for the profile "Edit profile" chip
- Raised by: pm (T-0450 — from T-0442's implementation and review)
- Owner: **owner** (needs a native Russian and native Ukrainian speaker; this is not a technical default)
- Resolve-by: pre-prod (and **before the demo** if the demo is shown in `ru`/`uk`)
- Date: 2026-07-30 · **Answered: 2026-08-01**
- Question: `profile_row_edit` is `"Edit profile"` (en) and `"Редактировать профиль"` (ru) /
  `"Редагувати профіль"` (uk). The ru string measures ~216.8dp against en's ~120.2dp and does not fit
  the chip, which is capped at `0.45 × width` (`ProfileTab.kt:246-248`) to stop it starving the name
  column — so it renders **"Редактиров…"**. What is the correct shorter ru/uk wording? And should the
  **chip** label diverge from the **screen title** (`profile_edit_title`, same string today), given
  that only the chip is width-constrained?
- **ANSWER (owner, 2026-08-01), verbatim:**
  > *"the ios and android apps have 'Edit profile'. And when translated then it's a long one. I want
  > just to keep 'Edit'/'Редактировать' and truncate it if it doesn't fit by the whole length."*
- **What that settles, stated so it is not re-litigated:**
  1. **The label is the verb alone** — `Edit` / `Редактировать`, and the equivalent verb in `cs`, `sk`
     and `uk`. The noun ("profile" / "профиль" / "профіль") is dropped.
  2. **Overflow is handled by TRUNCATION**, not by wrapping to a second line and not by shrinking the
     type. This is the answer to "what if the verb alone still does not fit at 320dp" — and it is a
     real case, because `Редактировать` is 13 characters against `Edit`'s 4.
- **What it does NOT settle (do not infer either):**
  - **Q-BRAND-01 is untouched.** Poppins still covers **0 of 98** Cyrillic code points on both mobile
    platforms, so `Редактировать` and every `ru`/`uk` hero name still falls back to a system face
    regardless of length. Shortening the string does **not** touch that defect. Split out as **T-0472**.
  - **Whether the truncation is a tail ellipsis**, and **what the accessibility label announces when the
    label is visually truncated.** Both are implementation questions, not owner decisions — they are
    written as **explicit AC on T-0450 (AC4, AC5)** so no developer invents an answer silently.
  - **The second half of the original question — chip-vs-screen-title divergence — was not separately
    named** by the answer. T-0450 **AC6** carries a stated PM default (apply the verb-only form to the
    width-constrained chip `profile_row_edit`; leave `profile_edit_title` and the partner `edit_profile`
    alone) and requires the choice to be **recorded**, not inferred. Cheap to extend if the owner meant
    all three surfaces.
- **Locked into:** `tickets/T-0450-…md` (rewritten 2026-08-01 to half (A) only, now `ready`, size `S`).
  The analyst panel T-0450 was carrying is **discharged** — it existed to produce a defensible answer to
  this question, and an owner decision outranks a panel.
- **Downstream:** T-0450 → `ready`; **T-0448** and **T-0449** keep **T-0450 as their sole remaining
  dependency** and clear when it lands (shared-file lanes on `ProfileTab.kt` / `ProfileTab.swift` /
  `values-{ru,uk}/strings.xml` / `Localizable.xcstrings`).
- **RESOLVED.** Full entry copied to `answered.md`.

### Q-BRAND-01 — [blocking: no] Poppins has no Cyrillic — what renders `ru`/`uk` headings on all three platforms?
- Raised by: pm (T-0450 — measured while grounding a T-0442 finding)
- Owner: **owner** to ratify (a brand-typeface change is an owner call); `architect` to author the options
- Resolve-by: pre-prod
- Date: 2026-07-30
- Question: All three bundled Poppins weights cover **0 of 98** Cyrillic code points
  (`poppins_{medium,semibold,bold}.ttf`; PM parsed the `cmap` directly, 2026-07-30), while all three
  Nunito weights cover 98/98. The Android and iOS binaries are **byte-identical** (sha1-verified), and
  the web apps load the same Google-Fonts family. So on **every** platform, every Poppins slot
  (`displayLarge/Medium/Small`, `headlineLarge/Medium/Small`, plus the hard-coded call sites in
  `WordmarkSplash.kt`, `CleansiaErrorState.kt`, `CodeInput.kt`, `ProfileTab.kt:437`,
  `EditProfileScreen.kt:214`) falls back to a system face for `ru`/`uk`. What is the strategy —
  a Cyrillic-capable fallback inside the family, a per-locale family swap, a subset-merge, or replacing
  Poppins outright?
- Why it matters: two of five shipped locales currently render headings in Roboto/system-serif beside
  Nunito body text — three typefaces on one card. Replacing Poppins is a **brand** decision, not an
  engineering one; the other three options are engineering decisions with different costs. Getting it
  wrong once means re-cutting every heading on three platforms.
- Default taken (non-blocking): **T-0450 fixed only the label**; the font half is now **T-0472**, which
  fixes the two mobile profile heroes and whose architect panel rules on the mechanism for that surface.
  The platform-wide remediation (web + every other Poppins slot) is explicitly out of T-0472's scope and
  stays here until answered — it is *not* silently deferred.
- **2026-08-01 — Q-I18N-02's answer does NOT touch this.** The owner shortened the label to the verb
  alone; a shorter Russian string is still Cyrillic and still falls back. The two defects were on one
  surface, never one cause. This question is the reason T-0450 was split: the label half is the one that
  gates the avatar tickets; **this half gates nothing.**
- Answer: _(owner fills in)_

### Q-CI-01 — [blocking: no] Should `master` carry branch protection (required status checks)?
- Raised by: architect (ADR-0031 panel lead, 2026-07-30)
- Owner: **owner** (a repo-administration setting; no agent can apply it)
- Resolve-by: **post-prod**
- Date: 2026-07-30
- Question: `master` has no branch protection, so a push whose frontend/backend CI is red is not
  prevented from landing. Should required status checks be enabled, and on which workflows?
- **Explicitly non-blocking, and ADR-0031 does not depend on the answer.** The lead's reasoning for
  filing this `post-prod` rather than `pre-prod` is worth preserving, because it is the part a future
  reader would otherwise re-litigate: **the prod deploy is `workflow_dispatch`-only behind the
  `prod-weu` Environment (`deploy-pro.yml:19-29`), so an unbuilt `master` push cannot ship itself.**
  Branch protection would improve the *inner loop* (a red `master` costs the next agent a confusing
  build), not the *release* safety property — which is already held by the Environment gate.
- Why it matters anyway: a red `master` is what produced T-0438, and the cost lands on whoever picks
  up the next ticket rather than on whoever pushed. T-0439 (regen-drift guard) and T-0455 (whether the
  lint gate can be flipped to blocking) both sharpen the same edge from the workflow side; this
  question is the repo-settings side of it.
- Default taken: **none applied** — no agent can change repo settings, and nothing is blocked on it.
- Answer: _(owner fills in)_

---

### Q-DESIGN-01 — [blocking: no — does NOT gate T-0473] "Report an issue" is going RED. Does the danger token now carry a second sanctioned meaning, or is this a named exception?
- Raised by: pm (T-0473 — from the owner's 2026-08-01 defect report)
- Owner: **owner to ratify**; `analyst` to author the semantics, `architect` to rule on the catalog entry
- Resolve-by: post-prod
- Date: 2026-08-01
- **This is not a request to reverse the owner's decision.** The owner asked for red explicitly and it
  is going red — T-0473 ships it. What is open is what the **design system** says afterwards.
- Question: red/error on both mobile design systems currently means **destructive or error**.
  "Report an issue" is a **reporting** affordance — it opens a dispute form; nothing is destroyed and
  nothing has failed. So one of three things must become true, and somebody has to choose which:
  **(a)** the danger/error role gains a **second sanctioned meaning** ("this is the serious/attention
  path", covering both destructive and problem-reporting), **(b)** "Report an issue" is recorded as a
  **named exception** with the reason written next to it, or **(c)** the design system grows a distinct
  **warning/attention** role separate from both `primary` and `error`.
- Why it matters — and why absorbing it silently is the bad outcome: `agents/knowledge/patterns-mobile.md:245`
  states the iOS destructive affordance as *"the ONE way"*, and `core/.../CleansiaButton.kt:80-99`
  (Android's `CleansiaDestructiveButton`) carries a written rank argument — *"Danger must not out-rank
  the primary; it must read as danger."* Both are laws about **what red means**. Painting a
  non-destructive action red without amending either one leaves the next developer with a catalog that
  says one thing and a codebase that does another, and the reviewer after that with no way to tell an
  approved exception from a defect. That is exactly the class ADR-0032 exists to prevent.
- **Aggravating, concrete, and already true in the code:** the order-detail footer stacks **Cancel**
  (already `error` on both platforms) directly above **Report issue**, separated by one 8dp spacer
  (`OrderDetailScreen.kt:505-508`; iOS the same `VStack` at `OrderDetailView.swift:288-307`). After this
  change the two adjacent buttons are the same colour, the same shape and the same rank — one cancels a
  booking, the other files a complaint. T-0473 **AC3** forces a stated differentiator; this question is
  whether the *system* should have prevented the collision rather than each ticket noticing it.
- Default taken (non-blocking): **(b) — treat it as a named exception for now.** T-0473 ships the colour
  and records the reasoning at the call site and in its `## Review`; no catalog law is amended by a
  ticket that is fundamentally a two-line colour change. The durable ruling waits for this answer.
- Answer: _(owner fills in — ratify the exception, or commission (a) a second sanctioned meaning for the
  danger role, or (c) a distinct warning/attention role)_

---

### Q-PROFILE-01 — [blocking: YES — blocks T-0447 AC2/AC3 end-to-end, and the customer web "Save profile" button is already dead] `UpdateCurrentUser` requires a client-supplied `Id` the customer **web** app cannot obtain
- Raised by: frontend (T-0447)
- Owner: **backend** to author the fix; **owner/architect** to pick which of the three shapes
- Resolve-by: **pre-prod** (it is in demo scope — the avatar feature was ruled demo scope 2026-07-30)
- Date: 2026-08-01
- **Traced, not suspected.** `UpdateCurrentUser.Validator` gates every call on
  `AllowedToUpdateUser` (`src/Cleansia.Core.AppServices/Features/Users/UpdateCurrentUser.cs:33-36,
  66-71`): it loads the session user by email and returns `user?.Id == command.Id`. The command's
  `Id` is **client-supplied** (`UpdateCurrentUser.cs:97-98` positional record) and the customer
  `UserController.UpdateCurrentUser` (`src/Cleansia.Web.Customer/Controllers/UserController.cs:28-38`)
  does **not** stamp it from the session.
- **The customer web app has no id to send, by construction.** `MyProfileDto` carries no `id`
  (`UserMappers.cs:28-51`), and the customer web session is an **HttpOnly cookie**
  (`libs/core/customer-services/src/lib/interceptors/auth.interceptor.ts`) — so JS cannot read the
  JWT. Both mobile clients solve it by decoding the token, and say so in their own comments:
  Android `UserRepository.kt:82-86` — *"User id isn't part of the profile response — it's in the JWT
  sub claim"*; iOS `UserProfileClient.swift:55` — `JwtDecoder.userId(of: accessToken)`.
  Web therefore sends `id: undefined`, `user.Id == null` is false, and every customer-web profile
  save fails validation with `user.not_allowed_to_update` (400).
- **This is pre-existing, not introduced by T-0447.** `id: undefined` has been in
  `profile.component.ts` since `29de7b48` (2026-05-16). Nobody has filed it, and there is no
  integration/host test for the customer `UpdateCurrentUser` route — only unit tests that pass a
  matching `Id` (`Cleansia.Tests/Features/Users/UpdateCurrentUserValidatorTests.cs:57-63`). Note
  `user.not_allowed_to_update` is **not** in the customer error contract
  (`apps/cleansia.app/src/app/i18n/error-contract-parity.spec.ts`), so the user currently sees only
  the generic `api.common.error_occurred`.
- Question: which shape? **(a)** the handler/validator resolves the user from
  `IUserSessionProvider` and `Command.Id` is dropped — it is redundant on a *current-user* endpoint
  and is an IDOR-shaped parameter; **(b)** the customer controller stamps `command.Id` from the
  session before `Mediator.Send`; **(c)** `MyProfileDto` gains `Id` so every client can echo it back
  — the weakest option (it keeps a client-supplied identity on an authenticated self-service write,
  and it needs an `nswag-regen` + Android/iOS regen).
- **Not fixable from the frontend.** There is no id source in the customer web app, and inventing one
  (reading the cookie, a second endpoint) would be working around an authorization check.
- Default taken: **none — T-0447 ships the avatar UI built to the current contract** (it sends the
  same `id: undefined` the profile save has always sent, so nothing regresses) and its facade-level
  ACs are proven by unit tests. AC2/AC3's **manual round-trip evidence cannot be produced** until
  this is answered.
- Answer: _(owner/architect fills in — (a), (b) or (c), then a backend ticket)_
