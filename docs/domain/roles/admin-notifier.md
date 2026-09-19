# Role — AdminNotifier (the administrators' feed and e-mail) (ADR-0065, accepted 2026-09-19) (CRC card)

> Introduced by **ADR-0065** (`docs/decisions/adr-0065.md`, **`accepted`** 2026-09-19; owner ruling on
> D5, 2026-09-19: *"both in-app and email"*). Shipped as T-0768 (the notifier's feed half, the admin feed
> routes, the first event), T-0774 (the e-mail channel, the template, the mailbox key), T-0775 (the seven
> remaining sites) and T-0769 (the admin web bell and page); the ninth event rides T-0770 (ADR-0067). The
> files that are the role: `Core.Domain/Notifications/AdminNotificationEventCatalog.cs` (the nine keys) ·
> `Core.AppServices/Features/AdminNotifications/AdminEventCatalog.cs` (per key: the audience and the exact
> arg set, in e-mail order) · `Core.AppServices/Services/{IAdminNotifier,AdminNotifier}.cs` ·
> `Core.AppServices/Services/EmailService.AdminNotification.cs` (the copy, five locales) ·
> `Core.Queue.Abstractions/Messages/SendAdminNotificationEmailMessage.cs` + `MessageKeys.AdminNotificationEmail` ·
> `Functions.Core/Handlers/SendEmailHandler.cs` (the discriminator arm) · `email-templates/admin-notification.html` ·
> `Web.Admin/Controllers/AdminNotificationController.cs` · the admin web page
> `libs/cleansia-admin-features/notifications` and the sidebar badge in `libs/core/admin-services`.

## Responsibility (one sentence)

Turn **one admin event** — a key, the company, a subject and the loc-args — into one `UserNotification`
row per eligible administrator of that **named** company and one e-mail intent per recipient address on
the `send-email` outbox, inside the caller's unit of work, so the rows exist iff the event committed, a
failing e-mail can never fail the command, and nothing the notifier reads depends on the ambient tenant.

## Collaborators

- **`AdminNotificationEventCatalog`** (Domain) — the nine `admin.*` keys and `All`; `NotificationFeedEventKeys.Admin`
  **is** `All`, so the feed audience `NotificationFeedAudience.Admin = 2` serves the catalogue by
  construction. Disjoint from the customer and partner keysets; `IsFeedEvent` does not know them, so the
  push seam cannot write an admin row. Every key maps to `null` in `GetCategoryFor`: no category, nothing
  to mute.
- **`AdminEventCatalog`** (AppServices) — one `Entry(Key, Audience, EmailArgOrder)` per key. `EmailArgOrder`
  is the **exact** set of arg names a site passes, in the order the copy substitutes `{n}`; the notifier
  throws on an undeclared or a missing arg, so the copy and the site cannot disagree on what `{1}` is and a
  site cannot smuggle a name in. `Audience` is the **name of an administrator set** (ADR-0066 D8) — never a
  policy, because the notifier filters rows, not principals: order events, disputes, payment failures and
  crew lost → `SupportOrAbove`; erasure failures → `ManagerOrAbove`; the three company milestones →
  `AdministratorOnly`; **`admin.dispute.chargeback` → `AdminOnly`, every role** — Support answers the bank,
  the Accountant reconciles the money that left, so the Accountant's bell is not empty by construction.
- **`AdminEvent(Key, TenantId, Subject, Args)`** — the call. `TenantId` is an **argument**; `Subject` is the
  dedup subject, unique per logical event across requests (below); `Args` never carries a person.
- **`IUserRepository.GetActiveAdministratorsAsync(tenantId)`** — `IgnoreQueryFilters()` with an explicit
  `TenantId == tenantId`: `Profile == Administrator && IsActive && IsEmailConfirmed && !anonymised`, ordered
  by id, projected to `(Id, Email, FirstName, LastName, PreferredLanguageCode, AdminRole)`. The notifier
  keeps the rows whose `AdminRole` is in **`AdminRoleSets.For(entry.Audience)`** — the same class the
  authorization handlers ask about a principal ([AdminRoleGate](./admin-role-gate)). An empty set after
  the filter is a logged warning naming the audience and a return, not an error.
