# Fiscal Compliance

Cleansia integrates with country-specific fiscal reporting systems (CZ EET 2.0, SK eKasa, DE TSE, AT RKSV, ES VeriFactu, IT RT, HU, PL KSeF). Different countries impose very different legal requirements on when a fiscal signature must be attached to a receipt, so the platform uses a per-country **enforcement mode** that routes each receipt through the correct flow without forcing any customer to wait on a fiscal authority.

The core guarantee: **no customer loses an order to a fiscal outage.** Order completion (Stripe payment, order persistence, confirmation) is fully decoupled from fiscal registration in every mode and every failure scenario.

## Enforcement Modes

`FiscalEnforcementMode` is a per-country setting stored on `CountryConfiguration.FiscalEnforcementMode`. It controls how the fiscal flow branches inside `ReceiptService.HandleFiscalAsync`.

| Mode | Countries | Behaviour |
|---|---|---|
| `None` | CZ today | No fiscal system. Receipt generated and emailed immediately. |
| `AsyncBackground` | CZ EET 2.0, SK eKasa, IT RT, HU, PL KSeF | Fiscal registration is required but lenient. The receipt PDF and email may be sent before fiscal registration succeeds. Failures retry in the background. |
| `BlockingOnline` | DE TSE, AT RKSV, ES VeriFactu | Fiscal signature is legally required before the receipt is delivered. The email is held until the fiscal authority signs; the background job releases it once the signature is obtained. |
| `BlockingWithOfflineCache` | (future) | Strict like `BlockingOnline` but supports an offline POS cache that signs locally and syncs later. Reserved for future hardware/TSE integrations. |

Adding a new strict country only requires: (1) an `IFiscalService` implementation in `Cleansia.Infra.Fiscal`, (2) registering it in `FiscalServiceCollectionExtensions`, and (3) setting the country's `FiscalEnforcementMode`. Zero changes to `ReceiptService` or the retry pipeline.

## Receipt Flow by Mode

### None — no fiscal system

```
Stripe webhook → Order saved → Receipt generated → Email sent
```

