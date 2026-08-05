# ADR-NNNN (DRAFT — number NOT allocated) — The request-intake ceiling is one host-level, config-driven Kestrel limit; the pre-auth body read is bounded separately

- **Status:** `proposed`
- **Date:** 2026-08-05 (drafted)
- **Number:** **not allocated on purpose** — two architects collided on a number this sprint. Highest on
  disk today is **0042**. I am asking for **0044** (or whatever the PM has free after the
  `client-price-display` draft); the file is renamed on allocation.
- **Ticket:** **T-0557** (scoped out of T-0548 `97bb7265` by the backend lane, which declined to decide it)
- **Applies to:** all five `Cleansia.Web.*` hosts via `Cleansia.Config/Abstractions/CleansiaStartupBase.cs`
- **Consumes:** ADR-0003 (partitioned rate limiting — the pipeline order this depends on), ADR-0032
  (a constraining catalog entry names an enforcer and declares a tier)
- **Living doc:** `agents/architecture/decisions/request-intake-limits.md`

> ### ⚠️ Method declaration — read before relying on anything here
> **1. No defense panel has run.** One architect instance wrote this. T-0557 **AC1** requires
> author ≠ challengers ≠ lead as *distinct* instances; §Challenge below is an author-run self-challenge,
> which is weaker by construction. **AC1 is NOT satisfied and this ADR cannot be `accepted` until the
> panel runs.** Every ruling below is a defensible starting position for that panel, not a closed one.
>
> **2. No shell in this invocation** (`Read`/`Write`/`Edit`/`Glob`/`Grep`; no `Bash`). Nothing was
> compiled or executed. Every fact is from reading source at HEAD and is cited at `file:line`. Three
> claims turn on **runtime** behaviour of Kestrel/ASP.NET rather than on repository text — they are marked
> **⚠ not run** and the implementing ticket must pin each with a test rather than inherit it from here.
>
> **3. Two of T-0557's stated premises are stale at HEAD** and the ADR restates them rather than
> inheriting them — see §Context, "What changed under the ticket".

---

## Context

### The gap, restated at HEAD

`MaxRequestBodySize`, `RequestSizeLimit`, `MultipartBodyLengthLimit` and `DisableRequestSizeLimit`
appear **nowhere** in `src/**` (verified at HEAD, zero hits). The effective ceiling on every intake on
all five hosts is therefore **Kestrel's default, 30,000,000 bytes (≈28.6 MiB)** — by accident.

All five hosts run Linux App Service (`deploy/bicep/main.bicep:278`, `DOTNETCORE|10.0`), so Kestrel is
the only knob; there is no IIS/ANCM limit in play. All five `Startup` classes derive from
`CleansiaStartupBase`, so a single registration reaches all of them.

### What changed under the ticket (do not inherit T-0557's §Context verbatim)

**Premise 1 — "three intake paths accept unbounded arrays and none has a count cap."** Two of the three
have been capped since the ticket was filed:

| Path | Per-item byte cap | Count cap at HEAD |
|---|---|---|
| `Features/EmployeeDocuments/SaveMyDocuments.cs` | `DocumentFileValidator` → `BlobFileSize`, 10 MiB decoded | **10** (`:53`, `:70-74`) |
| `Features/Employees/UpdateEmployee.cs` (`Documents`) | `DocumentFileValidator` → `BlobFileSize`, 10 MiB | **10** (`:30`, `:136-138`) |
| `Features/Orders/SaveOrderPhotos.cs` (`Photos`) | `BlobFileSize.HasContentWithinLimit` (`:67`) | **NONE** — `NotEmpty()` only (`:47-49`) |

So the premise is now true of **one** path, not three. **The routing was still right, for a stronger
reason** (below), and the ticket's AC3 is satisfied by this table rather than by the original three.

**Premise 2 — "`SaveMyDocuments` still has no cap today."** False at HEAD; T-0556 landed.

### The reason the number is not derivable from the endpoints — the *strong* form

Not "the arrays are unbounded" but: **the per-request policy the API already states is 4.7× the ceiling
the host already enforces, and has been all along.**

