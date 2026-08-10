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
- **Answer (owner, 2026-08-08):** **Go with the default** — even weights backfilled for existing packages, adjusted per bundle through the admin package-pricing screen when a specific bundle needs it.
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
- _(superseded placeholder — the live answer is the bullet above; retained line neutralised 2026-08-08)_ _(choose (a) Language.IsDefault + fallback, or (b) mandatory-all + documented
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
- **Answer (owner, 2026-08-07):** **Add a `CurrencyCode` to the DTO — do NOT hardcode.** Owner, verbatim: *"NO, DON'T HARDCODE ANYTHING. ADD A DTO."* This is a DTO change and therefore carries `manual_step: nswag-regen`. It also **reverses the existing partner-dashboard precedent**, which hardcodes `Kč` — that call site is now a defect, not a precedent.

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
- **Answer (owner, 2026-08-08):** **Keep "Resolved + pending refund row".** The dispute resolves and the failed refund is carried as its own pending row rather than deferring the resolution or surfacing a separate failure state.

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
- **Answer (owner, 2026-08-08):** **No, the iOS map must not mirror Android's** — but the owner wants them to **look similar**, and asks whether MapKit can carry a **custom style**. So: MapKit stays on iOS and Mapbox stays on Android (per the 2026-08-07 ruling that iOS is the primary platform), and the open item is now a *visual* one — how close MapKit can be brought to the Mapbox styling. That is a research-plus-design task, not a provider decision, and it is **not** blocking. Filed as `Q-IOS-05` below rather than answered here, because what MapKit can and cannot restyle on the iOS 16 floor is a fact nobody in this repo has established.

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
- **Answer (owner, 2026-08-08):** *"I don't understand trusted device. Wdym by that? It's already built, no?"* — **the owner is right, and my earlier note was wrong in its framing. It IS built, and it is live on web.**
  **What the feature is:** after too many failed password attempts an account is locked out. A *trusted device* is allowed past that lockout, so a legitimate user fumbling their own password on their own device is not locked out while an attacker on an unfamiliar device still is (`LoginValidator.AccountIsNotLockedOutOrTrustedDevice`, `:119`).
  **How a device proves it is trusted:** it presents a still-valid **refresh token** from a previous successful login. On web the browser does not send anything special — the three hosts read it from the refresh-token cookie and enrich the command server-side (`Web.Customer/AuthController.cs:40`, `Web.Partner/…:52`, `Web.Admin/AdminAuthController.cs:34`). **So the feature works today, on all three web apps.**
  **The real gap, restated correctly:** the mobile login commands accept the token **in the body** (`MobileLogin.cs:37`, `MobilePartnerLogin.cs:38`) and **no mobile app fills it in**. So a cleaner or customer who is locked out cannot get past it on their **phone**, even from the device they always use — while the same person on the web app can. My earlier phrasing (*"the server accepts a token no client has ever sent"*) was literally true of mobile and gave the wrong impression about the feature as a whole.
  **Decision needed is therefore much smaller than "build trusted device":** should the mobile apps send their stored refresh token on login so lockout behaves the same as on web? **Default: yes** — it is a few lines per app and removes an inconsistency where the phone is stricter than the browser. Not blocking; no answer required unless the owner disagrees.
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
- **Answer (owner, 2026-08-07):** **Custom domains are ALREADY SET for everything in DEV** — web and APIs. So the recorded default ("no for dev, default Azure hostnames") is **wrong about the live state**, not merely superseded. Every artifact that assumes `*.azurewebsites.net` for DEV needs re-checking.

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
- **Answer (owner, 2026-08-07):** **One subscription + two resource groups.**

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
- **Answer (owner, 2026-08-07):** **Dev stays public-endpoint + firewall; the VNet / private-endpoint / managed-identity seam is left for prod.**

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
- **Answer (owner, 2026-08-07):** **⚠️ RESIDENCY IS REQUIRED — this REVERSES the recorded default.** Owner: *"since we'll work B2B with cleaners and require IČO (or another number that is attached to a specific country) then they must have residency. Otherwise they won't be able to open an IČO."* The recorded default said no market is residency-regulated. Cleaner **country registration** is the trigger the question was asking for, and it is present from day one, not gated on a second region. See the PM note appended below — this needs an architect panel, not a status flip.
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
- **Answer (owner, 2026-08-07):** **Yes** — country-driven assignment, one home region per tenant, no reassignment story until a second region is real; a multi-market legal entity is two tenants.

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
- **Answer (owner, 2026-08-07):** **One subscription.** Owner: *"In case of high traffic I just up-scale the services."*

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
- **Answer (owner, 2026-08-08):** **Excluded.** Promo pushes do not appear in the customer notifications feed, which matches the behaviour today. Confirmed as a decision rather than a default.

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
- **Answer (owner, 2026-08-08):** **Do not think about it — branch protection is the owner's concern.** Closed. No agent may change repository settings in any case, and nothing is blocked on it.
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
- **Answer (owner, 2026-08-07):** **Yes — add partner-targeted notifications.** Plus a new requirement the question did not ask about: *"I want to have push notifications arriving once per time (I guess once per hour) if new jobs appeared nearby."* The existing partner digest is **30 minutes**, so this is a cadence change plus a nearby-jobs trigger, not only new event types.

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
- **Answer (owner, 2026-08-07):** **Keep Poppins.** The brand face does not change. The Cyrillic fallback shipped on all three platforms (`94d5bf99`, `50ed0c2d`, `55ad850e`) is therefore the permanent answer, not an interim one.

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
- **Answer (owner, 2026-08-08):** **Do not think about it — branch protection is the owner's concern.** Same ruling as the sibling `Q-CI-01`; recorded on both entries so neither reads as open. No agent can change repository settings in any case.

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
- **Answer (owner, 2026-08-07):** **(b) — named exception.** Red keeps its single meaning (destructive or error); "Report an issue" is recorded as an exception at the call site rather than the catalog gaining a second sanctioned meaning.
  danger role, or (c) a distinct warning/attention role)_

---

### ✅ Q-PROFILE-01 — CLOSED 2026-08-09, fixed rather than answered. Nothing owed.

> Re-verified at HEAD before it was put back in front of the owner: `UpdateCurrentUser.cs:110` now carries
> the `[OWN-DATA]` note *"the row written is ALWAYS the JWT caller's — this id is never read"*, and
> `Cleansia.HostTests/Tests/UpdateCurrentUserSessionIdentityTests` pins it end to end through the real
> MVC model binder, including the web client's body-with-no-id shape. The three shapes the question
> asked the owner to choose between are moot: the id is inert. **Original text below, for the record.**

