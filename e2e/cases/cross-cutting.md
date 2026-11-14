# Cross-cutting — cases

What belongs to no single flow: which operating company owns a row, how a change reaches the other roles as a notice, the five locales of every app, and the security rules a user or a tester can see from outside. Jobs, the outbox and dead letters are listed under *Not testable on DEV*.

Ids are `E2E-XCUT-NNN`. DEV has one operating company (`cleansia-cz`) and one default market (CZ), so every tenancy case checks that things *carry* the company, and none can check isolation between two companies. A refusal that must look like "not found" is checked against the answer for an id that does not exist. Admin roles: Support, Accountant, Manager and Administrator, as in the sets named by `AdministratorOnly` ⊂ `ManagerOrAbove` ⊂ (`SupportOrAbove` ∪ `AccountantOrAbove`).

## Tenancy and markets

### E2E-XCUT-001 — Every token carries the company the account belongs to
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API
- **Rule:** → /flows/cross-cutting#tenancy — an authenticated request's tenant is the JWT's `tenant_id` claim, minted from the user's own row
- **Given** a customer, a cleaner and an Administrator. **When** each signs in on its own host (customer API, partner API, admin API, and both mobile hosts) and the access token is decoded. **Then** every token has a `tenant_id` claim, all are the same value on DEV, and no response body and no URL carries a tenant id.
- **DEV:** reading a token's claims needs no secret; the value is not asserted, only that it is present and equal.

### E2E-XCUT-002 — A registration that names no market lands in the default market
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API · Web
- **Rule:** → /product/business-rules#market — an anonymous write names a market, never a tenant; with none named, the default market decides
- **Given** the market list from `GET api/Market/GetOverview` (anonymous). **When** a customer registers once with `countryId` of the default market and once with none. **Then** both accounts are created, both sign in with a token whose `tenant_id` is the same, and the list showed only markets that are serviced, have an active currency and an operating company, with CZ flagged default.
- **DEV:** two run accounts.

### E2E-XCUT-003 — A country that is not a serviced market is refused on an anonymous write
- **Priority:** P2
- **Role:** Guest
- **Surfaces:** API · Web
- **Rule:** → /flows/cross-cutting#tenancy — a country that is not a market is `country.not_serviced`
- **Given** the id of a seeded country that is not in `Market/GetOverview` (for example a country the platform lists for addresses only). **When** a guest registers naming it, then sends a promo-code request naming it. **Then** both answer `country.not_serviced`, no account exists afterwards, and the refusal is a row with the caller's IP in the audit trail (not readable by the test).
- **DEV:** which seeded countries are not serviced is read from the country list at run time; with none, the case is not run.

### E2E-XCUT-004 — The market list shows only serviced markets with a currency and an operator
- **Priority:** P2
- **Role:** Guest
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /product/business-rules#market — a market is a serviced country with an active currency and an operating company
- **Given** no sign-in. **When** the home page of the customer web, and the customer apps' Profile, Preferences, Market screen, are opened. **Then** the market chip reads the country code beside the currency code ("CZ · CZK") as a static label because there is one market; each price on the home catalogue is labelled with that currency; the apps' market screen lists the same markets as `GetOverview`.
- **DEV:** with one market no selector is offered; the selector and the `preferred_market` cookie are listed as not testable.

### E2E-XCUT-005 — A booking and the account it belongs to show the same company to the admin
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /flows/cross-cutting#tenancy — a row gets its tenant from the ambient company at commit time, so a customer's order and the admin's view agree
- **Given** a customer's booking made as a guest and one made signed in (BOOK journey). **When** a Support admin lists orders (`GET api/AdminOrder/get-paged`) and opens both. **Then** both are listed and readable; none is missing because of a tenant filter, and no detail response carries a tenant id.
- **DEV:** also proves that an anonymous write is stamped with the market's operator, since a guest order not stamped would not be listed at all.

### E2E-XCUT-006 — A cleaner's work country must be served by the company that approves them
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /product/business-rules#market — a cleaner must be approved for a work country that the company serves (`employee.work_country_operator_mismatch`)
- **Given** a registered cleaner whose work country is a serviced market of the DEV company. **When** an admin approves them. **Then** the approval passes the work-country check; the mismatch key itself cannot be reached with one company (see the last section).
- **DEV:** a positive control only; the refusal is not reachable.

## Notifications