```
per-file cap        = 10 MiB decoded                    (BlobFileSize.cs:8-9)
                    ≈ 13.33 MiB on the wire             (base64 is +33%; the check is len*3/4 <= 10 MiB)
stated per-request  = 10 files × 13.33 MiB ≈ 133 MiB    (the count caps above)
actual ceiling      = 28.6 MiB                          (Kestrel default)
                    → 2 max-size files fit; the 3rd does not
```

A caller who does exactly what the validators say is allowed is refused by the transport. Nobody noticed
because **the refusal is indistinguishable from a generic error** (§D3), so "we have had no reports" is
not evidence that legitimate traffic fits — it is evidence that the failure is illegible.

`SaveMyDocuments.cs:48-53` is the smoking gun for why this must become a *decision* rather than stay a
default: its comment already reasons about *"the host body limit"* as though it were a chosen, known
quantity —

> *"the per-document size cap bounds one item, not the list, and the host body limit buys thousands of
> SMALL ones."*

— and no such chosen quantity exists. An endpoint author is already designing against a number nobody
picked, that is written down nowhere, and that a framework upgrade may move.

### The finding that reframes the whole ticket

**The body is not merely buffered before validation. It is fully materialized as a managed string before
authentication and before rate limiting.**

`CleansiaStartupBase.Configure` pipeline order:

```
:136-140  Request.EnableBuffering()          ← unbounded: 30 KiB in memory, remainder to a TEMP FILE
:146      UseForwardedHeaders()
:166      UseMiddleware(RequestLoggingMiddleware)   ← reads the WHOLE body to a string
:168      UseExceptionHandler(...)                  ← 500 + plain text, for everything
:180      UseAuthentication()
:181      UseRateLimiter()                           ← ADR-0003
:187      UseAuthorization()
```

`RequestLoggingMiddleware.InvokeAsync` awaits `LogRequestAsync` (`:32`) before calling `_next`;
`LogRequestAsync` (`:75`) calls `ReadRequestBodyAsync` (`:129-144`), which does
`new StreamReader(request.Body).ReadToEndAsync()` — **the entire body as a UTF-16 `string`, ≈2× the byte
count, on the Large Object Heap.** `SafeBody` (`:166-179`) then **discards it** for anything over
`RedactionScanLimit` (64 KiB, `:16`), returning `"[suppressed: body too large to redact]"`.

So today, per in-flight request, on an **unauthenticated, un-rate-limited** path:

- up to 28.6 MiB of temp-file/disk (the `FileBufferingReadStream`), **plus**
- up to ≈57 MiB of LOH string that is thrown away unread,

on an App Service plan that is **B2 in dev, S1 (1.75 GB) in prod**, shared by 5 APIs + the SSR site +
the Functions host (`deploy/bicep/modules/appServicePlan.bicep:19-22`). The middleware's own comment
(`:14-16`) shows the class of defect was already recognised — it bounded the redaction **CPU** at 64 KiB
and left the **allocation** unbounded.

This is the single most important fact for the decision, and it is not in the ticket: **a host body
limit alone does not make this a resource-exhaustion control.** It caps the multiplier; the amplifier and
its pre-auth reachability are a separate defect.

## Decision

### D1 — There IS a chosen ceiling, and it lives in `CleansiaStartupBase`, config-driven

Registered once in `CleansiaStartupBase.ConfigureServices` (all five hosts inherit; a sixth cannot opt
out by omission):

```csharp
// Sketch — not code to paste. Key + default belong beside the registration.
public const string MaxRequestBodyBytesKey = "Intake:MaxRequestBodyBytes";
public const long   DefaultMaxRequestBodyBytes = 33_554_432; // 32 MiB — see ADR §D2

services.Configure<KestrelServerOptions>(options =>
    options.Limits.MaxRequestBodySize =
        Configuration.GetValue<long?>(MaxRequestBodyBytesKey) ?? DefaultMaxRequestBodyBytes);
```

**Default in source, override in configuration** — so an environment can be tuned without a release and
a fresh checkout is never unbounded.

**Be honest about what this buys.** It is *not* primarily a security fix — 28.6 MiB was already the same
order of magnitude as the number chosen. It buys three things the accident did not: the number is
**greppable** (an endpoint author can find it), **pinned** (a framework upgrade that moves the default
reddens a test), and **tunable per environment**. The security fix is **D4**.

