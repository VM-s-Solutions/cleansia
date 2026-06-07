# ADR-0005 — External-integration contract: pooled `IHttpClientFactory` clients, one failure-classification taxonomy, and async email off the critical path

- **Status:** accepted   <!-- proposed | accepted | superseded | rejected -->
- **Date:** 2026-06-06
- **Supersedes:** —
- **Superseded by:** —
- **Applies to:** backend | cross-cutting
- **Extends:** ADR-0002 (the async-email path rides the `IPendingDispatch` / `notifications-dispatch` seam and reuses its transient/permanent retry vocabulary)
- **Ticket:** T-0141 (ADR-INTEGRATION) · **Implements:** BLIND-5 (T-0144), BLIND-6 (T-0145), BLIND-1 (T-0146), LG-06 (T-0147), BLIND-7 (Wave-2)

> **Filename note (orchestrator).** T-0141 AC1 names the file `0004-integration-resilience-contract.md`,
> but `0004` is already taken by the accepted fiscal-receipt ADR. This decision is filed at the real
> next-free id **ADR-0005**; the dependent tickets cite **ADR-0005**, not "0004."

> This ADR is **ADR-INTEGRATION**. It freezes the **contract** every outbound integration codes against
> — Stripe, SendGrid, FCM/Firebase, Mapbox — and the contract the async-email path honors. It designs
> **no client and migrates no call site**; the code lands in BLIND-5/6/1, LG-06, BLIND-7, each test-first.
> Once `accepted` it is immutable — change it by superseding, never by editing.

---

## Context

Every outbound dependency the platform calls is constructed and failed-handled ad hoc. All facts below
are verified against the real code.

**1 — Socket-per-call HTTP, no pooling (BLIND-5).** Two of the three integration shapes bypass
`IHttpClientFactory` entirely:
- `StripeClient` (`src/Cleansia.Infra.Clients/Stripe/StripeClient.cs`) constructs a **fresh**
  `new global::Stripe.StripeClient(config.SecretKey)` on **every** method —
  `:42, 50, 83, 108, 140, 151, 165, 186, 225, 262, 312`. The Stripe SDK's default `HttpClient` is a new
  socket per client instance; per-call construction defeats connection reuse.
- `SendGridClientFactory.CreateClient()` returns `new SendGridClient(sendGridConfig.ApiKey)` per send
  (`src/Cleansia.Infra.Clients/SendGrid/SendGridClientFactory.cs:13-16`), registered transient
  (`SendGridExtensions.cs:11-15`).

Neither flows through a `SocketsHttpHandler` with a bounded `PooledConnectionLifetime`, so the platform
carries the classic **socket-exhaustion + stale-DNS** footgun under load. The one service that does it
right is `MapboxGeocodingService`
(`src/Cleansia.Infra.Services/Geocoding/MapboxGeocodingService.cs:12,15,53`): a named client
`IHttpClientFactory.CreateClient("Mapbox")`. **This ADR makes the Mapbox shape the rule.**

**2 — No failure classification (BLIND-6).** Each integration invents its own failure handling:
- `SendGridClientFactory.SendTemplateEmailAsync` collapses **any** non-2xx into the single
  `email.sending_failed` (`SendGridClientFactory.cs:11,26-30`) — a 503 (retry) and a 400 (never retry)
  are indistinguishable.
- `MapboxGeocodingService` swallows a fixed exception set and returns `null`
  (`MapboxGeocodingService.cs:68-74`) — a transient timeout and a permanent bad-request both degrade
  silently.
- `StripeClient` lets `StripeException` propagate raw; `CancelOrder.cs:146` and consumers each guess at
  what it means.

There is no shared taxonomy a command handler **or** a queue consumer can branch on. ADR-0002 D3.3
already mandates that *queue consumers* classify transient (throw → retry) vs permanent (ack), but that
vocabulary stops at the queue boundary; the **integration layer below it** has none.

