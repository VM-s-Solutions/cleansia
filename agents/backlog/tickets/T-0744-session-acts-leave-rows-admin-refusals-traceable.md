---
id: T-0744
title: Sign-in and session acts leave customer audit rows; admin refusals are traceable by order (L5, O2)
status: done
size: M
owner: —
created: 2026-09-14
updated: 2026-09-14
depends_on: []
blocks: []
stories: []
adrs: [ADR-0062, ADR-0061]
layers: [backend, frontend]
security_touching: true
manual_steps: []
sprint: 16
---

## Context

Owner rulings 2026-09-14. **L5** "overruled variant" — record login/session history beyond the
90-day refresh-token window. **O2** *"keep it … the only question is if we can trace the order
somehow — the reason is worth nothing if I can't trace the failed order."*

## Doing

- Markers (Customer audience, `AllowsAnonymousActor = true` where the actor has no session yet):
  `Login` → `customer.session.login` (`LoginEvidence`; failure keys `auth.invalid_credentials`, …);
  `GoogleAuth`/`AppleAuth` sign-in of an existing account → `customer.session.login` with the method
  (the provisioning branch unrecorded); `Logout` → `customer.session.logout`; `RefreshToken` not
  recorded; `RequestPasswordChange` → `customer.password.reset_requested` (no email on the row);
  `ChangePassword` → `customer.password.reset_completed`; `ConfirmUserEmail` →
  `customer.account.email_confirmed`.
- **Host gate:** an anonymous act lands in the customer table only when the serving host is a customer
  host; a cleaner's login on a partner host lands nowhere. `AuditGate.Resolve` takes the host audience.
- **Unknown email:** a failed login / reset request for an unknown address writes a row with
  `UserId = null`, the key, IP + device — never the email (`PayloadJson` null on failure rows).
- Retention and erasure unchanged (same table). Every newly marked action behind a rate-limit policy.
- **O2:** an admin refusal on order X writes an admin failure row with `ResourceType = "Order"`,
  `ResourceId = X`; `GetActionTimeline` by resource returns it; the admin list filters by `resourceId`;
  fix any admin order/dispute descriptor whose failure row lacked the id.
- Five admin locale labels for the new actions.

## NOT

No refresh-token rows; no employee session rows; no change to the auth flows themselves.

## Status log

- 2026-09-14 — shipped in `048e6efb` (`Login`, `MobileLogin`, `GoogleAuth`, `AppleAuth` →
  `customer.session.login` with `LoginEvidence(method, rememberMe, clientAudience, emailConfirmed)`;
  the social provisioning branch calls the new `IAuditContext.DeclineSuccessRow()`; `Logout`,
  `RequestPasswordChange`, `ChangePassword`, `ConfirmUserEmail` marked; `AuditGate.Resolve` takes
  `IHostAudienceProvider`; the five anonymous session commands implement `IOperatorScopedRequest`
  with `CountryId => null`; `AdminCancelOrder` `order.cancel`, `AdminReassignOrder` `order.reassign`,
  `UpdateDisputeStatus` `dispute.status.update`, `AddDisputeMessage` `dispute.message.add` with a
  `ResourceType`; roster 25 commands / 21 labels / 9 anonymous; host tests on the customer, partner and
  admin hosts) and `f8e909d9` (a session act's **success** row is stamped with the account's own
  operator — `TokenService` adopts it before the confirmation check, the two reset handlers adopt it
  themselves; `SessionAuditTests` under the second operator; the roster guard that every
  `AllowsAnonymousActor` marker sits on an `IOperatorScopedRequest`). Reported, not built: a refused
  sign-in on a **known** account is `UserId = null` — attributable by IP only (Q-AUD-O4); an
  Administrator's sign-out on the admin host lands in the admin table under the frozen
  `customer.session.logout` label (a ratification question). Recorded in ADR-0062 D3/D6/D7 and
  ADR-0061 D3/D4 as amended.
