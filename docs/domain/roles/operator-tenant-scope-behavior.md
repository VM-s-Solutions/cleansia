# Role — `OperatorTenantScopeBehavior` + `IOperatorScopedRequest` (CRC card)

> Introduced by **ADR-0061 D3** (`docs/decisions/adr-0061.md`, **`accepted`** 2026-09-13).
> `Cleansia.Core.AppServices.Tenancy`; a MediatR `IPipelineBehavior` constrained to `BusinessResult`
> responses, registered in `FluentValidationExtensions` **between `PostCommitDispatchBehavior` and
> `ValidationPipelineBehavior`** — the position is the contract, and `OperatorTenantScopeBehaviorOrderTests`
> pins it. The marker is one property: `string? CountryId`. Nine request records carry it: `Register`,
> `RegisterEmployee`, `GoogleAuth`, `AppleAuth`, `RequestPromoCode`, `ValidateReferral` (each gained an
> optional `CountryId`), `QuoteOrder`, `QuotePlusSavings` (their existing `CountryId`) and `CreateOrder`
> (`CustomerAddress?.CountryId`, by explicit interface implementation). A grep for the interface is the
> roster.

## Responsibility (one sentence)
Before validation runs, give an anonymous market-scoped request the ambient tenant of its market's
operating company; step aside when a claim exists; refuse `country.not_serviced` when the named country
is not a market and `tenant.not_found` when it is one nobody operates.

## Collaborators
- `ITenantProvider` — read once (`GetCurrentTenantId() is not null` ⇒ a claim is present ⇒ do nothing;
  S1: a request can never re-scope an authenticated caller) and written once (`SetTenantOverride`).
- `IOperatorTenantResolver` — the two answers. The behaviour owns the mapping from answers to keys; the
  resolver owns the answers.
- `ValidationPipelineBehavior.CreateValidationResult` — the refusal is shaped exactly like a validation
  failure (`Error(nameof(CountryId), key)`), so every client renders it through the path it already has.
- The `IOperatorScopedRequest` marker — the request says *which market*; it never says which tenant.

## Does NOT know
- **What the request writes or reads.** It sets the ambient tenant and leaves; `Register`'s `User`,
  `CreateOrder`'s `Order` and children, `ValidateReferral`'s filtered read all follow from that
  one call.
- **The address resolver.** `CreateOrder` exposes its country through the marker; a guest whose request
  named no country lands the default operator, and `CreateOrder.Validator`'s unconditional
  `order.country_operator_mismatch` rule is what makes that agree with the address (ADR-0061 D6).
- **Authenticated requests.** A claim wins, always. The behaviour is not a second tenant resolver; it is
  the *first* one for requests that have none.
- **The later override.** On a social sign-in of an *existing* user under another operator,
  `TokenService` re-sets the override to the user's tenant before the `RefreshToken` is added — the
  last writer wins, nothing stamped is added in between, and the behaviour neither knows nor cares.
- **The user's language, the host, the audience.** A market is a country; nothing else.

## Invariants a reviewer checks
- **Registered before validation.** The validators' filtered pre-checks are the first tenanted reads;
  a behaviour that ran after them would scope nothing. `OperatorTenantScopeBehaviorOrderTests` fails if
  the registration line moves.
- **No validator on a scoped request carries an `IsMarket` / `IsServiced` rule on `CountryId`.** The
  behaviour has already answered it; such a rule is dead code with a misleading key (ADR-0061 CH-2).
- **Two keys, both pre-existing in meaning.** `country.not_serviced` is user input;
  `tenant.not_found` is a configuration defect. Every app that can reach a scoped request owes both in
  five locales under `api.*` (the parity specs pin which).
- **The seven anonymous writers are stamped with the named market's operator, not the default's.**
  `AnonymousWriterLandsInMarketOperatorTests`, one row per request, plus the not-a-market row, the
  no-operator row and the promo-envelope row.

## Watch-list
The next anonymous writer that forgets the marker does not land `NULL` rows — the column is NOT NULL,
so it fails `23502` on first use in DEV. That is the intended failure and the reason ADR-0061 D8 is a
constraint rather than a test; the fix is the marker, never a `SetTenantOverride` in a handler.