### D2 — The default is **32 MiB (33,554,432 bytes)**, and here is the derivation

Three constraints, in the order they bind:

1. **Do not break what works today.** 32 MiB > 30,000,000 B, so **no request that succeeds today begins
   to fail.** A ceiling below today's accidental one is a silent regression on a path with no telemetry
   and an illegible failure — that risk is not worth the marginal byte reduction.
2. **Admit the batch the endpoints' own policy implies is legitimate.** Two max-size files on the wire
   are 26.7 MiB; add JSON framing, filenames, descriptions and headers. 32 MiB admits two; nothing
   admits ten without abandoning constraint 3.
3. **Stay survivable on the prod SKU.** Post-D4, the pre-auth cost per in-flight request is 64 KiB of
   heap + up to 32 MiB of temp file. Post-auth (behind the 60/min `interactive` partition), model-binding
   a max-size base64 string still costs ≈2× — ≈67 MiB per concurrent max-size upload on a 1.75 GB shared
   instance. That is the real bound on how large this number may ever get, and it is why option A2
   ("honour the stated 133 MiB policy") is refused.

**The stated 10 × 10 MiB per-request policy is NOT honoured and will not be.** The count cap's job is
bounding **rows and blob uploads**, not bytes (its own comment says so); the byte bound is this ceiling.
They now visibly disagree, and the repair is **legibility** (D3) plus a **client-side aggregate budget**
(D7-b), not a 140 MiB ceiling.

**The number is provisional by construction and says so.** No telemetry exists on rejected bodies,
because the rejection has never been legible. The config key is the instrument for changing it on
evidence; the implementing ticket states where a rejection becomes visible.

### D3 — The failure contract: **413, no body — legible by STATUS, not by error key**

A request over the ceiling is refused by Kestrel with **413** and **no ProblemDetails body**. It is
therefore **not** distinguishable from a validator rejection by *content*: the validator answers **400**
with `errors: { file: "file.size_exceeded" }`, and this answers **413** with nothing. **The status code
is the whole contract.**

Today that difference reaches no user. The shared web interceptor
(`libs/core/services/src/lib/interceptors/http-error.interceptor.ts:14-20`) reads the first value out of
the ProblemDetails `errors` bag; an empty body yields nothing, so it substitutes
`api.common.error_occurred` — *"An error occurred. Please try again."* The mobile clients degrade the
same way through their `ApiResult` mappers (ADR-0011). **A user who uploads too much is told the server
had a problem.**

So the ruling has two halves and neither ships without the other:

