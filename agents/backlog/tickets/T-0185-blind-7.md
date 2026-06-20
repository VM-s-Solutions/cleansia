---
id: T-0185
title: Mapbox 429/rate-limit handling
status: done
size: M
owner: â€”
created: 2026-06-01
updated: 2026-06-15
depends_on: [T-0141, T-0145]
blocks: []
stories: []
adrs: [0004]
layers: [backend]
security_touching: false
manual_steps: []
sprint: 2
source: finding BLIND-7
---

## Context
Finding **BLIND-7** (`agents/backlog/audits/AUDIT-2026-06-01-slice-reports.md:2443`, findings.json
entry `BLIND-7`, execution-plan line 179): `MapboxGeocodingService.GeocodeAsync` cannot distinguish
"address genuinely not found" from "Mapbox rate-limited / down". On a 429 (or any 5xx / timeout)
`GetFromJsonAsync` throws `HttpRequestException`/`TaskCanceledException`, which is swallowed by the
broad `when` filter at `MapboxGeocodingService.cs:68`, logged as a routine `Warning` with the *same*
message as a real geocode miss (`:59-61` and `:70-72`), and `return null`. The caller
`AddressGeocoder.PopulateCoordinatesAsync` (`AddressGeocoder.cs:23`) treats `null` as "nothing to do"
and silently leaves the address with no coordinates.

Impact: Mapbox v6 enforces per-token rate limits, so a burst of bookings hits 429; during that window
**every** order lands with no coordinates, invisible in logs (identical Warning to a genuine miss),
with no retry and no backfill. Map/routing and distance-based pay
(`expensesPay = distance Ã— distanceRate`) silently degrade. The 5s client timeout
(`ServiceCollectionExtensions.cs:20-23`) means a slow Mapbox also fails open to `null`.

This ticket implements the **rate-limit / 429-aware** geocoding behavior that ADR-INTEGRATION
(ADR-0004, authored by the `depends_on` ticket T-0141, AC5/D4 â€” Retry-After honoring) requires. It is
the BLIND-7 hook the ADR explicitly reserves.

## Acceptance criteria
- [ ] **AC1 (transient â‰  not-found)** â€” Given Mapbox returns HTTP 429, When `GeocodeAsync` is called,
  Then the 429 is classified as **transient** and is NOT collapsed into the existing "address not found
  â†’ return null" Warning path; the two cases are logged distinctly (a rate-limit/transient event is
  observable, per `runtime-readiness.md:63`). A unit test simulates a 429 (mocked
  `HttpMessageHandler`/`HttpClientFactory`) and asserts the transient classification + distinct log
  level/event, not the genuine-miss Warning.
- [ ] **AC2 (5xx & timeout treated as transient)** â€” Given Mapbox returns 503 (or the request times out
  via the 5s `HttpClient` timeout â†’ `TaskCanceledException`), When `GeocodeAsync` is called, Then the
  failure is treated as transient (same classification as AC1), distinct from a genuine empty feature
  set. Tests cover 503 and the timeout/`TaskCanceledException` branch.
- [ ] **AC3 (Retry-After honored on 429)** â€” Given a 429 response carrying a `Retry-After` header, When
  the resilience handling retries, Then the back-off respects `Retry-After` (per ADR-0004 AC5). A test
  asserts the `Retry-After` value drives the wait/decision rather than being ignored.
- [ ] **AC4 (genuine miss unchanged)** â€” Given Mapbox returns 200 with an empty/short `features` list
  (`MapboxGeocodingService.cs:56-63`), When `GeocodeAsync` is called, Then it still returns `null` and
  logs the existing genuine-miss Warning (no retry, no transient escalation). A test pins this
  current-behavior contract so the change does not regress real misses.
- [ ] **AC5 (happy path unchanged)** â€” Given Mapbox returns a valid feature, When `GeocodeAsync` is
  called, Then it returns `new GeoCoordinates(coordinates[1], coordinates[0])` (lon/lat order preserved,
  `:65-66`). A test asserts the parsed coordinate is unchanged.
- [ ] **AC6 (caller contract preserved)** â€” Given the geocoder degrades after exhausted transient
  handling, When `AddressGeocoder.PopulateCoordinatesAsync` runs, Then order/address creation still
  succeeds (geocoding stays best-effort, never throws into the order path) but the transient degrade is
  now visible in logs/metrics rather than indistinguishable from a miss. A test asserts the caller is
  not made to throw.

## Out of scope
- The shared error-classification helper (Transient/Permanent/Configuration/Unknown) and the Mapbox
  boundary-log refactor â€” authored by **BLIND-6** (T-0145, AC3); this ticket consumes that
  classification and adds the **429-specific Retry-After retry policy** on top. T-0145's AC3 explicitly
  defers 429 handling to this ticket.
