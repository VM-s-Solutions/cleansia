# Runtime Readiness — Observability & Outage Safety

Code that's shaped correctly can still fall over in production. This catalog covers what happens at
**runtime**: can you see what the system is doing, and does it **degrade gracefully** when a
dependency is down? Checked before a feature is `done` (Gate 5 / a runtime-readiness review for
anything touching an external service, a background job, or a hot path). The bar: the platform runs
safely with minimal manual intervention.

The real infra (from `docs/architecture/`): 5 .NET APIs + Azure Functions, PostgreSQL, Azure
Blob/Queue Storage, Stripe (payments), SendGrid (email), Firebase (push), Sentry (errors), .NET
Aspire. There's already a `RequestLoggingMiddleware` and a global exception handler.

---

## Observability

- **Structured logging, not string-concatenation.** Log with named properties
  (`logger.LogInformation("Order {OrderId} cancelled by {ActorRole}", id, role)`), never interpolated
  blobs. **No PII above Debug** (S6) — log `userId`, never email/phone/card.
- **Correlation id on every request.** A request/trace id flows through `RequestLoggingMiddleware`,
  into logs, into queue messages, and into the Function that processes them — so a customer action and
  its async side effects (receipt, invoice, push) can be stitched together. When you enqueue a
  message, carry the correlation id; when a Function picks it up, log with it.
- **Errors go to Sentry with context** (the app already integrates Sentry) — tenant id, user id (not
  PII), correlation id, the operation. A swallowed exception with no telemetry is invisible in PROD.
- **Every external call is logged at its boundary** with outcome + duration + error classification
  (`Transient | Permanent | Configuration | Unknown`), so a Stripe/SendGrid/Firebase slowdown is
  visible before it becomes an incident.

## Health & readiness

- **Each API exposes a health check** that verifies it can reach its critical dependencies (DB, and
  the queue/blob it needs) — used by Azure to route traffic only to healthy instances.
- **Functions are observable** — each background job logs start/finish/outcome and emits a metric or
  log the owner can alert on (e.g. "fiscal retry processed N, failed M").

## Graceful degradation (the dependency-down matrix)

The guiding rule: **a customer's core action must never be blocked by a non-core dependency being
down.** Each external dependency has a defined failure behavior:

| Dependency down | Must NOT happen | Correct behavior |
|---|---|---|
| **Stripe** (payments) | Order creation hard-crashes; customer sees a 500 | Classify the error (`Transient` vs `Permanent`); on transient, surface a retry-able message; never leave an order in a half-paid limbo — the order state + payment state are reconciled by webhook idempotency (S7). |
| **SendGrid** (email) | Order/booking fails because the email didn't send | Email is a **side effect** — enqueue it; a send failure is logged + retried by the queue/Function, it does **not** fail the command. |
| **Firebase** (push) | A state change fails because the push didn't deliver | Same — push is best-effort, enqueued, never blocks the state transition (see `CancelOrder`'s push being in its own try/catch). |
| **Fiscal authority** | Customer order completion is blocked | Per fiscal enforcement modes: lenient countries deliver immediately + retry registration in the background (`RetryFailedFiscalRegistrations` Function); only `BlockingOnline` countries hold the **receipt**, never the order. |
| **Azure Queue/Blob** | Data loss; the command silently drops a side effect | If the enqueue is part of the transaction, a failure should fail the command **before** committing user-visible state, OR use the outbox pattern so the side effect is durable. Never "fire and hope". |
| **PostgreSQL** | Cascading crash | Connection resilience (Aspire/Polly) for transient blips; a hard outage returns a clean 503, not a stack trace. |

## Background jobs & retries

- Side-effecting work that can fail transiently goes through a **queue + Function**, not inline in the
  request — so it's durable and retried, and the user isn't blocked waiting on it.
- Retries use **backoff** and read the **error classification**: `Transient` → retry; `Permanent` →
  stop + flag for the owner (e.g. the fiscal-failures admin area); `Configuration` → alert, don't
  retry forever.
- Every retry path has a **dead-end**: a max attempt count and a visible place a human can see what's
  stuck (a failures table/admin screen), so nothing retries silently forever.

## What to alert on (so the owner isn't surprised)

- A spike in `Permanent`/`Configuration` external errors (a key rotated, a provider contract changed).
- A background job's failure count crossing a threshold (fiscal retries, invoice generation).
- Health check failing for any API instance.
- A queue backing up (messages not being processed).

## Reviewer / readiness checklist (for anything touching external services, jobs, or hot paths)

1. Structured logs with correlation id; no PII above Debug.
2. Every external call classifies its error and logs the boundary.
3. The feature degrades per the matrix above — core action not blocked by a non-core dependency.
4. Side effects are enqueued (durable + retried), not inline-fire-and-forget.
5. Idempotent (S7) so retries/webhook re-deliveries are safe.
6. There's a visible dead-end for failures (a human can see what's stuck).
7. Health check covers any new critical dependency.
