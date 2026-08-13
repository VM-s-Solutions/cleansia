---
id: T-0560
title: RequestLoggingMiddleware materializes the whole request body as a string before auth and before the rate limiter — bound the read to the scan cap on all five hosts
status: done
size: S
owner: backend
created: 2026-08-05
updated: 2026-08-05
depends_on: []
blocks: []
stories: []
adrs: [drafts/NNNN-host-request-intake-ceiling]
layers: [backend]
security_touching: true
manual_steps: []
sprint: 15
source: ADR draft `NNNN-host-request-intake-ceiling` **D4**, scoped out of T-0557 by the architect as
  "the one that carries the security value". Graded **FAIL — exploitable, reachable today,
  availability-only (S5)** by a security review that measured the amplification on the production path
---

## Context

`RequestLoggingMiddleware` is registered at `CleansiaStartupBase.cs:166` — **before**
`UseExceptionHandler` (`:168`), `UseRouting` (`:176`), `UseAuthentication` (`:180`) and `UseRateLimiter`
(`:181`). It read the **entire** request body via `EnableBuffering()` + `StreamReader.ReadToEndAsync()`,
and `SafeBody` then **discarded** it above `RedactionScanLimit` (64 KiB).

Measured by the security review on the exact production path:

| body | allocation | amplification |
|---:|---:|---:|
| 1 MiB | 4.26 MB | 14.9x |
| 10 MiB | 42.3 MB | 148x |
| 28.6 MiB (Kestrel ceiling) | **120.9 MB** | **423x** |

**Ten concurrent max-size requests held ~1,068 MiB — ~61 % of a production S1 instance that also runs
four other APIs, SSR and Functions.** Nothing gated it: the rate limiter is 15 lines too late, routing is
too late (so a POST to a **non-existent** path was fully read before the 404), and there are zero Kestrel
limits, IP restrictions or proxy caps anywhere.

**This corrects the ADR draft in the worse direction.** D4 assumed ~2x; the real figure is **~4.03x** —
`ReadToEndAsync` builds a chunked `StringBuilder` (~2x as UTF-16) and `ToString()` then allocates a
second contiguous copy.

### Erratum against the ADR draft: the bound is CHARACTERS, not bytes

ADR draft §D4 says *"read at most `RedactionScanLimit + 1` **bytes**"*. **That is wrong and would change
log output.** `SafeBody`'s decision is `rawBody.Length > RedactionScanLimit`, and `string.Length` counts
**chars**. A byte bound hands `SafeBody` a short string for any multi-byte body, puts it **under** the
cap, and logs the redacted prefix of a body the whole-body read **suppresses** — a behaviour change in
the leaking direction. Implemented as a **character** bound; §Review carries the demonstration.

## Acceptance criteria

- [x] **AC1 — the read is bounded, in characters.** Given a request body larger than
      `RedactionScanLimit`, When the middleware logs it, Then at most `RedactionScanLimit + 1`
      **characters** are read and the allocation is constant in the body size. Measured: **66,560 bytes**
      pulled for a 2 MiB body, on every host.
- [x] **AC2 — log output is byte-identical.** Given any body at, under or over the cap, When it is
      logged, Then the emitted line is exactly what the unbounded read emitted. Both directions pinned:
      an over-cap body's whole line, and an under-cap body's whole **redacted** line.
- [x] **AC3 — all five hosts.** Given `RequestLoggingMiddlewareType` is abstract at
      `CleansiaStartupBase.cs:23` and overridden by five hosts, When the fix lands, Then it lands on all
      five and every test is a `[Theory]` over `RequestLoggingHarness.HostMiddlewareTypes`. **A fix
      landing on one host is worse than none.**
- [x] **AC4 — `request.Body.Position = 0` is retained.** The buffering stream seeks freely within the
      buffered region and downstream binding continues on demand.
- [x] **AC5 — `EnableBuffering` carries explicit bounds.** Given `CleansiaStartupBase.cs:138`, When
      buffering is enabled, Then an explicit threshold and limit are passed, so the temp-file spill is
      not bounded only by free disk.
- [x] **AC6 — the middleware is NOT relocated.** Its pre-auth position is not deliberate
      (`PipelineOrderTests` pins only forwarded-headers-before-logging and auth-before-rate-limiter),
      but below `UseRateLimiter` it would **stop logging every 429**, and below `UseAuthentication` it
      would silently change log content on all five hosts.
- [x] **AC7 — the mutation contrast holds.** Given the fix is reverted to `ReadToEndAsync()`, When
      `Cleansia.Tests/Logging/` runs, Then the byte-count test reddens on **all five hosts** and **every
      other test in the folder stays green**. That contrast is the whole point: output is identical, so
      only a stream-level observable can distinguish the two.

## Out of scope

- **The host-level Kestrel ceiling (ADR draft D1/D2/D3, T-0557).** Still owed, still un-panelled. This
  ticket caps the **amplifier**; the ceiling caps the **multiplier**. `RequestBufferLimitBytes` is set to
  the ADR's proposed 32 MiB so the two agree when D1 lands.
