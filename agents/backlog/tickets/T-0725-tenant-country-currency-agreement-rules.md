---
id: T-0725
title: Tenant / country / currency agreement rules; markets without an operator are not listed (ADR-0061 D6)
status: done
size: M
owner: —
created: 2026-09-13
updated: 2026-09-13
depends_on: [T-0723]
blocks: [T-0726, T-0727]
stories: []
adrs: [ADR-0061, ADR-0058]
layers: [backend]
security_touching: true
manual_steps: []
sprint: 16
---

## Context

Landed inside the T-0722 commit. The order's currency is the service address's country's; its tenant
is the ambient one. Two reads of one country can disagree — a customer of one company booking a
country another operates, a cleaner approved into another company's country — and an order stamped
with a tenant its own account cannot list is the outcome the two rules refuse.

## Doing

- `CreateOrder.Validator`: `order.country_operator_mismatch` on `CustomerAddress`, unconditional
  (guest and customer), comparing the resolver's operator for the cached country against
  `ITenantProvider.GetCurrentTenantId()`; the country id is cached beside the currency id so both
  rules judge one resolution.
- `ApproveEmployee.Validator`: `employee.work_country_operator_mismatch` after the serviced check,
  comparing the work country's operator against the ambient tenant.
- `GetMarkets`: a market whose `OperatorTenantId` is null is not listed and never the default; one
  `LogError` names it.

## New error keys

`tenant.not_found` (T-0723; Customer, Customer-Mobile, Partner, Partner-Mobile — every anonymous D3
endpoint), `order.country_operator_mismatch` (Customer, Customer-Mobile),
`employee.work_country_operator_mismatch` (Admin); `country.not_serviced` is newly reachable from
`Register`, `RegisterEmployee`, `GoogleAuth`, `AppleAuth`, `RequestPromoCode`, `Referral/Validate`.
Locales are T-0727's.

## Acceptance criteria (met)

`CreateOrderOperatorAgreementTests` (refusal, the one-operator-two-countries negative case, the guest
case, the unresolved country left to the handler, one resolution for both rules),
`ApproveEmployeeWorkCountryOperatorTests`, `GetMarketsHandlerTests` (the operator-less market),
`CreateOrderCallerCurrencyTests` (TC-TEN-MISMATCH-1 over Postgres, the guest booking landing in the
Slovak company).