- **(a)** The ceiling is set (D1/D2).
- **(b)** Each client's error mapper gains a **status-413 branch ahead of the body read**, resolving to
  the **existing** `file.size_exceeded` key — `api.file.size_exceeded` on web (already present in all
  five locales of all three apps, per T-0548's sweep), and each mobile client's existing size-exceeded
  string. **No new key**, so the `error-contract-parity.spec.ts` guards (which assert against
  `BusinessErrorMessage.cs` directly) are untouched and there is no orphan translation.

  *Why reusing the per-file key is acceptable:* on an upload path the sentence is true and the residual
  imprecision is "one file too big" vs "the batch too big" — which D7-b removes at the point it actually
  helps, before the bytes are sent. On a **non**-upload path a 32 MiB body is not a user, so the only
  reader of a slightly-wrong sentence is an attacker.

**⚠ not run — the implementing ticket must pin this, not assume it.** Which component emits the 413
depends on *when* Kestrel raises it. For a `Content-Length` body the limit is evaluated when the body
read starts (deliberately late, so MVC filters can adjust the feature first) — and in this pipeline the
read starts inside `RequestLoggingMiddleware`, which sits **upstream** of `UseExceptionHandler` (`:168`)
and rethrows (`:58-63`), so the exception should escape to Kestrel and produce a bare 413 rather than
that handler's `500 "An unexpected error occurred."`. **If it instead produces a 500, (b) is wrong as
written and the client branch must key off 500 — which is unacceptable — so the implementing ticket owes
an integration/HostTests case that asserts the observed status and body for an over-ceiling request
before the client work starts.**

### D4 — The pre-auth body read is bounded separately, and the ceiling is not sold as safe without it

`ReadRequestBodyAsync` must read **at most `RedactionScanLimit + 1` bytes**, not the whole body. Above
that, `SafeBody` already returns `"[suppressed: body too large to redact]"`, so **the log line is
byte-identical** — the only change is that the discarded string is no longer allocated. This removes the
≈2× LOH allocation from an unauthenticated, un-rate-limited position for **every** request on **every**
host.

Secondary, same ticket: `EnableBuffering()` (`CleansiaStartupBase.cs:138`) takes an explicit
`bufferThreshold`/`bufferLimit` rather than defaulting to unlimited, so the temp-file spill is bounded
by the same configured ceiling instead of by disk.

**Five copies** of the middleware exist (one per host, `src/Cleansia.Web.*/Middleware/RequestLoggingMiddleware.cs`),
which is itself worth a note: a defect in it is a five-site fix, and this is the second time that has
mattered.

**This is D4's status, stated so it cannot be misread:** the ADR's own justification for 32 MiB
(constraint 3) *assumes* D4. Shipping D1/D2 alone leaves the amplifier in place and would let the change
be reported as a resource-exhaustion fix that it is not. **D4 is a separate ticket and it is the one
that carries the security value.**

### D5 — Per-endpoint `[RequestSizeLimit]` is rejected — and not merely because it is forgettable

The ticket's reason ("an attribute is what the next endpoint forgets") is true and is **not the strong
one**. The strong one is that in *this* pipeline an attribute **cannot function**:
`IHttpMaxRequestBodySizeFeature.MaxRequestBodySize` becomes read-only once the body read has started,
and `RequestLoggingMiddleware` starts the read before any MVC resource filter runs. An attribute would
at best be ignored and at worst throw. **⚠ not run** — but the conclusion is robust either way: it is
not the mechanism.

**The rule this fixes into the catalog:** the ceiling is a **host** property. If a future endpoint
genuinely needs a *higher* one, the prerequisite is D4 plus moving or skipping the body read for that
path — an ADR, not an attribute. A *lower* per-endpoint bound is a **validator** concern, because a
validator can answer 400 with an error key and the transport cannot.

### D6 — The guard (AC5): a sixth host cannot omit it

`WebSdkContentGlobTests` is the right *model* and the wrong *shape* — the limit is not a csproj
property. Three assertions in `src/Cleansia.Tests/`:

1. **Discovery + non-vacuity.** Walk `*.csproj` from the solution root (excluding `bin`/`obj`) for
   `Sdk="Microsoft.NET.Sdk.Web"`; assert **≥ 5** found, so a broken walk fails loudly instead of passing
   vacuously. This is exactly `WebSdkContentGlobTests:76-79`.
   **Do NOT use `WireSurface.HostAssemblies()` for discovery** — it derives from
   `RequestLoggingHarness.AllHostMiddleware`, a hand-maintained roster, which by construction cannot
   notice a new host.
2. **Every discovered host's `Startup` derives from `CleansiaStartupBase`.** A sixth host that rolls its
   own startup is the omission mode, and this is what catches it.
3. **`CleansiaStartupBase.ConfigureServices` actually sets the limit**, asserted through the resolved
   `IOptions<KestrelServerOptions>` — both the source default and a configuration override. Deleting the
   registration must redden this. A test that reads the constant and not the registration is not an
   enforcer.

### D7 — What the array paths owe, and where

- **(a) `SaveOrderPhotos` owes a count cap** — its own ticket, mirroring `SaveMyDocuments.cs:53` /
  `UpdateEmployee.cs:30` (10, with the same `.When(...)` guard so per-item rules do not decode a list
  already refused, and `BusinessErrorMessage.FileCountExceeded`, which exists and is translated). It is
  an **answer-correctness** control that bounds rows and blob uploads — **not** a resource control, since
  it runs after buffering. Say that in the ticket, or the next reader treats it as one.
- **(b) The batching clients owe an aggregate staging budget** — the user-facing repair. Web stages an
  arbitrary number of documents/photos into one request
  (`profile-documents.facade.ts:209-220`, `order-photos.facade.ts:43-48`) with only a per-file check
  (`cleansia-file.component.ts:36`, 10 MB). It should refuse the *batch* before upload, against the same
  ceiling. iOS and Android already send **one item per request** (`DocumentsSectionViewModel.swift:46-56`,
  `PartnerOrderClient.swift:212-222`) and are unaffected.
- **(c)** `UploadEmployeeDocument` / `UploadNewDocumentVersion` remain dead code (T-0548 follow-up 3),
  unchanged by this ADR — but if either is ever wired, it joins `Base64UploadIntakeRosterTests`' roster.

### D8 — The catalog entry (AC7)

One entry in `agents/knowledge/patterns-backend.md`: *"An intake bound is a host property; a per-request
count/size answer is a validator property; they are different guarantees and a change owes both."*
**Enforced by:** the D6 test trio — **`T1-CI`**, scope = the host registration and host discovery (it does
not assert that any particular endpoint has a count cap). It is routed to the Architect by the same
ADR-0033 test 2 that routed this ticket, and lands with this ADR rather than inline.

## Alternatives considered

**A1 — "No limit, and here is why" (keep the default).**
Taken seriously; **rejected**, and not on the number. 28.6 MiB is a defensible order of magnitude — the
chosen 32 MiB is *larger*. It is rejected because it is not a **decision**: it is undocumented (an
endpoint author already reasons about "the host body limit" that does not exist), unpinned (a framework
default can move with no test noticing), and untunable per environment. D1 ratifies roughly the status
quo in a place that is greppable, testable and configurable. **What A1 gets right, and this ADR
concedes:** the ceiling is not where the resource-exhaustion risk lives. That is D4.

**A2 — Set the ceiling to honour the stated per-request policy (~140 MiB).**
Rejected on §D2 constraint 3. Pre-D4 it is an unauthenticated ≈280 MiB LOH allocation on a 1.75 GB
shared instance; post-D4 it is still ≈280 MiB of heap per concurrent max-size upload at model-bind time.
The correct resolution of the policy/ceiling contradiction is to make the ceiling legible and let the
clients batch within it, not to raise the ceiling to a number the plan cannot hold.

**A3 — Per-endpoint `[RequestSizeLimit]` as the primary mechanism.** §D5 — inoperative here.

**A4 — Enforce the ceiling in custom middleware (read `Content-Length` at the top of the pipeline and
return a ProblemDetails 413 with an error key).**
The strongest alternative, and **partially conceded in spirit by D3(b)**. It buys a *legible body*, not
just a legible status. Rejected as the primary mechanism because (i) it is a second number that can
drift from Kestrel's, requiring its own guard; (ii) it only binds callers that send an honest
`Content-Length` — a chunked or lying caller still needs Kestrel, so it *adds* a layer rather than
replacing one; (iii) the same user outcome is reachable for far less by mapping 413 in the three error
mappers that already exist. **Revisit if** the implementing ticket's ⚠ test shows Kestrel's 413 arrives
as a 500 through `UseExceptionHandler`, in which case A4 becomes the cheapest way to get a truthful
status at all.

**A5 — Aggregate byte-budget validators on the array paths, sized just below the ceiling.**
Rejected as ceremony. It converts *some* 413s into 400s — only for bodies between the budget and the
ceiling — while still running after full buffering, so it buys no resource protection and a narrow
legibility win that D3(b) covers completely and D7-b covers better (before the upload, not after).

**A6 — Per-host ceilings (mobile hosts lower than web).**
Rejected for now, and the seam is preserved: the value is read from configuration per host, so a host
that later needs a different number sets its own key with no code change. Choosing five numbers today
would be five guesses instead of one, and the intake surface does not differ by host — the same
`SaveMyDocuments` is reachable on `Web.Partner` **and** `Web.Mobile.Partner`
(`Base64UploadIntakeRosterTests.cs:31-43`).

## Consequences

- Every host gains a stated, greppable, per-environment ceiling. Nothing that works today stops working.
- A user who exceeds it sees a size message instead of "An error occurred" — **once D3(b) ships**. Until
  then the ceiling is invisible to users exactly as the accident was.
- The API's stated per-request policy (10 files) and its byte ceiling openly disagree, and the ADR says
  so rather than pretending a count cap is a byte cap. D7-b is where that disagreement stops hurting.
- **D4 is where the security value is.** If only D1/D2 ships, the amplifier is untouched and the ticket
  must not be closed as a resource-exhaustion fix.
- `security_touching: true` on T-0557 remains correct — but the security-relevant half is D4, which
  belongs to its own ticket. Security should review **that** one.

## How a reviewer verifies compliance

1. `grep -r "MaxRequestBodySize" src/` returns exactly the `CleansiaStartupBase` registration and its
   tests — no per-endpoint attribute anywhere.
2. The D6 trio exists and each assertion fails on its own mutation: delete the registration (3 reddens);
   add a sixth Web SDK host with a bare `Startup` (2 reddens); break the csproj walk (1 reddens on the
   `>= 5` floor, not silently).
3. An over-ceiling request against a real host returns the status the ⚠ test pinned, and each of the
   three generated clients surfaces the size message rather than the generic fallback.
4. `ReadRequestBodyAsync` in **all five** middleware copies reads a bounded number of bytes; a body over
   `RedactionScanLimit` still logs `[suppressed: body too large to redact]`.

## Challenge (author-run — NOT a panel; an independent round is owed)

**C-1 — "32 MiB is a made-up round number wearing a derivation."**
Partly conceded, and the ADR now says so in D2's closing paragraph. The derivation genuinely fixes a
*floor* (≥ today's ceiling, non-negotiable, evidence-based) and a *ceiling* (the S1 heap budget). Between
those, "two max-size files plus framing" selects 32 MiB and the alternative candidates in that band
(28, 40, 48) differ by no argument I can defend. The honest posture is the config key plus the statement
that it is provisional — not a fake precision.

**C-2 — "You are shipping a limit whose user-visible half (D3-b) is in someone else's ticket. That is
how the avatar gap happened."**
Sustained, and the ADR is amended to bind them: **D3 explicitly states that (a) and (b) ship together.**
The PM should file them as one ticket or as two with a hard `depends_on`, because a ceiling with no
legible failure is measurably worse than no ceiling — it converts a rare accident into a policy while
leaving the user with "An error occurred."

**C-3 — "D4 belongs in this ADR at all? It is a different defect."**
Not conceded. D2 constraint 3 *depends* on D4; without it the chosen number is not defensible on the
prod SKU. Recording it elsewhere would leave the ADR's own arithmetic resting on an unstated assumption.
It is scoped as a **named dependency with its own ticket**, not implemented here.

**C-4 — "You rejected A4 partly on a runtime claim you did not run."**
Sustained as a caveat, and handled: A4 carries an explicit **revisit trigger** keyed to the ⚠ test's
outcome, and D3 requires that test before any client work. If the 413 does not survive the pipeline, A4
is the answer and this ADR says so in advance rather than being re-litigated.

**C-5 — "AC2 asks for the largest legitimate request 'with the endpoint and the evidence'. You gave a
policy maximum, not an observed one."**
Sustained; there is no observed maximum to give, because success/failure at the boundary has never been
distinguishable in logs. The ADR states the policy maximum (133 MiB), the actual ceiling (28.6 MiB), the
gap, and the reason no observation exists. **An independent challenger should push on whether that is
good enough or whether the ceiling should wait for one release of telemetry** — my position is that it
should not wait, because D1 changes no behaviour for any caller and D3(b) is what creates the telemetry.

**Not self-challenged; start here:** whether `context.Response.Body = new MemoryStream()`
(`RequestLoggingMiddleware:35-36`) buffers large *responses* — invoice PDFs, GDPR exports — into memory
on the same shared plan, which is the mirror image of this defect on the egress side and is out of scope
here; and whether the Functions host (`Cleansia.Functions`, not a `Cleansia.Web.*` host and not covered
by `CleansiaStartupBase`) has an intake surface that needs the same ruling.

## Verdict

**Not adjudicated.** No independent challenger has run and no lead has ruled, so **T-0557 AC1 is not
met** and this ADR is not `accepted`. The rulings above are the author's position going into that panel.
