# Offerability and take: cases

How a job reaches a cleaner's board and what happens when cleaners take, drop, cover or are placed on it. Cleaner A and Cleaner B are two approved cleaners with a complete profile who accepted the cleaner documents; a "one-cleaner job" needs at most 120 booked minutes, a "two-cleaner job" more than that (on DEV services seed at 2 minutes, so a two-cleaner job needs an admin to raise a service duration in the catalogue first, and to restore it after).

## The open board: who sees which order

### E2E-TAKE-001 — A paid card order rests at New and is on the open board
- **Priority:** P1
- **Role:** Cleaner (Customer books)
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /domain/offerability#why-a-status-list-cannot-express-it — a one-off card order with payment `Paid` is offerable before slot end, and its status stays `New` until a cleaner takes it
- **Given** a Customer paid a one-off card order (4242 card) for a slot at least 2 h ahead in Cleaner A's market and currency. **When** Cleaner A opens the available orders. **Then** the order is listed with status `New`, payment axis not shown to the cleaner, and Cleaner A can open its preview; the same list through the partner API and the partner mobile API holds the same order.
- **DEV:** books one card order; the earliest slot is 2 h after "now" and between 08:00 and 19:45 Prague.

### E2E-TAKE-002 — A one-off cash order is on the board from creation
- **Priority:** P1
- **Role:** Cleaner (Customer books)
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /domain/offerability#why-a-status-list-cannot-express-it — a one-off cash order is offerable from creation until slot end, because nothing scheduled can retract it
- **Given** a signed-in Customer with a saved test card books a one-cleaner cash order. **When** Cleaner A refreshes the board right after the booking is created. **Then** the order is listed with its payment still pending on the customer's side, and a take of it succeeds (see E2E-TAKE-013).
- **DEV:** cash needs a signed-in customer, one cleaner and a usable saved card.

### E2E-TAKE-003 — An unpaid card order is not offered and not takeable
- **Priority:** P1
- **Role:** Cleaner
- **Surfaces:** API
- **Rule:** → /domain/offerability#the-take-is-gated-not-just-the-list — the take chain has offerability right after "not cancelled" and "not completed"
- **Given** a Customer started a one-off card checkout and did not pay (payment `Pending`). **When** Cleaner A lists the board, then sends `TakeOrder` for that order id with a valid contract text id. **Then** the order is not on the list, and the take answers `order.not_takeable`; the order never gains a seat. An hour later the stale sweep cancels the order (CANCEL flow).
- **DEV:** the checkout is left unpaid on purpose; do not wait for the sweep here.

### E2E-TAKE-004 — The board shows the job, not the household
- **Priority:** P1
- **Role:** Cleaner
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /flows/execution-and-completion#what-a-cleaner-who-has-not-taken-the-job-can-see — a browsing cleaner gets no customer name, e-mail, phone, address, coordinates, notes, special or entry instructions, review or crew phone numbers
- **Given** an open order whose customer wrote a note, special instructions and entry instructions. **When** Cleaner A (not on the crew) reads the board row and then the order detail. **Then** none of those fields, and none of the customer's name, e-mail, phone, street or coordinates, appear; the services, rooms, window, reward and the coarse location do.
- **DEV:** the same order is read by Cleaner A before the take and by Admin for contrast.

### E2E-TAKE-005 — The coarse location is the district or the city, never the postcode
- **Priority:** P2
- **Role:** Cleaner
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /product/business-rules#approximate-location — the label is Praha N for a Prague address and otherwise the city alone, and the list carries a distance band instead of a distance
- **Given** one open order at a Prague address (postcode 1xx xx) and one at a Brno address. **When** Cleaner A reads both rows. **Then** the Prague row says "Praha N" for the district and the Brno row says "Brno" alone, with no part of a postcode; the list also carries a `homeDistanceBand` for a cleaner with a stored home, and no exact distance or coordinates.
- **DEV:** needs a Brno test address in a serviced market; if DEV has none, run the Prague half only and say so.

### E2E-TAKE-006 — The confirmation code is on no DTO
- **Priority:** P2
- **Role:** Cleaner
- **Surfaces:** API
- **Rule:** → /flows/execution-and-completion#what-a-cleaner-who-has-not-taken-the-job-can-see — the confirmation code left `OrderItem` entirely and authenticates nothing
- **Given** a Customer's booking with Cleaner A on its crew. **When** Cleaner A reads the order detail and the list over both partner hosts. **Then** neither response carries a confirmation code field, and a guest lookup by order number plus e-mail plus code is not offered anywhere.
- **DEV:** read-only.

### E2E-TAKE-007 — A seat stops being offered at slot end, not before
- **Priority:** P2
- **Role:** Cleaner
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /domain/offerability#the-rule — a seat stops being offered and takeable at its cleaning start plus the raw booked minutes
- **Given** an unstaffed one-cleaner order whose slot has started but not ended. **When** Cleaner A lists the board before slot end and again after it, then sends a take from the stale list. **Then** the order is on the board before slot end and a take then succeeds; after slot end it is gone from the board and the stale take answers `order.not_takeable`.
- **DEV:** book the earliest slot (2 h ahead) with a 2-minute service and wait past start plus the booked minutes; keep the order away from Cleaner A until the check (the unfilled sweep cancels at start + 30 min, so test before that).

