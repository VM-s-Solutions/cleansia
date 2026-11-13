# Cancellation, refund and dispute: cases

Three ways money goes back: a cancellation (customer, guest, administrator, no-show, lockout), a refund (full, partial, failed, bounced, repaid) and a dispute (complaint, resolution, chargeback). Source: `docs/flows/cancellation-refund-dispute.md`.

Timing recipes the DEV notes refer to. A fee needs a cleaner on the job, so each fee case first has a cleaner take the order.
- **Free by notice (24 h or more):** book a start at least 25 h ahead.
- **25 % tier (4 to 24 h):** book a start 6 to 20 h ahead, have a cleaner take it, wait out the oops window (60 min on a customer's first booking, 15 min after), then cancel.
- **50 % tier (under 4 h):** book at about 08:05 Prague for a start of 12:15 or later, have a cleaner take it, and cancel once under 4 h remain and the oops window has passed. An express booking (2 to 4 h ahead, +20 %) is already inside this tier.
- Money figures below use T for the order total and F for a fee, F = rate × T; a card order with no credit unless stated.

## Customer cancellation: free cases

### E2E-CANCEL-001 — A booking nobody has taken is cancelled free at any time
- **Priority:** P1
- **Role:** Customer
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /product/business-rules#oops-window — no cleaner on the job means free, whatever the timing
- **Given** a paid card order starting under 4 h ahead that no cleaner has taken. **When** the customer opens *Cancel* (sheet shows the preview) and confirms. **Then** the preview showed 0 % and a refund of T; the order is `Cancelled`, no fee, a card refund of T is created and then recorded, payment `Refunded`, and the customer gets the `order.cancelled` or `order.refunded` push in the apps.
- **DEV:** A slot under 4 h ahead is an express booking made after 08:00 Prague.

### E2E-CANCEL-002 — A cancellation inside the oops window is free even with a cleaner on the job
- **Priority:** P1
- **Role:** Customer
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /product/business-rules#oops-window — 60 minutes on a first booking, free whatever the notice
- **Given** a customer's first ever booking, a start under 4 h ahead, and a cleaner who took it within minutes. **When** the customer cancels 20 minutes after booking. **Then** the preview and the cancel say tier free, fee 0 %, `oopsMinutesApplied` 60 and rule `FirstBooking`; the refund is T; the cleaner is told the job is off.
- **DEV:** A first booking needs an account (and e-mail and phone) with no earlier order; the E2E customer is fresh per run, so use its first booking for this case.

### E2E-CANCEL-003 — A cancellation with 24 hours or more of notice is free
- **Priority:** P1
- **Role:** Customer
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /product/business-rules#cancellation — 24 h or more is free, 0 %
- **Given** a paid card order starting 25 h ahead with a cleaner on it, past the oops window. **When** the customer cancels. **Then** tier free, fee 0, refund T, order `Cancelled`, payment `Refunded`.
- **DEV:** Needs the cleaner to take the job and the oops window to pass.

### E2E-CANCEL-004 — The oops window is 15 minutes on a returning customer's booking
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API · Web
- **Rule:** → /product/business-rules#oops-window — anyone not first-time and not Plus gets 15 minutes
- **Given** a customer with an earlier order, a new booking starting 10 h ahead and a cleaner on it. **When** they cancel at 30 minutes after booking. **Then** the tier is partial 25 %, `oopsMinutesApplied` 15, rule `Standard`; the same cancel at 10 minutes would have been free.
- **DEV:** Compare with E2E-CANCEL-002 on the first booking at 20 minutes. Exact-minute boundaries are not tested (see closing section).

### E2E-CANCEL-005 — The previews state the customer's own oops window and tier
- **Priority:** P1
- **Role:** Customer
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /flows/cancellation-refund-dispute#cancellation — both previews return `oopsWindowMinutes` beside the tier
- **Given** an order of each tier (E2E-CANCEL-001, -003, -010, -011). **When** the customer opens the cancel sheet (`GET api/Order/CancellationPreview`). **Then** the preview's tier, fee rate, refund and `oopsWindowMinutes` (15 or 60) equal the figures the cancel then records, and the sheet prints that window, not a fixed text.
- **DEV:** Read the preview just before the cancel; tiers move with time.

## Customer cancellation: fee tiers

### E2E-CANCEL-010 — A cancellation with 4 to 24 hours of notice costs 25 %
- **Priority:** P1
- **Role:** Customer
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /product/business-rules#cancellation — 4 h to 24 h is the partial tier, 25 %
- **Given** a paid card order with a cleaner on it and 6 to 20 h of notice, past the oops window. **When** the customer cancels with a reason. **Then** the cancel records fee rate 0.25 and a refund of T − F (75 % of T); the order is `Cancelled`, payment `PartiallyRefunded`; the response has `refundPending` false; the card refund appears on the Refunds page as *Refunded*, origin cancellation.
- **DEV:** 25 % tier recipe.

### E2E-CANCEL-011 — A cancellation with under 4 hours of notice costs 50 %
- **Priority:** P1
- **Role:** Customer
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /product/business-rules#cancellation — under 4 h is the last-minute tier, 50 %
- **Given** a paid card order with a cleaner on it and under 4 h of notice, past the oops window. **When** the customer cancels. **Then** fee rate 0.50, a refund of T − F (half of T), order `Cancelled`, payment `PartiallyRefunded`.
- **DEV:** 50 % tier recipe.

### E2E-CANCEL-012 — A taken job cancelled by the customer disappears from the cleaner and the cleaner is told
- **Priority:** P1
- **Role:** Cleaner
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /product/business-rules#a-guest-cancels-under-the-same-policy — assigned cleaners still receive their cancellation notice
- **Given** a cleaner with the order of E2E-CANCEL-010 or -011 among their jobs. **When** the customer cancels it. **Then** the cleaner's job list and board no longer offer it as an active job, the job detail reads cancelled, and a cancellation notice (push or inbox) arrives; the slot is free for the cleaner's other work.
- **DEV:** The cleaner agent checks the partner web or app on the same run. Android and iOS partner apps read the same list.

### E2E-CANCEL-013 — A late cancellation pays the crew half of the fee collected
- **Priority:** P2
- **Role:** Cleaner
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /product/business-rules#fee-share — each seat is paid its share of half of what the company collected as fee
- **Given** the cancelled order of E2E-CANCEL-010 (fee F, one seat). **When** the cleaner opens My Pay after the pay queue ran. **Then** a line of type late-cancellation fee share equals F × 0.5 for a one-seat job; a free cancellation (E2E-CANCEL-003) writes no such line.
- **DEV:** The queue consumer may take a minute; if no row appears, check again before logging a defect.

### E2E-CANCEL-014 — The cancellation is recorded as the server priced it
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /flows/cancellation-refund-dispute#cancellation — `customer.order.cancel` stores tier, rate, amounts, notice, oops window and why
- **Given** the cancelled order of E2E-CANCEL-010. **When** an administrator opens *View audit history* on the order. **Then** a `customer.order.cancel` row carries tier partial, rate and fee amount, refund amount, notice in hours, `oopsMinutesApplied` and `oopsRuleApplied`, that a cleaner had accepted, and that a reason was given; the reason text is on the order, not in the row.
- **DEV:** Exact field names are unsure; the triager checks them against the audit page.

### E2E-CANCEL-015 — The admin order detail shows the fee rate and the fee still owed; the customer does not
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /product/business-rules#cancellation — *Cancellation fee* and *Fee still owed* are for administrators only
- **Given** a late-cancelled cash order (E2E-CANCEL-020) and a late-cancelled card order (E2E-CANCEL-010). **When** the administrator opens each order detail and the customer reads the same orders through `GetById`. **Then** the admin sees the rate on both, *Fee still owed* equal to F on the cash order and 0 on the card order; the customer payloads carry neither figure.
- **DEV:** Customer payload field names are unsure; compare the two payloads.

### E2E-CANCEL-016 — A card cancellation returns applied credit exactly once and splits the fee across the tenders
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API · Web
- **Rule:** → /flows/cancellation-refund-dispute#refund — credit and card are returned in the proportion the price was paid
- **Given** a card order paid with credit C and card (T − C), a cleaner on it, 25 % tier. **When** the customer cancels. **Then** the card refund is 75 % of the card share and the credit returned is 75 % of C, so 75 % of each tender is back; a free cancellation of the same kind of order returns all of C once, never twice; the credit ledger shows one return line.
- **DEV:** The administrator grants credit first (`POST api/AdminCredit/issue`); credit is capped at 70 % of the order.

### E2E-CANCEL-017 — A free cancellation of a partly refunded card order refunds the rest on both tenders
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API · Web
- **Rule:** → /flows/cancellation-refund-dispute#refund — a fee-free cancellation of a partly refunded order returns what the sale has left
- **Given** a paid card order (with credit if available) that an administrator partly refunded (E2E-CANCEL-082). **When** the customer cancels it free. **Then** the order is `Cancelled`, the rest of the sale goes back (card and credit in proportion), the cancel records what it gave back, the total returned equals T, and payment reads `Refunded`.
- **DEV:** Choose a start 25 h or more ahead so the cancel is free.

### E2E-CANCEL-018 — A fee cancellation of a partly refunded card order returns what is left less the fee
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API · Web
- **Rule:** → /flows/cancellation-refund-dispute#refund — `max(0, left to give back − fee)` is returned and recorded
- **Given** a card order with total T where the administrator refunded R, a cleaner on it, 25 % tier. **When** the customer cancels. **Then** the amount returned is (T − R) − 0.25 × T, never below 0 (T = 1 000 and R = 400 returns 350), and it is recorded as the cancel's refund; the preview shows the assessor's full figure (750) while the confirmation shows the real one.
- **DEV:** Combines the 25 % recipe with a partial refund made before the cancel.

## Customer cancellation: cash

### E2E-CANCEL-019 — A free cancellation of a cash booking records nothing owed
- **Priority:** P1
- **Role:** Customer
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /flows/cancellation-refund-dispute#cancellation — money never taken is never refunded; a free cancellation opens no receivable
- **Given** a cash booking (saved card required) 25 h ahead with a cleaner on it. **When** the customer cancels. **Then** the order is `Cancelled`, the refund recorded is 0, no receivable exists, the cancel sheets print no refund line, and the customer can book again at once.
- **DEV:** Needs a saved card first (E2E-PAY-040) and a cleaner for the cash order.

### E2E-CANCEL-020 — A late cancellation of a cash booking opens a receivable and blocks booking
- **Priority:** P1
- **Role:** Customer
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /flows/cancellation-refund-dispute#cancellation — a signed-in customer's late cash cancellation opens a receivable for the fee
- **Given** a cash booking with a cleaner on it, 6 to 20 h ahead, past the oops window. **When** the customer cancels. **Then** the refund is 0 and no refund line is printed; an open cash-cancellation-fee receivable for F exists (admin Orders → Receivables, customer's list); a new booking by cash or card is refused with `order.unpaid_receivable`; no card is charged. Paying it is E2E-PAY-070.
- **DEV:** 25 % tier recipe with cash. The setup for the customer receivable cases in `payment-and-fiscal.md`.

### E2E-CANCEL-021 — A late cash cancellation by an administrator opens no fee
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /product/business-rules#platform-cancellation — no fee is ever charged on a platform cancellation
- **Given** a cash booking with a cleaner on it inside the 25 % tier. **When** an administrator uses *Cancel order*. **Then** the order is `Cancelled`, no receivable is opened, no fee is recorded, nothing is refunded and the customer can book again.
- **DEV:** Compare with E2E-CANCEL-020.

## Customer cancellation: refusals and rights

### E2E-CANCEL-030 — After the booked start, with a cleaner on the job and not started, the customer cannot cancel
- **Priority:** P1
- **Role:** Customer
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /product/business-rules#after-the-start — self-cancel is refused once the booked start has passed with a cleaner on the job
- **Given** a confirmed order whose start has passed, a cleaner assigned who has not tapped Start. **When** the customer calls the cancel and the preview. **Then** both answer `order.start_passed_cannot_cancel`; the clients replace *Cancel* with *the cleaner did not arrive*; the order is unchanged.
- **DEV:** Needs a cleaner who takes the job and never starts it; wait until the slot has passed (late in the day a start in the past costs nothing but time).

### E2E-CANCEL-031 — A job in progress cannot be cancelled by the customer
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API · Web
- **Rule:** → /product/business-rules#after-the-start — an order in progress stays refused
- **Given** a job the cleaner has started. **When** the customer calls the cancel and the preview. **Then** both answer `order.in_progress_cannot_cancel`; the failure is a row on the order's audit history.
- **DEV:** Start is allowed from 60 min before the slot.

### E2E-CANCEL-032 — A finished or already cancelled order cannot be cancelled again
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API · Web
- **Rule:** → /flows/cancellation-refund-dispute#cancellation — a refused cancel is a row too, keyed `order.already_cancelled`
- **Given** a cancelled order and a completed order. **When** the customer cancels each. **Then** the answers are `order.already_cancelled` and `order.already_completed`, and no refund or credit moves a second time.
- **DEV:** The completed half needs a finished job.

### E2E-CANCEL-033 — A customer cannot cancel, or learn about, someone else's order
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API
- **Rule:** → /flows/cancellation-refund-dispute#edge-cases — "Cancel by someone who does not own the order": refused, and the probe is recorded
- **Given** customer A's order and customer B signed in. **When** B calls cancel and preview with A's order id. **Then** both answer `order.not_found`, A's order is unchanged, and the admin sees a failure row on B (resource A's order) in the audit history of that order.
- **DEV:** Needs two customers.

### E2E-CANCEL-034 — The cancel reason is capped and never reaches the cleaner
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API · Web
- **Rule:** → /flows/cancellation-refund-dispute#cancellation — a reason is stored on the order; it is not added to cleaner DTOs
- **Given** a cancellable order. **When** the customer sends a 501-character reason, then a 500-character one. **Then** the first answers `common.max_length` and the second succeeds; the cleaner's job detail carries no customer reason.
- **DEV:** The own-cancel reason of an unstaffed booking (E2E-CANCEL-055) is stored separately.

### E2E-CANCEL-035 — A Plus member's window and free notice differ from a standard customer's
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API · Web
- **Rule:** → /product/business-rules#oops-window — an entitled Plus member has 60 minutes and a 4 h free notice
- **Given** a customer on a current Plus trial (see `loyalty-and-memberships.md`) with a returning-customer booking starting 10 h ahead and a cleaner on it. **When** they cancel 30 minutes after booking. **Then** it is free with rule `Plus`, `oopsMinutesApplied` 60; the order detail's `freeCancellationHours` reads 4 while entitled and 24 for a standard customer; a lapsed member cancels under standard figures.
- **DEV:** Subscribing uses a Stripe test subscription; the lapse half cannot run on DEV (no time to wait).

### E2E-CANCEL-036 — The order detail shows the free-cancellation figure the cancel will use
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API · Web
- **Rule:** → /flows/cancellation-refund-dispute#cancellation — `GetOrderDetails` carries `freeCancellationHours`
- **Given** a standard customer's order. **When** the order detail opens on the web. **Then** the *free cancellation* note states 24 hours, read from `freeCancellationHours`, and shows no note when the field is absent.
- **DEV:** The Plus value is in E2E-CANCEL-035.

## Guest cancellation

### E2E-CANCEL-040 — A guest previews and cancels a booking free with the access token
- **Priority:** P1
- **Role:** Guest
- **Surfaces:** API · Web
- **Rule:** → /product/business-rules#a-guest-cancels-under-the-same-policy — a guest cancels with the booking's access token under the same policy
- **Given** a guest's paid card booking 25 h ahead, reached through its access-token link. **When** the guest opens the preview (`POST api/Order/GuestCancellationPreview`) and cancels (`POST api/Order/CancelGuest`). **Then** the preview showed free and a refund of T; the booking is `Cancelled`, a card refund of T is recorded, and the same token no longer opens the booking (`Lookup` answers `order.not_found`).
- **DEV:** The token comes from the success-page link; the confirmation e-mail cannot be read. Guest cancel exists on the web only: both mobile apps run in a signed-in session.

### E2E-CANCEL-041 — A guest's late cancellation keeps the fee
- **Priority:** P2
- **Role:** Guest
- **Surfaces:** API · Web
- **Rule:** → /product/business-rules#a-guest-cancels-under-the-same-policy — the same fee tiers and oops window; no Plus extension
- **Given** a guest's paid card booking with a cleaner on it inside the 25 % tier, past the oops window. **When** the guest cancels. **Then** the refund is T − F, the booking is `Cancelled`, the cleaner is told, the crew's pay for the fee share is asked for, and no receivable is opened.
- **DEV:** The guest's oops window is 60 minutes on a first booking and 15 otherwise.

### E2E-CANCEL-042 — A guest cancel with a bad, expired or foreign token is refused identically
- **Priority:** P2
- **Role:** Guest
- **Surfaces:** API
- **Rule:** → /product/business-rules#a-guest-cancels-under-the-same-policy — unknown, expired, revoked and account-owned bookings all answer `order.not_found`
- **Given** a made-up token, a revoked token (after a cancel) and the order id of an account-owned booking. **When** each is sent to `CancelGuest` and `GuestCancellationPreview`. **Then** every call answers `order.not_found` with the same shape.
- **DEV:** A 30-day expiry cannot be waited for; only the revoked and invented cases run.

### E2E-CANCEL-043 — A guest cannot cancel after the start; the guest reports that the cleaner did not arrive
- **Priority:** P2
- **Role:** Guest
- **Surfaces:** API · Web
- **Rule:** → /product/business-rules#after-the-start — the guest reports through `ReportGuestNoShow`, which raises the alert and moves no money
- **Given** a guest booking whose start passed with a cleaner assigned who has not started. **When** the guest calls `CancelGuest`, then `ReportGuestNoShow`. **Then** the cancel answers `order.start_passed_cannot_cancel`; the report succeeds and raises `admin.order.cleaner_not_started` once; nothing is refunded; the order stays `Confirmed`.
- **DEV:** Before the start the report answers `order.start_time_not_reached`; once the cleaner has started, `order.cleaner_already_started`; run those two first.

### E2E-CANCEL-044 — A guest cancel of a started or finished booking is refused
- **Priority:** P2
- **Role:** Guest
- **Surfaces:** API
- **Rule:** → /product/business-rules#a-guest-cancels-under-the-same-policy — cancellation is refused once cleaning is under way, completed or cancelled
- **Given** a guest booking in progress, one completed, one cancelled. **When** each is sent to `CancelGuest`. **Then** the answers are `order.in_progress_cannot_cancel`, `order.already_completed` and `order.not_found` (the token of a cancelled booking was revoked).
- **DEV:** Needs three bookings in those states.

## No-show, unfilled bookings and the no-cleaner remedy

### E2E-CANCEL-050 — The customer reports that the cleaner did not arrive and the administrators are alerted
- **Priority:** P1
- **Role:** Customer
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /flows/cancellation-refund-dispute#dispute — a signed-in *service not provided* dispute on a staffed, unstarted job raises `admin.order.cleaner_not_started`
- **Given** the order of E2E-CANCEL-030. **When** the customer taps *the cleaner did not arrive* (a *service not provided* dispute). **Then** a dispute (reason 2) is open on the order, `admin.order.cleaner_not_started` is in the admin feed once for the order (the reminder sweep at start + 30 minutes would add none), and no money has moved.
- **DEV:** Run before the start + 30 reminder or after; the alert is once per order either way.

### E2E-CANCEL-051 — An administrator confirms the no-show: refund, credit, apology and the dispute closes
- **Priority:** P1
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /product/business-rules#when-the-cleaner-cancels-or-no-shows — the confirmation runs the sweep's body: no fee, whole refund, applied credit back, apology
- **Given** the order of E2E-CANCEL-050 (paid card, with credit if available). **When** a Support-or-above administrator uses *Cancel as a no-show* (`POST api/AdminOrder/cancel-no-show`). **Then** the order is `Cancelled` with reason `order.cancelled.no_cleaner_available`, a card refund of the whole price is made, applied credit comes back, the customer is credited the apology (250 on a CZK order), the open *service not provided* dispute is closed, the cleaner is told, and the customer gets the push `order.no_cleaner_refunded` naming the credit ("250 Kč") and an e-mail.
- **DEV:** Credit shows on the customer's credit page and in the admin credit ledger with the note *The assigned cleaner did not arrive.* The e-mail cannot be read.

### E2E-CANCEL-052 — The no-show confirmation is refused before the start, in progress, and on closed orders
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /product/business-rules#when-the-cleaner-cancels-or-no-shows — refusals: before the start, a started job, a completed or cancelled order
- **Given** a staffed order before its start, one in progress, one completed, one cancelled. **When** the administrator confirms a no-show on each. **Then** the answers are `order.start_time_not_reached`, `order.cleaner_already_started`, `order.already_completed` and `order.already_cancelled`, and a started job's dispute stays.
- **DEV:** Needs orders in those four states.

### E2E-CANCEL-053 — An administrator may call off an unstaffed booking before its start
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /flows/cancellation-refund-dispute#cancellation — before the start, *Cancel as a no-show* is accepted on a booking nobody is on that the sweep would cancel
- **Given** a paid card order nobody has taken, offered on the board, before its start. **When** the administrator uses *Cancel as a no-show*. **Then** the customer gets the same refund and apology the sweep would pay, the reason is `order.cancelled.no_cleaner_available`, and the ledger note reads *No cleaner was on the booking.*; the same action on a booking with a crew is refused `order.start_time_not_reached`.
- **DEV:** `admin.order.no_cleaner_yet` precedes it in the feed; the unfilled sweep runs on the 15-minute tick.

### E2E-CANCEL-054 — The unfilled-order sweep cancels, refunds and credits a booking nobody took
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /product/business-rules#when-the-cleaner-cancels-or-no-shows — 30 minutes past the slot, nobody ever took the seat
- **Given** a paid card order that no cleaner took, its slot past by more than 30 minutes. **When** the 15-minute tick runs. **Then** the order is `Cancelled` with reason `order.cancelled.no_cleaner_available`, the card is refunded in full, the apology credit (250 CZK) lands, the push says `order.no_cleaner_refunded`, and an e-mail is queued.
- **DEV:** Leave the order untaken until 30 minutes past the slot, then wait up to 15 minutes.

### E2E-CANCEL-055 — A customer's own cancel of an unstaffed booking from its start gets the no-cleaner remedy
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /flows/cancellation-refund-dispute#cancellation — the account owner's cancel of an unfilled booking from its start runs the no-cleaner body
- **Given** a paid card order nobody took, its booked start reached but the sweep not yet run. **When** the customer cancels with a typed reason. **Then** the order is cancelled with reason `order.cancelled.no_cleaner_available`, the customer is refunded in full and credited the apology, and the typed reason is retained separately and shown nowhere to cleaners.
- **DEV:** The window is between the start and the sweep's tick (30 minutes plus 15); a guest booking listed through a matching e-mail is cancelled on guest terms instead, with no apology credit.

### E2E-CANCEL-056 — A no-show on a cash order charges nothing, returns credit and still pays the apology
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API · Web
- **Rule:** → /product/business-rules#when-the-cleaner-cancels-or-no-shows — the push says `order.no_cleaner_nothing_charged` when the order took no payment
- **Given** a cash order whose assigned cleaner did not arrive. **When** an administrator confirms the no-show. **Then** no refund is made, the push is `order.no_cleaner_nothing_charged` naming the credit, the apology credit is paid, and no receivable is opened.
- **DEV:** Cash needs a saved card.

### E2E-CANCEL-057 — A guest's no-show is refunded without an apology
- **Priority:** P2
- **Role:** Guest
- **Surfaces:** API · Web
- **Rule:** → /flows/cancellation-refund-dispute#cancellation — a guest has no credit account and gets the refund only
- **Given** a guest card booking whose assigned cleaner did not arrive. **When** the administrator confirms the no-show. **Then** the card is refunded in full, no credit row is created for anyone, the booking is `Cancelled`, and the guest's old links stop working.
- **DEV:** The guest e-mail cannot be read; the check is the order state through admin and the revoked token.

### E2E-CANCEL-058 — Plain *Cancel order* pays no apology
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /product/business-rules#platform-cancellation — the plain administrator cancel serves a phone request, a duplicate or a test, with no apology
- **Given** a staffed paid card order. **When** the administrator cancels it with *Cancel order* and a reason. **Then** the card is refunded in full, no apology credit is paid, the signed-in customer is told by e-mail and (unless the refund notice went) by push `order.cancelled`, and the assigned cleaner is told.
- **DEV:** The push is checked on iOS or Android.

## Administrator cancellation

### E2E-CANCEL-060 — An administrator cancels a paid card order with a full refund
- **Priority:** P1
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /product/business-rules#platform-cancellation — a platform cancellation charges no fee
- **Given** a paid card order with a cleaner on it, inside the 50 % tier. **When** a Support-or-above administrator cancels it (`POST api/AdminOrder/cancel`) with a reason of 500 characters. **Then** the order is `Cancelled`, the card refund is T, the cancel's response reports no fee, the customer sees the order cancelled and refunded, and the cleaner's job is off.
- **DEV:** The admin audit row is `order.cancel` (name unsure; check the audit page).

### E2E-CANCEL-061 — The administrator cancel validates and refuses closed orders
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /flows/cancellation-refund-dispute#cancellation — a cancel on a closed or running order is refused
- **Given** a cancelled order, a completed order, one in progress, and a reason of 501 characters. **When** the administrator cancels each. **Then** the answers are `order.already_cancelled`, `order.already_completed`, `order.in_progress_cannot_cancel` and `common.max_length`.
- **DEV:** A job in progress is refused by the plain cancel; the lockout confirmation is admitted on one (E2E-CANCEL-075).

### E2E-CANCEL-062 — Only Support and above may cancel, refund or confirm no-shows
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /product/business-rules#when-the-cleaner-cancels-or-no-shows — the no-show confirmation is Support and above; refund, repayment and dispute actions use the same level
- **Given** an Accountant, a Support user and a Manager on DEV. **When** each calls cancel, cancel-no-show, cancel-lockout, refund, partial refund, retry, record-repayment and dispute resolve. **Then** the Accountant is answered 403 on every one; Support and Manager are admitted (the write-off is Manager and above, see `payment-and-fiscal.md`); a customer token is answered 401 or 403.
- **DEV:** Needs admin users of three roles created in user management. The levels for refund, repayment and dispute come from the permission map in code (Support or above; the Accountant is not in that set), not from a docs page.

## Lockout

### E2E-CANCEL-070 — The cleaner reports a lockout with the entrance photo and a note
- **Priority:** P2
- **Role:** Cleaner
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /flows/cancellation-refund-dispute#lockout — from start + 15 minutes, with an entrance photo, the cleaner reports it and nothing is cancelled
- **Given** a staffed order, 20 minutes past its start, not started, with an `Entrance` photo uploaded. **When** the cleaner reports *I cannot get in* with a note of calls. **Then** `POST api/Order/ReportLockout` succeeds, `admin.order.lockout_reported` appears in the admin feed, the order stays `Confirmed` and nothing is charged or refunded; the apps open the report at the 15-minute mark.
- **DEV:** Wait until start + 15 minutes. The note is required and at most 1 000 characters.

### E2E-CANCEL-071 — The lockout report is refused when it is too early, photo-less, repeated, closed, or off the crew
- **Priority:** P2
- **Role:** Cleaner
- **Surfaces:** API
- **Rule:** → /flows/cancellation-refund-dispute#lockout — the five refusals and the note limits
- **Given** an assigned cleaner, another cleaner off the crew, a note of 1 001 characters and an empty note. **When** the assigned cleaner reports before start + 15 minutes, without an entrance photo, a second time, and on a completed order; the outsider reports; and the note limits are tried. **Then** the answers are `order.lockout.too_early`, `order.lockout.photo_required`, `order.lockout.already_reported`, `order.lockout.order_closed`, `order.not_found`, `common.max_length` and `common.required`.
- **DEV:** The order must sit in these states in turn; use separate orders where needed.

### E2E-CANCEL-072 — An administrator confirms a lockout on a paid card order: the whole price is kept
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /product/business-rules#lockout — fee 100 %, no refund, payment and credit kept
- **Given** the reported lockout of E2E-CANCEL-070 on a paid card order. **When** a Support-or-above administrator confirms it (`POST api/AdminOrder/cancel-lockout`). **Then** the order is `Cancelled` with reason `order.cancelled.customer_lockout`, fee rate 1.0, no refund, payment stays `Paid`, applied credit is kept, the express waiver stays consumed, the customer gets `order.cancelled` and an e-mail stating the whole price, and each seat is paid its full job reward (cleaner My Pay shows a lockout line).
- **DEV:** The customer apps do not yet render the reason key, so the e-mail is where the reason reads and it cannot be read here; assert the key through the API.

### E2E-CANCEL-073 — A lockout on a signed-in customer's cash booking returns credit and owes the whole price
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /product/business-rules#lockout — an unpaid cash booking becomes a *Lockout* receivable for the whole price
- **Given** a reported lockout on a cash booking. **When** the administrator confirms it. **Then** a *Lockout* receivable for T is open, credit applied (none on cash) is returned, new bookings are refused with `order.unpaid_receivable`, the crew is still paid in full at the confirmation, and paying the pay link later asks for no second crew pay.
- **DEV:** The cleaner's pay is checked once, right after the confirmation.

### E2E-CANCEL-074 — A guest's lockout keeps the prepayment and charges nothing more
- **Priority:** P2
- **Role:** Guest
- **Surfaces:** API · Web
- **Rule:** → /product/business-rules#lockout — a guest always prepays and is never charged beyond it
- **Given** a reported lockout on a guest's paid card booking. **When** the administrator confirms it. **Then** the booking is `Cancelled`, the payment is kept (no refund), no receivable exists, and the guest's links stop working.
- **DEV:** The guest cancellation e-mail cannot be read.

### E2E-CANCEL-075 — The lockout confirmation needs a report and an open order
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /flows/cancellation-refund-dispute#lockout — `order.lockout.not_reported`, and not on a cancelled or completed order
- **Given** an order with no lockout report, a cancelled one and a completed one. **When** the administrator confirms a lockout on each. **Then** the answers are `order.lockout.not_reported`, `order.already_cancelled` and `order.already_completed`; a job in progress with a report is admitted.
- **DEV:** Admitted-in-progress needs the cleaner to start after reporting.

### E2E-CANCEL-076 — A lockout confirmed on an already refunded order still pays the seat
- **Priority:** P2
- **Role:** Cleaner
- **Surfaces:** API · Web
- **Rule:** → /product/business-rules#lockout-pay — a lockout pays the seat's full reward, always
- **Given** a reported lockout on a card order that an administrator already refunded. **When** the lockout is confirmed. **Then** the seat's lockout pay line still appears for the full job reward and no collected fee is read.
- **DEV:** Combines E2E-CANCEL-070 with a refund before the confirmation.

## Refunds

### E2E-CANCEL-080 — An administrator refunds a paid card order in full and the customer sees it
- **Priority:** P1
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /flows/cancellation-refund-dispute#refund — the Stripe call happens before the status flips; the full refund is one seam
- **Given** a paid card order (test card 4242) that a cleaner has taken or that is completed. **When** the administrator presses *Full refund* (`POST api/AdminOrder/refund`) and confirms. **Then** the card refund of T succeeds in the same command, payment reads `Refunded`, the Refunds page lists it *Refunded* with origin full refund and a Stripe refund id, the customer gets `order.refunded` (push and inbox) and the order detail reads refunded; the customer's loyalty points for the order are taken back.
- **DEV:** The loyalty clawback shows only when points were granted (a completed order); see E2E-CANCEL-086.

### E2E-CANCEL-081 — A second full refund, or a refund on a cash order, is refused
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /flows/cancellation-refund-dispute#refund — a cash order has no card to refund; a refunded order is refused
- **Given** the refunded order of E2E-CANCEL-080 and a paid cash order. **When** the administrator refunds each. **Then** both answer `refund.order_not_refundable`, no Stripe call is made and no notice is queued.
- **DEV:** The cash order is paid after the cleaner records the cash.

### E2E-CANCEL-082 — A partial line refund refunds the share of the lines chosen
- **Priority:** P1
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /flows/cancellation-refund-dispute#refund — `IssuePartialRefund` loads every component of the split
- **Given** a completed paid card order with a service and an extra. **When** the administrator refunds the service line only (`POST api/AdminRefund/partial`) with a reason. **Then** the refund is the service's share of what was paid (before any processing fee), the extra stays in the denominator, payment reads `PartiallyRefunded`, the Refunds page lists it with origin partial refund, and the customer gets a refund notice.
- **DEV:** The refund window is 14 days from completion, so a freshly completed order needs no override reason.

### E2E-CANCEL-083 — A partial refund validates its lines and the override reason
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /flows/cancellation-refund-dispute#refund — lines are required and must be on the order; past the window a written reason is required
- **Given** a completed paid card order and one never completed. **When** the administrator sends no lines, a line with no service id, a line not on the order, and an override reason of 501 characters; and sends a valid line on the never-completed order with no override reason. **Then** the answers are `refund.lines_required`, `refund.line_invalid`, `refund.line_invalid`, `common.max_length`, and `refund.override_reason_required`; with a non-empty override reason the never-completed order's refund is accepted.
- **DEV:** The 14-day window cannot be aged on DEV, so the never-completed order stands in for a closed window.

### E2E-CANCEL-084 — A refund is bounded by what is left
- **Priority:** P1
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /flows/cancellation-refund-dispute#edge-cases — "Refund more than was paid": clamped to what remains, refused at zero
- **Given** a paid card order T partly refunded twice until little remains. **When** the administrator asks a partial refund larger than the remainder, and then another once nothing remains. **Then** the first is clamped to what remains and the order reads `Refunded` when the card has given back all it took; the second answers `refund.nothing_refundable` and nothing moves.
- **DEV:** Three admin calls on one order.

### E2E-CANCEL-085 — A refund of an order paid partly with credit splits across card and credit
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /flows/cancellation-refund-dispute#refund — the slice is split in the proportion the price was paid
- **Given** a card order paid with credit C and card (T − C). **When** the administrator refunds in full. **Then** the card gets T − C, the customer's credit balance gets C back (ledger lines under the refund's key), and the total returned is T; payment reads `Refunded`.
- **DEV:** Credit granted first by the administrator.

