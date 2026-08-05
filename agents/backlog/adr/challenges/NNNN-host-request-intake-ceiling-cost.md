# ADR-NNNN draft ("the request-intake ceiling is one host-level, config-driven Kestrel limit") — Challenger pass

**Mode:** challenger. Distinct instance from the author; did not write the draft, T-0557, or T-0548.
Target: `agents/backlog/adr/drafts/NNNN-host-request-intake-ceiling.md` @ `fe6db0ca` and its living doc
`agents/architecture/decisions/request-intake-limits.md`.

**Gate 0: REFUTED by default.** Every claim below cites a `file:line` I opened in the working tree on
2026-08-05. No Bash — nothing compiled, nothing executed, no HTTP request issued. Where a claim turns on
runtime behaviour I say so rather than assert it. No git write, no ADR edit, nothing outside
`agents/backlog/adr/challenges/`.

---

## Headline

**The evidence base is excellent and I could not break most of it.** I re-verified twenty of the draft's
citations line by line and every one held — the zero-hit grep, the pipeline order, the count-cap table,
the `WireSurface.HostAssemblies()` warning, the `WebSdkContentGlobTests` model, the bicep SKU. §Found
sound lists what I attacked and failed to move. This is the best-evidenced draft I have reviewed this
sprint.

What does not survive is the layer above the evidence — **what the decision is for, and whether the
number and the failure contract can carry the weight put on them**:

1. **D3's central claim is falsifiable by reading, and it is false.** "413, no body — the status code is
   the whole contract" has a **second, deterministic answer** on this pipeline that no test needs to
   discover: `ShouldSkipLogging` short-circuits before the body read, so on those paths the read happens
   *inside* `UseExceptionHandler`'s scope and produces the 500 the draft itself calls unacceptable.
   `/payment/webhook` is on that list. (**CH-R1**)
2. **The nominated fallback does not close the hole it is nominated for.** A4 is rejected partly because
   *"a chunked or lying caller still needs Kestrel"* and then named as "the answer" if Kestrel's status
   is unusable — in which case the chunked caller still gets the unusable status. (**CH-R2**)
3. **Tunability is the whole remaining justification, and the tuning surface is unvalidated.** The D1
   sketch clamps nothing; D6 asserts that an override *works*, never that a bad one is *refused*.
   (**CH-R3**)
4. **A1 was rejected in its weak form only.** The strong rival — *pin the framework default with the
   same D6 test, comment it, ship D4* — delivers "greppable" and "pinned" at zero behavioural risk and
   was never met. One real benefit of D1 survives it, and the draft never names that benefit. (**CH-R4**)
5. **"No new key" is a constraint that does not exist.** The parity guards orphan-check a hand-maintained
   roster, not the locale's `api.*` set. The draft forecloses the truthful message on a false premise —
   and the key it picks instead is wrong for the case D7-b describes. (**CH-R5**)
6. **D2 constraint 3's memory budget is half-measured.** The same middleware buffers every **response**
   into a `MemoryStream` and then materializes it as a UTF-16 string, on paths that return 10 MiB
   documents, on all five hosts. D4 does not touch it, and the draft files it out of scope while using
   the budget as the binding constraint that rejects A2. (**CH-R6**)
7. Two smaller ones: an Azure-infrastructure inference presented as a repository fact (**CH-R7**), and
   the policy/ceiling contradiction documented rather than resolved, with the half that reaches users
   left unbound by the draft's own "ship together" rule (**CH-R8**).

**CH-R1, CH-R3 and CH-R6 I consider blocking.** CH-R4 I do not — but the ADR is oversold until it
answers it.

---

## CH-R1 — D3's "the status code is the whole contract" is false on this pipeline, and it is false *by reading*, not by running. There are two answers, deterministic by path, and one of them is the 500 the draft calls unacceptable. **BLOCKING**

**The hole.** D3 states one failure contract for the whole platform and marks the *component* that emits
it as ⚠ not run. The reasoning is right as far as it goes: `RequestLoggingMiddleware` is registered at
`CleansiaStartupBase.cs:166`, `UseExceptionHandler` at `:168`, so the logging middleware is **upstream**;
`LogRequestAsync` is awaited at `RequestLoggingMiddleware.cs:32` **before** the `try` block that wraps
`_next` opens at `:38`; so an exception during the read escapes past the handler entirely.

