# Payment and fiscal: cases

How money arrives and is recorded: web Checkout and mobile PaymentSheet, the webhook that settles them, saved cards, what a customer owes and the pay link, receipts, and the card kill switch. Source: `docs/flows/payment-and-fiscal.md`.

Webhooks have no user surface: a case that says "the webhook lands" is observed through the order, the receipt, the admin notifications feed and the customer's push or order page, never by calling Stripe. The role agents have no Stripe access.

## Card payment on the web (Checkout)

### E2E-PAY-001 — A guest pays a booking by card and the webhook settles it
- **Priority:** P1
- **Role:** Guest
- **Surfaces:** API · Web
- **Rule:** → /flows/payment-and-fiscal#the-path — the signed webhook sets `PaymentStatus = Paid` and the order stays `New`
- **Given** a guest with a card booking built to the Stripe Checkout page (`POST api/Payment/CreateOrder`, card), payment `Pending`. **When** the guest pays with 4242 4242 4242 4242 and Stripe returns to the success page. **Then** within about a minute the guest's order page (access-token link) reads payment `Paid`, order status `New` (not `Confirmed`), and the order carries its payment intent; a second reload changes nothing.
- **DEV:** Needs a slot from 08:00 Prague. A guest's e-mail never arrives, so the order is reached through the success page link or `Lookup`.

### E2E-PAY-002 — A signed-in customer pays by card on the web and sees the paid price card
- **Priority:** P1
- **Role:** Customer
- **Surfaces:** API · Web
- **Rule:** → /flows/payment-and-fiscal#amounts-are-never-reconciled-and-do-not-need-to-be — the card is asked for the server's `AmountDueOnCard`, never a client figure
- **Given** a signed-in customer on the payment step with card chosen and no credit. **When** they pay through Checkout. **Then** the Stripe page asks for exactly the order total; the order detail shows *Paid by card* with the total, `creditAppliedAmount` 0, payment `Paid`, status `New`; before the webhook landed (success page opened first) the price card reads *To pay by card*, not *Paid by card*.
- **DEV:** The window between redirect and webhook is a few seconds; re-read once without waiting to see the *To pay by card* wording, and record it as unobservable if the webhook already landed.

### E2E-PAY-003 — A card order becomes offerable to cleaners only when it is paid
- **Priority:** P1
- **Role:** Cleaner
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /flows/payment-and-fiscal#edge-cases — "Card order paid": the order became offerable on this write, never at creation
- **Given** a one-off card booking in an approved cleaner's market, payment `Pending` (Checkout not completed). **When** the cleaner opens the job board, then the customer pays, then the cleaner refreshes the board. **Then** the order is absent before payment and present after, with the same slot and area; taking it succeeds.
- **DEV:** Needs an approved DEV cleaner in the order's market and a free slot. Android and iOS partner apps read the same board.

### E2E-PAY-004 — Administrators are told once that a card order was paid
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /flows/payment-and-fiscal#edge-cases — a paid card order writes `admin.order.new`; a redelivery never reaches the site
- **Given** a card order just settled (E2E-PAY-001 or -002). **When** an administrator opens the notifications feed and the order in the order list. **Then** exactly one `admin.order.new` row names the order, the order list shows payment `Paid` and status `New`, and no second row appears after a reload.
- **DEV:** The e-mail copy of the notice cannot be read.

### E2E-PAY-005 — One web payment raises two events and settles once
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API · Web
- **Rule:** → /flows/payment-and-fiscal#saved-cards-and-receivables — a web payment raises `checkout.session.completed` and `payment_intent.succeeded`; only the session settles the order
- **Given** a signed-in customer paying by card on the web with *Save this card* ticked. **When** the payment completes. **Then** the order is `Paid` once, exactly one sale receipt exists (one number), the admin feed holds one `admin.order.new`, and the saved card is listed once.
- **DEV:** The two events cannot be sent separately; this is the observable consequence of Stripe's own two events.

### E2E-PAY-006 — Abandoning Checkout leaves the order unpaid and resumable
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API · Web
- **Rule:** → /flows/payment-and-fiscal#the-stale-checkout-sweep — an unpaid card checkout is retracted only after an hour
- **Given** a card booking sent to Stripe Checkout. **When** the customer presses back on the Stripe page (cancel URL). **Then** the cancel page appears, the order stays payment `Pending` and status `New`, and *Resume payment* (`POST api/Payment/ResumeCheckout`) returns a Checkout session for the same amount that can be paid.
- **DEV:** Resume within the hour. Paying the resumed session must settle the order exactly as E2E-PAY-001.