### E2E-TAKE-008 — A cleaner is shown only orders in their own currency
- **Priority:** P2
- **Role:** Cleaner
- **Surfaces:** API
- **Rule:** → /domain/offerability#cleaner-currency — an order in another currency is not on the board, and the take answers `order.not_found` exactly as for a held order
- **Given** an open order in a currency other than Cleaner A's work-country currency. **When** Cleaner A lists the board and sends a take for that order id. **Then** the order is not on the list and the take answers `order.not_found`.
- **DEV:** needs a second serviced market and a cleaner in the first; if DEV has only one market, this case cannot run (unsure) and is reported as blocked.

### E2E-TAKE-009 — A cleaner over the float cap sees no cash jobs
- **Priority:** P2
- **Role:** Cleaner (Admin sets the cap)
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /domain/offerability#cash-float-cap — a cleaner holding strictly more company cash than `cash.float_cap` is not offered cash jobs; the take answers `order.cash_float_cap_exceeded`
- **Given** the company's float cap is set below the cash Cleaner A holds (one collected cash job), and one open cash order and one open card order exist. **When** Cleaner A reads the board, the dashboard count and `GetCashHeld`, then takes the cash order by id. **Then** the cash order is off the board and the count, the card order stays, `GetCashHeld` reports `cashJobsHidden` for the currency, and the take answers `order.cash_float_cap_exceeded`; after Admin records a remittance bringing the cash to the cap or below, the cash order is on the board again on the next read.
- **DEV:** changes a company setting and writes a remittance; restore the setting after. Needs cash held by Cleaner A from an earlier collection (EXEC flow).

## The contract for work and the take

### E2E-TAKE-010 — The contract preview shows the text and the facts that will be frozen
- **Priority:** P1
- **Role:** Cleaner
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /flows/offerability-and-take#the-take-carries-the-acceptance — the preview returns the text in the cleaner's language rendered with the order's currency plus the job facts (number, window, price, coarse location, rooms, bathrooms, services, packages, extras)
- **Given** an open order Cleaner A may take. **When** Cleaner A calls `GetWorkContractPreview` (the app opens it before the take). **Then** the response holds the id of the exact text row, the text in Cleaner A's language, and the job facts; the price is the reward for the seat, not the customer's price; no customer personal data is in it.
- **DEV:** read-only.

### E2E-TAKE-011 — A preview of a missing, held, gone or full order discloses nothing
- **Priority:** P2
- **Role:** Cleaner
- **Surfaces:** API
- **Rule:** → /flows/offerability-and-take#the-take-carries-the-acceptance — a held order is a missing order to everyone but its beneficiary; a cancelled, finished, full or unpaid-card job the caller is not on answers `order.not_found`
- **Given** four orders Cleaner A is not on: one held for Cleaner B, one cancelled, one completed, one unpaid card. **When** Cleaner A calls `GetWorkContractPreview` for each and for a random id. **Then** every answer is `order.not_found` with the same body shape.
- **DEV:** needs the four orders; the held one needs a Plus customer (E2E-TAKE-035).

### E2E-TAKE-012 — The preview still opens for a cash job hidden by the float cap
- **Priority:** P2
- **Role:** Cleaner
- **Surfaces:** API
- **Rule:** → /domain/offerability#cash-float-cap — the order detail and the contract preview do not read the cap, so a cash job reached another way still opens and its take is refused
- **Given** E2E-TAKE-009's state (Cleaner A over the cap). **When** Cleaner A calls `GetWorkContractPreview` for the cash order. **Then** it answers the contract and facts, and the take then answers `order.cash_float_cap_exceeded`.
- **DEV:** runs inside the E2E-TAKE-009 state.

### E2E-TAKE-013 — Cleaner A takes a one-cleaner order: one seat, one acceptance
- **Priority:** P1
- **Role:** Cleaner
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /flows/offerability-and-take#the-take-carries-the-acceptance — the take echoes the text row id; the seat, the status row, the acceptance and the audit row are one transaction
- **Given** an open cash order (E2E-TAKE-002) and the preview of E2E-TAKE-010. **When** Cleaner A sends `TakeOrder { orderId, acceptedWorkContractTextId }` with the previewed id. **Then** the call succeeds; the order status is `Confirmed`; payment is unchanged (`Pending` for cash); Cleaner A is the only assignee on seat 0; the order is listed under Cleaner A's jobs and off the open board; the order detail shows "You accepted the contract for work on {date}, version {version}" with Read the contract.
- **DEV:** the order is reused by the EXEC flow.

### E2E-TAKE-014 — The customer sees the take
- **Priority:** P1
- **Role:** Customer
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /domain/order-lifecycle#confirmed-means-one-thing-a-cleaner-took-the-job — a take writes `Confirmed`, and a cleaner-found notice goes to the customer
- **Given** E2E-TAKE-013 just happened. **When** the Customer opens the order in My orders (web, iOS or Android) and the customer API. **Then** the order shows `Confirmed` in the progress; the customer has the `order.cleaner_assigned` ("Cleaner found!") inbox entry on iOS and Android; nothing names the cleaner's contact data beyond what the customer detail shows today.
- **DEV:** the e-mail "Cleaner found" goes to `@example.com` and cannot be read; the push needs a device and is checked as an inbox entry only.