- **Moving or skipping the middleware for any route** (AC6).
- `SaveOrderPhotos`' count cap (D7-a) — a live sibling lane owns that file.

## Implementation notes

**Files touched:**
- `src/Cleansia.Web.{Partner,Admin,Customer,Mobile.Partner,Mobile.Customer}/Middleware/RequestLoggingMiddleware.cs`
  — new `ReadBoundedAsync`; both `ReadRequestBodyAsync` and `ReadResponseBodyAsync` call it; the
  defensive `EnableBuffering()` fallback takes the shared bounds.
- `src/Cleansia.Config/Abstractions/CleansiaStartupBase.cs` — `RequestBufferThresholdBytes` /
  `RequestBufferLimitBytes` consts + the pipeline `EnableBuffering` call.
- `src/Cleansia.Tests/Logging/RequestLogBodyReadBoundTests.cs` (new),
  `src/Cleansia.Tests/Logging/CountingRequestBodyStream.cs` (new),
  `src/Cleansia.Tests/Logging/RequestLoggingHarness.cs` (two optional params).

**The five copies stay in lockstep.** Verified before and after: the only difference across the five
files is one pre-existing comment block in the Customer copy. Applied as one exact string replacement
across all five, asserting exactly one match per file.

**`manual_steps: []`** — no schema change and no DTO/route/command change, so neither `ef-migration` nor
`nswag-regen`.

## Status log
- 2026-08-05 — created and implemented by the backend lane from the security review's FAIL grade.

## Review

### Backend lane, 2026-08-05 — implemented, mutation-verified

**The fix.** `ReadBoundedAsync` fills a `char[RedactionScanLimit + 1]` and stops; `SafeBody`'s verdict is
unchanged because one character past the cap decides `Length > cap` exactly as the whole body would.
Allocation is now constant (~131 KB worst case) instead of ~4.03x the body.

Equivalence is total, and the boundary is the interesting part:
- **≤ cap chars** — the read completes, the string is identical, redaction and truncation unchanged.
- **= cap exactly** — `Length == cap`, not `>`, so still redacted. Pinned.
- **cap + 1 chars** — the read stops at `cap + 1`, `Length > cap`, same suppression string. Pinned. A
  read of `cap` characters (the off-by-one) reports `Length == cap` and logs the redacted prefix, so
  this case is what forces the `+ 1`.

**Why the bound is in characters — demonstrated, not asserted.**
`MultiByteRequestBody_OverTheCharCapButUnderItInBytes_IsStillSuppressed` builds a body that is over the
cap in **chars** and, in its first `cap + 1` **bytes**, under the cap in chars — and the test asserts
both of those fixture properties before it asserts the behaviour, so it cannot pass vacuously. Under a
byte bound that body's prefix reaches the log; under the shipped char bound it is suppressed.

**The response read is bounded too.** Same helper, same equivalence argument. It was not in the review's
scope, but leaving the twin unbounded would have been arbitrary: a large response allocates the same
~4x. **Caveat for the reviewer:** it carries **no byte-count assertion**, because the middleware creates
its own `MemoryStream` for the response internally (`using var responseBody = new MemoryStream()`), so no
counting stream can be injected. Its equivalence is covered by the pre-existing
`RequestLogRedactionScanLimitTests.ResponseBody_OverTheScanLimit_IsSuppressedWholesale`.

**`EnableBuffering` bounds (AC5).** `RequestBufferThresholdBytes = 30 KiB` restates the framework
default (so nothing changes about when a body spills), and `RequestBufferLimitBytes = 32 MiB` is the new
bound. 32 MiB is **above** Kestrel's 30,000,000-byte default deliberately: Kestrel still refuses first,
so nothing that works today begins to fail, and the limit is a backstop for the case where D1 raises the
ceiling or an endpoint escapes it. `PipelineOrderTests`' `IndexOf(src, "EnableBuffering(")` still
matches — checked, that suite is green.

### Why the two obvious tests do not work — and what is asserted instead

- **A log-output assertion cannot work.** Today's code and the fixed code emit the *identical* line; such
  a test stays green through the fix **and** through its reversion.
- **An allocation assertion cannot work.** `GC.GetAllocatedBytesForCurrentThread()` is per-thread and
  `ReadToEndAsync`'s continuation hops thread-pool threads — the review measured **negative** figures.
  The process-wide counter is too noisy under parallel xUnit.
- **So the observable is bytes pulled from the stream.** `CountingRequestBodyStream` forwards
  `CanSeek`/`Position`/`Seek` to an inner `MemoryStream` so the production seekable branch is taken, and
  does **not** reset its counter on seek. The counter is snapshotted **inside the `next` delegate**, not
  after `InvokeAsync` returns, so it measures the request read alone.

**Mutation result (AC7), run:** reverting `ReadRequestBodyAsync` to `ReadToEndAsync()` on all five hosts
→ `Failed: 5, Passed: 145, Total: 150`. The five failures are exactly
`OversizeRequestBody_PullsOnlyTheScanBoundFromTheStream`, one per host; **every other test in
`Cleansia.Tests/Logging/` stayed green**, including this ticket's own 25 equivalence assertions. Restored
and re-verified.

