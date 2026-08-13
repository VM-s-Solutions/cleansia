# Cross-cutting concerns

Things that belong to no single flow and are documented once rather than repeated in each.

## Tenancy

Every tenant-scoped entity carries a `TenantId`, and EF global query filters scope reads
automatically. A JWT carries the tenant claim.

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

⚠️ **A unique index containing `TenantId` enforces nothing while `TenantId` is null.** Postgres treats
NULLs as distinct, so `(TenantId, …)` admits unlimited duplicates in single-tenant mode — which is
production today. No design may use such an index as its only concurrency arbiter; the ones that need
to arbitrate declare `NULLS NOT DISTINCT`.

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
