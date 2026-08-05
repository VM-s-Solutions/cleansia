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
- **Q-PLUS-01** (`pre-prod`, **blocking: yes** — blocks T-0497) — does Stripe enforce a
  once-per-customer trial on the Plus price? The two candidate defects have **opposite fixes** and the
  repo cannot distinguish them. **One dashboard check settles it.**
- **Q-PAYOUT-01** (`pre-prod`, **blocking: no — ANSWERED FOR CZ 2026-08-02**, still open for **SK**) —
  the owner supplied **a real Czech ISDOC invoice they issued themselves**, which is exactly the
  *"one real example your accountant accepts"* the question asked for. **CZ is specified; T-0508 is
  unblocked.** SK remains unanswered and is carved out by T-0508 AC11.
- **Q-PAYOUT-02** (`pre-prod`, **blocking: yes** — now blocks **T-0522**, not T-0508) — is a cleaner an
  employee or a self-employed supplier (OSVČ/živnostník), and **who issues** the document? This decides
  **which document** we are generating. **Sharpened 2026-08-02 with a PM finding: the platform's
  current PDF runs in the OPPOSITE direction from the owner's specimen.**
- **Q-PAYOUT-03** (`pre-prod`, **blocking: yes** — blocks T-0522) — **NEW.** The specimen states *"Nejsme
  plátci DPH"*. How does the platform know whether a cleaner is VAT-registered, and what does each
  variant print?
- **Q-PLUS-02** (`pre-prod`, **blocking: yes** for T-0512/T-0493; **non-blocking** for the T-0511
  panel) — **NEW.** The express-upgrade quota's three numbers: one per month or unlimited? does an
  unused month roll over? does the counter reset on the **billing date** or the **calendar month**?
- **Q-PLUS-03** (`pre-prod`, **blocking: yes** — blocks T-0516) — **NEW.** Favourite cleaner: does it
  stay **universal**, or become **Plus-only**? The two answers have opposite diffs and one of them is
  not a backend ticket at all.
- **Q-IOS-LEGAL-01** (`pre-submission`, **blocking: no**) — **NEW.** Which origin do the shipped apps'
  Terms/Privacy links point at, and when does real legal text exist there? Owner ruled **DEV URLs for
  now**; recorded as a **pre-iOS-review gate** (T-0524), not a blocker today.
- **Q-OBS-01** (`pre-prod`, **blocking: no** — shapes T-0500/T-0501) — DEV, the only live environment,
  has **no error tracking from any source**. Turn Sentry on for dev, add an App Insights exporter, or
  accept the gap with a date?
- **Q-PROMISE-01** (`pre-prod`, **blocking: no**) — **NEW.** Both mobile clients promise every customer
  *"Cleaner being assigned · Within 1 hour"*, unconditionally, in five languages. **Nothing enforces it.**
  Is it true in practice on DEV/prod? If not, it is the same class as the express claim just removed.
- **Q-PROMISE-02** (`pre-prod`, **blocking: no**) — **NEW.** On the **Plus checkout page**, cs/sk/ru
  promise the favourite cleaner *"will be preferentially assigned"* where en/uk promise only priority.
  Three locales sell a stronger product than the design delivers. Which is the intended promise?

**Five `blocking: yes` questions are open in this file as of 2026-08-02: Q-PROFILE-01, Q-PLUS-01,
Q-PLUS-02, Q-PLUS-03, Q-PAYOUT-02, Q-PAYOUT-03** *(six entries; Q-PAYOUT-01 came off the list when the
owner supplied a real invoice)*. **The two payout ones remain legal questions no agent may answer.**
**The three new ones are all narrow and pre-scoped** — two numbers and a yes/no each — because they
came out of the owner's own answers rather than out of a panel.

