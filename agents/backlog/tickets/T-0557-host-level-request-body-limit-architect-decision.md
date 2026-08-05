---
id: T-0557
title: There is no request-body limit anywhere — Kestrel's ~28.6 MB default is the real ceiling on every intake path. Decide the host-level shape (ADR)
status: done
size: S
owner: architect
created: 2026-08-05
updated: 2026-08-05
depends_on: []
blocks: []
stories: []
adrs: []
layers: [architect, backend]
security_touching: true
manual_steps: []
sprint: 15
source: scoped **out** of T-0548 (`97bb7265`) by the backend lane with its reason recorded — the right
  number is not derivable from the avatar path. Routed to `architect` and filed by the PM 2026-08-05
---

## Context

`MaxRequestBodySize` appears **nowhere in the solution** — PM-verified at HEAD across `src/**/*.cs` and
`src/**/*.json`, zero hits. So the effective ceiling on every intake path on all five hosts is
**Kestrel's ~28.6 MB default**, by accident rather than by decision.

**Why the avatar fix (`97bb7265`) does not close this.** That fix makes the *answer* correct — an
oversized avatar is now rejected before it is decoded — but it does not stop the *allocation*: the
request body is fully buffered by the server before any validator runs. Validation cannot be the
outermost bound; only the host can.

**Why the number is not derivable from the avatar path, which is the reason this was scoped out rather
than done.** Three intake paths accept **unbounded arrays and none has a count cap**. A single
host-wide limit therefore silently caps those three as well — so choosing a number is choosing a
policy for paths whose requirements were never stated. Picking one from the avatar's 10 MB would be
picking it for endpoints nobody measured.

**Why an attribute is the wrong shape, stated up front because it is the tempting one.** A
per-endpoint `[RequestSizeLimit]` is exactly what the *next* endpoint forgets — which is precisely how
the avatar gap arose, and how `SaveMyDocuments` (T-0556) still has no cap today. The recommended shape
is **host-level and config-driven in `src/Cleansia.Config/Abstractions/CleansiaStartupBase.cs`**, so a
new host inherits it and a new endpoint cannot opt out by omission.

This is an **architect decision, not a backend one**: it sets a platform-wide ceiling that interacts
with every current and future intake path, and the trade-off (a limit low enough to matter vs. high
enough not to break a legitimate multi-document upload) is exactly the kind that belongs in an ADR
rather than in a startup file nobody re-reads.

## Acceptance criteria

- [ ] **AC1 — the decision is made by a panel, not by an author.** Given `agents/process/deliberation.md`,
      When the ADR is written, Then **author ≠ challenger ≠ lead** as distinct instances, and §Verdict
      declares the composition. The ADR number is collision-checked **immediately before the file is
      written** (highest at HEAD is **0042**; **T-0547** is reserved by ADR-0042).
- [ ] **AC2 — the ADR states the number and derives it.** Given the chosen limit, When it is recorded,
      Then the ADR names the largest legitimate request the platform accepts today (with the endpoint
      and the evidence), and the chosen ceiling is justified against it — not against the avatar path
      alone.
- [ ] **AC3 — the three unbounded-array paths are enumerated and their fate stated.** Given that a
      host-wide limit caps them implicitly, When the ADR is written, Then each is named with its file
      path and the ADR says whether it also owes an explicit count cap, and in which ticket. **A
      host-wide limit that silently becomes those endpoints' policy without naming them is the failure
      mode this AC exists to prevent.**
- [ ] **AC4 — the shape is host-level and config-driven.** Given the ruling, When implementation is
      specified, Then the limit is applied in `CleansiaStartupBase` (inherited by all five hosts) and
      read from configuration, with the default in source. The ADR states explicitly that a
      per-endpoint attribute is rejected, and why (an attribute is what the next endpoint forgets).
- [ ] **AC5 — a guard proves a new host cannot omit it.** Given a hypothetical sixth host, When it is
      registered without opting in, Then a test fails. Model: `WebSdkContentGlobTests` (T-0538), which
      goes red on a **new** host that reintroduces the defect. A rule with no witness is guidance.
- [ ] **AC6 — the rejection is legible to a client.** Given a request over the limit, When it is
      rejected, Then the client receives a stable, translatable failure rather than a truncated
      connection — and the ADR states what each of the three generated clients sees.
- [ ] **AC7 — the catalog entry carries its enforcer and tier** (ADR-0032). If the outcome adds a
      `agents/knowledge/patterns-backend.md` entry, it names its enforcer and declares its tier, and its
      routing follows whatever ADR-0033's routing test resolves to at that time (see T-0549/T-0551).

## Out of scope

- **Per-endpoint validation limits.** `SaveMyDocuments` is **T-0556** and does not wait for this ticket
  — a correct endpoint answer and a correct host ceiling are two different guarantees and both are owed.
- Re-opening the avatar limit shipped in `97bb7265` (T-0548).
- Rate limiting, which is ADR-0003's subject and a different control.

## Implementation notes

