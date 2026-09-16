---
id: T-0765
title: A customer books in any market the holding serves — the order belongs to the market's operator, loyalty and credit follow the account, one holding Stripe account
status: done
size: M
owner: —
created: 2026-09-16
updated: 2026-09-16
depends_on: []
blocks: []
stories: []
adrs: [ADR-0061, ADR-0058, ADR-0062]
layers: [backend, frontend, android, ios]
security_touching: true
manual_steps: []
sprint: —
---

## Body (the Batch 3 ticket as written for the lanes, 2026-09-16)

# Batch 3 — cross-market booking under one holding Stripe account (T-0765)

Branch `fix/remove-membership-free-trial` (PR #255). Owner rulings 2026-09-15: Q-TENANCY-01 "I'd like a
user to be able to book in a market another Cleansia company serves — not limited"; Q-TENANCY-05 "like
Wolt — the user switches market"; Q-TENANCY-01/05 follow-up: "keep one holding company Stripe account
for now, maybe expand in the future". Orchestrator defaults (overrulable): the customer's audit row for an
order-scoped act follows the ORDER's operator and the by-user timeline reads across operators pinned by
the user id; loyalty and credit follow the ACCOUNT; recurring templates stamp from the address's market;
the market's admin sees a read-only "customer of <company>" panel for a customer of another company.

Shared facts (read-only tenancy scout 2026-09-15, file:line at HEAD then; verify — the tree wins):
- The refusal today: `CreateOrder.cs:182-185` rule `AddressCountryIsOperatedByAmbientTenantAsync` →
  `order.country_operator_mismatch` (`BusinessErrorMessage.cs:95`), unconditional (guest and signed-in);
  body `:438-452` compares `resolution.OperatorTenantId` to `_tenantProvider.GetCurrentTenantId()` —
  for a signed-in customer the JWT `tenant_id` claim = the ACCOUNT's operator; for a guest the override
  the scope behaviour set from the address. `OperatorTenantScopeBehavior.cs:30-47` steps aside when a
  claim exists. `CreateRecurringBooking` has NO operator rule; `MaterializeRecurringBookingTemplate.cs:110-112`
  stamps each generated order with `template.TenantId`.
- Stamping: `CleansiaDbContext.cs:75-95` stamps Added rows with the ambient tenant at commit; the D4
  idiom for a claim-bearing request that must write for another operator is an explicit
  `tenantProvider.SetTenantOverride(...)` in the handler (`TokenService.cs:44-47`).
- Readers that assume account-tenant == order-tenant (all filtered through `GetDbSet()`,
  `BaseRepository.cs:150-158`): `GetCustomerOrders.cs:29-56`; every by-id customer action via
  `OrderRepository.GetByIdAsync` (`OrderRepository.cs:178-180`) then `OrderAccessService.CanAccessOrderAsync`
  (`Authentication/OrderAccessService.cs:39-63`, `order.UserId == userId`) — CancelOrder,
  GetCancellationFeePreview, DownloadOrderReceipt, CreatePaymentIntent, ResumeOrderCheckout,
  SubmitOrderReview, RevealOrderAccessInstructions, ChoosePreferredCleaner, DeclinePreferredOffer,
  GetOrderPhotos, GetMyServingCleaners; profile stats `OrderRepository.cs:86-92`; `CreateDispute.cs:112-118`.
- Already cross-operator: `GdprExportService`/`GdprDeletionService` via `SubjectOrders.Of` over
  `GetQueryableIgnoringTenant()` (`Core.Domain/Orders/SubjectOrders.cs`); the incident file's proven-order
  rule; `GetActionTimeline.ByUserAsync` (T-0752 follow-up).
- Money (one Stripe account): `StripeConfig.cs:9` one secret key, `StripeClientFactory.cs:12-15`
  `CreateClient()` takes no tenant; `UserStripeCustomer` is per (user, currency) — right shape for one
  account; receipts issue from the ambient tenant's `CompanyInfo` + `FiscalCounter` (`ReceiptService.cs:43-63`)
  — for an SK-stamped order the webhook re-pins from the row, so SK's issuer and counter: correct;
  refunds from the one account, `Refunds.TenantId` from the order.
- The sharp seams: `LoyaltyAccountRepository.cs:17,32` (`GetDbSet()`), `IX_LoyaltyAccounts_UserId` unique
  with NO tenant term; `CompleteOrder.cs:301` grants under the cleaner's claim (= the order's operator)
  → for a CZ customer's SK order the filtered read finds no account and `EnsureForUserAsync` inserts a
  second row → `23505`. `CreditAccountRepository.cs:16,33` likewise with `IX_CreditAccounts_UserId_CurrencyId`;
  `CancelUnfilledOrders.cs:138-143` credits under the ORDER's tenant. `MembershipBenefitUsage` is written
  on the order path (`CreateOrder.cs:291-346` checks entitlement under the claim; the usage row would be
  stamped with the order's operator). `DbContextAuditWriter.cs:18,24` stamps customer audit rows with the
  claim.
- Admin: admins are stamped with their company (ADR-0061 D5); `GetUser`/`GetPagedUsers` and the order
  detail's customer panel are filtered — the SK admin can open the SK order but not its CZ customer.
- Clients: the market switcher exists on web (`cleansia-market-switcher`), Android (`MarketScreen.kt`),
  iOS (`MarketPickerView`); "my orders" carries `CurrencyId` per row and `GetCustomerOrders.Request.Filter`
  has a `currencyId` filter.
- Batch 1/2 landed since the scout: TenantId FK + `TenantAuditable`; company lifecycle (a deactivated
  company's markets are not serviced — `CountryRepository.IsServicedAsync` carries the operator term).
- Machine rules, conventions and commit rules: as in the previous batches (orchestrator's brief).

---

## T-0765 — A customer books in any market the holding serves; the order belongs to the market's operator · lanes: backend (M, own review) · customer web + Android + iOS (S each, one review) · admin web (S) · docs

**Doing (backend)**
- **Stamp the order with the market's operator.** `CreateOrder.Handler`: after validation, when the
  resolved operator differs from the claim, `SetTenantOverride(resolution.OperatorTenantId)` before
  the order is added (the D4 idiom) so the order and every child row written by override-from-row
  (`OrderStatusHistory`, `OrderEmployeePay`, `Refund`, `Dispute`, receipts…) carry the market's
  operator. The validator's `AddressCountryIsOperatedByAmbientTenantAsync` rule becomes "the address's
  country is a serviced market with an active operator" for a signed-in customer (guests keep today's
  rule — their ambient tenant already IS the market's). `CreateRecurringBooking` / `UpdateRecurringBooking`
  stamp the template from the address's market the same way; the materialiser stamps each occurrence
  from the ADDRESS's market (not `template.TenantId`) — read `MaterializeRecurringBookingTemplate`.
- **Customer reads union by owner.** `OrderRepository` gains owner-pinned variants that read
  `GetQueryableIgnoringTenant()` with `o.UserId == callerId` (S8's permitted pin: the caller's own id
  from their JWT; `SubjectOrders.Of` is the shipped shape): `GetByIdForOwnerAsync`,
  `GetPagedForOwnerAsync` (+ count), `GetCustomerProfileStatsAsync`; the ~12 customer handlers listed
  above switch to them (the by-id ones still re-check `order.UserId == userId` in `OrderAccessService`
  — keep that). `CreateDispute` reads the order the same way and the dispute is stamped with the
  ORDER's operator (override from the row, like the webhook). `DownloadOrderReceipt` reads the receipt
  by the order past the filter, pinned by the order's ownership.
- **Account-shaped rows follow the account.** `LoyaltyAccountRepository` / `CreditAccountRepository`
  `EnsureForUserAsync`/reads: `IgnoreQueryFilters()` pinned by `UserId`, and an explicit
  `TenantId = user.TenantId` on insert (never the ambient order tenant); `CompleteOrder`'s grant and
  `CancelUnfilledOrders`' credit go through them. `MembershipBenefitUsage`: stamped with the
  ACCOUNT's tenant (the membership's) — decide by reading how `UserMembership`/usage are read back;
  pin whichever keeps the usage visible to the entitlement check under the claim.
- **Audit rows.** The customer audit row for an order-scoped act (`customer.order.*`, `customer.dispute.*`)
  is stamped with the ORDER's operator (the override is already ambient at commit — verify the writer
  uses the ambient tenant, not the claim); `GetActionTimeline.ByUserAsync` and the admin list-by-user
  read across operators pinned by the user id (the T-0752 follow-up did the timeline's user arm —
  extend the paged list). ADR-0062 gets the one-sentence rule from the docs lane.
- **Admin.** The market's admin opens the order (its tenant) but the customer belongs to another
  company: `GetOrderDetails` (admin) carries a `CustomerCompany` member (the operator's display name)
  and the admin `GetUser` by id answers a read-only `CustomerOfAnotherCompanyDto` (id, first name,
  masked e-mail, company name, no PII beyond what the order already shows) when the user's tenant
  differs — pinned by `CanViewOrderDetail`; `GetPagedUsers` stays filtered (no cross-company listing).
- **Error keys.** `order.country_operator_mismatch` stays for guests; no new key unless a refusal
  needs one; five locales + parity rosters if one is added.
- **Tests.** Unit: validator arms (signed-in SK address on a CZ account passes; unserviced refused;
  guest keeps the rule); handler stamps with the market's operator. Postgres: a CZ customer books an
  SK address → `Orders.TenantId = cleansia-sk`, receipt from SK's `CompanyInfo` and counter, refund row
  SK, the customer's "my orders" lists it with its currency, order detail/cancel preview/cancel/receipt
  download work, `CompleteOrder` by an SK cleaner grants loyalty to the CZ account's single row (no
  `23505`), `CancelUnfilledOrders` credits the CZ account's EUR credit row stamped CZ, a dispute on the
  SK order is SK-stamped and the customer can read it, the customer audit row for the cancel is
  SK-stamped and the customer's timeline (by user, under a CZ admin) still shows it, the SK admin's
  order detail names the customer's company and the read-only customer panel resolves, the CZ admin's
  list by user shows the SK act; recurring template on an SK address materialises SK-stamped orders;
  the export and erasure still cover the SK order; HostTests for the admin cross-company read (403 for
  a customer, 200 for an admin of either company on THEIR order's customer only).

**Doing (customer web / Android / iOS)** — "my orders" groups or labels rows by market (country name +
currency, from the row's `CountryId`/`CurrencyId`; the market directory is already loaded) and the
order detail shows the market; nothing else changes (the switcher is the browsing context, the
address the booking context). Five locales on web; string resources on Android; xcstrings on iOS.

**Doing (admin web)** — order detail shows "Customer of <company>" and the read-only customer panel
when the DTO says so; the customer link stays on the same-company case only.

**NOT** — no per-company Stripe (recorded on ADR-0061); no cross-company customer listing; no holding
role; no change to memberships' market rule (a subscription follows the account); no change to the
partner apps; no re-prompt or notification.

**Done looks like** — three backend suites green; the Postgres scenario above end to end; web/Android/iOS
order lists show the market; ADR-0061 D6 and ADR-0058 amended, ADR-0062 D7 rule added, business rules
("a customer may book in any serviced market; the order belongs to the market's company; loyalty and
credit belong to the customer"), flows/booking-and-pricing, customer-app overview, backlog row done,
Q-TENANCY-01/05 marked answered as built.

## Implementation checkpoint — 2026-09-16

The backend now resolves a signed-in booking’s operator from its service address, stamps recurring
templates and occurrences from their address, and keeps customer order/dispute/receipt/photo and
schedule reads pinned to the caller across operators. Loyalty, currency-specific credit, membership
usage and promo/referral account records retain the account’s company. Notifications use the
persisted recipient’s account company for the feed, outbox and delivery envelope.

Successful order/dispute audit acts follow the booking operator; the failure sink adopts an operator
only for a proven-owned resource. Foreign or missing probes keep the caller’s account company. The
admin by-user timeline and paged customer-action list join across operators after a filtered user
read proves access to the account. Operator dispute views use the booking identity snapshot for
foreign customers.

The admin customer read is **order-keyed**: `GET api/AdminOrder/{orderId}/customer` under the new
AdminOnly `CanViewOrderCustomer` policy. It proves access to the filtered order before returning a
full same-company `UserItem` or a masked, read-only `CustomerOfAnotherCompanyDto`. The shared order
DTO carries `CustomerCompany`, without adding a customer account id. Guest orders return
`400 order.not_found` for this read, rendered as an explicit empty account state. The six existing
AdminUser management routes remain. Cross-company listing stays closed.

Customer web and Android list/detail market labels and the admin web panel are implemented and
independently reviewed and locally verified. Labels resolve the order’s country from the
full directory and use the order’s currency. The detail contract’s missing `CountryId`, caught by
the web tests, was added in `a0e9d7eb` and all five contracts were regenerated.

**Evidence already complete:** full backend suites passed — 5,827 unit, 486 integration and 296 host
tests — before the final additive detail-country correction. Three web clients and both mobile
OpenAPI specifications were regenerated. The final detail-country correction passed the solution
build, 105 affected unit tests, the cross-market PostgreSQL scenario and all three web app typechecks.
All 336 targeted web tests passed. The full web pass covered 2,591 tests across 73 projects; its one
stale error-provenance expectation was corrected and all 48 tests in that app passed on rerun.
All 76 lint projects and all three production builds passed. Android customer tests passed 1,079/1,079
and partner tests passed 579/579, with 53 tasks executed in each run and caches disabled. Gradle was stopped. There is no
schema change, Initial regeneration, deployed DEV database drop, SQL or deployment in this batch.

The repository checks passed. Two stale self-test fixtures were repaired without relaxing either
production checker: offerability now exercises the four-status floor (12 scenarios passed), and
module-boundary ratchet scenarios inject their retired baseline while separately testing the shipped
empty baseline (23 scenarios passed). The docs build passed. The code graph rebuilt with one worker:
79,985 nodes and 196,473 edges; its optional SQL parser was absent (18 files skipped), and eight files
had parser limitations. These graph limitations do not replace compiler or test results.

**Separate build finding:** Angular route extraction bootstraps the customer market initializer
without an incoming HTTP request, and the production build configuration names the production API.
The first customer build ran before this was discovered and may have attempted a production read;
no network trace was captured to establish whether it reached the server. No production query was
made to investigate it. `6c873a4e` now skips market loading in request-less server bootstrap while
preserving normal SSR and browser loading. Its 57-test store suite and lint passed; a repeat
production build passed with application API connections blocked and zero such attempts recorded
(only the Google Fonts build asset host was allowed).

**Still open:** iOS order market labels are not implemented. The handover’s requirement to use only
members already present in a committed Swift client conflicts with the repository’s generated Swift
clients being gitignored and created from committed OpenAPI specifications in CI. The requested
owner clarification is pending; the Mac regeneration/Xcode session remains parked. T-0765 stays
`in_progress` until the remaining client work and verification are complete.

The documentation now records the implemented split between account and operator ownership in
ADR-0058, ADR-0061, ADR-0062, business rules, booking flow and customer web overview. The ADR-0061
ruling table also corrects its stale Q-TENANCY-03 claim: Batch 2’s lifecycle is already implemented
under ADR-0064.
