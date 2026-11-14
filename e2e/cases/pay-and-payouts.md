# Pay and payouts — cases

What a cleaner earns, which period it lands in, when the period closes, which invoice it becomes and how the cash a cleaner holds is set off against it. The platform issues the document and an admin keys the bank transfer by hand, so no case moves money.

Ids are `E2E-PAYOUT-NNN`. A DEV pay period lasts 3 days (end = start + 2) and the night run closes it at 02:00 UTC after its end date; **Close now** is the way to see a close within the day, and its invoices appear 15 to 20 minutes later. "Accountant" means an admin whose role is Accountant, Manager or Administrator; Support is the role that is refused.

## Pay rows

### E2E-PAYOUT-001 — A completed job writes one pay row in the open period
- **Priority:** P1
- **Role:** Cleaner
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /flows/pay-and-payouts#the-path — a completed job writes one `OrderEmployeePay` row per assigned seat, in the open period, in the order's currency
- **Given** an open pay period and a cleaner who completed a job (the EXEC journey). **When** the cleaner opens My Pay (partner web `/invoices`, Android and iOS Pay and Earnings, or `GET api/EmployeePayroll/GetPeriodPays`) for the open period, after the pay queue has run. **Then** one row for that order is listed, `currencyCode` equals the summary's, `totalPay` is the seat's share, and the period total includes it.
- **DEV:** reuses the job of the EXEC cases; the row appears when the `calculate-order-pay` message is consumed, so poll for up to 2 minutes before calling it missing.

### E2E-PAYOUT-002 — The dashboard card counts the pay no invoice holds yet
- **Priority:** P1
- **Role:** Cleaner
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /partner-app/dashboard#stat-cards — the card reads "This period, not invoiced yet" and sums the open period's pay that no invoice holds
- **Given** PAYOUT-001 done. **When** the cleaner opens the dashboard (`GET api/Dashboard/GetStats`). **Then** `currentPeriodEarnings` equals the sum of the open period's uninvoiced rows in `currencyCode`; `currentPayPeriodStart` and `currentPayPeriodEnd` are the open period's days; the card is titled "This period, not invoiced yet" and says when the period closes.
- **DEV:** none.

### E2E-PAYOUT-003 — No app or endpoint shows a payout date
- **Priority:** P2
- **Role:** Cleaner
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /decisions/adr-0082#d4-—-r4-next-payout-leaves-the-cleaner-apps — "next payout" is removed because the platform cannot know a date it does not control
- **Given** a signed-in cleaner with pay. **When** the cleaner reads the dashboard (JSON and screens) and My Pay on every surface. **Then** no field named `nextPayoutDate` is in the JSON, and no screen shows a "next payout" label or date; the only date offered is when the period closes.
- **DEV:** none.

### E2E-PAYOUT-004 — A two-seat job pays each seat half, and the rows add up to the job
- **Priority:** P2
- **Role:** Cleaner
- **Surfaces:** API · Web
- **Rule:** → /flows/pay-and-payouts#edge-cases — each cleaner's row is half the job's base, extras and bounds; the first seat takes the residue cents
- **Given** a job that needs two cleaners, taken by two cleaner accounts and completed. **When** each cleaner reads their row. **Then** the two `totalPay` values add up to the job's pay computed from the rates (the first seat's row may be one cent higher), and each row's `basePay` is half the job's base.
- **DEV:** needs two approved cleaner accounts and a `RequiredEmployees = 2` package; the second cleaner is a run account.

### E2E-PAYOUT-005 — A two-seat job worked by one cleaner pays one seat
- **Priority:** P2
- **Role:** Cleaner
- **Surfaces:** API
- **Rule:** → /flows/pay-and-payouts#edge-cases — the divisor is the seats the job needs, not the cleaners who came
- **Given** a two-seat job completed while only one cleaner holds a seat. **When** that cleaner reads their row. **Then** the row is one seat's share (half the job's base), plus the residue cents, not the whole job's pay.
- **DEV:** needs the job to be completable with one seat filled (admin status override or the auto-complete path); mark unsure which of the two the DEV build allows.

