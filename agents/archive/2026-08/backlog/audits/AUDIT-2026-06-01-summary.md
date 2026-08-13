# Codebase Audit — Consolidated Backlog (2026-06-01)

- **Auditor:** orchestrated workflow — 8 domains × 4 dimensions (analyst/reviewer/security/optimizer),
  adversarial verification of every critical/major finding, opus synthesis + completeness critic.
- **Scope intended:** all backend feature subsystems, the 3 Angular apps, the 2 Android apps.
- **Method:** real-code reading grounded in file:line, against `knowledge/conventions.md`,
  `consistency.md`, `security-rules.md` and the order/pay lifecycles.

## ⚠️ Coverage caveat — read this first
This run had a **tooling failure: 23 of 32 investigator slices did not return structured output**, so
the consolidated findings are weighted toward **orders/booking, pay/payroll, and payments-fiscal**.
The **loyalty-growth, disputes-addresses, identity-auth, catalog-config, and employees** domains are
**under-represented** — their low ticket count reflects the failure, **not** clean code. The findings
JSON was also truncated mid-finding at PAY-6. **These domains must be re-audited** (a clean re-run is
queued as a follow-up). Treat this report as a strong-but-partial first pass, not a complete bill of
health.

81 findings were confirmed (1 refuted) across the 9 slices that succeeded.

---

## 1. Executive summary

The platform is **functionally mature but operationally incomplete**: the customer/partner/mobile
happy paths are well-built, yet large swaths of the **back-office (Admin) and payroll lifecycle are
dead or unreachable** — domain methods, enum states, and UI chips exist with no command/endpoint/UI to
drive them. **No verified finding in the covered slices is a security, data-loss, or crash defect** —
every issue is a missing capability (`gap`), a structural smell (`spaghetti`), a magic value
(`hardcoded`), or a display bug. *(But the critic flags a suspected webhook-signature gap the domain
split walked past — see §4.)* Code health is reasonable but uneven: a handful of god-units
(`CreateOrder.Handler`, `order-wizard.facade.ts`, `HandlePaymentNotification`) concentrate
disproportionate risk on the most business-critical write paths, and the project's own conventions are
violated in well-understood, repeatable ways beyond the 187 already-catalogued.

**Top 3 risks before PROD:**
1. **No admin order intervention** (AUD-01) — back-office can observe but cannot cancel, reassign,
   refund, or override status on *any* order; incidents force manual DB edits.
2. **Payroll lifecycle is half-dead** (AUD-02/04) — bonus/deduction adjustments, `PayPeriod.Paid`,
   invoice Dispute/Reject, period reopen, and failed-PDF recovery are unreachable; payroll cannot be
   corrected or fully settled in-system.
3. **CQRS/quality erosion on the money & booking paths** — a read query that silently writes (AUD-17),
   a 297-line untested webhook handler (AUD-20), god-units on booking (AUD-06/07) — the most critical
   paths are the riskiest to change, and **have ~zero tests** (per the critic).

**Single highest-value fix:** **AUD-01 — Admin order operations end-to-end**, which also unblocks the
generalized cancellation backend the rest of the order-intervention story depends on.

---

## 2. Ranked ticket list (AUD-01 … AUD-25)

### Major
| ID | Title | Type | Size | Layers | ADR? |
|---|---|---|---|---|---|
| **AUD-01** | Admin order operations + generalized cancellation (cancel / reassign / refund / status-override) | gap+bug | L | backend, domain, admin-api, frontend-admin | **Yes** |
| **AUD-02** | Wire up the dead payroll adjustment & settlement lifecycle (bonus/deduction, PayPeriod.Paid, invoice Dispute/Reject, period Reopen) | gap | L | backend, admin, partner, mobile | **Yes** |
| **AUD-03** | Build admin Extras management (CRUD + translations + pricing) | gap | L | backend, admin-api, frontend-admin | No |
| **AUD-04** | Reconcile partner payroll surface (cleaner "my period pay" screen, prune admin-only endpoints off partner host, surface failed-PDF invoices) | gap | L | backend, partner, mobile, admin | **Yes** |
| **AUD-05** | Add order-cancellation flow to the customer **web** app (parity with mobile) | gap | M | frontend-customer | No |
| **AUD-06** | Decompose `CreateOrder.Handler` god-handler (484 lines, 15 deps) into collaborators | spaghetti | L | backend | No |
| **AUD-07** | Split `order-wizard` god-facade (1048 lines) + migrate to the C3 pipe | spaghetti | L | frontend | No |
| **AUD-08** | Move ownership/profile checks to handler & resolve caller once in Take/Complete/Start order (B4/S3) | spaghetti | M | backend | No |
| **AUD-09** | Add `RecurringBookingTemplate.MapToDto` + `Address.ToSingleLine`; dedupe recurring projection/validators (B9/B3) | spaghetti | M | backend | No |
| **AUD-10** | Move cleaner weekly-order-limit magic numbers into `BookingPolicy` | hardcoded | S | backend | No |
| **AUD-11** | Convert partner `OrdersListUiState` to sealed `UiState` + shared `ActionState` (E1/E2) | spaghetti | M | android | No |
| **AUD-12** | Fix off-by-one `OrderStatus` class/icon maps in partner web order-detail helpers | bug | S | frontend-partner | No |