**Files this ticket touches (decision first, implementation after acceptance):**
- `agents/backlog/adr/<NNNN>-<slug>.md` + `agents/backlog/adr/challenges/<NNNN>-<topic>.md` — the panel.
- `src/Cleansia.Config/Abstractions/CleansiaStartupBase.cs` — the host-level limit (AC4).
- The five host projects under `src/Cleansia.Web.*` / `src/Cleansia.Web.Mobile.*` — inheritance, and the
  AC5 guard test in `src/Cleansia.Tests/`.
- `src/Cleansia.Core.AppServices/Features/EmployeeDocuments/SaveMyDocuments.cs` and the other unbounded
  array paths — **named in the ADR (AC3), edited by their own tickets.**

⚠️ **Nothing is implemented before the ADR is accepted.** The routing precedent from this sprint is
explicit that a described fix is not a fix — but the inverse also holds: an implemented decision that
skipped its panel is not a decision.

### Staleness detectability (sprint-15 §D3)

The implementation half names **product paths under `src/`** (`CleansiaStartupBase.cs`), so the
candidate-3 path rule can flag it. The decision half lives under `agents/backlog/adr/**`, which no path
signal covers — re-verify by hand whether the ADR file exists at each checkpoint.

## Status log
- 2026-08-05 — created `ready` by pm, owner `architect`. Filed from the T-0548 lane's scope-out with its
  stated reason carried in rather than paraphrased: the number is not derivable from the avatar path
  because three intake paths take unbounded arrays with no count cap, so a host-wide limit sets policy
  for endpoints nobody has measured. `security_touching: true` — an unbounded intake is a resource
  exhaustion surface on all five hosts.
- 2026-08-05 — **architect (author mode) drafted the decision. NOT decided: AC1 is unmet.** Draft at
  `agents/backlog/adr/drafts/NNNN-host-request-intake-ceiling.md` (`proposed`, **number deliberately not
  allocated** — two architects collided on a number this sprint; asking for **0044**). Living doc:
  `agents/architecture/decisions/request-intake-limits.md`. **This ticket stays open** until an
  independent challenger round + a lead adjudication run and the ADR is renamed/accepted.

## Review

### Architect (author mode), 2026-08-05 — ruling drafted, panel owed

**Two of this ticket's stated premises are stale at HEAD and the ADR restates rather than inherits them.**

1. *"Three intake paths accept unbounded arrays and none has a count cap."* — **one** does at HEAD.
   `SaveMyDocuments` (`:53`, `:70-74`) and `UpdateEmployee.Documents` (`:30`, `:136-138`) both cap at 10
   since T-0556. **`SaveOrderPhotos.Photos` is the only uncapped array** (`:47-49`, `NotEmpty()` only).
2. *"`SaveMyDocuments` (T-0556) still has no cap today."* — false; T-0556 landed.

**The routing was still correct, for a stronger reason than the one recorded.** Not "the arrays are
unbounded" but: **the per-request policy the API already states is 4.7× the ceiling the host already
enforces, and always has been.** 10 files × 10 MiB decoded ≈ **133 MiB** on the wire (base64 +33 %)
against Kestrel's **28.6 MiB** — two max-size files fit, the third does not. That contradiction cannot be
resolved inside any one endpoint, which is exactly why it is an Architect call.

**The finding that reframes the ticket, and is not in §Context.** The body is not merely buffered before
validation — it is **fully materialized as a UTF-16 string before authentication and before the rate
limiter**. `RequestLoggingMiddleware` sits at `CleansiaStartupBase.cs:166`; `UseAuthentication` is `:180`
and `UseRateLimiter` is `:181`. `ReadRequestBodyAsync` (`:129-144`) does `ReadToEndAsync()` on the whole
body (≈2× bytes on the LOH) and `SafeBody` (`:166-179`) then **discards it** above `RedactionScanLimit`
(64 KiB). On S1 prod (1.75 GB, shared by 5 APIs + SSR + Functions) that is an anonymous, un-rate-limited
allocation primitive. **A host ceiling alone therefore does not make this a resource-exhaustion control**
— it caps the multiplier, not the amplifier.

**The ruling, in brief** (full reasoning + rejected alternatives in the ADR draft):