**But the middleware does not always read.** Its first act is:

```csharp
// RequestLoggingMiddleware.cs:23-27
if (ShouldSkipLogging(context.Request.Path))
{
    await _next(context);
    return;
}
```

and `ShouldSkipLogging` (`:260-271`) matches `/health`, `/swagger`, `.js`, `.css`, `.map`, `/hangfire`
and **`/payment/webhook`**. On any of those the body read never happens here — it happens downstream, in
model binding, which is **inside** `UseExceptionHandler`'s scope. `ExceptionHandlerMiddleware` catches
the `BadHttpRequestException` and runs the registered handler:

```csharp
// CleansiaStartupBase.cs:168-175
app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
{
    context.Response.StatusCode = 500;
    await context.Response.WriteAsync("An unexpected error occurred.");
}));
```

Of those seven skipped patterns exactly one takes a request body in production: **`/payment/webhook`** —
the Stripe endpoint, on two hosts, carrying two separate signing secrets (`main.bicep:268-275`). An
over-ceiling webhook therefore answers **500 + plain text**, not 413.

**Why it matters.** D3 says, of the 500 outcome: *"the client branch must key off 500 — which is
unacceptable."* The draft treats that as a risk to be resolved by one integration test. It is not one
risk; it is **two code paths with two different answers**, one of which is already knowable at HEAD and
is the unacceptable one. A ⚠ marker on "which component emits the 413" hides a design fact that is not
uncertain at all: **the failure contract is a function of `ShouldSkipLogging`'s path list**, i.e. of a
`Contains`-based denylist written for a completely unrelated reason (log noise), which nobody will
remember governs the intake failure contract.

Two follow-on consequences the draft does not price:

- **The over-ceiling request is not logged at all** on the 413 path. The `catch (Exception)` at `:58-63`
  wraps `_next` only; an exception from `LogRequestAsync` at `:32` is outside it, so `LogError` never
  runs. That is consistent with D2's *"no telemetry exists on rejected bodies"* — but it means D3(b)'s
  ⚠-test must assert a **server-side** observation too, or the ceiling stays unmeasurable after it is
  chosen, which defeats D2's stated instrument-for-changing-it-on-evidence.
- Stripe retries a webhook it cannot deliver. A 500 on an over-ceiling webhook is retried; a 413 is a
  permanent failure. The two statuses have *different operational semantics* for the one caller in this
  system that retries, and the draft's single contract flattens them.

**What I want changed.**

1. D3 states **both** paths and which one each surface gets, keyed on `ShouldSkipLogging`
   (`RequestLoggingMiddleware.cs:260-271`) — not one contract with one ⚠.
2. The implementing ticket's pinning test covers **both classes** (a logged path and a skipped path),
   because they exercise different middleware.
3. If the panel wants one contract rather than two, that is a *decision* — and the cheapest form is not
   A4 but moving the ceiling refusal **above** `ShouldSkipLogging`, which is CH-R2's real content.

*Blocking?* **Yes.** D3(a) and D3(b) are bound to ship together by the draft's own C-2, and D3(b) is
specified against a contract that is wrong for a live endpoint.

---

## CH-R2 — A4 is nominated as the fallback for exactly the failure it is rejected for being unable to cover. As written, the fallback does not work.

**The hole.** A4's rejection (point ii): *"it only binds callers that send an honest `Content-Length` —
a chunked or lying caller still needs Kestrel, so it adds a layer rather than replacing one."* Then its
revisit trigger: *"Revisit if the implementing ticket's ⚠ test shows Kestrel's 413 arrives as a 500
through `UseExceptionHandler`, in which case A4 becomes the cheapest way to get a truthful status at
all."*

Compose the two. If Kestrel's refusal surfaces as a 500, then for the **chunked / no-`Content-Length`**
caller — the population A4 admits it cannot bind — the answer is still a 500. A4 does not become "the
answer"; it becomes "the answer for the subset that was never the problem." The draft's own C-4 records
this as *"sustained as a caveat, and handled"*, and it is not handled: the caveat and the remedy are
the same sentence pointing in opposite directions.

**Two things the draft never states about A4, both of which change its cost.**