### E2E-CANCEL-086 — A refund takes back its share of loyalty points
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /flows/cancellation-refund-dispute#refund — every refund takes back floor(earned × returned / total) of the completion earn
- **Given** a completed card order that earned points. **When** the administrator refunds half of it, then the rest. **Then** the customer's points fall by half the earn after the first refund and by the remainder after the second, with no point left behind and none taken twice if the same lines are refunded again.
- **DEV:** Points show on the rewards page (web, apps). See `loyalty-and-memberships.md` for the grant.

### E2E-CANCEL-087 — A refund before completion is taken back at completion
- **Priority:** P2
- **Role:** Cleaner
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /flows/cancellation-refund-dispute#refund — a card order refunded before the job ends can still be completed
- **Given** a card order that an administrator partly refunded while the job was in progress. **When** the cleaner completes the job. **Then** the completion succeeds (no `order.payment_not_confirmed`), the crew pay is asked for, and the points granted are net of the share of what was returned.
- **DEV:** Needs a job started and refunded before completion.

### E2E-CANCEL-088 — The Refunds page lists refunds by state and origin
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /flows/cancellation-refund-dispute#refund-states — six states worked out from the row
- **Given** refunds from the cases above. **When** the administrator opens `/order-management/refunds`. **Then** it opens on *Needs action* (Not sent, Failed, Bounced), the state filter picks each of the six states alone, the origin column reads cancellation, dispute, full refund or partial refund, and `?orderId=` scopes the list; a cancellation made for *service not rendered* reads as a cancellation.
- **DEV:** Not sent, Failed and Closed rows cannot be made on DEV (see the closing section) and are checked only when they exist.