**3 — Email is synchronous on the critical path (BLIND-1).** `Register.Handler` **awaits**
`emailService.SendEmailConfirmationAsync(...)` inline (`src/Cleansia.Core.AppServices/Features/.../Register.cs:89`)
*before* returning success; resend-confirmation follows the same shape. A SendGrid outage therefore
turns into a **user-facing 500 on registration / reset** — a downstream-dependency failure hard-fails a
core flow. This is exactly the "customer completion blocked by a downstream effect" anti-pattern that
ADR-0002's post-commit dispatch was built to remove for the *queue* effects; email never got the same
treatment.

These three are **one decision** — "how the platform talks to the outside world and what it does when
the outside world misbehaves" — because they are inseparable: a pooled client without a classification
taxonomy still can't tell a handler whether to retry; a classification taxonomy without the
async-email contract still lets a SendGrid 503 fail registration; and the async-email path *needs* the
taxonomy (a permanent 400 must not be retried forever on the queue). Splitting them lets one half ship
while another re-opens the hole.

---

## Decision

> **Contract principle (governs D1–D4).** Every outbound HTTP integration is constructed through a
> **pooled, named/typed `IHttpClientFactory` registration** — never `new HttpClient(...)` and never a
> per-call SDK-client construction in a handler/service. Every integration failure is mapped to **one
> closed classification** at the adapter boundary, and **consumers branch on the class, not on the raw
> status/exception type**. Any send that is **not** load-bearing for the caller's own committed result
> (registration / reset / resend confirmation email) is dispatched **off the request critical path**
> through the ADR-0002 `IPendingDispatch` seam, so a provider outage degrades the side effect, never the
> core operation.

### D1 — Every outbound integration is a pooled, named/typed `IHttpClientFactory` client (BLIND-5)

**D1.1 — The mandate.** Each of Stripe, SendGrid, FCM, Mapbox is registered **once**, in its
`Infra.*` registration extension, as a named or typed client with an explicit pooled handler:

```csharp
// Reference shape, frozen. PooledConnectionLifetime bounds stale-DNS; the handler is reused.
services.AddHttpClient("<Provider>")
    .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
    {
        PooledConnectionLifetime = TimeSpan.FromMinutes(5),   // bounded → DNS refresh, no socket churn
    })
    .AddPolicyHandler(/* D1.2 resilience policy */);
```

For an **SDK that wraps its own HTTP** (Stripe's `global::Stripe.StripeClient`, SendGrid's
`SendGridClient`), the SDK MUST be handed the **factory-provided `HttpClient`** (Stripe:
`new StripeClient(apiKey, httpClient: new SystemNetHttpClient(factoryClient))`; SendGrid:
`new SendGridClient(factoryClient, apiKey)`) rather than letting the SDK mint its own socket. The
**client/SDK instance is built once per scope from the factory**, not per call — the per-method
`new ...Service(new global::Stripe.StripeClient(...))` pattern at the eleven `StripeClient.cs` sites is
deleted.

**D1.2 — Resilience policy (Polly via `AddPolicyHandler`).** Each named client carries, at minimum:
- a **per-attempt timeout** (`Polly.Timeout`, pessimistic — bounds a hung socket; default **10 s**,
  config-overridable per provider);
- a **bounded retry with exponential backoff + jitter** that retries **only the Transient class** (D2),
  default **3 attempts** — never retries a Permanent/Caller-error or an Auth/Config class;
- a **circuit-breaker** that opens after a configured consecutive-Transient-failure threshold and
  fast-fails (as a Transient `IntegrationException`) while open, so one dead provider does not exhaust
  threads platform-wide.