**Measured bound.** A probe run with the slack removed reported **66,560 bytes** pulled on all five hosts
for a 2 MiB body — 65,537 chars rounded up to 65 × 1 KiB reader fills. The asserted ceiling is
`RedactionScanLimit + 4 KiB = 69,632`, i.e. 3,072 bytes of genuine headroom, and the unbounded read
overshoots it by **31.5x**.

### Bonus finding — NOT fixed, and it is a decision, not a cleanup

`context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "Anonymous"` at `:73` runs at a position
where **nothing has populated `context.User`** — `UseAuthentication` is 14 lines later. So the
**request**-line `User:` field is permanently `"Anonymous"` on every host for every authenticated caller.
The **response** line (`:93`) is correct, because it runs after `_next`.

**Left as-is, deliberately.** Any correction changes log content on all five hosts — either to a real
user id or to a different literal — and AC6 forbids the relocation that would fix it properly. The
information is **not lost**: the response line carries the real user id and the two lines are joined by
the same `[{RequestId}]`. Fixing it is a logging-contract decision (it would also put a user id on a
pre-auth line, i.e. on unauthenticated requests, before any claim has been validated). **Routed to the
Architect / PM rather than done silently.** The two equivalence tests in this ticket pin
`User: Anonymous` on the request line, so a future change to it is a deliberate, visible edit.

### Catalog-edit routing

**No catalog edit made.** Test 1 (code sweep): `EnableBuffering` has exactly two call sites in the
solution (`CleansiaStartupBase.cs:151`, and the defensive fallback in each of the five middleware
copies) — verified by a solution-wide grep excluding `bin`/`obj`; there is no second, differing idiom to
generalise from. Test 2 (catalog floor): searched `agents/knowledge/patterns-backend.md`,
`consistency.md` and `security-rules.md` for `RequestLoggingMiddleware`, `EnableBuffering`,
`RedactionScanLimit` and `SafeBody` — `security-rules.md` **S6** already governs this middleware, and it
governs it as a **PII-disclosure** control. This change is **availability (S5)** and does not narrow,
widen or except S6 at any level of generality. The ADR draft already reserves the
`patterns-backend.md` entry for this area (its **D8**, `T1-CI`), so the entry lands with the accepted
ADR under T-0557, not inline here.

### Baselines

| suite | baseline | observed | exit |
|---|---:|---|---|
| unit (`Cleansia.Tests`) | 3072 | **3123 total, 3115 passed, 8 failed** (all 8 = pricing lane) | non-zero |
| unit — `Logging` + `PipelineOrderTests` slice | — | **157 total, 157 passed, 0 failed** | **0** |
| integration | 144 | **144 discovered, 0 executed** — every one failed in its fixture constructor | non-zero |
| host | 120 | **not runnable** — same fixture-construction failure | non-zero |

Build: `dotnet build Cleansia.Api.sln` **executed** (not up-to-date) — `Cleansia.Config` and all five
`Cleansia.Web.*` assemblies recompiled and emitted; **0 errors**, 115 warnings (pre-existing).

**The Docker-backed suites could not be run in this environment, and it is not this change.** Both fail
at *fixture construction*, before any product code loads:
`System.Text.RegularExpressions.RegexMatchTimeoutException` thrown from
`DotNet.Testcontainers.Images.MatchImage.Match` → `DockerImage..ctor` — Testcontainers' own 1-second
image-name regex timing out. Integration: 144/144 failed this way in 5 s. HostTests: 24 ×
`System.TimeoutException` out of `HostTestPostgresFixture.ResetAsync()`, then the same regex timeout on a
focused re-run.

**Cause: the Docker daemon on this machine is saturated by a concurrently-running sibling lane.**
`docker ps` itself exceeded 120 s; the container list showed a sibling lane's `postgres:16` + `ryuk`
running throughout. My runs were **stopped deliberately** so as not to degrade that lane's suite further
(`shared-file-lanes.md` reasoning applied to shared machine resources, not just files). No sibling
container was touched.

**What this leaves owed:** HostTests exercise a real host and would be the only end-to-end witness that
the pipeline still boots and serves with `EnableBuffering(threshold, limit)`. **Re-run
`Cleansia.HostTests` and `Cleansia.IntegrationTests` on a quiet machine before merge.** The risk is low
(the arguments are the framework default threshold plus a limit above Kestrel's own ceiling, so neither
alters a request that succeeds today) but it is unwitnessed here and should not be reported as verified.

**The 8 unit failures are the pricing lane's, not this one's.** All 8 are
`Cleansia.Tests.Features.Orders.ExpressSurchargeDiscountCompositionTests`, an **untracked** file created
at 15:59 today alongside that lane's modified `QuoteOrder.cs` / `OrderFactory.cs`. Nothing in it reaches
`RequestLoggingMiddleware` or `CleansiaStartupBase`. **Not touched** (`shared-file-lanes.md`).

3123 − 3072 = 51 new: **30 are this ticket's** (6 tests x 5 hosts); the other 21 belong to the two live
sibling lanes.

`Cleansia.Tests.Logging` + `PipelineOrderTests` in isolation: **157/157 passed, exit 0.**