### E2E-TAKE-015 — The administrator sees the seat and the accepted contract
- **Priority:** P1
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /product/business-rules#work-contract — the admin's order detail says "accepted {date}, v{version}" per crew member with Read, and an Administrator also sees the text row's SHA-256
- **Given** E2E-TAKE-013 just happened. **When** Admin opens the order detail and reads the work contract (`GetWorkContract`). **Then** the crew list shows Cleaner A with "accepted {date}, v{version}" (not "contract pending"), the status is `Confirmed`, and the order timeline holds `employee.order.contract_accepted` for Cleaner A.
- **DEV:** read-only.

### E2E-TAKE-016 — Taking a paid card order leaves the payment axis alone
- **Priority:** P1
- **Role:** Cleaner
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /domain/order-lifecycle#why-one-axis-is-not-enough — a card order a cleaner has taken is `Confirmed` with payment `Paid`
- **Given** the paid card order of E2E-TAKE-001. **When** Cleaner A takes it. **Then** `CurrentStatus` is `Confirmed` and `PaymentStatus` stays `Paid`; Admin's order detail shows both.
- **DEV:** reused by the EXEC card journey.

### E2E-TAKE-017 — A take without a contract text id is refused before anything else
- **Priority:** P1
- **Role:** Cleaner
- **Surfaces:** API
- **Rule:** → /flows/offerability-and-take#the-take-carries-the-acceptance — a blank id is refused `contract.not_accepted` before existence, so it leaks neither existence nor the hold
- **Given** an open order, a held order and a random id. **When** Cleaner A sends `TakeOrder` with a blank `acceptedWorkContractTextId` for each. **Then** all three answer `contract.not_accepted`; no seat is created on the open order.
- **DEV:** the app never sends a blank id, so this runs on the API only.

### E2E-TAKE-018 — A wrong text id is refused last
- **Priority:** P2
- **Role:** Cleaner
- **Surfaces:** API
- **Rule:** → /flows/offerability-and-take#edge-cases — an id that is not a text of this order's document is `contract.text_mismatch`, judged last, so a full order still says "no available spots"
- **Given** one open order and one full order, and a well-formed id that is not a text of any order document. **When** Cleaner A takes each with that id. **Then** the open order answers `contract.text_mismatch` and no seat is created; the full order answers `order.no_available_spots`.
- **DEV:** whether the id of the same document in another language counts as a match is unsure; the case uses an id that matches nothing.

### E2E-TAKE-019 — An order with no contract text answers legal.document_not_found
- **Priority:** P2
- **Role:** Cleaner
- **Surfaces:** API
- **Rule:** → /flows/offerability-and-take#the-take-carries-the-acceptance — an order with no text answers `legal.document_not_found` on the preview and `contract.text_mismatch` on the take
- **Given** an order with no contract text. **When** Cleaner A previews and takes it. **Then** the preview answers `legal.document_not_found` and the take `contract.text_mismatch`.
- **DEV:** the one production writer always stamps a text, so the booking path cannot make such an order; the case cannot run on DEV (see the closing section).

## Take refusals, in chain order

### E2E-TAKE-020 — Two cleaners, one seat, the same instant
- **Priority:** P1
- **Role:** Cleaner (two accounts)
- **Surfaces:** API
- **Rule:** → /flows/offerability-and-take#the-seat-is-decided-by-the-database — exactly one assignment survives and the loser sees "no available spots"
- **Given** one open one-cleaner order and valid previews for Cleaner A and Cleaner B. **When** both send `TakeOrder` at the same instant (two parallel requests). **Then** exactly one call succeeds and the other answers `order.no_available_spots`; the order has one assignee and one acceptance row; the loser's jobs list does not hold the order; the order is `Confirmed`.
- **DEV:** repeat three times with fresh orders, since a race can resolve before the second request starts; a run where the second request lands after the first committed still ends in the same state.

### E2E-TAKE-021 — A full order answers no available spots, a taken order leaves the board
- **Priority:** P1
- **Role:** Cleaner
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /domain/offerability#the-take-is-gated-not-just-the-list — a free seat is a gate of the take chain
- **Given** Cleaner A took a one-cleaner order (E2E-TAKE-013). **When** Cleaner B, who still had the order on a stale list, sends a take with a valid text id. **Then** the call answers `order.no_available_spots`, the app shows "Another cleaner has already taken this job.", and Cleaner B's board no longer lists the order after a refresh.
- **DEV:** reuses the E2E-TAKE-013 order.

### E2E-TAKE-022 — The same cleaner cannot take a seat twice
- **Priority:** P1
- **Role:** Cleaner
- **Surfaces:** API
- **Rule:** → /domain/offerability#the-take-is-gated-not-just-the-list — "not already assigned" is a gate of the take chain after the free-seat check
- **Given** a two-cleaner order with Cleaner A on seat 0 and seat 1 free. **When** Cleaner A sends `TakeOrder` again. **Then** the call answers `order.employee_already_assigned` and the crew is unchanged. On a one-cleaner order the same repeat answers `order.no_available_spots` instead, since the free-seat check comes first.
- **DEV:** needs a two-cleaner job (see the intro).