| | Decision |
|---|---|
| **D1** | One ceiling, `CleansiaStartupBase.ConfigureServices` → `KestrelServerOptions.Limits.MaxRequestBodySize`, default in source + config key. All five hosts inherit. |
| **D2** | **32 MiB (33,554,432 B)** — ≥ today's accidental 28.6 MiB so **nothing that works stops working**; admits the two-max-size-file batch; inside the S1 budget once D4 lands. Provisional by construction; the config key is the instrument for revising it on evidence that does not exist yet. |
| **D3** | Over-limit → **413, no body**. **Not** distinguishable from the validator's 400 by content — only by **status**. Each client's error mapper gains a status-413 branch resolving to the **existing** `file.size_exceeded` key (no new key ⇒ `error-contract-parity` guards untouched). **Ships with the ceiling, not after it.** |
| **D4** | Bound the pre-auth read to `RedactionScanLimit + 1` bytes (log output byte-identical) + explicit `EnableBuffering` bounds. **Own ticket — this is where the security value is.** |
| **D5** | `[RequestSizeLimit]` rejected — and *not* merely as forgettable: in this pipeline it **cannot function** (the read starts upstream of MVC's resource filters; the size feature is read-only once the read starts). A lower per-endpoint bound belongs in a validator, which can answer 400 with a key. |
| **D6** | AC5 guard = csproj walk for `Sdk="Microsoft.NET.Sdk.Web"` + `>= 5` non-vacuity floor + every host's `Startup : CleansiaStartupBase` + the registration asserted through resolved options. **Not** `WireSurface.HostAssemblies()` (hand-maintained roster — cannot notice a new host). |
| **D7** | `SaveOrderPhotos` owes a count cap (own ticket). Web owes a client-side **aggregate staging budget** — the actual user-facing repair. |

**AC status.**

- **AC1 — NOT MET.** One architect instance; §Challenge in the draft is an author-run self-challenge.
  Independent challenger(s) + lead owed. Number **not** allocated on purpose (asking for **0044**).
- **AC2 — met, with a caveat the panel should test.** The largest *policy* request is derived
  (133 MiB, `SaveMyDocuments`/`UpdateEmployee`, with the arithmetic). There is no *observed* maximum,
  because success/failure at the boundary has never been distinguishable in logs — stated rather than
  papered over. (Self-challenge **C-5** is exactly this; an independent challenger should push on it.)
- **AC3 — met.** All three named with file paths and their state at HEAD; only `SaveOrderPhotos` still
  owes a cap, in its own ticket.
- **AC4 — met.** Host-level + config-driven; attribute rejected with the stronger reason.
- **AC5 — specified** (D6), not implemented.
- **AC6 — met and sharpened.** The answer is "413 with no body, legible by status, mapped client-side to
  the existing size key" — plus the ⚠ below.
- **AC7 — specified** (D8 in the draft): one `patterns-backend.md` entry, `T1-CI`, enforcer = the D6 trio.

**⚠ ONE RUNTIME CLAIM MUST BE PINNED BEFORE THE CLIENT WORK STARTS.** This invocation had no shell;
nothing was executed. Whether Kestrel's over-limit 413 survives this pipeline, or is converted to
`500 "An unexpected error occurred."` by `UseExceptionHandler` (`CleansiaStartupBase.cs:168-175`), turns
on runtime behaviour. `RequestLoggingMiddleware` starts the read **upstream** of that handler and
rethrows (`:58-63`), so a bare 413 is expected — **but the implementing ticket owes an integration /
HostTests case asserting the observed status and body first.** If it is a 500, rejected alternative
**A4** (a `Content-Length` check in middleware returning ProblemDetails) becomes the answer, and the ADR
says so in advance.

### Production code this needs (specified, NOT written — two backend lanes are live in `src/`)

Nothing was written under `src/`. In dependency order:

1. **`src/Cleansia.Config/Abstractions/CleansiaStartupBase.cs`** — in `ConfigureServices`, a
   `services.Configure<KestrelServerOptions>` registration reading `Intake:MaxRequestBodyBytes` with the
   `33_554_432` default as a `public const` beside it. Sketch in the ADR §D1 (do not paste; it is a
   sketch).
2. **`src/Cleansia.Tests/Configuration/`** — the D6 trio (discovery + `>= 5` floor; every Web SDK host's
   `Startup : CleansiaStartupBase`; the registration asserted through resolved
   `IOptions<KestrelServerOptions>`). Model: `WebSdkContentGlobTests.cs:45-94`.
3. **`src/Cleansia.IntegrationTests/` or `src/Cleansia.HostTests/`** — the ⚠ case above: an over-ceiling
   request against a real host, asserting the observed status **and** body. This gates step 4.
4. **Client 413 mapping** (one branch each, ahead of the ProblemDetails body read):
   `src/Cleansia.App/libs/core/services/src/lib/interceptors/http-error.interceptor.ts` (covers all
   three web apps); the iOS generated-error mapper behind `ApiError.fromGenerated`; its Android twin.
   **No new translation key** — reuse `file.size_exceeded`, already present in all five locales of all
   three web apps and in both mobile string sets.
5. **Separate ticket (D4 — the security half):** bound `ReadRequestBodyAsync` to
   `RedactionScanLimit + 1` bytes in **all five** `src/Cleansia.Web.*/Middleware/RequestLoggingMiddleware.cs`
   copies, and give `CleansiaStartupBase.cs:136-140`'s `EnableBuffering()` explicit
   `bufferThreshold`/`bufferLimit` arguments. Log output is byte-identical. **Security should review this
   one, not the ceiling.**
6. **Separate ticket (D7-a):** `SaveOrderPhotos` count cap, mirroring `SaveMyDocuments.cs:47-99`.
7. **Separate ticket (D7-b):** web aggregate staging budget in `profile-documents.facade.ts` /
   `order-photos.facade.ts`, against the same ceiling — refuse the batch *before* the upload.
8. **`agents/knowledge/patterns-backend.md`** — the AC7 entry, landing with the accepted ADR.

**No `manual_steps` for any of the above** — no schema change, and no DTO/route/command change, so no
`ef-migration` and no `nswag-regen`.