### Q-PROFILE-01 (original) — [was: blocking YES] `UpdateCurrentUser` requires a client-supplied `Id` the customer **web** app cannot obtain
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
- **Answer (owner, 2026-08-07):** **IČO, IBAN, bank details and other details** — the owner re-states that they already sent a reference of how the invoice should look. **They did, and it IS recorded**: `status/sprint-15.md` §A4 carries the field-by-field analysis of the Czech ISDOC specimen they photographed. My first pass at this answer said the reference was missing; that was wrong — only the *photo* is absent from the repo, not the specification derived from it. **The CZ field set is therefore known.** What §A4 also establishes, and what the owner's IČO/IBAN answer confirms, is that the current document has **the two parties the wrong way round** — it prints Cleansia as issuer and the cleaner as 'Billed To', where the specimen has the **cleaner as Dodavatel** and **Cleansia as Odběratel**. That is a document of a different kind, not a missing-fields problem. **SK is still unstated**, and so is whether gapless numbering is required.

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
- **Answer (owner, 2026-08-07):** **A cleaner is an OSVČ (self-employed supplier), and the platform SELF-BILLS.** Owner, verbatim: *"This is what I meant by self bill, instead of them making invoices on their own and sending them to us one by one, we have all of the invoices generated from their completed orders in place for all of the cleaners and pay for them in 1 day. So there is a need to have an agreement of self bill/paying."* So the agreement is not optional paperwork — it is what makes the single-day batch payout lawful. **T-0522 unblocks.**

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
- **Answer (owner, 2026-08-05) — part (a) SETTLED, part (b) still open:**
  > *"I wouldn't ask them if they're VAT payers. So I wouldn't consider that there is a need to
  > implement VAT functionality around cleaner invoices."*

  So: **the platform does not ask, and no VAT functionality is built around cleaner invoices.** The
  self-billing agreement's version key therefore needs **no VAT axis** — this unblocks ADR-0041's D1
  key, which was the only thing this question gated there.

  ⚠️ **One tension the owner should see, recorded as fact, not as a challenge to the ruling.** The
  intent is "we don't ask", but the tree already asks in two places and already branches on the answer:
  - a cleaner can **self-service edit** `VatNumber` (`UpdateIdentificationInfo.cs:77`), and an admin can
    set it (`AdminUpdateEmployee.cs:65`);
  - the invoice PDF branches on it — `CountryInvoiceContext.VatWithinGross(...)` returns 0 when the
    supplier is not a payer, and `ReceiptPdfData` swaps VAT rows for a non-payer notice.

  So "not asking" is true of the onboarding flow and false of the data model. Nothing is broken today
  (null ⇒ non-payer ⇒ the specimen's *"Nejsme plátci DPH"* is correct), and this does **not** reopen the
  ruling. It is a **cleanup question for later**: either remove the field and its branch, or leave both
  as dormant capability and say so. Part **(b)** — what a VAT-registered variant would print — is moot
  under this ruling and should be closed with it if the field goes.

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
- **Answer (owner, 2026-08-07):** **(1) One express upgrade per month. (2) No rollover. (3) No reset on plan switch.**

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
- **Answer (owner, 2026-08-07):** **Plus-only.** The favourite-cleaner feature is gated on an active membership, matching what all three clients already advertise. This REMOVES a live capability from non-members, which is why the PM declined to default it — the owner has now ruled.

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
- **Answer (owner, 2026-08-07):** **Keep the DEV URLs for now**; the owner will switch them to production later. The pre-submission gate stands.

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
- **Answer (owner, 2026-08-07):** **Enforce once-per-customer trial.** Owner: *"Not sure if it's set up in Stripe or not, but indeed there is a need to enforce once-per-customer trial… I don't want a user to be in the situation when he tries a trial subscription, then cancels and then tries a trial again. So basically he wouldn't pay for a subscription."* So the answer is the REQUIREMENT, not the current state — the current state still has to be established (T-0497 AC1) and then made to match.

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
- **Answer (owner, 2026-08-07):** **(c) — do not populate Sentry for DEV.** Owner is still using App Insights for DEV. No Sentry DSN is set; this is now a decision rather than a drift.

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
- **Answer (owner, 2026-08-07):** **The text was generated by Claude, and everything will be checked with a lawyer.** So it is **not** binding today and must not be presented as reviewed. The existing banner stays; the store-submission gate stands until the lawyer's pass.

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
- **Answer (owner, 2026-08-07):** **The promise is TRUE and must be kept: a cleaner is assigned within 1 hour, in PROD.** The copy stays. **But the owner reports a NEW defect in the same breath:** the iOS **Live Activity displays the wrong time until the cleaner's arrival**. That is a separate defect and is being ticketed — it is not covered by this answer.

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
- **Answer (owner, 2026-08-07):** **Assignment, not priority — and the flow is bigger than the copy.** Owner, verbatim: *"if an employee has a free spot then it has to work in a way that he has to be assigned, not just set a priority. There is a need to check also the functionality around it for both employee and customer. And send a notification to the employee when customer created an order and then ask employee to confirm the order; if not then to offer customer either select another employee that will go through the same flow of approval, or suggest a random cleaner."* So `cs`/`sk`/`ru` were right and `en`/`uk` understate it. This is a **feature**, not a copy fix — see the PM note appended below.

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
- **Answer (owner, 2026-08-07):** **Canonical = the backend declaration, surfaced through the generated TypeScript clients.** Owner: *"the ones that are coming and set on backend and are generated via TS client from them."* So the hand-written shared mirror is not canonical; it is a mirror, and the parity spec that makes drift loud is the right shape.
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

---

## Signup-consent questions (2026-08-06) — raised while fixing the web signup tick that recorded nothing

### Q-CONSENT-01 — [blocking: no] Google / Apple signup collects no consent tick at all
- Raised by: frontend (web signup consent fix)
- Owner: owner (legal)
- Resolve-by: pre-prod
- Date: 2026-08-06
- Question: the "I agree to the Terms of Service and the Privacy Policy" checkbox sits on the
  email/password form only. The Google and Apple buttons on the same screen are **not** gated by it and
  a social signup therefore creates an account with **no tick anywhere**. Should those buttons be
  gated by the same checkbox, should the screen carry a separate "by continuing you accept …" line
  under them (a distinct, weaker form of evidence), or is account creation itself the acceptance?
- Why it matters: the web fix delivers exactly what the user ticked and nothing more, so a social
  signup today produces **no consent record** on either app — the same evidentiary hole this work
  closed for the email/password path, in a flow that is one click shorter. Recording a consent nobody
  ticked would be the worse defect (a manufactured record), so it was deliberately not done.
- Default taken: nothing is recorded for Google/Apple signups. The email/password tick is recorded,
  as **two** rows — `TermsOfService` and `PrivacyPolicy` — because the checkbox label names both
  documents by title in all five locales. If the owner wants marketing consent captured at signup it
  needs its **own** checkbox; the cookie banner is the only surface that grants `MarketingEmails`
  today, and `ConsentSyncService.syncConsent`
  (`libs/core/customer-services/src/lib/services/consent-sync.service.ts:36`) returns early for a
  visitor who is not signed in — so an anonymous visitor's banner choice is kept on the device and
  never becomes an account record, even after they register. Whether it should is the same
  legal question in a second place, and was left alone rather than guessed at.
- **Answer (owner, 2026-08-07):** **GATE THE BUTTONS.** The Google and Apple buttons are gated on the same checkbox as the email/password form, so a social signup produces the same evidence as an email one — a tick by a specific person at a specific moment, not a claim that they saw a sentence. The passive *"By continuing you accept…"* line is **rejected**. Accepted cost: a disabled social button until the box is ticked, which is the same friction the email form already imposes.

---

## Self-billing agreement question (2026-08-06) — raised by the ADR-0041 round-3 defense panel

### Q-SELFBILL-06 — [blocking: **YES** — gates only the acceptance of ADR-0041; blocks no ticket and no schema] A cleaner who does not read Czech is approved, works, and is self-billed **without ever being shown the self-billing agreement**
- Raised by: architect (ADR-0041 round-3 lead, ruling on `challenges/0041-rev3.md` CH-R3-1)
- Owner: owner (legal / launch sequencing)
- Resolve-by: pre-prod
- Date: 2026-08-06
- Question: ADR-0041 makes the self-billing agreement **demandable only in the caller's own language** —
  we may not block a Ukrainian-speaking cleaner on ticking a Czech legal text they cannot read, because a
  signature over bytes the signer cannot read is not evidence. On your stated plan — **Czech text
  first** — that means every cleaner whose app language is `sk`, `uk`, `ru` or `en` is **approved without
  the agreement ever being shown to them**, works, accrues pay, and is issued invoices **in their own
  name**. Which do you want?
  **(a)** Do not open a country to cleaner registration until its agreement text exists in **all five**
  partner languages. Closes the hole by sequencing; costs four translations + review before launch.
  **(b)** Open on Czech, accept that non-Czech readers are self-billed with no recorded agreement until
  their text lands, and make it **visible**: one query naming the (country × language) pairs with no
  reviewed text, plus an in-app prompt to those cleaners once their text arrives.
  **(c)** Open on Czech and accept the gap silently — nothing in the platform counts it.
- Why it matters: your ruling *"I'll drop an entire DB, we're not PRO just DEV, so don't be bothered with
  existing cleaners"* removes the **backlog** of un-agreed cleaners. It does not stop the platform
  **creating new ones** — and this one grows with hiring. The ADR currently claims this population
  *"cannot form"*; that claim is false against the ADR's own design, and the last revision deleted the
  report row and the in-app prompt that would have counted and reached these people **on the strength of
  it**. This is the exact population the whole feature exists for: we issue invoices in the cleaner's
  name, and the agreement is our evidence that we were allowed to. An architect may not choose between
  *"delay the launch"* and *"self-bill people who were never shown the agreement"* — that is your call.
- Default taken if unanswered: **(b)**. It is the only option that neither delays your Czech-first launch
  nor hides the gap, and its visibility half is a `SELECT` over a config table of tens of rows — not a
  scan over invoices. **ADR-0041 stays `proposed` until you rule**, because accepting it freezes the
  sentence, and an accepted ADR may not be edited (only superseded). Nothing else is blocked: no ticket,
  no schema, no migration.
- Related — **and this needs the PM, not you:** `Q-SELFBILL-01` (the agreement text itself, which
  ADR-0041 calls *"the single thing standing between the design and a working feature"*) and
  `Q-SELFBILL-02`…`-05` are specified in ADR-0041 §Escalations and recorded there as *"filed… by the
  PM"* — **they are not in this file, and a grep of the whole backlog finds them nowhere.** They have
  never actually reached you. This entry does not substitute for them. **PM: file that block, and add
  this entry to the Pre-prod blocking index at the top of this file.**
- **Answer (owner, 2026-08-07):** **(b) — open on Czech, with visibility.** Launch is not delayed and the un-agreed cohort is counted and surfaced rather than hidden. ADR-0041 can now be accepted on this limb.

---

## Self-billing block (2026-08-06) — filed late; ADR-0041 §Escalations has claimed since rev 1 that these were filed, and they were not

> **Provenance.** ADR-0041's §Escalations opens *"Filed as one block in `agents/backlog/questions/open.md`
> by the PM."* That sentence was false for the whole life of the ADR. The rev-3 lead grepped the backlog
> and found them nowhere; I re-ran the grep and confirmed it — `Q-SELFBILL-01`…`-05` appear **only inside
> ADR-0041 itself**. Three deliberation rounds reasoned about defaults "if the owner does not answer"
> while the owner had never been shown the questions. They are filed here now, verbatim in substance from
> the ADR's table so the two cannot drift. `Q-SELFBILL-06` (above) was filed correctly and is separate.

### Q-SELFBILL-01 — [blocking: **YES** — gates the feature's activation, not its build] The self-billing agreement text, and who reviewed it
- Raised by: architect (ADR-0041), filed by PM 2026-08-06
- Owner: owner (with counsel)
- Resolve-by: pre-prod
- Question: we need the **agreement text** itself, in as many of the five locales as you can supply, and
  **who reviewed it** — you, or a lawyer. ADR-0041 calls this *"the single thing standing between the
  design and a working feature."*
- Why it matters: self-billing means **we** issue the invoice in the cleaner's name. In the EU that is
  only lawful where the supplier has agreed to it in advance. Without reviewed text there is nothing to
  show a cleaner and nothing to record them accepting.
- Default if unanswered: every version stays `NotReviewed`, so nothing is rendered and nothing is
  demanded, and no jurisdiction is opened. Safe, and visibly incomplete — the feature ships inert.
- **Answer (owner, 2026-08-07):** **A lawyer will review it in the future.** So the text is NOT reviewed yet and no version may be marked reviewed on the owner's say-so. The feature stays inert until reviewed text exists — that is the recorded default and it now has an end condition rather than an open wait.

### Q-SELFBILL-02 — [blocking: **YES** for the severed coverage decision; blocks no ticket] Which date authorizes a self-billed invoice — the print date or the work period?
- Raised by: architect (ADR-0041, re-framed rev 2, narrowed rev 3), filed by PM 2026-08-06
- Owner: owner (with counsel)
- Resolve-by: pre-prod
- Question: two halves. (a) If we open a country **before** its agreement text exists, and cleaners work
  and are invoiced there, is the document valid? (b) Were we authorized to issue **this document** (its
  print date) or to self-bill **this work** (the pay period it covers)?
- Why it matters: (b) gives **different answers for the same work**, decided by when a timer happened to
  run — a cleaner who accepts on 31 July covers June's work; one who accepts on 5 August does not.
- Default if unanswered: do not open a jurisdiction before its text exists, which makes (a) empty by
  construction. That is an engineering sequencing call, **not legal advice**, and it does not answer (b).
- **Answer (owner, 2026-08-07):** **(a) the invoice is INVALID** — and the residue is empty by construction, because *"it never will be a case that a company registers before the agreement text exists."* That is an owner commitment about sequencing, not merely an engineering default. **(b) the PRINT DATE (`GeneratedAt`) authorizes the document**, not the pay period it covers.

### Q-SELFBILL-03 — [blocking: no] Must the invoice itself say it was issued on the cleaner's behalf?
- Raised by: architect (ADR-0041), filed by PM 2026-08-06
- Owner: owner (with counsel)
- Resolve-by: pre-prod
- Question: must the **invoice document** state that it was issued by us on the supplier's behalf, and in
  what words?
- Why it matters: it is a line of text on a PDF, but it is the line that makes the document a
  self-billed invoice rather than an ordinary one.
- Default if unanswered: nothing is printed. If the answer requires an **immutable acceptance date** on
  the document, that is a separate design trigger — say so explicitly if it does.
- **Answer (owner, 2026-08-08):** **Nothing printed.** The invoice carries no statement that it was issued by the platform on the supplier's behalf. The recorded default stands, and it is now a decision rather than an unanswered default.

### Q-SELFBILL-04 — [blocking: no] May a cleaner withdraw from self-billing in-app, and what follows?
- Raised by: architect (ADR-0041), filed by PM 2026-08-06
- Owner: owner
- Resolve-by: post-launch
- Question: may a cleaner **withdraw** their agreement from inside the app, and what happens next — they
  invoice us instead, or they stop working?
- Why it matters: it is a product question, not a schema one. The record already supports a revocation
  action, so answering it later costs no migration.
- Default if unanswered: not exposed in v1.
- **Answer (owner, 2026-08-08):** **Withdrawal does not exist as a concept here.** Verbatim: *"It's impossible for them to withdraw something. We'll send them money manually from our bank account to their bank account when the invoice is created, also putting a special variable number that is written in the invoice."*
  **Two facts in that answer are bigger than the question asked, and are carried into the PM notes below:** **(i)** payout is a **manual bank transfer performed by a person**, not an automated payout rail; **(ii)** the **variable symbol printed on the invoice is the reconciliation key** for that transfer. So a cleaner cannot withdraw from self-billing because there is nothing running to withdraw *from* — each payment is an individual human act against an individual invoice.
  Nothing is exposed in v1, as the default said. ADR-0041's `Action.Revoked` stays in the record as unused capacity — it is accepted and immutable, and removing it would cost a superseding ADR for no gain.

### Q-SELFBILL-05 — [blocking: no] Does an operator recording your countersigned paper contract count as the agreement?
- Raised by: architect (ADR-0041), filed by PM 2026-08-06
- Owner: owner
- Resolve-by: pre-prod
- Question: if you have a signed paper contract with a cleaner, may an admin record that as the
  agreement instead of the cleaner ticking it in-app? And what may the contract reference field carry —
  a contract number, a scan id, or free text?
- Why it matters: an operator-typed free-text field of unconstrained content is treated as personal data
  and redacted on erasure until you constrain it. Constraining it makes it cheaper to handle.
- Default if unanswered: yes, an admin may record it, kept permanently distinct from a self-service tick;
  the reference field is treated as PII and redacted on erasure.
- **Answer (owner, 2026-08-08):** **Yes** — an operator recording the owner's countersigned paper contract counts as the agreement, kept permanently distinct from a self-service tick. The contract-reference field is treated as personal data and redacted on erasure until its content is constrained.

---

### Q-ART-01 — [blocking: no] Do we keep accepting formats we have decided not to scrub? (DOC/DOCX on employee documents; PDF on dispute evidence)
- Raised by: architect (panel lead, user-artifact content-policy ADR) — T-0458 / T-0459
- Owner: owner
- Resolve-by: post-prod
- Date: 2026-08-06
- Question: two accept-set narrowings, same shape, one decision. The panel ruled that user-uploaded
  **images** get their metadata scrubbed at intake, and that **document formats do not** — rewriting a
  PDF object graph or an OOXML package is refused as disproportionate. That leaves two formats
  accepted-but-never-scrubbed:
  - **(a) DOC/DOCX on employee documents** — they carry author names and revision history. Dropping
    them leaves PDF/JPEG/PNG, which cover every real document-scan case, but it narrows what a cleaner
    may upload and changes a five-locale string that promises *"Accepted: PDF, JPEG, PNG, DOC, DOCX"*.
  - **(b) `application/pdf` on dispute evidence** — the flow is photo evidence, and an uploader who
    wants metadata preserved can simply wrap the photo in a PDF, which the scrub will not touch.
    Unlike (a) this changes no five-locale promise. It does remove a customer's ability to attach a PDF
    document (a receipt, a bank statement) as evidence, which is the cost to weigh.
- Why it matters: an accept set is a product promise. Widening one later is cheap; narrowing one after
  launch breaks a flow someone has already used. It also sets the precedent for every future format
  request — do we accept a format we cannot scrub and say so, or refuse it?
- Default taken (non-blocking): **keep accepting both.** The architecture is complete either way — the
  exclusion is written per surface, with its own reason, on the upload intake roster, so no format is
  silently unscrubbed. The panel explicitly declined to decide this as an architecture call.
- **Answer (owner, 2026-08-07):** **Keep accepting both** — DOC/DOCX on employee documents and PDF on dispute evidence. The per-surface exclusion stays written on the intake roster with its own reason, so no format is silently unscrubbed.

---

### Q-ART-02 — [blocking: no] A cleaner's photo fails the byte check mid-job: refuse it, or store it un-previewable?
- Raised by: architect (challenger, byte-derived intake ADR) — T-0561 / T-0459
- Owner: owner
- Resolve-by: pre-prod
- Date: 2026-08-06
- Question: `SaveOrderPhotos` is about to derive the stored content type from the payload's **bytes**
  rather than from what the client declared. Two behaviours are on the table when the bytes are not
  JPEG/PNG/WebP — realistically a mislabelled or renamed file picked in partner **web** (both mobile
  apps re-encode to JPEG and cannot produce this case):
  - **(A) Refuse it (400).** The cleaner sees a translated error and must re-pick. If they are on site,
    on a phone, and do not retry, that before/after photo does not exist in the job record at all.
  - **(B) Store it, serve it as `application/octet-stream`.** The upload succeeds and the bytes are
    kept, but the tile does not render in the gallery — the file downloads instead of previewing, and
    nobody is told why.
- Why it matters: order photos are the evidence a **dispute** is later adjudicated on. (A) is what the
  sibling single-photo endpoint already does, so it is also the consistent answer; (B) never loses
  bytes. The trade-off is *a legible refusal the cleaner can act on* versus *never lose a before/after
  photo*. Architecture can defend either — this is a product call.
- Default taken: none. Flagged before the ADR is ruled on, so whichever you pick is what gets built.
- **Answer (owner, 2026-08-07):** **(A) Refuse it — force a re-pick.** Owner: *"in another case it's a data corruption and we have some problems with it if it's stored incorrectly."* So the refusal must be legible and actionable; the reused image-family content-type-mismatch message already resolves in all five locales on every client.

> **Two measurements owed before this lands, neither of which an architect could take (no shell):**
> (i) how many `OrderPhotos` rows on DEV already carry `application/pdf` or `image/gif` — an owner
> query, and the number decides whether a read-path clamp needs a migration or is free; (ii) whether
> the rewritten pin test actually reddens under each named mutation, which is the closing ticket's
> Gate 0.5 and must not be taken on trust.

---

# PM notes on the 2026-08-07 answer batch

Written the same day as the answers, so nothing above depends on a claim recorded nowhere. **The
owner's instruction on delivering this batch was: *"Write my every answer and DON'T FORGET ABOUT IT
SINCE I HAD TO ANSWER THE QUESTIONS THAT WERE ALREADY RESERVED IN THE PAST."*** That happened because
`Q-SELFBILL-01`…`-05` were recorded in ADR-0041 as *"filed"* since revision 1 and had never reached
this file. The answers were therefore committed **on their own, before any work acted on them**
(`57cdb535`).

## N1 — `Q-IOS-03`: the trusted-device flow is **NOT** built on mobile

The owner asked to check first. Checked, and the belief does not hold:

- **Backend: present.** Six auth handlers reference it — `MobileLogin.cs:28,37` carries
  `string? TrustedDeviceToken = null`, plus `Login`, `PartnerLogin`, `MobilePartnerLogin`, `AdminLogin`
  and `LoginValidator`.
- **Android: zero references.** `grep -ri trusteddevice src/cleansia_android` → nothing outside build
  output.
- **iOS: zero references.** Same grep over `src/cleansia_ios` → nothing.

So the server accepts an optional token that **no mobile client has ever sent**. The recorded default
(*"omit from v1 to match Android"*) is the accurate description of today. The question is still open as
a product call: **build it on both, or delete the unused parameter?** Leaving a half-built auth
affordance is the one option with no argument for it.

## N2 — `Q-CONSENT-01`: the owner asked which is better. **Gate the buttons.**

Recommended: **gate the Google and Apple buttons on the same checkbox**, not a "By continuing you
accept…" line. Three reasons, in order of weight:

1. **It produces the same evidence as the email path.** A tick is an affirmative act by a specific
   person at a specific moment; a sentence under a button is an assertion that they saw something.
   Under GDPR the first is a record and the second is a claim. Since this whole work exists because we
   had *no* record, deliberately choosing the weaker form for the shorter flow is the wrong direction.
2. **It is one consent surface, not two.** The delivery machinery already shipped on all four clients
   and is keyed on the tick. Gating reuses it exactly; the passive line needs a second, weaker rule
   about what counts as acceptance, and that rule has to be defended separately for every future
   document.
3. **The cost is one disabled button**, and it is the same friction the email form already imposes.

**The argument against, stated honestly:** a disabled social button converts worse, and social signup
is where drop-off is most price-sensitive. If conversion wins, the passive line is defensible — but
then say so explicitly, because it is a deliberate downgrade of the evidence, not a neutral
alternative.

## N3 — `Q-REGION-01` **reverses** a recorded default and needs a panel, not a status flip

The recorded default was *"none yet — the EU-centric markets keep data in West Europe."* The owner's
answer is that cleaners are **B2B suppliers who must hold a country-attached registration number
(IČO or equivalent)**, so *"they must have residency."*

This is the **named trigger** the question was written to catch, and it is present **from day one** —
not gated on a second region, which is what every downstream artifact assumes. Before anything is
built on it, two things need establishing and neither is the PM's to assert:

- **Does "the supplier must be country-registered" actually imply a data-residency obligation**, or is
  it a *business* requirement about who we may contract with? Those are different constraints with
  very different costs, and the answer's phrasing (*"otherwise they won't be able to open an IČO"*)
  reads more like the second. **This distinction is the whole decision** and it is a legal question,
  not an architecture one.
- If it *is* residency, ADR-0017's one-shared-DB model is what the trigger flips, and that is a
  superseding ADR with a migration story — not an edit.

**Filed as `Q-REGION-04` below rather than assumed either way.**

## N4 — `Q-IOS-02` **reverses the parity direction** and contradicts a shipped ADR

Owner: *"iOS has to be primary, so Android has to be similar to iOS, not the other way around."*

**ADR-0018 (iOS design-parity principle) says the opposite**, and so does the iOS charter — Android is
the reference implementation the iOS apps mirror. Every iOS ticket this sprint was briefed that way,
including work that shipped. This is not a contradiction to resolve silently: it changes which
platform a future divergence is judged against.

The map half is clean and needs nothing — **MapKit on iOS, Mapbox on Android, deliberately not
identical**, which is what both platforms already do. The *principle* half needs a superseding ADR.
**No existing iOS work is invalidated**: parity was a tie-breaker for undecided details, not a source
of requirements, and no shipped iOS behaviour was chosen *because* Android did it first.

## N5 — `Q-INFRA-01`: the recorded default is **wrong about the live state**

Custom domains are already set for every DEV web app and API. The default said *"no for dev — the
default `*.azurewebsites.net` hostnames are sufficient."* That is not superseded, it is **inaccurate
about today**, so any artifact that reasons from the Azure hostnames needs re-checking rather than
merely updating. Nothing is blocked; the risk is a doc that sends someone to the wrong URL.

## N6 — a question I missed when I listed them for the owner

I gave the owner a list of 36 and called it complete. **`Q-W3-4`** — *dispute Resolve when the Stripe
refund fails: keep "Resolved + pending refund row", or defer/surface?* — was not in it. My extraction
matched on an answer-line pattern that block does not use. It is still open, and it is listed below so
it is not lost a second time.

Three more are open because the owner's batch did not cover them, which is fine and not an omission:
**`Q-SELFBILL-03`** (must the invoice say it was issued on the cleaner's behalf), **`Q-SELFBILL-04`**
(may a cleaner withdraw in-app), **`Q-SELFBILL-05`** (does an admin-recorded paper contract count).
They are all `blocking: no` and all carry defaults.

## N7 — work the answers created that the questions did not ask about

Recorded here so the scope is not lost between the answer and the ticket:

| From | New work |
|---|---|
| `Q-PROMISE-01` | **The iOS Live Activity shows the wrong time until the cleaner arrives.** A defect, reported in passing, not covered by the promise answer |
| `Q-PROMISE-02` | Favourite cleaner must **assign**, not prioritise — plus notify the cleaner on booking, ask them to confirm, and on refusal offer the customer another cleaner through the same approval flow or a random one. Both the cleaner and customer sides need checking |
| `Q-FEED-02` | Partner-targeted notifications, **plus an hourly nearby-jobs digest**. The existing partner digest is 30 minutes, so this is a cadence change as well as new events |
| `Q-W3-2` | Add `CurrencyCode` to the pay DTO (`manual_step: nswag-regen`), and the partner dashboard's hardcoded `Kč` becomes a defect rather than a precedent |
| `Q-PLUS-01` | Enforce once-per-customer trial. The answer is the **requirement**; what Stripe does today is still unestablished |
| `Q-PLUS-03` | Gate favourite cleaner on Plus — this **removes a capability real users have today**, so it needs a release note, not just a gate |
| `Q-IOS-03` | Either build trusted-device on both clients, or delete the unused server parameter |

## N8 — `Q-CI-01` was filed twice

The owner's *"I need to have it only once"* is read as an instruction about the **duplicate**, not about
branch protection. The second occurrence is retired below. **The branch-protection decision itself
remains open and non-blocking** — I cannot change repository settings in any case.

---

### Q-REGION-04 — [blocking: no — but it decides whether ADR-0017 stands] Does "cleaners must be country-registered" mean data residency, or only who we may contract with?
- Raised by: PM, from the owner's `Q-REGION-01` answer
- Owner: owner (with counsel)
- Resolve-by: pre-prod
- Date: 2026-08-07
- Question: you answered that cleaners must hold a country-attached registration number (IČO or
  equivalent), *"otherwise they won't be able to open an IČO"*, and concluded *"they must have
  residency."* Two different constraints fit that sentence and they cost very different amounts:
  **(a)** a **business** rule — we may only contract with suppliers registered in the country we
  operate in. Nothing about where data is stored; ADR-0017's single shared database in West Europe is
  untouched. **(b)** a **data-residency** obligation — that country's data must physically stay in
  that country, which is the named trigger that flips the platform to region-pinned databases and
  needs a superseding ADR plus a migration story.
- Why it matters: (a) is a validation rule and a week of work. (b) is an architecture change that
  touches every tenant-scoped table. Guessing (b) buys an expensive rewrite nobody asked for; guessing
  (a) and being wrong is a compliance failure found late.
- Default taken: **none.** Deliberately not defaulted — the recorded default said "no residency
  requirement", the owner's answer appears to contradict it, and the contradiction may be only in the
  wording. Nothing is built on either reading until this is settled.
- **Answer (owner, 2026-08-07):** Verbatim: *"I think that it has to be a rule that both of the sides have to follow, you cannot work in a certain country if you don't have residency there."*
  **PM reading — stated explicitly because acting on a misread is how this goes wrong: this is option (a), a BUSINESS / CONTRACTING rule, not a data-residency obligation.** The sentence is about who may *work* in a country (both the platform and the cleaner must be registered there), not about where bytes are stored. So **ADR-0017's single shared database in West Europe stands**, and what this creates is a **validation rule plus a `CountryConfiguration` fact** — which country registration each side must hold to operate in a market.
  **If that reading is wrong** — i.e. you also mean each country's data must physically stay in that country — say so, because that is the trigger that flips the platform to region-pinned databases and needs a superseding ADR with a migration story. Nothing built under reading (a) is wasted if (b) later turns out to be true: the validation rule is needed either way.

### Q-CI-01 (second occurrence) — RETIRED 2026-08-07
- Duplicate of the `Q-CI-01` filed 2026-07-30. Retired at the owner's instruction (*"I need to have it
  only once"*). The surviving entry keeps the open branch-protection decision.

---

### ✅ Q-CONSENT-02 — CLOSED 2026-08-08 by extending the owner's existing ruling. Nothing owed.

> **Owner:** *"I ALREADY GAVE YOU ALL OF THE RELATED ANSWERS TO THIS TOPIC."* Correct. `Q-CONSENT-01`
> settled the principle — **an account must not come into existence without an affirmative tick** —
> and that decides this too. It was wrong to re-ask; only the mechanism was ever open, and mechanism
> is mine.
> **Built: option (b).** Social sign-in signs in an **existing** account only; an unknown identity is
> refused with *"no account found — sign up first"* instead of silently creating one, so every new
> user goes through the signup screen where the gate already is.
> **Why not (a), a checkbox on the sign-in screen:** it would ask a returning user to re-accept the
> terms at every sign-in, which is both odd and evidentially worthless — the record that matters is
> the one made when the account was created, not a re-tick years later.
> **Overrulable**: if the one-tap sign-up-by-signing-in flow is worth more than the record, say so and
> it reverts to (c).

<details><summary>Original question, kept for the record</summary>

### Q-CONSENT-02 — [blocking: no] Signing IN with Google/Apple creates an account too — and that screen has no checkbox
- Raised by: frontend (social signup gate, `f6cba0e0`)
- Owner: owner
- Resolve-by: pre-prod
- Date: 2026-08-08
- Question: `Q-CONSENT-01` is now built — the Google and Apple buttons on the **sign-up** screen are gated
  on the terms tick. But the customer **sign-in** screen carries the same two buttons and has **no
  checkbox**, and the backend **auto-provisions an account** when no user matches the social identity
  (`GoogleAuth.cs:124` calls `User.CreateWithGoogle` then `userRepository.Add`; `AppleAuth.cs:176` is
  the twin). **Verified independently.** So a brand-new visitor who taps "Sign in with Google" gets an
  account created with **no consent record anywhere** — the exact hole the signup gate just closed, one
  screen over.
  Three shapes, and the choice is yours because each is a different product:
  **(a)** put the same checkbox on the sign-in screen — consistent evidence, but it asks a returning
  user to re-accept every time they sign in, which is odd and arguably meaningless;
  **(b)** let the social buttons sign in **only an existing account**, and refuse an unknown identity
  with "no account found — sign up first". This makes sign-in mean sign-in, and routes every new user
  through the gated signup. It is a **behaviour change for a flow that works today**;
  **(c)** keep auto-provisioning and record nothing, accepting that social sign-in is an ungated
  account-creation path.
- Why it matters: it is the same evidentiary gap, and closing it on one screen while leaving it open on
  the other is worse than either answer — it looks closed. (b) is the shape most products use, but it
  turns a currently-working one-tap flow into a two-step one, and that is a conversion decision rather
  than an architecture one.
- Default taken: **none, deliberately.** Nothing was invented for the sign-in screen. The signup gate
  ships as ruled; this stays open and visible rather than being silently defaulted either way.
- Answer: _(answered above — this copy is the archived original)_

---

### ✅ RESOLVED 2026-08-08 — express-upgrade quota: fixed in the seed, nothing owed by the owner

> **Owner:** *"All of the data is in DEV only, so fixing the issues would be just a usual drop of db."*
> **Correct — with one thing that had to change first, which is what I failed to say clearly.** A drop
> and reseed would have reinstated the zero, because the seed never set the column at all. It does now:
> both Plus plans carry `ExpressUpgradesPerMonth = 1`. **A drop-and-reseed is now sufficient and
> nothing is owed by the owner.** The admin-screen route is unnecessary given a planned drop, and is
> recorded below only as the alternative for an environment that cannot be dropped.

<details><summary>Original entry, kept for the record</summary>

### 🔴 OWNER DATA STEP (2026-08-08) — the express-upgrade perk is switched OFF in data, on every environment
- Raised by: backend (`a3ac501a`, verifying `Q-PLUS-02`)
- Owner: owner — **this is a data change, not a code change; no agent may make it**
- Resolve-by: **before anyone tests the Plus express perk**
- Not a question — a **step**. Recorded here because it is the only place the owner reads.

**What is true today, verified three ways:**

1. `sql-scripts/insert_seed_data.sql` contains the string `ExpressUpgradesPerMonth` **zero times** — the
   `PLUS_MONTHLY` / `PLUS_YEARLY` INSERT column lists (`:1677-1703`) omit it entirely.
2. The column is `IsRequired().HasDefaultValue(0)`, so both plans land on **0**.
3. `ExpressWaiverResolver.cs:52` returns early when `ExpressUpgradesPerMonth <= 0`.

**Therefore no Plus member on a freshly seeded database receives any express waiver** — a perk
advertised on web, Android and iOS, switched off in data. It is fail-closed and it is not the owner's
ruling of **1**.

**Two ways to land it; the second also fixes the already-deployed DEV database:**

- **(a)** Add `"ExpressUpgradesPerMonth"` and the value `1` to both INSERT column lists in the seed.
  ⚠️ This fixes **new** databases only. The seed's INSERTs are `WHERE NOT EXISTS`, so they **no-op on
  existing rows** — a re-run will not retro-fix DEV.
- **(b)** Set it to `1` on both plans through the **admin Membership Plans screen**. No deploy, and it
  reaches the live DEV rows. `UpdateMembershipPlan` already exposes the field.

**Recommended: do both** — (b) now so DEV is correct, (a) so the next seeded environment is born
correct. Doing only (a) leaves DEV wrong; doing only (b) means the next fresh database is wrong again.

This interacts with the owner's earlier "I'll drop the entire DB" plan: **a drop-and-reseed without (a)
reinstates the zero.**

</details>

</details>
### ✅ The self-billing signature-date floor — CLOSED 2026-08-08 with a default. Nothing owed.

**What it was, since I never explained it.** ADR-0041 lets an **admin record that a cleaner signed the
self-billing agreement on paper**, rather than the cleaner ticking it in the app — that is
`Q-SELFBILL-05`, whose default is *"yes, an admin may record it."* Recording it means typing **when**
they signed. The ADR declined to invent a rule for which dates are believable, and I relayed that as
*"the earliest believable signature date"* with no explanation of what it was for. That was
meaningless without the context above.

**The real question was:** should the system refuse an obviously impossible signature date — a date
before the company existed, or one in the future?

**Default taken, so nothing is owed:** refuse **future** dates, which is unambiguously wrong in every
reading and needs no business input. **No lower floor is set.** A lower bound looked derivable from
the company record, but `CompanyInfo` carries a registration *number* and no founding **date**
(`src/Cleansia.Core.Domain/Company/CompanyInfo.cs` — checked), and its `CreatedOn` is when the row was
configured, which can legitimately be **after** a paper contract was signed. Refusing on that basis
would reject valid records, so the floor stays open and configurable rather than guessed.

If the owner ever wants a hard floor — e.g. the company's incorporation date — it is one config value
and no redesign.



---

# PM notes on the 2026-08-08 answers

## N9 — `Q-SELFBILL-04` answered more than it was asked, and the extra part is architectural

The question was *"may a cleaner withdraw from self-billing in-app?"* The answer is **no, because the
concept does not apply** — and the reason discloses two facts that no artifact in this repo currently
states:

> *"We'll send them money manually from our bank account to their bank account when the invoice is
> created, also putting a special variable number that is written in the invoice."*

**(i) The payout rail is a human doing a bank transfer.** There is no automated payout provider, no
scheduled disbursement, no batch file. Each payment is an individual act by a person, against an
individual invoice. That is why withdrawal is meaningless: there is nothing running to withdraw from.

**(ii) The variable symbol printed on the invoice is the reconciliation key** between that manual
transfer and the invoice it settles.

**What this changes, and what it confirms:**

- **It confirms `EmployeeInvoice.VariableSymbol` is load-bearing, not decorative.** It already exists,
  is generated per employee per pay period, and prints. Under this answer it is the *only* link between
  money leaving the bank and the invoice it paid. Anything that changes how it is generated, or that
  lets two invoices share one, is a **reconciliation defect**, not a cosmetic one. The invoice rebuild
  ticket must treat it that way.
- **It reframes what the stored payout details are for.** `EmployeePayoutDetails` holds an IBAN so a
  **human** can type it into a banking screen — not so a payment API can consume it. That does not
  change the storage or the reveal-audit design, both of which are right either way, but it does change
  what "correct" means: a malformed IBAN fails at a bank counter, not at an API boundary, so there is no
  machine downstream that will catch it for us.
- **It makes "paid" an unobserved state.** Nothing in the system can know a transfer happened; only a
  person does. Whether the platform needs a way to record that, and who records it, is not something the
  owner was asked and is **not** assumed here — filed as `Q-PAYOUT-04` below.

## N10 — the remaining self-billing answers close the set

`Q-SELFBILL-01`…`-06` are now all answered. **The only outstanding self-billing input is the agreement
text itself** (`Q-SELFBILL-01`: a lawyer will review it in future), which gates the feature's
activation and nothing else — schema, endpoints and clients ship inert until reviewed text exists.

## N11 — deploy sequencing: the owner will hold

Owner, 2026-08-08: *"I'll hold the deploy until everything is ready."* So the release gate recorded in
`ab699b6a` — that the generated clients cannot send the new consent flag, and deploying the backend
before regeneration plus the web facade change would stop web social signup provisioning — is
**managed by holding, not by racing**. Mobile consent ticks, the client regeneration and the currency
DTO can therefore all batch into one regeneration rather than forcing two.

---

### Q-PAYOUT-04 — [blocking: no] Nothing in the system can know an invoice was actually paid. Should it?
- Raised by: PM, from the owner's `Q-SELFBILL-04` answer
- Owner: owner
- Resolve-by: post-launch
- Date: 2026-08-08
- Question: you pay each cleaner by **manual bank transfer**, referencing the variable symbol printed on
  their invoice. That means the platform never observes the payment — no webhook, no statement import,
  nothing. So an invoice's state stops at "generated" and the fact that money moved lives only in your
  bank account and your head. Three options: **(a)** leave it — you know what you paid, and the invoice
  is the record; **(b)** an admin marks an invoice paid, giving the platform a date and a person, which
  is a small screen and an audit row; **(c)** import bank statements and match on the variable symbol —
  much more work, and the only option that is self-verifying.
- Why it matters: it decides whether "which cleaners have I not paid yet" is a question the platform can
  answer or one you answer from your bank. It also decides whether a cleaner can ever see *paid* rather
  than *invoiced*, which is the single most common support question in this shape of business.
- Default taken: **(a) — leave it.** Nothing is built, and this is recorded rather than assumed because
  the answer that produced it was about withdrawal, not about payment tracking. You were never asked
  this.
- **Answer (owner, 2026-08-08):** **(b) — an admin marks the invoice paid.** Verbatim: *"I'd assume that only when we send money then we manually mark the invoice as paid."* So the platform learns about payment from a person, not from a bank feed: whoever makes the transfer records it, which gives the invoice a paid date and an actor. Bank-statement matching **(c)** is not built. This makes *"which cleaners have I not paid yet"* answerable in the app, and lets a cleaner see **paid** rather than only **invoiced**.

---

# PM notes, 2026-08-08 (second batch)

## N12 — I was wrong about trusted device, and the correction matters

I reported it as *"the server accepts a token no client has ever sent"*, which reads as **not built**. The
owner pushed back — *"it's already built, no?"* — and they are right. Re-read at HEAD:

- `Web.Customer/Controllers/AuthController.cs:40`, `Web.Partner/Controllers/AuthController.cs:52` and
  `Web.Admin/Controllers/AdminAuthController.cs:34` each do
  `command with { TrustedDeviceToken = RefreshTokenFromCookieOrBody(string.Empty) }`.
- So **all three web apps supply it today**, from the refresh-token cookie, and the lockout bypass works.

My statement was true **of mobile only**. Generalising it to "no client" was the error, and it made a
shipped feature look absent. The gap is narrow: mobile accepts the token in the request body and no
mobile app fills it in, so lockout is **stricter on the phone than in the browser** for the same person.

## N13 — Android having no Apple sign-in is correct, not a gap

Owner: *"The android mustn't have apple sign-in since it's android."* Agreed and recorded. I reported it
as a *product gap*; it is a **deliberate platform difference**. Nothing is owed. The Android consent
string naming only Google (`45ebffb9`) is right for the same reason, and the iOS string naming both is
right on iOS.

## N14 — the clients have been regenerated, so the web send-half is unblocked

Owner: *"All of the clients were regenerated."* That clears the release gate recorded in `ab699b6a`. The
remaining web work is small and is **not** a regeneration: pass the signup tick through
`CustomerAuthService.authenticateWithGoogle` / `…Apple` into the now-regenerated command, exactly as
Android (`45ebffb9`) and iOS (`03b80211`) already do. Until that lands, web social **signup** still
refuses new users on deploy; web social **sign-in** is unaffected.

## N15 — `Q-PAYOUT-04` answered (b), which is a build, not a note

An admin marking an invoice paid needs: a paid state and a paid-at stamp on the invoice, an action on
the admin invoice screen, an audit row (the audit engine already covers admin actions), and the cleaner
seeing **paid** rather than **invoiced**. It also gives the platform the answer to *"who have I not paid
yet"*, which today lives only in the owner's bank. Schema change ⇒ **owner-run migration**.

---

### Q-IOS-05 — [blocking: no] How close can MapKit be brought to the Mapbox styling?
- Raised by: PM, from the owner's `Q-IOS-02` answer
- Owner: owner (design), after an iOS spike establishes what is possible
- Resolve-by: post-launch
- Date: 2026-08-08
- Question: you ruled that iOS keeps MapKit and Android keeps Mapbox, and that the two need not be
  identical — but you would like them to **look similar**, and asked whether MapKit can take a custom
  style. Nobody in this repo has established what MapKit can actually restyle on the iOS 16 floor, so
  the honest first step is a spike rather than an answer: enumerate what is controllable (map
  configuration and point-of-interest filtering, overlay and annotation styling, and whether the base
  cartography's colours can be influenced at all), against the specific things the Android custom style
  does. Then you choose how close is close enough.
- Why it matters: it is purely visual and blocks nothing, but it is the kind of item that silently
  becomes "we'll do it later" and never gets measured. Naming the unknown makes it decidable.
- Default taken: **stock MapKit styling**, which is what ships today. Nothing changes until the spike
  says what is available.
- **Answer (owner, 2026-08-08):** **No restyle. One thing only: the map PIN.** Verbatim: *"Don't do anything, except the pointer on the map. I want to change it to be the same as in android app if possible. The blue one."* So the MapKit base cartography is left stock, and the single change is the **marker/annotation** on iOS matching the Android app's blue pin. That is an annotation-view change, not a base-map style — which is the half of MapKit that is fully controllable, so the *"if possible"* is almost certainly a yes. Scoped to a small iOS ticket; the spike this question originally asked for is **no longer needed**.

---

## Favourite-cleaner assignment questions (2026-08-08) — raised by the design of the owner's assign-and-confirm request

> Context, because the phrase alone means nothing: the owner asked that a cleaner with a free slot be
> **assigned** rather than merely prioritised, be **notified** when a customer books, and be asked to
> **confirm** — and that on refusal the customer be offered another cleaner through the same flow, or a
> random one. The design draft answers that. These four are the parts it deliberately did **not**
> decide, because each is a product or policy call rather than an engineering one.

### Q-ASSIGN-01 — [blocking: no] When we offer "a random cleaner", does the customer see a NAME or a promise?
- Raised by: architect (assign-and-confirm draft) · Owner: owner · Resolve-by: pre-prod · 2026-08-08
- Question: your flow ends with *"or suggest a random cleaner"*. Two very different products fit that.
  **(a)** we show a **specific person** — name, photo, rating — and the customer accepts them, which
  means that cleaner then goes through the same confirm-or-decline loop and can also say no.
  **(b)** we show **no name** — *"we'll assign the first available cleaner"* — and the job simply goes
  to the open board, where whoever takes it first gets it.
- Why it matters: (a) is warmer and is what "suggest a cleaner" sounds like, but it can decline again,
  and each round costs the customer another wait. (b) always terminates, is what the platform actually
  does under the hood today, and is the one promise we can keep without qualification. **This is the
  same failure class as the checkout copy where three languages promise assignment and two promise only
  priority** — a sentence that outruns the mechanism.
- Default taken: **none.** The mechanism is decided either way; only the promise is open, and a promise
  is not the architect's to make.
- **Answer (owner, 2026-08-08):** **(b) — a promise, not a name.** The customer is told the platform will find them a cleaner; no specific person is shown at that step. This is the only option that always terminates and the only promise the mechanism can keep without qualification.

### Q-ASSIGN-02 — [blocking: no] Two rounds of asking, before the job goes to everyone. Is two right?
- Raised by: architect · Owner: owner · Resolve-by: pre-prod · 2026-08-08
- Question: when a favourite cleaner declines or does not answer, the customer may pick again. The draft
  caps that at **two rounds**, then the job opens to the whole board. Two is **derived, not measured**:
  each round holds the job off the open board for a slice of its lead time, and the cap is what keeps
  the total held share inside the platform's own floor for how much of the board must stay open.
  Raising it to three means a job can spend more of its life invisible to everyone else, which is how a
  booking ends up with nobody on it.
- Why it matters: it trades the customer's preference against the job actually getting filled. Two is a
  defensible starting point; it is not a measured one, and the measurement it would need does not exist
  yet.
- Default taken: **two rounds**, which is what the draft specifies and pins with a test.
- **Answer (owner, 2026-08-08):** **Keep two rounds.** The derived cap stands and is pinned by a test.

### Q-ASSIGN-03 — [blocking: no] Should a cleaner who keeps declining lose the favourite perk?
- Raised by: architect · Owner: owner · Resolve-by: post-launch · 2026-08-08
- Question: nothing today records that a cleaner declined — the reservation simply lapses. So no policy
  can accrete by accident, which is deliberate. But should repeated declining have a consequence: they
  stop being offered first, they are told, or nothing at all?
- Why it matters: being offered first is a real advantage, and a cleaner who always declines it while
  keeping the badge is taking the benefit without the obligation. Equally, declining a job at a bad time
  is legitimate and punishing it makes the perk hostile. Recording declines is the precondition for
  either answer and is **not** built.
- Default taken: **nothing recorded, nothing enforced.**
- **Answer (owner, 2026-08-08):** **Not now.** Declines stay unrecorded and nothing is enforced, so no policy can accrete by accident. Revisit only if declining becomes a real pattern — and note that **recording declines is the precondition** for any future answer here, so it is a build, not a config flip.

### Q-ASSIGN-04 — [blocking: no] Should the job ever be simply THEIRS, without confirming?
- Raised by: architect · Owner: owner · Resolve-by: post-launch · 2026-08-08
- Question: the draft builds a **reservation the cleaner must confirm**, because you said *"ask employee
  to confirm the order."* The stronger reading of *"he has to be assigned"* would be that the job is
  theirs whether or not they answer, and they must actively decline to give it up. Which did you mean?
- Why it matters: the difference is who carries the risk of silence. Under the built design, silence
  releases the job to everyone — the customer is never left with a cleaner who is not coming. Under the
  stronger reading, silence keeps the job assigned to someone who may never show, which is worse for the
  customer and better for the cleaner.
- Default taken: **the reservation**, built from your own "ask employee to confirm" clause.
- **Answer (owner, 2026-08-08):** **The reservation is confirmed. Verbatim:** *"They either have to confirm that they took this order and then there is a message for the customer that it was confirmed. If it's declined then it's gonna propose to find another cleaner, if no found and none confirmed then a random is assigned."*
  **This settles the design and adds one thing the draft did not have:** a **customer-facing confirmation message** when the cleaner confirms. That is a new customer-targeted notification, and it is the first thing in this flow the customer hears rather than infers.
  **One ambiguity I am recording rather than resolving silently.** *"a random is assigned"* has two readings. **(i)** the job goes to the **open board** and whoever takes it first ends up with it — which *looks* random to the customer and is exactly what the platform does today; or **(ii)** the platform actively **picks** a cleaner and assigns them. I am building **(i)**, because it is what `Q-ASSIGN-01`'s *"promise, not a name"* answer describes (*"the first available cleaner"*), it needs no new dispatch model, and it cannot put a job on someone who never agreed to it. **Say so if you meant (ii)** — that is a genuinely different product, in which a cleaner can be given work they never accepted.

---

### 🔴 Q-PROMISE-03 — [blocking: **YES** for the assignment feature] Two answers you gave a day apart cannot both hold
- Raised by: architect (challenger, assign-and-confirm draft) · Owner: owner · Resolve-by: **before the assignment flow is built** · 2026-08-08
- **This is not a new question. It is a collision between two of your own answers, and neither was given with the other in view.** Surfacing it is the PM's fault for asking them separately.

**Answer A — `Q-PROMISE-01`, 2026-08-07.** Asked whether the screen shown right after booking is telling the truth when it states, as a number, in five languages, on both mobile apps, that a cleaner is assigned **within 1 hour**. You answered: **yes, it is true and must be kept, in PROD.** The copy is live and unconditional today — `booking_success_t2_title` / `_t2_desc`: *"Cleaner being assigned · Within 1 hour"* (verified at `customer-app/src/main/res/values/strings.xml:758-759` and in the iOS catalog).

**Answer B — `Q-ASSIGN-02`, 2026-08-08.** Asked whether two rounds of offering a favourite cleaner is the right cap. You answered: **keep two.** Each round holds the job for **10% of its remaining lead time, capped at 12 hours**, during which **no other cleaner can see or take it**.

**Why they cannot both hold.** Round one alone withholds the job for longer than an hour as soon as the lead time exceeds **ten hours** — on a next-day booking that is about **2 hours 24 minutes**, before the second round. So a customer who picks a favourite cleaner is shown a one-hour promise the mechanism is not trying to keep. The withheld-share invariant the cap is derived from is a **share** of lead time; it structurally cannot bound an **absolute** one-hour figure.

**Three ways out — this is the decision:**
- **(a) Soften the copy when a favourite is chosen.** The one-hour line stays for ordinary bookings; a booking with a preferred cleaner says something else. Cheapest, and it is the only option that changes no mechanism — but it makes the promise conditional, which is a product statement.
- **(b) Cap the hold at one hour** whenever the promise is shown. Keeps the copy honest everywhere and keeps two rounds, but shortens the favourite's window sharply on long-lead bookings, which is where the perk is worth most.
- **(c) Drop the one-hour number** from the post-booking screen entirely and say what the platform actually does. Honest in every case; loses a reassurance customers currently get at the moment they have just paid.
- Default taken: **none.** Nothing is built on either reading, and the assignment feature is held until this is answered — building it under (a) and later choosing (b) means redoing the copy in five languages on two platforms.
- **Answer (owner, 2026-08-08):** **(c) — drop the one-hour number.** The post-booking screen stops stating an absolute time. It says what the platform actually does; it does not promise a deadline the dispatch mechanism has never been able to guarantee — and could not guarantee even before the favourite-cleaner hold existed, since an order can always reach its cleaning time unclaimed.
  **Consequence:** the copy change is a five-locale edit on **both** mobile apps (`booking_success_t2_*` and the iOS catalog twins). Web does not carry this screen. **`Q-ASSIGN-02`'s two rounds are unaffected** and the assignment feature is unblocked.

---

### 🔴 The variable symbol is NOT on the invoice — and I told you twice that it was
- Raised by: backend (`37aec315`, building mark-as-paid) · Owner: **owner + architect** · Resolve-by: **before you pay anyone through this flow**
- Not a question. A correction and a blocking finding.

**What you told me** (`Q-SELFBILL-04`, 2026-08-08): *"We'll send them money manually from our bank account to their bank account when the invoice is created, also putting a special variable number that is written in the invoice."*

**What I told you, twice:** that the variable symbol *"already exists, is generated per employee per pay period, and prints"*, and is *"the reconciliation key"*. I took that from `status/sprint-15.md` §A4 and relayed it without checking.

**What is actually true, verified at HEAD by grep:**

1. `EmployeeInvoice.SetVariableSymbol` and `EmployeeInvoice.GenerateVariableSymbol` have **zero production callers**. The only callers anywhere are four test files.
2. So **every invoice row has `VariableSymbol = NULL`.**
3. The PDF renders the field only when it is non-empty, so **it never prints**.
4. The fallback field `PaymentReference` exists on the PDF model and is **rendered by no layout at all** (`grep` over the whole PDF service returns only its declaration).

**Therefore the payout invoice carries no payment reference of any kind.** The number you put on the bank transfer is not on the document you are transcribing it from, and nothing in the platform can link a transfer back to the invoice it settled. "Mark this invoice paid" currently records a claim that cannot be reconciled against anything.

**And the generator would not be safe to simply switch on.** It is a hash of the employee id joined to a hash of the pay-period id. Within one pay period the second half is constant, so two cleaners are separated only by a 10,000-bucket hash:

| cleaners in one pay period | chance of at least one collision |
|---|---|
| 25 | 3 % |
| 50 | 11.5 % |
| 100 | **39 %** |
| 150 | **67 %** |

A collision hits the database unique index **after** the handler has returned, so it surfaces as an unhandled exception — a poison message for a single invoice, or a **failed batch** in the pay-period job. There is no catch, no business error and no fallback.

**Two green tests are why nobody noticed.** One sets the symbol **by hand** and then proves it maps and validates — the catalog's own named anti-pattern, *a fixture supplying an input production never produces*. Its comment even asserts *"the generated numeric symbol is what reaches the document"*, which is false in production and checked by nothing.

**What I need from you:** nothing yet — this is an architect call on the generator (a per-period sequence, or the existing fiscal-counter claim pattern, rather than a hash), and I will run it. **What you need to know now** is that until it lands, an invoice you pay against has no reference number printed on it. If you have been transcribing something, it is not coming from this document.

---

### 🔴 Q-PROMISE-04 — [blocking: no] The same screen makes a SECOND promise nothing keeps: "We'll remind you 1 hour before"
- Raised by: android (dropping the one-hour assignment promise) · Owner: owner · Resolve-by: pre-prod · 2026-08-08
- **Found while fixing the line directly above it**, which is the part worth noting: `Q-PROMISE-03` was one line of a four-step timeline, and the step two rows down has the same shape.

**The copy** (`customer-app/.../values/strings.xml:763`, and its four translations plus the iOS twins):
*"Cleaning day — **We'll remind you 1 hour before**"*

**What I verified at HEAD:**
- `NotificationEventCatalog` contains **no reminder event of any kind** — the grep returns nothing.
- The reminder jobs that exist are for **recurring-order confirmations** (24 h), **pay periods**, and **membership lifecycle**. There is **no pre-cleaning reminder for a one-off order.**

So a customer booking a one-off clean is told they will be reminded an hour before, and nothing sends that reminder.

**This is deliberately NOT covered by the guard I just added.** That guard forbids an absolute time on the *assignment* step, because assignment is a thing the platform cannot promise. A reminder is different — it is an action the platform **fully controls**, so "1 hour before" is a perfectly keepable promise. **The defect is that it is not implemented, not that it is unkeepable.**

**Which is why the fix is probably the opposite of last time:**
- **(a) Build the reminder.** A timer, an event, a push template on both platforms and the feed row. The promise then becomes true, and it is a genuinely useful notification — arguably the most useful one on the customer's day.
- **(b) Drop the sentence**, as with the assignment line. Cheapest, and it removes something customers likely value.

- Default taken: **none.** I did not silently edit this line while I was in the file, because unlike the assignment promise this one is worth *keeping* and making true.
- **Answer (owner, 2026-08-08):** **Build the reminder.** The line stays and becomes true. This is the case where the promise was keepable all along — a reminder is entirely within the platform's control — so the defect was the missing implementation, not the sentence.
  Scope: a pre-cleaning reminder for a **one-off** order at the stated hour, its notification event, push copy in five locales on both platforms, and the feed row. The recurring-order reminder that already exists is a different job with a different lead time and is not the model to copy blindly.

---

### 🔴 Q-FEED-03 — [blocking: no] "Jobs near you" is country-wide. The apps have been promising proximity that does not exist
- Raised by: backend (partner-notification lane) · Owner: owner · Resolve-by: pre-prod · 2026-08-08
- **You asked for an hourly digest of new jobs *nearby*. The cadence is shipped. "Nearby" is not — and it never was.**

**What the digest actually filters on**, verified at HEAD: `o.CustomerAddress.CountryId == cleaner.WorkCountryId`. That is **country**. There is no radius, no city match, and **zero** references to latitude, longitude, distance or radius anywhere on that code path.

So a cleaner in Prague is notified about a job in Ostrava — 300 km away — and the notification calls it *"near you"*.

**Three shipped strings promise proximity**, on the partner Android app alone (each with four translations, and iOS twins):
- `notification_new_jobs_body` — *"%1$d new jobs available **near you**"*
- `onboarding_ready_body` — *"see available jobs **near you**"*
- `address_why_reason_jobs` — ***"Show you jobs near you, with distance from your home"***

**The third is the one to look at.** It is the justification shown to a cleaner explaining **why the app is asking for their home address** — and it promises *distance from your home*, which is computed nowhere. The address is geocoded and stored; nothing reads those coordinates for targeting.

**Why the data exists but is unused, which is the interesting part.** The service-area model is explicit that it validates the **customer's** address only — its own comment says *"Employee addresses do NOT have to match — cleaners can live anywhere and commute into served cities."* So the platform deliberately does not tie a cleaner to a place, and "nearby" has no definition to compute against.

**This is the same class as the one-hour promise you just dropped**: copy selling something the mechanism does not deliver. It is bigger, though — that was one line, this is a feature.

**What has to be decided before it can even be a ticket:** what defines a cleaner's area?
- **(a) A radius from their home address** — the data exists and is geocoded; it makes the address-permission justification true. But it ties work to where someone lives, which the service-area model deliberately avoided.
- **(b) An explicit opt-in** — the cleaner picks the cities or areas they will travel to. More honest and more work, and it is what the "commute into served cities" comment implies.
- **(c) Keep it country-wide and fix the copy** — cheapest, immediately honest, and loses nothing that exists today. The three strings become *"new jobs available"*.
- Default taken: **none.** The cadence you asked for shipped; the proximity did not, and I did not invent a definition. **Note that (c) is available immediately and independently** — the copy is false today regardless of which way you eventually go.
- **Answer (owner, 2026-08-08):** **Make the distance configurable per employee.** So neither of the three options as written — the cleaner sets their own radius rather than the platform picking one, and rather than the copy being softened to match a country-wide reality.
  **This is a build, and it makes the shipped copy true rather than deleting it** — including the address permission justification, which promises *"distance from your home"*. The one geocoded point on a cleaner is their address, so that is what the radius is measured from unless the owner says otherwise.
  **Two things the answer does not settle and that the build must not guess:** what happens to a cleaner who has **set no radius**, and what happens to one whose address **has no coordinates** — silently sending them nothing would be a regression from today's country-wide behaviour, and silently sending them everything makes the copy false again for exactly the people it is least able to serve.

---

### Q-VS-01 — [blocking: no] Is a Czech variable symbol really ten numeric digits, and is a bare sequence acceptable to your accountant?
- Raised by: architect (payout variable-symbol draft) · Owner: owner (with the accountant) · Resolve-by: pre-prod · 2026-08-08
- Question: the platform encodes *"numeric, at most 10 digits"* for the variable symbol in four places — but **all four are an earlier agent's encoding, not your accountant's**, and the loosest of them accepts one to ten digits. No agent may assert a tax-law requirement. So: is that constraint right, and is a plain sequence (a year followed by a running number) acceptable as the payment reference on a self-billed payout invoice, or does your accountant expect it derived from something — the invoice number, the cleaner's registration number, the period?
- Why it matters: it does **not** block the build — the design fits under any narrower answer. It blocks calling the constraint *verified* rather than *assumed*, and the number goes on a document your accountant reads.
- Default taken: ten digits, a four-digit year followed by a six-digit running number, first digit never zero.
- Answer: _(owner fills in)_

### Q-VS-03 — [blocking: no, but it decides whether a later migration is a contingency or a plan] Does every payout leave one bank account you control — including if a franchise ever runs cleaners here?
- Raised by: architect (challenger, payout variable-symbol draft) · Owner: owner · Resolve-by: pre-prod · 2026-08-08
- Question: the case for one **global** reference namespace rests on the sentence *"the payer's account is one account."* If a franchise operator ever runs cleaners on Cleansia, would they pay **their own** cleaners from **their own** bank account, or would payouts still leave yours?
- Why it matters: if payouts always leave your account, global is right forever. If a franchise pays from its own, then a statement line already belongs to exactly one account, per-tenant references become sufficient, and a cross-tenant volume-inference channel the design currently accepts would be a cost paid for nothing. **It does not block the build** — global is the cheapest correct shape today either way, and the shipped index is already global. It decides whether narrowing that index later is a contingency or a scheduled migration, and narrowing it **fails on pre-existing duplicates** and is owner-only, so it is far cheaper to know before the first franchise has invoices than after.
- Default taken: global, as the shipped index already is.
- Answer: _(owner fills in)_

---

## N16 — a security read of the browse gate found something wider than the gap it was sent for (2026-08-09)

I asked for a characterization of one thing: `OrderAccessService.CanBrowseOrderAsync`
(`src/Cleansia.Core.AppServices/Authentication/OrderAccessService.cs:88-91`) admits a cleaner on
`HasAvailableSpots && NotHeldFrom` with **no offerability conjunct**, so it disagrees with the board,
the take gate and `GetMyPendingOffers`, all of which read `OrderAvailability`. That is real and it is
recorded in two places in the tree as *verified and deliberately not touched*.

**It is the second-most-important thing missing from that gate.** The first is that `GetOrderDetails`
applies **no pre-take PII redaction at all**, while its sibling list handler applies a documented one
(`GetPagedOrders.cs:180-194`: *"Full PII, the exact geocoded coordinates, and the confirmation code
stay hidden until the caller takes the job"*). So for every order on the Available board, one extra
`GET /api/Order/GetById` returns exactly what the list just withheld — customer name, e-mail, phone,
full street address with latitude and longitude, the `AccessInstructions` **door code**, the
confirmation code, and the other cleaners' personal phone numbers. `GetOrderPhotos` likewise hands a
non-assignee a one-hour SAS URL per photograph of the customer's home interior.

**Being fixed now, no owner input needed:** the redaction parity and the photos gate. Nothing here is
a new rule — it is the list's own rule applied to the detail, and the write paths for photos were
already assignment-gated.

**Four things that are NOT being fixed silently, listed so they are yours to see:**

### Q-BROWSE-01 — [blocking: no] Should the browse gate refuse orders a cleaner could never take?

Adding the offerability conjunct makes the detail agree with the three surfaces it currently
contradicts. **It has one real cost**, which is why it is a question and not a fix: the
preferred-offer push fires at order *creation* (`OrderFactory.cs:196-207`), when a **card** order is
`New` + `Pending` and therefore not yet offerable, and its deep link opens the order detail. Today
that tap works. After the conjunct it fails until the Stripe webhook lands — seconds usually, longer
when Stripe is slow. Cash orders are unaffected.
- **(a)** Add the conjunct and accept the dead window on card orders.
- **(b)** Add the conjunct and delay the preferred-offer push until the order is offerable — the
  cleaner is told slightly later, but every notification we send then leads somewhere.
- **(c)** Leave the gate wide, now that the PII behind it is redacted.
- Default taken: **none.** With the PII fix landed the leak is closed either way, so this is now a
  correctness-and-consistency call rather than an urgent one.

### Q-CREW-01 — [blocking: no] May one cleaner complete a job that was booked for two?

`CompleteOrder.Validator` has **no full-crew rule**, and nothing frees or fills a seat on a terminal
transition. So a 2-seat order (any 3-hour booking — `RequiredEmployees = ceil(180/120) = 2`) that one
cleaner took and finished alone stays `Completed` with **seat 2 open forever**. That is what makes the
browse gate's seat term reach finished jobs at all. Two separate questions in it: should the platform
**stop** a solo completion, and should the customer be **told** their two-cleaner booking was worked
by one? I did not guess either.

### The mobile partner API admits an unapproved account — being fixed as parity, not a decision

`MobilePartnerLogin.Handler` gates on `IsActive` and profile only — **no `ContractStatus`, no
`IsEmailConfirmed`** — and the mobile partner order controller carries **no `[RequireCompleteProfile]`**,
which the four partner *web* controllers do. Registration is `[AllowAnonymous]`. So a self-registered,
never-approved account can read order detail on `:5002` today. The web/mobile asymmetry is a defect
rather than a design, so it is being closed to match web. Flagging it because it changes who can sign
in to the partner app, which you may want to know before the next TestFlight build.

### Two gaps recorded rather than fixed

- `Order/GetById` and `Order/GetPhotos` have **no cross-tenant host test**. The code path is correct —
  both go through the tenant query filter — but the pin is missing while four sibling routes have one.
- Photo rows written **before** ADR-0043 landed may still carry EXIF; there was no backfill, and
  whether any such rows exist was **not established** (it needs the database, not the tree).

---

## N17 — manual steps the branch has accumulated (2026-08-09). **All owner-run; none blocks further work.**

Recording them together because three separate lanes each hit one, and the third is not the one anybody
would predict.

**1. `manual_step: nswag-regen` — web.** Two features shipped server-side with no web client:
`UpdateJobRadius` (`ae86be42`) and ADR-0045's four preferred-offer endpoints (`51799dbe`). The partner
and customer TypeScript clients cannot call either until the specs are regenerated.

**2. `manual_step: mobile-spec-regen` — Android and iOS.** Sharper than the web one, because it fails
**silently** rather than by absence. `src/cleansia_android/openapi/partner-mobile-api.json` was dumped
2026-08-08 13:37; the radius landed at 22:31 the same day. So the committed spec has no
`UpdateJobRadius` path — and, quieter, **no `jobRadiusKm` field on `EmployeeItem`**, which means the
profile call the app *already makes* decodes the value away. Nothing warns; the field simply arrives as
null forever. Both mobile lanes worked around it with a hand-written client on the existing
`PeriodPayApi` precedent, and both collapse into the generated client at the next dump.

**3. `manual_step: ef-migration`.** `Initial` has been amended in place on this branch (the pre-prod
convention), most recently by ADR-0045's preferred-offer columns and the radius column. **DEV needs a
database drop and re-seed** before it will start against this branch.

**Not a manual step, but it belongs beside them:** ADR-0046's counter table is an owner-only migration
that rides **nothing** — the ADR originally planned to fold it into a pending T-0522 pass, and that
pass turned out to have already landed. It is its own window, and there is no code for it yet.

---

## N18 — the PII fix landed, and it left two follow-ups plus one question (2026-08-09)

`b2a8cf62`. Every field is mutation-proved (25 mutations, all red, each restored byte-exact) and the
half-crewed scenario is pinned over real Postgres. Three things it did **not** decide:

### Q-DISCOUNT-01 — [blocking: no] Should a browsing cleaner see that the customer holds a Plus plan?

`TierDiscountAmount`, `MembershipDiscountAmount` and `PromoDiscountAmount` are **kept** on the redacted
detail. Each is a non-zero number that discloses the customer holds a loyalty tier, a Cleansia Plus
membership, or used a promo code. They were kept for a defensible reason — **they are already unblanked
on the list row today**, so blanking only the detail would recreate the list/detail mismatch this whole
fix exists to remove — but "consistent with the list" is not the same as "right". Blanking both is a
behaviour change on a surface nobody asked about, so it was flagged rather than taken.
- **(a)** Blank all three on both surfaces for a non-entitled cleaner. The cleaner still sees
  `TotalPrice` and their own `EstimatedCleanerPay`, which is what the take decision needs.
- **(b)** Keep them. A discount amount is arguably commercial rather than personal.
- Default taken: **(b) by inaction**, and that is exactly why it is written down.

### Two follow-ups the fix created, being closed on this branch

1. **iOS shows a spurious error toast.** `OrderDetailView.swift:45` fires `photosVM.load()`
   unconditionally on every detail open, while the photos *section* is correctly gated on assignment.
   With photos now behind the strict gate, a cleaner opening an **Available** order gets a toast reading
   *"Order not found."* over a screen that is otherwise correct. One line on the iOS side; routed.
   Android and web are unaffected — both gate the fetch itself.
2. **A browsing cleaner now sees no location at all on the detail.** `OrderItem` has no coarse-location
   counterpart to `OrderListItem`'s approximate address, so blanking the address record removes the zone
   as well as the street. The cleaner sees the zone on the Available card they tapped and nothing on the
   detail they opened. Adding a coarse field is a DTO shape change (`manual_step: nswag-regen`), so it is
   a follow-up ticket rather than part of the security fix. **No client drops the row** — iOS, Android and
   web all handle a null address and gate their maps on coordinates.

---

## N19 — the partner apps never tell the server what language the cleaner picked (2026-08-09)

Raised by the Android lane at the end of an unrelated ticket, verified by me before it was actioned.

`agents/knowledge/patterns-mobile.md:558` already states the rule — *"a local preference the server
also stores needs a sync seam, not just a DataStore write"* — and names the **customer** implementation
plus its iOS mirror. The **partner** apps have none, on either platform: all three `setLanguage` call
sites write DataStore and stop.

**What that actually costs is worse than mis-addressed email.**
`PayPeriodBackgroundService.cs:230` reads `employee.User.PreferredLanguageCode ?? "en"` and threads it
through **two** things — the period-closed email **and the rendered invoice PDF** (`:237`, `:269`,
`:301`, `:341`). So a cleaner who switches the app to Czech keeps receiving their **payout invoice, a
tax document they file**, in whatever language signup happened to send. The app shows them one language
and the document that matters is in another, and nothing on either side ever disagrees out loud.

Being fixed on both platforms, no owner input needed. The difficulty is not the push — it is that the
server's update path takes a **profile**, so a command carrying only the language blanks every other
field on the row. That is why the customer app has a seam rather than a line in a view model.

**This also closes the loop on why the E9 allowlist read the way it did.** `consistency.md:279` exempts
`AppSettingsRepository` from the session-wipe set because it *"holds device-level prefs"* — and the
language genuinely is one. What the allowlist could not say is that a device-level preference may still
have a server-side twin. Those are different questions, and this is the case where they part company.

---

## N20 — three rows the lanes produced that are yours, not mine (2026-08-09)

Each was found by a lane doing something else, each was declined by that lane for a stated reason, and I
agree with all three refusals.

### 1. The eager type-catalog probe spends up to 15 s inline in worker composition

`DbContextBindingExtensions.TryEagerlyReloadTypeCatalog` makes a **synchronous** `OpenConnection()`
inside `ConfigureServices`, on one host only — the isolated Functions worker — and the justification is
real and documented: its timer triggers can fire before `IHostedService` start, so the catalog must be
seeded before any consumer can reach the data source. **Making it async would break the ordering
guarantee that is the whole point**, so that is not the fix.

What costs is the **latency**: no retry, and Npgsql's default 15 s connect timeout, spent inline
whenever Postgres is not yet up — which under Aspire is the normal cold-start ordering rather than an
edge case. The narrow row: *bound the probe's connect timeout independently of the connection string*
(a few seconds), keep it synchronous, keep the swallow.

**One caveat the lane volunteered rather than smoothing over:** it also observed **two** sockets per
composition where the code makes one `OpenConnection()` call, consistent with `SslMode=Prefer` retrying
in plaintext — so the worst case may be two timeouts, not one. That doubling is **inferred from the
socket count, not confirmed** against Npgsql's source or a capture. The fix direction holds either way;
only the worst-case number moves. Worth five minutes before sizing the bound off "15 doubled".

It touches a host-start path on a live environment, which is why it is here rather than on the branch.

### 2. There is no reconcile-on-sign-in for the language push

Both partner platforms now push the cleaner's language to the server, and both are **silent on
failure** — the local preference stands, the person is told nothing, and there is no retry. The
self-heal is a subsequent language change, since the seam re-reads the server's code and re-pushes on
any drift.

The customer app does not have this exposure because its ordinary profile save carries the language;
`UpdatePersonalInfoCommand` has no language field, so the partner has no second path. A
reconcile-on-sign-in — call the seam once after login — would close it on both apps and both platforms.
**Deliberately not invented in either lane**, because a retry story is a behaviour the other platform
then has to reproduce exactly, and inventing it twice independently is how they diverge.

### 3. `MapToDto(OrderListRow)` re-derives seat arithmetic the entity already owns

`OrderMappers.cs:94-95` computes `availableSpots = row.MaxEmployees - assignedCount` and
`HasAvailableSpots: availableSpots > 0` — a second copy of `Order.cs:136-137`. It is currently pinned
(`OrderListProjectionEquivalenceTests` compares both `MapToDto` overloads by full serialization, so any
divergence reddens), so this is tidiness with a guard already on it, not a live risk. Touching the list
projection carries more risk than the tidiness buys. **A ticket, not a drive-by.**

---

## N21 — OWNER ANSWERS, 2026-08-09 (batch 7). All seven recorded verbatim.

### ✅ Q-VS-01 — ANSWERED: **"Confirm"**
The ten-digit numeric constraint is real and a bare sequence is acceptable to the accountant. ADR-0046's
format (`YYYY` + a six-digit per-year ordinal, first digit never zero) is now **verified, not assumed**,
and §D8's caveat that no agent may assert a tax-law requirement is discharged. Nothing to build — the
design already fits.

### ✅ Q-VS-03 — ANSWERED: **"No, we won't have franchises, DON'T OVERCOMPLICATE THINGS"**
Payouts always leave one bank account the owner controls. So the **global** reference namespace is right
**permanently**, not contingently, and `IX_EmployeeInvoices_VariableSymbol` on the bare column is its
final shape.

Two consequences, and the second is the instruction rather than the answer:
1. ADR-0046 §D3.2's written-down flip — *"add a tenant term to the counter key and replace the index with
   `(TenantId, VariableSymbol) NULLS NOT DISTINCT`"* — is **retired**, not merely deferred. The
   cross-tenant volume-inference channel it was hedging against is a cost paid for a scenario that will
   not happen.
2. **"Don't overcomplicate things" is a standing instruction and is recorded as one.** The contingency
   above cost real words in an ADR, and a reviewer check, for a case the owner already knew was
   impossible. The lesson for future panels: **ask whether the hedge's premise is real before pricing the
   hedge.** A written-down flip is not free — it is a thing every future reader has to read.

### ✅ Q-BROWSE-01 — ANSWERED: **option (b)**
Add the offerability conjunct to the browse gate **and** delay the preferred-offer push until the order
is offerable. So the cleaner is told slightly later, and every notification we send leads somewhere.

This is a build on both halves, and it also closes an exposure the owner did not have to weigh: a
`Completed` order is not offerable, so the half-crewed finished job whose seat stays open forever stops
being browsable at all. That is the interaction with Q-CREW-01 below, resolved by this answer rather than
by that one.

### ✅ Q-CREW-01 — ANSWERED: **"if there is no second cleaner then there is no option for us and 1 of the cleaners have to complete the job"**
Solo completion of a two-seat job is **allowed and must stay allowed**. No full-crew rule on
`CompleteOrder`. Nothing to build; the current behaviour is ratified.

The second half of the question — whether the **customer** is told their two-cleaner booking was worked
by one — was not answered and is **not re-asked**. Default taken: **no notification**, because the
customer already sees the assigned crew on their own order detail, and a message whose only content is
"fewer people came than planned" invites a complaint about a service the owner has just said we have no
alternative to. If that default is wrong it costs one string, not a design.

### ✅ Q-DISCOUNT-01 — ANSWERED: **"yes, he would see that he sees the benefits"**
The three discount amounts stay visible to a browsing cleaner. The default taken by inaction is now a
ruling, which is the whole reason it was written down. Nothing to build.

### ✅ Q-IOS-04 — CLOSED as already built, not answered
The owner delegated it (*"do what you think is the best for long-term approach"*). Checked the tree
before deciding: **the decision was already made, and made the same way.** `AppleAuth` exists as an
anonymous endpoint on **both** customer hosts — `Cleansia.Web.Mobile.Customer/Controllers/AuthController.cs:62`
and `Cleansia.Web.Customer/Controllers/AuthController.cs:65` — sharing one command and handler
(`Features/Auth/AppleAuth.cs`), exactly the `googleauth`-analogous shape the question offered as its
first option, with the browser flow as the only difference. It shipped in the July batch (`798e16e3`).
Nothing to build and nothing to ratify.

### ✅ Q-AZURE-01 — ANSWERED with data, and one premise in the answer is wrong
The owner supplied both figures the question asked for: **Log Analytics €49.29** is the largest service
line (App Service €23.51, Container Registry €5.46, Storage €2.56, **Azure Monitor €0.63** — so the
investigation's "Alerts are €0.63 and not €50" attribution is **confirmed**), and ingestion is
**27.29 GB/month → $81.61 at list**, all of it Analytics-tier, zero Basic and zero Auxiliary.

The owner's conclusion — *"I don't think that we need this one since we have App Insights and Sentry"* —
**cannot be executed as written**, and this is recorded rather than quietly reinterpreted:
`deploy/bicep/modules/appInsights.bicep:59` sets `WorkspaceResourceId: logAnalytics.id` and
`IngestionMode: 'LogAnalytics'`. **Application Insights is workspace-based here — the €49.29 "Log
Analytics" line IS App Insights' data.** Deleting the workspace deletes App Insights with it. There is no
version of "drop Log Analytics, keep App Insights" that exists.

**What does deliver the saving, on the same bill:** the volume, not the product. Two settings, both
already parameterised and both currently at their most expensive values on DEV:
- `appInsights.bicep:22` — `samplingPercentage = env == 'prod' ? 50 : 100`. **DEV samples nothing.**
- `appInsights.bicep:25` — `dailyCapGb = env == 'prod' ? 5 : 1`. 27.29 GB over 31 days is ~0.88 GB/day,
  so DEV is running just under a cap that has therefore never fired.

Sampling DEV at 20–25 % takes the line down roughly proportionally with no code change and no loss of
exception telemetry (`host.json` already excludes `Exception` from sampling). **The genuinely free half
first:** find out which tables the 27 GB is in before choosing a number — the original question's second
query. If it is `AppDependencies` (every SQL round trip), the honest fix is narrowing what is collected
rather than sampling what we then cannot read.

---

## N22 — the manual-step list was incomplete, and N17 has gone stale (2026-08-09)

An audit reconciled every `manual_step` implied by the branch's code against what N17 actually filed.
**N17's three are correct and stand.** Four more were implied by commits and filed nowhere — I grepped
`open.md` for each and got zero hits.

**Add to the regen pass:**

4. **`nswag-regen` — admin: `AssignInvoiceVariableSymbol`.** This is the one with **no paper trail at
   all**: not in `d410f002`'s message (which flags `ef-migration` only — the word "nswag" does not
   appear), not in N17, not on the T-0573 row. `AdminPayrollController.cs:67` exposes the route, its
   four error keys ship in five admin locales, and `admin-client.ts` contains zero occurrences of it.
   So ADR-0046's own remedy — the command that repairs a symbol-less invoice — **has no button**, while
   `MarkInvoicePaid` now hard-refuses exactly the rows it repairs.
5. **`nswag-regen` — partner: the `updateEmployee` return type** (`86229699`). Flagged in that commit,
   never rolled into the register.
6. **`nswag-regen` — all three web clients: `OrderItem.CustomerAddressApproximate` and the five seat
   members** (`572ce5d2`, `538af8f6`). Flagged in both commit messages; N18 mentions the coarse address
   in prose; the seat members appear nowhere.

**And a correction to N17 itself:** its closing note says ADR-0046's counter table *"is its own window,
and there is no code for it yet."* `d410f002` shipped the code and folded the table into `Initial`
1 h 46 m later, so it **rides item 1's window** rather than needing a second one. That note was true
when written and false ninety minutes later — the same decay class as N21's stale questions and the ten
catalog claims fixed in `1aee881a`.

**Practical ordering, unchanged in shape:** drop and re-seed DEV → **one** regen pass covering items 2,
4, 5 and 6 together (all additive, no ordering constraint between them) → the mobile spec dump → rebuild
the mobile clients.

---

## N23 — partner web's language sync landed, and it surfaced two things bigger than itself (2026-08-09)

`14c1f78f`. Partner web was the third surface that never told the server the cleaner's language — the
register entry that scoped this work said *"both platforms"* and never considered the browser.

**A correction to my own brief, and it is worse than I framed it.** I told the lane to grep two roots and
it found a third: `libs/data-access/partner-stores/src/lib/user/user.effects.ts:110` **does** call
`updateCurrentUser` and **does** set `languageCode` — but the action has **zero dispatchers**, and the
partner profile page saves the *Employee*, never the user. `selectCurrentUser` has zero readers. So
`User.PreferredLanguageCode` had **no writer at all** on partner web, not merely no language writer.
(Filed below as a cleanup, not deleted in-lane, because deleting it would have obscured the diff.)

### A cross-stack testing fact that two independent lanes hit today

Both are recorded because two instances in one day is a pattern, not an anecdote, and neither is
obvious from reading the test.

1. **iOS** (`d1784833`): assertions distinguishing `.gmt` from `TimeZone.current` are **unobservable
   when the machine is GMT** — live on a CEST desk, **inert on a UTC CI runner**.
2. **Web** (`14c1f78f`): the same, plus a mechanism — **V8 caches the timezone at isolate startup**, so
   writing `process.env.TZ` inside a spec is *silently ignored* (measured: the offset stayed `-120`).
   The lane's first mutation **survived** because of it. Pinned via a Jest `globalSetup`, with a guard
   test that reddens if the pinning is removed.

**The uncomfortable half:** CI is a UTC runner, so for this defect class **no probe of any shape can
expose the bug there** — local and UTC days coincide by definition. These tests protect a developer
machine and a real user's handset; they cannot protect the pipeline. That belongs in the catalog next
to the existing evidence rules, and it is **filed rather than written** because the rule that landed in
`1aee881a` is about claims decaying and this is a different law needing its own panel.

### Two findings the lane declined, correctly

- **A failed language push still raises a toast.** `HttpErrorInterceptorFn`
  (`libs/core/services/.../http-error.interceptor.ts:31`) fires on every non-404/403 error with **no
  per-request opt-out** — no `HttpContext` suppression mechanism exists anywhere in the workspace. The
  facade is silent; the interceptor is not, which contradicts the "silent on failure" contract all three
  surfaces now share. The fix is a suppression token on a **shared, three-app** interceptor plus a
  catalog entry — a cross-app mechanism a feature lane may not ratify. Exposure is low: every business
  validator on this path passes by construction, so only network and 5xx reach it.
- **Dead code:** the `updateUserCurrent` action, its `updateCurrent$` effect and `selectCurrentUser` in
  `libs/data-access/partner-stores` have no dispatchers or readers.

---

## N24 — a catalog entry two lanes independently reached for, and it cannot be written inline (2026-08-09)

Both the Android detail-rendering lane and the backend redaction lane arrived at the same sentence, and
neither could land it:

> **A redacted field's replacement is rendered off the field's own arrival. The client never re-derives
> entitlement, and never hides a field the server populated.**

It is the rule that decided the Android predicate correctly. `isAssignedToCurrentUser` **is** on the
wire, so choosing `address == null` instead was a real choice — and the right one, because `b2a8cf62`
redacts on `CanAccessOrderAsync`, deliberately **not** on assignment: an employee who books a cleaning
for their own home arrives at that handler as the order's **customer**, with a full address and
`isAssignedToCurrentUser` false. A client gating the address on that flag would redact their own data
from them, and would be a second authorization implementation living beside the server's.

**It routes to the Architect and cannot be written inline, because Test 1 fires.** A sweep of the partner
app returns shipped call sites that gate *populated* fields on exactly that flag — the phone and SMS
chips on `CustomerCard`, the access-instructions card, the work sections, and three arms of the primary
action. An entry worded as above puts all of them in violation, so it needs a deviation entry and a
canonicalization ticket alongside it, which a feature lane may not file for itself.

Recorded here so the next panel has the sweep already done. Catalog files searched, for the record:
`patterns-mobile.md` and `consistency.md` for `redact|PII|coarse|approximate|entitle` — nothing governs
client-side rendering of a server-redacted field today (`consistency.md:297` is header redaction in
logging; `patterns-mobile.md:1227` is a repository read-scope gate).

**Update, same evening: the iOS lane reached the same entry independently and routed it to the Architect
for a DIFFERENT reason, which is worth more than either argument alone.** Android routed it because
**Test 1 fires** — shipped call sites gate populated fields on `isAssignedToCurrentUser`, so the entry
puts them in violation. iOS routed it because **Test 2 fires**: `patterns-mobile.md:430-447` already says
*"a number the server computes has no client-side twin … render the discriminator, never re-derive it"*,
which governs this at a level of generality that covers it — so a redaction-specific form is **carving
within an existing sentence's scope, not filling a vacuum**.

Two lanes, two platforms, two independent routing tests, same destination. The entry the panel writes
should probably be a **narrowing of `:430-447`** rather than a new law, with the deviation entry Android's
sweep already enumerates. iOS's own suggested wording, for the panel to start from: *"when the server
redacts a field for a caller class and ships a coarse substitute, the client models the pair as one
sealed three-case value discriminated by the **arrival** of the precise field — never by a flag that
re-derives entitlement client-side."*

---

## N25 — the partner-web radius control, and a testing hole under the whole workspace (2026-08-09)

`89c8da1f`. All three partner surfaces now have the control.

### The finding that outlives the ticket

**`jsdom` has no `Blob.prototype.text`, so the shared error interceptor's blob branch is untested across
the entire workspace — and it is the branch production takes.**

The NSwag clients issue `responseType: 'blob'`, so every real refusal arrives as a Blob and
`parseBlobToJson` is what turns it into a translatable key. Under jest that rejects, and **every**
refusal silently falls through to the generic `api.common.error_occurred` catch. The lane's five locale
cases were red for exactly this until it added a spec-local polyfill.
`libs/core/services/src/lib/interceptors/http-error.interceptor.spec.ts` only ever exercises the
**non-blob** branch — the one production does not take.

So the interceptor that every app depends on to turn a backend refusal into a sentence a person can read
has its real path covered by nothing, and any test asserting "this refusal renders its message" is
passing on the fallback rather than the message. That is the same class as the boot-IO assertions and the
birth-date assertion found earlier today: **a guard that passes without looking.** It wants a shared
polyfill in the jest setup plus a test of the blob branch itself, and it is a cross-app change on a
shared file, so it is filed rather than done in a feature lane.

### A decision worth preserving, because "do what mobile did" would have been wrong

The lane **declined** to build the one-time onboarding prompt on web, and argued it rather than skipping
it:

1. The radius tunes the **push** digest, and partner web has **no push channel at all** — a sweep for
   Firebase / web-push / service-worker / device registration across the partner app and its libs returns
   nothing. A web-only cleaner would be asked to tune a notification that surface never delivers.
2. Web lands on Orders, not a dashboard, so mobile's *"the only screen every cleaner opens"* argument
   does not transfer; the analytics dashboard would spend the ask on the smallest audience.
3. A **third** per-client "already asked" flag re-asks the cleaner who answered *"keep every job"* on
   their phone — that answer is a **null radius**, invisible to another client's flag. Two clients already
   carry that seam; a third on the weakest surface worsens a known defect for no delivery benefit.

If web push for partner ever ships, reason 1 retires and it is worth revisiting.

### Two smaller ones

- **The wire form diverges deliberately from iOS.** Web sends `radiusKm` **absent** where iOS sends an
  explicit `null`: the generated member is `number | undefined`, so assigning null is a type error and
  hand-editing the client is forbidden. Both bind to the same `int?`, and the backend lane's binder probe
  established that absent and null are equivalent. Pinned over the real bytes either way, and never `0`.
- **Copy, pre-existing, cross-app:** `api.employee.job_radius_out_of_range` reads *"The travel radius you
  entered…"* in all five partner locales. "Travel radius" mildly implies an eligibility or commute
  constraint rather than a notification one, which is the exact confusion the whole feature's copy works
  to avoid. Left alone — it is a copy decision spanning three surfaces.

---

## N26 — the customer half of ADR-0045 shipped, and it confirmed a backend finding twice over (2026-08-10)

`e9815f75` + `677da8cb`. The web wizard never had a partial picker — it had a **deliberate refusal to
build one**, in a comment at the submit site (*"`preferredEmployeeId` waits on the Plus rollout to
surface a cleaner picker"*), which ADR-0045 §D12 and ADR-0039 §D7.2 both cite as the known gap. It is
built now.

### `PreferredOfferExit.IsOpen` — confirmed reachable, and there is a second hole beside it

The backend lane filed this and declined to fix it because it changes what a **customer** may do. The
web lane independently reached the same line and confirmed the exploit path exactly:

```csharp
order.RecurringTemplateId is null
&& order.PreferredOfferRound < BookingPolicy.MaxPreferredOfferRounds
&& order.AssignedEmployees.Count == 0
&& !PreferredOffer.HasLiveReservation(...)
&& BookingPolicy.ComputePreferredHold(order.CleaningDateTime, nowUtc) > TimeSpan.Zero
```

A **cancelled** order with a future cleaning time and no assignment satisfies **every term**, so
`canChooseAnother` is true and `ChoosePreferredCleaner` would grant a hold and **push a named cleaner
about work nobody will do**. (`Completed` is blocked only incidentally, by the assignment count, not by
intent.)

**Second hole, found while building and closed client-side in `677da8cb`:** `IsOpen` says nothing about
the **caller**, but the validator's first gate is an active Plus membership — so the flag alone offered
the re-choose to every non-Plus customer and refused it on tap.

The client now conjoins `orderStatus ∉ {Cancelled, Completed}` and the membership check — both strict
narrowings of the server's own answer using values already on the same screen, never re-derived policy
constants. **Both narrowings should be DELETED when `IsOpen` gains its offerability and caller terms.**
That is the backend row, and it is now confirmed from two directions rather than one.

### A pre-existing copy defect, left for the affirmative-copy wave

`membership.benefit_favorite_body` uses an **assignment verb** in cs and sk — *"bude přednostně
přiřazen"* / *"bude prednostne priradený"* — on the membership benefits page. ADR-0036's copy constraint
and ADR-0045's whole design forbid telling a customer their favourite **will be assigned**; the truth is
that they are **asked**. It is outside the feature's own keys and lives in a shared bundle another lane
is editing, so it was flagged rather than reworded mid-flight.

### One correction to my own brief, worth recording

I told the lane to follow what the two mobile apps render while an offer is pending. **Neither renders
anything** — a grep for `preferredOffer` across both trees returns only push and feed template
registrations. Both have the booking-time picker and the closure push; **the pending state is unbuilt on
every platform.** So web derived its copy from the ADR plus the one shipped corpus that does exist — the
five-locale closure push strings — rather than from a shape that was never there.

---

## N27 — one shape question the gate fix raised, for an Architect rather than a lane (2026-08-10)

`PreferredOfferExit` is a **pure predicate type**. Closing the caller-term gap gave it an **async,
repository-taking helper** — `CallerHasActiveMembershipAsync(session, membershipRepo, ct)` — sitting
beside that predicate, because the alternative was two implementations of *"is this caller an active
member"*: one in `ChoosePreferredCleaner.Validator` and one wherever the read path resolved it.

The lane judged one implementation worth more than the type's purity, and said so rather than quietly
choosing. I agree with the call — it is the exact drift class that ticket existed to close, and the
disagreement it was closing had already cost three client lanes a workaround each. But it is the kind of
thing a catalog entry could rule either way, and it is now a precedent whether or not anyone ratifies it.

**Not blocking anything.** Recorded so the next reader of that file finds the reasoning rather than
re-deriving it, and so a panel can rule on the shape without re-discovering the trade.

---

## N28 — the two catalogs had drifted 39 ways, and reconciling them found a grammar trap (2026-08-10)

`81a19cb4` + `ffca9830`. The last of the ADR-0045 client wave, and the finding is about **translation
review**, not about offers.

Both mobile lanes translated the same 21 keys independently, before the agree-the-wording instruction
existed. Reconciling them found **39 divergences across cs, sk, uk and ru**; 33 strings were adopted from
Android and 102 of 110 shared cells are now byte-identical, asserted programmatically after unescaping
rather than eyeballed.

**The one that mattered most was a dropped actor.** Android's cs read *"Zakázku se **nám** nepodařilo
předat"*; iOS's read *"Zakázku se nepodařilo předat"*. The English is *"**We** couldn't hand this job
over"* — and the whole point of that framing is that the platform takes the blame for reserving a job
without checking the cleaner's cap. An impersonal rendering deletes the apology and leaves a bare report.
Same in sk, uk and ru. **A translation can be word-correct and still lose the only thing the sentence was
for.**

**And one went the other way — Android had a grammar error in six strings.** sk and ru used the neuter
*"Vaše …"* / *"Ваше …"* for a referent that is feminine in Slovak (`zákazka`) and masculine in Russian
(`заказ`). iOS declined to adopt it and raised it instead of silently diverging.

### The trap, which is why "make them all match" would have been wrong

Czech and Ukrainian were already correct **for different reasons**: Czech's feminine nominative singular
of *váš* is **syncretic with its neuter**, so `Vaše` is right in cs and wrong one border away in sk. A
well-meant sweep making all four locales match Slovak would have **broken two to fix one**. The guard
therefore pins the expected possessive **per locale**, with the referent reasoning in its doc comment, and
its four mutations run in both directions — reintroducing the error in one string of a language, and
"fixing" a correct language to match a different one.

Two smaller things the referent check produced that a pattern-match would not have:

- **Slovak had no ambiguity to resolve** — both candidate referents (`zákazka`, `ponuka`) are feminine.
- **Russian very nearly did.** The ru copy previously carried a competing **neuter** noun,
  `предложение`, in the release-failure body — under which the neuter form would have been arguable. The
  wording convergence two commits earlier had replaced that sentence with one using `заказ`. As the lane
  put it: *"had I done this check before the convergence I would have had to raise it."* The correct
  answer depended on a change made for an unrelated reason an hour before.

**For the next copy wave:** a five-locale parity guard proves the keys exist and the placeholders survive.
It cannot see a dropped actor or a wrong gender, and both shipped past one today.
