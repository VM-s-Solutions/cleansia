---
id: T-0565
title: Five error keys are written as bare string literals, so the parity guards cannot see them and every one shows the generic error
status: ready
size: S
owner: backend
created: 2026-08-06
updated: 2026-08-06
depends_on: []
blocks: []
stories: []
adrs: []
layers: [backend, frontend]
security_touching: false
manual_steps: []
sprint: 15
---

## Context

Found while fixing the swapped `Error` slots (`99af0bc4`). That fix normalised sixteen constructions
that put the translation key in the wrong slot. This is the adjacent defect: keys that are in the
**right** slot but were never written as a `BusinessErrorMessage` constant at all.

The parity guards (`apps/<app>/src/app/i18n/error-contract-parity.spec.ts`) assert against
`BusinessErrorMessage.cs` **directly**. A key that never becomes a constant is therefore invisible to
them by construction — the guard is not failing, it is not looking. All five below resolve to
`api.common.error_occurred` ("An error occurred. Please try again.") on every client.

**Verified 2026-08-06** — the leaf keys exist nowhere in any locale of any app. The apparent
`registration_number` / `vat_number` hits are **field labels**; the only `invalid_format` entries are
admin's two `email.invalid_format`.

| Site | Emitted key | Surface | Reachable by |
|---|---|---|---|
| `Features/Orders/CreatePaymentIntent.cs:47` | `order.payment.already_paid` | customer money path | customer web + both mobile customers |
| `Features/Orders/ConfirmRecurringOrder.cs:93` | `order.payment.already_paid` | customer money path | same |
| `Features/Employees/UpdateEmployee.cs:104` | `validation.registration_number.invalid_format` | partner profile | partner web + mobile partner |
| `Features/Employees/UpdateIdentificationInfo.cs:74` | `validation.registration_number.invalid_format` | partner profile | same |
| `Features/Employees/UpdateEmployee.cs:118` + `UpdateIdentificationInfo.cs:89` | `validation.vat_number.invalid_format` | partner profile | same |

The money-path pair is the one that matters most: a customer who tries to pay for an order that is
already paid is told nothing useful, on the one flow where a clear message is the difference between
"I already paid" and "I will try again".

## Acceptance criteria

- [ ] **AC1** — Three new `BusinessErrorMessage` constants carrying exactly the strings above. Do
      **not** rename the keys: they are already on the wire, and renaming turns a missing translation
      into a second missing translation.
- [ ] **AC2** — All five call sites use the constants. `.WithMessage(BusinessErrorMessage.X)` for the
      FluentValidation sites, house shape `new Error(nameof(field), BusinessErrorMessage.X)` for the
      `ConfirmRecurringOrder` one.
- [ ] **AC3** — `api.*` translations in **all five locales**, in **each app that can reach the
      endpoint** per the table above. Customer keys go to the customer app; the partner-profile keys go
      to partner. Do not add a key to an app that cannot reach it — say so instead.
- [ ] **AC4** — Both mobile platforms carry the two partner-profile keys and the money-path key in
      their own five-locale resources, matching the client each surface actually has. Check what is
      there before adding; the mobile bundles use different prefixes from web.
- [ ] **AC5** — A guard that would have caught this. `BusinessErrorSlotContractTests` already scans
      source for the slot defect; extend the same scan (or add a sibling fact) to assert **no
      dot-notation string literal is passed to `.WithMessage(...)` or to an `Error`'s message slot
      unless it came from `BusinessErrorMessage`**. That closes the class, not the five instances.
- [ ] **AC6** — Mutation-proved. Reverting any one call site to its literal must turn AC5's fact red,
      and removing any one added locale key must turn the corresponding parity spec red. Applied one
      at a time, restored byte-exact.

## Notes

AC5 is the point of the ticket. Fixing five call sites is twenty minutes; the reason they existed for
this long is that **nothing could see them**, and the sixth will be written next sprint unless the
scan closes the class. Note the anti-vacuity discipline the sibling guard uses: a scanner that
silently matches nothing passes every other assertion, so the new fact needs its own proof that it
reaches a real call site.

Do not widen the scan to *all* string literals — plenty are legitimately prose. The signal is
**dot-notation**, which is what the interceptor treats as a key.
