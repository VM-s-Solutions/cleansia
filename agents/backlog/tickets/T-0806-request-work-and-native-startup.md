---
id: T-0806
title: Reduce request logging work, evaluate HTTP JSON compression and overlap native reads
size: L
owner: pm
created: 2026-10-09
updated: 2026-10-10
depends_on: [T-0805]
blocks: []
stories: []
adrs: []
layers: [backend, android, ios, docs]
security_touching: true
---

## Context

Mike authorized finishing and merging Wave B, then the next two roadmap waves C
and D one at a time, using recommended routine choices and simulators only. Wave B
[PR #313](https://github.com/VM-s-Solutions/cleansia/pull/313) actually merged at
2026-10-09 08:08:21 UTC as `18a2e5ac8e118be1c527443dc8f6cc48ee30ce6c`; its tree
equals the corrected head that passed seven workflows, eight jobs and 36 critical
steps. C starts from that fresh master on `codex/wave-c-request-native-startup`.
The original audit SHA-256
`589a02629cbb0b0848b04dce6c11b44b81aa7e2c4cc3388189ee6c1b29724ff4` remains immutable.

This is one user-approved whole-wave ticket and one branch/PR, estimated at 3–5
engineering days, with one commit per independent cause: B001, B009, MOB-PERF-01
and MOB-PERF-02. The three B delivery-closure Markdown records are carried separately
on C after the actual B merge. INDEX.md alone records ticket status. Current native
source bytes match the retained finite source proposal; fresh source/product/fixture
binding, meaningful RED and comparable BEFORE runtime evidence still precede fixes.
Old audit/B products and preparation notes do not constitute the new runtime baseline.
Evidence and scratch helpers remain outside the tree under
`/Users/michael/.codex/scratchpads/cleansia-wave-c-2026-10-09`.

## Doing

- **B001:** retain the same bounded 65,537-character request read at every logger
  level in all five existing RequestLoggingMiddleware copies: the pre-authentication
  body read participates in Kestrel's existing 413 enforcement. When Information is
  disabled, use capture=false to skip StringBuilder/result construction and query,
  user and redaction formatting, while preserving that read and stream semantics.
  Measure the 4,096-character pooled read chunk, explicit logical cap and clearing/
  return in finally. Enabled capture preserves whole-body redaction before truncation,
  UTF-8/BOM handling and sensitive-path suppression. Status-disabled response logging
  may skip its bounded read, but must independently reset the capture MemoryStream
  Position to zero before the final copy. Retain full response buffering, status,
  headers, exception/cancellation behavior and the real-pipeline body limit.
- **B009:** use stock ASP.NET gzip/Brotli Fastest application/json compression only
  for direct HTTP, with explicit EnableForHttps=false and compression before
  RequestLogging so logs redact original JSON. Decline compression for every request
  containing X-Forwarded-Proto, regardless of its value; do not parse a value into a
  new scheme or forwarding/trust policy. Direct TLS remains identity under the stock
  HTTPS setting. Current forwarding handles XFF rather than protocol. An unmarked TLS
  terminator forwarding HTTP is a residual not covered by this boundary or proof;
  no blanket forwarded-HTTPS protection or hosted HTTPS benefit is claimed. Preserve
  other pipeline ordering; no bespoke type or broad eligibility policy is added.
- **MOB-PERF-01:** overlap independent dashboard reads inside existing structured
  lifetimes. iOS preview can start while employee→stats remains dependent; retain the
  final loaded publication and in-place refresh. Android retains stats first and
  immediately observable, then overlaps upcoming/preview with independent field
  updates and final flags only after both. Preserve registration, identity, freshness,
  radius/greeting, critical/optional error and cancellation semantics; obsolete work
  cannot publish after cancellation, clear or replacement.
- **MOB-PERF-02:** one bounded country/session plan flight in each existing customer
  repository. Ordinary same-key callers share success and ordinary failure; failures
  are discarded so the next read retries. Empty Android success remains cached;
  iOS sequential reads remain fresh without a settled success cache. Explicit force
  starts fresh, including during a flight. Capture country/session/reset generation
  and flight identity so market change, clear/new login and late completion cannot
  publish into or remove a replacement flight.

The four primary native holders are iOS partner DashboardViewModel.swift, Android
partner data/dashboard/DashboardRepository.kt, iOS customer
Membership/Data/MembershipRepository.swift and Android customer
core/memberships/MembershipRepository.kt. The existing iOS
Membership/MembershipViewModel.swift reloadPlans caller is explicitly in scope if
needed to forward the defaulted force seam and preserve retry behavior. Existing
Android force parameters and normal callers remain; no other production caller or
holder is implicitly added.

## Acceptance criteria

- [x] **AC1 / B001 behavior:** meaningful RED/GREEN exposes and removes disabled
  capture/formatting work while preserving the same bounded request read at all
  logger levels; zero request-body reads is not an acceptance requirement. The real
  pre-authentication Kestrel 413 behavior and downstream complete-request semantics
  remain unchanged. All five copies retain enabled logged-line/redaction equality,
  exact 65,536/65,537 and multibyte caps, a secret crossing the 4,096-character chunk
  boundary, Warning400/Error500, health/swagger skips, client abort and thrown-before-
  write/partial-write→throw behavior in the real exception-handler pipeline. Explicit
  disabled-response controls prove capture Position=0 before final copy independently
  of whether response logging read anything. Status, body and headers remain
  equivalent; pool clearing/return is reviewed. No new type or weakened bound.
- [x] **AC2 / B001 evidence:** fresh merged-B and AFTER products use the same five-host
  GET/POST, empty/1 KiB/64 KiB/oversize-body and logger-level profiles: authored PRO
  Warning, Information enabled and NullLogger. Record five observations per profile,
  the fixed 100 iterations and ten excluded warmups, exact bytes, allocations and
  elapsed median/p90. Observe actual pooled capacity/cleared length and enabled-body
  tradeoffs. CPU claims use separate TotalProcessorTime deltas; Stopwatch, allocation
  reductions or NullLogger-only results do not establish HTTP latency/production gain.
- [x] **AC3 / B009 safety:** actual public customer web/mobile Service/Package/Extra
  GetOverview direct-HTTP gzip/br negotiation is RED before and GREEN after, with
  decompressed byte/DTO equality and unchanged tenant/market/currency/pay rules.
  Every X-Forwarded-Proto-marked request declines compression, including http, https,
  empty and multiple-value controls. Direct TLS remains identity through stock
  EnableForHttps=false, including auth, quote, personal/order/board, payout/GDPR/audit
  controls. Exercise identity/q=0/unsupported/no Accept-Encoding, Vary tokens, status/
  content-type/cache/CORS, HEAD/range/error/tiny and synthetic protected direct-HTTP
  representation controls. No forwarded-protocol processing or trust policy is added.
  Unmarked terminator traffic remains outside the demonstrated boundary; do not claim
  blanket forwarded-TLS protection, HTTP private-header/Set-Cookie exclusions or a
  stock minimum-size threshold.
- [x] **AC4 / B009 evidence:** paired n=5 direct-HTTP path/host/encoding observations
  retain actual raw wire bytes, uncompressed hashes and latency; qualified warm serial
  CPU batches
  use recorded process ps-time deltas with resolution/noise qualifications and retain RSS snapshots. Tiny JSON/empty arrays may grow and CPU may
  regress; report them. With materially worse cost or no useful HTTP benefit, retain
  the measurements and explicitly defer B009 rather than force a gain. Local TLS uses
  a pinned scratch certificate without installing a trust root. No deployed HTTPS/CDN
  or production transfer claim is made.
- [x] **AC5 / dashboards:** explicit start/release/completion barriers in existing Swift
  and Android suites prove preview starts while iOS employee is held, stats uses its
  actual captured employee ID, and the final load still waits for required legs.
  Android stats stays visible while optional work is blocked; preview completes while
  upcoming is held; both completion orders retain both fields. Optional errors retain
  last good data, critical errors and null-ID/force/freshness behavior remain. Cancelled,
  cleared or superseded work cannot overwrite current snapshot, prompt or final flags.
- [x] **AC6 / plans:** held concurrent same-country success and failure produce one API
  call and equal current-waiter outcomes, followed by fresh retry after failure. Cover
  nil country, empty success, later failure retaining good plans, force during flight,
  reversed market completion, clear/new session and mutation-follow-up refresh. iOS
  owns/publishes the task once; a cancelled waiter cannot suppress a surviving waiter
  or mutate presentation after await. Android uses an actual local Deferred, not a
  success-only mutex claim: a cancelled waiter leaves its owner intact; a cancelled
  first caller cancels its structured child and active same-key waiters may re-elect.
  Old-country/session waiters cannot adopt a new generation/re-elect stale work; force
  replacements start fresh without an endless replacement loop. Matching identity
  controls cancellation-safe cleanup; no network await under the short flight/cache
  mutex and no app-global coroutine scope or error cache. These are acceptance
  requirements and source recommendations, not already-closed runtime evidence.
- [x] **AC7 / native measurements:** preserve exactly 80 signed-in observations per arm
  (four apps × ten process-cold + ten warm-resume), 160 paired observations total, on
  the same owned representative iOS18.6 and Android API35 guests. Extra iOS cold
  warmup is explicitly excluded; signed-out controls have a separately declared equal
  method. Freeze the same merged-B backend executable/configuration, synthetic DB
  fixture/profile, accounts, login/onboarding/permissions, app/harness and observer
  method across BOTH native arms. C backend measurements use a separate host/fixture
  profile. Retain source/product/provider inspections, raw observations, request
  overlap/GetPlans count and bytes, errors/cancellations, median/nearest-rank p90.
  Responsive launch, Android top-activity timing, useful UI and HTTP completion remain
  distinct. Keep valid no-effect startup cohorts; if the changed path is not reached,
  add separately named n=5 production VM/repository controlled-flight diagnostics
  without calling them actual startup/HTTP/physical-device gains.
- [x] **AC8 / full verification:** meaningful RED→GREEN and independent source/security/
  optimizer/artifact review pass. The coordinating reviewer executes fresh combined
  backend build/unit/integration/host suites and clean latest native full schemes;
  actual Android core/partner/customer tests execute with fresh task/XML proof and
  required rerun/no-build-cache handling. Both pinned iOS whole-tree lints, applicable
  generic Debug device builds, touched-logic iOS16.4 floor tests, scoped render proof
  via existing native harnesses, repo/docs/graph checks and all applicable existing
  required checks pass. Preserve six string catalogs, all generated Swift/Kotlin
  keys/bytes, both OpenAPI specs and B's three warning-source bytes; normal generated
  commands, if required by the build, are recorded and resulting identity verified.
  Author-only or historical PASS does not close these gates. Keep conditional skips,
  existing diagnostics and failed attempts visible.
- [ ] **AC9 / delivery:** fresh master is incorporated before final verification;
  actual current-head CI is green, the PR reports results and limits, exact commands
  have observed metadata, and normal SHA-guarded C merge is recorded separately.
  Start Wave D implementation only after actual C merge. No future check/merge is
  inferred from a prepared script, command announcement or this ticket.

## Out of scope

Wave D implementation before C merge; later waves and unranked audit candidates;
API/DTO/schema/index changes; broad HTTP/HTTPS eligibility or settled-cache policy;
unrelated Profile/Board/address/image-cache/dead-code/brand-hold/visual work;
getMine/profile duplication without demonstrated matching force/freshness semantics;
intentional authoritative onboarding profile reads; the 1.8-second splash hold and
Home 1.5-second first-paint fallback; new maintained framework/type/harness/CI gate;
PRO/DEV deployment, reset or load; real payment/mail/push/APNs/provider actions;
distribution/TestFlight/physical devices; owner settings/guest resets, shared AVD or
global-cache cleanup. The original audit, B failures/archives/raw receipts and primary
owner checkout stay intact. No blanket warning-free, security/egress or physical
performance certificate is implied.

## Implementation and evidence notes

Use existing suites/fakes with explicit barrier handshakes; no arbitrary sleep or
Task.yield-only request-start proof. Await and release all owned test work on failure.
Retain one heavy measurement lease and quiet scored windows; light source preparation
may proceed independently. Native guards remain 12 GiB start, 8 GiB reserve, 1,800 s
suite/build deadline and TERM followed by 10 s KILL; ownership-specific guest bounds
and exact cleanup apply. No heavy gate overlaps scored apps. The coordinating lane
owns runtime, generated/source fixture synchronization, fresh baseline/product
binding, command recording and Git/PR actions.

Recorded preparation inputs are C/STATE.md; B raw/planning/backend-c-proposal-001.md
and backend-c-b001-read-buffer-options-001.md; B raw/ios/wave-c-mobile-source-proposal-01.md,
wave-c-mobile-runtime-proposal-02.md and new-source-prep-03-mobile-minimum.md. The newer
accepted bounded Android Deferred decision supersedes the old success-only mutex
minimum. Source-only task/ownership details are recommendations for implementation
and reviewer challenge. Fresh runtime observations close acceptance, with exact
input hashes retained in the off-tree preparation manifest.

The subsequent B001/B009 source challenge supersedes earlier proposal language about
zero disabled request reads and forwarded-TLS handling. Request-limit reads remain;
disabled capture/formatting and independently rewound response copy are the B001
candidate. The settled B009 slice is direct HTTP only, declining every X-Forwarded-
Proto-marked request and retaining stock direct-TLS identity. Unmarked terminator
traffic is an explicit residual. No current implementation or runtime PASS is
claimed by this source-only ticket preparation.

## Work record

- 2026-10-09 — Wave C authorized as the next whole wave after actual B merge. Source-only
  ticket preparation uses the frozen merged-B entry and existing proposals; fresh
  source/products, safety inspections, RED and comparable BEFORE closure remain
  prerequisites to maintained fixes. Final B closure records and the C ticket/INDEX
  entry are separate documentation changes.
- 2026-10-10 — Final local gates recorded at `43c2168`:
  - Backend 9,894 passed.
  - Android 2,760 passed.
  - iOS 3,556 passed plus one conditional VoiceOver skip; touched iOS 16.4 classes 61/61.
  - Lints, generic Debug builds, repository checkers, VitePress and graph all pass.

  The six render checks were captured on owned guests through the existing reviewed
  lifecycle, with Mike-approved AXe for iOS input; everything started was stopped.
  Independent render and code-diff lens reviews pass with qualifications, and AC8 is
  closed. Exact-head CI and the normal merge (AC9) are pending; this record implies
  neither.

## Review

Implementation, measurements, final local gates and independent reviews are recorded
below. Exact-head hosted CI and the normal merge (AC9) remain pending.

### B001 actual focused implementation and comparison, 2026-10-09

All five logging copies and existing tests/architecture documentation are implemented.
Tests-first RED: 55 failed and 271 passed; GREEN: 326 passed with no failures or skips.
Real Kestrel body-limit/exception preservation controls pass 56/56 before and after.
Five runs of 100 scored iterations match all 360 groups, with identical scratch
Program/project bytes and checked forwarded lengths. Warning empty-GET with a
1 KiB response allocates 277066.08 → 6473.68 B/request; sample p50 is
.0061 → .0006 ms and p90 .022 → .0007 ms. Information with a 1 MiB response
regresses in Partner p90 (.1296 → .1592 ms) and separate CPU
(9.914 → 10.252 ms/100 requests); full response buffering remains.
No HTTP or production gain is inferred. A separate stock pool diagnostic observes
requested/actual capacity 4096, the same array re-rented fully zero; it does not
instrument the production pool.

Full methods, raw pins and commands are in agents/WAVE-C-2026-10-09.md.
C/raw/backend/logging-comparison-b001-001.json (SHA256
0db2163039583c65a8f4c5fbbd4032dd412451595b8dc3870c7d09643b445ca1)
retains complete results and TRX lineage. Independent artifact review passes with
mixed Information results explicitly retained. Combined gates and delivery remain pending.

### Native closed evidence and B009 decision, 2026-10-10

Dashboard and plan-flight cause commits retain the independently reviewed candidate bytes. The closed native comparison contains 160 observations per arm: 80 signed-in and 80 separately declared signed-out, 320 total. Selected tagged requests fall 561→544; Customer cold signed-in GetPlans falls 2→1 in each observation on both platforms. Independent closure preserves mixed timing/RSS/PSS results and the proxy/permission-activity limits. Android full suites passed 2760/2760. Fresh combined backend/iOS, render/repo/docs/graph and current-head CI/delivery remain pending.

B009 was evaluated with actual focused RED and corrected GREEN36, then physically measured in 280 groups per arm. Useful HTTP-message reductions coexist with materially higher Brotli catalogue median latency and tiny-message growth. Under AC4 it is explicitly deferred: production/test candidate bytes were restored to retained HEAD, while all source/measurement/test evidence remains preserved. AC3 records evaluated-candidate safety evidence, not delivered compression; measured authenticated controls cover profile, orders, quote and GDPR consent only, with customer-audience board/payout/audit route gaps explicit. No deployed HTTPS, forwarded-terminator, production-transfer or broad security claim is made. See the Wave C report and raw/backend/b009-deferral-001/decision.json. AC8 and AC9 stay open until actual final gates and normal merge.

### Final local verification and six render checks, 2026-10-10

**AC8 is complete.** The earlier "pending" sentences describe their original cutoffs and remain unchanged. These results are at head `43c2168788aed51c1ffbff0323f4d079635c1a71`; the recorded pre-PR fetch found `origin/master` still at `18a2e5ac`, an ancestor (0 behind, 7 ahead, clean).

| Gate | Result |
|---|---|
| Backend fresh Release build | **9,894 passed** (unit 8,564, integration 845, host 485), 0 skipped |
| Android unfiltered suites | **2,760 passed**; unchanged Android inputs, so not rerun |
| iOS lints | SwiftFormat 0/946 after the formatting-only `43c2168`; SwiftLint strict 0 violations in 945 files |
| iOS 18.6 full schemes | **3,557 discovered, 3,556 passed**, one existing conditional VoiceOver skip, 0 failed |
| Generic Debug device builds | Pass for both apps |
| iOS 16.4 touched floor | **61/61** (17 + 44). The Partner wrapper's unsupported `.xctestrun` expectation (exit 1 after its tests passed) is preserved; a separate signed inspection and postguard passed |
| Repository and docs | Nine repository checkers, eight self-test files and VitePress build pass |
| Graph | Refresh and current-source coverage 002 pass |
| Android bridge | Current tracked inputs equal the inspected APK inputs |

Each gate has an accepted independent review.

**The six required render checks were captured on owned guests (iOS 18.6, Android API 35)** through the existing reviewed backend, observer and emulator lifecycle, with ordinary navigation only:
- **Pass:** both Partner Dashboards, the Android Customer Profile and the Android Customer Plus.
- **Pass with a header-truncation observation:** iOS Customer Profile.
- **Pass for loaded nonempty plans:** iOS Customer Plus. Its purchase bar is not shown because the provider-safe product has no Stripe key.

The read-only eight-agent adversarial review confirms all six with qualifications. Its 15 wording corrections are in the receipt corrigendum. Everything started for the run was stopped, the iOS input tool AXe 1.8.0 was installed with Mike's approval, and the observations outside scope are listed in the report, not fixed.

The project's security, optimizer and reviewer lenses reviewed the `src/` diff read-only and found no blocker:
- **Security:** pass, no findings.
- **Optimizer:** pass with one low note (the measured Information-path string build) and one info note (Android stats-first round trip).
- **Correctness:** pass, with two info-level latent contracts.

Review: `raw/reviews/c-diff-lens-review-independent-001.json`.

The report's [final verification section](../../WAVE-C-2026-10-09.md#final-verification-and-six-render-checks-2026-10-10) holds the exact receipts, failed attempts, side effects and closure. **AC9** (exact-head hosted CI and the normal SHA-guarded merge) stays open until those happen. Wave D starts only after the actual C merge.