- **Placement is forced and it is not where you would put it.** To be reachable at all the check must
  sit **above** `RequestLoggingMiddleware` (`CleansiaStartupBase.cs:166`), because that is where the read
  starts — which puts it above `UseForwardedHeaders` (`:146`), `UseRouting` (`:176`),
  `UseAuthentication` (`:180`) and `UseRateLimiter` (`:181`). It is therefore an **unauthenticated,
  un-rate-limited responder** writing a ProblemDetails body, ahead of the ADR-0003 band. That is fine on
  cost (it reads a header) but it is a new pre-auth response surface and ADR-0003's pipeline-order
  ruling is a `Consumes:` of this ADR — it deserves a sentence, not silence.
- **It is also the fix for CH-R1**, and that is its strongest argument, which the draft does not make. A
  `Content-Length` check above `ShouldSkipLogging` gives **one** contract on **both** path classes,
  including the webhook. That is a materially better reason to adopt A4 than "the 413 came back as a
  500."

**What I want changed.** A4's revisit trigger is rewritten to say what it actually buys and what it
leaves open:

> If the ⚠ test shows a 500, A4 gives a truthful status **for `Content-Length` callers only**; the
> chunked case still needs whatever Kestrel emits, so adopting A4 does not by itself deliver one
> contract. A4 *does* unify the `ShouldSkipLogging` split (CH-R1) if placed above
> `CleansiaStartupBase.cs:166`, and that — not the 500 — is the reason to reconsider it.

*Blocking?* No — but the draft currently claims a pre-decided fallback and does not have one.

---

## CH-R3 — Tunability is the only benefit D1 has that the cheap rival does not, and the tuning surface is unclamped, unvalidated and untested against a bad value. **BLOCKING**

**The hole.** D1's sketch:

```csharp
public const long DefaultMaxRequestBodyBytes = 33_554_432;
services.Configure<KestrelServerOptions>(options =>
    options.Limits.MaxRequestBodySize =
        Configuration.GetValue<long?>(MaxRequestBodyBytesKey) ?? DefaultMaxRequestBodyBytes);
```

No floor. No ceiling. No rejection of `0`, of a negative, of `999999999999`. D6 assertion 3 asks for
*"both the source default and a configuration override"* — i.e. it proves the happy path and says
nothing about a hostile or fat-fingered one.

**Why it matters.** The draft concedes D1 *"is not primarily a security fix"* and that its value is
greppability, pinning and **tunability**. CH-R4 shows the first two are obtainable without it. So the
config key is carrying the decision — and it is a new externally-settable value on **five** production
App Services, resolved from App Service application settings, with these unhandled cases:

| Configured value | Behaviour | Consequence |
|---|---|---|
| absent / typo'd env-var name (`Intake__…` vs `Intake:…`) | silently falls to the source default | the "tuned" environment is not tuned, and nothing says so |
| `0` | `MaxRequestBodySize = 0` | **every** request with a body is refused, on that host, with the illegible failure of §D3 |
| negative | `ArgumentOutOfRangeException` on the options callback | host fails to start — loud, acceptable, but unstated |
| non-numeric | `Configuration.GetValue<long?>` throws `InvalidOperationException` | host fails to start — loud, acceptable, but unstated |
| `536_870_912` (someone "fixing" an upload complaint) | accepted | a one-app-setting amplifier on the exact allocation path D4 exists to bound, with no review and no test |

The last row is the one that matters, because it is *reasonable behaviour by a well-meaning operator*
responding to the user complaint D3 exists to create. The ADR hands them a dial with no stop and tells
them it is the instrument for changing the number on evidence.

There is precedent in this repository for the opposite: `RateLimitPolicies.ConfigureForwardedHeaders`
(`CleansiaStartupBase.cs:120-121`) is described in the comment as carrying a *"trust boundary +
fail-closed guard"* configured at registration, and `GuardSwaggerExposure` (`:152-155`) **refuses to
boot** on a prod-shaped misconfiguration. This platform clamps its config surfaces; the sketch does not.

**What I want changed.**

1. The registration clamps: a stated `MinMaxRequestBodyBytes` (the smallest legitimate intake — one
   base64 document plus framing) and a stated `AbsoluteMaxRequestBodyBytes` (the D2 constraint-3
   number), with an out-of-range configured value either clamped-with-a-warning or refused at boot in
   the `GuardSwaggerExposure` style. Say which and why.