### E2E-CANCEL-089 — An order whose card refunds, credit and settlements reach the price reads Refunded
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /flows/cancellation-refund-dispute#refund — the seam writes `Refunded` when the card gave back all it took or the sum reaches `TotalPrice`
- **Given** a card order partly refunded in two steps adding up to T. **When** the second refund lands. **Then** payment reads `Refunded` (not `PartiallyRefunded`); two refunds of 600 and 400 on a 1 000 order give `Refunded`.
- **DEV:** The same end state through a dispute settled in credit is E2E-CANCEL-111.

## Failed, bounced and repaid refunds

### E2E-CANCEL-090 — A refund Stripe accepts and then fails is marked Bounced and every administrator is told
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /flows/cancellation-refund-dispute#refund-states — `refund.failed` stamps the row; a *Refunded* one becomes *Bounced*
- **Given** a card order paid with 4000 0000 0000 5126 (pays, then fails any refund). **When** an administrator refunds it in full and the `refund.failed` webhook lands. **Then** the Refunds page shows the row *Bounced* (money left and came back), payment still reads `Refunded`, `admin.payment.refund_bounced` is in the feed for every admin role including the Accountant, and the customer is still owed the money.
- **DEV:** Stripe sends the failure some time after the refund; wait until the row changes (minutes) before concluding.

