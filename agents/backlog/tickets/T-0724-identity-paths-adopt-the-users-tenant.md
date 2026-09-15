---
id: T-0724
title: Identity paths adopt the user's tenant; email is one identity; secret-keyed reads bypass and pin (ADR-0061 D4/D5.1)
status: done
size: M
owner: —
created: 2026-09-13
updated: 2026-09-13
depends_on: [T-0723]
blocks: [T-0726]
stories: []
adrs: [ADR-0061, ADR-0050, ADR-0051]
layers: [backend]
security_touching: true
manual_steps: []
sprint: 16
---

## Context

Landed inside the T-0722 commit. Login and refresh are anonymous and add a `RefreshToken` row; with
the column NOT NULL the request must adopt the user's own company before the row is added. One email
is one identity across the holding, so every anonymous identity pre-check reads ignoring the tenant.

## Doing

- `TokenService.GenerateTokenAsync` sets the override from `user.TenantId` before
  `refreshTokenService.Issue` (replacing the market operator the scope behaviour set on a social
  sign-in of an existing account); `RefreshToken.Handler` does the same before `CommitRotationAsync`.
- `Register` / `RegisterEmployee` validators read `GetByEmailIgnoringTenantAsync` and allow the
  unconfirmed re-registration only when the row's tenant is the ambient one; the handlers load the
  same way. `CreateAdminUser` uses `ExistsWithEmailIgnoringTenantAsync`. `ResendConfirmationEmail`
  goes tenant-ignoring too (absorbed — it would otherwise fail every resend).
- `GetByConfirmationCodeAsync` → `GetByConfirmationCodeIgnoringTenantAsync` (the legacy link's hash
  is the pin); `UserRepositoryTokenLookupTenantTests` roster gains the row.
- `LookupOrder` / `LookupOrderBatch` read `GetQueryableIgnoringTenant()`; the secret predicate is the
  pin (ADR-0051's bypass-and-re-pin cell).

## Acceptance criteria (met)

`AuthenticatedRequestAdoptsUserTenantTests` (login and rotation stamp the SK company, the JWT says
so), the existing-SK-Google-user row in `AnonymousWriterLandsInMarketOperatorTests`,
`UsersEmailGloballyUniqueTests` (TC-TEN-EMAIL, both halves), `SecretKeyedAnonymousReadsTests`
(TC-TEN-LOOKUP, TC-TEN-CONFIRM), `TokenServiceAdoptsUserTenantTests`, the `Register` and
`CreateAdminUser` validator cases.

## Review

Second pass, 2026-09-13, against the acceptance criteria on the tree at `e5a45de3`. Every criterion
already has its named test; nothing was added. Sabotages, each restored byte-exact before the next:

- `UserRepository.GetByConfirmationCodeIgnoringTenantAsync`: `.IgnoreQueryFilters()` removed →
  `UserRepositoryTokenLookupTenantTests` 1 red / 2 green (the roster mutation check the AC asks for).
- `Register.Validator`: the same-market comparison on the unconfirmed branch removed →
  `RegisterValidatorTests` 1 red / 27 green (the register pre-check mutation check).
- `TokenService`: the `SetTenantOverride(user.TenantId)` removed → `TokenServiceAdoptsUserTenantTests`
  1 red / 1 green.
- `LookupOrder.Handler`: `GetQueryableIgnoringTenant()` back to `GetQueryable()` →
  `LookupOrderSecretTests` 8 red.

The `CreateAdminUser` cross-company case is
`AdminUserProfileFieldsTests.When_Create_Email_Is_Held_In_Another_Operator_Then_Validation_Fails_With_AdminUserEmailExists`;
that surface keeps its existing key `admin_user.email_exists` rather than `ExistingUserWithEmail`.
Suites: Cleansia.Tests 4887, IntegrationTests 351, HostTests 183, green.
