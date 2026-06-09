---
id: T-0146
title: "Move registration/password-reset email off the critical path (async/queue)"
status: done
size: M
owner: backend
created: 2026-06-01
updated: 2026-06-07
depends_on: [T-0141, T-0118]
blocks: []
stories: []
adrs: [0002, 0005]
layers: [backend, functions]
security_touching: true
manual_steps: []
sprint: 1
source: finding BLIND-1 (critical)
---

## Context

Audit finding **BLIND-1** (critical, integrations/resilience):
`agents/backlog/audits/AUDIT-2026-06-01-findings.md:59-60` — *"email sent inline & synchronously on the
critical path; a SendGrid outage hard-fails registration and password-reset."* Tracked in
`agents/backlog/INDEX.md:45` (BLIND-1, crit, ADR-INTEGRATION).

The account-creation and password-reset command handlers `await IEmailService.Send…Async(...)`
**inside the handler**, on the synchronous request path:

- `Register.Handler` (`Register.cs:89`) — `await emailService.SendEmailConfirmationAsync(...)`.
- `RegisterEmployee.Handler` (`RegisterEmployee.cs:84`) — `await emailService.SendEmailConfirmationAsync(...)`.
- `ResendConfirmationEmail.Handler` (`ResendConfirmationEmail.cs:59`) — `await emailService.SendEmailConfirmationAsync(...)`.
- `RequestPasswordChange.Handler` (`RequestPasswordChange.cs:43`) — `await emailService.SendResetPasswordEmailAsync(...)`.

`EmailService` calls SendGrid behind a 3-retry Polly policy (`EmailService.cs:32-43`) that runs **on
that same awaited path**. When SendGrid is degraded/down, the handler blocks through all retries and
then surfaces the failure to the caller — so a third-party outage **hard-fails the user's
registration / reset request** (and, because the send happens before the UnitOfWork pipeline commit,
the failure shape also interacts with the dual-write hazard ADR-0002 fixes). Sending a confirmation /
reset email is a notification side effect; it must not be on the user-facing critical path.

This is a **Wave-1** finding under **ADR-INTEGRATION** (T-0141 — the async-email + IHttpClientFactory +
error-classification ADR) and lands on the **frozen dispatch seam** delivered by **F2/SEC-W1**
(T-0118): a handler **records intent** via `IPendingDispatch.Enqueue` and the email is realized
**after** the commit succeeds, never inline. ADR-0002's contract principle governs the seam (D1, lines
122-128). This is the registration/reset analog of the receipt-email move T-0118 already made for the
order path.

## Acceptance criteria

- [ ] **AC1** (the hole is closed — async send) — Given a healthy app, When a user registers
  (`Register` / `RegisterEmployee`), re-requests confirmation (`ResendConfirmationEmail`), or requests
  a password reset (`RequestPasswordChange`), Then the confirmation/reset email is **enqueued as
  post-commit intent** (an email-dispatch message via `IPendingDispatch.Enqueue` per ADR-INTEGRATION /
  ADR-0002 D1) and is **realized by a consumer after the UnitOfWork commit** — the four handlers no
  longer `await IEmailService.Send…Async(...)` on the request path (`Register.cs:89`,
  `RegisterEmployee.cs:84`, `ResendConfirmationEmail.cs:59`, `RequestPasswordChange.cs:43`).
- [ ] **AC2** (SendGrid outage no longer hard-fails the user) — Given the email transport (SendGrid)
  is failing, When a user registers or requests a password reset, Then the command still returns
  `BusinessResult.Success` and the account/code state is committed (the user can retry / the reset
  token exists); the dispatch failure is **logged and swallowed**, never converted into a 500 (ADR-0002
  D1 lines 184, 192-196). Proven by a test that mocks the transport to throw.
- [ ] **AC3** (intent gates on a committed success only) — Given a registration/reset command whose
  **validation fails** (e.g. `BusinessErrorMessage.ExistingUserWithEmail`,
  `BusinessErrorMessage.NotExistingUserWithEmail`) or whose **commit throws**, Then **no** email
  message is dispatched (the buffer is discarded — ADR-0002 D1.2, D4 ordering Dispatch → Validation →
  UoW → Handler). Proven by a test.
- [ ] **AC4** (deterministic key — safe to run twice) — Given the same logical email (same user +
  email type + code/token generation), Then the enqueued message carries a **deterministic
  `MessageKey`** (no `Guid.NewGuid()`/timestamp) so a duplicate enqueue / redelivery is recognized as
  already-done by the consumer; the consumer asserts before sending (ADR-0002 D2.1 / D2.2). The key
  formula for the email effect is defined alongside the existing frozen formulas
  (`agents/backlog/adr/0002-outbox-dispatch-contract.md:247-253`).
