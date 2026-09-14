# Cross-cutting concerns

Things that belong to no single flow and are documented once rather than repeated in each.

## Tenancy

A tenant is an **operating company** under the holding ([ADR-0061](/decisions/adr-0061)); each market
is served by one (`CountryConfiguration.OperatorTenantId`), and every tenant-scoped entity carries a
`TenantId` that is **NOT NULL** — the database refuses a business row with no owner. EF global query
filters scope reads automatically. A row gets its tenant at commit time from whatever is ambient:

| Who is writing | Where the tenant comes from |
|---|---|
| An authenticated request | the JWT's `tenant_id` claim, minted from the user's own row |
| An anonymous request that writes (register, social sign-up, guest booking, promo request, referral check) | the **market** it names (`countryId`, or the default market) → that market's operator, set by `OperatorTenantScopeBehavior` before validation. A country that is not a market is `country.not_serviced`; a market nobody operates is `tenant.not_found` |
| A request that authenticates a user (login, refresh, social sign-in, email confirm) | the user being authenticated — `TokenService` adopts it **before the confirmation check** and before the `RefreshToken` is written, replacing the market's operator on a social sign-in of an existing account; the two password-reset commands mint no token and adopt it themselves. The session acts name no market and implement `IOperatorScopedRequest` with an explicit `CountryId => null`, so their *refusal* audit row lands under the default market's operator and their *success* row under the account's ([ADR-0061](/decisions/adr-0061) D3/D4 as amended) |
| A system job or webhook | the row it read — see below |
| An audit row (admin or customer) | the same ambient tenant as the act it records, stamped by the audit writer (success) or the out-of-band failure sink — so the row and the `Order`/`User` it describes agree by construction. A customer refusal raised *before* an anonymous request's operator is resolved (`country.not_serviced`, `tenant.not_found`) has none, and the sink skips it with one warning rather than writing an orphan ([ADR-0062](/decisions/adr-0062) D7) |
| A `Failed` GDPR request row | the erasure's own ambient tenant — written out of band by `OutOfBandGdprDeletionFailureSink` in a scope of its own, so the rolled-back walk cannot take it with it; the daily retry job sets the row's tenant per candidate scope before re-running it |

**System jobs carry no JWT**, which makes them the interesting case. They read across tenants
deliberately, and when they *write* they must group by tenant, set the override per group, and commit
**inside** the loop:

```mermaid
flowchart LR
  A[Read across all tenants] --> B[Group by TenantId]
  B --> C[Clear override]
  C --> D[Set override for this group]
  D --> E[Write]
  E --> F[Commit — INSIDE the loop]
  F -->|next group| C

  classDef key fill:#dbeafe,stroke:#1d4ed8,color:#1e3a8a
  class F key
```

> A new row is stamped from the **ambient** tenant at commit time. One deferred commit at the end
> therefore stamps every group with whichever tenant happened to be processed last. Committing inside
> the loop is what makes the override mean anything.

⚠️ **A job that forgets its override does not read someone else's rows — it reads nothing, and writes
a `23502`.** The filter's `null == null` clause matches nothing on a stamped table now that the column
is NOT NULL, so a sweep with no ambient tenant sees an empty set and a commit with none is refused by
the database. That is the loud direction on purpose; the fix is the override-per-row shape above, never
a default tenant. A unique index containing `TenantId` fires unconditionally on the tenant term; the
ones that arbitrate a race on another nullable term still declare `NULLS NOT DISTINCT`
(→ [Security rules — S8](/architecture/security-rules#s8-tenant-isolation-correctness)).

## The outbox

A state change and its outgoing message commit together, so the message is durable if and only if the
change happened. A drainer then puts it on the wire.

The drainer claims work with a single `UPDATE … RETURNING` carrying a claim token and a lease cutoff —
atomic, so two drainers cannot claim the same row, and a crashed drainer's lease expires rather than
stranding the message.

## Consumer idempotency

A queue consumer claims a message key before performing its terminal effect. The claim **owns its own
commit**, so it is durable even if the effect later crashes — that is the point: at-most-once *after*
the marker. Two parallel redeliveries both racing the claim are separated by a unique index.

## Notifications

One producer writes the in-app row and enqueues the push in the same unit of work as the change that
caused it. The tenant is passed **explicitly** down this path rather than inherited from ambient
context, which is why notification rows from system sweeps are correctly tenanted even where the
sweep itself is not.

## Rate limiting

Two named policies — `auth` and `interactive` — partitioned per real client IP for anonymous callers
and per JWT subject for authenticated ones, plus a separate third policy for the Stripe webhook so a
webhook flood consumes none of the interactive allowance.

Establishing the *real* client IP is load-bearing: behind a front end, a per-IP partition that trusts
the wrong header collapses to one bucket. In non-development the host **refuses to boot** on an unset
or over-broad forwarded-headers configuration rather than starting with the partitioning silently
disabled.

## Authorization posture

Secure by default: the default policy requires an authenticated user, and `[AllowAnonymous]` is the
explicit, greppable opt-out. Counting controllers without an `[Authorize]` attribute tells you nothing
— the fallback is what protects them.

## Poisoned messages and dead letters {#dead-letters}

A message that exhausts its retry budget on a business queue is moved by the Storage-queue runtime to
`<queue>-poison`. Every poison consumer does exactly three things and nothing more:

```mermaid
flowchart LR
  A[Poisoned message] --> B["1. persist a dead-letter row — body VERBATIM"]
  B --> C["2. alert — identity only, never the body"]
  C --> D["3. ACK — return, never throw"]

  classDef warn fill:#fee2e2,stroke:#b91c1c,color:#7f1d1d
  class D warn
```

**It never re-runs the original effect.** No receipt, invoice, push or pay is re-processed here — the
handler is purely *persist and alert*.

**And nothing replays the row either.** No query, no admin endpoint, no replay command reads a
`DeadLetter`; the only code that touches one after the write is GDPR erasure, which deletes it. The row
is **the record that a thing failed, not the mechanism for making it succeed** — recovery today means a
human reading the alert and acting. → [`dead-letter-record`](/domain/roles/dead-letter-record)

**Acking is mandatory.** Throwing would re-poison the message into an endless loop. The durable row is
what makes acking safe.

### Why the alert carries the identity and never the body {#poison-alert-body}

One of the bodies that reaches this path is the outbound-email message, whose code field is a **raw
confirmation or reset token** — a live credential that grants account takeover until it is consumed or
expires.

The dead-letter row's sink is our own database. The alert's sinks are the host's retained log stream
and a separate vendor, where structured values additionally become **indexed tags** and a scope
breadcrumb that re-attaches to later, unrelated events.

The two live consumers on the same worker already hold this line, and the message-key helper hashes the
token so the secret never appears in a key or a log line. The poison handler was the one place on the
queue path that did not.

### When persisting fails {#persist-failed}

The handler still alerts and still acks — never re-poisons — so that message ends with **no durable
row**. The alert is deliberately **not** widened to carry the body as a last-copy substitute:

1. the log was never assigned a recovery role; the dead-letter row is the recovery source;
2. the queues whose durable row is mandatory lose nothing — the receipt and invoice messages contain no
   credential and no PII, and their whole subject is already inside the message key the alert carries;
3. the one body this subtracts is the one where "the log is the last copy" is a **liability**.
   Recovering a poisoned email is a *re-issue*, which needs the user id and the purpose — both already
   in the clear in the key — not a replay of a live token out of a vendor's log store.

Persisting fails when the database does, which is not an independent per-message coin flip: every
poisoned message in that window takes this branch at once. This is the burst case, not the singleton.