### E2E-PAYOUT-006 — The dirtiness level is its own figure on the row
- **Priority:** P2
- **Role:** Cleaner
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /flows/pay-and-payouts#my-pay-shows-one-currency — each pay row carries `dirtinessPay` beside `basePay` and `extrasPay`; the web shows it only when it is not zero
- **Given** one job booked at the Heavy level and one at Standard, both completed. **When** the cleaner opens both rows. **Then** the Heavy row has `dirtinessPay` greater than 0 (30 % of the job's capped pay, split per seat) and the period summary's `totalDirtinessPay` includes it; the Standard row has 0 and the partner web shows no dirtiness line for it.
- **DEV:** two completed jobs.

### E2E-PAYOUT-007 — A rate edited after the take does not change a held job
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /flows/pay-and-payouts#the-path — the take freezes the seat's base, extras and bounds at that cleaner's rates
- **Given** a cleaner who took a job, then an admin (Manager or above) raises that cleaner's rate for the job's service. **When** the cleaner completes the job. **Then** the pay row uses the figures frozen at the take, not the new rate; a job taken after the edit uses the new rate.
- **DEV:** edits a DEV pay config; the test restores the old value afterwards.

### E2E-PAYOUT-008 — A cancellation that collected nothing writes no pay row
- **Priority:** P2
- **Role:** Cleaner
- **Surfaces:** API
- **Rule:** → /flows/pay-and-payouts#the-path — a cancelled order that collected nothing is refused with `payroll.no_collected_fee` and writes nothing
- **Given** a cash booking taken by a cleaner, then cancelled by the customer before any cash was collected and with no fee collected. **When** the cleaner reads My Pay for the open period. **Then** no row exists for that order and the dashboard figure is unchanged. The key `payroll.no_collected_fee` is logged server-side only and is not returned to any user.
- **DEV:** none.

### E2E-PAYOUT-009 — A late cancellation of a paid card order pays the crew half the fee collected
- **Priority:** P2
- **Role:** Cleaner
- **Surfaces:** API · Web
- **Rule:** → /product/business-rules#fee-share — each seat is paid its share of half of what the company collected as the cancellation fee
- **Given** a paid card order with a cleaner on it, cancelled by the customer inside the fee window. **When** the cleaner reads My Pay. **Then** a row of type `CancellationFeeShare` exists with total equal to a seat's share of half the fee still held after refunds, and the invoice line later says it is a share of the fee.
- **DEV:** the cancel itself is the CANCEL journey; this case only reads the pay side.

### E2E-PAYOUT-010 — A confirmed lockout pays each seat its full reward
- **Priority:** P2
- **Role:** Cleaner
- **Surfaces:** API · Web
- **Rule:** → /product/business-rules#lockout-pay — a confirmed lockout pays each seat what the completed job would have paid it
- **Given** a lockout reported by the cleaner and confirmed by an admin. **When** the cleaner reads My Pay. **Then** a `LockoutFeeShare` row equal to the figures frozen at the take exists, whether or not the customer ever pays.
- **DEV:** the lockout is the CANCEL journey; it needs a job 15 minutes past its start with an entrance photo.

### E2E-PAYOUT-011 — A dispute charge on a job not yet invoiced is a deduction on the row
- **Priority:** P2
- **Role:** Cleaner
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /flows/pay-and-payouts#edge-cases — the resolution may charge the cleaner's pay row; My Pay shows the deduction with its reason
- **Given** a dispute resolved by an admin with `chargeToCleaner`, on a job whose pay is not yet invoiced. **When** the cleaner opens the row. **Then** the row carries a deduction, its `DeductionReason` is shown beside it, and `totalPay` is reduced by the charge but never below 0.
- **DEV:** the dispute is the CANCEL journey.

### E2E-PAYOUT-012 — A dispute charge on an invoiced job goes on the next invoice
- **Priority:** P2
- **Role:** Cleaner
- **Surfaces:** API · Web
- **Rule:** → /flows/pay-and-payouts#dispute-charge — the invoice is never rewritten; a line of its own lands in the open period and the next invoice deducts it
- **Given** an invoiced job and an open period, then a dispute resolved with a charge to the cleaner. **When** the next invoice of that cleaner is issued. **Then** the earlier invoice is unchanged, the new invoice's deduction includes the charge, and the PDF shows it in the deduction and not as a job line. With no open period the resolution is refused.
- **DEV:** needs a second period cycle; schedule it with Close now.

## Periods

### E2E-PAYOUT-013 — Only one period is open, and it has the configured length
- **Priority:** P1
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /flows/pay-and-payouts#periods — one open period per company; its end is its start plus the length minus one
- **Given** an Accountant signed in to the admin web. **When** the admin lists pay periods (`GET api/AdminPayPeriod/get-paged`, page `/pay-periods`). **Then** exactly one row is `Open`; on DEV its end date is its start date plus 2 days; every other row is `Closed` or `Paid`; the heading of its details page names the first and last day in the admin's language.
- **DEV:** none.

### E2E-PAYOUT-014 — The night run closes the period the night after its end date
- **Priority:** P1
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /decisions/adr-0082#d1-—-r1-a-period-closes-by-itself-early-close-is-an-exception-no-period-is-reopened — the 02:00 UTC run closes every open period whose end is before today and opens the successor in the same commit
- **Given** an open period whose end date is today (UTC). **When** the 02:00 UTC run has passed. **Then** the period is `Closed`, `Closed By` reads *Automatically*, and a new `Open` period starts the day after the old end and lasts 3 days.
- **DEV:** waits for the next 02:00 UTC run; the period must be left to reach its end date naturally or be shortened with PAYOUT-021 so that the end falls on the day of the test.

### E2E-PAYOUT-015 — The night close invoices each cleaner who has pay, and only them
- **Priority:** P1
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /flows/pay-and-payouts#one-invoice-per-currency — a cleaner with no pay in the period gets no invoice and no e-mail
- **Given** PAYOUT-014 with two cleaners, one with a completed job in the period and one with none. **When** the invoice list is filtered to that period (`/invoice-management?payPeriodId=…`) up to 20 minutes after the close. **Then** the first cleaner has one invoice per currency (one on DEV), the second has none, and the period details count the pay items as invoiced.
- **DEV:** the safety net picks up any cleaner the night run skipped, 15 to 20 minutes after the close.

### E2E-PAYOUT-016 — Close now ends the period today and opens the next from tomorrow
- **Priority:** P1
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /admin-app/pay-periods#close-now — an Accountant ends the open period today with a required reason; the next period starts tomorrow
- **Given** an open period whose start is today or earlier and an Accountant. **When** the admin presses Close now on the period's details page with a reason (`POST api/AdminPayPeriod/close`). **Then** the toast "Pay period closed successfully" shows, the period is `Closed` with its end date set to today (UTC) unless it had ended earlier, `Closed By` is the admin's e-mail, a new `Open` period starts tomorrow and runs 3 days, and the audit log holds a row with the old end, the new end and the reason (never on the period's notes).
- **DEV:** this is the fast way to a closed period; the invoices follow in 15 to 20 minutes (PAYOUT-017).

### E2E-PAYOUT-017 — Invoices follow Close now within about 20 minutes
- **Priority:** P1
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /flows/pay-and-payouts#one-invoice-per-currency — the five-minute sweep invoices every closed period whose close is older than 15 minutes, whoever closed it
- **Given** PAYOUT-016 and a cleaner with pay in the closed period. **When** the invoice list is read at 25 minutes after the close. **Then** that cleaner has a `Pending` invoice in the period, and the cleaner received the period-closed notification in their e-mail (not readable on DEV).
- **DEV:** wait 15 to 20 minutes; do not call it a defect before 25.

### E2E-PAYOUT-018 — Close now needs a reason of at most 500 characters
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /admin-app/pay-periods#close-now — the reason is required and capped at 500 characters
- **Given** an open period and an Accountant. **When** the admin sends Close now with an empty reason, then with 501 characters. **Then** the first is refused `common.required`, the second `common.max_length`, and the period is still `Open`; on the web the button stays disabled until a reason is typed.
- **DEV:** none.

### E2E-PAYOUT-019 — Close now refuses a closed period, an unknown id and a period that has not started
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** API
- **Rule:** → /admin-app/pay-periods#close-now — only an `Open` period whose start is today or earlier can be closed
- **Given** a closed period, a made-up id, and the successor period that Close now opened for tomorrow. **When** the admin sends Close now for each. **Then** the answers are `payroll.pay_period.not_open`, `payroll.pay_period.not_found` and `pay_period.not_started` in that order, and nothing changes.
- **DEV:** the third needs PAYOUT-016 first; the web hides the button for a period that has not started.

### E2E-PAYOUT-020 — Close now is refused to Support
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /decisions/adr-0082#d5-—-the-permission-map — `CanClosePayPeriod` and `CanUpdatePayPeriod` keep the Accountant-and-above permission
- **Given** an open period and an admin whose role is Support. **When** the admin calls Close now and Change end date. **Then** both answer 403, the web does not offer the buttons, and an Accountant or Manager can do both.
- **DEV:** needs a Support admin created on DEV by the Administrator.

### E2E-PAYOUT-021 — Change end date shortens the open period
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /admin-app/pay-periods#change-end-date — the end moves to any day from tomorrow to the start plus the length minus one; the start stays
- **Given** an open period of 3 days that started today or earlier, whose end is at least two days away. **When** the admin sets the end to tomorrow with the unchanged start (`PUT api/AdminPayPeriod/update`). **Then** the period keeps its start, the end is tomorrow, the audit log holds the old and the new end, and the period closes in the night after the new end.
- **DEV:** on DEV the last allowed day equals the default end, so only a shortening is possible.

### E2E-PAYOUT-022 — Change end date refuses today, a past day and anything past the last allowed day
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /admin-app/pay-periods#change-end-date — an end today or earlier points to Close now; a later end than start + length − 1 is refused
- **Given** an open period. **When** the admin sends an end of today, then of yesterday, then of start + 3 days. **Then** the first two are refused `pay_period.end_too_soon` and the third `pay_period.invalid_duration`; the period is unchanged.
- **DEV:** none.

### E2E-PAYOUT-023 — Change end date keeps the start, and refuses an end before the start
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** API
- **Rule:** → /product/business-rules#pay-periods — a different start is refused with `validation.invalid_date`
- **Given** an open period. **When** the admin sends a start different from the stored one, then an end equal to the start. **Then** both are refused `validation.invalid_date` and the period is unchanged.
- **DEV:** none.

### E2E-PAYOUT-024 — Change end date is refused on a closed period
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /flows/pay-and-payouts#periods — Change end date requires the period to be `Open`
- **Given** a closed period. **When** the admin sends Change end date. **Then** the answer is `payroll.pay_period.not_open`, and the web does not offer the panel on a closed period.
- **DEV:** none.

### E2E-PAYOUT-025 — A new period cannot be created while one is open, or in the future
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /admin-app/pay-periods#create-new-pay-period — Create works only when no period is open and refuses a start after today
- **Given** an open period. **When** the admin sends Create (`POST api/AdminPayPeriod/create`) with a start of today, then with a start of tomorrow. **Then** the first is refused `company.has_open_pay_period`; the second answers with a validation error that includes `validation.invalid_date`; on the web the "Create New Pay Period" button is absent while a period is open.
- **DEV:** none.

### E2E-PAYOUT-026 — Create opens a period when none is open
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /admin-app/pay-periods#create-new-pay-period — the dialog takes a start only; the backend sets the end at the configured length
- **Given** no open period (after PAYOUT-028 deleted an empty open period). **When** the admin creates one with the day after the latest period's end. **Then** the new period is `Open`, ends 2 days after its start, and a start that overlaps an earlier period is refused `pay_period.overlapping_period`.
- **DEV:** state-changing; run it right after PAYOUT-028 so that pay in flight does not open a period by itself first.

### E2E-PAYOUT-027 — A closed period is never reopened
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /decisions/adr-0082#d5-—-the-permission-map — `CanOpenPayPeriod` and `CanReopenPayPeriod` are deleted with their endpoints
- **Given** a closed period. **When** the admin tries `POST api/AdminPayPeriod/reopen` and `POST api/AdminPayPeriod/open`, and looks at the closed period's page. **Then** both calls answer 404 or 405, and the page offers no Reopen and no Open, only Mark Period as Paid when applicable.
- **DEV:** none.

### E2E-PAYOUT-028 — Delete works only on an open period with no pay
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** API
- **Rule:** → /flows/pay-and-payouts#periods — Delete requires an `Open` period
- **Given** a closed period, and an open period that holds pay rows. **When** the admin calls `DELETE api/AdminPayPeriod/delete/{id}` for each. **Then** the first is refused `payroll.pay_period.not_open`, the second `pay_period.has_order_pays`; an open period with no pay is deleted. The admin web has no Delete button (the endpoint is API only).
- **DEV:** deleting an empty open period leaves none open, so PAYOUT-026 must follow at once.

### E2E-PAYOUT-029 — Mark period as paid is refused until every row is invoiced and every invoice is settled
- **Priority:** P1
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /flows/pay-and-payouts#periods — Mark paid requires `Closed`, every pay row invoiced and every invoice `Paid` or `Cancelled`
- **Given** an open period, a closed period with an uninvoiced pay row, and a closed period with a `Pending` invoice. **When** the admin sends Mark paid (`POST api/AdminPayPeriod/mark-paid`) for each. **Then** the answers are `pay_period.not_closed`, `pay_period.has_uninvoiced_pay` and `pay_period.has_unpaid_invoices`; the web button is disabled with a one-line reason.
- **DEV:** the second needs a pay row the sweep has not yet invoiced (a cleaner with only a dispute-charge line that does not fit), unsure it can be built; run the other two.

### E2E-PAYOUT-030 — Mark period as paid closes the cycle
- **Priority:** P1
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /admin-app/pay-periods#mark-period-as-paid — the period becomes `Paid` once every invoice is `Paid` or `Cancelled`
- **Given** a closed period whose invoices are all `Paid` (PAYOUT-043) or `Cancelled`. **When** the admin presses Mark Period as Paid. **Then** the status is `Paid`, `Paid At` is set, and a later pay for the same dates lands in the open period, not here.
- **DEV:** the last step of the money journey.

### E2E-PAYOUT-031 — The cleaner sees the periods and My Pay opens on the open one
- **Priority:** P2
- **Role:** Cleaner
- **Surfaces:** API · Web
- **Rule:** → /flows/pay-and-payouts#my-pay-shows-one-currency — My Pay opens on the open period, the newest only when none is open; periods are named by their first and last day in the partner's language
- **Given** a cleaner with at least one closed period. **When** the cleaner opens My Pay on the partner web (`GET api/PayPeriod/GetPagedPayPeriods` feeds the picker), then switches the app language from English to Czech. **Then** the picker starts on the open period, each period is named by two dates, and the names are written again in Czech after the switch.
- **DEV:** the mobile apps have no period picker in the tree (they open the period from an invoice or the earnings screen); unsure, record what the screen offers.

## Invoices

### E2E-PAYOUT-032 — A closed period issues a numbered, pending invoice from the pay rows
- **Priority:** P1
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /flows/pay-and-payouts#one-invoice-per-currency — the invoice's currency is derived from its pay rows; one invoice per employee, period and currency
- **Given** PAYOUT-015 or PAYOUT-017. **When** the admin opens the cleaner's invoice (`GET api/AdminInvoice/details/{id}`, page `/invoice-management/:id`). **Then** status is `Pending`; `currencyCode` is the order currency; `subTotal` equals the sum of the period's row totals for that cleaner; there is exactly one non-cancelled invoice per cleaner, period and currency.
- **DEV:** none.

### E2E-PAYOUT-033 — Invoice number and variable symbol follow the company's series
- **Priority:** P1
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /product/business-rules#payout-numbering — `INV-YYYY-NNNNNN` and a ten-digit variable symbol, one series per company per year
- **Given** two invoices of the same run. **When** the admin reads both. **Then** each number matches `INV-<year>-` plus six digits, each variable symbol is ten digits with no leading zero, and the numbers and the symbols are all different (gaps allowed, duplicates never).
- **DEV:** none.

### E2E-PAYOUT-034 — Generate invoice is refused for an open period and a pair with nothing to invoice
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** API
- **Rule:** → /flows/pay-and-payouts#one-invoice-per-currency — `GenerateInvoice` refuses an open period before any reference is claimed
- **Given** an open period, and a closed period whose pay of one cleaner is already invoiced. **When** the admin sends `POST api/AdminPayroll/generate-invoice` for the cleaner and each period. **Then** the open period answers `pay_period.not_closed`; the invoiced pair answers `payroll.no_unpaid_order_pays` or `payroll.invoice.already_exists`; no invoice or number is created.
- **DEV:** an unknown employee id answers `employee.not_found`.

### E2E-PAYOUT-035 — The cleaner reads the invoice on every surface
- **Priority:** P1
- **Role:** Cleaner
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /partner-app/invoicing#invoice-list — the list shows number, period, total, cash set off, bank transfer and status; the detail lists the earnings breakdown
- **Given** PAYOUT-032. **When** the cleaner opens Invoices on the partner web, or Pay and Earnings on iOS and Android. **Then** the invoice is listed with the number the admin sees, `Pending`, the same total, and its lines match the pay rows; the web list has the Cash Set Off and Bank Transfer to You columns.
- **DEV:** none.

### E2E-PAYOUT-036 — The invoice PDF downloads for the admin and for the cleaner
- **Priority:** P1
- **Role:** Cleaner
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /partner-app/invoicing#pdf-download — the PDF is stored per period and invoice and the owner can download it
- **Given** PAYOUT-032. **When** the cleaner (`GET api/EmployeePayroll/DownloadInvoice/{id}`) and the Accountant (`GET api/AdminInvoice/download/{id}`) download the PDF. **Then** both answer `application/pdf`; the text contains the invoice number, the variable symbol, the cleaner as supplier and the total; the cleaner's copy and the admin's copy are the same document.
- **DEV:** the Android and iOS apps open it from the invoice detail.

### E2E-PAYOUT-037 — A cleaner cannot read another cleaner's invoice
- **Priority:** P1
- **Role:** Cleaner
- **Surfaces:** API
- **Rule:** → /flows/pay-and-payouts#one-invoice-per-currency — a foreign or nonexistent invoice answers the same not-found
- **Given** invoices of cleaner A and a second signed-in cleaner B. **When** B calls `GetInvoiceById/{A's id}` and `DownloadInvoice/{A's id}`, and then the same with a made-up id. **Then** all four answer `payroll.invoice.not_found`; B's list never contains A's invoice.
- **DEV:** the second cleaner is a run account.

### E2E-PAYOUT-038 — A PDF that is not ready answers a not-ready key, and the admin can render it again
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /flows/pay-and-payouts#one-invoice-per-currency — Regenerate PDF is offered on `Pending`, `Approved` and `Disputed` invoices only
- **Given** a `Pending` invoice, a `Paid` one and a `Cancelled` one. **When** the admin calls `POST api/AdminInvoice/regenerate-pdf` with a language code each, and once with a code that does not exist. **Then** the pending invoice is rendered again and the PDF downloads; the paid one is refused `payroll.invoice.already_paid`, the cancelled one `payroll.invoice.already_cancelled`, the unknown language `language.not_found`.
- **DEV:** `payroll.invoice.pdf_not_ready` needs the window between the night commit and the PDF stamp, which cannot be forced (see the last section).

### E2E-PAYOUT-039 — Approve moves a pending invoice to approved
- **Priority:** P1
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /flows/pay-and-payouts#approval-is-the-last-refusal — Approve requires `Pending` or `Disputed`, a usable payout record and a matching account currency
- **Given** a `Pending` invoice of a cleaner who has provided payout details in the work country's currency. **When** an Accountant presses Approve (`PUT api/AdminInvoice/approve`). **Then** the status is `Approved`, the cleaner sees `Approved`, and the audit log holds the approval.
- **DEV:** the E2E cleaner must have saved payout details in registration or the profile.

### E2E-PAYOUT-040 — Approve is refused when the payout account holds another currency
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /flows/pay-and-payouts#approval-is-the-last-refusal — a mismatch is refused as `payroll.invoice.payout_currency_mismatch`, then fixed by the cleaner
- **Given** a `Pending` CZK invoice and a cleaner who declared an account that holds EUR. **When** the Accountant presses Approve. **Then** the answer is `payroll.invoice.payout_currency_mismatch` and the invoice stays `Pending`; once the cleaner declares CZK (or clears the declaration) the same approval succeeds.
- **DEV:** the declaration is saved by the cleaner in the payout details form of the partner web, Android or iOS.

### E2E-PAYOUT-041 — Approve is refused without a usable payout record
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** API
- **Rule:** → /flows/pay-and-payouts#approval-is-the-last-refusal — the presence rule reads `EmployeePayoutDetails` and is checked before the currency
- **Given** an invoice of a cleaner with pay rows and no payout record. **When** the Accountant approves. **Then** the answer is `payroll.invoice.payout_details_missing`, never the currency key.
- **DEV:** the state is reached only by an admin reassigning an order onto an incomplete cleaner or by GDPR erasure of the record; unsure it can be built on DEV, otherwise report as not run.

### E2E-PAYOUT-042 — Approve is refused on any other status
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** API
- **Rule:** → /flows/pay-and-payouts#approval-is-the-last-refusal — only `Pending` and `Disputed` can be approved
- **Given** an `Approved`, a `Paid` and a `Cancelled` invoice, and an unknown id. **When** the Accountant approves each. **Then** the first three answer `payroll.invoice.invalid_status` and the unknown id `payroll.invoice.not_found`.
- **DEV:** none.

### E2E-PAYOUT-043 — Mark paid records the transfer and is final
- **Priority:** P1
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /flows/pay-and-payouts#approval-is-the-last-refusal — Mark paid requires `Approved`; `Paid` is terminal
- **Given** an `Approved` invoice. **When** an Accountant presses Mark paid with a bank transfer note (`PUT api/AdminInvoice/mark-paid`). **Then** the status is `Paid`, the note and the actor are recorded, the cleaner sees `Paid`, and nothing leaves the platform (no Stripe call, no bank call).
- **DEV:** none.

### E2E-PAYOUT-044 — Mark paid is refused on every other status and on an over-long note
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** API
- **Rule:** → /flows/pay-and-payouts#approval-is-the-last-refusal — a second mark is refused rather than overwriting the first actor's record
- **Given** a `Pending`, a `Paid` and a `Cancelled` invoice, and an `Approved` one. **When** the Accountant marks each paid, and the approved one with a note of 501 characters, then with admin notes of 1001. **Then** the answers are `payroll.invoice.not_approved`, `payroll.invoice.already_paid`, `payroll.invoice.already_cancelled`, then `common.max_length` twice.
- **DEV:** none.

### E2E-PAYOUT-045 — The Mark paid confirmation shows what to transfer before it sends anything
- **Priority:** P1
- **Role:** Admin
- **Surfaces:** Web
- **Rule:** → /flows/pay-and-payouts#approval-is-the-last-refusal — the confirm shows the amount to transfer, the variable symbol, the constant symbol and the masked account
- **Given** an `Approved` CZ invoice. **When** the admin presses Mark paid on the invoice page. **Then** an in-page confirm lists the amount (the transfer amount, not the total), the variable symbol, the constant symbol `0308` and the masked account, with a note field; nothing is sent and the invoice stays `Approved` until the confirm button is pressed; the invoice detail shows the same constant symbol.
- **DEV:** none.

### E2E-PAYOUT-046 — Cancel an invoice and a corrected one follows
- **Priority:** P1
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /flows/pay-and-payouts#one-invoice-per-currency — a cancelled invoice frees its pay rows; the sweep issues a corrected invoice with a new number
- **Given** a `Pending` or `Approved` invoice. **When** an Accountant cancels it with a reason (`PUT api/AdminInvoice/cancel`). **Then** the invoice is `Cancelled`; within about 10 minutes a new `Pending` invoice for the same cleaner, period and currency exists with the same jobs, a new invoice number and a new variable symbol; the cleaner's My Pay links the new one; the cancelled invoice's detail lists no jobs.
- **DEV:** the period is already closed so the next five-minute sweep tick reissues; wait up to 10 minutes.

### E2E-PAYOUT-047 — Cancel is refused without a reason, with a too long one, on a paid invoice and twice
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** API
- **Rule:** → /flows/pay-and-payouts#approval-is-the-last-refusal — Cancel is offered on `Pending`, `Approved` and `Disputed`; a paid invoice is marked paid instead
- **Given** a `Pending`, a `Paid` and a `Cancelled` invoice. **When** the Accountant cancels with an empty reason, with 1001 characters, then the paid and the cancelled invoice with a reason. **Then** the answers are `common.required`, `dispute.max_length_exceeded` (the key the validator emits for this length rule), `payroll.invoice.cannot_cancel_paid` and `payroll.invoice.already_cancelled`.
- **DEV:** the triager decides whether a payroll refusal speaking of a dispute is a defect or by design; the key is taken from the code, not from the docs.

### E2E-PAYOUT-048 — Adjust amounts re-renders the PDF and works only before approval
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /flows/pay-and-payouts#approval-is-the-last-refusal — Adjust works on `Pending` and `Disputed` only
- **Given** a `Pending` invoice. **When** the Accountant sets a bonus of 50 and a deduction of 20 (`PUT api/AdminPayroll/update-invoice-amounts`), then repeats it on an `Approved`, a `Paid` invoice, and with a bonus of -1. **Then** the total moves by +30, the PDF shows the new amounts, and the refusals are `payroll.invoice.invalid_status`, `payroll.invoice.already_paid` and `validation.must_be_positive`.
- **DEV:** none.

### E2E-PAYOUT-049 — A bonus or deduction must be set again on the corrected invoice
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /flows/pay-and-payouts#approval-is-the-last-refusal — the corrected invoice is rebuilt from the order payments
- **Given** an invoice with a bonus set through Adjust, then cancelled (PAYOUT-046). **When** the corrected invoice is issued. **Then** its bonus is 0 and its total is the sum of the rows; setting the bonus again works.
- **DEV:** none.

### E2E-PAYOUT-050 — A disputed invoice can be adjusted and then approved
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /flows/pay-and-payouts#approval-is-the-last-refusal — a disputed invoice is approved after it is adjusted
- **Given** a `Pending` invoice. **When** the Accountant disputes it with a reason (`PUT api/AdminPayroll/dispute-invoice`), adjusts it, then approves it. **Then** the status goes `Disputed`, stays `Disputed` after the adjustment, then `Approved`; an empty reason is `common.required` and 1001 characters `common.max_length`; disputing a `Paid` invoice is `payroll.invoice.already_paid` and a `Cancelled` one `payroll.invoice.already_cancelled`.
- **DEV:** the partner doc says the partner disputes the invoice, but the only route in the tree is the admin one; unsure, the triager checks the partner apps for a dispute button and reports the doc if there is none.

### E2E-PAYOUT-051 — There is no Reject on an invoice
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /decisions/adr-0082#d5-—-the-permission-map — `CanRejectInvoice` is deleted with the Reject action and its endpoint
- **Given** a `Pending` invoice. **When** the admin looks for Reject on the invoice page and calls `PUT api/AdminInvoice/reject`. **Then** the page has no such action and the call answers 404 or 405; no invoice can reach `Rejected`.
- **DEV:** none.

### E2E-PAYOUT-052 — The payroll actions are refused to Support and open to Accountant
- **Priority:** P1
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /architecture/security-rules#administrator-roles-adr-0066-accepted-2026-09-19 — approve, mark paid, cancel, adjust, dispute, generate and the pay-period reads are `AccountantOrAbove`
- **Given** a Support admin and an Accountant admin. **When** each calls the pay-period list, the invoice list, Approve, Mark paid, Cancel, Adjust and Generate. **Then** every call by Support answers 403 and the web hides those pages; every call by the Accountant passes the role check (a business refusal is fine, a 403 is not).
- **DEV:** needs one Support and one Accountant admin created by the Administrator.

### E2E-PAYOUT-053 — The admin invoice list filters by period and status
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /admin-app/pay-periods#invoices — "View invoices" opens the list filtered to the period; the period details count all, unpaid and uninvoiced
- **Given** a period with a `Pending` and a `Paid` invoice. **When** the admin opens the period, presses View invoices, and filters by status. **Then** the list shows only that period's invoices, the status filter narrows it, and the period details count equal the list.
- **DEV:** none.

## Cash held and the set-off

### E2E-PAYOUT-054 — Cash a cleaner collected is held, and both sides see it
- **Priority:** P1
- **Role:** Cleaner
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /product/business-rules#cash-held — a cash collection enters the ledger as cash the cleaner holds for the company, one entry per order
- **Given** a cash job completed with the cash marked collected (the EXEC journey). **When** the cleaner opens My Pay (`GET api/EmployeePayroll/GetCashHeld`) and the Accountant opens Cash held (`/pay-periods/cash-held`, `GET api/AdminCashHeld/get-all`). **Then** the cleaner sees *cash I hold* in the order's currency equal to the amount due (price less credit applied), with the subtitle that it is set off at the next invoice; the admin list shows the cleaner with the same balance.
- **DEV:** none.

### E2E-PAYOUT-055 — The invoice sets cash off up to its total
- **Priority:** P1
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /flows/pay-and-payouts#cash-set-off — `TransferAmount = TotalAmount - CashSetOffAmount`; the invoice's own amounts do not change
- **Given** a cleaner who holds cash of C and an invoice whose total is T in the same currency (on DEV a cash job's price is above the cleaner's pay, so C is usually larger than T). **When** the invoice is issued and the admin reads it. **Then** `cashSetOffAmount = min(C, T)`, `transferAmount = T - cashSetOffAmount`, `totalAmount` is still T, the cleaner's balance falls by the set-off, and the amount C - T is carried forward when C is larger.
- **DEV:** run once with C larger than T (one cash job) and, if time allows, once with C smaller (a card job in the same period).

### E2E-PAYOUT-056 — Every invoice screen shows the set-off and the transfer
- **Priority:** P1
- **Role:** Cleaner
- **Surfaces:** Web · iOS · Android
- **Rule:** → /flows/pay-and-payouts#cash-set-off — the lists and details show the set-off as a minus and the bank transfer in bold when cash was set off
- **Given** PAYOUT-055. **When** the admin web list and detail, the partner web list and detail, and the Android and iOS details are opened. **Then** each shows the cash set off (as a minus) and the amount to transfer under the total; an invoice with no set-off ends at the total.
- **DEV:** none.

### E2E-PAYOUT-057 — The PDF prints the set-off statement in the cleaner's language
- **Priority:** P1
- **Role:** Cleaner
- **Surfaces:** API · Web
- **Rule:** → /flows/pay-and-payouts#cash-set-off — the statement below the summary is in the cleaner's language (en, cs, sk, uk, ru); the invoice itself stays in its jurisdiction's
- **Given** a cleaner with a set-off invoice, whose account language is each of the five in turn. **When** the invoice PDF is downloaded after a regenerate. **Then** the statement lists the invoice total, the cash set off, the transfer, and that the amount is unchanged and the cash still held is carried forward, written in that language; the rest of the document stays in the jurisdiction's language (Czech for CZ); a second regenerate reads the same language.
- **DEV:** whether the admin's language code sent with Regenerate PDF overrides the cleaner's is unsure; the triager records the observed behaviour against this rule.

### E2E-PAYOUT-058 — Cancelling an invoice gives its set-off back, and the corrected one takes it again
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /flows/pay-and-payouts#cash-set-off — a cancelled invoice enters its whole set-off back and unassigns its pay rows
- **Given** an invoice with a set-off S. **When** the Accountant cancels it. **Then** the cleaner's cash balance rises by S at once, and after the corrected invoice is issued it shows `cashSetOffAmount` S again and the balance falls by S.
- **DEV:** none.

### E2E-PAYOUT-059 — Lowering an invoice below its set-off releases the difference
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** API
- **Rule:** → /flows/pay-and-payouts#cash-set-off — `TransferAmount` never goes negative
- **Given** a `Pending` invoice with total T and set-off S equal to T. **When** the Accountant sets a deduction D. **Then** the new total is T - D, the set-off is now T - D, the cleaner's balance rises by D, and the transfer is 0.
- **DEV:** none.

### E2E-PAYOUT-060 — A remittance lowers the cash a cleaner holds
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /product/business-rules#cash-held — a remittance is a positive amount never above the balance
- **Given** a cleaner holding C. **When** the Accountant records a remittance of R (`POST api/AdminCashHeld/record-remittance`), then of 0, of C + 1, for an unknown cleaner and for an unknown currency, and with a note of 501 characters. **Then** R lowers the balance on both sides, and the refusals are `cash_held.amount_invalid`, `cash_held.amount_exceeds_balance`, `employee.not_found`, `currency.not_found` and `common.max_length`.
- **DEV:** none.

### E2E-PAYOUT-061 — A write-off needs a note and is open to Manager, not to Accountant
- **Priority:** P2
- **Role:** Admin
- **Surfaces:** API · Web
- **Rule:** → /product/business-rules#cash-held — a write-off carries a required note of at most 500 characters and needs Manager or above
- **Given** a cleaner holding C, a Manager and an Accountant. **When** the Manager writes off W with a note, with an empty note, and with a note of 501 characters; the Accountant tries once (`POST api/AdminCashHeld/write-off`). **Then** the first lowers the balance by W and is audited, the others are `common.required` and `common.max_length`, and the Accountant gets 403.
- **DEV:** needs a Manager admin.

### E2E-PAYOUT-062 — Above the float cap a cleaner no longer sees cash jobs
- **Priority:** P2
- **Role:** Cleaner
- **Surfaces:** API · Web · iOS · Android
- **Rule:** → /domain/offerability#cash-float-cap — a cleaner holding strictly more than `cash.float_cap` loses cash jobs from the board; a take is refused `order.cash_float_cap_exceeded`; it lifts by itself
- **Given** the DEV company's `cash.float_cap` set by an admin to a value just below a cleaner's balance, and an open cash job. **When** the cleaner opens the board and tries to take the cash job; an Accountant then records a remittance bringing the balance to the cap. **Then** the cash job is absent from the board, the preview and the count, and the take answers `order.cash_float_cap_exceeded`; card jobs stay; *cash I hold* states the cap and that cash jobs are hidden; after the remittance the cash job reappears with no other action.
- **DEV:** edits a company setting; restore it to 0 afterwards.

## Not testable on DEV

- **Moving money.** The platform never talks to a bank or Stripe for a payout (→ /decisions/adr-0082#d3-—-r3-payouts-are-manual-first-then-the-bank-and-never-stripe). The case set checks that Mark paid records and sends nothing; whether a transfer really left a bank cannot be seen.
- **E-mails.** The period-closed e-mail with the PDF, the period-end reminders (3 and 1 day before the end, only for a cleaner with a job or a fee-share row) and the cash remittance request are e-mails to `@example.com` addresses; no case can read them. The 30-day request also needs a balance older than the DEV data.
- **Two currencies in one period.** Two invoices, two references and two e-mails need pay in two currencies, which is reachable only through an admin reassignment onto an order in another currency; whether DEV serves a second currency is unsure, so it is not run.
- **The 02:00 window.** A job finished between midnight and the 02:00 UTC run lands in the period that has just ended; the timing cannot be forced.
- **The night-run switch.** `PayPeriodClosing:Enabled` is a deploy setting; the claim that the sweep and Close now still invoice with the switch off cannot be tested without redeploying DEV.
- **The race between the night commit and the PDF stamp.** `payroll.invoice.changed_concurrently` and `payroll.invoice.pdf_not_ready` need a concurrent admin action inside a gap of milliseconds.
- **Concurrency of numbering and of the cash lock.** Two invoices claiming a number at once, or a remittance and a set-off taking the same cash at once, are covered by the integration suite on a real Postgres, not by a UI run.
- **A company cannot be archived while a cleaner holds cash** (`company.has_cash_held`): archiving DEV's only company would end the environment.
- **A deactivated company's pay.** The successor period that a deactivated company does not get needs the lifecycle action on the only company.
- **Production period length.** The 14-day length is a production setting; DEV runs 3 days and no case may read it as production behaviour.