### E2E-TAKE-023 — A cancelled or completed order says the job is gone
- **Priority:** P1
- **Role:** Cleaner
- **Surfaces:** API
- **Rule:** → /flows/offerability-and-take#edge-cases — a cancelled order with a free seat reads "this job is gone", not "this job is full"
- **Given** one cancelled order with a free seat (the Customer cancelled it) and one completed order. **When** Cleaner B sends `TakeOrder` for each with a valid text id. **Then** the cancelled order answers `order.take.already_cancelled` and the completed one `order.take.already_completed`.
- **DEV:** a cancelled order is cancelled by the Customer before any cleaner took it.

### E2E-TAKE-024 — A cleaner who is not approved cannot take
- **Priority:** P1
- **Role:** Cleaner
- **Surfaces:** API · Web
- **Rule:** → /domain/offerability#the-take-is-gated-not-just-the-list — complete profile and `ContractStatus.Approved` precede the seat
- **Given** a newly registered Cleaner C with documents pending, and an open order. **When** Cleaner C sends `TakeOrder`. **Then** the call answers `employee.profile_incomplete` while the profile is incomplete, and `employee.not_approved` once the profile is complete but Admin has not approved the documents; no seat is created.
- **DEV:** Cleaner C comes from the AUTH onboarding cases, taken before Admin approves.

### E2E-TAKE-025 — A cleaner who has not accepted the cleaner documents cannot take
- **Priority:** P1
- **Role:** Cleaner
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /flows/offerability-and-take#cleaner-documents-gate — `TakeOrder`, `ApproveEmployee` and an administrator's placement refuse with `employee.legal_documents_not_accepted`
- **Given** an approved Cleaner D who has not accepted a cleaner document in force. **When** Cleaner D takes an open order. **Then** the call answers `employee.legal_documents_not_accepted`, the app leads to the documents, and after Cleaner D reads and accepts them (`GetMyLegalDocuments`, `AcceptLegalDocument`) the same take succeeds.
- **DEV:** relies on the documents in force on DEV; the framework agreement version of 2026-10-27 is accepted again by anyone who accepted an earlier one.

### E2E-TAKE-026 — A caller who is not a cleaner cannot take
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API
- **Rule:** → /domain/offerability#the-take-is-gated-not-just-the-list — "caller is an employee" is a gate of the take chain
- **Given** an open order. **When** a Customer token sends `TakeOrder` on the partner host, and an Admin token sends it too. **Then** the Customer is refused at the host (403 or 401); the Admin passes the policy and answers `employee.not_found`; no seat is created in either case.
- **DEV:** the Customer token on the partner host may be refused at authentication instead of the policy (unsure); either refusal is a pass.

### E2E-TAKE-027 — A weekly cap blocks the next take and carries its reason
- **Priority:** P2
- **Role:** Cleaner (Admin sets the cap)
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /product/business-rules#placement-is-an-offer — a weekly job limit carries a reason the cleaner sees on their profile
- **Given** Admin sets Cleaner A's weekly limit to 1 with a reason (`PUT /api/AdminEmployee/{employeeId}/weekly-order-limit`) and Cleaner A holds one job this week. **When** Cleaner A takes a second open order. **Then** the call answers `order.weekly_limit_reached`; Cleaner A's profile shows the cap and the reason; after Admin sends a null limit the take succeeds and the profile shows no cap.
- **DEV:** restore the cap to null after.

### E2E-TAKE-028 — Admin cannot set a bad cap
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /product/business-rules#placement-is-an-offer — setting a cap without a reason is refused, and the floor is 1
- **Given** Cleaner A with no cap. **When** Admin sets a limit of 0, then a limit of 2 with an empty reason, then a limit of 2 with a 501-character reason. **Then** the answers are `employee.weekly_limit_invalid`, `employee.weekly_limit_reason_required` and `common.max_length`; the cap stays null.
- **DEV:** the 501-character case: the 500-character limit is from the docs; the key is unsure, check the response.

### E2E-TAKE-029 — A time conflict blocks a take
- **Priority:** P1
- **Role:** Cleaner
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /domain/offerability#the-take-is-gated-not-just-the-list — "no time conflict" is the last gate before the take
- **Given** Cleaner A holds a job from 12:00 and two open orders: one for 12:00 and one for a slot starting after the first one's booked minutes end. **When** Cleaner A takes the overlapping order, then the later one. **Then** the first answers `order.time_conflict`; the second succeeds.
- **DEV:** book both slots at least 2 h ahead; the booked minutes are short with 2-minute services, so use slots a quarter hour apart for the non-overlapping case.

### E2E-TAKE-030 — The take checks run in the documented order
- **Priority:** P2
- **Role:** Cleaner
- **Surfaces:** API
- **Rule:** → /domain/offerability#the-take-is-gated-not-just-the-list — the chain is one ordered `Cascade.Stop`: a refusal ahead of another wins
- **Given** Cleaner A is over their weekly cap, over the float cap and in a time conflict with the target order. **When** Cleaner A takes it; Admin lifts the weekly cap; Cleaner A takes it again; the cash is remitted; Cleaner A takes it again. **Then** the three answers are `order.weekly_limit_reached`, `order.cash_float_cap_exceeded` and `order.time_conflict` in that order.
- **DEV:** combines E2E-TAKE-009, -027 and -029; skip if those could not run.

