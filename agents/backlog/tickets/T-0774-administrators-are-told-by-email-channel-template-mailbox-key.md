---
id: T-0774
title: Administrators are told by e-mail — the channel, the template, the mailbox key (ADR-0065 D2 step 4, D3)
status: todo
size: M
owner: —
created: 2026-09-19
updated: 2026-09-19
depends_on: [T-0768]
blocks: [T-0775]
stories: []
adrs: [ADR-0065]
layers: [backend, frontend]
security_touching: true
manual_steps: []
sprint: —
---

## Context

ADR-0065's e-mail half (D2 step 4, D3), split off T-0768 by the panel (challenge B11). Every event T-0768
and T-0770 already raise gains its e-mail twin here without touching their code: the notifier's e-mail
step is the seam T-0768 left as a no-op. The mailbox key is read **by argument** (challenge B1): the
ambient `GetTenantSettingAsync(key)` (`AppConfigurationProvider.cs:8-15`) would have sent company A's order
numbers to company B's mailbox under a wrong override.

## Doing (backend)

- `IAppConfigurationProvider.GetTenantSettingAsync(tenantId, key)` (tenant-ignoring, explicit predicate) and
  `TenantSettingReader.GetAsync(provider, tenantId, definition)`.
- `TenantSettingValueType.Email = 3`; `EmailTenantSetting` (trim, lowercase, `MailAddress.TryCreate`, ≤ 150,
  the empty string invalid — "unset" is `ResetTenantSetting`); `TenantSettingCatalog.AdminNotificationEmail`
  (`notifications.admin_email`, category `notifications`); the shared-mailbox / every-administrator rule in
  `AdminNotifier` (the seam T-0768 left).
- `EmailType.AdminNotification = 10`; `email-templates/admin-notification.html`;
  `IEmailService.SendAdminNotificationEmailAsync`; five-locale in-code defaults per event for **all nine
  keys**, layered under `EmailTemplateTranslation`; `SendAdminNotificationEmailMessage` +
  `MessageKeys.AdminNotificationEmail`; the `SendEmailHandler` discriminator arm (act-then-claim).
- `CompanyWindDownService.ResolveLocale` hoisted (second caller).
- Tests per ADR-0065 §Verification 1 (the throwing-stub test), 2 (the outbox `Email` assertion), 3, 4, 6
  (the redelivery assertion for the two events already raised), 10; the admin NSwag client regenerated
  (the new value type crosses the wire) and committed.

## Doing (web)

- Company settings page: the `Email` value-type branch (`<cleansia-text-input dataType="email">`), *"every
  administrator"* for the empty default, the `notifications` category label, the setting's description key,
  client-side format check mirroring the server key; five locales; facade spec.

## NOT

- No per-event channel switch (O-5 decides whether one is needed). No digest. No language key for the
  shared mailbox. No new events (T-0775).

## Done looks like

Every event T-0768 and T-0770 already raise also produces one outbox e-mail per recipient address (or one
to the shared mailbox); the Functions consumer renders and sends from the new template; the settings page
accepts a valid address and refuses an invalid one with the server's key; three backend projects green.

## Acceptance criteria

- [ ] **AC1** — *Given* company A with two administrators, *when* a dispute is filed, *then* two outbox rows
      on `send-email` with distinct keys exist, each addressed to an administrator in their preferred
      language.
- [ ] **AC2** — *Given* `notifications.admin_email` set to `ops@example.com` for A, *when* any admin event of
      A fires, *then* exactly one outbox row exists, addressed there, `LanguageCode = en`, and the feed rows
      are still one per administrator.
- [ ] **AC3** — *Given* a mailbox set on company **B** and an event for company **A** raised under an ambient
      override of B, *then* the outbox row is addressed to A's administrators, never to B's mailbox
      (Testcontainers).
- [ ] **AC4** — *Given* `SetTenantSetting("notifications.admin_email", "not an address")`, *then*
      `tenant_setting.invalid_value`; *given* `""`, *then* `common.required`; *given* `Reset`, *then* the row
      is gone and the fallback applies.
- [ ] **AC5** — The `SendEmailHandler` arm throws on a SendGrid fault and acks a malformed body; the rendered
      subject and body for `admin.order.crew_lost` in `cs` contain the order number and the time and say
      the clean was under way when `statusAtLoss = OnTheWay`.
- [ ] **AC6** — *Given* the settings row with no override, *then* the value column reads *"every
      administrator"*; *when* `ops@example.com` is saved, *then* the row reads it and *Source = overridden*;
      *when* reset, *then* the default text returns; *given* `not-an-address` typed, *then* the save is
      refused and the message maps `api.tenant_setting.invalid_value`.

## Implementation notes

ADR-0065 D2 step 4 (the second message shape on the `send-email` queue and why — the frozen shape's one
payload slot is a secret by contract, `SendEmailHandler.cs:74`, and a shared-mailbox send has no row to
re-read), D3 (the recipient rule and the key), §Residuals (two silent ends on the e-mail path — the feed is
the surviving channel). The admin NSwag regen is the lane's to run and report.

**Security (Gate 3) — `security_touching: true`, routed to the Security Reviewer beside the code
reviewer.** This is the ticket the panel's B1 hole lives in: a **tenant-ignoring read**
(`GetTenantSettingAsync(tenantId, key)`) that must take the company from the event's argument and never
from the ambient override (AC3 is the executable assertion — a mailbox set on B must never receive A's
order numbers); a **new outbound e-mail channel** whose bodies carry order numbers, amounts and dispute
ids to an address an administrator typed (S6 — the address is validated on the way in, the body is
rendered from catalogue args only, never free text); a **new tenant-setting value type crossing the wire**
(`TenantSettingValueType.Email`) under the existing `CanUpdateTenantConfiguration` policy; and a second
message shape on the `send-email` queue whose consumer must ack a malformed body rather than poison the
queue (AC5). The reviewer walks S1/S6/S8 against the diff.

## Status log

- 2026-09-19 — filed by the docs lane from the batch-6 panel; runs after T-0772 on the backend lane, then
  the admin web part.
- 2026-09-19 (review) — `todo`, no owner: it was filed `in_progress` for a lane that does not exist yet;
  the backend lane flips it when it picks the ticket up. `security_touching` corrected to `true` — a
  tenant-ignoring read, an outbound e-mail channel and a wire-crossing value type are each on Gate 3's
  own list; the routing note above says what the Security Reviewer looks at.