No fiscal call, no delay. A receipt whose first upload failed is still owed its file: the retry job renders it and sends the e-mail ([below](#a-receipt-s-pdf-is-repaired)).

### AsyncBackground — lenient countries

```
Stripe webhook → Order saved → Receipt generated
                                     ↓
                        Try fiscal register (bounded, resilient)
                                     ↓
                             Email sent regardless
                                     ↓
              On failure → FiscalNextRetryAt scheduled → retry job
```

The customer receives their receipt email on the normal path. If the fiscal authority is unreachable, the receipt is marked `FiscalRegistrationFailed` with a classified `FiscalErrorKind` and picked up by the retry job later. The retry job renders the PDF again with the fiscal code as a new file beside the first (`receipt_….pdf`, then `receipt_…-2.pdf`), points the receipt at it, and keeps the first as the customer received it → [ADR-0083](/decisions/adr-0083).

### BlockingOnline — strict countries (DE/AT/ES)

```
Stripe webhook → Order saved → Receipt generated
                                     ↓
                        Try fiscal register (bounded, resilient)
                                     ↓
                           ┌─────────┴─────────┐
                      success               failure
                           ↓                   ↓
                     Email sent        Email HELD
                                        (customer sees
                                         order confirmed;
                                         receipt delivered
                                         once signed)
                                             ↓
                                       retry job → on
                                       success, release
                                       held email
```

Legal nuance: in DE TSE and AT RKSV, "delivery" of the receipt is when it reaches the customer. For an online service, that means the email. Holding the email until the signature is obtained is compliant. The customer's order and payment are never blocked — only the receipt email is delayed (typically seconds; worst case minutes).

## Register Idempotency (ADR-0004 go-live gate 2)

The claim-before-register ordering leaves a rare *registered-but-stamp-not-persisted* residual: the authority has the receipt but the local `FiscalCode` was not yet committed, and recovery re-calls `RegisterReceiptAsync` for the same receipt. For a `BlockingOnline` regime a double registration is a compliance incident, so the fiscal contract makes the dedup token explicit and gates blocking regimes on idempotency.

- **Explicit token.** `FiscalReceiptRequest.IdempotencyKey` carries the natural token — the `ReceiptNumber`. The initial register (`ReceiptService.RealizeFiscalAndPdfAsync`) and the recovery re-register (`ReceiptService.CompleteReceiptAsync`) build the request through the same `FiscalReceiptRequest.Create` factory, so both present the same key for the same receipt.
- **Provider self-declaration.** `IFiscalService.RegisterIsIdempotent` states whether a repeat call on the same key returns the prior signature/code without burning a new authority entry.
- **The gate.** `FiscalGoLiveGate.EnsureRegisterIdempotent` runs in the register path and throws `FiscalGoLiveGateException` if a provider whose `RegisterIsIdempotent` is `false` is used under `BlockingOnline` / `BlockingWithOfflineCache`. `AsyncBackground` tolerates a rare extra registration and is not gated.

Per-provider status:

| Provider | Enforcement | Register-idempotent on `ReceiptNumber`? | Basis |
|---|---|---|---|
| `NoOpFiscalService` | None | Yes (trivial) | Contacts no authority — re-running is a no-op. |
| `CzechEet2FiscalService` (CZ EET 2.0) | AsyncBackground | Yes | EET 2.0 re-submission of the same receipt returns the original FIK; the implementation must dedup on the receipt number. |
| DE TSE | BlockingOnline | Required before go-live | TSE is number-first/then-sign; a repeat sign of the same transaction must return the prior signature. The provider implementation, once added, must declare `RegisterIsIdempotent = true` or the gate blocks production use. |
| AT RKSV | BlockingOnline | Required before go-live | Per-issuer continuous numbering; re-signing the same receipt number must not mint a new entry. Gated until the implementation declares idempotency. |
| ES VeriFactu | BlockingOnline | Required before go-live | Per-issuer chained records; a resend with the same record id is deduped. Gated until the implementation declares idempotency. |

DE/AT/ES have no implementation yet (only `NoOpFiscalService` and the CZ stub ship today). The gate guarantees none of them can run under a blocking mode until its `IFiscalService` declares `RegisterIsIdempotent = true`, which closes ADR-0004 go-live gate item (2) in code rather than relying on a checklist.

## Error Classification

`FiscalErrorKind` drives the retry decision. Transient errors retry automatically; permanent/configuration errors alert an admin and stop.

| Kind | Meaning | Retry? |
|---|---|---|
| `Transient` | Network hiccup, 5xx, timeout, rate limit | Yes — full backoff schedule |
| `Permanent` | Invalid data, business-rule violation (e.g., VAT mismatch) | No — admin must fix order/data |
| `Configuration` | Missing credentials, wrong endpoint, expired certificate | No — ops must fix config |
| `Unknown` | Unclassified exception | Yes — limited retries then escalate |

## Retry Schedule

`OrderReceipt.ComputeNextRetry(attemptNumber)` uses exponential backoff:

| Attempt | Delay |
|---|---|
| 0 → 1 | 1 minute |
| 1 → 2 | 2 minutes |
| 2 → 3 | 5 minutes |
| 3 → 4 | 15 minutes |
| 4 → 5 | 1 hour |
| 5 → 6 | 6 hours |
| 6 → 10 | 24 hours |

After `MaxFiscalRetries = 10` attempts, `FiscalNextRetryAt` is cleared and the receipt stops retrying until an admin forces a manual retry or acknowledges the failure.

## A receipt's PDF is repaired {#a-receipt-s-pdf-is-repaired}

`FiscalNextRetryAt` marks a receipt that still owes its fiscal registration **or a stored PDF carrying its code**, in every mode, `None` included:

- `ReserveAsync` sets it on every claim, 15 minutes ahead (`ScheduleRetryAfterClaim`), so the job never races an issue still in progress. The issue clears it once the PDF is stored and no registration is owed (`MarkDocumentComplete`).
- `GetDueForRetryAsync` selects on the due instant alone, so a receipt stamped before its render failed is picked again.
- `CompleteReceiptAsync` registers only when the mode requires it and the receipt has no code, so a stamped code is never registered twice, and renders a new numbered file when the current file is missing (`IsPdfStoredAsync`) or the receipt carries a code. A register that throws counts as a refusal, so the render of a missing file still runs. A failure records an attempt on the schedule above.
- `FiscalRetryService` releases the held e-mail of a blocking market once the coded file is stored, and in any other market sends the e-mail only when its own pass stored the receipt's first file.
- `DownloadOrderReceipt` answers `receipt.not_found` until the file is stored.

The flow, its reasons and its one remaining loss → [Payment and fiscal](/flows/payment-and-fiscal#a-receipt-s-pdf-is-repaired).

## Components

### Domain (`Cleansia.Core.Domain`)

- **`OrderReceipt`** — Aggregate that owns the fiscal state. Retry-tracking fields: `FiscalErrorKind`, `FiscalRetryCount`, `FiscalLastRetryAt`, `FiscalNextRetryAt`, `FiscalAcknowledged`, `FiscalAcknowledgedAt`. Domain methods: `SetFiscalData`, `MarkFiscalRegistrationFailed`, `MarkFiscalRetryAttempted`, `AcknowledgeFiscalFailure`, `ScheduleImmediateFiscalRetry`, `ScheduleRetryAfterClaim`, `MarkDocumentComplete`.
- **`CountryConfiguration.FiscalEnforcementMode`** — Per-country enforcement policy.

### Abstractions (`Cleansia.Core.Fiscal.Abstractions`)

- **`IFiscalService`** — Contract every country implementation satisfies: `Task<FiscalResult> RegisterReceiptAsync(FiscalReceiptRequest, CancellationToken)` plus the `RegisterIsIdempotent` capability flag (see Register Idempotency above).
- **`IFiscalServiceResolver`** — Routes a country to the right `IFiscalService` by the ISO 3166-1 **alpha-2** code the service declares (`CZ`). `Country.IsoCode` is stored alpha-3 (`CZE`), so `ReceiptService` translates it through `CountryIsoCode.ToAlpha2` before every resolve; a code with no alpha-2 is treated as an unresolved country. → [Payment and fiscal — the regime](/flows/payment-and-fiscal#no-guessed-unit-no-guessed-regime)
- **`FiscalGoLiveGate`** — Enforces that a provider may only run under a blocking enforcement mode if its register is idempotent on the receipt number.
- **`FiscalResult`** — Carries `IsRegistered`, `FiscalCode`, `RegisteredAt`, `ErrorKind`, `ErrorCode`, `ErrorMessage`. Factories: `Success`, `NotRequired`, `TransientError`, `PermanentError`, `ConfigurationError`, `UnknownError`.
- **`FiscalEnforcementMode`**, **`FiscalErrorKind`** — Enums described above.

### Implementations (`Cleansia.Infra.Fiscal`)

- **`NoOpFiscalService`** — Returns `FiscalResult.NotRequired()`. Used as the fallback when no country-specific service is registered.
- **`CzechEet2FiscalService`** — CZ EET 2.0 stub. Currently returns `ConfigurationError("NOT_IMPLEMENTED")` because EET 2.0 is not yet active. Wired through `AddStandardResilienceHandler()` (Polly circuit breaker) on its `HttpClient`.
- **`FiscalServiceCollectionExtensions.AddFiscalServices`** — DI registration entry point. New countries are added here.

### Orchestration (`Cleansia.Core.AppServices`)

- **`ReceiptService.HandleFiscalAsync`** — Reads the enforcement mode from `CountryConfiguration`, branches on it, and catches every exception so the customer flow is never aborted. The request is built from the order's own currency code and country ISO code and **never from a default**: an order with no resolved currency (`FiscalCurrencyCodeOf`) or no resolved country (`FiscalCountryCodeOf`) throws before the authority is called, lands as a recorded failed attempt, and is retried like any other failure. The receipt-number counter resolves an empty provider key to the `DEFAULT` issuer scope, not the Czech provider's. → [Payment and fiscal](/flows/payment-and-fiscal#no-guessed-unit-no-guessed-regime)
- **`ReceiptService.CompleteReceiptAsync`** — Completes what a due receipt owes: registers it where the mode requires and it has no code, renders the PDF as a new numbered file when the current file is missing or lacks the stamped code, and moves `OrderReceipt.BlobName` to it; no render overwrites another (ADR-0083).
- **`FiscalRetryService.ProcessDueRetriesAsync`** — Batch processes receipts where `FiscalNextRetryAt <= UtcNow`. Releases the held receipt email of a blocking mode once the coded file is stored, and sends the email of any other mode when its pass stored the receipt's first file.

### Background worker (`Cleansia.Functions`)

- **`RetryFailedFiscalRegistrationsFunction`** — Timer-triggered Azure Function running every 5 minutes (`0 */5 * * * *`). Invokes `FiscalRetryService.ProcessDueRetriesAsync`.
- **`GenerateReceiptFunction`** — Queue-triggered function that generates the initial receipt. Contains the email-hold guard for `BlockingOnline` countries when the initial fiscal attempt fails.

### Database indices

- `IX_OrderReceipts_FiscalNextRetryAt` — Filtered index `WHERE "FiscalNextRetryAt" IS NOT NULL`. Keeps the retry-job query cheap even as the `OrderReceipts` table grows.

## Resilience Layers

Multiple defences prevent a fiscal outage from cascading:

1. **Per-call try/catch in `ReceiptService.HandleFiscalAsync`** — Any exception is caught, classified as `Unknown`, and stored on the receipt. The customer flow continues.
2. **Polly circuit breaker via `AddStandardResilienceHandler`** — Fiscal HttpClients trip a breaker under sustained failure, stopping hammering of the downstream authority during an outage.
3. **Error classification** — `Permanent` and `Configuration` failures are not retried, so a bad credential doesn't burn through the retry schedule.
4. **Exponential backoff with a cap** — The retry schedule caps at 24 hour intervals, and `MaxFiscalRetries = 10` ensures no infinite loop.
5. **Filtered DB index** — The retry job's query only scans receipts with `FiscalNextRetryAt IS NOT NULL`, so retry scanning is O(receipts still owed), not O(receipts).
6. **Admin escalation path** — See [Admin → Fiscal Failures](/admin-app/fiscal-failures). Ops can force a retry or acknowledge a permanent failure.

## Customer Safety Invariants

- Order persistence never depends on fiscal success.
- Stripe webhook acknowledgment never waits on the fiscal authority.
- Receipt generation never throws because of fiscal failure — it marks the receipt and continues.
- A receipt is never registered in a guessed currency or under a guessed regime — a missing currency or country is a recorded failure, not a CZK declaration to the Czech authority.
- Blocking countries hold the receipt *email*, never the order or payment.
- A committed receipt is owed a stored PDF until one is stored; the retry job renders it, whatever the mode.
- An admin can always force a retry or acknowledge a failure, breaking any stuck state.

## See Also

- [Fiscal Failures admin page](/admin-app/fiscal-failures) — How ops manages the action queue.
- [Backend architecture](/architecture/backend) — CQRS + MediatR layering this system sits within.