## Seats on multi-cleaner orders

### E2E-TAKE-031 — A two-cleaner job fills seat by seat, with no spare seat
- **Priority:** P1
- **Role:** Cleaner (two accounts), Admin
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /domain/order-lifecycle#seats — `RequiredEmployees = ceil(EstimatedTime / 120)` and there is no spare seat
- **Given** an open order booked for more than 120 and at most 240 minutes. **When** Cleaner A takes it, Cleaner B takes it, then Cleaner E (a third approved cleaner) tries. **Then** after A's take the order is `Confirmed` with one seat free and still on Cleaner B's board; after B's take the order is full and off every board; Cleaner E's take answers `order.no_available_spots`; Admin's order detail shows two crew rows and no third.
- **DEV:** needs a two-cleaner job (see the intro) and a third cleaner; without the third, check the full state from B's refresh.

### E2E-TAKE-032 — Two cleaners racing for a free seat of a two-cleaner job
- **Priority:** P2
- **Role:** Cleaner (two accounts)
- **Surfaces:** API
- **Rule:** → /flows/offerability-and-take#edge-cases — both derive the same ordinal from the same stale read, so the loser is refused while a seat is free and the next tap succeeds
- **Given** a two-cleaner order with both seats free. **When** Cleaner A and Cleaner B take at the same instant. **Then** at least one succeeds; if one is refused with `order.no_available_spots` while a seat is free, the immediate retry by that cleaner succeeds; the final crew is two distinct cleaners with two acceptance rows. A refusal here is a known accepted behaviour, not a defect.
- **DEV:** timing decides whether the refusal happens; run until both outcomes were seen or five runs passed.

### E2E-TAKE-033 — A released seat is reused, not skipped
- **Priority:** P2
- **Role:** Cleaner (two accounts)
- **Surfaces:** API
- **Rule:** → /flows/offerability-and-take#the-seat-is-decided-by-the-database — the ordinal is the smallest free one, so a seat released and retaken stays usable
- **Given** a two-cleaner order with A on one seat and B on the other. **When** A drops, then Cleaner E takes. **Then** the take succeeds, the order reads as full again, and the crew holds B and E.
- **DEV:** the ordinals themselves are not shown in the apps (unsure about the API); the case checks that the seat is usable.

### E2E-TAKE-034 — A take racing the drop of the last cleaner ends in one of two states
- **Priority:** P2
- **Role:** Cleaner (two accounts)
- **Surfaces:** API
- **Rule:** → /flows/offerability-and-take#edge-cases — a take already writing is counted and the drop leaves the job short; a take that read before the drop committed is refused `no_available_spots`
- **Given** a two-cleaner order with A alone on it (`Confirmed`). **When** A drops while B takes at the same instant. **Then** either B holds a seat and the job is `Confirmed`, or B was refused with `order.no_available_spots` and the job is `New`; never a `New` order with a cleaner on it. A next tap by B then succeeds.
- **DEV:** timing decides; run several times and record the state seen.

## The preferred-cleaner hold

### E2E-TAKE-035 — A held order is invisible to every cleaner but its beneficiary
- **Priority:** P2
- **Role:** Cleaner (two accounts), Customer
- **Surfaces:** API · iOS · Android
- **Rule:** → /domain/offerability#the-preferred-cleaner-hold — until `PreferredHoldUntilUtc` the first seat is offered to the preferred cleaner alone, and a held order is indistinguishable from a missing one
- **Given** a Plus Customer books a one-off order naming Cleaner A as the preferred cleaner (a cleaner who served the customer before). **When** Cleaner B lists the board, previews the order and takes it, and a random id is taken too. **Then** the order is not on Cleaner B's board; the preview and the take answer `order.not_found`, identical to the random id's answer; Cleaner A sees the order in pending offers (`MyPendingOffers`) and on the board, and it is not exposed on any partner DTO as a preferred id.
- **DEV:** needs a Plus trial customer and a past completed job with Cleaner A; the picker is on iOS and Android (web booking has a Plus roster too, unsure of its surfaces). The hold is 10 % of the lead time, capped at 12 h, so a 4 h lead holds for 24 minutes.

### E2E-TAKE-036 — The beneficiary takes a held order
- **Priority:** P2
- **Role:** Cleaner
- **Surfaces:** API · iOS · Android
- **Rule:** → /domain/offerability#the-preferred-cleaner-hold — the hold folds into the existence check, and `OrderVisibility.NotHeldFrom` opens it to the beneficiary
- **Given** the held order of E2E-TAKE-035. **When** Cleaner A previews and takes it. **Then** the take succeeds with a contract acceptance; the order is `Confirmed`.
- **DEV:** as E2E-TAKE-035.

### E2E-TAKE-037 — When the hold lapses the order opens to everyone at once
- **Priority:** P2
- **Role:** Cleaner (two accounts)
- **Surfaces:** API · iOS · Android
- **Rule:** → /flows/offerability-and-take#the-path — at `PreferredHoldUntilUtc` the order opens to everyone at once and a push goes out
- **Given** a held order nobody took. **When** the hold time passes and the sweep runs. **Then** Cleaner B sees and can preview the order on the next read; the push "open to all cleaners" is sent (checked as a notification on a device).
- **DEV:** wait the hold plus the sweep interval; the interval is unsure from the docs.

