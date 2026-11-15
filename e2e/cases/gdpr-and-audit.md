# GDPR and audit: cases

How a person's data is exported and erased, what survives the erasure, and what the platform records about customers, cleaners and administrators. Every case here is P2: none is one of the core journeys. Erasure is final, so each erasure case runs on a throw-away account (`e2e.<run>.gdpr<n>@example.com`), never on the run's customer or cleaner.

## Data export

### E2E-GDPR-001 — A customer exports their own data
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API · Web
- **Rule:** → /flows/gdpr-and-audit#subject-export — the Art. 15 export is one JSON document; the customer's own export is a `customer.gdpr.export` row with the section counts and commits a `GdprRequest` naming the fixed actor `self`
- **Given** the run customer with a booked order, a consent pair and a few session acts. **When** the customer presses Export on the GDPR page (API: `POST api/v1/Gdpr/export` on :5003). **Then** the browser gets a JSON file with the sections profile, address, orders, disputes, consents (each with IP, user agent, version and document id), `customerActions` (the account's own rows only) and the metadata; the Customer actions segment shows a `customer.gdpr.export` row that carries counts for orders, disputes, consents and trail rows and none of the exported data; the data-protection page lists a `GdprRequest` for the export with `self` as the actor and never the customer's e-mail.
- **DEV:** the customer mobile host serves the same route (`POST api/v1/Gdpr/export` on :5004) but the iOS and Android customer apps expose no export screen that a search of their sources found; the case runs the mobile route through the API only and says so.

### E2E-GDPR-002 — The export carries guest bookings made under the account's e-mail
- **Priority:** P2
- **Role:** Customer · Guest
- **Surfaces:** API · Web
- **Rule:** → /flows/gdpr-and-audit#subject-export — orders are the account's and the guest bookings under its e-mail, a live guest booking included, and `customerActions` never holds the guest rows on those orders
- **Given** a Guest booking made with the run customer's e-mail while signed out (one ended or live is enough), and an order another account placed with that e-mail in its contact field. **When** the customer exports. **Then** the orders section holds the account's orders and the guest booking, and not the other account's order; `customerActions` holds no row from the guest booking's own acts.
- **DEV:** needs a Guest booking from the BOOK flow with the run customer's address.

### E2E-GDPR-003 — A cleaner exports their data, including the paperwork
- **Priority:** P2
- **Role:** Cleaner
- **Surfaces:** API · Web
- **Rule:** → /flows/gdpr-and-audit#subject-export — a cleaner's export adds the employee block, payout details, documents, invoices and the acceptances of their own documents
- **Given** the approved run cleaner with documents and the three acceptances. **When** the cleaner presses Export on partner web's GDPR page (API: `POST api/v1/Gdpr/export` on :5000 or :5002). **Then** the JSON holds the employee block, payout details, documents and invoices, and an acceptance row per cleaner document with type, text, version, language, instant and client, plus the IP and device while the metadata window has not blanked them; it holds no customer terms consent; a `customer.gdpr.export` row is **not** written for a cleaner (a partner host writes no customer row).
- **DEV:** the partner apps' GDPR screens are deletion-only; the export is web and API.

### E2E-GDPR-004 — An admin exports another person's data and leaves an audit row
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /flows/gdpr-and-audit#admin-action-audit — the admin subject export is a command marked `gdpr.user.export`, so it leaves an audit row with the subject id, scope and row counts, never the exported data
- **Given** the run customer, an administrator subject, an unknown id and the Accountant, Support and Manager test admins. **When** each calls `POST api/v1/AdminGdpr/export/{userId}` for the customer; the Support also for the administrator and the unknown id. **Then** Support and Manager receive the same JSON document shape as the customer's own export; the Accountant is 403; the administrator subject is refused `admin_user.cannot_target_admin_via_gdpr_tool`; the unknown id is refused `user.not_existing_id`; the admin audit log shows `gdpr.user.export` rows with the subject id and counts and no personal content; the export committed a `GdprRequest`.
- **DEV:** the Export subject data button sits on the customer's admin page (`/customers/:id`).

### E2E-GDPR-005 — The incident file is a PDF with six sections and a hash
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /flows/gdpr-and-audit#incident-file — a PDF built by an admin from the customer page or an order, with identity, orders, disputes, consents, the trail and an integrity page
- **Given** the run customer with an order, and a stranger's order id. **When** a Support builds the file for the whole account, then for one order of the customer, then for the stranger's order (`POST api/v1/AdminGdpr/incident-file/{userId}?orderId=`). **Then** the first two answers are `application/pdf` files; the text holds identity (name, e-mail, the operator `Cleansia CZ s.r.o.` and the market `Czechia (CZ)`), orders, disputes, consents with IP and user agent, the trail, and an integrity page with a SHA-256, the generating admin's e-mail in every footer and *page x of y*; the order-scoped file holds only that order; the stranger's order is refused `order.not_found`; the admin audit log shows `gdpr.user.incident_file` rows carrying the subject id, the scope, the section counts and the same SHA-256 as the file.
- **DEV:** text is read from the PDF with a PDF text extractor; the hash on the file equals the hash on the audit row of the same build, and a later build of an unscoped file need not match.

## Erasure of a customer

### E2E-GDPR-006 — A customer deletes the account and is anonymised in place
- **Priority:** P2
- **Role:** Customer · Admin
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /flows/gdpr-and-audit#erasure-is-anonymise-in-place — the `User` row survives with its id and its personal fields are scrubbed, and every session is revoked in the erasure's one commit
- **Given** a throw-away confirmed customer with one ended order, a saved address, a consent pair and an open session. **When** the customer deletes the account (web GDPR page, iOS and Android profile; API: `POST api/v1/Gdpr/delete-account` on :5003 or :5004). **Then** the answer is 200; the customer is signed out everywhere (the old refresh token answers `auth.invalid_refresh_token`; on a mobile host the access token is refused once the revoked-user directory polls); a sign-in with the old e-mail and password answers `validation.invalid_password`; the admin customer page shows the user id with replaced name, e-mail and phone and the account deactivated; the saved addresses are gone; the data-protection page lists the request `Completed`; the consents are withdrawn but keep IP, user agent, version and document id.
- **DEV:** the admin-side view of the replaced values is the check; no SQL is needed. The app deletion dialogs warn that unused credit is forfeited; screenshot them.

### E2E-GDPR-007 — Deletion is refused while an order is live
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /flows/gdpr-and-audit#edge-cases — erasure requested with a job in progress is refused, because erasing mid-job would anonymise a customer while a cleaner is on the way to their home
- **Given** a throw-away customer with one order in each of the live statuses in turn (`New`, `Confirmed`, and `InProgress` if the run has one). **When** the customer requests deletion. **Then** every answer is 400 `gdpr.deletion_blocked_by_order`; nothing changes (name, e-mail, sessions, addresses intact); no `GdprRequest` row is created (a refusal before the walk is a plain answer); after the order ends (completed or cancelled) the deletion succeeds (E2E-GDPR-006).
- **DEV:** the blocking statuses in the code are `New`, `Pending`, `Confirmed`, `OnTheWay` and `InProgress`; the docs page names no list (finding). Reuse an order from the BOOK or CANCEL flow where possible.

### E2E-GDPR-008 — Unused credit is forfeited, in every currency, and the deletion is not refused
- **Priority:** P2
- **Role:** Customer · Admin
- **Surfaces:** API · Web
- **Rule:** → /product/business-rules#credit-on-account-deletion — a completed deletion forfeits the unused credit with no payout, as one `Expired` ledger row per account under `account-deletion:<account id>`
- **Given** a throw-away customer with a positive credit balance (the admin issues credit in the market currency, and in a second currency if one is active). **When** the customer deletes the account. **Then** the deletion succeeds; each account's balance is zero; each has one `Expired` ledger row with the key `account-deletion:<account id>` and the note "Account deletion: {reason}"; a later admin credit issue to the erased customer moves nothing and opens no account.
- **DEV:** the admin's Issue credit action funds the account; this is why the case needs the Administrator or a Support or above role.

### E2E-GDPR-009 — A live membership is cancelled in Stripe before the erasure
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API · Web
- **Rule:** → /flows/gdpr-and-audit#erasure-is-anonymise-in-place — an `Active` membership is cancelled in Stripe first and ends at its period end
- **Given** a throw-away customer subscribed to Plus with the 4242 test card. **When** the customer deletes the account. **Then** the deletion succeeds; the Stripe test subscription is set to cancel at its period end; the customer's membership is no longer renewable; a `PastDue` or `Paused` membership (cancelled at once with its open invoice voided) is not reachable on DEV.
- **DEV:** Stripe is in test mode; the subscription is read in the Stripe test dashboard or through the API. The subscribe step belongs to the LOYAL flow.

### E2E-GDPR-010 — An ended order loses its customer's details and its photos, and keeps the money
- **Priority:** P2
- **Role:** Customer · Cleaner · Admin
- **Surfaces:** API · Web
- **Rule:** → /flows/gdpr-and-audit#erasure-is-anonymise-in-place — anonymising an order clears the customer's name, e-mail, phone, notes, floor, flat, access mode and cancellation reason, deletes the photos, and replaces the address with a copy
- **Given** a throw-away customer with one completed order that has photos, a customer-written note and a customer-written cancellation reason on a second, cancelled order. **When** the customer is erased. **Then** the admin order detail and the cleaner's order history show no customer name, e-mail, phone, note, floor, flat or access instruction; the cancellation reason typed by the customer is cleared; the photo blobs are gone and the photo rows remain with their file name anonymised and notes cleared; the order's price, payment status, invoice and pay rows are unchanged; the order's status is unchanged.
- **DEV:** the blob check is a fetch of the photo URL (404); the address copy cannot be seen from the apps. A platform cancellation reason such as `order.cancelled.company_wind_down` and a confirmed lockout key keep their code; they occur on DEV only if the run produced one.

### E2E-GDPR-011 — Guest bookings under the e-mail are erased with the account, and a live one is left alone
- **Priority:** P2
- **Role:** Customer · Guest
- **Surfaces:** API · Web
- **Rule:** → /flows/gdpr-and-audit#erasure-is-anonymise-in-place — the ended guest bookings under the account's e-mail are anonymised and lose their access keys, while a guest booking still live is left out of the walk without refusing the erasure
- **Given** a throw-away customer, an ended Guest booking and a live Guest booking made with its e-mail, and each booking's access link. **When** the customer is erased. **Then** the erasure succeeds despite the live guest booking; the ended booking's contact data is cleared and its access link answers as an expired or unknown key; the live booking keeps its contact data and its link still opens and can cancel; the guest audit rows of the ended booking lose IP and device.
- **DEV:** needs two Guest bookings from the BOOK flow; the live one is cancelled afterwards so the slot is not wasted.

### E2E-GDPR-012 — The customer trail survives the erasure with its IP and device blanked
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /flows/gdpr-and-audit#customer-trail — the customer audit trail is pseudonymised, not deleted: the IP, device label and device id go, the act, outcome, evidence and subject id stay
- **Given** the erased customer of E2E-GDPR-006 and the Customer actions rows it had before. **When** a Support reads the customer's timeline and the order histories after the erasure. **Then** the same rows exist with the same acts, outcomes and evidence; their IP address, device label and device id are empty; the link from the erased user id to its orders is still there; the erasure itself is not a row in the trail (the `GdprRequest` is its record).
- **DEV:** the before-state is a copy of the rows taken before the erasure.

### E2E-GDPR-013 — An erased customer's dispute text stays readable for three years
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /flows/gdpr-and-audit#edge-cases — an erased customer's dispute text stays readable for `retention.dispute_text.years` from the erasure, then the weekly sweep blanks it; the evidence files go at erasure
- **Given** a throw-away customer with a resolved dispute that has a description, messages, a resolution note and an evidence file. **When** the customer is erased. **Then** the admin dispute detail still shows the description, the messages and the resolution notes; the evidence file is no longer downloadable and its row is blanked; the dispute carries a text-retained-until date about three years ahead.
- **DEV:** the weekly sweep that blanks the text three years later cannot run (below). Needs a dispute from the CANCEL flow.

### E2E-GDPR-014 — A refund on an erased customer's order returns the card share but not the credit share
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /flows/gdpr-and-audit#edge-cases — a refund or cancellation on an erased customer's order refunds the card share as usual and returns no credit share, no balance and no new account
- **Given** an erased customer who paid an order partly by card and partly with credit, where the order is still refundable (a dispute settled after the erasure, or a cancellation of an order that was not yet ended). **When** an admin refunds it. **Then** the card refund is created for the card share only; no credit entry is made; no credit account exists for the user; the refund is not larger than the card share.
- **DEV:** the order must be in a state that still allows a refund; if the run cannot reach one, record the case as not run. The erasure refuses a customer with a live order, so the order must have ended and be refundable afterwards.

### E2E-GDPR-015 — An admin erases a customer, and the role limits who may
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /flows/gdpr-and-audit#erasure-is-anonymise-in-place — an administrator's erasure runs the same walk, with the admin recorded as the actor
- **Given** a throw-away customer with no live order and the Administrator, Manager, Support and Accountant test admins. **When** the Support and the Accountant try, then the Manager performs it (`POST api/v1/AdminGdpr/delete-account/{userId}`), and the Manager tries it on an administrator account. **Then** Support and Accountant are 403; the Manager's succeeds and the `GdprRequest` records the admin's e-mail as who processed it; an admin audit row records the act; the administrator subject is refused `admin_user.cannot_target_admin_via_gdpr_tool`; the customer's data is anonymised as in E2E-GDPR-006.
- **DEV:** the Delete action is on the data-protection page of the admin web.

### E2E-GDPR-016 — A request that did not fail cannot be retried
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /flows/gdpr-and-audit#erasure-is-anonymise-in-place — only a `Failed` request, or one left `Processing` for more than thirty minutes, is re-run
- **Given** a `Completed` request (E2E-GDPR-006), a Pending cleaner request (E2E-GDPR-020) and an unknown id. **When** a Manager sends `POST api/v1/AdminGdpr/requests/{requestId}/retry-deletion` for each. **Then** the completed and the pending ones are refused `gdpr.request_not_retryable`; the unknown id is refused `gdpr.request_not_found`; nothing changes.
- **DEV:** a `Failed` request needs the erasure's commit to throw, which DEV cannot be made to do (below), so the success path is not run.

### E2E-GDPR-017 — The admin lists the requests, newest first, one page at a time
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /flows/gdpr-and-audit#a-cleaner-s-own-deletion-files-a-request-it-does-not-erase — a cleaner's request is `Pending` until an admin fulfils it, and every request not yet `Completed` counts as pending
- **Given** the requests from the cases above. **When** a Support reads `GET api/v1/AdminGdpr/requests` with the status filter `Pending`, `Completed`, and with page sizes 1, 100 and 101. **Then** each filter lists only its status; sizes 1 and 100 are served; size 101 is refused `validation.page_size_exceeded`; an Accountant is 403.
- **DEV:** the status names are read from `GdprRequestStatus` in 1b.

### E2E-GDPR-018 — A customer reads, grants and withdraws only the consents the platform allows
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API · Web
- **Rule:** → /product/business-rules#consents — the terms and the privacy policy are the only consents a customer grants, and none of the document-backed ones is withdrawn through the consent endpoints
- **Given** the run customer with the two consents. **When** the customer reads `GET api/v1/Gdpr/consents`, grants `TermsOfService` again, grants `MarketingEmails`, withdraws `TermsOfService`, and withdraws `DataProcessing` when no such row exists. **Then** the read lists terms and privacy with their version, date and a flag saying whether they cover the text in force; the repeat grant is refused `gdpr.consent_already_granted` (the business rules say the same document again writes a trail row; the case records which); the marketing grant is refused `gdpr.consent_not_editable`; the terms withdrawal is refused `gdpr.consent_not_editable`; the data-processing withdrawal is refused `gdpr.consent_not_found`; a grant or withdrawal that does change the row writes `customer.consent.grant` or `.withdraw` with the type and version.
- **DEV:** the web page shows the terms and privacy read-only; the posts are API-only.

### E2E-GDPR-019 — An admin reads a person's consents; a cleaner's consents are their own documents
- **Priority:** P2
- **Role:** Admin · Cleaner
- **Surfaces:** API · Web
- **Rule:** → /product/business-rules#cleaner-documents — a cleaner's consent rows are the three versioned documents they accept, and no customer consent is recorded for them
- **Given** the run customer and the approved run cleaner. **When** a Support reads `GET api/v1/AdminGdpr/consents/{userId}` for each, and the cleaner reads their own consents on partner web. **Then** the customer shows terms and privacy with version and document id; the cleaner shows the framework contract, self-billing agreement and data-processing agreement at their accepted versions and no customer terms; an Accountant is 403; the partner web's GDPR page lists the three documents read-only.
- **DEV:** none.

## Erasure of a cleaner

### E2E-GDPR-020 — A cleaner's own deletion files a request and erases nothing
- **Priority:** P2
- **Role:** Cleaner · Admin
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /flows/gdpr-and-audit#a-cleaner-s-own-deletion-files-a-request-it-does-not-erase — a cleaner's deletion files a `Pending` request and nothing else runs; they stay signed in and keep working
- **Given** an approved throw-away cleaner with no job, no invoice and no pay (the second onboarding cleaner, approved in E2E-AUTH-050). **When** the cleaner deletes the account on partner web, in the iOS app and in the Android app (API: `POST api/v1/Gdpr/delete-account` on :5000 or :5002; one platform per account). **Then** the answer is 200; the cleaner's name, e-mail, documents, payout details and sessions are unchanged and the cleaner still signs in and sees the board; the data-protection page lists a `Pending` request for them; the apps' screens say the request is filed, not that the account is gone.
- **DEV:** each platform needs its own throw-away cleaner; E2E-AUTH-047 to E2E-AUTH-050 make one, and two more are registered, approved and left without work the same way.

### E2E-GDPR-021 — A second deletion request is refused while the first is open
- **Priority:** P2
- **Role:** Cleaner
- **Surfaces:** API · Web
- **Rule:** → /flows/gdpr-and-audit#edge-cases — erasure requested twice is refused as already pending while any earlier request is not yet `Completed`
- **Given** the cleaner of E2E-GDPR-020 with a `Pending` request. **When** the cleaner requests deletion again. **Then** the answer is 400 `gdpr.deletion_already_pending`; no second request exists.
- **DEV:** none.

### E2E-GDPR-022 — A cleaner staffed on a live job cannot file, nor can an admin erase them
- **Priority:** P2
- **Role:** Cleaner · Admin
- **Surfaces:** API · Web
- **Rule:** → /flows/gdpr-and-audit#a-cleaner-s-own-deletion-files-a-request-it-does-not-erase — a seat on an order that is not terminal refuses the request, for the cleaner and for an administrator alike
- **Given** an approved cleaner holding a seat on a `Confirmed` order of another customer. **When** the cleaner requests deletion, then a Manager erases the cleaner. **Then** both answers are 400 `gdpr.deletion_blocked_by_assigned_order`; no request row is created by the cleaner's refusal; nothing is anonymised.
- **DEV:** use the run cleaner of the TAKE flow before it completes its job, or a second cleaner who takes a spare order; release the seat afterwards.

### E2E-GDPR-023 — A cleaner who is owed pay cannot be erased
- **Priority:** P2
- **Role:** Cleaner · Admin
- **Surfaces:** API · Web
- **Rule:** → /flows/gdpr-and-audit#a-cleaner-s-own-deletion-files-a-request-it-does-not-erase — a pay row not on an invoice, or whose pay period has not reached `Paid`, refuses the request as one condition
- **Given** the run cleaner who completed a job whose pay period is still open or closed but not `Paid`. **When** the cleaner requests deletion and a Manager tries to erase them. **Then** both answers are 400 `gdpr.deletion_blocked_by_unsettled_pay`; after the period is paid and the invoice settled the pay condition no longer applies (the invoice condition may).
- **DEV:** a DEV pay period lasts 3 days; run after the PAYOUT flow has created the pay row and before it is paid.

### E2E-GDPR-024 — A cleaner with an open invoice cannot be erased
- **Priority:** P2
- **Role:** Cleaner · Admin
- **Surfaces:** API · Web
- **Rule:** → /flows/gdpr-and-audit#a-cleaner-s-own-deletion-files-a-request-it-does-not-erase — an invoice that is `Pending`, `Approved` or `Disputed` means money is mid-flight and refuses the request
- **Given** the run cleaner with a payout invoice in one of those statuses. **When** the cleaner requests deletion. **Then** the answer is 400 `gdpr.deletion_blocked_by_invoice`.
- **DEV:** depends on the PAYOUT flow; the order of the checks in the code puts a live order first, then the invoice, so the case needs no live order.

### E2E-GDPR-025 — An admin fulfils a cleaner's request after the paperwork
- **Priority:** P2
- **Role:** Admin · Cleaner
- **Surfaces:** API · Web
- **Rule:** → /flows/gdpr-and-audit#a-cleaner-s-own-deletion-files-a-request-it-does-not-erase — the cascade runs only when an administrator fulfils the request, and invoices and pay records are retained
- **Given** the cleaner of E2E-GDPR-020 with its `Pending` request, no live job, no open invoice and no unpaid pay. **When** a Manager erases the cleaner (`POST api/v1/AdminGdpr/delete-account/{userId}`). **Then** the cleaner's personal fields, employee record and address are replaced, their payout identifiers are deleted and their sessions are revoked; the request becomes `Completed`; the acceptances of their own documents are kept with IP and device blanked (read through the admin subject export); any invoice or pay row stays; the cleaner can no longer sign in.
- **DEV:** the throw-away cleaner has no pay, so "invoices and pay rows stay" is a statement about rows that do not exist here; it is checked on a cleaner who worked only in a later run (below).

### E2E-GDPR-026 — Loyalty, referral and promo rows survive an erasure
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /flows/gdpr-and-audit#why-loyalty-referral-and-promo-rows-are-not-touched — those rows carry a foreign key and non-PII scalars only, so a promo code does not become redeemable again
- **Given** a throw-away customer who redeemed a one-shot promo code and earned loyalty points. **When** the customer is erased. **Then** the loyalty ledger and the redemption row remain pointing at the anonymised user; a second account cannot redeem the same one-shot code that the erased account used.
- **DEV:** needs a one-shot promo code and a booking from the LOYAL flow.

## Admin action audit

### E2E-GDPR-027 — The admin audit log is append-only
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /flows/gdpr-and-audit#admin-action-audit — every privileged action writes an append-only record carrying the actor's session
- **Given** an admin mutation (approving the run cleaner, E2E-AUTH-046). **When** the audit list is read (`GET api/AdminAuditLog/get-paged`) and a `PUT`, `DELETE` and `POST` are sent to the audit routes. **Then** the list holds a row for the mutation with the actor, role, label, time and before-and-after; the write methods answer 404 or 405; the controller exposes only the two reads.
- **DEV:** Support or above can read the log.

### E2E-GDPR-028 — A refused admin action is recorded by its order
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /flows/gdpr-and-audit#admin-action-audit — an admin's refusal on an order is traceable by the order: a resource type, the order id and the error key
- **Given** an order `InProgress` (from the EXEC flow). **When** an admin tries to cancel it (`AdminCancelOrder`). **Then** the cancel is refused `order.in_progress_cannot_cancel`; the audit log has a failed admin row with `ResourceType = Order`, the order id and that key; the audit list's resource filter and the order's history both find it.
- **DEV:** use an order the EXEC flow keeps in progress for the moment of the call; do nothing else to it.

### E2E-GDPR-029 — An audit row is read by id, and an unknown id is refused
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** API
- **Rule:** → /flows/gdpr-and-audit#admin-action-audit — a privileged action's record is retrievable and carries the actor and outcome
- **Given** an admin audit row id and a customer audit row id. **When** `GET api/AdminAuditLog/get-by-id/{id}`, `GET api/CustomerAudit/get-by-id/{id}` and the same with a random id are sent. **Then** the known ids return their rows with the actor, the outcome and the evidence or change; the random id is refused `audit.not_found`; an Accountant is 403.
- **DEV:** none.

### E2E-GDPR-030 — Revealing payout identifiers is a command, and each reveal is recorded
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /flows/gdpr-and-audit#admin-action-audit — revealing a cleaner's payout identifiers is a command, not a query, so it cannot happen unrecorded, and the entity stamps who looked and how often
- **Given** the approved run cleaner with payout details, the Manager and the Accountant. **When** the Accountant reads the masked payout details, then tries the reveal; the Manager reveals twice (`POST api/AdminEmployee/{employeeId}/payout-details/reveal`). **Then** the masked read shows masked identifiers and writes no reveal; the Accountant's reveal is 403; each Manager reveal returns the full identifiers and writes an admin audit row; the reveal counter and the last viewer on the employee's payout details rise by one each time.
- **DEV:** the identifiers are the cleaner's test bank details; never log them.

## The customer trail

### E2E-GDPR-031 — Support reads a customer's acts, newest first, in one timeline
- **Priority:** P2
- **Role:** Admin · Customer
- **Surfaces:** API · Web
- **Rule:** → /flows/gdpr-and-audit#customer-trail — support reads the trail in the Customer actions segment, the per-resource history and the customer's page; every route is behind `CanViewAuditLog`
- **Given** the run customer after registering, signing in, booking, exporting and signing out. **When** a Support reads `GET api/CustomerAudit/timeline?userId=` and, for an order, `timeline?resourceType=Order&resourceId=`. **Then** the user timeline interleaves the customer's rows with the admin and cleaner rows on the customer's orders, newest first; the order timeline shows every row of the three tables that names the order, including a Guest's; the customer's page (`/customers/:id`) shows the same timeline; an Accountant is 403 on all of them.
- **DEV:** the case is a read over rows the other flows produce; it runs last.

### E2E-GDPR-032 — The timeline needs exactly one key, a real user and a bounded page
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** API
- **Rule:** → /flows/gdpr-and-audit#customer-trail — the timeline by user finds the subject's orders by the order's user or by the subject's own successful acts on it
- **Given** a Support admin. **When** the timeline is requested with neither key, with both a user id and a resource, with an unknown user id, and with a page size of 101. **Then** the answers are `audit.timeline.filter_required`, `audit.timeline.filter_required`, `user.not_existing_id` and `validation.page_size_exceeded`; the offset is capped at 500.
- **DEV:** whether both keys at once is refused is read from `KeyedByExactlyOneOfUserOrResource` in 1b.

### E2E-GDPR-033 — A refused customer act is recorded with its key and no payload
- **Priority:** P2
- **Role:** Customer · Admin
- **Surfaces:** API · Web
- **Rule:** → /product/business-rules#customer-record — a refused attempt leaves a row with `Success = false` and the error key, never a field name and never a payload
- **Given** the run customer. **When** a booking is refused for the missing terms tick, a registration is refused for the same, a cancel of another customer's order id is attempted and a sign-in is refused. **Then** the Customer actions segment shows a failed row for each with `consent.terms_not_accepted`, `consent.terms_not_accepted`, `order.not_found` (with the probed order id) and `validation.invalid_password`, the caller's IP, and no payload; the anonymous ones carry no user.
- **DEV:** the refused booking and cancel need an order from the BOOK flow; otherwise run the other two.

### E2E-GDPR-034 — A customer row never holds a name, a contact detail or a token
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** API
- **Rule:** → /product/business-rules#customer-record — a row holds identifiers, money, enums and versions and never a name, an e-mail, a phone, an address line, free text or a token
- **Given** the run customer's strings (e-mail, names, phone, street, a booking note, a cancellation reason typed "private reason"). **When** the whole Customer actions list and the order timelines for the run are fetched and searched. **Then** none of those strings appears in any row payload, subject or resource field; a cancel row records that a reason was given and not the reason; the guest cancel rows hold no access token.
- **DEV:** run at the end of the run over everything the run produced.

### E2E-GDPR-035 — A guest's cancel is recorded with no user and no credential
- **Priority:** P2
- **Role:** Guest · Admin
- **Surfaces:** API · Web
- **Rule:** → /flows/gdpr-and-audit#customer-trail — the guest cancellation rows have no user, even if a JWT names one, and the token and the cancellation text are not copied into the trail
- **Given** a Guest booking and its access link. **When** the Guest cancels through the link, signed out and then again while a customer is signed in in the same browser. **Then** the success rows `customer.order.cancel` have no user id, the standard cancellation evidence (tier, money, notice, `refundAmount` as policy and `actualRefundAmount` as refunded) and no token or text; a refused attempt (such as a cancel once the cleaning has started) is a row with the key and no payload.
- **DEV:** needs a Guest booking from the BOOK flow and a cancellation window that allows it.

## Retention

### E2E-GDPR-036 — A company's retention windows are bounded where they are written
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /flows/gdpr-and-audit#retention — the audit window cannot be set below one year, the floor is enforced where the value is written, and a stored value the catalogue refuses falls back to the default
- **Given** the Administrator on the company settings page (`PUT api/AdminTenantSettings/set`). **When** `retention.customer_audit.years` is set to 0, to 1 and to 3, `retention.receipts.years` to 9, and a Manager tries to set any of them. **Then** 0 and 9 are refused `tenant_setting.invalid_value`; 1 and 3 are saved (and the default is restored with the reset route); the Manager is 403; an unknown key is refused `tenant_setting.unknown_key`.
- **DEV:** end with every changed setting reset to its default (`DELETE api/AdminTenantSettings/reset/{key}`); the sweeps themselves are not run (below). The ranges are the ones in the table of the business rules.

## Not testable on DEV

- **The weekly retention sweep and its fifteen tasks.** It runs once per company on a schedule; the windows are in days and years (photos 7 days, order data 2 years, audit rows 3 years, receipts 10 years), and the sweep has no trigger that a test can call. Photo expiry, order-PII anonymisation of old orders, the signed-out-device tombstones, the dead guest keys, the dispute-text blanking three years after an erasure, the acceptance-metadata blanking and the receipt PDF deletion are therefore not run; E2E-GDPR-036 checks only the bounds of the settings.
- **A failed erasure and its retry.** The erasure's commit throws only on a fault that DEV cannot be made to produce, so the `Failed` request row, the daily 05:00 UTC `RetryFailedUserDeletions` job, the administrators' `admin.erasure.failed` notice and a successful admin Retry cannot be seen; only the refusals of E2E-GDPR-016 run.
- **A frozen or deactivated company's erasure and sweep.** The legal-obligation gate lets a retention job and an erasure write to an archived company's books. DEV holds one active company, and freezing it would refuse every other case.
- **A `PastDue` or `Paused` membership at erasure.** Stripe test mode does not let a test make a card fail on a schedule the run can wait for.
- **The mobile apps' export screen.** No iOS or Android screen for the data export was found; the route is exercised through the API only.
- **Shared addresses between several customers.** The copy-before-delete rule and the cross-company reference census need several accounts sharing one address row and a second company; DEV has one company, and the rows are not visible from any app.
- **The cleaner's contract-for-work acceptances after an erasure.** They depend on a cleaner who worked, took a contract and then passed every erasure gate (paid period closed, invoice settled); the pay period needs three days plus the 02:00 UTC close, so the case belongs to a later run.
- **Real e-mail.** The erasure-failure e-mail, the refund-needs-retry e-mail and every admin notification e-mail go to `@example.com` and cannot be read.
- **Load.** The incident file holds a process-wide lock; nothing here measures how many can be built at once.