### E2E-XCUT-007 — A new offerable order raises an admin notice for Support and above, not for Accountant
- **Priority:** P1
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /product/business-rules#admin-notifications — `admin.order.new` reaches Support and above, once the order is offerable, as an Info row
- **Given** a Support admin and an Accountant admin signed in to the admin web. **When** a customer books a cash order (BOOK journey). **Then** the Support admin's feed (`GET api/AdminNotification/get-paged`) holds an `admin.order.new` row carrying the order number, amount with currency, tender and market; the Accountant's feed has none; the row is Info, so the bell's unread count (`unread-count`) does not rise.
- **DEV:** the Administrator and Manager roles are in the audience too; a card booking raises it only on payment.

### E2E-XCUT-008 — The bell counts critical unread rows only, and the tabs filter
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /product/business-rules#admin-notifications — the badge counts critical unread rows; the page has All, Critical and Info; the paged read takes a `critical` flag
- **Given** a feed with at least one Info row (XCUT-007) and one Critical row (XCUT-010). **When** the admin opens the Notifications page and reads `get-paged` with `critical=true`, `critical=false` and no flag. **Then** the flag returns the critical keys, the info keys and all in turn; the bell count equals the critical unread only; the Info tab hides *Mark all as read*; each row shows its type in words and a critical row carries a red *Critical* label.
- **DEV:** none.

### E2E-XCUT-009 — Each administrator has their own read state
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /product/business-rules#admin-notifications — one feed row per administrator, so the first one to glance at the bell does not silence it for the others
- **Given** two administrators with the same audience and one unread row each. **When** the first marks the row read (`POST api/AdminNotification/mark-read`) and then all read. **Then** the first's unread count falls; the second's is unchanged until they read it.
- **DEV:** needs two admins in the audience.

### E2E-XCUT-010 — A cleaner dropping a confirmed order tells the administrators and the customer
- **Priority:** P2
- **Role:** Cleaner
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /product/business-rules#admin-notifications — a drop that leaves nobody on the order raises the Critical `admin.order.crew_lost`
- **Given** a `Confirmed` order with one cleaner on it (TAKE journey). **When** the cleaner drops it. **Then** the admins in the Support audience get a Critical `admin.order.crew_lost` row with the order number, the cause, the status at the loss and the slot, and the order is `New` again; the customer's mobile feed holds an `order.cleaner_released` row.
- **DEV:** the drop is refused once the job is under way; the order must be `New` or `Confirmed`.

### E2E-XCUT-011 — The customer's feed follows the order through its steps
- **Priority:** P1
- **Role:** Customer
- **Surfaces:** API · iOS · Android
- **Rule:** → /flows/cross-cutting#notifications — one producer writes the in-app row in the same unit of work as the change that caused it
- **Given** a customer's order taken by a cleaner, then started, then completed (EXEC journey). **When** the customer reads the feed (`GET api/Notification/Paged` on the mobile host, and the Notifications screen of the iOS app and the inbox of the Android app). **Then** the feed holds, in order, `order.cleaner_assigned`, `order.in_progress` and `order.completed` (and `order.on_the_way` when the cleaner pressed it); the unread count rises by that many and falls to 0 after Mark all read.
- **DEV:** the customer web has no notification feed in the tree; the web case is not run.

### E2E-XCUT-012 — Mark read and Mark all read change only the caller's rows
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API · iOS · Android
- **Rule:** → /architecture/security-rules#s3-—-resource-by-id-endpoints-must-check-ownership — every by-id write checks that the row belongs to the caller
- **Given** two customers, each with unread rows. **When** the first marks one of their rows read, then sends `MarkRead` with an id of the second customer's row, then Mark all read. **Then** the first's count falls to 0 only after the last call; the second's rows stay unread throughout; the foreign id is refused or ignored and changes nothing.
- **DEV:** the exact answer for a foreign id is not in the docs; record it.

### E2E-XCUT-013 — A customer's feed never holds another customer's order
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API · iOS · Android
- **Rule:** → /flows/cross-cutting#notifications — the notice is written for the recipient's own persisted account, never inherited from the ambient context
- **Given** customer A with a taken order and customer B with none. **When** B reads the feed after A's order changed state. **Then** B's list is empty or holds only B's own rows, and no row names A's order number.
- **DEV:** none.