### E2E-TAKE-038 — The beneficiary declines and the order opens
- **Priority:** P2
- **Role:** Cleaner
- **Surfaces:** API · iOS · Android
- **Rule:** → /domain/offerability#the-preferred-cleaner-hold — a release ends the hold its beneficiary held
- **Given** a held order. **When** Cleaner A sends `DeclinePreferredOffer`, then sends it again. **Then** the first call succeeds and Cleaner B sees the order at once; the second call also succeeds with no further change.
- **DEV:** the web app has no decline action (unsure; none found in the partner web libraries).

### E2E-TAKE-039 — Taking a conflicting job ends a reservation the cleaner can no longer honour
- **Priority:** P2
- **Role:** Cleaner (two accounts)
- **Surfaces:** API
- **Rule:** → /flows/offerability-and-take#taking-one-job-can-end-another — confirming a take ends any preferred reservation the cleaner can no longer honour in that time window
- **Given** order X held for Cleaner A at 12:00 and an open order Y at 12:00. **When** Cleaner A takes Y. **Then** the hold on X ends and Cleaner B sees X on the board; Cleaner A gets the assignment notice for Y and no closure message for Y.
- **DEV:** needs two Plus bookings or one Plus booking and one plain one at the same slot.

## Drop and cover

### E2E-TAKE-040 — The last cleaner drops a confirmed job
- **Priority:** P1
- **Role:** Cleaner
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /product/business-rules#crew-lost — a `Confirmed` booking whose last cleaner leaves goes back to `New` and is re-offered
- **Given** Cleaner A is the only cleaner on a `Confirmed` one-cleaner job whose start is ahead. **When** Cleaner A drops it (web "Cancel job", Android and iOS "Withdraw from this job", or `DropOrder`). **Then** the call succeeds at no cost; the order is `New`; Cleaner A is off the crew; the order is back on the board for Cleaner B; the seat has no lockout or penalty.
- **DEV:** the audit row `employee.order.dropped` is read by Admin in E2E-TAKE-042.

### E2E-TAKE-041 — The customer is told a cleaner can no longer come
- **Priority:** P1
- **Role:** Customer
- **Surfaces:** Web · iOS · Android
- **Rule:** → /product/business-rules#crew-lost — the customer is told of every release, and the booking stands
- **Given** E2E-TAKE-040 just happened. **When** the Customer opens My orders. **Then** the order shows `New` again and nothing is cancelled or refunded; iOS and Android hold the inbox entry "A cleaner can no longer come"; the entry names no cleaner and gives no reason.
- **DEV:** the e-mail "A cleaner can no longer come" goes to `@example.com` and is not readable; the order page link is the proof on the web.

### E2E-TAKE-042 — The administrators are told, and the audit has the drop
- **Priority:** P1
- **Role:** Admin
- **Surfaces:** Web · API
- **Rule:** → /product/business-rules#admin-notifications — `admin.order.crew_lost` goes to the administrators when nobody is left on the order
- **Given** E2E-TAKE-040 just happened. **When** Admin opens the notification feed and the order timeline. **Then** the feed holds `admin.order.crew_lost` with the cause "the cleaner dropped it", the order number and the slot; the timeline holds `employee.order.dropped` for Cleaner A.
- **DEV:** the e-mail copy goes to a mailbox that cannot be read; the feed is the proof.

### E2E-TAKE-043 — One of two cleaners drops: the job stays confirmed and short
- **Priority:** P1
- **Role:** Cleaner (two accounts)
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /product/business-rules#crew-lost — when one of two cleaners leaves, the status stays and the administrators get `admin.order.crew_short`
- **Given** a full `Confirmed` two-cleaner job with A and B. **When** A drops it. **Then** the order stays `Confirmed`, B stays on it, a seat is free and the order is on Cleaner E's board; the Customer gets the same "A cleaner can no longer come" entry; Admin's feed holds `admin.order.crew_short`, not `crew_lost`.
- **DEV:** needs a two-cleaner job.

### E2E-TAKE-044 — A drop of a job on the way or in progress is refused
- **Priority:** P1
- **Role:** Cleaner
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /product/business-rules#crew-lost — a drop is refused once the job is `OnTheWay` or `InProgress` with `order.not_confirmed`, and nothing moves
- **Given** a job Cleaner A set off for (`OnTheWay`) and a job Cleaner A started (`InProgress`). **When** Cleaner A sends `DropOrder` for each over the API. **Then** both answer `order.not_confirmed`; crew and status are unchanged; the apps and the web do not offer the drop control on either job.
- **DEV:** runs inside the EXEC flow after the start window opens.

### E2E-TAKE-045 — The drop control closes at the job's start
- **Priority:** P2
- **Role:** Cleaner
- **Surfaces:** Web · iOS · Android
- **Rule:** → /product/business-rules#crew-lost — the apps offer the drop only while the start is ahead and no lockout is reported
- **Given** a `Confirmed` job for a slot a few minutes ahead. **When** Cleaner A opens the job before the start, waits past it, and reopens (Android on resume, iOS within 30 seconds, web on the next read). **Then** the control is shown before the start and gone after it.
- **DEV:** wait for the slot start (book 2 h ahead); the API itself still takes the drop until the job moves (the docs say the apps are narrower).

