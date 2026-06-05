---
id: T-0141
title: "ADR-INTEGRATION: IHttpClientFactory + error classification + async-email contract"
status: draft
size: M
owner: —
created: 2026-06-01
updated: 2026-06-01
depends_on: []
blocks: []
stories: []
adrs: []
layers: [architect, backend]
security_touching: false
manual_steps: []
sprint: 1
source: defense-panel ADR; theme 4
---

## Context
Wave-1 foundational ADR (TICKET-MAP `Wave 1`, line 66: "ADR-INTEGRATION — IHttpClientFactory +
error-classification + async-email ADR (defense panel)"). It is the **architect contract** that the
integration-resilience fixes downstream must honor — `BLIND-5` (Stripe+SendGrid via
`IHttpClientFactory`, T-map line 69), `BLIND-6` (error classification across the integration layer,
line 70), `BLIND-1` (registration/reset email async off the critical path, line 71), `LG-06`
(membership-command provider try/catch, line 72), and `BLIND-7` (Mapbox 429 handling, Wave-2 line 96)
all carry `depends_on: ADR-INTEGRATION`. This ticket **writes the ADR**; it does **not** implement the
clients.

The defense-panel "theme 4 — integrations / resilience" is grounded in the real code and the audit:
- **Socket-per-call HTTP, no pooling (BLIND-5).** `StripeClient` constructs a **fresh**
  `new global::Stripe.StripeClient(config.SecretKey)` on **every** call (`StripeClient.cs:42, 50, 83,
  108, 140, 151, 165, 186, 225, 262, 312`), and `SendGridClientFactory.CreateClient()` returns
  `new SendGridClient(sendGridConfig.ApiKey)` per send (`SendGridClientFactory.cs:13-16`,
  registered transient in `SendGridExtensions.cs:11-15`). Neither flows through `IHttpClientFactory`,
  so there is no `SocketsHttpHandler` pooling / `PooledConnectionLifetime` / Polly handler — the
  classic socket-exhaustion + stale-DNS footgun. The one service that **does** it right is
  `MapboxGeocodingService` (`MapboxGeocodingService.cs:15, 53` — `IHttpClientFactory.CreateClient("Mapbox")`),
  which the ADR uses as the reference shape.
- **No error classification (BLIND-6).** Each integration handles failure ad-hoc:
  `SendGridClientFactory` collapses any non-2xx into a single `email.sending_failed`
  (`SendGridClientFactory.cs:11, 26-30`) with no transient-vs-permanent distinction; `MapboxGeocodingService`
  swallows a fixed exception set and returns `null` (`MapboxGeocodingService.cs:68-74`); `StripeClient`
  lets `StripeException` propagate raw. There is no shared taxonomy (transient/retryable vs
  permanent/4xx vs auth/config) that handlers and queue consumers can branch on consistently.
- **Email is synchronous on the critical path (BLIND-1).** `Register.Handler` awaits
  `emailService.SendEmailConfirmationAsync(...)` **inline** (`Register.cs:89`) before returning success;
  password-reset / resend follow the same shape (`ResendConfirmationEmail.cs`). A SendGrid outage
  therefore **hard-fails registration and reset** — a downstream-dependency failure becomes a
  user-facing 500 on a core flow.

## Acceptance criteria
- [ ] **AC1 (ADR exists & accepted)** — Given the backlog, When this ticket is done, Then
  `agents/backlog/adr/0004-integration-resilience-contract.md` exists, follows the project ADR template
  (Status/Date/Applies-to header, Context, Decision, Consequences, "How a reviewer verifies
  compliance"), and is marked `Status: accepted`. The ADR number is recorded back into this ticket's
  `adrs:` frontmatter and added to TICKET-MAP's ADR list so `BLIND-5/6/1`, `LG-06`, `BLIND-7` can cite
  it.
- [ ] **AC2 (D1 — IHttpClientFactory contract, the BLIND-5 frozen seam)** — Given the ADR, When the
  decision section is read, Then it **mandates** that every outbound HTTP integration (Stripe, SendGrid,
  FCM, Mapbox) is constructed through a named/typed `IHttpClientFactory` registration with an explicit
  `SocketsHttpHandler.PooledConnectionLifetime`, and it **forbids** `new HttpClient(...)` /
  per-call SDK-client construction in handlers and services. It names the exact offending call sites to
  be migrated (`StripeClient.cs` per-method `new StripeClient(...)`; `SendGridClientFactory.cs:13-16` +
  `SendGridExtensions.cs:11-15`) and cites `MapboxGeocodingService.cs:53` as the conforming reference.
- [ ] **AC3 (D2 — error-classification taxonomy, the BLIND-6 frozen seam)** — Given the ADR, When the
  taxonomy section is read, Then it defines a single closed classification (at minimum:
  Transient/Retryable, Permanent/Caller-error, Auth/Config, Timeout) with a stated mapping from
  HTTP-status / SDK-exception families to each class, and the rule that **consumers branch on the class,
  not on raw status/exception type**. It specifies how a classification result surfaces to a command
  handler (a typed result/`Error.Code` family under `BusinessErrorMessage`, e.g. the existing
  `email.sending_failed`) vs. to a queue consumer (retry-vs-permanent-ack), consistent with ADR-0002's
  transient/permanent retry rules for `generate-receipt`.
- [ ] **AC4 (D3 — async-email contract, the BLIND-1 frozen seam)** — Given the ADR, When the
  async-email decision is read, Then it states that **registration / password-reset / resend
  confirmation email is dispatched off the request critical path** (enqueued via the ADR-0002
  `IPendingDispatch` / `notifications-dispatch` seam, **not** awaited inline), so a SendGrid outage no
  longer fails `Register.Handler` (`Register.cs:89`). It defines the success contract (the command
  returns success once the user + confirmation code are committed; the email is an at-least-once
  downstream effect) and the **carve-out**: which sends, if any, legitimately stay synchronous (and
  why). It cross-references ADR-0002 so the two contracts do not contradict.
- [ ] **AC5 (D4 — provider-call hygiene / LG-06 + BLIND-7 hooks)** — Given the ADR, When the
  consequences are read, Then it states the **narrow try/catch + classify** rule for provider calls
  inside command handlers (the B8/S7 hygiene `LG-06` enforces) and the **rate-limit / 429 handling**
  expectation (Retry-After honoring) that `BLIND-7` (Mapbox 429) implements — without designing those
  fixes here.
- [ ] **AC6 (scope & traceability)** — Given the ADR, When "Out of scope" is read, Then it explicitly
  states the ADR designs the **contract only** and lists the implementing tickets (`BLIND-5`,
  `BLIND-6`, `BLIND-1`, `LG-06`, `BLIND-7`) as the work that lands the code, each TEST-FIRST per
  `agents/knowledge/testing.md`. The ADR contains **no code migration** — those land in their own
  tickets.

## Out of scope
- **Implementing** the `IHttpClientFactory` registrations / Polly handlers (`BLIND-5`), the
  classification types and their wiring (`BLIND-6`), the async-email enqueue + `Register.Handler` change
  (`BLIND-1`), the membership try/catch (`LG-06`), or Mapbox 429 handling (`BLIND-7`). This ticket
  freezes the contract; those tickets write the code.
- The **Mapbox-token-in-URL leak** (`BLIND-2`, findings line 61) — a separate security finding, not
  part of this contract.
- The **outbox / dispatch ordering** itself — owned by **ADR-0002** (`0002-outbox-dispatch-contract.md`);
  this ADR references it for the async-email queue path and the transient/permanent retry rules but does
  not redefine it.
- Any webhook **signature-verification** decision (the suspected `StripeSubscriptionWebhookHandler`
  finding, blindspots line 20-25) — security pass, not this contract.

## Implementation notes
- **TEST-FIRST per `agents/knowledge/testing.md`:** an ADR is a contract document, so it has **no unit
  tests of its own** — but it must be written so the work it governs is testable test-first. Each AC
  for `BLIND-5/6/1/LG-06/BLIND-7` must be phrased as an **observable, assertable** behavior (e.g.
  "the same `HttpMessageHandler` instance is reused across N sends", "a 503 maps to Transient and is
  retried; a 422 maps to Permanent and is acked", "a SendGrid outage does not change `Register`'s HTTP
  status") so each downstream ticket can write the red test before the code (testing.md §"Test-first at
  the contract").
- **Governing ADR for the async-email path:** **ADR-0002** (`agents/backlog/adr/0002-outbox-dispatch-contract.md`)
  — the email enqueue rides the same `IPendingDispatch` / post-commit dispatch seam and must use its
  transient/permanent retry vocabulary. Do not invent a parallel dispatch mechanism.
- **This ticket authors a NEW ADR.** Model it on the existing accepted ADRs
  (`0001-authorization-model.md`, `0002-outbox-dispatch-contract.md`, `0003-partitioned-rate-limiting.md`):
  same header block, an immutable-once-accepted note, and a "How a reviewer verifies compliance" section
  with **mechanical checks** (e.g. grep for `new HttpClient`/per-call SDK-client construction returns
  zero in `Features/**` and `Infra.Clients/**` outside the factory registrations; every provider call
  in a handler is inside a classify-try/catch).
- **Serialization cluster:** **none.** This ticket is **not** in any TICKET-MAP shared-file cluster — it
  adds a doc under `agents/backlog/adr/` and touches no source file, so it is safe to run concurrently
  with any Wave-0/Wave-1 code ticket. (Its **dependents** `BLIND-5`/`BLIND-6` will touch
  `StripeClient.cs` / `SendGridClientFactory.cs` / host `ServiceExtensions.cs` and must serialize among
  themselves at implementation time — note that in those tickets, not here.)
- **Routing (`agents/process/routing.md`):** architect authors the ADR; spawn a **reviewer in parallel**
  to check it against the cited code and ADR-0002 for contradictions before `done`. `security_touching:
  false` (contract doc, no auth/secret/PII surface changed) → no Security gate; no QA gate (no code).
  PM reconciles the reviewer verdict, then marks `done` and unblocks `BLIND-5/6/1`, `LG-06`, `BLIND-7`.
- **Code evidence cited:** `StripeClient.cs:42,50,83,108,140,151,165,186,225,262,312`;
  `SendGridClientFactory.cs:13-16,26-30`; `SendGridExtensions.cs:11-15`;
  `MapboxGeocodingService.cs:53,68-74`; `Register.cs:89`. Audit refs: findings `BLIND-1`
  (`AUDIT-2026-06-01-findings.md:59-60`), blindspots A "Push/email/PDF/geocoding clients unaudited"
  (`AUDIT-2026-06-01-blindspots.md:27-31`) and D1 "Integration & webhook security pass"
  (`:68-70`).

## Status log
- 2026-06-01 00:00 — draft (created by pm)

## Review
<!-- reviewer / security / optimizer write verdicts here; PM reconciles before advancing state -->
