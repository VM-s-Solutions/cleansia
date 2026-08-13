# Role — `ITenantProvider` / `TenantProvider` (CRC card)

> The seam every tenancy bug in this repo has passed through, and the reason it has no earlier card is
> that it looks trivial: 20 lines, three members, no branches
> (`src/Cleansia.Infra.Database/TenantProvider.cs:12-30`). Interface:
> `Cleansia.Core.Domain.Repositories.ITenantProvider`. Registered per-request (scoped); the design-time
> / migration path passes `null` for it, which is the filter's first clause. Governed by **ADR-0017**
> (tenancy is app-level, claim-driven, no header) and narrowed by **ADR-0051** (which read may stand
> outside the filter) and **ADR-0050** (what a dormant `TenantId` can and cannot enforce).

## Responsibility (one sentence)
Answer *"what tenant is ambient on this unit of work, right now?"* — the explicit override if one is
set, else the request's `tenant_id` claim, else `null` — for exactly two consumers: the global query
filter, and the `Added`-entity stamp at commit time.

## Collaborators
- `CleansiaDbContext.ApplyTenantQueryFilters`
  (`src/Cleansia.Infra.Database/CleansiaDbContext.cs:262-268`) — the read side. The provider is captured by
  reference in the filter expression and called **lazily, at query translation time**, which is why an
  override set mid-request affects queries issued after it.
- `CleansiaDbContext.CommitAsync` (`src/Cleansia.Infra.Database/CleansiaDbContext.cs:89-91`) — the write
  side. It stamps `TenantId` on every `Added`
  `ITenantEntity` **from whatever is ambient at commit time**, not at `Add` time. This one sentence is
  the whole reason a background sweep must commit *inside* its per-tenant iteration.
- `IHttpContextAccessor` → the `tenant_id` claim minted from `user.TenantId`.
- Background jobs, via `SetTenantOverride` / `ClearTenantOverride` — the only writer of the override.

## Does NOT know
- **Whether the current request is authenticated.** It returns `null` for an anonymous request and for
  a tenant-less job identically, and nothing downstream can tell those two apart. Every "cell 2 / cell 3"
  bypass in `security-rules.md` §S8 exists because of this single fact.
- **Whether `null` means "single-tenant" or "no context yet".** Both. The filter's middle clause makes
  the first meaning work and thereby makes the second one silent.
- **Which rows exist.** A tenant id it returns need not have a single row; a tenant that has rows need
  not ever be returned. There is no registry, no validation, and — since ADR-0028 is `DECLINED` — no
  host resolution. *(`docs/decisions/adr-0028.md:3`. **Retires when:** that
  status line stops reading `DECLINED`.)*
- **Whether a `(TenantId, …)` unique index will fire.** It will not, while it answers `null`, unless the
  index is declared `NULLS NOT DISTINCT`. The provider is the reason that question exists and is the
  last place anyone thinks to ask it — see `consistency.md` §*"Tenant-scoped unique indexes"* and the
  `Users` deviation.
- **Regions, connection strings, countries.** ADR-0017's region resolver is a **different role**; a
  region clause reaching this provider or that filter is a conflation finding.
- **Anything a client sent.** S1: there is no header, no body field, no query parameter. Two sources
  only, and one of them is server-set.

## Invariants a reviewer checks
- **The override outranks the claim, and only jobs set it.** A request-path `SetTenantOverride` is a
  privilege-escalation shape; grep its callers and expect only sweeps/Functions.
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