### E2E-XCUT-014 — The cleaner's feed carries the cleaner's own events
- **Priority:** P2
- **Role:** Cleaner
- **Surfaces:** API · iOS · Android
- **Rule:** → /flows/cross-cutting#notifications — a partner notice is written for the recipient's own account, with the same producer and shape as the customer's
- **Given** a cleaner who has been assigned a job by an administrator (reassign). **When** the cleaner reads the feed (`GET api/Notification/Paged` on the partner mobile host). **Then** a row about the assignment exists, unread, and Mark read clears it; another cleaner's feed has none.
- **DEV:** the exact event key is not in the docs; record it.

### E2E-XCUT-015 — The in-app feed is written in the language chosen in the app
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** iOS · Android
- **Rule:** → /architecture/push-notifications#language — the feed text is drawn on the device from the key and arguments in the language picked in the app
- **Given** a feed with an `order.cleaner_assigned` row and the app set to English. **When** the customer switches the app language to Czech, then Russian. **Then** the same row is rewritten in each language with the same order number and no raw key or `push.` string visible.
- **DEV:** a push banner cannot be checked on a simulator (see the last section); the feed can.

### E2E-XCUT-016 — The notification preferences round-trip
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API · Web
- **Rule:** → /architecture/push-notifications#event-catalogue — each event key maps to a per-user opt-in category
- **Given** a signed-in customer. **When** the customer reads `GET api/NotificationPreferences/GetMine`, switches one category off with `PUT api/NotificationPreferences/Update`, and reads again. **Then** the second read shows the category off and the rest unchanged; a second account is not affected.
- **DEV:** whether the customer web or the mobile apps show a screen for it is unsure; the API round trip is certain. Whether the push is then withheld cannot be seen (no push on DEV).

## Locales

### E2E-XCUT-017 — Every app offers the same five languages
- **Priority:** P2
- **Role:** Guest
- **Surfaces:** Web · iOS · Android
- **Rule:** → /architecture/frontend#backend-error-keys-resolve-under-api — five locale files in every app (en, cs, sk, uk, ru)
- **Given** the customer web, the partner web, the admin web, the iOS and Android customer and partner apps, on their sign-in screens. **When** the language picker is opened in each. **Then** it lists English, Czech, Slovak, Ukrainian and Russian, and nothing else.
- **DEV:** whether the admin web has a picker on its sign-in screen is unsure; if it has none, record where the language is chosen.

### E2E-XCUT-018 — Switching language leaves no raw key on the main screens
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** Web · iOS · Android
- **Rule:** → /architecture/frontend#backend-error-keys-resolve-under-api — every user-visible string is translated in all five locales
- **Given** each app signed in. **When** the sign-in, home, orders list, order detail and profile screens are screenshotted in each of the five languages. **Then** no screen shows a dotted key (`api.…`, `errors.…`, `push.…`), the literal `undefined`, or a placeholder such as `{{x}}`; the page title, button labels and dates change with the language.
- **DEV:** screenshots are the evidence; untranslated English on a Czech screen is recorded as a finding for the triager.

### E2E-XCUT-019 — A backend refusal is shown translated in each locale
- **Priority:** P1
- **Role:** Guest
- **Surfaces:** Web · iOS · Android
- **Rule:** → /architecture/frontend#backend-error-keys-resolve-under-api — a backend key resolves under `api.*` in all five locales of every app that can reach the endpoint
- **Given** the sign-in screen of each app. **When** a known e-mail is submitted with a wrong password, in each of the five languages. **Then** the screen shows a sentence in that language for `validation.invalid_password`, never the generic "An error occurred" (`api.common.error_occurred`) and never the key itself.
- **DEV:** use an unknown e-mail on the customer host so that no real account takes a failed-login count; if the app reports the account as locked (`auth.account_locked`), that sentence must be translated too.

### E2E-XCUT-020 — The account's language follows the picker and survives a new session
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /flows/auth-and-identity#account-language — a change in the app's picker is sent to the server, and a new session re-states the language the user chose
- **Given** a password sign-up made while the app showed Czech, and then a picker change to Ukrainian. **When** the profile is read (`GET api/User/GetCurrent`) and the user signs out and in again. **Then** the profile's `preferredLanguageCode` is `cs` after the sign-up, `uk` after the change and still `uk` after the new session; on "System" the server's value is not overwritten.
- **DEV:** the same rule holds for a cleaner on the partner apps (run once per app).

