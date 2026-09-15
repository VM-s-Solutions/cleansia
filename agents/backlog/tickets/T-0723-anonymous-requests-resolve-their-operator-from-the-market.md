---
id: T-0723
title: Anonymous requests resolve their operator from the market (ADR-0061 D3)
status: done
size: M
owner: —
created: 2026-09-13
updated: 2026-09-13
depends_on: [T-0722]
blocks: [T-0726, T-0727, T-0728]
stories: []
adrs: [ADR-0061, ADR-0058]
layers: [backend]
security_touching: true
manual_steps: []
sprint: 16
---

## Context

Landed inside the T-0722 commit (see that ticket for why the group could not be green without it).
An anonymous request that writes a tenanted row, or reads one through the filter, names a MARKET —
never a tenant (S1) — and the pipeline resolves the market's operating company into the ambient
tenant before validation runs, because the validators' filtered pre-checks are the first tenanted
reads.

## Doing

- `Core.AppServices/Tenancy/`: `IOperatorScopedRequest { string? CountryId }`,
  `IOperatorTenantResolver.ResolveAsync(countryId) → (IsMarket, OperatorTenantId)`,
  `OperatorTenantResolver` (null ⇒ the directory's default market — `GetMarkets.Handler` called
  directly, one code path; a named country ⇒ `GetMarkets.ResolveMarketAsync`, the three ADR-0058 D1
  predicates extracted from the directory), `OperatorTenantScopeBehavior` registered after
  `PostCommitDispatch` and before `Validation`.
- Two refusals, both from the behaviour: `country.not_serviced` (user input, reused) and
  `tenant.not_found` (a market nobody operates — new key, on `CountryId`). No validator rule on
  `CountryId` was added; `QuoteOrder`/`QuotePlusSavings` keep their pre-existing `IsServiced` rule,
  which still guards the authenticated path the behaviour steps aside from.
- The marker on `Register`, `RegisterEmployee`, `GoogleAuth`, `AppleAuth`, `RequestPromoCode`,
  `ValidateReferral` (each gains a trailing optional `CountryId`), `QuoteOrder`, `QuotePlusSavings`
  (existing `CountryId`), `CreateOrder` (`CustomerAddress?.CountryId`, explicit implementation).
- `RequestPromoCode`'s outbox envelope carries `GetCurrentTenantId()` instead of `null` (CH-4).

## NOT

Clients sending `countryId` (T-0728); locales (T-0727); NSwag regeneration for the six request DTOs
(the orchestrator's — the wire shape gains only the optional trailing `countryId`).

## Acceptance criteria (met)

`AnonymousWriterLandsInMarketOperatorTests` (every D3 writer, no market ⇒ `cleansia-cz`, a market
nobody operates ⇒ `tenant.not_found` and zero rows, a non-market ⇒ `country.not_serviced` never
`tenant.not_found`, the promo envelope), `OperatorTenantScopeBehaviorOrderTests` (DI order, a claim
skips the resolver, the two refusals, the roster of exactly the marked requests),
`OperatorTenantResolverTests`.

## Review

Second pass, 2026-09-13, against the acceptance criteria on the tree at `e5a45de3`. Every criterion
already has its named test; nothing was added. Sabotages, each restored byte-exact (`git diff --quiet`)
before the next:

- `OperatorTenantScopeBehavior`: `tenant.not_found` swapped for `country.not_serviced` →
  `OperatorTenantScopeBehaviorOrderTests` 1 red / 6 green.
- `OperatorTenantScopeBehavior`: the claim step-aside removed (`|| GetCurrentTenantId() is not null`)
  → same class, 1 red.
- `FluentValidationExtensions`: the scope behaviour registered after `ValidationPipelineBehavior` →
  same class, 1 red.
- `RequestPromoCode.Handler`: the envelope's tenant back to `null` → `RequestPromoCodeTests` 1 red.

Validators grep: `CountryId` appears in `Register`, `RegisterEmployee`, `GoogleAuth`, `AppleAuth`,
`RequestPromoCode`, `ValidateReferral` only on the record parameter and its comment — no rule.
Guest `CreateOrder` naming Slovakia lands in the Slovak company in
`CreateOrderCallerCurrencyTests.A_Guest_Booking_At_A_Slovak_Address_Lands_In_Slovakias_Operating_Company`
(beside the other `CreateOrder` Postgres cases rather than in `AnonymousWriterLandsInMarketOperatorTests`).
Suites: Cleansia.Tests 4887, IntegrationTests 351, HostTests 183, green.
