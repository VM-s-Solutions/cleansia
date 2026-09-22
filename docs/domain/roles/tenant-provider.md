# Role — `ITenantProvider` / `TenantProvider` (CRC card)

> The seam every tenancy bug in this repo has passed through, and the reason it has no earlier card is
> that it looks trivial: 20 lines, three members, no branches
> (`src/Cleansia.Infra.Database/TenantProvider.cs:12-30`). Interface:
> `Cleansia.Core.Domain.Repositories.ITenantProvider`. Registered per-request (scoped); the design-time
> / migration path passes `null` for it, which is the filter's first clause. Governed by **ADR-0017**
> (tenancy is app-level, claim-driven, no header), narrowed by **ADR-0051** (which read may stand
> outside the filter), and given something to hold by **ADR-0061** (a tenant is an operating company;
> every stamped row carries one; `null` is never a tenant). ADR-0050's "what a dormant `TenantId` can and
> cannot enforce" is history — the column is NOT NULL.

## Responsibility (one sentence)
Answer *"what tenant is ambient on this unit of work, right now?"* — the explicit override if one is
set, else the request's `tenant_id` claim, else `null` — for exactly two consumers: the global query
filter, and the `Added`-entity stamp at commit time.

## Collaborators
- `CleansiaDbContext.ApplyTenantQueryFilters`
  (`src/Cleansia.Infra.Database/CleansiaDbContext.cs`) — the read side. The provider is captured by
  reference in the filter expression and called **lazily, at query translation time**, which is why an
  override set mid-request affects queries issued after it.
- `CleansiaDbContext.CommitAsync` — the write side. It stamps `TenantId` on every `Added`
  `ITenantEntity` **from whatever is ambient at commit time**, not at `Add` time. This one sentence is
  the whole reason a background sweep must commit *inside* its per-tenant iteration — and, since the
  column is NOT NULL, the reason a commit under `null` is a `23502` rather than an orphan row.
- `IHttpContextAccessor` → the `tenant_id` claim minted from `user.TenantId`, which is never null.
- **Three writers of the override**, each for one reason: `OperatorTenantScopeBehavior` (an anonymous
  request that names a market gets the market operator's tenant before validation — ADR-0061 D3);
  `TokenService` / `RefreshToken.Handler` (a request that authenticates a user adopts the user's tenant
  before the `RefreshToken` is written — D4; this deliberately *replaces* the behaviour's override on a
  social sign-in of an existing user under another operator); and background jobs / webhooks, via
  `SetTenantOverride` / `ClearTenantOverride` per row or per tenant group (D10).

## Does NOT know
- **Whether the current request is authenticated.** It returns `null` for an anonymous request before the
  scope behaviour ran and for a job before its per-row override identically, and nothing downstream can
  tell those two apart. Every "cell 2 / cell 3" bypass in `security-rules.md` §S8 exists because of
  this single fact.
- **What `null` means.** It means *no context* — an anonymous request before the scope behaviour ran, or
  a job before its per-row override. **It is never a tenant.** The filter's middle clause matches nothing
  on a stamped table, so a `null` reader reads nothing; a `null` writer is refused by the database.
- **Which rows exist.** A tenant id it returns need not have a single row; a tenant that has rows need
  not ever be returned. The registry is `Tenants` (seed-only); resolution is by **market**
  (`OperatorTenantResolver`, from `CountryConfiguration.OperatorTenantId`); there is still no host
  resolution and there will not be (ADR-0061 Alternative (c); ADR-0028's design stays declined).
- **Whether a `(TenantId, …)` unique index will fire.** It will — the tenant term is NOT NULL — but the
  provider is still not the place to ask about the *other* nullable terms; see `consistency.md`
  §*"Tenant-scoped unique indexes"*.
- **Regions, connection strings, countries.** ADR-0017's region resolver is a **different role**; a
  region clause reaching this provider or that filter is a conflation finding. The resolver that reads a
  country is `OperatorTenantResolver`, and it hands this provider a value — it does not live here.
- **Anything a client sent.** S1: there is no header, no body field, no query parameter. A request names
  a market (`countryId`) and the server maps it; the provider only ever sees the mapped value.

## Invariants a reviewer checks
- **The override outranks the claim, and only the three named writers set it.** A `SetTenantOverride`
  in a handler on a claim-bearing request is a privilege-escalation shape; grep its callers and expect
  the scope behaviour, the two token-mint sites, and sweeps/Functions/webhooks — nothing else.
- **The override is cleared per iteration and the unit of work commits inside the loop.** Without the
  commit the override is decorative — every child row of every iteration is stamped with the *last*
  tenant processed.
- **Nothing else reads the claim.** `TenantClaimType` has one consumer. A second reader of `tenant_id`
  is a second source of truth for tenancy.
- **No handler branches on a tenant id** — the country-code rule, extended. Per-tenant variation belongs
  in `CountryConfiguration`, not in a `if (tenantId == …)`.

## Watch-list
The provider is stateless about *how* a caller acquired its tenancy, so it cannot enforce any of the
above; every invariant here is a **caller obligation** and is enforced by tests over the callers, not by
this type. That asymmetry is the role's defining weakness and the reason its card is longer than its
source file.