- Routing Stripe/SendGrid (and any non-Mapbox integration) through `IHttpClientFactory` resilience â€”
  **BLIND-5** (T-0144).
- The Mapbox-token-in-URL secret leak (`MapboxGeocodingService.cs:45`) â€” separate security finding
  **BLIND-2**; do not change the URL/token handling here beyond what the resilience policy requires.
- An idempotent **geocode-backfill Function** for orders already missing coordinates â€” noted as a
  follow-up in the finding ("Geocode-backfill story"); this ticket does the rate-limit-aware geocoding
  only, not the backfill job.

## Implementation notes
- **Governing ADR:** ADR-0004 (ADR-INTEGRATION), authored by the `depends_on` ticket **T-0141**
  (AC5/D4 â€” narrow classify + Retry-After honoring). Do not start until T-0141 is `done` and ADR-0004 is
  `accepted`; the policy shape (resilience handler vs. enqueue-backfill, retry budget, Retry-After
  parsing) must follow that contract, not be invented here.
- **Serialization:** BLIND-7 is **not** in any TICKET-MAP shared-file cluster. However it edits
  `src/Cleansia.Infra.Services/Geocoding/MapboxGeocodingService.cs:54-74` and possibly
  `ServiceCollectionExtensions.cs:20-23` (the `"Mapbox"` `AddHttpClient` registration, to attach the
  resilience handler) â€” both also touched by **T-0145 / BLIND-6** (Mapbox classification + boundary
  log). Serialize after T-0145 to avoid a collision on `MapboxGeocodingService.cs`; build the 429 retry
  policy on top of T-0145's classification rather than re-adding a parallel `catch`.
- **Where the change lands:** the resilience/Retry-After policy belongs on the `"Mapbox"` named
  `HttpClient` (`ServiceCollectionExtensions.cs:20-23`) per ADR-0004's IHttpClientFactory seam; the
  distinct logging/degrade decision belongs in `MapboxGeocodingService.cs:51-74`. Preserve the existing
  best-effort contract for the caller `AddressGeocoder.cs:23` (null â†’ skip, never throw into order
  creation).
- **TEST-FIRST per `agents/knowledge/testing.md`:** this is integration-resilience logic (failure-branch
  behavior), so write the red tests first â€” `testing.md` Â§"Test-first at the contract". Each AC maps to a
  test case in `Cleansia.Tests` driving `GeocodeAsync` via a mocked `HttpMessageHandler`/
  `IHttpClientFactory`: 429â†’transient, 503â†’transient, timeoutâ†’transient, 429+Retry-After honored,
  empty-featuresâ†’genuine-miss-null, validâ†’coordinates, caller-does-not-throw. Status log must show
  "red â†’ green" with the failing test predating the policy code. No happy-path-only tests (per
  testing.md anti-patterns); the transient/miss distinction is the whole point.
- **Routing (`agents/process/routing.md`):** backend implements; spawn a **reviewer in parallel** on the
  same ticket. `security_touching: false` (no auth/secret/PII surface changed â€” token handling is BLIND-2)
  â†’ no Security gate. QA gate applies (resilience behavior under simulated 429/timeout). No `manual_steps`
  (no migration, no DTO/endpoint change â†’ no nswag-regen).
- **Code evidence:** `MapboxGeocodingService.cs:45,54,56-63,65-66,68-74`;
  `ServiceCollectionExtensions.cs:20-24`; `AddressGeocoder.cs:16-26`. Audit refs: finding **BLIND-7**
  (slice-reports.md:2443, findings.json `BLIND-7`, execution-plan line 179); dependency contract
  **T-0141** AC5; sibling **T-0145 / BLIND-6** AC3.

## Status log
- 2026-06-01 — draft (created by pm)
- 2026-06-09 — backend (Wave-3 Batch 3D, test-first): Mapbox geocoder — HTTP 429 / 503 / timeout
  (TaskCanceledException) classified TRANSIENT and logged distinctly from the genuine "address not found"
  Warning; `Retry-After` honored on 429 (ADR-0005 resilience); genuine miss (200 + empty features) still
  returns null + the existing Warning; happy-path coordinate parse unchanged (lon/lat order); the caller
  `AddressGeocoder.PopulateCoordinatesAsync` stays best-effort and never throws into the order path. Tests
  cover each branch + pin the unchanged contracts. Reviewer **APPROVED**. Build + tests green. No manual step.

## Review
- 2026-06-09 reviewer: **APPROVED** — transient classification + Retry-After verified; genuine-miss and
  happy-path contracts pinned; caller stays best-effort.