- **`IUserNotificationRepository.Add`** — one `UserNotification.Create(admin.Id, key, argsJson, tenantId)`
  per recipient, exactly as `NotificationProducer` writes a client row; the args serialised once by the
  notifier's own serialiser. **`AnyForEventAsync(tenantId, key, argName, argValue)`** — tenant-ignoring,
  explicit predicate, a **jsonb containment** (`@>`) on the one `{arg: value}` pair; the guard the two
  sites that can repeat one logical event read before calling (a repeated card decline on one order; two
  overlapping archive builds of one frozen company).
- **`IAppConfigurationProvider.GetTenantSettingAsync(tenantId, key)`** + `TenantSettingReader.GetAsync(provider,
  tenantId, definition)` — the mailbox read **by argument** (`IgnoreQueryFilters().Where(TenantId == tenantId
  && Key == key)`), beside the ambient overload every other reader uses. The notifier never calls the
  ambient one: under a wrong override the feed rows would have gone to A's administrators and the e-mail —
  order number, amount, dispute id — to B's mailbox.
- **`TenantSettingCatalog.AdminNotificationEmail`** — `notifications.admin_email`, category `notifications`,
  an `EmailTenantSetting` (value type `Email = 3`; trimmed, lower-cased, one `@` with something on both
  sides, no whitespace, ≤ 150; the empty string is invalid — *unset* is `ResetTenantSetting`). Set → exactly
  that address, one message, English. Unset → every recipient of the administrators read, one message each,
  in their `PreferredLanguageCode` through `EmailLocale.Resolve`. The feed rows are written either way.
- **`IPendingDispatch.Enqueue(QueueNames.SendEmail, QueueEnvelope<SendAdminNotificationEmailMessage>)`** — the
  outbox: one row per address under `MessageKeys.AdminNotificationEmail(key, subject, email)` =
  `admin-email:{key}:{subject}:{sha256(address lower-cased)}`. The envelope carries the event's tenant, which
  the outbox row reads back. No `IQueueClient`, no `IEmailService`, no `INotificationProducer`, no
  `CommitAsync` — a unit test with a throwing `IPendingDispatch` stub is the only way the notifier can fail,
  and it asserts the feed rows were staged first.
- **`SendEmailHandler`** (Functions) — dual-reads the `admin-notification` discriminator before the frozen
  `SendEmailMessage` shape, act-then-claim on the deterministic key; a transport fault throws (retry, then
  poison), a malformed body acks. **`IEmailService.SendAdminNotificationEmailAsync(email, key, args, lang)`**
  renders `admin-notification.html` with the chrome keys plus `{key}.Subject` / `{key}.Body` (or
  `.BodyUnderWay` for a crew lost `OnTheWay`/`InProgress`; `cause` through `{key}.Cause.{value}`; an ISO
  instant as `dd.MM.yyyy HH:mm UTC`, a `yyyy-MM-dd` day as `d. M. yyyy`) — in-code defaults per locale under
  admin `EmailTemplateTranslation` rows for `EmailType.AdminNotification = 10`.
