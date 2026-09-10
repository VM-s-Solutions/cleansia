---
id: T-0708
title: A payout can be issued in any currency to any bank account
status: todo
size: M
owner: —
created: 2026-09-10
updated: 2026-09-10
depends_on: []
blocks: []
stories: []
adrs: []
layers: [backend]
security_touching: false
manual_steps: []
sprint: 16
---

## Context

**The payout-details model has no currency, and nothing checks that the account can receive what is being sent.**

`MyPayoutDetails` and the payout schemes carry an IBAN, a scheme and a country — no currency. Nothing
compares the invoice's currency to what the destination account can accept.

Today every payout is CZK to a CZK account, so nothing has been wrong. With a second currency a
cleaner can be sent EUR to an account that will bounce it, and the platform will believe it paid.

## Acceptance criteria

1. A payout carries its currency, and a payout in a currency the account cannot receive is refused
   with a translated error.
2. The refusal is testable without a live banking integration.

## Notes

Related: EmployeeInvoiceRepository does not Include Currency on two reads, so an invoice can render with a null currency.

Split out of **T-0688** by the multicurrency readiness audit of 2026-09-10. Every claim here was
re-verified against the tree that day, and the ones that did not survive verification were not filed.