### E2E-PAY-007 — The stale-checkout sweep cancels an abandoned card order after an hour
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API · Web
- **Rule:** → /flows/payment-and-fiscal#the-stale-checkout-sweep — card orders `Pending` for over an hour are cancelled, every 15 minutes
- **Given** a one-off card order left unpaid at Checkout. **When** more than 60 minutes plus one sweep tick (15 minutes) pass. **Then** the order is `Cancelled` with reason `order.cancelled.payment_not_completed`, payment not `Paid`, no fee and no refund, applied credit (if any) returned once, and it is gone from the cleaners' board; *Resume payment* now fails.
- **DEV:** Waits 60 to 80 minutes. A guest's e-mail with a fresh link cannot be read.

### E2E-PAY-008 — A paid order cannot be paid again
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API
- **Rule:** → /flows/payment-and-fiscal#saved-cards-and-receivables — re-opening the charge surface of a settled order is refused
- **Given** a card order already `Paid`. **When** the customer calls `POST api/Payment/ResumeCheckout` and `POST api/Payment/CreatePaymentIntent` for it. **Then** both answer `order.payment.already_paid` and no new Stripe session or intent appears on the order.
- **DEV:** API only.

### E2E-PAY-009 — A card that is declined leaves the order retryable and tells the administrators once
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API · Web
- **Rule:** → /flows/payment-and-fiscal#edge-cases — a failed payment leaves the status alone; only the first decline raises `admin.payment.failed`
- **Given** a card order at Checkout. **When** the customer pays with 4000 0000 0000 0002 (declined), tries again with the same card, then pays with 4242 4242 4242 4242. **Then** after the declines the order is still payment `Pending` and status `New`; the admin feed holds one `admin.payment.failed` for the order, not two; after the good card the order is `Paid`.
- **DEV:** 4000 0000 0000 0002 is Stripe's generic decline test card and is not listed in `e2e/README.md`; confirm it in Stripe's test-card list first.