### E2E-TAKE-046 — Dropping a job you are not on succeeds and changes nothing
- **Priority:** P2
- **Role:** Cleaner
- **Surfaces:** API
- **Rule:** → /flows/offerability-and-take#the-take-carries-the-acceptance — the placed cleaner may drop at no cost; a cleaner already off the job has nothing to drop
- **Given** Cleaner B is not on a `Confirmed` order. **When** Cleaner B sends `DropOrder`. **Then** the call succeeds with the unchanged crew count, with no audit row and no notice. The docs are silent on this idempotent success; the code returns it before the status gate (unsure whether this is intended).
- **DEV:** the triager checks the docs before counting any other answer as a defect.

### E2E-TAKE-047 — Taking again after a walk-back tells the customer it is a new cleaner
- **Priority:** P2
- **Role:** Cleaner, Customer
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /product/business-rules#crew-lost — a take after a walk-back says that a new cleaner took the booking; two seats and two acceptance rows result when the same cleaner retakes
- **Given** the order after E2E-TAKE-040. **When** Cleaner A takes it again (or Cleaner B does). **Then** the order is `Confirmed`; the Customer gets a second `order.cleaner_assigned` entry; for the same cleaner the order detail lists only the current seat's acceptance while Admin's timeline holds two `employee.order.contract_accepted` rows.
- **DEV:** the e-mail "We've found you a new cleaner" cannot be read.

### E2E-TAKE-048 — A cleaner asks for cover and another takes the seat over
- **Priority:** P2
- **Role:** Cleaner (two accounts)
- **Surfaces:** API
- **Rule:** → /domain/order-lifecycle#confirmed-means-one-thing-a-cleaner-took-the-job — a cover request removes nobody; the seat becomes takeable and a cover swap replaces
- **Given** A is on a `Confirmed` one-cleaner job. **When** A sends `RequestCover`, then again; B then takes the job. **Then** after the request A stays assigned and the order stays `Confirmed`, and B sees the order on the board; the second request succeeds without a second stamp; B's take succeeds, A is off the crew, A gets the cover-answered notice, A's acceptance stays as history, and B has a new acceptance; Admin's timeline holds `employee.order.cover_requested`. A cleaner not on the crew gets `order.employee_not_assigned`, and a completed or cancelled order answers `order.not_confirmed`.
- **DEV:** no web, iOS or Android screen calls `RequestCover` (checked by search), so this runs on the API only; the swap writes no crew-lost notice.

## Admin placement

### E2E-TAKE-049 — Admin places a cleaner on an open order
- **Priority:** P1
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /admin-app/order-management#order-reassignment — a reassignment writes `Confirmed` itself and the placed partner is notified
- **Given** an open `New` order with nobody on it and approved Cleaner A. **When** Admin places A (`POST api/AdminOrder/reassign` with no from-cleaner). **Then** the order is `Confirmed`, A is on the crew, A gets a notice, and the order detail shows A's seat as "contract pending".
- **DEV:** reused by E2E-TAKE-050 and the contract gate in the EXEC flow.

### E2E-TAKE-050 — The placed cleaner accepts the contract with the same act as a take
- **Priority:** P1
- **Role:** Cleaner
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /flows/offerability-and-take#the-take-carries-the-acceptance — `AcceptWorkContract` is the same act for a seat formed without one, and a second accept answers success with the same row
- **Given** E2E-TAKE-049's placement. **When** Cleaner A opens the job (banner "Accept the contract for work before you start"), accepts, then accepts again. **Then** both accepts succeed with the same row; Admin's detail changes to "accepted {date}, v{version}"; a wrong text id answers `contract.text_mismatch`, a blank one `contract.not_accepted`, a non-crew cleaner `order.employee_not_assigned`, a cancelled order `order.take.already_cancelled`.
- **DEV:** the accept is admitted on any order not over, including one in progress.

### E2E-TAKE-051 — Admin replaces a cleaner and must give a reason
- **Priority:** P2
- **Role:** Admin, Cleaner (two accounts)
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /product/business-rules#placement-is-an-offer — a reassignment that removes a cleaner is refused without `removalReason` (`order.reassign.removal_reason_required`, at most 500 characters), and the removed cleaner reads it
- **Given** A is on a `Confirmed` order. **When** Admin replaces A with B without a reason, with a 501-character reason, then with "A cannot come". **Then** the answers are `order.reassign.removal_reason_required`, `common.max_length` and success; the order stays `Confirmed`; A gets `order.assignment_revoked` and reads "A cannot come" through `GetMyAssignmentRemoval` (a page "You were taken off this job" on Android and iOS, a block above the job on the web); B's seat is "contract pending".
- **DEV:** the revoked notice carries only the order, not the reason.