### E2E-XCUT-021 — A language code the server does not know is refused
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API
- **Rule:** → /flows/auth-and-identity#account-language — the preferred language is a foreign key onto `Languages.Code`, so only a seeded code is accepted
- **Given** a signed-in customer. **When** the profile is updated with the code `xx`. **Then** the update is refused with a validation error naming the language (`language.not_found` is the key the language checks use) and the stored language is unchanged.
- **DEV:** unsure which exact key the profile update emits; record it.

## Authorization: who may do what

### E2E-XCUT-022 — A protected endpoint answers 401 to an anonymous caller
- **Priority:** P2
- **Role:** Guest
- **Surfaces:** API
- **Rule:** → /flows/cross-cutting#authorization-posture — the default policy requires an authenticated user; `[AllowAnonymous]` is the explicit opt-out
- **Given** no token. **When** one protected route on each host is called (customer `Order/GetMyOrders`, partner `Order/GetPaged`, admin `AdminPayPeriod/get-paged`, and the two mobile hosts' `Notification/Paged`). **Then** each answers 401 with no data; the public reads (`Market/GetOverview`, the legal text read) answer 200 without a token.
- **DEV:** none.

### E2E-XCUT-023 — A token for one audience does not work on another host
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API
- **Rule:** → /architecture/security-rules#s2-—-authorization-on-every-endpoint — every action carries `[Permission]` or an explicit anonymous or authenticated attribute, and the server is the gate
- **Given** a customer token, a cleaner token and an Administrator token. **When** the customer token calls an admin route and a partner route; the cleaner token calls an admin route and a customer route. **Then** every call answers 401 or 403, never 200, and returns no body with data; the Administrator's token passes the admin routes.
- **DEV:** whether the wrong audience answers 401 or 403 is not fixed in the docs; record which, and report a 200.

### E2E-XCUT-024 — Admin sign-in refuses a customer and a cleaner
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API · Web
- **Rule:** → /architecture/security-rules#s2-—-authorization-on-every-endpoint — the admin host serves administrators only
- **Given** a confirmed customer and a confirmed cleaner. **When** each signs in at the admin web or `POST api/AdminAuth/Login` with the right password. **Then** both are refused `auth.insufficient_privileges` and no token is issued; an unknown e-mail answers `validation.invalid_password`, the same as a wrong password. The admin web shows both in the user's language.
- **DEV:** none.

### E2E-XCUT-025 — Partner sign-in refuses a customer
- **See** E2E-AUTH-018: the same assertion, kept once in the file whose flow page owns the rule.

### E2E-XCUT-026 — The admin roles gate the server, and the web only hints
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /architecture/security-rules#administrator-roles-adr-0066-accepted-2026-09-19 — the server resolves each `[Permission]` to a set of roles and answers 403 outside it; the admin web mirrors the map
- **Given** one admin of each role. **When** each calls: `GET AdminOrder/get-paged` (Support and above), `GET AdminPayPeriod/get-paged` (Accountant and above), `POST AdminCashHeld/write-off` (Manager and above) and `POST AdminUser/create` (Administrator only). **Then** the answer is 403 exactly for the roles outside each set — Accountant on the orders, Support on the pay periods, Support and Accountant on the write-off, and Support, Accountant and Manager on creating an admin; the sidebar shows only the entries the role holds, and opening a forbidden URL in the browser lands on `/unauthorized`.
- **DEV:** needs one admin per role, created by the Administrator; use a business-invalid body for the writes so that nothing changes when the role passes.

### E2E-XCUT-027 — The customer's phone and address are behind the role on the admin host
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /architecture/security-rules#administrator-roles-adr-0066-accepted-2026-09-19 — a read the admin host shares with a partner host gets an `…Admin` twin so the role can gate it
- **Given** an order with a customer's phone and address, an Accountant and a Support admin. **When** each opens the order detail (`GET api/AdminOrder/details/{id}` or the page). **Then** Support reads the customer's contact and address; the Accountant is refused 403.
- **DEV:** the route name is taken from the controller at run time.

### E2E-XCUT-028 — The last administrator cannot be demoted or deactivated
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /architecture/security-rules#administrator-roles-adr-0066-accepted-2026-09-19 — a last-of-kind guard under the company's advisory lock
- **Given** a company with exactly one active Administrator (otherwise do not run). **When** that Administrator sets another role on their own account, and a second admin tries to deactivate them. **Then** the answers are `admin_user.cannot_demote_last_administrator` or the self-role refusal, and `admin_user.cannot_deactivate_last_admin`; the account stays active.
- **DEV:** DEV may hold more than one Administrator; if so, create the single-Administrator state on a throwaway run or skip. Never demote the published admin.

### E2E-XCUT-029 — Every admin change leaves an audit row with the actor's role and no personal data
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /architecture/security-rules#s2-—-authorization-on-every-endpoint — every admin mutation leaves an append-only `AdminActionAudit` row carrying the role it ran under
- **Given** an Accountant who has just closed a period (PAYOUT-016) and approved an invoice (PAYOUT-039). **When** a Support admin opens the audit log (`GET api/AdminAuditLog/get-paged`, `get-by-id`). **Then** both acts are listed with the actor's e-mail, the role `Accountant`, an action label and a time; the Close now row holds the old and new end and the reason, and no row holds a customer's name, address, card data or a token; an Accountant gets 403 from the log (it is Support and above).
- **DEV:** the action labels are read from the log at run time.

### E2E-XCUT-030 — Payout details are masked for the admin and the full account is a separate audited act
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /flows/pay-and-payouts#payout-details-never-ride-an-employee-dto — the admin list and detail carry a masked account only; the reveal is an audited command
- **Given** a cleaner with payout details. **When** an Accountant reads `GET api/AdminEmployee/{id}/payout-details`, then a Manager calls the reveal (`POST api/AdminEmployee/{id}/payout-details/reveal`), and the Accountant calls it. **Then** the first answer shows a masked account and no unmasked field exists in the JSON; the Manager's reveal returns the full account and an audit row appears; the Accountant's reveal is 403.
- **DEV:** the invoice PDF prints the full account on purpose (accepted in ADR-0034 D6), so it is not a finding here.

## Ownership: one user cannot reach another's data

### E2E-XCUT-031 — A customer cannot read another customer's order
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API
- **Rule:** → /architecture/security-rules#s3-—-resource-by-id-endpoints-must-check-ownership — a cross-user access answers not-found so that the existence of the resource is not confirmed
- **Given** customer A's order and a signed-in customer B. **When** B calls `GET api/Order/GetById`, `CancellationPreview`, `GetPhotos` and `DownloadReceipt` with A's order id, then with a made-up id. **Then** every call answers `order.not_found` (`receipt.not_found` can appear for a receipt not yet issued on the made-up id), the two answers are indistinguishable in body and status, and B's order list never contains A's order.
- **DEV:** the apps give no way to type an id, so only the API is run.

### E2E-XCUT-032 — A customer cannot cancel, review or dispute another customer's order
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API
- **Rule:** → /architecture/security-rules#s3-—-resource-by-id-endpoints-must-check-ownership — ownership is checked in the handler, so it holds on every host
- **Given** customer A's `New` order and customer B. **When** B calls `POST api/Order/Cancel`, `SubmitReview` and `ReportIssue` with A's order id on both the customer web host and the customer mobile host. **Then** every call answers `order.not_found` (or `dispute.not_owned_by_user` for a dispute) and A's order is unchanged, no fee is charged and no refund is raised.
- **DEV:** none.

### E2E-XCUT-033 — A guest booking is visible only to the e-mail it was made with
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API · Web
- **Rule:** → /architecture/security-rules#s3-—-resource-by-id-endpoints-must-check-ownership — listed guest access is a narrow permission for a confirmed session whose e-mail matches
- **Given** a guest booking made with e-mail X, a confirmed customer account with X, and a confirmed customer with another e-mail Y. **When** each opens My orders and tries to open and cancel that booking by its id. **Then** the account with X lists it, redacted, and can cancel it under guest terms; the account with Y does not list it and gets `order.not_found` on open and cancel.
- **DEV:** the guest token flow itself is the BOOK journey.

### E2E-XCUT-034 — A guest token opens only its own booking, and an unknown one answers not-found
- **Priority:** P2
- **Role:** Guest
- **Surfaces:** API · Web
- **Rule:** → /flows/booking-and-pricing#guest-order-lookup — an unknown, expired or revoked token and a booking that belongs to an account all answer `order.not_found`
- **Given** a guest booking token, a garbled token, and the id of a registered customer's order sent in place of a token. **When** each is sent to `POST api/Order/Lookup`. **Then** the real token returns the redacted booking (no address, no crew); the other two answer `order.not_found`, identical to each other; after the booking is cancelled the real token answers `order.not_found` too.
- **DEV:** the real token comes from the checkout response, since e-mail is not readable.

### E2E-XCUT-035 — A cleaner cannot start, complete or collect on a job that is not theirs
- **Priority:** P2
- **Role:** Cleaner
- **Surfaces:** API
- **Rule:** → /flows/execution-and-completion#only-the-assigned-cleaner-may-move-the-job — `StartOrder`, `CompleteOrder` and `NotifyOnTheWay` each require the caller to be assigned to this order
- **Given** a `Confirmed` order held by cleaner A and a second approved cleaner B. **When** B calls `StartOrder`, `NotifyOnTheWay`, `CompleteOrder` and `MarkCashCollected` on it. **Then** each answers `order.employee_not_assigned` and the order's status, crew and cash ledger are unchanged; on an order that is not `Confirmed` the start answers `order.not_confirmed` first.
- **DEV:** the second cleaner is a run account.

### E2E-XCUT-036 — A cleaner who has not taken the job sees the job, not the household
- **Priority:** P2
- **Role:** Cleaner
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /flows/execution-and-completion#what-a-cleaner-who-has-not-taken-the-job-can-see — the board redacts the customer's name, e-mail, phone, address, coordinates, free text and the crew's phones
- **Given** an open job on the board and a cleaner who has not taken it. **When** the cleaner reads the list and the detail (`GET api/Order/GetPaged`, `GetById`), and opens the card in each app. **Then** the JSON holds `customerAddressApproximate` (a district or city, no postcode) and `homeDistanceBand`, and no customer name, e-mail, phone, street, coordinates, notes, entry instructions or review; there is no `confirmationCode` or access token in any field.
- **DEV:** none.

### E2E-XCUT-037 — A cleaner cannot cancel a customer's booking with what the job shows them
- **Priority:** P2
- **Role:** Cleaner
- **Surfaces:** API
- **Rule:** → /architecture/security-rules#gate-secret-wrong-people — the guest credential is server-issued, single-channel, and reaches no DTO a cleaner reads
- **Given** a guest booking taken by a cleaner. **When** the assigned cleaner reads the order detail and then tries `POST api/Order/CancelGuest` with each value found in the JSON (order number, e-mail if present, confirmation code). **Then** none of the values cancels the booking (`order.not_found`), and the booking stays `Confirmed`.
- **DEV:** the guest's real token is not given to the cleaner.

### E2E-XCUT-038 — The wire carries the caller's identity from the token, not from the body
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API
- **Rule:** → /architecture/security-rules#s1-—-userid-is-server-truth-not-client-input — never trust `userId`, `tenantId` or `email` from the request body
- **Given** two customers A and B, and a write command in the generated client that still has a `userId`, `email` or `employeeId` field. **When** A sends that command with B's id in the field. **Then** the effect belongs to A (or the command is refused), B's data is unchanged, and no tenant id can be supplied by any request.
- **DEV:** which commands still carry the field is read from the generated client at run time (grep for `userId`).

### E2E-XCUT-039 — A deleted saved address is no longer listed
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API · iOS · Android
- **Rule:** → /architecture/security-rules#s10-—-soft-delete-isactive-semantics — every query that should hide deactivated rows filters them itself, for example "list my saved addresses"
- **Given** a customer with two saved addresses. **When** the customer deletes one (`DELETE api/SavedAddress/Delete/{id}`). **Then** the list (`GET api/SavedAddress/GetMine`) shows one; the deleted one cannot be chosen in the booking wizard; a booking already made with it is unchanged.
- **DEV:** whether the customer web has a saved-address screen is unsure.

### E2E-XCUT-040 — The web session is an HttpOnly cookie and no token sits in script-readable storage
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** Web
- **Rule:** → /architecture/security-rules#s1-—-userid-is-server-truth-not-client-input — the web session is an HttpOnly cookie
- **Given** a customer and a cleaner signed in on their web apps. **When** `document.cookie`, `localStorage` and `sessionStorage` are read from the page, and the sign-in response headers are read. **Then** the session cookie is `HttpOnly` and absent from `document.cookie`, and no access or refresh token is in either storage.
- **DEV:** whether the admin web does the same is unsure; record it.

### E2E-XCUT-041 — A shared phone shows nothing of the previous account
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** iOS · Android
- **Rule:** → /architecture/security-rules#s11-—-every-per-user-cache-on-mobile-is-wiped-on-session-end-shared-device-leak — every per-user cache is flushed on session end
- **Given** account A with orders, a profile and notifications visible in the app. **When** A signs out and account B (with different data) signs in on the same simulator or emulator, with the network on and again with it off. **Then** at no moment before B's own data loads does a screen show A's orders, name, addresses, notifications or invoices.
- **DEV:** run once per app (customer and partner, each with two accounts); with the network off the lists are empty or an offline message, never A's rows.

### E2E-XCUT-042 — Two simultaneous collections of the same cash count it once
- **Priority:** P2
- **Role:** Cleaner
- **Surfaces:** API
- **Rule:** → /architecture/security-rules#s7-—-idempotency-on-side-effecting-commands — a double submit must not repeat a side effect
- **Given** an in-progress cash job. **When** the cleaner sends `POST api/Order/MarkCashCollected` twice at the same instant. **Then** exactly one succeeds, the other is refused, and the cleaner's cash held rose by the amount due once.
- **DEV:** the cash-ledger rule behind it is in /product/business-rules#cash-held; the refusal's key is recorded, not asserted.

## Rate limiting

### E2E-XCUT-043 — The sign-in window answers 429 after ten attempts a minute from one address
- **Priority:** P2
- **Role:** Guest
- **Surfaces:** API
- **Rule:** → /architecture/security-rules#s5-—-rate-limiting-on-auth-side-effecting-endpoints — the `auth` window is 10 requests a minute per real client IP for anonymous callers
- **Given** a fresh minute and a made-up e-mail. **When** twelve sign-in attempts are sent in one minute. **Then** the first ten answer with the ordinary refusal and a later one answers 429 (with a retry hint); after the window passes, a sign-in with a real account works again.
- **DEV:** run it last and alone: the window is per IP, so it also throttles every other sign-in from the runner during that minute; use a made-up e-mail so that no real account takes a failed-login count.

## Not testable on DEV

- **Isolation between two operating companies.** DEV holds one company and no second tenant, so a CZ admin that lists none of another company's rows, `404` on every by-id read and a second operator's numbering are covered only by `SecondTenantIsolationHostTests` in CI (→ /architecture/security-rules#s8-—-tenant-isolation-correctness). The case set checks only that rows carry the one company.
- **A deactivated or archived company.** `auth.company_deactivated`, a market that stops being a market, `tenant.archived` (409) and the frozen-books write guard need an action on the only company, which would end DEV (→ /decisions/adr-0064, /product/business-rules#company-lifecycle).
- **The market selector and the `preferred_market` cookie.** With one market the web shows a static chip and the apps offer one choice.
- **`employee.work_country_operator_mismatch` and `order.country_operator_mismatch`.** They need a market served by another company.
- **System jobs and their tenant shapes.** Row-driven, company-driven and company-named sweeps, the per-row scope, and the "a job that forgets its override reads nothing" rule are internal; they show only through effects that other cases check (stale-order cancel, retention, wind-down).
- **The outbox, consumer idempotency and dead letters.** A drainer claim, a redelivered message and a poisoned message have no surface. Whether a dead-letter row exists, or that its alert carries an identity and never the body, cannot be read from outside (→ /flows/cross-cutting#dead-letters).
- **E-mail.** Admin e-mails (one shared mailbox or each administrator in their language), customer and cleaner e-mails and their language cannot be read: `@example.com` never receives mail.
- **Push delivery.** A push banner, the iOS Notification Service Extension and the Live Activity start alert need a device or a TestFlight build; `xcrun simctl push` never runs the extension (→ /architecture/push-notifications#language).
- **Rate limits of the Stripe webhook and the forwarded-headers refusal to boot.** They are deployment settings, not behaviour a client can trigger.
- **Real client-IP partitioning.** Behind the DEV front end the runner's address is the partition key; whether a wrong header collapses it into one bucket is not observable from one address.
- **Device revocation latency (30 s) and the password-reset session cutoff.** They are AUTH cases; the bound is a security setting that changes only with a superseding ADR.
- **Logging hygiene (S6), migration and DTO-contract safety (S9) and the stored-artifact disclosure table (S12).** Log contents, the migration and the generated clients are checked by repository guards in CI, not from outside.
- **The administrator role hint going stale for up to fifteen minutes after a role change.** It is testable only by waiting for a token to expire; it widens no server permission and is not run.