### E2E-PAY-010 — A payment that lands after the order was cancelled is not marked paid
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API · Web
- **Rule:** → /flows/payment-and-fiscal#edge-cases — "Card payment settles after the order was cancelled": escalates a dispute for a refund
- **Given** a signed-in customer with a card order and its Checkout page open in one tab. **When** the customer cancels the order in a second tab (free: no cleaner), then pays in the first tab. **Then** if Stripe still accepts the payment, the order is not `Paid`, a dispute for a refund exists on it (the customer's open one, or a new one) and the admin console lists it; if Stripe refuses the payment because the session was closed by the cancel, record the case as blocked, not failed.
- **DEV:** Unsure whether the cancel closes the Checkout session at Stripe; `docs/` does not say. The triager decides which outcome applies.

## Mobile payment (PaymentSheet)

### E2E-PAY-020 — iOS: a customer pays a booking in PaymentSheet and the order settles
- **Priority:** P1
- **Role:** Customer
- **Surfaces:** API · iOS
- **Rule:** → /flows/payment-and-fiscal#saved-cards-and-receivables — an app's intent carries the `OrderId`, so its `payment_intent.succeeded` settles the order
- **Given** a signed-in customer on the iOS booking review with card chosen. **When** they submit, complete PaymentSheet with 4242 4242 4242 4242 and reach the success screen. **Then** the success screen says *Paid by card*; the order detail then reads payment `Paid`, status `New`; an `order.payment_confirmed` push and inbox entry exist; the Stripe amount shown in the sheet equals *To pay by card* on the review step.
- **DEV:** Simulator with the DEV build; Stripe test mode accepts the test card in PaymentSheet. The success screen may read the order before the webhook, so check the detail screen after a pause.

### E2E-PAY-021 — Android: a customer pays a booking in PaymentSheet and the order settles
- **Priority:** P1
- **Role:** Customer
- **Surfaces:** API · Android
- **Rule:** → /flows/payment-and-fiscal#saved-cards-and-receivables — the same settlement on the other app
- **Given** a signed-in customer on the Android booking review with card chosen. **When** they submit and complete PaymentSheet with 4242 4242 4242 4242. **Then** the success screen says *Paid by card*; the order detail reads payment `Paid`, status `New`; the push `order.payment_confirmed` arrives.
- **DEV:** Emulator `cleansia35`.

### E2E-PAY-022 — Re-opening PaymentSheet hands back the same intent and does not cancel the booking
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API · iOS · Android
- **Rule:** → /flows/payment-and-fiscal#saved-cards-and-receivables — `CreatePaymentIntent` returns the open intent for the same amount, Customer and tick
- **Given** a booked card order, PaymentSheet opened once and dismissed. **When** the customer opens it again. **Then** the second `POST api/Payment/CreatePaymentIntent` returns the same payment intent id and amount, the order is still `New` with payment `Pending` and not cancelled, and paying completes the order.
- **DEV:** Compare the two responses by intent id (API call from the same session).

### E2E-PAY-023 — Changing the save-card tick replaces the intent without cancelling the order
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API · iOS · Android
- **Rule:** → /flows/payment-and-fiscal#saved-cards-and-receivables — an old intent cancelled as `duplicate` leaves the order alone
- **Given** a card order whose sheet was opened with the tick off. **When** the customer reopens the sheet with the tick on (or calls `CreatePaymentIntent` with `saveCard: true`) and waits for webhooks. **Then** a different intent id is returned, the order is not cancelled (still `New`, payment `Pending`, no `order.cancelled` push), and paying the new intent settles it.
- **DEV:** The `payment_intent.canceled` webhook for the old intent arrives within seconds; wait 30 seconds before reading the order.

### E2E-PAY-024 — A declined card in PaymentSheet keeps the booking open
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** iOS · Android
- **Rule:** → /flows/payment-and-fiscal#edge-cases — payment failure leaves the status alone so the client can retry
- **Given** the PaymentSheet open on a booked card order. **When** the customer enters 4000 0000 0000 0002 and the sheet shows the decline, then retries with 4242 4242 4242 4242. **Then** the sheet reports the decline and stays usable, the order stays payment `Pending` between attempts, and the second card settles it.
- **DEV:** Same Stripe decline test card note as E2E-PAY-009.

### E2E-PAY-025 — Credit is a tender: the card is asked for what the credit leaves
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /flows/payment-and-fiscal#amounts-are-never-reconciled-and-do-not-need-to-be — `AmountDueOnCard = TotalPrice − CreditAppliedAmount`
- **Given** the customer holds a credit balance in the order's currency (granted by the administrator, `POST api/AdminCredit/issue`). **When** they book by card. **Then** the confirm step shows *Your credit −X* and *To pay by card Y* with X no more than 70 % of the total, Stripe asks exactly Y, the order reads `creditAppliedAmount` X and `amountDueOnCard` Y, and the success screen and order detail show the same two figures; the receipt (E2E-PAY-050) states the credit applied and what the card paid.
- **DEV:** The administrator grants credit first (CZK, say 100). Credit is not spent on a cash booking.

## Webhook endpoint

### E2E-PAY-030 — A webhook call without a signature header is refused
- **Priority:** P1
- **Role:** Guest
- **Surfaces:** API
- **Rule:** → /flows/payment-and-fiscal#the-four-things-that-make-it-safe — the signature is the authentication
- **Given** the three hosts that serve the webhook (Customer, Partner, Customer Mobile). **When** an anonymous `POST api/Payment/webhook` carries a JSON body and no `Stripe-Signature` header. **Then** each answers 400 with `payment.stripe_signature_required`, and no order or event row changes.
- **DEV:** Anonymous call, no Stripe access needed. Rate limit `webhook` may answer 429 after many calls; keep to a handful.

### E2E-PAY-031 — A webhook call with a forged signature is refused
- **Priority:** P1
- **Role:** Guest
- **Surfaces:** API
- **Rule:** → /flows/payment-and-fiscal#the-four-things-that-make-it-safe — an invalid signature is rejected before anything is read
- **Given** a body shaped like a `checkout.session.completed` event for a real DEV order id and a made-up `Stripe-Signature` value. **When** it is posted to each webhook host. **Then** the answer is 400 with `InvalidSignature` ("Invalid webhook signature") and the order stays unchanged (payment still `Pending`).
- **DEV:** Use a throwaway order the test created. Never use an order that matters.

### E2E-PAY-032 — A webhook call with an empty body is refused
- **Priority:** P2
- **Role:** Guest
- **Surfaces:** API
- **Rule:** → /flows/payment-and-fiscal#the-four-things-that-make-it-safe — the endpoint needs the raw payload to verify
- **Given** any webhook host. **When** an anonymous `POST api/Payment/webhook` has an empty body and a signature header. **Then** the answer is 400 with `payment.json_payload_required`.
- **DEV:** API only.

### E2E-PAY-033 — A chargeback on a guest's web card booking reaches the console as an escalated dispute
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /flows/payment-and-fiscal#edge-cases — a chargeback is reflected onto the linked dispute, not the payment status, and the administrators are told
- **Given** a guest card booking paid with Stripe's chargeback test card 4000 0000 0000 0259. **When** Stripe raises the dispute (minutes after the charge) and the webhook lands. **Then** the order's payment status is unchanged, a `Chargeback` dispute (reason 8) is escalated on the order in the console, and an `admin.dispute.chargeback` row (reversed amount, dispute) is in the feed for every admin role including the Accountant.
- **DEV:** 4000 0000 0000 0259 is Stripe's documented test card for a chargeback and is not in `e2e/README.md`; confirm it in Stripe's test docs and with the owner, because it opens a dispute on the shared test account. The guest cannot see anything: the guest's check is that nothing changed on the order page.

### E2E-PAY-034 — A chargeback on a mobile card booking finds the order by its intent
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /flows/payment-and-fiscal#edge-cases — an app's payment stores its intent when it is created, so the webhook finds the order
- **Given** a signed-in customer's PaymentSheet booking paid with 4000 0000 0000 0259. **When** the chargeback webhook lands. **Then** the console shows an escalated `Chargeback` dispute on that order, linked to the customer's account, and `admin.dispute.chargeback` is raised once.
- **DEV:** Same card and consent note as E2E-PAY-033.

## Saved cards

### E2E-PAY-040 — A ticked card payment lands a saved card
- **Priority:** P1
- **Role:** Customer
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /product/business-rules#save-card — only a ticked payment keeps a card, and the tick is the consent
- **Given** a signed-in customer with no saved card and the box *Save this card for my next bookings* ticked (unticked by default, consent sentence printed with it). **When** they pay a card booking with 4242 4242 4242 4242. **Then** once Stripe confirms, Saved cards (web `/profile`; apps Profile → Payments) list one card: brand Visa, last four 4242, expiry, the booking's currency; it is retained across a reload; cash is now selectable on the next booking (see the cash cases in `booking-and-pricing.md`).
- **DEV:** The card appears a few seconds after the webhook; pending setups are not listed.

### E2E-PAY-041 — An unticked card payment keeps no card
- **Priority:** P1
- **Role:** Customer
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /flows/payment-and-fiscal#saved-cards-and-receivables — unticked, neither channel keeps a card
- **Given** a customer with no saved card. **When** they pay a card booking with the box unticked. **Then** the order is `Paid` and Saved cards stays empty (*No card saved.* / the empty-state copy), on all clients, and on the apps the PaymentSheet did not show Stripe's own save box.
- **DEV:** Check the list after the webhook has surely landed (order `Paid`).

### E2E-PAY-042 — A guest is never offered the tick
- **Priority:** P2
- **Role:** Guest
- **Surfaces:** API · Web
- **Rule:** → /product/business-rules#save-card — a saved card belongs to an account
- **Given** a guest on the web payment step. **When** the guest chooses card. **Then** no save-card box is shown; and a direct `POST api/Payment/CreateOrder` with `saveCard: true` and no account is refused with `saved_card.requires_account`, creating no order.
- **DEV:** The API half needs a hand-built request from the guest session.

### E2E-PAY-043 — Saving a card without consent is refused
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API · iOS
- **Rule:** → /product/business-rules#saved-cards — the capture needs the consent; the refusal asks only for that
- **Given** a signed-in customer. **When** they call `POST api/SavedCard/CreateSetupIntent` (mobile) or `POST api/SavedCard/CreateCheckoutSession` (web host) with consent false; and on iOS press *Save a card* without ticking the consent. **Then** the API answers `saved_card.consent_not_accepted`, the iOS button stays disabled or shows the consent error, and no `SavedCards` row appears in the list.
- **DEV:** The web has no screen for the standalone capture since 2026-10-04, so the web half is API only.

### E2E-PAY-044 — iOS saves a card from Profile → Payments
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API · iOS
- **Rule:** → /product/business-rules#saved-cards — iOS keeps *Save a card* on the Payments screen through a setup sheet
- **Given** a signed-in customer with consent ticked on Profile → Payments. **When** they complete the setup sheet with 4242 4242 4242 4242. **Then** `setup_intent.succeeded` lands the card and the list shows it; the account is charged nothing.
- **DEV:** Android has no standalone *Save a card* in the docs; whether it has one is unsure, so no Android case is claimed.

### E2E-PAY-045 — A newer card in the same currency retires the earlier one
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /product/business-rules#saved-cards — at most one card per currency
- **Given** a saved Visa 4242 in CZK. **When** the customer pays another CZK booking with a ticked Mastercard 5555 5555 5555 4444. **Then** the list shows only the Mastercard.
- **DEV:** Needs two paid bookings.

### E2E-PAY-046 — A payment that never completes saves nothing
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API · Web
- **Rule:** → /product/business-rules#save-card — a row whose payment never succeeds is never captured
- **Given** the box ticked on a card booking. **When** the customer abandons Checkout (or is declined with 4000 0000 0000 0002). **Then** Saved cards stays empty and a cash booking is still refused with `order.cash_requires_saved_card`.
- **DEV:** Compare with E2E-PAY-040, where the same steps with a good card save one.

### E2E-PAY-047 — Removing a saved card works only on one's own card
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /product/business-rules#saved-cards — `DELETE api/SavedCard/Remove/{id}`; another's card is `saved_card.not_found`
- **Given** customer A with a saved card and customer B signed in. **When** B calls Remove with A's card id, then A removes it from the UI. **Then** B's call answers `saved_card.not_found` and A's card is untouched; A's removal drops it from the list and cash is again refused with `order.cash_requires_saved_card`.
- **DEV:** Needs two customers.

### E2E-PAY-048 — A recurring card occurrence offers the same tick
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /flows/payment-and-fiscal#saved-cards-and-receivables — confirming a recurring occurrence by card offers the tick, off by default
- **Given** an unconfirmed recurring occurrence on a card schedule. **When** the customer confirms it with the tick on, then (second occurrence) off. **Then** ticked: the card is saved and listed; unticked: nothing saved; both occurrences are `Paid` after payment; on the apps the sheet after a ticked confirm opens on the Customer.
- **DEV:** Needs occurrences from the recurring materialiser, which runs on a timer; the first card occurrence may take a day to appear.

### E2E-PAY-049 — Nothing charges a saved card
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /flows/payment-and-fiscal#saved-cards-and-receivables — the off-session charges stay switched off
- **Given** a customer with a saved card and an open receivable (see E2E-PAY-070). **When** more than two 15-minute sweeps pass. **Then** the receivable is still `Open` with no charge attempt recorded and the customer received no payment; the pay link is still the only way to pay it.
- **DEV:** Waits about 35 minutes. The agent cannot see Stripe, so the check is the receivable row and the absence of a `Paid` order or fee receipt.

## Receipts

### E2E-PAY-050 — A paid card order has a sale receipt the customer can download
- **Priority:** P1
- **Role:** Customer
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /flows/payment-and-fiscal#what-the-receipt-says — one sale receipt once money has been received, under a gapless number
- **Given** a card order just settled. **When** the customer downloads the receipt (`GET api/Order/DownloadReceipt`; web order detail; app order detail). **Then** a PDF arrives with a receipt number, the customer's name and address and no e-mail or phone, lines that add up to the order total to the cent, the payment status and method as words, and the clean time in the market's zone; downloading again returns the same stored copy and number.
- **DEV:** Receipt generation is queued: allow up to a minute. Read the PDF with a text extractor.

### E2E-PAY-051 — An unpaid order has no receipt
- **Priority:** P1
- **Role:** Customer
- **Surfaces:** API · Web
- **Rule:** → /flows/payment-and-fiscal#what-the-receipt-says — a cash booking and an unpaid card order get none
- **Given** a cash booking (payment `Pending`) and a card order left unpaid. **When** the customer calls `DownloadReceipt` for each. **Then** both answer `receipt.not_found`, and the web order page shows no receipt button.
- **DEV:** Same call after the cash is recorded is E2E-PAY-052.

### E2E-PAY-052 — A cash sale's receipt appears at completion, after the cleaner records the cash
- **Priority:** P1
- **Role:** Customer
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /product/business-rules#cash-handover — the cash receipt comes with the completion, from the completion fallback
- **Given** a cash booking taken by a cleaner and started. **When** the cleaner records the cash (`MarkCashCollected`), the customer calls `DownloadReceipt`, the cleaner completes the job, and the customer calls it again. **Then** after the cash and before completion the answer is `receipt.not_found`; after completion a receipt exists that prints the booked slot, the completion time and the cash-received time, all in market time, with payment `Paid`.
- **DEV:** The cash and completion steps belong to `execution-and-completion.md`; the job starts 60 minutes before its slot.

### E2E-PAY-053 — A receipt belongs to its customer
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API
- **Rule:** → /flows/payment-and-fiscal#what-the-receipt-says — the download serves the order's own stored copy
- **Given** customer A's paid order and customer B signed in. **When** B calls `DownloadReceipt` with A's order id. **Then** the answer is `order.not_found` and no file is served.
- **DEV:** Needs two customers.

### E2E-PAY-054 — A receipt is in the language the booking was made in
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API · Web
- **Rule:** → /flows/payment-and-fiscal#what-the-receipt-says — the language is chosen once, at issue
- **Given** a booking made with the web in Czech, then the account switched to English. **When** the receipt is downloaded. **Then** labels, payment status and method are in Czech and amounts are written Czech style ("2 000,00 Kč"); the PDF is unchanged after the language switch.
- **DEV:** Repeat with Slovak, Ukrainian and Russian only as P2 extras; any other code prints English.

### E2E-PAY-055 — Receipt lines add up, surcharges and discounts are lines of their own
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API · Web
- **Rule:** → /flows/payment-and-fiscal#what-the-receipt-says — services, extras, dirtiness, express, then each discount negative; the lines equal the total
- **Given** a card booking with an extra, the heavy dirtiness level, an express slot (2 to 4 hours ahead) and a promo code. **When** the receipt is downloaded. **Then** it lists each service/extra at its price, *Heavy dirtiness surcharge*, *Express surcharge* and the promo discount as separate lines (zero lines absent), and the sum equals the total to the cent.
- **DEV:** The express slot needs a booking made 2 to 4 hours before a start time inside 08:00 to 19:45 Prague. Promo codes depend on DEV seed data: unsure which exist.

### E2E-PAY-056 — The receipt states credit applied and what the card paid
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API · Web
- **Rule:** → /flows/payment-and-fiscal#amounts-are-never-reconciled-and-do-not-need-to-be — the sale keeps `TotalPrice`; the receipt states credit and the card share
- **Given** the credit booking of E2E-PAY-025. **When** the receipt is downloaded. **Then** the lines sum to the full order total, and the receipt states credit applied X and paid by card Y.
- **DEV:** Depends on E2E-PAY-025.

### E2E-PAY-057 — The receipt prints the sale's VAT posture
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API · Web
- **Rule:** → /flows/payment-and-fiscal#what-the-receipt-says — a sale with VAT prints subtotal, VAT and the company VAT number; one without prints the non-payer notice and no VAT number
- **Given** a paid order. **When** its receipt is read. **Then** if the order charged VAT the PDF shows subtotal without VAT, VAT at the order's rate and the issuer's VAT number; if it charged none it shows the total and *Nejsme plátci DPH* (Czech) and no VAT-number line.
- **DEV:** Unsure which posture the DEV company row has; the case passes with either, provided the two never mix.

### E2E-PAY-058 — Fiscal registration is off on DEV and fails nothing
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /flows/payment-and-fiscal#no-guessed-unit-no-guessed-regime — with no enforcement mode there is nothing to register
- **Given** a paid card order with a receipt. **When** an administrator opens the fiscal failures page. **Then** the receipt is not listed as failed, the PDF carries no fiscal code, and no `FiscalRegistrationFailed` state is shown for it.
- **DEV:** Fiscal registration itself cannot be tested here; see the closing section.

## What a customer owes (receivables and pay links)

### E2E-PAY-070 — A customer lists what they owe and pays it through the pay link
- **Priority:** P1
- **Role:** Customer
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /product/business-rules#receivables — the pay link is the only way a receivable is paid today
- **Given** the customer has an open cash-cancellation-fee receivable (made by the late cash cancellation case in `cancellation-refund-dispute.md`). **When** they open what they owe (`GET api/Receivable/GetMine`; web order page; apps Profile → Payments), press *Pay now* (`POST api/Receivable/CreatePayLink/{id}`) and pay the Checkout with 4242 4242 4242 4242. **Then** the list showed kind, amount and currency before payment; after the webhook the receivable is `Paid`, the list is empty, and the order's own payment status, charge surface and refunds are untouched.
- **DEV:** Needs a prior late cash cancellation; see the CANCEL case for how long it takes to set up.

### E2E-PAY-071 — A paid fee earns a fee receipt of its own
- **Priority:** P1
- **Role:** Customer
- **Surfaces:** API · Web
- **Rule:** → /flows/payment-and-fiscal#fee-receipt — a receivable's payment is a sale of its own, stating the fee alone
- **Given** the receivable of E2E-PAY-070 now paid. **When** the customer looks for a receipt. **Then** the order has a fee receipt with its own number, one line labelled by the kind (*Late cancellation fee*) for the fee only, and it was not e-mailed (cannot be seen); `DownloadReceipt` on a cancelled cash order still answers `receipt.not_found` because it serves the sale receipt only.
- **DEV:** The fee receipt is reachable by the administrator on the order page or the receipt list: unsure which control shows it, since `docs/` says only that the download serves the sale receipt.

### E2E-PAY-072 — While a customer owes anything, no booking is taken, cash or card
- **Priority:** P1
- **Role:** Customer
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /product/business-rules#receivables — one open receivable refuses every new booking in any company
- **Given** the customer has an open receivable. **When** they try to book once with cash and once with card (web wizard, apps, `CreateOrder`). **Then** both are refused with `order.unpaid_receivable` before any credit, promo or Stripe session is touched; the web returns to the payment step listing what is owed with *Pay now*, the apps open a dialog with *Pay now*; the pay link and the list still work. After payment or write-off the next booking succeeds.
- **DEV:** A guest is not matched: a guest booking with the same e-mail still goes through, and that is checked separately in E2E-PAY-073.

### E2E-PAY-073 — A guest is not refused for a debt
- **Priority:** P2
- **Role:** Guest
- **Surfaces:** API · Web
- **Rule:** → /product/business-rules#receivables — refusing an anonymous checkout for a debt would tell anyone which addresses owe money
- **Given** an account with an open receivable. **When** a guest books by card with the same e-mail. **Then** the booking is accepted and no `order.unpaid_receivable` is returned.
- **DEV:** Use a throwaway guest booking and cancel it free afterwards.

### E2E-PAY-074 — The pay link is one per receivable, and only the owner's open receivable gets one
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API
- **Rule:** → /flows/payment-and-fiscal#saved-cards-and-receivables — the link recorded on the receivable is handed back while Stripe reports it open
- **Given** an open receivable, a second customer and a paid receivable. **When** the owner calls `CreatePayLink` twice in a row; the second customer calls it with the first's id; the owner calls it on the paid one. **Then** the two calls return the same session url; the second customer gets `receivable.not_found`; the paid receivable answers `receivable.not_open`.
- **DEV:** Needs a second customer and one paid receivable (after E2E-PAY-070).

### E2E-PAY-075 — A Manager writes off a receivable and its pay link dies
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /flows/payment-and-fiscal#saved-cards-and-receivables — the write-off closes the open Checkout Session at Stripe first
- **Given** an open receivable whose customer has pressed *Pay now* and holds the Checkout url. **When** a Manager or Administrator writes it off on Orders → Receivables with a note (`POST api/AdminReceivable/write-off`). **Then** the receivable reads `WrittenOff` with the note; the customer's held Checkout url shows an expired session; `CreatePayLink` now answers `receivable.not_open`; the customer can book again; a Support user or Accountant doing the same is refused with 403.
- **DEV:** Needs one administrator of each role (created in admin user management). The note is required and at most 500 characters: 501 characters answers `common.max_length`, an empty note `common.required`.

### E2E-PAY-076 — Administrators list receivables and filter them
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /product/business-rules#receivables — any administrator lists the company's receivables, filtered by status, kind, customer or order
- **Given** open, paid and written-off receivables. **When** an administrator of any role opens Orders → Receivables and filters by status and kind. **Then** each filter returns only matching rows and another company's receivables never appear.
- **DEV:** One company on DEV (`cleansia-cz`), so the other-company half is not testable.

### E2E-PAY-077 — A door default's pay-link payment earns a fee receipt labelled unpaid cash and no second crew pay
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API · Web
- **Rule:** → /product/business-rules#cash-not-paid — a paid unpaid-cash receivable asks for no crew pay, the crew was paid at the report
- **Given** a cash job the cleaner completed with *Customer did not pay* (an `UnpaidCash` receivable for the price less credit). **When** the customer pays the pay link. **Then** the receivable is `Paid`, a fee receipt labelled *Unpaid cash payment* exists, the order stays completed with payment `Pending`, and the cleaner's My Pay shows no second line for the job.
- **DEV:** The report is part of `execution-and-completion.md`. Reading the cleaner's pay needs the pay queue to have run.

### E2E-PAY-078 — Recording the cash on a door default writes the debt off as paid in cash
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /flows/payment-and-fiscal#saved-cards-and-receivables — *Record cash received* closes the pay link first, then writes the debt off
- **Given** an unpaid-cash door default with an open pay link. **When** the administrator records the cash (`POST api/AdminOrder/record-cash`, who, when, amount). **Then** the order becomes `Paid`, the sale receipt is issued because the order is complete, the receivable reads written off *Paid in cash* with no fee receipt, and the customer's held Checkout url shows an expired session.
- **DEV:** Time must be after the clean could begin and not in the future.

### E2E-PAY-079 — Recording cash is refused when the customer already paid online
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /flows/payment-and-fiscal#saved-cards-and-receivables — refused with `order.payment_not_outstanding` when Stripe reports the link paid
- **Given** a door default whose pay link the customer has paid. **When** the administrator records the cash. **Then** the answer is `order.payment_not_outstanding` and nothing is recorded.
- **DEV:** If the webhook already marked the receivable paid, the refusal may come from the order state instead; both give the same key.

### E2E-PAY-080 — Recording cash validates amount and time
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /product/business-rules#cash-handover — an amount above zero to the cent, not in the future, not before the clean could begin
- **Given** a cash order in progress or completed that still owes. **When** the administrator records cash with amount 0, with 12.345, with a time in the future, and with a time before the cleaner may start. **Then** the answers are `order.cash_amount_invalid`, `order.cash_amount_invalid`, `order.cash_received_at_in_future` and `order.cash_received_at_before_clean`; nothing is recorded.
- **DEV:** Needs a cash order the cleaner started; the 12.345 value assumes a two-decimal currency (CZK).

## Kill switch and cleaner cash on a card order

### E2E-PAY-090 — With card payments switched off every card surface refuses, and cash still works
- **Priority:** P2
- **Role:** Customer
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /architecture/infrastructure#kill-switches — `Stripe__Enabled` off closes the seven card-charge surfaces and keeps cash
- **Given** the owner has set `Stripe__Enabled=false` on DEV for the run. **When** a customer books by card on the web, books by card on an app, resumes a checkout and starts a Plus subscription; then books cash. **Then** each card attempt answers `order.payment_gateway_unavailable` and no Stripe session or intent exists; the cash booking is accepted; the translated message reads the same as a Stripe outage.
- **DEV:** Needs an app-setting change by the owner and a restore afterwards; the agents do not change DEV settings. Run last.

### E2E-PAY-091 — With card payments switched off refunds and write-offs still work
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /architecture/infrastructure#kill-switches — returning or releasing money is not gated
- **Given** `Stripe__Enabled=false` and a paid card order made before the switch, plus an open receivable with a pay link. **When** an administrator refunds the order and a Manager writes the receivable off. **Then** the refund succeeds and the write-off closes the link with no `order.payment_gateway_unavailable`.
- **DEV:** Same owner-only precondition as E2E-PAY-090.

### E2E-PAY-092 — A cleaner cannot record cash on an order that is already paid by card
- **Priority:** P2
- **Role:** Cleaner
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /product/business-rules#cash — cash is recorded only while money is still owed
- **Given** a paid card order the cleaner is on and has started. **When** the cleaner calls `MarkCashCollected` (the partner apps do not offer the button here). **Then** the answer is `order.cash_already_collected` and the payment status is unchanged; on a refunded or partly refunded order the answer is `order.payment_not_outstanding`.
- **DEV:** API only for the refusal; on the apps assert that the *Mark cash collected* action is absent.

## Not testable on DEV

- Fiscal registration with a real tax authority, its retry job, `FiscalReconciliation`, the fiscal currency and country refusals (`FiscalCurrencyCodeOf`, `FiscalCountryCodeOf`): fiscal is off on DEV and the enforcement modes cannot be set by a test.
- A webhook delivered twice or out of order, parallel redeliveries, a `23505` replay: the role agents cannot send Stripe events; only the owner can resend one from the Stripe dashboard.
- `checkout.session.expired`, a recurring occurrence's session expiring, and the system cancellation they cause: Stripe expires a session after 24 hours. The one-hour sweep is covered by E2E-PAY-007.
- A cash order that already settled in cash and is then paid by card (double settlement): a card order that is unpaid is not on the board, so no cleaner can record cash on it; only an administrator placement reaches it.
- A receivable paid twice, a door price paid after the cash was recorded (both refunded in full), and a Manager's write-off racing a payment: they need two live Checkout sessions on one receivable or a payment landing between a read and a commit.
- A chargeback that matches no order (`admin.dispute.chargeback_unmatched`): every DEV charge is made through the platform, so every chargeback matches an order.
- The off-session charge sweep when switched on (`Payments:OffSessionChargesEnabled`) and the failed-charge pay-link e-mail: the switch is off by owner ruling.
- Receipt PDF retention (ten years, `BlobDeletedAt`, `receipt.not_found` afterwards), e-mail copy (cash booking e-mail, receipt e-mail, money format in e-mail) and the fee receipt "not e-mailed": e-mail to `@example.com` never arrives.
- A missing currency or country row on an order (bare number receipt): the data is always complete on DEV.
- Real e-mail delivery, Apple and Google sign-in, load: out of scope of Phase 1.
