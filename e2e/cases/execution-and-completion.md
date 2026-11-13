# Execution and completion: cases

From an assigned cleaner to a finished job, a receipt and a pay row, and the order lifecycle that carries it. Cleaner A and Cleaner B are two approved cleaners who accepted the cleaner documents; every job here was taken by the cleaner (TAKE flow) unless the case says an administrator placed them; a "two-cleaner job" needs more than 120 booked minutes (on DEV services seed at 2 minutes, so an admin raises a service duration first and restores it after).

## On my way and start

### E2E-EXEC-001 — The assigned cleaner sets off and the customer is told
- **Priority:** P1
- **Role:** Cleaner (Customer observes)
- **Surfaces:** API · iOS · Android
- **Rule:** → /flows/execution-and-completion#the-path — "on my way" moves the order to `OnTheWay` and the customer gets a push
- **Given** Cleaner A is on a `Confirmed` job whose start is within 60 minutes. **When** Cleaner A taps On my way (`NotifyOnTheWay`). **Then** the order is `OnTheWay`; the Customer's order page and the customer apps show "on the way"; the customer gets `order.on_the_way` (iOS adds a Live Activity). On a job that is neither `Confirmed` nor `OnTheWay` (a `New` or `Completed` one) the call answers `order.not_confirmed`.
- **DEV:** the partner web app has no On my way action (none found in the partner web libraries), so the web is not a surface. The push needs a device; the simulator check is the order status.

### E2E-EXEC-002 — Start moves the order to InProgress
- **Priority:** P1
- **Role:** Cleaner (Customer, Admin observe)
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /flows/execution-and-completion#the-path — start moves the order to `InProgress` and the customer gets a push
- **Given** Cleaner A is on a job within the start window, once `Confirmed` and once `OnTheWay`. **When** Cleaner A starts it. **Then** both orders become `InProgress`; the start time is recorded and the elapsed timer runs on the app; the Customer sees "in progress" (iOS Live Activity); Admin's order detail shows `InProgress` and the start stamp.
- **DEV:** the job can be started from 60 minutes before its slot, so book the earliest slot (2 h ahead) and start about an hour after booking.

### E2E-EXEC-003 — Start and On my way are refused before the grace window
- **Priority:** P1
- **Role:** Cleaner
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /product/business-rules#start-grace-window — a job may be moved at most 60 minutes ahead of its time; earlier is `order.too_early_to_start`
- **Given** Cleaner A is on a `Confirmed` job booked for a slot more than 60 minutes ahead (at 61 minutes or more). **When** Cleaner A sends `NotifyOnTheWay`, then `StartOrder`; and again 59 minutes before the slot. **Then** the first two answer `order.too_early_to_start`, the status stays `Confirmed`, the customer gets no push; at 59 minutes before the slot both succeed.
- **DEV:** the same booking as E2E-EXEC-002; test at about 90 minutes before the slot and at about 59.

### E2E-EXEC-004 — Starting late is allowed and recorded
- **Priority:** P2
- **Role:** Cleaner
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /flows/execution-and-completion#edge-cases — a cleaner who starts three hours late is allowed and it is recorded
- **Given** a `Confirmed` job whose slot ended hours ago and is not yet cancelled. **When** Cleaner A starts it. **Then** the start succeeds with the late start time.
- **DEV:** the unfilled sweep cancels an unstaffed order 30 minutes after the start, but a staffed job is not cancelled by it; wait about 3 hours after the slot (long wait).

### E2E-EXEC-005 — Only the assigned cleaner may move the job
- **Priority:** P1
- **Role:** Cleaner (two accounts)
- **Surfaces:** API
- **Rule:** → /flows/execution-and-completion#only-the-assigned-cleaner-may-move-the-job — the gate is the assignment, not the role
- **Given** Cleaner A is on a `Confirmed` job and on an `InProgress` cash job, and Cleaner B (approved) is on neither. **When** Cleaner B sends `NotifyOnTheWay` and `StartOrder` for the first and `MarkCashCollected`, `MarkMyPartDone`, `CompleteOrder` and `ReportCashNotPaid` for the second. **Then** every call is refused and the jobs are unchanged; the answer is `order.employee_not_assigned` for all but `ReportCashNotPaid`, which answers `order.not_found` (its chain hides the order from a non-member). While the job is still more than 60 minutes ahead Cleaner B's Start answers `order.employee_not_assigned`, not `order.too_early_to_start`.
- **DEV:** the second half proves the clock rule is last, so the board is no schedule oracle.

### E2E-EXEC-006 — The transition guard refuses a move out of order
- **Priority:** P1
- **Role:** Cleaner
- **Surfaces:** API
- **Rule:** → /flows/execution-and-completion#edge-cases — a status moved out of order is refused by the transition guard
- **Given** Cleaner A is on one `Completed` job and on one job still `Confirmed`. **When** Cleaner A sends `StartOrder` for a `Completed` job, and `CompleteOrder` and `MarkCashCollected` for a job still `Confirmed`. **Then** the Start answers `order.not_confirmed`; Complete and Mark cash answer `order.not_in_progress`.
- **DEV:** uses jobs left over from E2E-EXEC-036.