### E2E-CANCEL-091 — A bounced refund is repaid by bank transfer
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /flows/cancellation-refund-dispute#refund-states — *Record repayment* takes a reference of up to 100 characters
- **Given** the bounced refund of E2E-CANCEL-090. **When** the administrator records a repayment by transfer with a reference of 101 characters, then of 100. **Then** the first answers `common.max_length`, the second succeeds and the state reads *Repaid*; a *Bounced* refund already counted as given back, so the order's payment status and points do not change again.
- **DEV:** Money outside the platform is not moved; the record only stamps it.

### E2E-CANCEL-092 — Repayment as credit is available only to a customer with an account who chose it
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /flows/cancellation-refund-dispute#refund-states — credit repayment needs an account that chose credit; `refund.credit_unavailable` otherwise
- **Given** a bounced refund of a guest's booking and one of an account whose dispute settlement choice was credit. **When** the administrator records each repayment as credit. **Then** the guest's answers `refund.credit_unavailable`; the account's repayment succeeds, creates a Goodwill credit in the order's currency keyed `{refundKey}:credit`, and the state reads *Repaid*.
- **DEV:** The UI offers the credit button only when the customer chose credit; the API refusal is the check.

### E2E-CANCEL-093 — Retry and repayment refuse refunds in the wrong state
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /flows/cancellation-refund-dispute#refund-states — retry for *Not sent* only; repayment for *Failed* and *Bounced* only
- **Given** a *Refunded* refund, another company's or unknown refund id. **When** the administrator retries the *Refunded* row, records a repayment on it, and retries an unknown id. **Then** the answers are `refund.not_retryable`, `refund.not_owed` and `refund.not_found`.
- **DEV:** One company on DEV, so "another company's" is the unknown id.