### Minor
| ID | Title | Type | Size | Layers |
|---|---|---|---|---|
| **AUD-13** | Standardize order/note/issue parity & remove dead endpoints across web/mobile | gap | M | partner-api, customer-api, frontend-partner |
| **AUD-14** | Add `OnTheWay` case to admin order status badge/icon helpers | bug | S | frontend-admin |
| **AUD-15** | Type order-status email param as `OrderStatus` enum + introduce `CancelledBy` enum (folds into AUD-01) | hardcoded | M | backend |
| **AUD-16** | Type recurring-booking command enums instead of raw ints (kills frontend `as unknown as number`) | spaghetti | M | backend, frontend |
| **AUD-17** | Remove geocoding **write** side-effect from `GetPagedOrders` query (restore CQRS read-only); extract per-row pay/PII mapper | spaghetti | M | backend |
| **AUD-18** | Fix partner `OrdersFacade` cleanup/error handling (takeUntil+catchError) & remove `setTimeout(100)` sequencing | spaghetti | M | frontend |
| **AUD-19** | Move customer recurring/wizard facade calls to the C3 pipe | spaghetti | M | frontend |
| **AUD-20** | Refactor `HandlePaymentNotification` webhook (297 lines): extract service, parse event once, `BusinessErrorMessage` codes, **add tests** | spaghetti | M | backend |
| **AUD-21** | Align `GetFiscalFailures` to `IQueryHandler` + decide paging (remove hidden 200 cap) | spaghetti | M | backend |
| **AUD-22** | Add `Response` records to fiscal commands (B1) | spaghetti | S | backend |
| **AUD-23** | Fix mobile `collectAsState` → lifecycle-aware; make hardcoded CZ/CZK config-driven | hardcoded | M | mobile |
| **AUD-24** | Correct stale "no recurring UI" comment in `MaterializeRecurringBookings` | spaghetti | S | backend |

### Aggregate
| ID | Title | Type | Layers |
|---|---|---|---|
| **AUD-25** | Burn down the 187 machine-detected consistency violations (T-0001…T-0016 epic) | spaghetti | backend, frontend, android |

---

## 3. Themes (systemic patterns)

1. **Dead lifecycle states & unreachable domain methods (dominant theme).** The team builds the domain
   model + UI chrome (enum states, `AddBonus`/`UpdateAmounts`/`MarkAsPaid`/`Dispute`/`Reject`/`Reopen`,
   filter chips, badge styles, DTO fields) but never ships the command + endpoint + UI to drive them.
   Symptom of building bottom-up and stopping before the last mile.
2. **Admin/back-office is a second-class persona.** The Admin host is read-only where it matters
   (orders, extras), and several mutation endpoints landed on the *partner* host instead.
3. **Web ↔ mobile ↔ API parity drift.** Capabilities exist on one client/host but not another
   (cancel: mobile-yes/web-no; "On my way"; note/issue edit-delete; a dead customer ReportIssue
   endpoint). The generated NSwag client usually already carries the method — the gap is UI wiring.
4. **God-units on the most critical paths.** Booking and money concentrate complexity
   (`CreateOrder.Handler`, `order-wizard.facade.ts` 1048 lines, `HandlePaymentNotification` 297 lines
   untested, `GetPagedOrders` mixing 5 concerns incl. a write). Highest regression risk where it hurts.
5. **Conventions violated in repeatable, mechanical ways** beyond the 187 catalogued (B4/S3, B9, C3,
   E1/E2).
6. **Magic strings/numbers where a named home already exists** (stringly-typed status into
   `EmailService`, the `"customer"` cancel literal, weekly-limit tiers outside `BookingPolicy`, raw-int
   recurring enums, hardcoded CZ/CZK on mobile).

> Source JSON truncated mid-finding at PAY-6; if findings followed it they are not reflected here.