> **UPDATE 2026-08-03 — `Q-PLUS-03` is ANSWERED** (*plus-only*, carried by ADR-0036 D7; the entry below
> still reads as open and should be moved to `answered.md` by the PM). **`Q-PLUS-01` is NARROWED but
> still open** — the 2026-08-03 trial ruling removes the express-waiver leg of the unlimited-trial loop;
> the discount and cancellation-window legs are untouched. **Four further membership rulings landed
> 2026-08-03 and are recorded below under §"Owner rulings recorded 2026-08-03".**

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
- Answer: **shape (a), and it has already shipped** — recorded by frontend 2026-08-05, verified
  first-hand on `master`, not assumed. `85c453f1` ("the caller's identity is server-truth on all
  seven self-service commands") rewrote `AllowedToUpdateUser` to *"No longer an ownership comparison
  — the subject is server-resolved, so there is nothing for a client to get wrong"*
  (`UpdateCurrentUser.cs:75-83`); the handler now loads
  `userRepository.GetByIdAsync(userSessionProvider.GetUserId()!)`. `Command.Id` survives on the wire
  as a nullable no-op solely so Android/iOS keep deserializing, and carries the
  `[OWN-DATA] (S1)` annotation saying it is never read. **The customer web save is therefore live
  again and T-0447's AC2/AC3 round-trip is unblocked** — the web client still sends no id, which is
  now correct rather than fatal. Nothing to change on the frontend. Ready for the PM to close.

---

## Sprint-15 questions (2026-08-02) — see `status/sprint-15.md`

> Filed from the owner's 15-remark batch and the four completed investigations. **Ranked by what they
> unblock**, which is the order they appear in below. The **nine** Cleansia Plus product questions and
> the **seven** partner-onboarding decisions are NOT pre-filed here — they are produced by the
> **T-0491** and **T-0504** panels respectively (each panel's AC requires them to be filed as **one
> consolidated block**), so filing them now would pre-empt the deliberation that gives them their
> options and defaults.

### Q-PAYOUT-01 — [blocking: **YES**] What must a CZ/SK supplier invoice legally contain?
- Raised by: pm (T-0508, from the partner-onboarding investigation)
- Owner: **owner** (this is an accountant's question, not an engineering one)
- Resolve-by: **pre-prod**
- Date: 2026-08-02
- Question: The document the platform generates for a cleaner carries **no IČO, no VAT/DIČ and no bank
  details**. What is the legally required field set for a supplier invoice in **CZ** and in **SK** —
  including the **not-VAT-registered** case, which is the common one for an individual cleaner? Is
  sequential/gapless numbering required?
- Why it matters: **a cleaner cannot be paid against the current document.** An accountant cannot book
  it and a transfer has no reference to key on. The pay-period machinery is built and running and its
  output is unusable for its one purpose.
- **Why no agent may answer it:** the plausible list (IČO, DIČ, bank account, variable symbol, issue
  date, taxable-supply date, sequential numbering, both parties' legal names and addresses) is exactly
  the kind of plausible-but-unverified list that must not be shipped on a tax document.
- What would help most: the field list, plus **one real example** of an invoice your accountant accepts.
- Default taken: **none.** T-0508 is `blocked`. Its **AC1 is dispatchable now** and will render the
  current document so this question arrives with a concrete sample attached.
- **✅ ANSWERED FOR CZ — 2026-08-02.** The owner supplied **a photograph of a Czech ISDOC invoice they
  issued themselves**, i.e. precisely the *"one real example"* this question asked for. The specimen is
  transcribed block-by-block into **T-0508**'s Context and is the specification for **T-0522**:
  *Dodavatel* (supplier = **the cleaner**) with name / street / postcode+city / country / **IČ** and a
  **VAT statement** (theirs: *"Nejsme plátci DPH"*); *Kontaktní údaje* (e-mail, telephone);
  *Odběratel* (customer = **Cleansia**) with **IČ** + **DIČ**; the *Faktura* number top-right plus a
  **barcode**; *Datum vystavení* and *Datum splatnosti*; a payment block with the **Czech local account
  number** (`5885638003/5500`), **IBAN**, **SWIFT**, **variabilní symbol** (= the invoice number),
  **konstantní symbol**, payment method and amount due, plus a **QR Platba +F**; line items as
  description / quantity / unit / unit price / line total; a late-payment interest notice; and
  **Celkem k úhradě**.
- **⚠️ STILL OPEN: SK.** The owner ruled **CZ first**. Nothing here is evidence about Slovak
  requirements and **T-0508 AC11 forbids reading this CZ answer as a CZ/SK one.** Downgraded to
  `blocking: no` because no ticket waits on the SK half today.

### Q-PAYOUT-02 — [blocking: **YES**] Is a cleaner an employee or a self-employed supplier (OSVČ)?
- Raised by: pm (T-0508)
- Owner: **owner**
- Resolve-by: **pre-prod**
- Date: 2026-08-02 · **sharpened 2026-08-02 after the owner's specimen arrived**
- Question: Employee, or self-employed supplier (OSVČ / živnostník)? And if a supplier: does the
  **cleaner** issue the invoice to the platform, or does the platform issue a **self-billing** document
  on their behalf?
- Why it matters: it decides **which document** is being generated. If employee → this is a **payslip**,
  with different content and different law, and T-0522 is not an `M` but a different feature. If
  supplier → self-billing has its own requirements, including the supplier's prior agreement.
  **Adding fields cannot fix a document of the wrong legal category.**
- Corroborating signal from the code: the entity is called **`EmployeeInvoice`** — the two competing
  models' names collided into one, which is evidence this was never decided.
- 🔴 **NEW GROUNDING — PM-verified first-hand 2026-08-02, and it is the reason this stayed blocking
  after the specimen arrived:** **the platform's current PDF runs in the OPPOSITE direction from your
  invoice.** `DefaultInvoiceLayoutBuilder.cs:29-31` puts **CLEANSIA** in the header as the issuer, and
  `:73-81` puts the **cleaner under "Billed To"**. Your specimen has the **cleaner as *Dodavatel***
  (supplier) and **Cleansia as *Odběratel*** (customer). **The two parties are the wrong way round.**
  So this is not "we are missing IČ and a bank block" — it is a document of a different kind.
- **The question in one line:** *on the document the platform generates, whose name goes in the header
  as the issuer — yours, or the cleaner's?*
- Default taken: **none.** **T-0508 is now `ready`** (the specification does not need this answer);
  **T-0522 (the build) is `blocked` on it.**
- Answer: _(owner fills in)_

### Q-PAYOUT-03 — [blocking: **YES**] How does the platform know a cleaner's VAT status, and what does each variant print?
- Raised by: pm (T-0522, from the owner's specimen)
- Owner: **owner** (with their accountant)
- Resolve-by: **pre-prod**
- Date: 2026-08-02
- Question: Your specimen states **"Nejsme plátci DPH"** (not VAT registered) — the common case for an
  individual cleaner. A **VAT-registered** supplier prints **DIČ** and VAT lines instead. Two parts:
  **(a)** how does the platform determine which a given cleaner is? `Employee.VatNumber` exists and is
  **nullable** — is "null means not registered" sufficient, or must a cleaner positively declare their
  status (and can it change mid-pay-period)? **(b)** what exactly does each variant print?
- Why it matters: **a document with an empty VAT field, or one that implies registration where there is
  none, is wrong in a way that matters.** And today `FileExtensions.cs:48` hardcodes `VatAmount = 0` —
  correct for a non-payer **by accident**, and wrong the moment one cleaner registers.
- What would help most: **the same specimen, from a VAT-registered supplier**, if you have one. One
  more photo settles part (b) completely.
- Default taken: **none.** T-0522 is `blocked` on this together with Q-PAYOUT-02.
- Answer: _(owner fills in)_

## Owner rulings recorded 2026-08-03 — **CLOSED ON ARRIVAL** (recorded by `architect`)

> **Why they are in `open.md` and not `answered.md`:** none of these five identifiers was ever carried
> into this file. `Q-PLUS-04` and `Q-PLUS-05` were escalated by the ADR-0036 panel and never recorded;
> `E-1`/`E-2`/`E-3` live only in ADR-0035's §Verdict escalation table. The owner has now ruled on all of
> them, so they are recorded here **as closed** rather than opened-then-closed. **PM: move this whole
> block to `answered.md` on the next pass; nothing here is awaiting anyone.**

### E-2 / Q-PLUS-05 — Do Plus benefits continue during Stripe dunning (`PastDue`)? — **CLOSED**
- Raised by: `architect` (ADR-0035 §Verdict E-2; ADR-0036 D7 / CH-P6) · **Answered: 2026-08-03**
- **Answer (owner): NO. *"PastDue keeps NO benefits. Cut everything on first payment failure."* No grace
  window.**
- Was: the domain contradicted itself — `MembershipStatus.cs:18-19` documented *"Benefits still apply
  during the grace window"*; `UserMembership.IsActive` (`:84-85`) and the one live-membership predicate
  (`UserMembershipRepository.cs:27-29`) both required `Status == Active`. **No code ever implemented the
  grace window.** Both ADRs shipped the predicate's behaviour and escalated.
- **Locked in:** the predicate is **unchanged** (no `WHERE` clause moved — the whole return on there
  being one). `MembershipStatus.cs:18-19`'s comment **corrected in the same pass**. ADR-0035 **AM-17**,
  ADR-0036 **AM-A**. `UserMembership.cs:46-51` still needs its correction → **T-0512**.
- **Consequences the owner accepted knowingly, recorded because three are worse than the headline:**
  a customer whose card merely expired **(a)** loses the discount immediately, **(b)** can have a booking
  hard-rejected before they know, **(c)** is told by the app they have **no membership at all**
  (`GetMyMembership` uses the same predicate, so `Response.Status` can never carry `PastDue`),
  **(d)** stops receiving the renewal reminder (`SendMembershipLifecycleNotifications.cs:77`), and
  **(e)** can start a **second** subscription while the first is in dunning (both app guards and the
  `WHERE "Status" = 1` index backstop miss the row). ⇒ **tickets P-1 and P-2.**

### E-1 — Does the 14-day free trial grant express waivers? — **CLOSED**
- Raised by: `architect` (ADR-0035 AM-14 / CH-B7) · **Answered: 2026-08-03**
- **Answer (owner): NO. *"No express waivers during the 14-day trial."* The trial keeps the discount and
  the cancellation window; metered waivers begin when they pay.**
- Was: `TrialPeriodDays = 14` on both plans, `"trialing"` collapses to `Active`
  (`UserMembership.cs:124`), so a subscriber signing up on the 28th could draw **four waivers for 0 Kč**
  across two `PeriodKey`s and cancel before conversion.
- **Cost, flagged at escalation and confirmed:** the ruling is **not expressible today** — `UserMembership`
  has no trial marker. It needs an **additive nullable `TrialEndsAtUtc`** column + `SubscriptionResult`
  and webhook plumbing. ⚠️ **owner-only `ef-migration`, BATCHED into the regenerated `Initial`** with the
  other pending schema changes — not stacked. ADR-0035 **AM-18**.
- ⚠️ **Sequencing constraint:** the seeded `ExpressUpgradesPerMonth` may now be set **only** in a wave
  that also ships the field. Seed without field = the 0 Kč loop re-opens.
- **Does NOT close `Q-PLUS-01`** — it removes the express-waiver leg of the unlimited-trial loop only.

### E-3 — Mid-month plan swap and the express quota counter — **CLOSED**
- Raised by: `architect` (ADR-0035 §Verdict E-3 — the one gap no challenger attacked) · **Answered: 2026-08-03**
- **Answer (owner): the counter CARRIES.** *"1 used on monthly, switch to yearly, 1 remaining. The quota
  belongs to the calendar month, not the plan."*
- **Established while recording it (the ADR had not checked):** a plan switch **mutates the
  `UserMembership` row in place** — `SwapMembershipPlan.cs:78-81` → `ApplyPlanSwap`
  (`UserMembership.cs:180-197`); `Id` and `StripeSubscriptionId` untouched, **no new row**. The
  challenger's "re-subscribing creates a new row" finding was correct but describes a **different** path.
- **Locked in (ADR-0035 AM-19):** the counting key is **`(TenantId, UserId, BenefitKind, PeriodKey)` +
  `IsActive`**; **`UserMembershipId` must not appear in any `WHERE`/`GROUP BY`/`HAVING`/join on a
  counting path.** The index and reservation statement already comply; **the read path is where this gets
  quietly violated**, because the resolver has the membership row in hand.
- **Two consequences worked out, neither of which needed a further owner decision:** cancel-and-resubscribe
  mid-month **also** does not grant a fresh quota (closing a churn loop the design had already priced);
  and the ruling **reinstates a cardinality guard AM-5 deleted** — a mid-month *downgrade* plus a release
  can otherwise over-grant while the read path says 0 remaining. Pinning test `TC-BENEFIT-DOWNGRADE-0`.

### Q-PLUS-04 — Should a lapsed member's recurring schedule keep materializing? — **CLOSED**
- Raised by: `architect` (ADR-0036 D8.6 / CH-P6) · **Answered: 2026-08-03**
- **Answer (owner): YES. *"A lapsed membership does NOT stop a recurring schedule. Occurrences keep being
  generated, at full non-member price, and the customer is notified of the price change."***
- Was: the materializer checks membership **nowhere** (`MaterializeRecurringBookings.cs:39-47`), so
  ADR-0036 D8.3 would revoke the *smaller* perk (the preference) on lapse while the *larger* one (the
  Plus-gated schedule) survived. The ADR named the asymmetry and refused to decide it.
- **Locked in (ADR-0036 AM-B):** the asymmetry is **confirmed as the ruled behaviour**. Two thirds cost
  **nothing** — the sweep already ignores membership, and `OrderFactory.cs:76-83` already re-resolves the
  discount **per occurrence** from the one predicate, so ruling 1 and ruling 4 **compose by construction**
  (a `PastDue` member's occurrence is generated and priced at full price with no recurring-specific rule).
- ⚠️ **The notification does NOT exist and is a ticket, not an assumption (P-3).** The materializer takes
  no `INotificationProducer`; `recurring.scheduled` carries `orderId` + `orderNumber` only (no price) and
  fires at ~T-24h while materialization runs 7 days ahead. Constraints pinned by the ADR: **one
  notification per price TRANSITION, not per occurrence**, and **it must fire in both directions**.

---

### Q-PLUS-02 — [blocking: **YES** for the build; **NO** for the design panel] The express quota's three numbers
- Raised by: pm (T-0511/T-0512/T-0493, from the owner's *"You can upgrade"* answer)
- Owner: **owner** (product/pricing)
- Resolve-by: **pre-prod**
- Date: 2026-08-02
- Question — three numbers, and they are independent:
  1. **One per month, or unlimited?** The copy on iOS and Android says *"One free same-day booking per
     month"*; **the web copy says *"Pay less for last-minute bookings inside the express window"*, which
     has no cap at all.** Which is the product?
  2. **Does an unused month roll over?** (Two free next month if you used none this month?)
  3. **Does the counter reset on the customer's BILLING DATE or on the CALENDAR month?** Billing-date
     is fairer to a customer who subscribes on the 28th; calendar-month is what the copy implies and is
     easier to explain.
- Why it matters: (3) is **not** a display preference — a billing-anchored window and a calendar window
  are different stored shapes. **T-0511 AC2 requires the design to survive either answer**, so the
  panel proceeds now; but T-0512 cannot write the column and T-0493 cannot enforce the cap until you
  pick.
- ⚠️ **A fourth thing you should see while deciding (1):** *"same-day"* **is not what express means in
  this codebase.** `BookingPolicy` defines express as a **2–4 hour** lead time. A booking made at 09:00
  for 18:00 today is same-day and **already carries no surcharge for anybody**. So the perk as worded
  promises to waive a charge that would never have applied. **T-0513 AC2** asks you to rule: change the
  word, or change the mechanic.
- Default taken: **none for (1) and (3).** For (2), T-0511 will propose **no rollover** as its stated
  default (simplest to explain, no unbounded accrual) — **overridable by your answer.**
- Answer: _(owner fills in)_

### Q-PLUS-03 — [blocking: **YES**] Favourite cleaner: universal, or Plus-only?
- Raised by: pm (T-0516, from the owner's *"I'd like to have it working fully"* answer)
- Owner: **owner** (product/pricing)
- Resolve-by: **pre-prod**
- Date: 2026-08-02
- Question: The feature is **advertised as a Cleansia Plus perk** on all three clients (iOS's own
  string reads *"**Plus benefit** · choose someone who's cleaned for you before"*), but the server
  gates it on **one thing only** — that the customer has previously **completed** an order with that
  cleaner (`CreateOrder.cs:140-154`). **There is no membership check of any kind.** Any customer can
  use it. Should it stay that way, or become Plus-only?
- Why it matters: **the two answers have opposite diffs, and one of them is not a backend ticket at
  all.** *Plus-only* = a server-side membership rule (small) **plus** taking a working feature away
  from existing non-subscribers who use it today. *Universal* = the perk comes **off** the Plus copy on
  three clients × five locales, because selling something everyone already has is the
  misrepresentation.
- Note: this is **not** the same question as whether the perk works. The owner already answered that —
  it must. **T-0495** (the dispatch ADR) and **T-0515** (the build) proceed regardless; only the gate
  waits.
- Default taken: **none — deliberately.** T-0516 is `blocked`. **The PM will not default this**, because
  defaulting to "gate it" silently removes a live capability from real users.
- Answer: _(owner fills in)_

### Q-IOS-LEGAL-01 — [blocking: no — a **pre-submission gate**] Which origin serves Terms and Privacy in the review build?
- Raised by: pm (T-0524, from the owner's housekeeping answers)
- Owner: **owner**
- Resolve-by: **pre-submission**
- Date: 2026-08-02
- Question: The owner ruled *"legal text later, DEV URLs for now."* **Which origin ships in the App
  Review build, and by when does real legal text exist at it?**
- Grounding, PM-verified 2026-08-02: both mobile apps resolve their consent links from **one** constant
  — iOS `CleansiaWeb.swift:8-21`, Android `CleansiaWeb.kt:13-20` — both pointing at
  `https://cleansia.cz/terms` and `/privacy`. The customer web app **defines** those routes
  (`app.routes.ts:140-147`), **but production has never been deployed** (`status/sprint-15.md`), so in
  a build today those links go to a host with no app behind it.
- Why it matters: a reachable privacy policy is a submission requirement, and consent links that 404
  are also a **GDPR-transparency** problem — which compounds **T-0507** (consent required on web, never
  persisted, never asked on mobile). **The fix is one line per platform** by design; the *text* is not.
- Default taken: **the owner's ruling stands — DEV URLs for now, recorded as a gate.** Nothing changes
  today; T-0524 is filed `blocked` so this surfaces at the pre-submission checkpoint rather than in a
  rejection.
- Answer: _(owner fills in — the origin, and the date real text lands)_

### Q-PLUS-01 — [blocking: **YES**] Does Stripe enforce a once-per-customer trial on the Plus price?
- Raised by: pm (T-0497, from the Cleansia Plus audit)
- Owner: **owner** (only the owner can read the Stripe dashboard)
- Resolve-by: **pre-prod**
- Date: 2026-08-02
- Question: A returning customer is offered the free trial again, unconditionally. Which is live?
  **(i)** Stripe *does* enforce once-per-customer → we are **advertising a free trial the customer will
  not get** (a misleading price, and a chargeback generator). **(ii)** Stripe does *not* enforce it →
  the customer **genuinely gets another free trial every time**: cancel, resubscribe, repeat — an
  **unlimited free-trial loop.**
- Why it matters: **the two defects have opposite fixes**, and applying the wrong one makes it worse —
  the "false price" fix removes an advertisement and leaves the loop open.
- **The decisive check, and it takes a minute:** in **test mode**, on a customer who has already had a
  trial, create a second subscription. Does it land in `trialing` or in `active`? *(Secondary: is
  `trial_period_days` on the price/product or passed by our code, and is Stripe's "limit trial to one
  per customer" control on?)*
- Default taken: **none — deliberately.** T-0497 is `blocked`. Its **AC1 (code archaeology: does our
  code set the trial, or is it a dashboard property?) is carved out as dispatchable today** so the wait
  is not wasted.
- Answer: _(owner fills in)_

### Q-OBS-01 — [blocking: no, but it changes what "green on DEV" means] Does DEV get error tracking?
- Raised by: pm (T-0500, from the Azure cost investigation)
- Owner: **owner to ratify**; `architect` to author the options
- Resolve-by: **pre-prod**
- Date: 2026-08-02
- **Grounding, PM-verified first-hand at `0e4ede1b` — and it corrects the investigation on one point:**
  - There is **no Application Insights exporter in any of the five API hosts**. `ApplicationInsights` /
    `AddAzureMonitor` / `UseAzureMonitor` return **zero hits** across `Cleansia.Config` and every
    `Cleansia.Web*` project. The connection string is provisioned by Bicep and **read by nothing**.
  - Sentry is **not "silently disabled"** — the empty-DSN guard at
    `Cleansia.ServiceDefaults/Extensions.cs:87-90` is deliberate and documented, all ten committed
    `appsettings*.json` carry `"Dsn": ""`, and `deploy/AZURE-DEV-RUNBOOK.md:239` says outright
    *"leave EMPTY for dev (Sentry off); real DSN in prod."*
  - **The conclusion survives the correction and gets worse:** prod has **never been deployed**, so the
    "prod" that was going to have Sentry does not exist. **DEV — the environment your iPhone runs
    against and the one you will demo — has no error tracking from either source.**
- Question: **(a)** populate the `SENTRY_DSN` GitHub secret and turn Sentry on for dev (free tier; the
  code path already exists — this is a **secret, not a build**); **(b)** add a real App Insights
  exporter to the five APIs — **but note this increases the bill T-0499 is fixing**; **(c)** accept no
  error tracking until prod exists, and write that down with a date.
- **Sub-question only you can answer: is `secrets.SENTRY_DSN` populated in GitHub at all?**
- ⚠️ **If (a): T-0457 should land first.** It is `ready` and P1: `GET /api/User/GetCurrent` writes every
  caller's email, name, phone and birth date into Information-level logs on all five hosts. An error
  tracker that ships log context would carry that to a third party. *(`SendDefaultPii = false` is
  already set at `Extensions.cs:103` — necessary, not sufficient.)*
- Default taken: **(c) by inaction, which is the current state.** Recorded so it is a decision rather
  than a drift.
- Answer: _(owner fills in)_

### Q-AZURE-01 — [blocking: no — but it gates T-0499 AC1 and AC5] The two cost queries only you can run
- Raised by: pm (T-0499, from the Azure cost investigation)
- Owner: **owner** (portal/subscription access)
- Resolve-by: **post-prod** (the fix does not wait; the *measurement* does)
- Date: 2026-08-02
- Not a decision — a **data request**. Two queries, whose output discharges T-0499 AC1 (attribution)
  and later AC5 (the saving, measured rather than predicted):
  1. **Cost analysis, grouped by resource then by meter**, for the last full month — to confirm the
     Application Insights / Log Analytics ingestion line is the €35–42 the investigation attributes to
     `host.json`, and that Alerts really are **€0.63** and not €50.
  2. **In the Log Analytics workspace:** ingestion volume by table (`AppDependencies`, `AppRequests`,
     `AppTraces`) for the same period — to confirm the ~7.3M queue-poll dependency records.
- **Two findings this would confirm or kill, both worth knowing:** the alert theory is **dead**
  (€0.63), and **retention cuts save €0** because the workspace is already under the free-tier floor.
  Do not spend time on retention.
- Default taken: T-0499 proceeds on the config change regardless — **the five `host.json` values are
  PM-verified and wrong on their own merits** (a 5s poll against a 60s default; sampling enabled with
  `Request` excluded from it). The queries size the win; they do not gate the fix.
- Answer: _(owner runs the queries / pastes the output)_

---

### Q-LEGAL-01 — [blocking: **YES** for store submission] Who wrote `/terms` and `/privacy`, and is that text binding?
- Raised by: frontend (legal routes + env-aware origin work)
- Owner: **owner** (a legal/business call no agent may make)
- Resolve-by: **pre-prod** — specifically **before either app is submitted to a store**
- Date: 2026-08-02
- The pages exist and always did (`libs/cleansia-customer-features/legal-pages`, routed at `/terms`
  and `/privacy`). What is new is what they *contain*: six sections of prose in five locales with **no
  recorded author and no review**, making concrete commercial and legal commitments —
  `terms_page.section4_text` states a cancellation-fee schedule ("free 24+ hours before, 25% fee 4–24
  hours before, 50% fee under 4 hours"), `section3_text` states prices are VAT-inclusive,
  `section5_text` states a 24-hour damage-claim window. Those read as binding terms.
- This ticket therefore did **not** touch a word of the wording. It added a translated draft banner
  (`terms_page.review_notice` / `privacy_page.review_notice`) that renders above the sections, and an
  empty `last_updated_date` so no publication date is fabricated.
- **Two edits, no code change, whichever way you answer:**
  1. The wording is fine / has been reviewed → set `review_notice` to `""` in all five locales, and set
     `last_updated_date` to the real date. The banner and the date line disappear/appear on their own.
  2. The wording is not yours → replace the `section*_text` values with the reviewed text, then do (1).
- **Do not submit to a store with the banner visible.** A reviewer following the in-app policy link
  would read "this wording has not yet been through legal review" on the privacy policy itself. The
  banner is the honest interim state, not a shippable one — it exists so this cannot be shipped by
  forgetting.
- Default taken: **banner visible, date absent.** Preferred over deleting the text (not an agent's call)
  and over rewriting it (would replace unreviewed prose with more unreviewed prose).
- Answer: _(owner fills in)_

---

## Challenger-round questions (2026-08-02) — surfaced by the ADR-0034/0035/0036 challenge lanes, belonging to no ADR

### Q-PROMISE-01 — [blocking: no — but it decides whether T-0525's sibling class is one defect or two] Both mobile clients promise "Cleaner being assigned · Within 1 hour" after every booking. Is that true on DEV/prod?
- Raised by: pm (challenger round on ADR-0036, `adr/challenges/0036-A-promise.md` CH-P1)
- Owner: **owner** (only you can say what actually happens in practice)
- Resolve-by: **pre-prod**
- Date: 2026-08-02
- Question: The screen shown immediately after a customer books states, as a **number**,
  **unconditionally**, in **five languages**, on **both** mobile clients, that a cleaner is being assigned
  **within 1 hour**. **Is that true in practice today on DEV — and will it be true in prod?**
- Grounding, PM-verified 2026-08-02:
  - Android `customer-app/src/main/res/values/strings.xml:741-742` —
    `booking_success_t2_title` = *"Cleaner being assigned"*, `booking_success_t2_desc` = *"Within 1 hour"*;
    plus `values-cs:731-732` (*"Do 1 hodiny"*), `values-sk:728-729`, `values-uk:728-729`
    (*"Протягом 1 години"*), `values-ru:728-729` (*"В течение 1 часа"*).
  - iOS `CleansiaCustomer/Resources/Localizable.xcstrings:4799` (`booking_success_t2_desc`) and `:4834`
    (`booking_success_t2_title`) — the same claim, the same five locales.
  - It is **unconditional**: `BookingSuccessTimeline.swift:10-14` is `CaseIterable` over
    `received → assigning → confirmed → cleaningDay` and `:44-46` makes `assigning` `.active` whenever no
    order status has loaded — i.e. exactly the moment after submit.
  - **Nothing enforces it.** There is no SLA, no timer, no escalation and no alert anywhere behind that
    sentence. Assignment is a **pull model**: an order sits on the board until a cleaner takes it. The only
    proactive nudge is the new-jobs digest, which sweeps every **30 minutes** — and which currently drops
    jobs permanently (**T-0528**) and can fail to advance its watermark (**T-0529**).
- Why it matters: **this is the same class as the express claim just removed.** A numeric, customer-facing
  time promise with no mechanism behind it. If the real median time-to-assignment is comfortably under an
  hour, the sentence stays and this closes. If it is not, the sentence comes off all five locales on both
  clients — corrective copy that ships ahead of any mechanism, exactly as the express perk did.
- **The decisive check, and only you can run it:** on DEV (or from the order data), what is the actual
  spread of *order created → first cleaner assigned*? Median and worst case. A rough answer is enough.
- Default taken: **none.** No ticket is filed yet, deliberately — the two answers have different diffs
  (leave it alone vs. a five-locale × two-platform copy correction) and filing the wrong one wastes a run.
- Answer: _(owner fills in)_

### Q-PROMISE-02 — [blocking: no — a copy/product call, but it is on the checkout page] cs/sk/ru tell the customer their favourite cleaner "will be assigned"; en/uk promise only "priority". Which is the intended promise?
- Raised by: pm (challenger round on ADR-0036, `adr/challenges/0036-A-promise.md` CH-P3)
- Owner: **owner** (product/marketing — no agent may pick which promise the platform makes)
- Resolve-by: **pre-prod**
- Date: 2026-08-02
- Question: On the **Cleansia Plus checkout page**, the favourite-cleaner perk is described differently by
  locale, and three locales sell a **stronger** product than the other two:

  | Locale | `pages.membership.benefit_favorite_body` (`apps/cleansia.app/src/assets/i18n/<l>.json:1095`) | Literal |
  |---|---|---|
  | en | *"…they'll be **prioritized** when matching."* | priority |
  | uk | *"…**матиме пріоритет** при підборі."* | priority |
  | **cs** | *"…bude **přednostně přiřazen**."* | **will be preferentially ASSIGNED** |
  | **sk** | *"…bude **prednostne priradený**."* | **will be preferentially ASSIGNED** |
  | **ru** | *"…он **будет назначен в первую очередь**."* | **will be ASSIGNED first** |

  Rendered by `libs/cleansia-customer-features/profile/src/lib/membership/membership-subscribe.component.html:102-103`
  — **the page where the customer pays for Plus.**
- Why it matters: **three locales promise an outcome the design does not deliver and is not planned to.**
  The dispatch model is **pull** — a cleaner chooses a job; the platform never assigns one to a customer's
  chosen cleaner. Even the favourite-cleaner work in flight (T-0495 / T-0515) is about giving that cleaner
  a **first chance**, not an assignment. A Czech or Russian customer is therefore told, at the point of
  sale, that something will happen which by design will not.
  Note the English is **also** not free: *"prioritized when matching"* describes a matching algorithm that
  does not exist — `PreferredEmployeeId` is written by the booking path and read by nothing.
- The question is genuinely a product one, and there are three coherent answers: **(a)** the perk is a
  *first chance* → all five locales say that, and cs/sk/ru's assignment verb comes off;
  **(b)** the perk really should be *preferential assignment* → that is a dispatch-model change and a much
  bigger ticket than anything currently filed; **(c)** something else you have in mind.
- Default taken: **none.** No copy ticket is filed. The T-0491 copy panel is the natural home for the
  wording **once the promise is decided** — but it cannot pick the promise, and a copy ticket written
  before this answer would be a guess in five languages.
- Answer: _(owner fills in)_

---

### Q-ENUM-01 — [blocking: no — the scope break is already fixed] Three generated clients each emit their own `OrderStatus`/`PaymentStatus`. Which declaration is canonical, and should the per-app copies stop being emitted?

- Raised by: frontend (the `libs/shared/pipes` scope-break fix)
- Owner: **architect** (it changes NSwag configuration and generated output, which is owner-run)
- Resolve-by: backlog
- Date: 2026-08-04
- Question: `OrderStatus` and `PaymentStatus` are declared **four** times in the web tree — once in each
  of `admin-client.ts`, `partner-client.ts`, `customer-client.ts` (generated per host spec), and now once
  in `@cleansia/models` (hand-written, because a `scope:shared` lib may not import an app-scoped client).
  All four are byte-identical today and all four mirror `Cleansia.Core.Domain.Enums.*`. Should the
  generated clients stop emitting their own copy — via NSwag configuration or a post-generation
  re-export — so there is one declaration per workspace instead of one per host?
- Why it matters: the shared copy is the one a **cross-app** pipe reads, and it is the one that cannot be
  regenerated. Today nothing forces the four to agree; a host whose spec drifts (a value renumbered, a
  member added) regenerates one client and leaves the other three — and the shared one — describing a
  different wire contract, silently. That is the same class of defect ADR-0031 exists for, one level up:
  ADR-0031 guards *call sites* against a regen, nothing guards *two generated clients* against each other.
- Default taken: **the shared declaration is a mirror, and drift is made loud rather than impossible.**
  `libs/shared/models/src/lib/models/order-status-enum-parity.spec.ts` reads all three generated clients
  **off disk** (an import would be the very scope break being fixed) and fails if any member or integer
  differs from the shared copy. So a regen that changes the contract goes red in `nx test models` instead
  of shipping. This is a detector, not a fix: it tells you the four disagree, it does not stop the
  disagreement. Collapsing to one declaration is the Architect's call and is a change to owner-run
  generation, so it was not taken here.
- **PARTIALLY ANSWERED — the owner's half is settled; the mechanism was RETURNED to its author on
  2026-08-05.** ADR-0042 is `adr/0042-shared-wire-enums-are-generated-from-the-nswag-output-at-regen-time.md`
  and is `proposed`, **not accepted** (`0041` is the self-billing-agreement ADR — an earlier pointer here
  named it and led readers into a different decision). Living doc:
  `architecture/decisions/generated-client-contract.md` §"The second surface". **This entry stays open —
  it does not move to `answered.md`.**
  - **Owner, verbatim:** *"I think that there is a need to refactor and better to use the one that is
    generated from nswag. Also consider using backend enums on frontend instead of generating your own."*
    ⇒ the hand-written mirror **goes**; the shared declaration must come out of the NSwag pipeline and be
    traceable to `Cleansia.Core.Domain.Enums` with no human retyping. That half is **settled** and is not
    re-litigable — the panel left it untouched.
  - **RETURNED, do not build against it — the architect's HOW as drafted:** a generator
    `src/Cleansia.App/tools/generate-wire-enums.mjs` running **inside** every `npm run generate-*-client`,
    deriving the client set from each `nswag-*.json`'s `output` key, keeping the enums **all** clients
    declare, **failing the owner's regen** on disagreement, and writing one `wire-enums.generated.ts` in
    `libs/shared/models`. The 2026-08-05 panel did not reach consensus and returned it: the value
    authority and the gate's placement are **inverted, not amendable** — a comparison among renderings of
    one declaration cannot detect the defect the ADR was written for, and the drift is created by a
    **backend** commit, so a regen-time gate sits where it is repaired. Seven rebuild constraints
    (RB-1 … RB-7) and a second panel are owed. The *shape* — one machine-written shared declaration, no
    hand-typing — survives.
  - **The per-app copies KEEP being emitted** — the sub-question this entry asked, and the one clause of
    the ADR the panel left standing (§D3 ground (i), unbroken). Hosts are regenerated independently;
    three clients from three specs is three contracts. One shared symbol imported by all three would let
    a client generated against a stale host *claim* the current contract — it removes the evidence of
    drift, not the drift. NSwag's config is **not** changed.
  - **Three facts the question did not have, and they change its framing:** (1) it counted **four**
    declarations; a **fifth** existed in `libs/core/services/src/lib/client/admin-client.ts`, already
    drifted through a renumbering (`InProgress=3` where the live contract says `OnTheWay=3`) and not
    covered by the parity spec — that dead client was **deleted** in `2d913b8b`, so at HEAD the count is
    back to four. (2) It is a **class**: **12** enums are declared by all three clients (36
    declarations), and `SortDirection` was already a third hand-mirror with no spec at all. (3) The
    parity spec **could not run** on a regen-only commit and was cache-replayable even when it was
    selected — **fixed independently of this ADR**: the clients are now a declared
    `{workspaceRoot}` glob input of `models`' `test` target, so a client-only diff selects `models` and
    no cached pass can be replayed over changed client bytes. Verified by mutation; see
    `agents/knowledge/patterns-frontend.md` §"Module boundaries". This closes the *detector*, not the
    question — it still only compares clients to the shared table, never either to the backend (RB-1).
- Answer: **open.** Settled: the hand-written mirror goes and the shared declaration must come out of
  the NSwag pipeline (owner). Undecided: what the shared file's integers are derived from, and where the
  gate that can go red actually lives — returned to the ADR's author, awaiting a second panel.

---

### 🟢 Two DECISION POINTS, not questions — the owner approves a design before code is written

Recorded here so they are visible alongside the questions, but they are **not** blocking entries:
neither has an answer to give yet, because the artifact being approved does not exist.

- **T-0484** — customer order-detail redesign. Produces **2–3 HTML concepts** at
  `agents/backlog/attachments/`, each with all six order statuses and a **per-platform S/M/L
  estimate**. **No implementation ticket exists behind it and none will be written until you pick
  one.** You are choosing a budget as much as a picture.
- **T-0488** — Live Activity redesign. Same shape: **2–3 HTML concepts**, all four surfaces (lock
  screen, island expanded/compact/minimal) at real proportions, each flagged if it needs a field the
  activity state does not carry today (which would make it a backend + push-payload change under
  ADR-0025).