### E2E-CANCEL-094 — A *Failed* refund starts counting as given back only when repaid
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /flows/cancellation-refund-dispute#refund-states — recording the repayment of a *Failed* refund counts it, once
- **Given** a *Failed* refund (Stripe failed it on its key). **When** the administrator records its repayment. **Then** the payment status, the credit share returned and the loyalty clawback follow once, and a record that the order has no room for is refused `refund.nothing_refundable`.
- **DEV:** A *Failed* row needs a Stripe failure found by a retry; unreachable on DEV, so the case is only listed. See the closing section.

## Manual credit

### E2E-CANCEL-095 — An administrator grants credit by hand and may not settle a dispute with it
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /flows/cancellation-refund-dispute#dispute — *Issue credit* cannot settle a dispute
- **Given** a customer account. **When** the administrator issues goodwill credit (`POST api/AdminCredit/issue`), then tries the reason *Dispute settlement*, a spend-side reason, an amount with more than two decimals, and an amount above the sanity cap. **Then** goodwill succeeds and shows on the customer's balance (web, apps); the others answer `credit.dispute_settlement_not_issuable`, `credit.reason_not_issuable`, `credit.amount_not_whole_minor_units` and `credit.amount_exceeds_sanity_cap`.
- **DEV:** The sanity cap value is not in the docs read for this case; use a very large amount.