### E2E-EXEC-007 — Start and Complete are refused without the contract for work, travel is not
- **Priority:** P1
- **Role:** Admin places, Cleaner acts
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /flows/execution-and-completion#the-contract-gate — Start and Complete refuse a seat with no acceptance with `contract.acceptance_required`; On my way is not gated
- **Given** Admin placed Cleaner A on a job (E2E-TAKE-049) and A has not accepted the contract. **When** A sends On my way inside the window, then Start, then (once `InProgress` through Admin's override) Complete. **Then** On my way succeeds; Start answers `contract.acceptance_required` and so does Complete; the app opens the contract sheet in accept mode, A accepts, and the same Start then succeeds. With the job also more than 60 minutes ahead, Start answers `contract.acceptance_required` before `order.too_early_to_start`.
- **DEV:** shares the placement fixture of E2E-TAKE-049 and -050.

### E2E-EXEC-008 — A customer or an administrator token cannot start a cleaner's job
- **Priority:** P2
- **Role:** Customer, Admin
- **Surfaces:** API
- **Rule:** → /flows/execution-and-completion#only-the-assigned-cleaner-may-move-the-job — a non-participant cannot advance somebody else's job
- **Given** a `Confirmed` job in the start window. **When** a Customer token and an Admin token call `StartOrder` on the partner host. **Then** the Customer is refused at the host and the Admin gets a business refusal (the key is unsure; the case records it); the job stays `Confirmed`.
- **DEV:** the Admin key is a finding only if the job moved.

### E2E-EXEC-009 — A re-added seat needs the contract again
- **Priority:** P2
- **Role:** Admin, Cleaner
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /flows/execution-and-completion#the-contract-gate — a cleaner who dropped and was re-added has a new seat and no acceptance
- **Given** Cleaner A accepted a placement, dropped the job, and Admin placed A again (E2E-TAKE-054). **When** A starts the job. **Then** Start answers `contract.acceptance_required`; after A accepts, Start succeeds. A second crew member who neither starts nor completes is never prompted.
- **DEV:** the second sentence needs a two-cleaner job.

### E2E-EXEC-010 — One job in progress at a time
- **Priority:** P2
- **Role:** Cleaner
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /flows/execution-and-completion#only-the-assigned-cleaner-may-move-the-job — `StartOrder` also refuses a cleaner who has another job in progress (`order.employee_already_has_order_in_progress`); the docs do not state this rule, the code does
- **Given** Cleaner A is on two non-overlapping `Confirmed` jobs both inside the start window and has started the first. **When** A starts the second. **Then** the call answers `order.employee_already_has_order_in_progress` and the second job stays `Confirmed`; after A completes the first, the start succeeds.
- **DEV:** two slots a quarter hour apart; unsure whether the docs name this refusal, so the triager decides.

### E2E-EXEC-011 — The reminders come two hours out and half an hour out
- **Priority:** P2
- **Role:** Cleaner
- **Surfaces:** iOS · Android
- **Rule:** → /flows/execution-and-completion#start-grace-window — a cleaner is told two hours out and nudged half an hour out; the nudge is dropped once on the way or out on another job
- **Given** two `Confirmed` jobs for Cleaner A, one A will set off for early and one A will not. **When** time passes. **Then** A gets a reminder about two hours out for both; at about half an hour out A gets the nudge for the job A did not set off for and none for the job already `OnTheWay`; none while A is out on another job.
- **DEV:** needs a device; the booking lead time of 2 h means the first reminder may arrive at once.

### E2E-EXEC-012 — A job that never starts raises an administrator alert and nothing else
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** Web · API
- **Rule:** → /flows/execution-and-completion#start-grace-window — an order `Confirmed` or `OnTheWay` with a cleaner on it whose start passed more than 30 minutes ago raises `admin.order.cleaner_not_started` once, and cancels nothing
- **Given** a `Confirmed` job with Cleaner A on it that nobody starts. **When** Admin watches the feed from the slot start to 45 minutes after it. **Then** `admin.order.cleaner_not_started` appears once, 30 minutes or more after the start, and the order is still `Confirmed`; a job where A set off (`OnTheWay`) also raises it; a job with nobody on it raises `admin.order.no_cleaner_yet` instead and is cancelled by the unfilled sweep.
- **DEV:** wait slot + 30 minutes + the five-minute sweep.

### E2E-EXEC-013 — Admin confirms the no-show
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** Web · API
- **Rule:** → /admin-app/order-management#cancel-as-no-show — Cancel as a no-show cancels with the unfilled sweep's remedy; refused before the start and once a cleaner has started
- **Given** the job of E2E-EXEC-012 (paid by card). **When** Admin first tries Cancel as a no-show on a job whose start is ahead (`order.start_time_not_reached`) and on a started job (`order.cleaner_already_started`), then confirms it on the stale job. **Then** the order is `Cancelled`, the card refund is whole, an apology credit is added (250 CZK on a CZK order), and the response says `refundedAmount`, `refundPending` and `apologyCredit`; the assigned cleaner is told. Money details belong to the CANCEL cases.
- **DEV:** Stripe test mode; the refund works with the 4242 card.

## Photos

### E2E-EXEC-014 — A before photo can be taken from the moment a cleaner holds the job
- **Priority:** P1
- **Role:** Cleaner
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /flows/execution-and-completion#photo-windows — a before photo is allowed in `Confirmed`, `OnTheWay` and `InProgress`
- **Given** Cleaner A on a `Confirmed` job. **When** A uploads a before photo (`UploadPhoto`, `SavePhotos`) while `Confirmed`, again while `OnTheWay`, and again while `InProgress`. **Then** every upload succeeds and the photo list (`GetPhotos`) holds all three with type "before"; the photo link expires after 15 minutes; Android and iOS offer the camera only; the web asks for the rear camera.
- **DEV:** the file is a small JPEG; the apps' camera is replaced by the simulator or emulator camera (unsure it works on the simulator; the API run is the fallback).

### E2E-EXEC-015 — An after photo only while the work is under way
- **Priority:** P1
- **Role:** Cleaner
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /flows/execution-and-completion#photo-windows — an after photo only while `InProgress`; otherwise `order.photo.window_closed`
- **Given** Cleaner A on a `Confirmed` job, then the same job `InProgress`, then `Completed`. **When** A uploads an after photo in each state. **Then** the first and the last answer `order.photo.window_closed`; the second succeeds.
- **DEV:** the last call is made after E2E-EXEC-036.

### E2E-EXEC-016 — A finished job keeps its photos
- **Priority:** P1
- **Role:** Cleaner
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /flows/execution-and-completion#photo-windows — `DeleteOrderPhoto` refuses once the order is `Completed` or `Cancelled`
- **Given** a photo taken during the job. **When** A deletes it during the job and tries to delete the after photo of a `Completed` job; and the same on a cancelled job. **Then** the first delete succeeds; the others answer `order.photo.locked` and the photo stays.
- **DEV:** runs after E2E-EXEC-036.

### E2E-EXEC-017 — Photos of a job are for its crew only
- **Priority:** P2
- **Role:** Cleaner (two accounts)
- **Surfaces:** API
- **Rule:** → /flows/execution-and-completion#edge-cases — photos requested by a non-assignee are refused by the strict access gate; photographs of a customer's home are not browsable
- **Given** photos on Cleaner A's job. **When** Cleaner B (not on the crew) calls `GetPhotos`, `UploadPhoto` and `DeletePhoto` for it. **Then** the calls answer `order.employee_not_assigned` (or a not-found for the delete), and the browsing detail of the order holds no photo.
- **DEV:** unsure of the exact read refusal key; the case records it.

### E2E-EXEC-018 — Photo input is validated
- **Priority:** P2
- **Role:** Cleaner
- **Surfaces:** API
- **Rule:** → /flows/execution-and-completion#photo-windows — the server decides what photo may be added
- **Given** an `InProgress` job. **When** A saves 31 photos in one `SavePhotos` call, uploads a photo above the size limit, a file that is not an image and an empty file. **Then** the answers are `file.count_exceeded`, `file.size_exceeded`, `file.invalid_file_type` and `file.required`; nothing is stored.
- **DEV:** the size limit value is unsure from the docs; take it from the response of the first oversize file (the 31-photo cap comes from the code).

### E2E-EXEC-019 — Admin and the customer's view of photos
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** Web · API
- **Rule:** → /admin-app/order-management#photo-management — administrators read a job's photos
- **Given** a job with before and after photos. **When** Admin opens the order detail photo section (`GET api/AdminOrder/photos/{orderId}`). **Then** all photos are listed with their type. A Customer has no photo section on the customer apps (unsure; none found).
- **DEV:** read-only; the customer half is a negative check.

## Access instructions and the customer's details

### E2E-EXEC-020 — The crew reads the customer's details while the job is live
- **Priority:** P1
- **Role:** Cleaner
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /flows/execution-and-completion#crew-access — an assigned cleaner reads the customer's name, phone, address and door instructions while the job is live
- **Given** a job whose customer entered entry instructions ("key under the mat"). **When** Cleaner A (on the crew) reads the job detail, and Cleaner B (not on it) reads the same order. **Then** A sees the customer's name, phone, address and the instructions; B sees none of them (E2E-TAKE-004).
- **DEV:** use fake test contact data.

### E2E-EXEC-021 — The first read of the entry instructions is recorded once
- **Priority:** P2
- **Role:** Cleaner, Admin
- **Surfaces:** API · Web
- **Rule:** → /flows/execution-and-completion#entry-instructions — the first detail response serving non-empty instructions records `employee.order.access_instructions_read`, naming the cleaner and order, never the text
- **Given** a job with non-empty instructions and Cleaner A who never read it. **When** A reads the detail twice, then the crew-mate reads it (needs a two-cleaner job). **Then** Admin's audit holds one `employee.order.access_instructions_read` for A and one for the crew-mate, none duplicated, and no row holds the instruction text.
- **DEV:** the audit-log page for the cleaner trail is unsure; look in the order timeline and the audit log of the admin app.

### E2E-EXEC-022 — Admin reads the instructions only through an audited reveal
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /flows/execution-and-completion#entry-instructions — the instructions are withheld from an administrator read and come only from `POST /AdminOrder/{orderId}/access-instructions/reveal`
- **Given** a job with instructions. **When** Admin opens the order detail, then uses the reveal. **Then** the detail holds `hasAccessInstructions` and no text; the reveal returns the text and writes an audit row naming who asked and when.
- **DEV:** an admin role below Support gets a 403 on the reveal (`SupportOrAbove`).

### E2E-EXEC-023 — Crew-mates see each other
- **Priority:** P2
- **Role:** Cleaner (two accounts)
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /flows/execution-and-completion#crew-access — an assigned cleaner's detail holds every seat's name, phone and done stamp; the mobile apps show the given name and offer Call only while `InProgress` and the mate has not marked their part
- **Given** a two-cleaner job with A and B, `InProgress`. **When** A opens the detail on the web, then on iOS or Android, then B marks done and A reloads. **Then** the web prints names, phones and stamps; the apps show given names and the done stamp, with Call only for B before B marks; after B's mark Call is gone; a browsing cleaner sees the given name only.
- **DEV:** the number is never drawn on the apps; the dialer opens with it, and the simulator has no dialer (unsure).

### E2E-EXEC-024 — The details close 24 hours after completion
- **Priority:** P2
- **Role:** Cleaner
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /flows/execution-and-completion#crew-access — the crew reads the customer's details for 24 hours after completion; then the past-job shape, own photos only, and the receipt answers `order.not_found`
- **Given** a job Completed more than 24 hours ago with A on it. **When** A opens the detail, the photos and the receipt (`DownloadReceipt`). **Then** the detail holds the past-job shape (order number, date, services, pay, completion notes, own notes and contract acceptance), no customer name, phone, address or instructions; the photos are only those A took; the receipt answers `order.not_found`. Within the first 24 hours all of it is still readable.
- **DEV:** the second half needs a day of waiting; run the first 24 hours the same evening and the rest the next day.

### E2E-EXEC-025 — A cancelled job hides the customer at once
- **Priority:** P2
- **Role:** Cleaner, Customer
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /flows/execution-and-completion#crew-access — the details are not readable at all once the order is cancelled
- **Given** A is on a `Confirmed` job and the Customer cancels it. **When** A reloads the detail. **Then** the customer's details are gone, the apps say they were removed, and the instructions are no longer served.
- **DEV:** the cancellation fee depends on timing (CANCEL flow); cancel as early as the policy allows.

## Cash at the door

### E2E-EXEC-026 — The cleaner records the cash
- **Priority:** P1
- **Role:** Cleaner (Admin, Customer observe)
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /flows/execution-and-completion#edge-cases — `MarkCashCollected` only while `InProgress`, by an approved cleaner on the crew: the order becomes `Paid` and the server stamps the amount due
- **Given** a cash job `InProgress` with Cleaner A. **When** A taps Mark cash collected. **Then** the order stays `InProgress` with payment `Paid`; `CashCollectedAmount` equals the total less any applied credit; the collector and the time are stamped; Admin's order detail shows who collected how much; the Customer has no receipt yet; Cleaner A's cash held rises (`GetCashHeld`).
- **DEV:** the amount is the stored total, not what the cleaner types.

### E2E-EXEC-027 — Cash collection refusals
- **Priority:** P1
- **Role:** Cleaner (two accounts)
- **Surfaces:** API
- **Rule:** → /flows/execution-and-completion#edge-cases — cash only while `InProgress`, by the approved assigned cleaner, not twice, not on a payment that is not outstanding
- **Given** cash jobs in `Confirmed`, `InProgress` and already `Paid`, and Cleaner B off the crew. **When** the callers send `MarkCashCollected`. **Then** the `Confirmed` job answers `order.not_in_progress`; the `Paid` job answers `order.cash_already_collected`; Cleaner B answers `order.employee_not_assigned`; an unapproved cleaner on a job (placed before rejection) answers `employee.not_approved`; a refunded or disputed order answers `order.payment_not_outstanding`.
- **DEV:** the last needs a refunded card order in progress (CANCEL flow fixture).

### E2E-EXEC-028 — Cash is refused on a credit-settled or an ineligible card order
- **Priority:** P2
- **Role:** Cleaner
- **Surfaces:** API
- **Rule:** → /flows/execution-and-completion#edge-cases — a cash record on a card order is reconciled with Stripe, refused when the booking could not have been paid in cash
- **Given** an `InProgress` cash order with applied credit, and an `InProgress` card order with payment `Pending` for a two-cleaner crew. **When** A sends `MarkCashCollected` for each. **Then** the credit order answers `credit.cash_not_collectable_on_credit_order`; the crew order answers `order.cash_not_allowed_on_card_order`.
- **DEV:** the card order is made `InProgress` by Admin's override; skipped (unsure) if the fixtures cannot be made. A card order with a usable saved card and a settled Stripe payment answers `order.card_payment_already_settled`.

### E2E-EXEC-029 — The customer paid nothing: the cleaner reports it
- **Priority:** P1
- **Role:** Cleaner (Customer, Admin observe)
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /flows/execution-and-completion#cash-not-paid — the order completes with the payment pending, an `UnpaidCash` receivable opens for the price less credit, the crew is paid in full
- **Given** a cash job `InProgress` for a signed-in customer, an after photo taken, payment `Pending`, and the contract accepted. **When** A taps Customer did not pay and confirms. **Then** the response holds the order id, the status `Completed` and the amount owed; payment stays `Pending`; Admin sees an open `UnpaidCash` receivable of `TotalPrice` less credit; the Customer gets `order.cash_not_paid` (push and e-mail with the pay link); Admin's feed holds `admin.order.cash_not_paid`; no receipt, no loyalty points, no cash-ledger entry; A's pay row is requested in full.
- **DEV:** the e-mail is unreadable; the order page carries the pay link. The apps offer the action only after the after photos are in.

### E2E-EXEC-030 — Customer did not pay: refusals in order
- **Priority:** P1
- **Role:** Cleaner (two accounts)
- **Surfaces:** API
- **Rule:** → /flows/execution-and-completion#cash-not-paid — one validator chain runs the completion's gates first, then the order's
- **Given** cash jobs in each state. **When** callers send `ReportCashNotPaid`. **Then** a cleaner off the crew gets `order.not_found`; an unapproved one `employee.not_approved`; a seat without the contract `contract.acceptance_required`; a `Confirmed` job `order.not_in_progress`; a card order `order.cash_not_allowed_on_card_order`; a job with cash already collected `order.cash_already_collected`; a job with no after photo `order.after_photos.required`.
- **DEV:** the incomplete-profile refusal `employee.profile_incomplete` sits between the crew gate and the contract gate (from the code).

### E2E-EXEC-031 — A payment that is not outstanding cannot be reported unpaid
- **Priority:** P2
- **Role:** Cleaner
- **Surfaces:** API
- **Rule:** → /flows/execution-and-completion#cash-not-paid — payment `Pending` on a signed-in customer's booking, otherwise `order.payment_not_outstanding`, which also answers a legacy guest cash order
- **Given** an `InProgress` cash job whose payment is not `Pending` (a partial refund recorded by Admin or a disputed state) and, if one exists, a legacy guest cash order. **When** A reports it unpaid. **Then** the call answers `order.payment_not_outstanding` and nothing changes.
- **DEV:** a guest cash order cannot be made any more; only the first half runs (unsure).

### E2E-EXEC-032 — A customer who owes books nothing
- **Priority:** P1
- **Role:** Customer
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /flows/execution-and-completion#cash-not-paid — every new booking, cash or card, in any company, is refused `order.unpaid_receivable` until the debt is paid or written off
- **Given** the Customer of E2E-EXEC-029. **When** the Customer books a new order (cash or card) and opens the debt's pay link. **Then** the booking answers `order.unpaid_receivable` and the customer apps show the owed amount with the pay action.
- **DEV:** use the same account; E2E-EXEC-033 and -034 lift the ban.

### E2E-EXEC-033 — Admin undoes a wrong report
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** Web · API
- **Rule:** → /admin-app/order-management#record-cash-received — Record cash received on a reported order marks it `Paid`, issues the sale receipt and writes the debt off as "Paid in cash"
- **Given** the reported order of E2E-EXEC-029. **When** Admin records the cash (`POST api/AdminOrder/record-cash` with the assigned cleaner, a time after the clean began, the amount). **Then** the order is `Paid`, the receipt is issued, the debt is written off with the note "Paid in cash", and the Customer can book again. A write-off by a Manager does the same for the ban without the receipt.
- **DEV:** the write-off path needs the receivables page (Manager and above).

### E2E-EXEC-034 — Record-cash refusals
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /admin-app/order-management#record-cash-received — not in the future, not before the clean could begin, above zero in whole cents, only a cash order in progress or completed whose money is owed
- **Given** cash orders. **When** Admin records cash with a time in the future, a time before the clean, an amount of 0 and of 10.005, a cleaner not on the crew, on a cancelled order, on an `InProgress` order already `Paid` and after the debt was paid online. **Then** the answers are `order.cash_received_at_in_future`, `order.cash_received_at_before_clean`, `order.cash_amount_invalid` (twice), `order.employee_not_assigned`, `order.already_cancelled`, `order.cash_already_collected` and `order.payment_not_outstanding`.
- **DEV:** a card order answers `order.cash_not_allowed_on_card_order`; the key for the debt paid online follows the docs.

### E2E-EXEC-035 — The customer pays the debt online
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** Web · iOS · Android
- **Rule:** → /flows/execution-and-completion#cash-not-paid — paid online, the debt earns a fee receipt labelled "Unpaid cash payment" and asks for no crew pay
- **Given** the open debt of E2E-EXEC-029. **When** the Customer pays the link with the 4242 card. **Then** the debt closes, a receipt labelled "Unpaid cash payment" is available, no second pay row appears for the crew, and the booking ban lifts.
- **DEV:** Stripe test mode; the receipt text is read from the PDF.

## Completion

### E2E-EXEC-036 — Complete a cash job
- **Priority:** P1
- **Role:** Cleaner (Customer, Admin observe)
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /flows/execution-and-completion#the-path — complete moves the order to `Completed`, with a push and, for cash, the first receipt
- **Given** a cash job `InProgress` with an after photo and the cash recorded (E2E-EXEC-026). **When** A completes it (optional notes). **Then** the order is `Completed` with payment `Paid`; the Customer gets `order.completed` and the receipt (downloadable from My orders); Admin sees the receipt and a pay row requested for A; the elapsed time is recorded; the Live Activity ends on iOS.
- **DEV:** the pay row is read by the PAYOUT cases; the receipt is a PDF.

### E2E-EXEC-037 — Complete a card job
- **Priority:** P1
- **Role:** Cleaner (Customer, Admin observe)
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /flows/execution-and-completion#the-path — a card job completes after the after photo with no cash step
- **Given** a paid card job `InProgress` with an after photo (E2E-TAKE-016). **When** A completes it. **Then** the order is `Completed`, payment stays `Paid`, the Customer gets the push and the receipt, A's pay is requested.
- **DEV:** reuses the card order of E2E-TAKE-016.

### E2E-EXEC-038 — Complete is refused without the after photo or the right state
- **Priority:** P1
- **Role:** Cleaner (two accounts)
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /flows/execution-and-completion#only-the-assigned-cleaner-may-move-the-job — `CompleteOrder` gates on the assignment first, then the state, then the photo
- **Given** jobs in each state. **When** callers send `CompleteOrder`. **Then** a cleaner off the crew gets `order.employee_not_assigned` (an unknown order id gets `order.not_found`); a seat without the contract gets `contract.acceptance_required`; a `Confirmed` job `order.not_in_progress`; an `InProgress` job without an after photo `order.after_photos.required` (the web warns before the call); a second Complete on a finished job `order.not_in_progress`.
- **DEV:** the gate order follows the validator chain in the code (assignment, contract, profile, approval, state, photo); the docs list the contract rule right after the assignment rule.

### E2E-EXEC-039 — Complete is refused until the payment is settled
- **Priority:** P1
- **Role:** Cleaner
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /flows/execution-and-completion#edge-cases — a card payment `Pending` or `Failed` is refused `order.payment_not_confirmed`; cash must be recorded first
- **Given** an `InProgress` cash job with no cash recorded and an after photo, and an `InProgress` card job whose payment is `Pending` (made by Admin's override). **When** A completes each. **Then** the cash job answers `order.cash_not_collected` (the app tells A to collect and mark it, or to report the customer unpaid); the card job answers `order.payment_not_confirmed`.
- **DEV:** the second fixture is made by Admin's override and may be skipped (unsure).

### E2E-EXEC-040 — Completion notes have a length limit
- **Priority:** P2
- **Role:** Cleaner
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /flows/execution-and-completion#the-path — complete takes notes and photos
- **Given** an `InProgress` job ready to complete. **When** A completes it with notes above the limit. **Then** the call answers `order.completion_notes.too_long` and the job stays `InProgress`.
- **DEV:** the limit is not in the docs; take it from the validator by trying 5000 characters (unsure).

### E2E-EXEC-041 — A card order refunded before the end can still be completed
- **Priority:** P2
- **Role:** Cleaner, Admin
- **Surfaces:** API · Web
- **Rule:** → /flows/execution-and-completion#edge-cases — a card payment `Paid`, `PartiallyRefunded` or `Refunded` completes; `Pending` and `Failed` do not
- **Given** an `InProgress` card job Admin refunded in part, with an after photo. **When** A completes it. **Then** the order is `Completed`; the crew's pay is requested; the loyalty points take the share of what was returned.
- **DEV:** uses Admin's refund (CANCEL flow); the 4000 0000 0000 5126 card cannot be refunded, so use 4242.

### E2E-EXEC-042 — A crew marks done, then any cleaner completes
- **Priority:** P1
- **Role:** Cleaner (two accounts)
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /flows/execution-and-completion#each-cleaner-marks-done-before-the-crew-completes — each seat is marked, the others are pushed `order.crew_mate_done`, and when all are marked any cleaner completes
- **Given** a two-cleaner card job `InProgress` with A and B and an after photo. **When** A marks I'm done (`MarkMyPartDone`), twice; B marks; then either completes. **Then** A's stamp is the first one on the second call; B gets `order.crew_mate_done` once; the mobile slide says "I'm done" then "Slide to complete"; after B's mark the job completes by A or B; the receipt is issued once and both seats get a pay row.
- **DEV:** needs a two-cleaner job (see the intro).

### E2E-EXEC-043 — A premature Complete is refused
- **Priority:** P1
- **Role:** Cleaner (two accounts)
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /flows/execution-and-completion#each-cleaner-marks-done-before-the-crew-completes — a Complete before every seat is marked answers `order.crew_not_done`, even if nobody marked
- **Given** the job of E2E-EXEC-042 with only A marked, then with nobody marked. **When** A completes. **Then** both calls answer `order.crew_not_done` and the job stays `InProgress`.
- **DEV:** the crew list-row Complete on the web opens the detail instead.

### E2E-EXEC-044 — A short crew completes as a solo job
- **Priority:** P2
- **Role:** Cleaner
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /product/business-rules#crew-completion — the gate counts actual assignments rather than the booked crew requirement
- **Given** a two-cleaner job `InProgress` with only A on it. **When** A completes. **Then** the ordinary Complete works with no mark needed.
- **DEV:** needs a two-cleaner job; the second seat is left unfilled.

### E2E-EXEC-045 — Marking done has its own refusals
- **Priority:** P2
- **Role:** Cleaner
- **Surfaces:** API
- **Rule:** → /flows/execution-and-completion#each-cleaner-marks-done-before-the-crew-completes — `MarkMyPartDone` stamps the caller's seat while the job is `InProgress`
- **Given** a job `Confirmed` and one `InProgress`, Cleaner B off the crew. **When** A marks the `Confirmed` job, B marks the `InProgress` job, and anyone marks an unknown id. **Then** the answers are `order.not_in_progress`, `order.employee_not_assigned` and `order.not_found`.
- **DEV:** whether a lone assignee may mark is unsure from the docs; the case does not test it.

### E2E-EXEC-046 — A fully marked job completes itself two hours later
- **Priority:** P2
- **Role:** Cleaner (Customer observes)
- **Surfaces:** API
- **Rule:** → /domain/order-lifecycle#crew-completion — a fully marked job is completed automatically two hours after the last mark on a fifteen-minute sweep, keeping the after-photo and payment checks
- **Given** a two-cleaner job with an after photo and settled payment where both marked and nobody completed. **When** 2 hours and one sweep pass. **Then** the order is `Completed` with the receipt and the pay rows once; with no after photo or unsettled payment it stays `InProgress`.
- **DEV:** a 2 h 15 min wait.

### E2E-EXEC-047 — Seat times show to the crew and admin, not the customer
- **Priority:** P2
- **Role:** Cleaner, Admin, Customer
- **Surfaces:** Web · iOS · Android
- **Rule:** → /product/business-rules#crew-completion — partner web and admin show the per-seat times; customers see no new completion timestamps
- **Given** the job of E2E-EXEC-042. **When** A, Admin and the Customer open the order. **Then** A sees Done with the time or Not yet per seat (web) or by given name (Android, iOS); Admin sees the seat times; the Customer sees none.
- **DEV:** read-only.

### E2E-EXEC-048 — Another cleaner's mark appears only after a reload
- **Priority:** P2
- **Role:** Cleaner
- **Surfaces:** Web · iOS · Android
- **Rule:** → /flows/execution-and-completion#each-cleaner-marks-done-before-the-crew-completes — a foreground push alone does not refresh an already-open detail
- **Given** A has the detail open and B marks done. **When** A waits, then reloads (web reload, Android pull-to-refresh, iOS reopen). **Then** the mark and the shared Complete show only after the reload; a Complete tapped before it is refused `order.crew_not_done` and is not a defect.
- **DEV:** by design; the triager closes a finding about this as "by design".

## The order lifecycle: two axes

### E2E-EXEC-049 — A cash job walks both axes
- **Priority:** P1
- **Role:** Admin (reads), Cleaner, Customer
- **Surfaces:** API · Web
- **Rule:** → /domain/order-lifecycle#why-one-axis-is-not-enough — paying does not move the fulfilment axis and accepting does not move the money axis
- **Given** a one-cleaner cash order. **When** the order is created, taken, set off for, started, cash-recorded and completed, and after each step Admin reads the order. **Then** the (status, payment) pairs are `New/Pending`, `Confirmed/Pending`, `OnTheWay/Pending`, `InProgress/Pending`, `InProgress/Paid` (with the collector and amount) and `Completed/Paid`; the type stays `Cash` throughout.
- **DEV:** one pass of the whole journey; combine with E2E-EXEC-001, -002, -026 and -036.

### E2E-EXEC-050 — A card job walks both axes
- **Priority:** P1
- **Role:** Admin (reads), Cleaner, Customer
- **Surfaces:** API · Web
- **Rule:** → /domain/order-lifecycle#why-one-axis-is-not-enough — a card order reaches `Paid` with nobody assigned and rests at `New`
- **Given** a one-cleaner card order. **When** the checkout is opened, paid, taken, set off for, started and completed. **Then** the pairs are `New/Pending`, `New/Paid` (nobody assigned, on the board), `Confirmed/Paid`, `OnTheWay/Paid`, `InProgress/Paid` and `Completed/Paid`; the type stays `Card`; the status never reaches `Confirmed` from the payment.
- **DEV:** Stripe test mode; the webhook writes `Paid`.

### E2E-EXEC-051 — Pending is dead
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /domain/order-lifecycle#pending-1-is-dead-and-stays — nothing writes `OrderStatus.Pending`; the override refuses it as a target
- **Given** the orders of this run. **When** Admin lists orders by status and tries to override an order to `Pending`. **Then** no order holds `CurrentStatus` `Pending`; the override answers `order.invalid_status_transition`.
- **DEV:** legacy DEV rows could hold it (unsure); count them and report rather than fail.

### E2E-EXEC-052 — The override moves strictly forward
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** Web · API
- **Rule:** → /admin-app/order-management#order-status-override — same-state, backward and off-lifecycle targets are `order.invalid_status_transition`; a terminal state is `order.already_completed` or `order.already_cancelled`
- **Given** a `Confirmed` order with a cleaner, a `Completed` order and a `Cancelled` one. **When** Admin overrides to `Confirmed`, to `New`, to `Cancelled`, and the terminal orders to anything. **Then** the answers are `order.invalid_status_transition` (three times), `order.already_completed` and `order.already_cancelled`; a legal forward move to `OnTheWay` succeeds and is audited (`order.status.override`).
- **DEV:** the override pushes a state-card update to a Live Activity if one exists.

### E2E-EXEC-053 — Confirmed needs a crew
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** Web · API
- **Rule:** → /domain/order-lifecycle#currentstatus-is-a-denormalisation-with-one-writer — `Confirmed` as a target on an order with nobody assigned is refused (`order.status.confirmed_needs_crew`)
- **Given** an unstaffed `New` order. **When** Admin overrides to `Confirmed`, then to `OnTheWay`. **Then** the first answers `order.status.confirmed_needs_crew`; the second succeeds (the "cleaner is there but never tapped" repair).
- **DEV:** the OnTheWay order is cancelled afterwards, as no sweep cancels it.

### E2E-EXEC-054 — Force-completing without an after photo needs a reason and dates the order
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** Web · API
- **Rule:** → /admin-app/order-management#order-status-override — `Completed` with no after photo needs a written reason; an override to `Completed` stamps `CompletedAt`
- **Given** an `InProgress` order with no after photo. **When** Admin overrides to `Completed` without a reason, with a 501-character reason, then with a short reason. **Then** the answers are `order.status.force_complete_reason_required`, `common.max_length` and success; the order has a completion time that falls in the right month of the revenue report; the reason is on the audit row.
- **DEV:** the revenue report is read on the admin app.

### E2E-EXEC-055 — Force-completing an unpaid cash job in progress opens the door debt
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** Web · API
- **Rule:** → /admin-app/order-management#order-status-override — a signed-in customer's cash order in progress with payment `Pending` opens the `UnpaidCash` debt, asks the crew's pay and raises no administrator alert
- **Given** a cash job `InProgress` (payment `Pending`) and another still `Confirmed`. **When** Admin overrides each to `Completed`. **Then** the first opens the receivable, sends the customer `order.cash_not_paid`, asks A's pay, issues no receipt and raises no `admin.order.cash_not_paid`; the second opens no debt and asks no pay.
- **DEV:** the second order is then fixed by Record cash received or a cancel.

### E2E-EXEC-056 — The only backward edge is a release
- **Priority:** P2
- **Role:** Admin, Cleaner
- **Surfaces:** API · Web
- **Rule:** → /domain/order-lifecycle#confirmed-means-one-thing-a-cleaner-took-the-job — the last cleaner leaving a `Confirmed` order is the only writer that walks it back to `New`, and the override cannot fake the forward one
- **Given** a `Confirmed` order with one cleaner. **When** Admin tries to override to `New`, then the cleaner drops (E2E-TAKE-040). **Then** the override answers `order.invalid_status_transition`; the drop walks the order to `New`; the history shows `New`, `Confirmed`, `New` in sequence.
- **DEV:** the history is read from the order timeline.

### E2E-EXEC-057 — Override and cash recording need Support or above
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** API
- **Rule:** → /admin-app/order-management#order-status-override — the override is an administrator's audited act
- **Given** a Cleaner token, a Customer token and an Accountant-role admin token. **When** each calls `POST api/AdminOrder/override-status` and `POST api/AdminOrder/record-cash`. **Then** every call is refused (403 or 401) and no order changes.
- **DEV:** DEV may have no Accountant account (unsure); run the other two.

## Not testable on DEV

- **A start window below the clock.** The 60-minute window and the late start need real time: nothing here moves the DEV clock, so EXEC-003, -004 and -011 wait for it.
- **Live Activity and push on a simulator or emulator.** DEV pushes and the iOS Live Activity reach a physical device only; the cases check the order status and the inbox instead.
- **E-mail and receipts by mail.** `@example.com` never receives mail; receipts are read from the app download.
- **The 24-hour rule on the same day.** EXEC-024 needs a day of waiting after the completion; it is a next-day check.
- **A legacy guest cash order.** EXEC-031's second half needs a pre-existing row; the booking path no longer creates one.
- **The partial payment case.** The docs say an administrator records what was received; there is no flow to run.
- **Failed refunds after a cash job.** The 4000 0000 0000 5126 card pays and then fails any refund; it belongs to the CANCEL cases.
- **Load, Apple and Google sign-in.** Not covered.
