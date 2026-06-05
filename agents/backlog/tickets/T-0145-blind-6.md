---
id: T-0145
title: Error classification (Transient/Permanent/Config) across integration layer
status: draft
size: M
owner: â€”
created: 2026-06-01
updated: 2026-06-01
depends_on: [T-0141, T-0144]
blocks: []
stories: []
adrs: []
layers: [backend]
security_touching: false
manual_steps: []
sprint: 1
source: finding BLIND-6
---

## Context
Audit finding **BLIND-6** (`agents/backlog/audits/AUDIT-2026-06-01-findings.json` id `BLIND-6`;
slice report `AUDIT-2026-06-01-slice-reports.md:2431`): there is **no error classification**
(`Transient | Permanent | Configuration | Unknown`) anywhere in the integration layer, so retries
treat all failures alike. This violates `agents/knowledge/runtime-readiness.md:26-28` ("every external
call classifies its error and logs the boundary") and `:55-57` (`Transient` â†’ retry; `Permanent` â†’
stop + flag; `Configuration` â†’ alert, don't retry forever).

Concrete holes confirmed in code:
- `EmailService.cs:32-43` â€” Polly `HandleResult(r => !r.IsSuccessStatusCode).Or<HttpRequestException>()`
  retries **3Ã—** on *any* non-2xx, including permanent 4xx (bad template id, invalid recipient) and a
  401 from a rotated SendGrid key (a Configuration error) â€” burning attempts that can never succeed and
  never surfacing a config-spike signal for the owner (`runtime-readiness.md:63`).
- `MapboxGeocodingService.cs:54-74` â€” a broad `catch ... when (ex is HttpRequestException or ...)`
  collapses every failure (incl. 429 and 401) to a Warning + `return null`; no class distinction.
- `FcmPushDispatcher.cs:75-81` â€” the outer `catch` treats an init/transport failure as all-failed and
  prunes nothing; per-token classification at `:96-98` is correct and is the model to generalise.
- `StripeClient.cs` â€” no classification at all (idempotency keys are present, but failures aren't typed).

This ticket is the **second** Wave-1 integration ticket: it builds the shared classifier on top of the
`IHttpClientFactory` routing delivered by its dependency **T-0141 / BLIND-5**.

## Acceptance criteria
- [ ] **AC1** â€” Given a shared classifier helper `Transient | Permanent | Configuration | Unknown`,
  When it is given a representative failure (HTTP status, exception type, SendGrid `Response`, Stripe
  `StripeException`, FCM `MessagingErrorCode`), Then it returns the documented class; a unit test in
  `Cleansia.Tests` covers each mapping including the boundary cases (429â†’Transient, 5xxâ†’Transient,
  401/403â†’Configuration, bad-template/invalid-recipient/400-validationâ†’Permanent, unknownâ†’Unknown).
  *(test-first: these cases are written red before the helper exists.)*
- [ ] **AC2** â€” Given the SendGrid path (`EmailService.cs`), When the send fails, Then Polly retries
  **only** `Transient` failures; a `Permanent` 4xx (bad template / invalid recipient) is **not** retried;
  a `Configuration` 401/403 is **not** retried, is logged **once at Error**, and increments a metric/
  counter for owner alerting. A unit test asserts attempt count = 1 for Permanent and for Configuration
  vs the existing retry budget for Transient.
- [ ] **AC3** â€” Given the Mapbox path (`MapboxGeocodingService.cs`), When geocoding fails, Then the
  failure is classified before the existing `return null` degrade; a `Configuration` (401) is logged once
  at Error + metric and not silently swallowed as a routine Warning. (429 handling is BLIND-7's ticket â€”
  here only the classification + boundary log, not a 429-specific retry policy.)
- [ ] **AC4** â€” Given the FCM path (`FcmPushDispatcher.cs:75-81`), When the outer dispatch throws, Then
  the failure is classified and logged at its boundary with the class; the existing per-token dead-token
  pruning at `:96-98` is preserved (no regression â€” covered by a characterization test pinning current
  pruning behavior first).
- [ ] **AC5** â€” Given the Stripe path (`StripeClient.cs`), When a `StripeException` is thrown, Then it is
  classified at the boundary and logged with the class; idempotency-key behavior is unchanged.
- [ ] **AC6** â€” Every external-call boundary touched logs outcome + error **class**, satisfying
  `runtime-readiness.md:71`; a spike in `Permanent`/`Configuration` is observable via the emitted
  metric/counter (`runtime-readiness.md:63`).

## Out of scope
- Mapbox 429 / rate-limit-aware retry policy and geocode backfill â€” that is **BLIND-7** (Wave 2).
- Moving email off the registration/reset critical path â€” that is **BLIND-1**.
- Idempotent push fan-out / dedup â€” that is **F7/BLIND-8**.
- Routing Stripe/SendGrid through `IHttpClientFactory` â€” delivered by the dependency **T-0141/BLIND-5**;
  this ticket only adds the classification layer on top.
- Wiring alerts/dashboards on the new metric (owner ops); this ticket emits the metric, it does not
  configure alerting.

## Implementation notes
- **Governing ADR:** **ADR-INTEGRATION** (`IHttpClientFactory` + error-classification + async-email
  contract â€” see `AUDIT-2026-06-01-execution-plan.md:151`). It is the Wave-1 contract this ticket
  implements; none of the numbered ADRs 0001/0002/0003 govern here, hence `adrs: []`. Read
  ADR-INTEGRATION before starting.
- **Dependency:** `T-0141` (BLIND-5) must be `done` first â€” the classifier consumes the SDK transport
  routed through `IHttpClientFactory` that BLIND-5 establishes (Mapbox already uses it,
  `MapboxGeocodingService.cs:53`).
- **Serialization cluster:** **not** in any shared-file cluster in `TICKET-MAP.md`. It edits four
  distinct integration clients (`EmailService.cs`, `MapboxGeocodingService.cs`, `FcmPushDispatcher.cs`,
  `StripeClient.cs`) plus a new shared classifier helper â€” no overlap with the BLIND-5 surface. Still,
  serialize **after** T-0141 per `depends_on` (T-0141 may touch the same client constructors/DI).
- Generalise the existing correct FCM per-token classifier (`FcmPushDispatcher.cs:89-102`) into the
  shared helper rather than inventing a new taxonomy.
- Cited code to fix: `EmailService.cs:32-43`, `MapboxGeocodingService.cs:54-74`,
  `FcmPushDispatcher.cs:75-81`, `StripeClient.cs` (boundary catches).
- **Built TEST-FIRST** per `agents/knowledge/testing.md`: the classifier is pure logic â†’ strict
  red-green-refactor (knowledge/testing.md:32). Each class-mapping is a red unit test before the helper;
  the FCM pruning regression is a **characterization test** (testing.md:53) pinning current behavior
  before refactor. Status log must show the redâ†’green order.

## Status log
- 2026-06-01 â€” draft (created by pm)

## Review
<!-- reviewer / security / optimizer write verdicts here; PM reconciles before advancing state -->