## Disputes: filing

### E2E-CANCEL-100 — A customer files a complaint after the clean and the administrators are told
- **Priority:** P1
- **Role:** Customer
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /flows/cancellation-refund-dispute#dispute — a filing writes one feed row per administrator Support or above and one e-mail per address
- **Given** a completed paid order. **When** the customer files a dispute (reason, description of 10 to 2 000 characters, a line or two, settlement preference card refund by default). **Then** the dispute is `Pending` and appears in the customer's list; the admin feed holds `admin.dispute.filed` (order number and reason, no text) for Support and above and none for an Accountant; the admin dispute list and detail show it with *Card refund* as the settlement choice and `FiledWithinWindow` true.
- **DEV:** The form exists on the web and both customer apps (dispute reasons 1 to 7 are customer-selectable; 8 is a chargeback).

### E2E-CANCEL-101 — The complaint filing validates its fields
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API · Web
- **Rule:** → /product/business-rules#disputes — description 10 to 2 000 characters, up to 50 lines, only lines on the order
- **Given** a completed order. **When** the customer files with an empty description, 9 characters, 2 001 characters, 51 lines, and a line the order does not have. **Then** the answers are `common.required`, `common.min_length`, `dispute.max_length_exceeded`, `dispute.max_length_exceeded` and `dispute.line_not_on_order`; the line failure is a row on the order's audit history.
- **DEV:** Per `CreateDispute` validators and handler.