2. D6 gains a fourth assertion: **an out-of-range override is rejected** (or clamped) — not merely that
   an in-range one is honoured. A tunable with no test on the tuning is the same defect class as an
   `Enforced by:` label on a gate that cannot fail.
3. D2's "provisional by construction" paragraph names the clamp as what makes provisionality safe.

*Blocking?* **Yes.** The decision's stated value is the dial; the dial has no stops and no test.

---

## CH-R4 — A1 was rejected in its weak form. The strong rival — *pin the default, comment it, ship D4* — is never met, and it takes two of D1's three claimed benefits.

**The rival, stated properly, because the draft asks for it and the task asks me to argue it.**

Do not set `MaxRequestBodySize`. Instead:

- **Ship D4 unchanged.** It carries the security value; the draft says so itself (*"D4 is where the
  security value is"*).
- **Ship D6 unchanged, with assertion 3 inverted**: resolve `IOptions<KestrelServerOptions>` and assert
  `Limits.MaxRequestBodySize == 30_000_000`, with an XML comment naming it as the framework default this
  platform has *chosen to keep*. The property has a value whether or not this codebase writes one, so
  the assertion is available today. Assertions 1 and 2 (the `>= 5` csproj walk, the
  `CleansiaStartupBase` derivation) are unchanged and still catch the sixth host.
- **Ship D8's catalog entry unchanged** — *"an intake bound is a host property; a per-request
  count/size answer is a validator property"* — pointing at the test as the place the number is written
  down.

Score it against D1's own three claims:

| D1's claim | Rival | Verdict |
|---|---|---|
| **greppable** — "an endpoint author can find it" | `rg MaxRequestBodySize src/` returns the test and its comment | **delivered** |
| **pinned** — "a framework upgrade that moves the default reddens a test" | the assertion is *exactly* that test, and it reddens on the same event | **delivered** |
| **tunable per environment** | not delivered | **lost** |

And "tunable" is unevidenced *by the draft's own reasoning*: A6 rejects per-host numbers because
*"choosing five numbers today would be five guesses instead of one"* and *"the intake surface does not
differ by host"*. The same argument applies to per-*environment*: there is no environment whose intake
surface differs, and none is named. A config key nobody sets is a key that drifts (dev sets it, prod
does not, and the "one number" quietly becomes two) — and it is CH-R3's unclamped dial.

**The one real benefit D1 has over the rival, which the draft never states.** With D1, the number does
not *move* on a framework upgrade — the test reddens *and* behaviour is unchanged. With the rival the
test reddens *after* behaviour has already changed on that build. That is behaviour-stability-across-
upgrades, and it is a genuine, if modest, argument for D1 that A1's rejection paragraph
("undocumented, unpinned, untunable") does not contain.

**Why this matters procedurally.** A1 as written is *"No limit, and here is why"* — the do-nothing that
also does no documentation. That is a strawman of the strongest do-nothing. `deliberation.md`'s bar is
that *"a decision with a real trade-off must have its alternatives and why-not in the record"*; the
alternative that takes two of three benefits at zero risk is not in the record.

**What I want changed.** A1 is split into **A1a** ("keep the default, document nothing" — rejected as
written) and **A1b** ("keep the default, pin it with the D6 trio, comment it, ship D4"), and A1b is
answered on its merits. My own position, having argued it: **A1b loses, but only on
behaviour-stability-across-upgrades plus the clamped-dial version of tunability from CH-R3.** State
that and D1 stands. Leave it unstated and D1 is a platform-wide config surface justified by two benefits
it does not uniquely provide.

*Blocking?* No. But the ADR is oversold until §Alternatives answers A1b.

---

## CH-R5 — "No new key" is a constraint that does not exist, and the key chosen instead is wrong for the case D7-b describes.

**The hole.** D3(b): *"resolving to the **existing** `file.size_exceeded` key … **No new key**, so the
`error-contract-parity.spec.ts` guards (which assert against `BusinessErrorMessage.cs` directly) are
untouched and there is no orphan translation."*

The guards do not work that way. `apps/cleansia.app/src/app/i18n/error-contract-parity.spec.ts`:

- the orphan check (`:233-239`) iterates **`CUSTOMER_SURFACE_ERROR_KEYS`** — a hand-maintained array at
  `:92-228` — and asserts each is a `BusinessErrorMessage` value. It does **not** iterate the locale
  file's `api.*` set;
- `apiKeySet` (`:69-72`) is used only by AC2 (`:249-264`), which asserts the **five locales agree with
  each other** — nothing about the backend;
- AC1/AC3 (`:241-247`) and AC2's translation check (`:266-275`) again iterate the roster.

So a client-only key — say `api.file.request_too_large` — added to all five locales of all three apps
and **not** added to `CUSTOMER_SURFACE_ERROR_KEYS` reddens **nothing**. Fifteen JSON edits. The stated
constraint that forced the reuse is not a real constraint.

**And the reused key is wrong for the case that matters.** At HEAD:

```jsonc
// apps/cleansia.app/src/assets/i18n/en.json:1525-1530
"file": {
  "size_exceeded":   "The file is too large.",
  "count_exceeded":  "You've attached too many files.",
  …
}
```

D7-b's own scenario is the web partner staging *N legal documents into one request*
(`profile-documents.facade.ts:196-220` — `staged.map(...)` into a single `SaveMyDocumentsCommand`, with
only a per-file 10 MB check at `cleansia-file.component.ts`). Every file is under 10 MiB; the **batch**
is over the ceiling. The message the user gets is *"The file is too large."* — which instructs them to
shrink a file that is fine, when the correct instruction is to send fewer. That is not "residual
imprecision"; it is advice that cannot succeed.

The draft's defense — *"on a non-upload path a 32 MiB body is not a user, so the only reader of a
slightly-wrong sentence is an attacker"* — is sound for the non-upload path and does not touch the
upload-batch path, which is the one D7-b exists for.

**What I want changed.**

1. Delete the false premise. The guards' shape is `:92-228` + `:233-239`, and a client-only key is free.
2. D3(b) resolves 413 to a **new, truthful key** (`api.file.request_too_large` — *"Your upload is too
   large. Send fewer files, or smaller ones."*) in all five locales of all three apps, plus the mobile
   equivalents. If the panel prefers to reuse, `file.count_exceeded` is closer for the batch case than
   `file.size_exceeded` and is already present in all fifteen locale files — but neither is right for
   both causes, which is why a new key is the honest answer.
3. Note in the ADR that the roster arrays are per-app and hand-maintained, so a *backend*-originated key
   is the case that needs the roster edit — the reverse of what D3(b) assumed.

*Blocking?* No, but it is the user-facing half of the decision, D3 binds it to ship with D1/D2, and it
is currently specified to say the wrong thing to the only user who will ever read it.

---

## CH-R6 — D2 constraint 3 is the binding constraint that rejects A2, and it measures half the instance. The same middleware buffers every RESPONSE into memory and then materializes it as a UTF-16 string — on the document/PDF routes, on all five hosts. **BLOCKING**

**The hole.** D2 constraint 3: *"Post-D4, the pre-auth cost per in-flight request is 64 KiB of heap +
up to 32 MiB of temp file. … That is the real bound on how large this number may ever get, and it is why
option A2 … is refused."*

Fourteen lines below the code D4 fixes, in the same class:

```csharp
// RequestLoggingMiddleware.cs:34-44
var originalBodyStream = context.Response.Body;
using var responseBody = new MemoryStream();          // ← whole response, in managed memory
context.Response.Body = responseBody;
try {
    await _next(context);
    …
    await LogResponseAsync(context, requestId, stopwatch.ElapsedMilliseconds);
    await responseBody.CopyToAsync(originalBodyStream);

// :146-156
private static async Task<string> ReadResponseBodyAsync(HttpResponse response) {
    …
    using var reader = new StreamReader(response.Body, Encoding.UTF8, leaveOpen: true);
    var body = await reader.ReadToEndAsync();          // ← whole response again, as UTF-16
```

`SafeBody` (`:166-179`) then discards it above 64 KiB, exactly as on the request side. **D4 fixes the
request half and leaves the response half untouched**, and the response half is not hypothetical:

```
src/Cleansia.Web.Admin/Controllers/AdminEmployeeDocumentController.cs:92   return File(…)   ← up to 10 MiB, a stored document
src/Cleansia.Web.Admin/Controllers/AdminInvoiceController.cs:108           return File(…, "application/pdf", …)
src/Cleansia.Web.Partner/Controllers/EmployeePayrollController.cs:96       return File(…, "application/pdf", …)
src/Cleansia.Web.Partner/Controllers/EmployeeController.cs:125             return File(result.Value.FileBytes, …)
src/Cleansia.Web.Customer/Controllers/OrderController.cs:128               return File(result.Value!.PdfBytes, …)
… and the two Mobile hosts, same shapes
```

None of those routes is in `ShouldSkipLogging` (`:260-271`). So a 10 MiB document download costs, per
in-flight request, roughly: 10 MiB `MemoryStream` + ~20 MiB UTF-16 string (binary decoded as UTF-8 into
replacement characters, then thrown away) + the copy — on the same S1 / 1.75 GB instance shared by five
APIs, the SSR site and the Functions host (`appServicePlan.bicep:19-22`, which the draft cites
correctly).

**Why it matters.** The draft's rejection of A2 (~140 MiB) rests entirely on constraint 3 being *the
real bound*. If the platform already spends ~30 MiB of managed memory on an ordinary authenticated
document download and the ADR's budget does not include it, then the budget is not the budget, and the
sentence *"that is the real bound on how large this number may ever get"* is not supported by the
measurement offered. The conclusion (do not raise to 140 MiB) is very likely still right — but it is
right for a reason the ADR has not shown, and the number 32 was selected inside a band whose upper edge
the ADR mis-measured.

The draft's §"Not self-challenged; start here" names the response buffering and declares it *"out of
scope here"*. **It cannot be out of scope**: it is the second term of the arithmetic that selects the
decision's only number, in the same file, in the same middleware, uncovered by D4.

**What I want changed.**

1. Constraint 3 states the **whole** per-request footprint, ingress and egress, and cites
   `RequestLoggingMiddleware.cs:34-36` and `:146-156` beside the ingress citations. If the number still
   comes out at 32 MiB, say so with the corrected arithmetic — that is a stronger derivation, not a
   weaker one.
2. D4 gains the symmetric limb, or the ADR states explicitly that D4 halves an amplifier it knows to be
   two-sided and names the ticket for the other half. The draft's own note that **five copies** of this
   middleware exist makes the cost of doing them separately concrete.
3. The living doc records that `ShouldSkipLogging` governs the egress buffering too, so a future reader
   knows why a PDF route is buffered and `/health` is not.

*Blocking?* **Yes.** Not because 32 MiB is wrong, but because the one constraint that bounds it is
computed from half the instance, and the missing half is in the file the ADR is already editing.

---

## CH-R7 — "Kestrel is the only knob" is an inference about Azure infrastructure presented as a repository fact, un-⚠'d, while three softer runtime claims are marked.

**The hole.** §Context: *"All five hosts run Linux App Service (`deploy/bicep/main.bicep:278`,
`DOTNETCORE|10.0`), so Kestrel is the only knob; there is no IIS/ANCM limit in play."* The citation is
exact — `main.bicep:278` is `var apiLinuxFxVersion = 'DOTNETCORE|10.0'`. The *inference* from it is
about the behaviour of Azure App Service's Linux front-end proxy, which is not in this repository and
which the draft did not test.

The IIS/ANCM half is right (there is no IIS on Linux). The "only knob" half is a claim about a managed
front end. It is stated flatly while three genuinely softer claims — the 413 emitter, the
`[RequestSizeLimit]` behaviour, the feature read-only-ness — carry **⚠ not run**.

**Why it matters.** Consequences differ by direction. If a front-end cap sits **below** 32 MiB, D2
constraint 1 still holds (nothing that works stops working — it is a raise), but constraint 2's *"32 MiB
admits two max-size files"* is false in production while true against a local host, and the D6 trio —
which asserts the *configured option*, not the *observed ceiling* — cannot tell the difference. A green
`Cleansia.Tests` run would certify a ceiling the platform does not enforce.

**What I want changed.** Mark it ⚠ like its siblings, and make the implementing ticket's pinning
evidence an **end-to-end request against a deployed host** (DEV is live), not only a `Cleansia.HostTests`
case against a locally-hosted Kestrel. That is one curl in the ticket's evidence block and it converts
the ADR's most load-bearing environmental assumption from inference to observation.

*Blocking?* No.

---

## CH-R8 — The policy/ceiling contradiction is documented, not resolved — and the draft's own "ship together" discipline is applied to D3(a)/(b) and withheld from the half that actually reaches a user.

**The hole.** The draft is admirably honest here: *"The stated 10 × 10 MiB per-request policy is NOT
honoured and will not be."* But look at what changes for the caller who provoked the finding:

| | Before | After D1+D2+D3 |
|---|---|---|
| 2 max-size files | works | works |
| 3 max-size files | fails, illegibly | **still fails**, now with *"The file is too large."* (CH-R5) |
| 10 legal files (the validator's stated allowance) | fails | **still fails** |

Nothing a real cleaner does gets easier. The validator at `SaveMyDocuments.cs:53` and
`UpdateEmployee.cs:30` continues to advertise ten, the transport continues to admit two, and the ADR's
answer is that a **client** should refuse the batch first (D7-b) — which is (a) not the contract, (b)
web-only (iOS/Android send one item per request, so they were never the problem), and (c) **not bound to
this decision** by the rule the draft applies elsewhere. C-2 sustains exactly this objection for
D3(a)/(b) — *"a ceiling with no legible failure is measurably worse than no ceiling"* — and then D7-b,
the only limb that changes a user outcome, is left as *"its own ticket"* with no `depends_on`.

**Two resolutions the draft does not consider,** neither of which is A2 or A5:

1. **Make the count cap a function of the ceiling.** `MaxDocumentsPerRequest` becomes
   `floor(ceiling / maxWireItemBytes)` — today `floor(32 MiB / 13.33 MiB) = 2` — computed from the same
   configured number. The validator then answers **400 + `file.count_exceeded`** ("You've attached too
   many files", already translated in all fifteen locale files) for the batch the transport would
   refuse, and the two numbers cannot drift because there is one. This is A5's legibility win without
   A5's second byte-budget, and it costs one shared constant.
2. **Say plainly that the count cap is not a per-request allowance.** `SaveMyDocuments.cs:47-52`'s own
   comment already concedes the cap bounds *rows and blob uploads*, not bytes — but a validator that
   accepts `documents.Count <= 10` is read by every client author as "ten is allowed". If it is not, the
   error key and the OpenAPI description should say what is.

**What I want changed.** D2 states the contradiction's **resolution**, not just its existence: either
resolution 1, or an explicit ruling that the count cap stays 10 with the reason spelled where an
endpoint author reads it. And D7-b gains the same `depends_on` binding C-2 gave D3(b), or the ADR
concedes in §Consequences that no caller's outcome improves in the shipped scope.

*Blocking?* No — but "we documented the contradiction" is the weakest possible discharge of AC2, and the
draft's C-5 already anticipates being pushed here. My answer to C-5's question — *should the ceiling
wait for a release of telemetry?* — is **no, it should not wait**, for the reason the draft gives (D1
changes nothing for any caller, D3(b) is what creates the telemetry). I sustain the author on that
point. What should not ship is the framing that the contradiction has been *resolved*.

---

## Found sound — what I attacked and could not move

Stated at length because silence is not assent, and because this draft's evidence discipline is the
best I have reviewed this sprint.

- **The zero-hit claim.** `rg 'MaxRequestBodySize|RequestSizeLimit|MultipartBodyLengthLimit|DisableRequestSizeLimit' src/`
  → **no matches.** Verified.
- **The pipeline order.** `CleansiaStartupBase.cs` — `EnableBuffering` `:136-140`, `UseForwardedHeaders`
  `:146`, `UseMiddleware(RequestLogging)` `:166`, `UseExceptionHandler` `:168`, `UseAuthentication`
  `:180`, `UseRateLimiter` `:181`, `UseAuthorization` `:187`. **Every line exact.**
- **The pre-auth read.** `RequestLoggingMiddleware.cs:32` awaits `LogRequestAsync` before `_next`;
  `:75` → `ReadRequestBodyAsync` `:129-144` → `new StreamReader(request.Body).ReadToEndAsync()`;
  `SafeBody` `:166-179` discards above `RedactionScanLimit` `:16` (64 KiB). **Exact, and it is the
  finding that reframes the ticket — the draft is right that this, not the ceiling, is the security
  content.**
- **The count-cap table.** `SaveMyDocuments.cs:53` (`MaxDocumentsPerRequest = 10`), `:70-74` (the
  `Cascade.Stop` chain with `FileCountExceeded`); `UpdateEmployee.cs:30`, `:136-138`, and its `.When`
  guard at `:152`; `SaveOrderPhotos.cs:47-49` — `NotEmpty()` only, **no count cap**, with per-item
  `BlobFileSize.HasContentWithinLimit` at `:67`. **All three rows exact.** The correction of T-0557's
  stale premises is right and well made.
- **The 133 MiB arithmetic.** `BlobFileSize.cs:8-9` (10 MiB) and `:25-27`
  (`fileSizeInBytes = (base64Data.Length * 3L) / 4L`), so the wire allowance is ≈13.33 MiB per item and
  10 items ≈133 MiB. **Correct.**
- **The `WireSurface.HostAssemblies()` warning in D6.** `WireSurface.cs:169-170` is
  `RequestLoggingHarness.AllHostMiddleware.Select(t => t.Assembly).Distinct()`, and
  `RequestLoggingHarness.cs:21-28` is a **hand-written list of five types**. The draft's instruction not
  to use it for discovery is exactly right, and it is the kind of trap a reviewer would have fallen into.
- **The `WebSdkContentGlobTests` model.** `:61-94`, with the non-vacuity floor at `:76-79`
  (`webHosts.Count >= 5`). Exactly the shape D6 asks to copy. `rg 'Sdk="Microsoft.NET.Sdk.Web"' src/`
  returns exactly **five** csprojs.
- **D5 (per-endpoint attribute rejected).** I could not construct a counterexample. The strong reason
  given — the feature is read-only once the read has started, and the read starts at
  `RequestLoggingMiddleware.cs:32`, upstream of every MVC filter — is structurally right on this pipeline
  regardless of the exact exception behaviour, and the draft correctly marks the behaviour ⚠. **Sustained.**
- **D2 constraint 1.** 33 554 432 > 30 000 000. Nothing that succeeds today begins to fail. **Sustained.**
- **A6 (per-host ceilings).** `Base64UploadIntakeRosterTests.cs:31-43` does show the same
  `SaveMyDocuments`/`UpdateEmployee`/`SavePhotos` triple on both the web and mobile partner hosts. The
  "intake surface does not differ by host" premise holds. **Sustained.**
- **The author's second nominated attack — the Functions host — does not land.**
  `rg 'HttpTrigger' src/Cleansia.Functions` returns exactly one hit: `HealthFunction.cs:17`, an
  anonymous **GET** on `health`. There is no body-taking intake there, so there is nothing for this ADR
  to rule on. Worth one sentence in the ADR so the next reader does not re-derive it; not a gap.

**Net.** The author's own two self-nominated starting points were the response buffering (which I found
is *not* out of scope — CH-R6) and the Functions host (which is a non-issue). As on the sibling draft,
the self-challenge converged on the author's reasoning; the defects are in the two places the author
declared settled — the failure contract (CH-R1) and the justification (CH-R3/CH-R4).

---

## Verdict I am asking the lead for

**CH-R1, CH-R3 and CH-R6 block.** Each is a statement the ADR makes that its own cited source
contradicts, and each is fixable inside the draft without re-deciding anything.

**On the question the panel actually has to answer — is D1 worth doing?** Having argued the rival as
hard as I could: **yes, narrowly**, and for one reason the draft does not currently state
(behaviour-stability across a framework upgrade) plus one it states but does not secure (tunability,
which needs CH-R3's clamp to be worth having). The ADR must add both or it is a platform-wide config
surface defended by benefits a five-line test would also deliver.

**On the number:** 32 MiB survives constraint 1 and I could not move it — but its upper bound is
computed from half the instance (CH-R6), and the policy contradiction is documented rather than resolved
(CH-R8). Fix the arithmetic and the number will probably stand; ship it as-is and the derivation is
decorative.

**On the 413:** the draft's precondition is right and its scope is too narrow. The answer is not one
unknown — it is two paths, and one of them already reads 500 at HEAD. If the backend lane comes back
"500", **A4 is not the ready-made answer the draft claims**, for the reason in CH-R2; the version of A4
that works is the one placed above `ShouldSkipLogging`, which also fixes CH-R1 and which nobody has
costed.