- **The nine sites** — each names the company from the subject it already holds, never from the override:
  `NewOrderAdminNotifier.NotifyIfOfferableAsync` (shared by `OrderFactory`, `HandlePaymentNotification`'s
  paid arm and `ConfirmRecurringOrder`; the same `OrderAvailability.IsOfferable` read as the preferred
  cleaner's offer; a null `TenantId` is a warning and nothing written) · `OrderCrewLostNotifier.NotifyAsync`
  (shared by `DropOrder` and `RejectEmployee`, whenever the crew emptied, subject `{orderId}:{releasedAssignmentId}`)
  · `CreateDispute` (subject the dispute id) · `HandlePaymentNotification`'s chargeback arms (subject the Stripe
  dispute id; the open customer dispute's id in the args when one exists) and its `payment_intent.payment_failed`
  arm (subject the order id, guarded by `PaymentStatus == Pending && CurrentStatus != Cancelled && !AnyForEventAsync`)
  · `RetryFailedUserDeletions` (subject `{requestId}:{day}`, in a fresh scope with the override set and its own
  commit — the failing walk's scope is discarded by design) · `WindDownCompany` (only when a date is set;
  subject `{tenantId}:{requestInstant}`) · `CompanyWindDownService` (only when `cancelled + refunded +
  refundFailures + periodsClosed > 0`; subject the run instant **to the tick**) · `CompanyArchiveService.MarkArchived`
  (subject `{tenantId}:{freezeInstant}`, guarded by `AnyForEventAsync` on the day; the one event written on a
  frozen company — admitted because `UserNotification` and `OutboxMessage` are on
  `ArchivedCompanyWriteGuard.AccountSurface`).
- **`AdminNotificationController`** — `GET api/AdminNotification/get-paged`, `GET …/unread-count`,
  `POST …/mark-read`, `POST …/mark-all-read`, every route `Policy.CanViewAdminNotifications` (`AdminOnly`),
  the audience enriched to `Admin` server-side. `MarkNotificationRead.Command` and
  `MarkAllNotificationsRead.Command` carry `[AuditAction(Audited = false)]`: a bell click is not a ledger
  entry, and the same attribute stopped the admin audit rows an administrator used to write by marking read
  in the partner app's feed.
- **The admin web** — `AdminNotificationBadgeService` polls `unread-count` every 60 s while the document is
  visible and stamps the badge on the sidebar's *Notifications* entry (first in the list); the
  `/notifications` page renders each row from `pages.notifications.events.<key>.*`, deep-links by event
  family (order/payment → order detail, dispute → dispute detail, erasure → data protection, company →
  company lifecycle), marks read on click, and marks all read up to the newest row's `createdOn` plus one
  millisecond (the column is microsecond-resolution).

## Does NOT know

- **The ambient tenant.** Every read is a function of `AdminEvent.TenantId`; a Testcontainers test raises an
  event for A under an override set to B, with a mailbox set on B, and asserts the rows are A's
  administrators' and the outbox row is addressed to A's fallback, never B's mailbox.
- **What an event means, or whether it repeats.** The subject rule and any cross-request guard are the
  caller's; the notifier dedups nothing.
- **How e-mail is rendered or sent, or what a push is.** It writes an intent; the consumer renders; nothing
  here touches SendGrid or FCM.
- **Whether the caller will commit.** A `*Command` handler's pipeline or a job's own `CommitAsync` inside its
  loop lands the rows with the business state.
- **What a role may do.** It knows which *set* an entry names and filters rows by membership; which
  permissions that set carries is the map's business (ADR-0066 D3), never read here.
- **A preference.** Admin keys map to no category; `UserNotificationPreferences` is a customer/partner mute
  table.

## Invariants a reviewer checks

1. **`AdminNotifier` calls neither `INotificationProducer`, `IEmailService`, `IQueueClient` nor any
   `CommitAsync`, and never the ambient `GetTenantSettingAsync(key)`** — the collaborator list is five, and
   the throwing-stub test is the only failure path.
2. **Both reads are `IgnoreQueryFilters()` with `TenantId == tenantId` in the predicate**
   (`UserRepository.GetActiveAdministratorsAsync`, `AppConfigurationProvider.GetTenantSettingAsync(tenantId, key)`).
3. **The three key lists are one**: `AdminNotificationEventCatalog.All` = `AdminEventCatalog`'s keys =
   `NotificationFeedEventKeys.Admin`, disjoint from the client keysets, every key `admin.*`, every key `null`
   in `GetCategoryFor`, every key with `title`/`body` in five admin locales and `Subject`/`Body` in five
   e-mail locales.
4. **No arg is a person.** The catalogue guard fails an arg named like contact identity; every site passes
   exactly `EmailArgOrder` and the notifier throws otherwise.
5. **Each site calls once, inside the business write's unit of work, and a redelivery adds nothing**: the
   Stripe short-circuit for the webhook arms; the candidate predicate for the daily retry; the counts guard
   for the wind-down run; `AnyForEventAsync` for the decline and the archive.
6. **The four admin routes answer 401 anonymous, 403 to an Employee token, 200 to an administrator**; a
   mark-read on another administrator's row is refused; a mark-read writes **no** `AdminActionAudits` row.
7. **The settings page's `Email` branch** refuses a malformed address client-side with the server's
   `tenant_setting.invalid_value` key and shows *every administrator* for the default.

## Watch-list

- **The audience narrowing** (ADR-0066 D8) is one filter in the recipients step through the entry's
  `Audience` — shipped with T-0748, no second design. A new event picks a set, not a role; an
  Accountant-area event (a pay period closed, a payout PDF failed) would be the first `AccountantOrAbove`
  entry.
- **A per-event `Channels` flag** is the first follow-up if the owner overrules O-5 (feed-only for
  `admin.order.new` until a mailbox is set); not a digest.
- **A `DeadLetter` row tells nobody** — the candidate tenth event, not this role's.
- **The shared mailbox is English**; a language key for it is the "do nothing" answer until asked.
