# Role — `IOperatorTenantResolver` / `OperatorTenantResolver` (CRC card)

> Introduced by **ADR-0061 D3** (`docs/decisions/adr-0061.md`, **`accepted`** 2026-09-13).
> `Cleansia.Core.AppServices.Tenancy`, scoped. Returns a named record struct,
> `OperatorResolution(bool IsMarket, string? OperatorTenantId)`, with a `NotAMarket` constant. Three
> callers today: `OperatorTenantScopeBehavior` (every anonymous scoped request), `CreateOrder.Validator`
> (the address country's operator against the ambient tenant) and `ApproveEmployee.Validator` (the work
> country's operator against the admin's claim).

## Responsibility (one sentence)
Answer **two** questions for one country id — *is it a market?* and *which operating company serves
it?* — or, when no country is given, for the default market, using the same default choice the market
directory makes.

## Collaborators
- `ICountryRepository.GetByIdAsync` — a country that does not exist, is not serviced or is not active is
  **not a market**, before any configuration is read.
- `GetMarkets.ResolveMarketAsync` (static, shared with the directory) — the remaining market predicates:
  a configuration naming a currency, and that currency switched on (ADR-0058 D1). One code path, so the
  directory and the anonymous scope can never disagree on what a market is.
- `GetMarkets.Handler`, invoked **directly** (not through the mediator — re-entering the pipeline from
  inside a pipeline behaviour buys nothing) — for the null-country case, the one place that chooses the
  default market (the flagged `IsDefaultMarket`, else the default-currency rule).
- `ICountryConfigurationRepository.GetByCountryIdAsync` — the `OperatorTenantId` column, the only source
  of the second answer.
- `ILogger` — a platform with no listed default market is logged as an error and answered *"market, no
  operator"* (`IsMarket = true, OperatorTenantId = null`): a configuration defect of the same class as a
  market nobody operates, and the caller refuses it as one.

## Does NOT know
- **The request, the claim, the host, the override.** It returns a value; the behaviour decides whether
  to set anything. It never calls `SetTenantOverride`.
- **The user.** Whether the caller is a visitor registering, a guest booking or an admin approving is
  the caller's business; the answer is the same for all three.
- **The currency.** That is `ICurrencyResolutionService`'s answer from the **same country** — a different
  fact read from the same row, which is exactly why tenant and currency cannot disagree on a booking.
- **Which error key either answer maps to.** `!IsMarket → country.not_serviced` and
  `null → tenant.not_found` are the behaviour's mapping; the two validators map the second to their own
  mismatch keys.
- **Whether the operator id is real.** It reads a column that is FK'd to `Tenants`; it does not read
  `Tenants`.

## Invariants a reviewer checks
- **Both answers are returned, always.** A caller that collapses them to one boolean re-creates the
  challenge ADR-0061 CH-2 sustained: a bogus country mislabelled as a configuration defect.
- **`IsMarket` is the directory's predicate, not a re-derivation.** If `GetMarkets` gains a predicate, the
  resolver gains it through `ResolveMarketAsync` or the two drift — `OperatorTenantResolverTests` and
  `GetMarketsHandlerTests` are the pair to read together.
- **The default is the directory's default.** `IsDefaultMarket` first, the default-currency fallback
  second, the same logging; the resolver never picks a country of its own.

## Watch-list
The null-country branch costs a full directory listing per anonymous request that names no market —
every registration today. Fine at this size; if it ever shows on a profile, the answer is a cached
default, not a second way of choosing one.