- [ ] **AC5** (consumer realizes the email; dual-read at deploy) — Given an enqueued email message,
  When the consumer runs, Then it sends via the existing `IEmailService` (confirmation or reset
  template per type, preserving language: `Register`/`RegisterEmployee`/`ResendConfirmationEmail` pass
  `command.Language`; `RequestPasswordChange` uses `user.PreferredLanguageCode ?? command.Language`,
  `RequestPasswordChange.cs:42`), classifies transport failure as **transient/throw** vs
  malformed/business-rejected as **ack** (ADR-0002 D3.3), and **dual-reads** any in-flight bare payload
  at the deploy boundary (ADR-0002 D2.1a).
- [ ] **AC6** (tests prove it — TEST-FIRST, same merge) — Failing tests written first, then green:
  - **handler tests**: each of the four handlers `Enqueue`s the email intent and does **not** call
    `IEmailService.Send…Async` directly (AC1); on transport failure the command still succeeds (AC2);
    on validation failure / commit-throw nothing is dispatched (AC3).
  - **key test**: same inputs emit the same `MessageKey` (AC4).
  - **consumer idempotency test** ("safe to run twice", `testing.md:94-96` #6): invoking the consumer
    **twice** with the same message sends the email **exactly once**.

## Out of scope

- **The ADR-INTEGRATION ADR itself** (T-0141) — `IHttpClientFactory` adoption for Stripe/SendGrid
  (BLIND-5), the error-classification layer (BLIND-6), and the async-email decision are authored there;
  this ticket *consumes* it.
- **The F2/SEC-W1 dispatch seam** (T-0118 — `IPendingDispatch`, `PostCommitDispatchBehavior`, pipeline
  reorder) — built there; this ticket only adds the four registration/reset producers + the email
  consumer onto the existing seam.
- **Order-receipt / push / invoice / pay queue effects** — already covered by T-0118 / F3 / F4; not
  touched here.
- **Identity hardening** — cryptographic email/reset tokens + user-scoped lookup (IDA-SEC-03, T-0106)
  and the auth rate-limiter (BSP-4) are separate tickets; this ticket changes *where the email fires*,
  not the token/code design.
- **The full transactional outbox** (F2-FULL, Wave-1) — when it lands it swaps only the
  `IPendingDispatch` backing; this ticket's producers/consumer are unchanged (ADR-0002 D5 Wave-1 note).
- No NSwag change (the queue contract is internal) and no migration here — `manual_steps: []`. (Any new
  `ProcessedMessage`/`DeadLetter` store is delivered by T-0118's bundle, not re-added here.)

## Implementation notes

- **Built TEST-FIRST per `agents/knowledge/testing.md`** — this is idempotency + a "safe to run twice"
  side effect (`testing.md:94-96` must-cover #6) and a state-conditional dispatch, exactly the class the
  catalog marks strict red→green→refactor. Write the failing handler/key/consumer tests (AC6) **first**;
  the status log must note "red: <test> failing → green", and tests land in the **same merge** as the fix.
- **Governing ADRs:** **ADR-INTEGRATION** (T-0141) decides *that* email is async and *how* the
  integration layer classifies transport errors; **ADR-0002** (`agents/backlog/adr/0002-outbox-dispatch-contract.md`)
  governs the *seam* the email rides on — D1 (handler records intent via `IPendingDispatch.Enqueue`,
  realized post-commit, dispatch failure logged-and-swallowed), D1.2 (buffer discarded on non-success),
  D2.1 (deterministic `MessageKey`), D2.2 (consumer asserts before its terminal effect), D2.1a
  (dual-read at deploy), D3.3 (transient-vs-permanent failure classification), D4 (Dispatch → Validation
  → UoW → Handler order). ADR-0002 is `accepted` and **immutable**.
- **Real edit sites:** replace the four inline `await IEmailService.Send…Async(...)` calls
  (`Register.cs:89`, `RegisterEmployee.cs:84`, `ResendConfirmationEmail.cs:59`,
  `RequestPasswordChange.cs:43`) with `pending.Enqueue(...)` of an email message wrapped in
  `QueueEnvelope<T>` with its deterministic key; add the email-dispatch consumer (in the testable
  `Cleansia.Functions.Core` library T-0118 establishes) that resolves the template by email type and
  calls the existing `IEmailService`. Preserve per-handler language selection (AC5).
- **Serialization clusters (TICKET-MAP):** this ticket edits the four **auth/user command handlers**
  (`Register`, `RegisterEmployee`, `ResendConfirmationEmail`, `RequestPasswordChange`), which are **not**
  in the `UnitOfWorkPipelineBehavior.cs` + queue-call-site cluster (`F11 → F2/SEC-W1 → F4 → F3` — those
  are the order/webhook handlers) and **not** in the `CreateOrder.cs` cluster. So it does **not**
  serialize against those clusters on file collision. It **does depend on** the seam from that cluster:
  hold until **T-0118 (F2/SEC-W1) is `done`** so `IPendingDispatch` + the reordered pipeline exist, and
  until **T-0141 (ADR-INTEGRATION) is `done`** so the async-email contract is frozen. If a later auth
  ticket (e.g. IDA-SEC-03 / T-0106) is in flight on these same four files, serialize against it.
- **Routing** (`agents/process/routing.md`): backend (four producer edits + key) → functions (email
  consumer + dual-read + failure classification). Spawn a **reviewer in parallel** with each developer
  instance, same ticket. `security_touching: true` → **Security gate mandatory** before `done` (reset
  emails carry the account-takeover token; confirm the move does not widen exposure). Then `qa` for the
  idempotency/dispatch tests.

## Status log
- 2026-06-01 — draft (created by pm)
- 2026-06-06 — ready (Batch 1B; gate **ADR-0005 / T-0141 done ✓** + dep **T-0118 done ✓**; `adrs` updated to
  `[0002,0005]` (ADR-0005 D3 governs the async-email contract; ADR-0002 the dispatch seam). Edits the four
  auth handlers (`Register/RegisterEmployee/ResendConfirmationEmail/RequestPasswordChange`) + adds an email
  consumer — **file-disjoint from T-0144/T-0145** (different handlers) → may run parallel to the integration
  chain. Routed to backend → functions; **security gate mandatory** (security_touching — reset-token path);
  reviewer + security in parallel).

- 2026-06-06 — in_progress (backend + functions). TDD red→green.
  - RED (test-first): wrote `Cleansia.Tests/Dispatch/EmailMessageKeyTests.cs` (key determinism),
    `Cleansia.Tests/Features/Auth/RegistrationEmailDispatchTests.cs` (each of the four handlers enqueues
    the email + no inline email service; language preservation), `Cleansia.Tests/Dispatch/EmailDispatchPipelineTests.cs`
    (outage→success, commit-throw / validation-fail → nothing dispatched) and
    `Cleansia.Tests/Functions/SendEmailHandlerTests.cs` (consumer twice→sends once, dual-read envelope+bare,
    template-by-type + language, malformed→ack / infra→throw). Failed to compile for the right reason —
    `MessageKeys.Email`/`HashCode`, `QueueNames.SendEmail`, `SendEmailMessage`, `IIdempotencyGuard`,
    `SendEmailHandler`, and the rebound handler ctors did not exist yet.
  - GREEN (backend): added `QueueNames.SendEmail`, `MessageKeys.Email`/`HashCode` (deterministic
    `email:{purpose}:{userId}:{codeHash}`, code-hash never the raw token), `SendEmailMessage`,
    `IIdempotencyGuard` + in-memory singleton backing (`InMemoryIdempotencyGuard`, registered in
    `AddAzureStorageQueues`). Replaced the four inline `await IEmailService.Send…Async` calls with
    `pending.Enqueue(QueueNames.SendEmail, QueueEnvelope<SendEmailMessage>, key)` via a shared
    `EmailDispatch` helper; dropped the now-unused `IEmailService` from all four handlers (so they
    cannot send inline) and made the four `Handler` classes public.
  - GREEN (functions): added `SendEmailHandler` (Functions.Core) — dual-reads envelope/bare, synthesizes
    the key, claims via `IIdempotencyGuard` before the terminal send, resolves the template by
    `EmailType`, preserves the message language, classifies malformed→ack vs infra→throw; plus the thin
    `SendEmailFunction` trigger shell, `SendEmailPoisonHandler` + `SendEmailPoisonFunction`, and DI in
    `Cleansia.Functions/Program.cs`.
  - Build: `dotnet build src/Cleansia.Api.sln -c Debug` → Build succeeded, 0 errors.
  - Tests: the 22 new cases pass; the Auth/Dispatch/Functions/Users/Behaviors areas = 182 passed / 0
    failed. The 14 unrelated failures in the suite (Memberships Stripe-failure + SavedAddresses
    soft-delete) are pre-existing concurrent WIP (untracked/modified test files outside this ticket),
    not regressions from this change.
  - No EF migration, no NSwag, no new ProcessedMessage/DeadLetter store. Not committed.
- 2026-06-07 — done (PM reconciliation: Wave-1 Batch 1B merged to master in a4f14094 / PR #73 chain; status corrected from ready/draft to done; reviewer+security gates were satisfied in the merged PR per sprint-3 closeout).

## Review
<!-- reviewer / security / optimizer write verdicts here; PM reconciles before advancing state -->
