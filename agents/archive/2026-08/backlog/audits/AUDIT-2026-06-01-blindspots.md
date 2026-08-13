# Audit — Completeness Critic & Blind Spots (2026-06-01)

What the 8-domains × 4-dimensions structure systematically **missed**. This is as important as the
findings themselves — it defines the follow-up passes. Includes one **suspected security finding** the
domain split walked past.

## A. Unowned subsystems & integrations (zero coverage)

- **Azure Functions — 18 of them, completely unaudited.** `src/Cleansia.Functions/Functions/` holds
  the *real* execution surface for almost every "dead lifecycle" the audit flagged
  (`GenerateInvoiceFunction`, `GenerateReceiptFunction`, `RetryFailedFiscalRegistrationsFunction`,
  `PayPeriodTimerFunction`, `CalculateOrderPayFunction`, `AutoCancelStaleRecurringOrdersFunction`,
  `CleanupStalePendingOrdersFunction`, `SendPushNotificationFunction`, the membership/promo notifiers,
  `DataRetentionTimerFunction`, `RefreshTokenCleanupTimerFunction`, …). **The audit declared
  payroll/fiscal "dead" by looking only at AppServices handlers — but the driver may be a timer/queue
  function, not an Admin endpoint. Those "unreachable" verdicts (AUD-02/04) are UNVERIFIED until the
  Functions trigger graph is mapped.** Conversely, the Functions have their own unaudited failure modes
  (poison messages, idempotency, retry storms, dead-letter).

- **🔴 SUSPECTED SECURITY FINDING — a second Stripe webhook handler may not verify signatures.**
  `src/Cleansia.Core.AppServices/Services/StripeSubscriptionWebhookHandler.cs` exists alongside
  `HandlePaymentNotification.cs`. A grep for `ConstructEvent`/signature verification returns **nothing**
  in the subscription handler, while `HandlePaymentNotification` calls `EventUtility.ConstructEvent` 3×.
  If confirmed, an attacker could forge subscription/membership webhook events. **Must be read
  end-to-end and confirmed first thing** in the re-run (this would be the audit's first true S-finding).

- **Push/email/PDF/geocoding clients unaudited as components** — `Infra.Clients/Fcm/FcmPushDispatcher`,
  `SendGrid/*`, `Infra.Services/Geocoding/MapboxGeocodingService`, `Pdf/QuestPdfService`,
  `Templates/HandlebarsTemplateEngine`. Unexamined: FCM token lifecycle/invalidation, SendGrid failure
  handling, Mapbox rate-limit/error handling (ties to AUD-17), and **Handlebars template injection**
  (user data into templates = an S-law concern).

- **AppHost / Aspire wiring unaudited** — `src/Cleansia.AppHost/Program.cs` is the single source of
  truth for service wiring, connection strings, secret injection, CORS/host exposure across all APIs +
  Functions + Postgres. Intersects directly with AUD-04's "endpoints on the wrong host".

## B. Under-covered dimensions

- **i18n completeness across 5 locales — not machine-checked.** All 15 files exist, but key sets were
  never diffed across locales. Every "add a feature" ticket adds English keys with no guarantee the
  other 4 get them. No finding measures the current drift.
- **Error-contract parity (`BusinessErrorMessage` ↔ frontend `errors.*`) — not checked.** An explicit
  project rule; nobody cross-referenced the constants against the i18n keys. Unmapped keys render raw
  codes to users.
- **The consistency checker is structurally narrow.** `check-consistency.mjs` line-scans for ~16
  structural rules — it cannot see i18n parity, error-contract parity, migration/seed integrity, or
  NSwag drift. The 187 is a **floor, not a ceiling**.
- **Test coverage vs. the must-cover list — effectively unaudited.** `Cleansia.Tests` covers only Auth
  validators + `BookingPolicyTests` + 3 order validators; `IntegrationTests` covers Auth, GDPR delete,
  4 catalog overviews. **Zero tests** on the highest-risk paths the audit itself ranked top-3:
  `CreateOrder`, both Stripe webhooks, pay calculation, invoice generation, the 18 Functions. The
  systemic absence is never stated as its own finding.
- **Migration & seed integrity — not covered.** No domain validated EF migrations against entity
  configs or the `sql-scripts/` seeds — a recurring real defect class per git history (`IsVatPayer`,
  "missing insert for script").
- **Accessibility — entirely absent** across the 3 Angular apps.
- **NgRx effects** (11 files incl. `dispute.effects.ts`, `order.effects.ts`) weren't examined for
  error-action handling or cancellation — frontend findings focused on facades.

## C. The methodological caveat (carry forward)
**23 of 32 investigator slices failed to return structured output**, and the JSON truncated at PAY-6.
So **loyalty-growth, disputes-addresses, identity-auth, catalog-config, and employees** contribute few
or no AUD tickets — this reflects the **tooling failure, not cleanliness**. Re-run consolidation on the
complete findings set before trusting per-domain coverage.

## D. Recommended follow-up audit passes (ordered by risk)

1. **Integration & webhook security pass** — read both Stripe handlers end-to-end; confirm signature
   verification on `StripeSubscriptionWebhookHandler` (suspected missing), idempotency, replay
   protection; extend to SendGrid/FCM/Mapbox/Handlebars. *(Likely the first true S-finding.)*
2. **Re-run the 5 under-covered domains** (loyalty-growth, disputes-addresses, identity-auth,
   catalog-config, employees) — they were lost to the structured-output failure.
3. **Azure Functions trigger-graph pass** — map every trigger to the handlers called "dead";
   re-validate AUD-02/04; audit idempotency / poison / dead-letter / retry.
4. **Contract-parity pass (automatable)** — extend `check-consistency.mjs` (or a sibling) to diff
   (a) i18n key sets across 5 locales × 3 apps, (b) `BusinessErrorMessage` ↔ `errors.*`, (c) NSwag drift.
5. **Test-coverage gap pass** — enumerate zero-test write paths (orders, payments, payroll, fiscal, the
   18 Functions) into a prioritized must-cover backlog.
6. **AppHost/Aspire + secrets/CORS/host-exposure pass** — informs AUD-04.
7. **Migration/seed integrity pass** — diff EF migrations against configs; validate `sql-scripts/` seeds.
8. **Accessibility pass** — the `cleansia-*` library + the order wizard across all 3 apps.