### E2E-CANCEL-102 — A complaint about a clean that has not started is refused, and only one dispute is open per order
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /product/business-rules#what-cannot-be-disputed — the gate is the scheduled time, and a new dispute waits for the open one
- **Given** a future booking and a completed one with an open dispute. **When** the customer files on the future one and again on the completed one. **Then** the answers are `dispute.cleaning_not_started` and `dispute.already_exists`; after the open dispute is resolved or closed, filing again is accepted.
- **DEV:** The refused filing is a failure row on the order, not on a dispute.

### E2E-CANCEL-103 — A complaint filed after the 24 hour window is still heard
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /product/business-rules#disputes — the window decides what is promised, not what is heard
- **Given** a completed order more than 24 hours old. **When** the customer files a dispute. **Then** it is accepted, and the admin detail shows it filed outside the window (`FiledWithinWindow` false).
- **DEV:** Needs an order completed over a day earlier; run on the second day of the run.

### E2E-CANCEL-104 — Messages and evidence stay between the parties of the dispute
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /flows/cancellation-refund-dispute#dispute — dispute messages and evidence write no audit row; ownership is checked on the dispute
- **Given** customer A's open dispute and customer B signed in. **When** A adds a message and an image, the administrator replies, and B adds a message and an upload to A's dispute. **Then** A's and the administrator's messages appear in both views in order; B's two calls answer `dispute.not_owned_by_user`; an evidence image shows as a thumbnail in the admin detail.
- **DEV:** Needs two customers; file types are limited by the upload validator.

## Disputes: resolution

### E2E-CANCEL-110 — An administrator resolves a complaint with a card refund
- **Priority:** P1
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /flows/cancellation-refund-dispute#dispute — resolving with a refund moves the money first, then writes the dispute
- **Given** the open dispute of E2E-CANCEL-100 on a paid card order. **When** the administrator resolves it with notes and a refund of R less than T (`POST api/AdminDispute/resolve`). **Then** R goes back to the card first, the dispute is `Resolved`, the detail shows *Refund requested* R, *Refunded to card* R and *Returned as credit* 0, the customer gets `order.refunded`, the order's payment reads `PartiallyRefunded`, and the audit row carries all three figures.
- **DEV:** The customer sees the resolved dispute on web and in the apps.

