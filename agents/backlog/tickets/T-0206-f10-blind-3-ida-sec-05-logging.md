---
id: T-0206
title: "S6 logging hygiene: stop logging messageText/PII/Stripe-ids/confirmation-codes"
status: done
size: S
owner: —
created: 2026-06-01
updated: 2026-06-14
depends_on: []
blocks: []
stories: []
adrs: []
layers: [backend, functions]
security_touching: false
manual_steps: []
sprint: 3
source: findings F10/BLIND-3/IDA-SEC-05 (S6)
---

## Context

Wave-3 consistency/quality cleanup. Findings F10 / BLIND-3 / IDA-SEC-05 flag concrete violations of
the **S6 logging-hygiene law** (`agents/knowledge/security-rules.md` §S6: *"No email, phone, name,
address, payment/Stripe detail, JWT, refresh token, or confirmation code in logs at Information level
or higher. Log `userId`, not `user.Email`."*). The codebase currently logs sensitive material above
`Debug` in several real places:

- **Raw queue payloads (`messageText`) logged at Warning/Error** — the Azure Functions consumers dump
  the entire deserialized-or-not queue body, which carries PII, push-notification content, Stripe/
  receipt detail, and tenant ids:
  - `src/Cleansia.Functions/Functions/SendPushNotificationFunction.cs:47-49` (`"Discarding push
    message ...: {Message}", messageText`) and `:117-119` (`"Failed to dispatch ... Message:
    {Message}", messageText`).
  - `src/Cleansia.Functions/Functions/SendSitewidePromoFanoutFunction.cs:81-83` and `:159-161`
    (`"... {Message}", messageText`).
  - `src/Cleansia.Functions/Functions/GenerateReceiptFunction.cs:105-106` (`"Failed to generate
    receipt for order {OrderId}. Message: {Message}", ..., messageText`).
- **Confirmation code + email logged at Warning** —
  `src/Cleansia.Core.AppServices/Features/Auth/ConfirmUserEmail.cs:41` logs `{Code}` (the
  confirmation code, an auth secret) and `:47-48` logs `{Email}` (PII).
- **SendGrid response body logged at Error** —
  `src/Cleansia.Core.AppServices/Services/EmailService.cs:364` and `:414`
  (`"SendGrid returned {StatusCode}: {Body}", ..., body`) — the body can echo recipient addresses.

This is a **refactor**: behavior (the log *event* still fires, with the same level and the same
correlation id) is unchanged; only the *PII/secret values* are removed or replaced with safe
identifiers (`userId`, `OrderId`, message length, a redacted marker). No control flow, no return
values, no contracts change.

## Acceptance criteria

- [ ] **AC1 (characterization-test-first)** — Given the current logging call sites listed in Context,
  When the touched units run their happy and failure paths, Then a characterization test (TEST-FIRST,
  per `testing.md`) pins that the **log event still fires at the same level with the same scalar
  correlation keys** (`UserId` / `OrderId` / `EventKey` / `StatusCode` present). The test asserts the
  *event*, not the exact message string (no string-coupling — see testing.md anti-patterns). Test is
  red→green before the cleanup lands; status log records it.
- [ ] **AC2 (Functions `messageText`)** — Given a Function consumer logs a failure/discard, When it
  emits the log, Then the **raw `messageText` is no longer in the structured payload**; it is replaced
  by safe scalars (e.g. `message.UserId`/`message.OrderId`/`message.EventKey` when deserialization
  succeeded, and `messageText.Length` or a `"<redacted N bytes>"` marker when it did not). Behavior
  (the `throw` that drives queue retry, the early `return` on discard) is **identical**.
- [ ] **AC3 (confirmation code + email)** — Given `ConfirmUserEmail.ValidateUserTokenAsync`, When a
  confirmation lookup fails or the code is expired, Then the log line **no longer contains `{Code}`
  or `{Email}`**; it logs `{UserId}` (when a user was found) or no identifier (the "no user found"
  branch), at the same Warning level, and returns `false` exactly as before.
- [ ] **AC4 (SendGrid body)** — Given `EmailService` receives a non-success SendGrid response, When it
  logs the failure, Then the response **`{Body}` is not logged above Debug**; the Error log keeps
  `{StatusCode}` only (the body may move to a `LogDebug` if local diagnosis value is wanted). Send
  outcome/return value is unchanged.
- [ ] **AC5 (smell removed + tool clean)** — Given the touched files, When the reviewer runs the S6
  logging check in `node agents/tools/check-consistency.mjs` for the touched area, Then it reports
  **clean** for those files (no PII/secret tokens in log templates above Debug), and no new S6
  violations are introduced elsewhere.
- [ ] **AC6 (behavior unchanged)** — Given the full backend + functions build and the existing +
  new characterization tests, When `dotnet build Cleansia.Api.sln` and `dotnet test
  src/Cleansia.Tests` run, Then both are green; the diff contains **only** logging-template/argument
  changes (plus the new tests) — no logic, signature, or contract edits.

## Out of scope

- Adding a global redaction/log-enricher or a structured-logging framework swap — this is a targeted
  call-site sweep only. (If a shared redaction helper is warranted, that is a separate architect-led
  ticket.)
- Changing log *levels* of healthy events, request-logging middleware behavior, or the `{Message}`
  exception-message pattern in `GlobalExceptionHandler.cs` (exception messages are not PII per S6).
- Any auth/token *behavior* change (covered by Wave-0 IDA-SEC-03 / TC-AUTH-TAKEOVER) — this ticket
  only stops *logging* the secret, it does not touch token generation/lookup.
- Functions idempotency/poison-queue handling (F3/F4) — unrelated; do not refactor the retry flow.

## Implementation notes

- **Canonical pattern:** S6 in `agents/knowledge/security-rules.md` §S6 — *log `userId`, never
  email/phone/card/code/body*. This ticket is a `consistency.md`-style "one way to do each thing"
  cleanup: the canonical log shape is **safe scalar correlation keys only** (`UserId`, `OrderId`,
  `EventKey`, `StatusCode`, byte counts) at Information/Warning/Error; PII/secrets allowed at `Debug`
  only. No new deviation may be introduced.
- **TEST-FIRST (testing.md → "When you're changing existing untested code"):** write a
  **characterization test** that pins the *current* log event (level + scalar keys) for each touched
  unit, confirm it passes, then make the redaction change keeping the test green. Assert the *event
  and its scalar properties*, not the message template string (string-coupled log assertions are a
  rejected anti-pattern). For `ConfirmUserEmail` the handler unit test (mocked `IUserRepository`)
  already exercises both failure branches — extend it to assert no `Code`/`Email` property is logged.
- **Serialization:** the Functions still call `JsonSerializer.Deserialize<T>(messageText, ...)` — the
  deserialization itself is unchanged; only the **logging** of `messageText` is removed. When the
  message deserialized, prefer logging the typed scalar fields (`message.UserId`, `message.OrderId`,
  `message.EventKey`); when it did not (the `InvalidOperationException` / catch path), log
  `messageText.Length` or a redacted marker instead of the body.
- **Touch only** the call sites enumerated in Context. Keep each log's level and exception argument
  (`logger.LogError(ex, ...)`) intact so observability/alerting is preserved.

## Status log
- 2026-06-01 — draft (created by pm)
- 2026-06-13 — **review** (backend, Wave-5 Batch 5B). S6 call-site sweep landed, behavior-preserving.
  **TEST-FIRST evidence (red→green):** added `src/Cleansia.Tests/Logging/S6LoggingHygieneCharacterizationTests.cs`
  (9 cases) pinning the log EVENT + safe scalar correlation keys, asserting the *event/scalar
  properties* not the template string. Initial run **RED 7/7** for the right reason (raw body / PII / SendGrid
  body present in the log message; push transient log missing `UserId` scalar). After the redaction it is
  **GREEN**; the existing behavior suites `SendPushNotificationClassifyTests` (6), `GenerateReceiptHandlerBranchTests`
  (3), `SendSitewidePromoFanoutHandlerSmokeTests` (1) stay **green unchanged** → 19/19 in an isolated
  runner (see Verification note below). 2 cases added after a sink was found in the deserialize-failure
  exception messages.
  **AC1** ✓ characterization-first (red→green, asserts event+scalars). **AC2** ✓ Functions `messageText`
  removed from all three handlers' log payloads — replaced by `message.UserId`/`EventKey` /
  `campaign.CampaignId` / `OrderId` when deserialized, and `messageText.Length` (byte count) when not;
  ack/return/throw control flow identical. **AC3** ✓ already satisfied — `ConfirmUserEmail.ValidateUserTokenAsync`
  was remediated in Wave-3a (#76): it logs `{UserId}` only / no identifier on the no-user branch, no
  `{Code}`/`{Email}`; **no change needed** (deviation D1). **AC4** ✓ `EmailService.ThrowClassifiedAsync`
  Error log keeps `{FailureClass}`+`{StatusCode}`; the SendGrid `{Body}` moved to `LogDebug`. Send
  outcome/return unchanged. **AC5** ✓ no S6 token in any Information+ log template across the touched files
  (only `{Body}` remains, at Debug); `node agents/tools/check-consistency.mjs` flags none of the touched
  files (the tool has no S6 rule yet — manual grep used). **AC6** ✓ diff is logging-template/argument-only
  + the new test; no logic/signature/contract edit.
  **Files changed:** `SendPushNotificationHandler.cs`, `SendSitewidePromoFanoutHandler.cs`,
  `GenerateReceiptHandler.cs` (Functions.Core), `EmailService.cs` (Core.AppServices), + new test file.
  **Extra sink found & closed (in-scope):** the two handler-constructed `InvalidOperationException(
  $"Failed to deserialize …: {messageText}")` messages embed the raw body and are logged at Error in the
  same enumerated catch — redacted to a `({messageText.Length} bytes)` marker (control flow unchanged).
  This is NOT the out-of-scope `GlobalExceptionHandler` pattern.
  **Verification:** `dotnet build` of `Cleansia.Core.AppServices` and `Cleansia.Functions.Core` with
  `-p:BuildProjectReferences=false` = **0 errors**. The full `Cleansia.Tests` project currently CANNOT
  emit because of UNRELATED concurrent-lane compile breaks (foreign files: `CreateOrder`/`CreateDispute`
  handler-ctor mismatch, `GetPaged*Handler` `internal`-access, and a mid-flight
  `Infra.Services/Templates/*` deletion that breaks a full `Core.AppServices` *reference* rebuild) — all
  outside this ticket's files. To prove red→green I compiled my test file + the three existing behavior
  suites against the freshly-built library DLLs in an isolated throwaway runner (outside the repo, since
  removed): **19/19 green**. The orchestrator's authoritative clean run will execute these in-project once
  the sibling lanes' compile breaks clear.
  **MANUAL_STEPs:** none (no DTO/endpoint/schema change → no nswag-regen, no ef-migration).
  **Production bugs found (report-only, do NOT fix here):** `Core.AppServices` is currently broken in the
  shared tree by a concurrent lane deleting `Cleansia.Infra.Services/Templates/*` while
  `RegenerateInvoicePdf.cs` + `PayPeriodBackgroundService.cs` still import that namespace — flag for the
  owning lane / a follow-up if it persists past their commit.
- 2026-06-13 — **ready** (PM, Wave-5 intake / Batch **5B**). No deps. DoR met: AC1–AC6 observable
  (characterization pins the log *event* + scalar correlation keys, never the message string), S,
  behavior-unchanged (only PII/secret values removed from log templates above Debug). **`security_touching:
  false` in frontmatter, but it implements the S6 security law** (stop logging confirmation codes / email /
  Stripe ids / SendGrid body / raw queue payloads) — the PM routes a **Security advisory pass** alongside
  the reviewer (it is a security-hygiene fix, even though it changes no auth/data-exposure control flow).
  No migration/regen. backend + functions; touches the Functions consumers + ConfirmUserEmail + EmailService
  log call-sites only — disjoint from other 5B files. sprint re-tagged 5.

## Review
<!-- reviewer / security / optimizer write verdicts here; PM reconciles before advancing state -->