Retry counts/timeouts/breaker thresholds are **config-driven per provider** (a payment retry budget is
not an email retry budget). The retry policy MUST be **idempotency-aware**: it may auto-retry only calls
that are safe to repeat. Stripe **write** calls (refund, charge, subscription) MUST carry a Stripe
idempotency key (the producer's deterministic key — see ADR-0006 for refunds, S7 for the general rule)
so a Polly retry of a write cannot double-charge/double-refund. A call with no idempotency key is **not**
auto-retried by the policy.

**D1.3 — Forbidden.** `new HttpClient(...)` anywhere in `Features/**`, `Infra.Clients/**`, or
`Infra.Services/**` outside the factory registrations; and per-call construction of an SDK HTTP client
inside a handler or a per-call service method. Both are blocking review findings (verification #1).

**This adapts** the conforming `MapboxGeocodingService` shape and the backend command pattern
(`agents/knowledge/patterns-backend.md:281` — "Side effects (Stripe/email/queue)? -> narrow try/catch +
idempotency (B8)"): B8's narrow-try/catch stays at the *handler* layer (D4); D1 fixes the *construction*
layer below it.

### D2 — One closed failure-classification taxonomy (BLIND-6)

Every integration failure is mapped, **at the adapter boundary**, to exactly one of a **closed** set.
The mapping is the integration's job; **no consumer ever sees a raw `StripeException`/`HttpRequestException`/
status code.**

| Class | Meaning | Maps from (examples) | Consumer rule |
|---|---|---|---|
| **Transient** | retry may succeed | HTTP 408, 429, 5xx; `TaskCanceledException`/timeout; socket reset; circuit-open | retry within the D1.2 budget (handler) / **throw → queue retry** (consumer, ADR-0002 D3.3) |
| **Permanent** (caller-error) | retry will never succeed | HTTP 4xx except 401/403/408/429 (e.g. 400/404/409/422) | map to a deterministic business error (handler) / **ack — do not throw** (consumer) |
| **Auth/Config** | our credentials/config are wrong | HTTP 401/403; missing/invalid API key; misconfigured endpoint | **never retried**; surfaced as a Critical-logged Permanent-class failure + alert (it is an ops incident, not a caller error) |
| **Timeout** | no response within budget | Polly timeout; `TaskCanceledException` from the per-attempt timeout | treated as **Transient** for retry, but **distinctly metered** (a timeout storm is the circuit-breaker's signal) |

**D2.1 — The carrier type.** Adapters surface the class through a single frozen abstraction —
`IntegrationFailure(IntegrationFailureClass Class, string Provider, string? ProviderCode, string Message)`
— returned in an adapter result (`IntegrationResult<T>`) **or** thrown as `IntegrationException`
(carrying the same `IntegrationFailure`) where the existing surface throws (Stripe SDK). The closed enum
`IntegrationFailureClass { Transient, Permanent, AuthConfig, Timeout }` lives in
`Cleansia.Core.Clients.Abstractions`. The classification function per provider (status/exception →
class) is a small, **unit-testable** pure mapper (verification #2).

**D2.2 — How it surfaces to each consumer kind.**
- **To a command handler:** a Permanent/AuthConfig failure maps to a `BusinessResult.Failure` with an
  `Error.Code` under the existing `BusinessErrorMessage` family (e.g. `email.sending_failed`,
  `payment.refund_failed`); a Transient failure that exhausted the D1.2 budget surfaces as a non-blocking
  follow-up (logged + the operation's own success rules decide — see ADR-0002 D1 / ADR-0006 for refunds).
- **To a queue consumer:** the class **is** ADR-0002 D3.3's transient-vs-permanent decision —
  `Transient`/`Timeout` → **throw** (queue retries to `maxDequeueCount`, then dead-letters);
  `Permanent` → **ack** (log Warning, return). This makes the queue-layer rule and the integration-layer
  rule **the same vocabulary**, not two parallel ones. The fiscal target-not-found carve-out (ADR-0002
  D3.3 / ADR-0004) is unchanged — it is a *domain* not-found, not an integration class.

**This adopts and extends** ADR-0002 D3.3 from the queue boundary down to the integration boundary,
so a single classification flows end to end.

### D3 — Async email off the request critical path (BLIND-1)

**D3.1 — The contract.** Registration / password-reset / resend-confirmation email is **enqueued via
the ADR-0002 `IPendingDispatch` seam onto `notifications-dispatch` (or a dedicated `send-email` queue —
see D3.4), not awaited inline.** `Register.Handler` (`Register.cs:89`) records intent and returns; the
command's **success contract** is: *the user row + confirmation code are committed* → success. The email
is an **at-least-once downstream effect** (Wave-1 outbox) / **best-effort** (Wave-0 in-memory) realized
post-commit, exactly like every other ADR-0002 side effect. A SendGrid outage therefore **no longer
changes `Register`'s HTTP status** (verification #4).

**D3.2 — Idempotency of the email effect.** The email send is a terminal effect on a queue consumer and
MUST satisfy ADR-0002 D2.2 (target-state check or `IIdempotencyGuard`) with a deterministic key
(`email:{purpose}:{userId}:{confirmationCodeId}` shape — frozen by the implementing ticket against the
ADR-0002 D2.1 table). A redelivery re-sends nothing.

**D3.3 — The carve-out (which sends stay synchronous).** A send stays **inline/awaited** only when its
result is load-bearing for the **caller's own committed decision** — i.e. the command cannot define
success without knowing the send outcome. Today that set is **empty** for transactional email: the
confirmation/reset code is already persisted, so the email is purely informative and is always async.
**Receipt/invoice email is already async** (it rides `generate-receipt`/`generate-invoice` per ADR-0002/
ADR-0004 — unchanged). The carve-out exists so a future "we must confirm delivery before committing X"
flow is a *documented, deliberate* exception, not the default. **No current send qualifies.**

**D3.4 — Queue choice (named, decided by the implementing ticket within this contract).** The send may
ride the existing `notifications-dispatch` queue (push + email both being "user notifications") **or** a
new dedicated `send-email` queue. This ADR does **not** force one; it requires that *whichever* is chosen
honors the full ADR-0002 queue contract (frozen `MessageKey` formula, idempotent consumer, `-poison`
consumer + dead-letter, dual-read at deploy, transient/permanent classification per D2). A **new** queue
is the cleaner seam (email and push have different providers and failure profiles) and is the
**recommended** default; the implementing ticket (BLIND-1) decides and records it. **It cross-references
ADR-0002 and does not invent a parallel dispatch mechanism.**

### D4 — Provider-call hygiene in handlers (LG-06) + rate-limit/429 handling (BLIND-7)

**D4.1 — Narrow try/catch + classify (LG-06).** A provider call that remains **inside a command
handler** (i.e. is load-bearing for the commit, e.g. `CreateOrder`'s `CreateCheckoutSessionAsync`, which
must hold the returned `StripeSessionId` before commit — out of scope of D3) MUST be wrapped in a
**narrow** try/catch for that provider's exception, **classified per D2**, and mapped to a
`BusinessResult.Failure` (Permanent/AuthConfig) or a bounded handler retry / non-blocking follow-up
(Transient). A broad `catch (Exception)` for control flow is forbidden (B8). The membership-command
no-try/catch sites LG-06 targets are brought under this rule.

**D4.2 — Rate-limit / 429 (BLIND-7).** A 429 from any provider is **Transient** (D2) **and** MUST honor
the provider's `Retry-After` header: the D1.2 retry policy reads `Retry-After` and waits at least that
long before the next attempt (capped by the per-attempt timeout budget); on budget exhaustion it
surfaces Transient. Mapbox specifically (BLIND-7) stops swallowing a 429 into a silent `null` and routes
it through the Transient class. This ADR sets the **expectation**; BLIND-7 implements the Mapbox case.

---

## Alternatives considered

- **Leave the SDK clients per-call, just wrap them in `using`.** Rejected: per-call SDK construction
  still mints a fresh socket/handler; `using` does not pool. The footgun is the missing pooled handler,
  not disposal. Only `IHttpClientFactory` + `SocketsHttpHandler.PooledConnectionLifetime` gives DNS
  refresh *and* socket reuse.
- **A typed client per integration instead of named.** Both are acceptable; this ADR **mandates one of
  the two** and names the conforming reference (named `"Mapbox"`). Typed is preferred for new
  integrations (compile-time injection); named is acceptable where an SDK wraps the client. The choice is
  per-integration and not load-bearing — the pooling + policy mandate is.
- **An open/extensible classification (let each integration add classes).** Rejected: an open set
  defeats the point — consumers could not exhaustively branch. The four-class closed set covers every
  observed failure family; adding a class is a superseding ADR.
- **Map raw exceptions at the consumer, not the adapter.** Rejected: it scatters provider knowledge
  (Stripe error codes, SendGrid status families) across every handler/consumer — the BLIND-6 status quo.
  Classification belongs **once**, at the adapter that owns the provider.
- **Keep registration email synchronous "so the user knows it was sent."** Rejected: the user is told
  "check your email," not "the SMTP call returned 202." A SendGrid outage failing registration is a
  strictly worse UX than a slightly-delayed email, and it violates the same downstream-blocks-core
  invariant ADR-0002 closed for queue effects.
- **Polly via `Microsoft.Extensions.Http.Resilience` (the newer standard) vs classic `AddPolicyHandler`.**
  Either is acceptable; the ADR mandates the *capabilities* (timeout/retry/breaker, idempotency-aware,
  config-driven), not the package. The implementing ticket picks one and keeps it uniform across the four
  clients.

---

## Consequences

**Cheaper:**
- Socket exhaustion and stale-DNS under load are structurally removed — one pooled handler per provider,
  DNS refreshed on `PooledConnectionLifetime`.
- A provider outage degrades the *side effect*, not the core flow: registration/reset survive a SendGrid
  outage (D3); one dead provider trips its breaker instead of exhausting platform threads (D1.2).
- One classification vocabulary spans integration → queue: a reviewer/consumer reasons about
  Transient/Permanent/AuthConfig/Timeout **once**, and the ADR-0002 D3.3 queue rule and the handler rule
  are the same rule (D2.2).
- A retried write cannot double-charge/double-refund (idempotency-aware retry, D1.2).

**More expensive (new obligations):**
- Every outbound integration MUST be a pooled `IHttpClientFactory` client with the D1.2 policy; `new
  HttpClient` / per-call SDK construction in `Features/**` / `Infra.*` is a blocking finding.
- Every adapter MUST classify failures into the closed D2 set and surface `IntegrationResult<T>` /
  `IntegrationException` — never a raw provider exception/status.
- Transactional email MUST be enqueued (D3), idempotent (D3.2), and ride the full ADR-0002 queue contract.
- Provider calls left in handlers MUST be narrow-try/catch + classify (D4.1); 429 honors `Retry-After`
  (D4.2).
- Retry budgets/timeouts/breaker thresholds become per-provider **config** an operator owns.

**Rollout (ticket sequencing — the contract, then the code, each test-first):**
- **BLIND-5 (T-0144):** migrate Stripe + SendGrid onto pooled named/typed clients (Mapbox already
  conforms; FCM brought in). Touches `StripeClient.cs`, `SendGridClientFactory.cs`/`SendGridExtensions.cs`,
  host `ServiceExtensions.cs` — these serialize among the BLIND-* tickets at implementation time.
- **BLIND-6 (T-0145):** the `IntegrationFailureClass` enum + per-provider mappers + `IntegrationResult`/
  `IntegrationException`; wire consumers to branch on class.
- **BLIND-1 (T-0146):** async email enqueue + `Register.Handler` change + the (new or reused) email queue
  consumer with its `MessageKey`, idempotency guard, `-poison` consumer, dual-read.
- **LG-06 (T-0147):** membership-command narrow-try/catch + classify.
- **BLIND-7 (Wave-2):** Mapbox 429 → Transient + `Retry-After`.
- **No EF migration** (no schema). **No NSwag change** (no DTO contract changes for the contract itself;
  if a Wave-2 consumer changes a response DTO it flags `nswag-regen` in *that* ticket). The new email
  queue (if D3.4 picks one) plus any `ProcessedMessage`/`DeadLetter` reuse is per ADR-0002's existing
  `ef-migration` flag — owner-only, flagged in BLIND-1.

---

## How a reviewer verifies compliance

**Mechanical (automated — the gate; candidates for `agents/tools/check-consistency.mjs`):**
1. **No raw HTTP/SDK client construction outside the factory.** Grep `new HttpClient(` and per-call
   `new global::Stripe.StripeClient(` / `new SendGridClient(` in `Features/**`, `Infra.Clients/**`,
   `Infra.Services/**` → **zero** outside the registration extensions. The eleven `StripeClient.cs`
   per-method constructions and `SendGridClientFactory.cs:13-16` must be gone.
2. **Pooled handler present.** Each provider's `AddHttpClient("<Provider>")` (or typed) has a
   `ConfigurePrimaryHttpMessageHandler` setting `SocketsHttpHandler.PooledConnectionLifetime` and an
   `AddPolicyHandler` (D1.2). A registration without both is a finding.
3. **Classification mapper is closed + unit-tested.** For each provider, the status/exception → class
   mapper exists and a unit test asserts representative mappings (503→Transient, 422→Permanent,
   401→AuthConfig, timeout→Timeout). A consumer that branches on a raw `StripeException`/status (not the
   class) is a finding.
4. **`Register.Handler` does not await email.** Grep `Register.cs` (+ resend) for an awaited
   `SendEmail*`/`emailService.*` inline → must be **zero**; the send is an `IPendingDispatch.Enqueue`.

**Test contract (these are the gate — each implementing ticket writes the red test first):**
5. **TC-POOL-0 (D1).** Over N sends, the **same** `HttpMessageHandler` instance is reused (assert via a
   spy handler that the factory hands back one handler) — proves pooling, fails against per-call
   construction.
6. **TC-CLASSIFY-INT-0 (D2).** Per provider: a simulated 503 → Transient (retried within budget, then
   thrown to a consumer); a 422 → Permanent (acked by a consumer, no throw); a 401 → AuthConfig
   (Critical-logged, not retried). Aligned with ADR-0002 TC-CLASSIFY-0 so the two layers agree.
7. **TC-EMAIL-ASYNC-0 (D3).** A simulated SendGrid outage during `Register` does **not** change
   `Register`'s `BusinessResult`/HTTP status; the email is enqueued; a redelivery re-sends exactly once
   (D3.2). Proves BLIND-1.
8. **TC-RETRY-IDEMP-0 (D1.2).** A Transient failure on a Stripe **write** causes a Polly retry that
   reuses the **same** idempotency key (no second charge/refund); a write with no key is **not** retried.
9. **TC-429-0 (D4.2).** A 429 with `Retry-After` is classified Transient and the next attempt waits ≥
   the header value (BLIND-7).

---

## Roles affected

Role files in `agents/knowledge/roles/`:
- **`integration-client.md`** (new, generic CRC) — *responsibility:* construct exactly one pooled
  `IHttpClientFactory`-backed client per provider, apply the D1.2 resilience policy, and classify every
  failure into the closed D2 set before returning/throwing. *Collaborators:* `IHttpClientFactory`, the
  provider SDK, the per-provider classification mapper, `IntegrationResult`/`IntegrationException`.
  *Does NOT know:* business meaning of the call, whether the caller is a handler or a consumer, retry
  policy of the *caller* (only its own transport-level policy), or any domain rule.
- **`integration-failure-classifier.md`** (new) — *responsibility:* map one provider's status/exception
  families to the closed `IntegrationFailureClass`. *Collaborators:* the provider's error model.
  *Does NOT know:* the call's idempotency, the consumer's retry budget, or what effect failed.
- **`queue-consumer.md`** (existing, ADR-0002) — updated: its transient/permanent decision (D3.3) is now
  **the same `IntegrationFailureClass`** the adapter returns (D2.2), not an independent guess.

Catalog edit (same change): `agents/knowledge/patterns-backend.md §B8` cross-references ADR-0005 — a
provider call constructed outside the factory, or surfacing a raw provider exception to a consumer, is a
B8/ADR-0005 violation; the async-email rule is recorded against the synchronous-email anti-pattern.

---

## Challenge / Defense / Verdict trail (condensed)

Author drafted; challengers (distributed-systems, pragmatic, security) attacked; the Lead re-verified
every load-bearing citation against the real code and adjudicated. **Verdict: all challenges RESOLVED;
zero blocking; consensus reached.**

| # | Challenge (severity) | Disposition | Where |
|---|---|---|---|
| CH-1 | A pooled client + a naive Polly retry would **auto-retry Stripe writes → double-charge/refund** (CRITICAL) | CONCEDE + REVISE | D1.2 idempotency-aware retry; only keyed/safe calls auto-retry; TC-RETRY-IDEMP-0 |
| CH-2 | An *open* classification set is unbranchable by consumers; "transient/permanent/business" was three loose words (MAJOR) | CONCEDE + REVISE | D2 closed four-class set + carrier type `IntegrationFailureClass`; verification #3 |
| CH-3 | Async email creates a **silent-loss** path (SendGrid outage → no email, success returned) — same gap ADR-0002 names (MAJOR) | DEFEND + ALIGN | D3.1 makes it an at-least-once outbox effect (Wave-1) / best-effort (Wave-0) under the **same** ADR-0002 residual framing; not a new silent-loss class |
| CH-4 | D3 risks inventing a parallel email dispatcher beside ADR-0002 (MODERATE) | CONCEDE + REVISE | D3.4 mandates the full ADR-0002 queue contract; new `send-email` queue is recommended, not a new mechanism |
| CH-5 | `CreateOrder`'s `CreateCheckoutSessionAsync` cannot go async (order must hold `StripeSessionId` pre-commit) — D3 must not sweep it (MODERATE) | DEFEND | D3.3 carve-out + D4.1 explicitly keep load-bearing-for-commit calls in-handler under narrow-try/catch |
| CH-6 (sec) | A 401/403 (our key is wrong) silently retried looks like a transient blip and hides an ops incident (MODERATE) | CONCEDE + REVISE | D2 AuthConfig class — never retried, Critical-logged + alert |
| CH-7 (sec) | Mapbox token-in-URL (`MapboxGeocodingService.cs:45`) is a real leak — does this ADR fix it? (note) | OUT OF SCOPE (named) | BLIND-2 / T-0159 owns it; D1 does not move the token to a header here — flagged, not folded |
| Prag-1 | "Typed vs named client" left undecided would re-create the inconsistency this ADR exists to fix (MODERATE) | DEFEND | Mandate is *pooled + policy*; typed/named is a per-integration non-load-bearing choice with a stated default |

**Affirmed unchallenged:** `IHttpClientFactory` + `SocketsHttpHandler.PooledConnectionLifetime` over
per-call construction; classification at the adapter not the consumer; reusing the ADR-0002 seam for
async email; the Mapbox shape as the reference.

**Lead re-verification (all against current code):** `StripeClient.cs` per-method
`new global::Stripe.StripeClient(config.SecretKey)` at `:42,50,83,108,140,151,165,186,225,262,312`;
`StripeClientFactory.cs:9-12` returns a new `StripeClient(config)` per `CreateClient`;
`SendGridClientFactory.cs:13-16` new client per send + `:26-30` single `email.sending_failed`;
`MapboxGeocodingService.cs:12,15,53` conforming `IHttpClientFactory.CreateClient("Mapbox")`, `:45`
token-in-URL (BLIND-2 scope), `:68-74` swallow-to-null; `Register.cs:89` inline-awaited email;
`IStripeClient.RefundCheckoutSessionAsync` (`Cleansia.Core.Clients.Abstractions/Stripe/IStripeClient.cs:13`)
carries no idempotency key (the write-retry hazard CH-1 names; the key is added by ADR-0006's consumer
ticket).

**Escalations to the owner:** none. Retry budgets/timeouts/breaker thresholds are operator-tunable
config, not business decisions. The email-queue choice (D3.4) is an implementer decision within this
contract.
