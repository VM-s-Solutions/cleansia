# Role — CompanyLifecycle (the four acts, the sign-in gate, the sweep) (ADR-0064, accepted 2026-09-16) (CRC card)

> Introduced by **ADR-0064** (`docs/decisions/adr-0064.md`, **`accepted`** 2026-09-16; owner ruling
> Q-TENANCY-03, 2026-09-15: *"I'd build up to (c). Archive is also a good functionality to introduce in
> the beginning."*). Shipped as T-0760 (deactivate / reactivate, the serviced predicate, the sign-in gate),
> T-0762 (wind-down) and T-0764 (the admin page). The files that are the role:
> `Core.AppServices/Features/CompanyLifecycle/{DeactivateCompany,ReactivateCompany,WindDownCompany,
> GetCompanyLifecycle,CompanyWindDownDispatch,CompanyLifecycleSnapshot}.cs` ·
> `Core.AppServices/Tenancy/CompanySignInGate.cs` · `Core.AppServices/Services/CompanyWindDownService.cs` ·
> `Functions.Core/Handlers/CompanyWindDownHandler.cs` (+ `CompanyWindDownFunction` and its poison twin) ·
> `Web.Admin/Controllers/AdminCompanyLifecycleController.cs` · the admin web page
> `libs/cleansia-admin-features/company-lifecycle`. The archive half has its own card →
> [Company archive](./company-archive).

## Responsibility (one sentence)

Move **the admin's own operating company** through announce → close → seal — tell every customer and
cleaner by e-mail, cancel and refund in full every open booking on or after the last day, pause the
schedules, end every Plus at period end, discharge unspent credit and invoice the last pay period once
the door is closed; delist its markets everywhere at once and refuse its cleaners on the partner apps;
then hand a settled company to the archive — idempotently, from the admin app, never deleting a row and
never naming another company.

## Collaborators

- **`Tenant`** ([card](./tenant)) — the state and the stamps; every act is one transition method on the
  row, refused when frozen. `CompanyLifecycleState` is the highest that applies.
- **`CountryRepository.GetServicedAsync` / `IsServicedAsync`** — the fourth market predicate: a country is
  serviced only while `IsServiced && IsActive` **and** its configuration names an operator whose
  `Tenant.IsActive` is true. `OperatorTenantResolver` asks `IsServicedAsync`, so the directory
  (`Market/GetOverview`), the wizard picker (`Country/GetServiced`), the anonymous scope behaviour, the
  address resolver, the quotes, the catalogue overviews, the plans, the two Plus writers, the work-country
  rules, `CreateRecurringBooking` and `AddSavedAddress` all answer "closed" from the same read —
  `country.not_serviced`, the user-input key. `SetCountryServiced(true)` refuses a country whose operator
  is deactivated (`country.market_not_ready`); `SetDefaultMarket` refuses its market (`country.not_serviced`).
- **`ICompanySignInGate.RefusalForAsync(user, audience)`** — scoped, memoised per request; for the
  `Employee` profile on the Partner and Mobile audiences only, one `Tenants` read, `auth.company_deactivated`
  when `IsDeactivated`. Called by `PartnerLogin`, `MobilePartnerLogin`, `GoogleAuth` / `AppleAuth` (the
  existing-staff branch on a partner host) and `RefreshToken` (partner audiences), and enforced again in
  `TokenService.GenerateTokenAsync` beside the audience/profile invariant through the same instance.
  Customers never pay the read (profile first); administrators are not refused (O-1).
- **`DeactivateCompany.Command()`** → `company.deactivate`. Refuses, in order, `tenant.not_found`,
  `company.archived`, `company.already_deactivated`, **`company.operates_default_market`** (the flagged
  default configuration's `OperatorTenantId` is the ambient tenant — delisting it would refuse every
  anonymous identity request on every host). `Auditable.Deactivated(adminId, now)`; when `WindDownFrom` is
  set, re-enqueues the sweep. Cancels nothing, e-mails nobody.
- **`ReactivateCompany.Command()`** → `company.reactivate`. `tenant.not_found`, `company.archived`,
  `company.not_deactivated`. `Auditable.Reactivated()` and the wind-down stamps cleared; what a wind-down
  already did does not come back.
- **`WindDownCompany.Command(DateOnly? FromDate)`** → `company.wind_down`. `tenant.not_found`,
  `company.archived`; a re-run (no date) while `IsWindDownRunning(now)` is `company.wind_down_in_progress`;
  a date when one is set is `company.wind_down_already_requested`; no date and none set is `common.required`; a
  date already past in the company's easternmost market is `company.wind_down_date_in_past`.
  `RequestWindDown` on the first request, then **one** message on `company-wind-down`
  (`wind-down:{tenantId}:{now:yyyyMMddHHmmss}`) through `CompanyWindDownDispatch`, which is shared with
  `DeactivateCompany`, drained after the commit, and deduplicated on the outbox's `(queue, key)` so two
  acts within a second ask for one run.
- **`CompanyWindDownService.RunAsync(tenantId)`** — the sweep, under the envelope's override; a permanent
  no-op when the company is missing, has no date or is frozen; `StartWindDownRun` committed first; then
  **(1) notices** — every `IsActive && IsEmailConfirmed && not anonymised` customer and every such cleaner
  with `ContractStatus == Approved`, keyset-paged, one `SendEmailMessage` each straight onto `send-email`
  through `IQueueClient`, `Code = WindDownRequestedOn`, a `CampaignProgress` cursor per page;
  **(2) orders** — `New`/`Confirmed`/`OnTheWay` (never `InProgress`), `Paid || Cash`, on or after
  midnight of the date in the address's market zone (`WindDownCutoff.Utc`) — every open order once the
  company is deactivated — status re-read untracked before each cancel, then
  `IPlatformOrderCancellation.CancelAsync(order, WindDownRequestedBy, System, "order.cancelled.company_wind_down",
  ServiceNotRendered)` and a commit per order; then the **re-drive** of every earlier run's cancelled,
  card-paid, still-`Paid` order through the refund leg alone (the key `refund:{id}:admin` resolves to the
  `Pending` row; Stripe replays once) → [Platform order cancellation](./platform-order-cancellation);
  **(3) templates** paused; **(4) every Active Plus** cancel-at-period-end, any currency;
  **(5) credit** — only when deactivated — every positive balance discharged through
  `TryDebitAsync(…, Expired, "wind-down-credit:{account}:{run}", note: "company wind-down")`;
  **(6) the last period** — only when deactivated, no open order and no completed order awaiting pay —
  `PayPeriodBackgroundService.ClosePeriodAsync(period, "Company wind-down", openNext: false)`; then
  `RecordWindDownRun` and a summary at Error.
- **`IEmailService.SendCompanyWindDownCustomerNoticeAsync` / `…CleanerNoticeAsync`** —
  `EmailType.CompanyWindDownCustomer = 8` / `CompanyWindDownCleaner = 9`, two templates, five locales of
  default copy under admin `EmailTemplateTranslation` rows; the company is named by each market's
  `CompanyInfo.LegalName`.
- **`MaterializeRecurringBookings`** and **`PayPeriodBackgroundService`'s rollover** — each reads the
  `Tenant` row once per group: no order for a deactivated company's templates; no successor period for a
  deactivated company.
- **`GetCompanyLifecycle.Query`** → `CompanyLifecycleDto`: name, state, `OperatesDefaultMarket`, every
  stamp with the actor's **e-mail**, the manifest hash, and the settlement facts →
  [Company settlement reader](./company-settlement-reader).
- **`AdminCompanyLifecycleController`** — `GET api/AdminCompanyLifecycle/get` (`CanViewCompanyLifecycle`),
  `POST …/deactivate` (`CanDeactivateCompany`), `…/reactivate` (`CanReactivateCompany`), `…/wind-down`
  (`CanWindDownCompany`), `…/archive` (`CanArchiveCompany`); the four writes under the `auth` rate window;
  every policy `AdminOnly`.
- **The admin audit** — the four labels with `ResourceType = "Tenant"` and a
  `CompanyLifecycleSnapshot(State, WindDownFrom)` before/after; frozen by `CompanyLifecycleAuditLabelTests`.
- **The admin web page** — `/company-lifecycle`, sidebar under `CanViewCompanyLifecycle`: the state
  banner with stamps and actor e-mails, the sixteen facts with links to the lists that settle them, the
  four acts behind confirmations, *Run wind-down again* / *Build archive again* once the date is set /
  the company is frozen, every refusal the server would give shown client-side as the reason line and, if
  it slips through, as the interceptor's `api.company.*` sentence; one act in flight at a time; re-read
  after every act.

## Does NOT know

- **Another company.** Every command acts on the ambient tenant; no tenant id crosses the wire (S4); there
  is no holding role (T-0748) and no cross-company read.
- **The platform's own shutdown.** The company that holds the default market cannot be deactivated;
  closing the last company is a different decision.
- **Whether to refuse a booking after the last day.** Nothing in the booking path reads `WindDownFrom`; a
  booking made after the announcement for a day after the date is cancelled and refunded by the sweep's
  re-run at deactivation (ADR-0064 Alt. (f), not built).
- **A second date.** The date is set once; an earlier date is an admin cancelling stragglers by hand.
- **Trip compensation.** An `OnTheWay` order cancelled once the door is closed is refunded to the
  customer; there is no rule for the cleaner's trip and none is added.
- **Guests.** A guest booking is cancelled and refunded with the standard push path; the notice goes to
  accounts only.
- **Time.** Nothing here runs on a timer — the sweep runs when an admin asks (the request, the
  deactivation, *Run wind-down again*).

## Invariants a reviewer checks

1. **`Login`, `MobileLogin` and `AdminLogin` do not call the gate**; the five partner/refresh call sites do,
   and the mint enforces it through the same scoped instance (`CompanySignInGateTests`,
   `TokenServiceAudienceProfileGuardTests`).
2. **The sweep's refund key is the literal `refund:{id}:admin`**, and the admin cancel's stays
   `refund:{id}:cancel` — the extraction changed no key (`PlatformOrderCancellationTests`,
   `AdminCancelOrderHandlerTests`).
3. **Notices before cancellations; every step commits alone** (`CompanyWindDownSweepTests` on Postgres,
   two companies — a second delivery of the same message changes nothing).
4. **The date floor is lifted only when `IsDeactivated`; credit and the period step run only then**; the
   period step is skipped while any offerable order or any completed order awaiting pay exists, and never
   re-invoices a Closed period.
5. **`DeactivateCompany`'s enqueue is not gated on a running sweep**; the button's re-run is
   (`WindDownCompanyTests`, `CompanyLifecycleRouteTests`).
6. **The four commands refuse a missing registry row `tenant.not_found` before the frozen check.**
7. **Every `company.*` key is in the five admin locales**; `auth.company_deactivated` in the five locales
   of partner web, Android and iOS; `order.cancelled.company_wind_down` renders in the three customer
   clients (the booking-policy parity gate).

## Watch-list

- **A continuation-message sweep** (K orders per delivery) is one ticket if a company ever has more than
  ~9,000 open future bookings; until then poison-then-*Run wind-down again* is the designed recovery.
- **O-2 (every Plus, any currency) and O-5 (credit discharged at closing)** are defaults the owner may
  narrow; each is one predicate.
- **Batch 3 (cross-market booking on one account)** changes what a customer of a closed company can do
  next; it does not touch this role.