### E2E-TAKE-052 — Placement refusals name the reason
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /admin-app/order-management#order-reassignment — placement refuses a missing, unapproved, other-market, busy or document-less cleaner and a finished order
- **Given** open orders and cleaners: one unapproved, one in another market, one busy at the slot, one who has not accepted the cleaner documents. **When** Admin places each, then places A on a completed and on a cancelled order, then A on an order A is already on, then on a full order. **Then** the answers are `order.reassign.employee_not_approved`, `order.reassign.employee_other_market`, `order.reassign.employee_busy`, `employee.legal_documents_not_accepted`, `order.already_completed`, `order.already_cancelled`, `order.employee_already_assigned` and `order.no_available_spots`. The code checks the documents second and the market third; the docs table lists the documents last, which only matters when two apply at once.
- **DEV:** the other-market cleaner needs a second market (unsure DEV has one); report the others.

### E2E-TAKE-053 — A placement skips the weekly cap, the hold and the profile checks
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** API
- **Rule:** → /admin-app/order-management#order-reassignment — the weekly cap, the preferred-cleaner hold and profile completeness are not checked on a placement
- **Given** Cleaner A at their weekly cap. **When** Admin places A on an open order. **Then** the placement succeeds.
- **DEV:** restore the cap after.

### E2E-TAKE-054 — A placement dropped and re-added needs a new acceptance
- **Priority:** P2
- **Role:** Admin, Cleaner
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /flows/offerability-and-take#edge-cases — a cleaner who drops and is re-added by Admin has a new seat with no acceptance, refused at Start until they accept again
- **Given** Cleaner A accepted a placement (E2E-TAKE-050), then dropped the job. **When** Admin places A again. **Then** the seat shows "contract pending" again; A's Start attempt answers `contract.acceptance_required` (EXEC flow) until A accepts.
- **DEV:** continues in E2E-EXEC-009.

### E2E-TAKE-055 — Staff roles and non-staff callers
- **Priority:** P2
- **Role:** Admin, Cleaner, Customer
- **Surfaces:** API
- **Rule:** → /admin-app/order-management#order-reassignment — reassignment is an administrator act
- **Given** a Cleaner token, a Customer token and an Accountant-role admin token (`SupportOrAbove` excludes Accountant). **When** each calls `POST api/AdminOrder/reassign` on the admin host. **Then** every call is refused (403 or 401); no seat changes.
- **DEV:** DEV may have no Accountant account (unsure); run the other two.

## Notifications and the digest

### E2E-TAKE-056 — Administrators are told when an order becomes offerable
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** Web · API
- **Rule:** → /flows/offerability-and-take#two-synchronised-broadcasts — a cash one-off at creation and a card order on its payment raise `admin.order.new`; an unpaid card checkout announces nothing
- **Given** Admin's notification feed. **When** a Customer books a cash order, then another books a card order and pays, then a third leaves a card checkout unpaid. **Then** the feed holds `admin.order.new` for the first two at the time the order became offerable and nothing for the third.
- **DEV:** feed only; the e-mail copy cannot be read.

### E2E-TAKE-057 — Nobody has taken a job a day before: the administrators are told once
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** Web · API
- **Rule:** → /flows/execution-and-completion#edge-cases — `admin.order.no_cleaner_yet` goes once, after any hold lapses and an hour after the last status change, and nothing is sent once the start has passed
- **Given** an unstaffed order with a start 2 h 15 min ahead. **When** Admin watches the feed from booking until 15 minutes after the start. **Then** `admin.order.no_cleaner_yet` appears once, about an hour after the booking and before the start; none after the start; the order is cancelled 30 minutes after the start by the sweep (CANCEL flow).
- **DEV:** about 3 h of waiting; run in parallel with other waits.

### E2E-TAKE-058 — The new-jobs digest tells a cleaner about fresh work once
- **Priority:** P2
- **Role:** Cleaner
- **Surfaces:** iOS · Android
- **Rule:** → /flows/offerability-and-take#new-jobs-digest — the sweep tells each cleaner about work that is fresh to them personally, at most one digest per interval, and the category can be turned off
- **Given** Cleaner A with push enabled and a watermark behind a new open order. **When** the sweep runs. **Then** Cleaner A gets one digest push naming the order; a second sweep without new work sends none; with the category switched off in the notification settings the next fresh order sends none.
- **DEV:** needs a device that receives DEV pushes (a simulator does not); the sweep interval is unsure from the docs.

## Not testable on DEV

- **Push delivery to a simulator or emulator.** DEV pushes reach a physical device only; on a simulator or the emulator the cases check the inbox entry or the feed, not the push.
- **E-mail delivery.** `@example.com` never receives mail, so no case reads "Cleaner found", "A cleaner can no longer come" or any administrator e-mail.
- **A second tenant or market.** E2E-TAKE-008 and the other-market half of E2E-TAKE-052 need a market with another currency, and the DEV registry holds one company (`cleansia-cz`); the cases run only if a second market exists.
- **An order with no contract text.** E2E-TAKE-019 needs a fixture; the booking path always stamps a text.
- **Exact race timing.** The seat race, the multi-seat refusal and the take-versus-drop outcomes (E2E-TAKE-020, -032, -034) depend on request timing; the suite can only run them repeatedly and assert the state that is never allowed.
- **The take-first case against the unfilled sweep and the admin placement racing a drop.** The docs list both as unguarded residuals; they cannot be forced from outside.
- **Load.** Thousands of cleaners tapping one seat after a broadcast is not tested.
- **Apple and Google sign-in.** Not used here.