### E2E-CANCEL-111 — A complaint the customer chose to settle in credit is paid as credit
- **Priority:** P1
- **Role:** Customer
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /product/business-rules#dispute-settlement — with credit chosen and an account, the amount is issued as credit
- **Given** a dispute filed with *Credit* as the settlement choice. **When** the administrator resolves it with an amount R. **Then** R lands on the customer's balance in the order's currency keyed to the dispute, the card is untouched, the detail shows *Returned as credit* R and *Refunded to card* 0, and loyalty points are taken back by R's share.
- **DEV:** Credit shows on the customer's credit page the same minute.

### E2E-CANCEL-112 — The resolution validates notes and amounts
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /product/business-rules#dispute-settlement — notes required up to 2 000 characters; a credit settlement is whole cents and no more than what is left
- **Given** an open dispute. **When** the administrator resolves with no notes, with 2 001 characters of notes, with a negative refund, and (credit chosen) with more than the order has left and with three decimals. **Then** the answers are `common.required`, `dispute.max_length_exceeded`, `dispute.invalid_refund_amount`, `dispute.invalid_refund_amount` and `dispute.invalid_refund_amount`; the dispute stays open and nothing moves.
- **DEV:** The web form also blocks blank notes with a translated message.

### E2E-CANCEL-113 — A dispute resolves once; the status machine refuses a skipped step
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /flows/cancellation-refund-dispute#dispute — a guarded state machine; terminal states are never resolved twice
- **Given** a resolved dispute and an escalated one. **When** the administrator resolves the resolved one again, moves the escalated one to *Under review*, and moves a pending one to *Under review*, then *Waiting for response*, then *Closed*. **Then** the answers are `dispute.already_resolved`, `dispute.invalid_status_transition`, and the valid chain succeeds; a closed dispute accepts no further status.
- **DEV:** Pending, under review and waiting move among themselves and into closed or escalated; escalated goes only to closed.

### E2E-CANCEL-114 — A card-refund complaint on a cash order cannot be refunded to a card
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /flows/cancellation-refund-dispute#refund — a cash booking has no card charge; credit settles it when the customer chose credit
- **Given** two cash-order disputes, one with card refund chosen, one with credit. **When** the administrator resolves each with an amount. **Then** the first answers `refund.order_not_refundable` and stays open (the detail warns *money back (paid in cash)*); the second is settled in credit.
- **DEV:** Cash needs a saved card; the disputes need completed cash jobs.

### E2E-CANCEL-115 — A finding of fault charges the cleaner and the cleaner reads it
- **Priority:** P2
- **Role:** Cleaner
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /product/business-rules#dispute-cleaner-charge — `chargeToCleaner` deducts from that cleaner's pay on the order, with the reason shown
- **Given** a dispute on a completed order whose pay row is not yet invoiced. **When** the administrator resolves it with a refund and charges the cleaner an amount A with a reason. **Then** the cleaner's pay record for the order shows a deduction A with the reason (partner web, iOS, Android); a charge above the job's pay or a second charge for the same dispute answers `dispute.cleaner_charge_not_chargeable`; three decimals answer `dispute.cleaner_charge_not_whole_minor_units`; and a refund alone, with no charge, touches no pay.
- **DEV:** An already invoiced job is charged on the next invoice instead; that needs the pay-period flow (`pay-and-payouts.md`).

### E2E-CANCEL-116 — A bank chargeback links to the customer's open dispute
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /flows/cancellation-refund-dispute#dispute — the chargeback is reflected onto the order's open dispute, and `admin.dispute.chargeback` names it
- **Given** an order paid with 4000 0000 0000 0259 and an open customer dispute on it. **When** Stripe raises the chargeback. **Then** no second dispute is created; the open dispute carries the Stripe status, and `admin.dispute.chargeback` names that dispute for every admin role.
- **DEV:** Same Stripe-card and consent caveat as E2E-PAY-033.

## Not testable on DEV

- The fee boundaries to the minute (exactly 24 h, exactly 4 h, exactly 15 or 60 minutes): the tests cannot land on an instant. They stay with the unit tests.
- A cancellation ladder frozen on the order (a later change of the ladder reaching only later bookings), and a Plus member who lapsed after booking: need a deploy of a changed constant or waiting out a membership.
- Stripe unreachable or refusing during a cancel, a no-show, the unfilled sweep, an administrator's cancellation or the wind-down: `refundPending`, the credit leg returned at once on the refund's key, the hourly re-drive, `admin.payment.refund_needs_retry`, `refund_stuck` after 24 hours and `refund_without_cancel`. The agents cannot make Stripe fail a call on demand; 4000 0000 0000 5126 only fails the refund afterwards.
- A retry that asks Stripe first (`FindRefundAsync`), a refund Stripe failed on its key closing as *Failed*, *Closed* rows, two refunds claimed at once, two partial refunds settling at once, and a re-driven refund after a complaint settled in credit: all need a pending row or concurrent calls.
- A dispute whose card refund is pending (`dispute.refund_pending`, closing refused, no-show leaving it open, resolved again after Stripe failed or confirmed other refunds): needs a pending dispute refund.
- The company wind-down (`order.cancelled.company_wind_down`) and its retry pass, a frozen company's credit returns, an erased account's credit: need company lifecycle changes, which the owner does on DEV.
- A recurring occurrence cancelled unconfirmed (`order.cancelled.recurring_not_confirmed`): needs the materialiser and the lead-time cut-off.
- One of two cleaners missing (stays a dispute): crews are rare and a two-cleaner job needs a large home.
- A chargeback matching no order, the 24 h filing window ageing (E2E-CANCEL-103 runs only on the second day), the 14-day partial-refund window ageing, and a 10-year receipt retention: time cannot be moved on DEV.
- Concurrency (two refunds, two retries, two cancels at once) and the dispute transition checker that fails the build: not user-observable.
- Real e-mail copy (guest cancellation, no-cleaner, lockout e-mails with their money lines), the lock-screen live activity ending, and the customer apps' rendering of `order.cancelled.customer_lockout` (not yet rendered): e-mail to `@example.com` never arrives and the key has no screen text.
- Apple and Google sign-in, load: out of scope of Phase 1.
