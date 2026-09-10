---
id: T-0705
title: Admin goodwill credit lands in the default currency, and the admin sees only one balance
status: todo
size: S
owner: —
created: 2026-09-10
updated: 2026-09-10
depends_on: []
blocks: []
stories: []
adrs: []
layers: [backend, frontend]
security_touching: false
manual_steps: []
sprint: 16
---

## Context

**The issue command carries no currency and the admin screen renders accounts[0].**

`IssueCustomerCredit` carries no currency and its handler unconditionally resolves the platform
default, while the dialog's amount label is bound to the customer's LARGEST-balance account currency.
So an admin granting "50 EUR" grants 50 CZK — but only once a customer holds a non-default balance,
which is why this is not yet reachable.

Separately, the backend admin read is already correct: `GetUserCredit` returns `Accounts[]` with a
ledger each, pinned by an integration test. The Angular admin screen receives that array and renders
only `accounts[0]`.

## Acceptance criteria

1. The issue-credit command carries a currency, and the form makes the admin choose it.
2. The admin credit screen renders every account the customer holds.
3. A test covers granting into a non-default currency.

## Notes

Verified 2026-09-10. The backend half of the original claim was refuted; the screen half was confirmed.

Split out of **T-0688** by the multicurrency readiness audit of 2026-09-10. Every claim here was
re-verified against the tree that day, and the ones that did not survive verification were not filed.
