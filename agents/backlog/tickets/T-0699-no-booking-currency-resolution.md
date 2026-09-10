---
id: T-0699
title: Nothing resolves a booking currency — every order is stamped with the platform default
status: todo
size: L
owner: —
created: 2026-09-10
updated: 2026-09-10
depends_on: []
blocks: []
stories: []
adrs: []
layers: [backend, frontend, mobile]
security_touching: false
manual_steps: []
sprint: 16
---

## Context

**Two currencies can never be live at once. The only "switch" is promoting a new default, which swaps the whole platform in one click.**

`CreateOrder.cs` resolves `currencyRepository.GetDefaultAsync()` unconditionally, two statements
after it has already resolved the address and while ignoring it. `OrderPricingCalculator` maps a null
currency to the same call. `CurrencyId` is still declared on both `QuoteOrder.Command` and
`CreateOrder.Command` and is read nowhere — Wave A closed the caller-currency hole by amputation, not
by resolution, and the validators say so in comments.

`CurrencyRepository.GetDefaultAsync` filters on `IsDefault` only, and `IX_Currencies_IsDefault_Unique`
pins exactly one such row platform-wide. So "the booking currency" is a single global scalar.

Nothing customer-facing selects a currency. `ICurrencyResolutionService` is EMPLOYEE-only and no order
path calls it. `QuoteOrder.Command` carries no address, no country and no saved-address id, so the
quote endpoint has no country signal at all. The web, Android and iOS clients all send nothing.

**Consequence:** a customer in Berlin is quoted, charged, receipted and fiscalised in CZK, with no
field in the response to notice it by. And the mirror: promoting EUR makes every existing Czech
customer's next order EUR too.

## Acceptance criteria

1. An order's currency is resolved from something real — the service address's country, via
   `CountryConfiguration` — not from the platform default.
2. The quote and the create resolve the SAME currency, or `CreateOrder`'s price-agreement re-check
   rejects every booking as a mismatch.
3. A caller may only name a currency the catalogue is actually priced in; anything else falls back.
4. CZK and EUR orders can exist side by side, proven by an integration test that books one of each.

## Notes

Verified 2026-09-10 by adversarial trace. The `CurrencyId` fields already on both commands can carry this with no wire change.

Split out of **T-0688** by the multicurrency readiness audit of 2026-09-10. Every claim here was
re-verified against the tree that day, and the ones that did not survive verification were not filed.
