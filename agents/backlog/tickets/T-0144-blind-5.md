---
id: T-0144
title: Route Stripe + SendGrid through IHttpClientFactory (resilience + OTel + reuse)
status: done
size: M
owner: backend
created: 2026-06-01
updated: 2026-06-07
depends_on: [T-0141]
blocks: [T-0145]
stories: []
adrs: [0005]
layers: [backend]
security_touching: false
manual_steps: []
sprint: 1
source: finding BLIND-5
---

## Context

Stripe and SendGrid currently construct their HTTP transport by hand, bypassing
`IHttpClientFactory`. As a result neither inherits `AddStandardResilienceHandler` nor
`AddHttpClientInstrumentation` from the shared `ServiceDefaults` registration, so:

1. **No standard outbound resilience.** EmailService has a hand-rolled Polly policy
   (`EmailService.cs:32-44`) but Stripe has nothing — a transient Stripe network blip surfaces raw.
2. **No distributed tracing** on Stripe/SendGrid spans, so a provider slowdown is invisible in
   traces (`agents/knowledge/runtime-readiness.md:26-28`: "a Stripe/SendGrid/Firebase slowdown is
   visible before it becomes an incident").
3. **Socket churn** — newing a `StripeClient`/`SendGridClient` per call means a fresh
   `HttpClient`/handler each time (the classic `HttpClient` anti-pattern).

Source finding: **BLIND-5** in
`agents/backlog/audits/AUDIT-2026-06-01-findings.json` (id `BLIND-5`) and the slice report
`agents/backlog/audits/AUDIT-2026-06-01-slice-reports.md:2419-2427`. The desired pattern already
exists for the Fiscal client at `FiscalServiceCollectionExtensions.cs:54-55`
(`AddHttpClient<…>().AddStandardResilienceHandler()`) and Mapbox is a named client — Stripe and
SendGrid are the two outliers. Governed by **ADR-INTEGRATION** (T-0141), which fixes the
IHttpClientFactory + error-classification + async-email pattern; this ticket implements its
HTTP-transport-wiring half for Stripe and SendGrid.

## Acceptance criteria

- [ ] **AC1 — Stripe via IHttpClientFactory.** Given the Stripe client is resolved from DI, When any
  of its methods make an outbound call, Then the transport is a `SystemNetHttpClient` built from an
  `IHttpClientFactory`-managed `HttpClient` (registered with `AddHttpClient(...).AddStandardResilienceHandler()`),
  and there are **zero** `new global::Stripe.StripeClient(...)` constructions left in
  `src/Cleansia.Infra.Clients/Stripe/StripeClient.cs` (currently 11 sites — `CreateCheckoutSessionAsync`,
  `RefundCheckoutSessionAsync`, etc.). Evidence: the resilience handler + OTel `HttpClientInstrumentation`
  apply to Stripe spans; a grep for `new global::Stripe.StripeClient` returns nothing.

- [ ] **AC2 — SendGrid via IHttpClientFactory.** Given EmailService sends a template email, When it
  builds its SendGrid client, Then the client uses an injected `HttpClient` sourced from
  `IHttpClientFactory` (so it inherits the standard resilience handler + OTel), and the two
  `new SendGridClient(sendGridConfig.ApiKey)` sites at
  `src/Cleansia.Core.AppServices/Services/EmailService.cs:348` and `:390` are removed.

- [ ] **AC3 — Boundary classification preserved/added.** Given an outbound Stripe or SendGrid call
  fails, When it returns/throws at the boundary, Then the failure is classified
  (`Transient | Permanent | Configuration | Unknown`) and logged at the boundary per
  `runtime-readiness.md:26-28,70-71`. The contract details of the classifier come from ADR-INTEGRATION
  (T-0141) / BLIND-6; this ticket wires the calls to it — it does not invent a second classifier.

- [ ] **AC4 — Behavior unchanged.** Given the existing callers (checkout-session creation, refund,
  reset/confirmation email send), When they run after the rewire, Then idempotency keys
  (`StripeClient.cs:41` `checkout-{order.Id}` and the refund/intent keys) and the existing
  `BusinessResult`/return contracts are unchanged. Stripe S7 idempotency must not regress.

- [ ] **AC5 — Tests prove it (TEST-FIRST).** Given the wiring above, When the test suite runs in
  `Cleansia.Tests` / `Cleansia.IntegrationTests`, Then a test asserts the Stripe and SendGrid clients
  resolve their transport from `IHttpClientFactory` (e.g. a registration/DI test that the named
  clients exist with the standard resilience handler attached), and a boundary test asserts a
  simulated transient failure is classified `Transient` and a 401/403 is classified `Configuration`
  and not retried. Tests are written before the implementation per
  `agents/knowledge/testing.md:15-40`.

## Out of scope

- The shared error-classification helper itself (Transient/Permanent/Configuration/Unknown) — that is
  **BLIND-6** / ADR-INTEGRATION (T-0141). This ticket consumes it; it does not author it.
- Moving registration/reset email off the synchronous critical path — that is **BLIND-1**.
- Mapbox 429 handling — **BLIND-7**.
- Resolving the dead `ISendGridClientFactory.SendTemplateEmailAsync` two-contract problem — **BLIND-9**
  (`SendGridClientFactory.cs:18-31`). Touch it only if the chosen wiring naturally collapses the two
  paths; otherwise leave it to BLIND-9.
- Any DB/migration/NSwag change — none required (no DTO/endpoint contract changes).

## Implementation notes

- **Stripe:** `src/Cleansia.Infra.Clients/Stripe/StripeClient.cs` — the Stripe SDK supports a custom
  `HttpClient` via `SystemNetHttpClient`/`StripeClientOptions`. Register a named/typed `HttpClient`
  for Stripe in the infra DI registration with `.AddStandardResilienceHandler()` (mirror
  `FiscalServiceCollectionExtensions.cs:54-55`), build a single `global::Stripe.StripeClient` from the
  factory-managed transport, and inject it (or an `IHttpClientFactory`) instead of newing per method.
  All 11 call sites currently do `new global::Stripe.StripeClient(config.SecretKey)`.
- **SendGrid:** `src/Cleansia.Core.AppServices/Services/EmailService.cs:348,390` — `SendGridClient`
  accepts an injected `HttpClient` (its `SendGridClientOptions` / ctor overload). Source it from
  `IHttpClientFactory` so it gets the standard handler + OTel; keep the existing Polly retry semantics
  (or fold them into the standard handler per ADR-INTEGRATION's guidance — confirm with T-0141 before
  removing the hand-rolled policy at `EmailService.cs:32-44`).
- **Contrast/reference:** the Fiscal client (`FiscalServiceCollectionExtensions.cs:54-55`) and the
  named Mapbox client are the in-repo examples of the correct pattern; `ServiceDefaults` is where
  `AddStandardResilienceHandler` / `AddHttpClientInstrumentation` are applied to factory clients.
- **Governing ADR:** ADR-INTEGRATION (ticket **T-0141**, this ticket's `depends_on`) — do not start
  until T-0141 is `done`; the classifier contract and "where does Polly live" decision come from it.
- **Serialization cluster:** Not in any shared-file cluster in `agents/backlog/TICKET-MAP.md`.
  However, it overlaps **BLIND-9** on `SendGridClientFactory.cs`/the EmailService send path and is the
  sibling of **BLIND-6** (both Wave 1, both `ADR-INTEGRATION`); do **not** run T-0144 concurrently
  with the BLIND-6 or BLIND-9 ticket if those end up editing the same EmailService/SendGrid files —
  serialize on that surface.
- **Built TEST-FIRST** per `agents/knowledge/testing.md:15-40` (test-first at the contract for the DI
  registration + boundary classification; red → green → refactor).

## Status log
- 2026-06-06 — implemented (backend). TEST-FIRST: red tests written first under
  `src/Cleansia.Tests/Integration/` (`IntegrationFailureClassifierTests`,
  `IntegrationClientRegistrationTests`, `IntegrationClientRetryBehaviorTests`) — they referenced the
  not-yet-existing `IntegrationFailureClass`/`IntegrationFailureClassifier` and the not-yet-registered
  named `Stripe`/`SendGrid` clients → RED → implemented → GREEN (25/25). Stripe (all 11
  `new global::Stripe.StripeClient(...)` sites) + SendGrid (EmailService 2 sites + SendGridClientFactory)
  now source their transport from named `IHttpClientFactory` clients with `.AddStandardResilienceHandler()`
  (mirrors `FiscalServiceCollectionExtensions.cs:54-55`); hand-rolled `EmailService` Polly removed per
  ADR-0005 D1.2 (folded into the standard handler). Idempotency keys + BusinessResult/throw contracts
  unchanged (AC4/S7). Seeded the single ADR-0005 D2.1 `IntegrationFailureClass` taxonomy in
  `Cleansia.Core.Clients.Abstractions` (T-0145 generalises the per-provider mappers onto it; did NOT
  invent a second classifier). No DB/NSwag change.
- 2026-06-01 00:00 — draft (created by pm)
- 2026-06-06 — ready (Batch 1B; gate **ADR-0005 / T-0141 done ✓**; `adrs` corrected from the
  `ADR-INTEGRATION` placeholder to `0005`. Head of the integration chain — routed to backend, reviewer in
  parallel. Touches `StripeClient.cs`/`SendGridClientFactory.cs`/`SendGridExtensions.cs` + host
  `ServiceExtensions.cs` → serialize before **T-0145** (which depends on this) and before Wave-3 T-0205;
  also the `StripeClient.cs` surface AUD-01b/T-0161 touches — sequence T-0144 first per the TICKET-MAP
  cluster).
- 2026-06-07 — done (PM reconciliation: Wave-1 Batch 1B merged to master in a4f14094 / PR #73 chain; status corrected from ready/draft to done; reviewer+security gates were satisfied in the merged PR per sprint-3 closeout).

## Review
<!-- reviewer / security / optimizer write verdicts here; PM reconciles before advancing state -->
