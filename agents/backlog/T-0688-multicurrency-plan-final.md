# Multicurrency — the final plan

**2026-09-08. This is the plan of record.** It supersedes Part 3 of
[`T-0688-multicurrency-design.md`](T-0688-multicurrency-design.md) and §§3–5 of
[`T-0688-multicurrency-plan-cz-only.md`](T-0688-multicurrency-plan-cz-only.md). §§1–2 of the CZ-only
plan — Adaptive Pricing, the drafted accountant letters, the EU SME answer — stand unchanged. The VAT
convention fix shipped in `56ad4d45` and is not re-planned.

**Owner rulings this pass is built on** (2026-09-08): Option B is ordered — per-currency price tables
built **now**, before production, because *"once working in PRO then it's complicated to build new
features overall, and reworking multicurrency system I wouldn't like (data corruption risks, risk of
money loss, not proper testing)."* **That argument defeated the recommendation to defer, and it was
right to:** the deferral case assumed the retrofit cost is flat pre-production, but the retrofit would
land *after* production exists. EUR is not launching; operations are CZK-only. He is the principal, not
a broker. And the design brief is extensibility — *"prepare everything in the way that we can either
configure it OR add a code that wouldn't need a rework of an entire system."*

Four research lanes fed this, and **all six adversarial verdicts came back refuted or materially
corrected** — an unusually high rate, and the corrections are marked in the text. Every load-bearing
code claim was then re-verified by hand against the tree at `142e8d54`.

**Three findings that outrank the currency work itself:** there is no code path to turn VAT on at all;
the customer website silently loses the address country while both mobile apps handle it correctly; and
the receipt decides its VAT posture from live company state rather than the order's own snapshot.

---

## 1. Answering you directly

### (a) The address picker — nearly right, and right in the two places you probably tested

**You are right about Android and iOS. You are wrong about the customer website, and wrong in one more place than any of the research found.**

Both mobile apps already do exactly what you describe. `ServiceAreaProvider.servicedCountryIsoCodes()` (`core/.../servicearea/ServiceAreaProvider.kt:64`) feeds the geocoder at `AddressManagerScreen.kt:621`, `ReverseGeocodingService.kt:144` captures Mapbox's `short_code`, and `BookingViewModel.kt:431-438` maps that ISO code to a serviced country id and sends it at `:454`. iOS is the same shape — `BookingAddressPickerView.swift:40` → `ServiceAreaProvider.swift:48` → `CountryResolver.swift:10-20`. Flip a second country on and both switch with no code change and no app release.

The customer website does none of it. Three gaps, all on one client:

1. **No country control on the booking wizard's address step.** The only `country` token in the entire template is `defaultCountryFlag="cz"` at `order-wizard.component.html:501`, which is the phone-number flag. The facade fetches the serviced list into a signal (`order-wizard.facade.ts:323, 333`) and auto-selects it **only when exactly one country is served** (`:338-341`). Nothing renders it. It is a picker's data source with no picker.

2. **The autocomplete throws the country away before it reaches the browser.** `MapboxGeocodingService.cs:311-327` reads only `postcode`, `place` and `locality` from Mapbox's context array, and the `SearchContext` record at `:348-350` does not even declare `short_code`. So `applyAddressSuggestion` (`order-saved-address.facade.ts:63-83`) stamps `countryId: current?.countryId ?? ''` — whatever was already there. The country whitelist is a hardcoded `['cz','sk']` (`mapbox-autocomplete.service.ts:83`), so a Bratislava address is suggestible today and would be written under a Czech country id.

3. **This one is mine, and nobody found it: the customer profile's saved-address dialog has no country control either.** `profile.facade.ts:287-308` loads the serviced list into `countryOptions` with a comment saying the profile "only ever uses this to render the address country picker" — but no template renders it. The only consumer is `profile.component.ts:471-477`, `defaultCountryId()`, which scans the option **labels** for `(CZE)` or the word `czech` and preselects that. With a second country serviced, every saved address a customer creates still silently becomes Czech, and they cannot change it.

**The notification you remember is a city check, not a country check** — `order-service-area.facade.ts:49-88` queries `/ServiceCity/{countryId}`. One correction to the research: it does **not** go silent everywhere when a second country is flipped on. A saved address carries its own `countryId`, and `order-wizard.facade.ts:385-391` auto-applies the account's default saved address, so logged-in customers with a saved address keep the check. It goes silent only on the typed-inline path — anonymous visitors and anyone entering a new address.

**What actually breaks on the flip.** `SetCountryServiced.cs:43` is one `country.SetServiced(...)` with no preconditions at all. The moment a second country is serviced, `OrderAddressResolver.cs:104-108` returns `CountryRequired` for every inline-address booking, and the wizard has no field to satisfy it. **Web booking dies on one admin click, in all five locales, on card and cash.**

So: the country control *is* the currency control, which is the payoff of your decision-2 ruling — there is no separate currency picker to build, ever. But it does not exist on the web, and building it is a **hard gate on the flip, not a follow-up**. See §2, E1.

### (b) Tax — what to do, in order

1. **Appoint an účetní this month, and hand them the company from its incorporation date.** This is not advice about VAT — it is an obligation that is already running. A Czech s.r.o. is an *účetní jednotka* (§1 odst. 2 ZoÚ) and must keep double-entry accounting *"ode dne svého vzniku"* (§4 odst. 1), in full scope unless a lighter one applies (§9 odst. 1). There is no *daňová evidence* option for a legal person. The annual DPPO return and the *účetní závěrka* in the sbírka listin are due at zero revenue; missing them risks an FÚ fine to 3% of assets and a registry-court fine to 100,000 Kč. This is the cheapest item in the whole stack and the only one that is overdue rather than upcoming.

2. **Send the principal/agent letter (CZ + EN, already drafted in `T-0688-multicurrency-plan-cz-only.md` §2.1) as their first written task.** Do not pursue a *závazné posouzení*: §132 odst. 1 DŘ issues one only *"v případech, kdy tak stanoví zákon"*, and Finanční správa's own list for VAT is three items — rate, reverse charge, books exemption. Principal-vs-intermediary is on none of them, at any price. A written opinion from a daňový poradce is the only defensible route, and you do not need one until item 4.

3. **Require the signed contractor agreement, and put the self-billing authorisation in it.** `DocumentType.Contract = 5` already exists and `SaveDocumentRequirement` is admin-configurable, but the seed requires only IdentityCard and WorkPermit (`insert_seed_data.sql:1085-1087`). No signed cleaner agreement exists anywhere in the platform, while `GenerateInvoice` already issues self-billed invoices in cleaners' names — and §28 ZDPH requires the supplier's **written** authorisation before you may do that. Fixing the requirement is one config row. Drafting the agreement is yours.

4. **At 1,000,000 Kč calendar-year-to-date gross bookings, buy an hour of a daňový poradce.** That is half the first threshold. Below it the question has no consequence.

5. **Fix `cs.json:1753` now.** It tells customers *"Ceny jsou uvedeny v CZK a zahrnují DPH"* while the company is a neplátce (`insert_seed_data.sql:1110`). The receipt path is correct (`ReceiptService.cs:389` prints "Nejsme plátci DPH"), but under §108 ZDPH anyone who states VAT on a document owes it with no deduction.

**Two numbers worth carrying, stated honestly.** Registration takes **190.91 Kč out of every 1,100 Kč clean** — that is 21/121 of gross, and the 21% is not escapable: interior cleaning in households (CZ-CPA 81.21.10) and household window cleaning (81.22.11) were the two items moved *out* of the reduced-rate annex into the standard rate on 1 January 2024. The "27% of margin" figure floating in the earlier passes is an artefact of the seeded 50% pay multiplier (`insert_seed_data.sql:753`) and is not a real business number — don't quote it. On the input side the platform already computes the reclaim: `FileExtensions.cs:33` calls `VatWithinGross(invoice.TotalAmount, supplier.IsVatPayer)` against a CZE config seeded `VatRequired = true, VatRate = 0.21`. A VAT-registered cleaner's 400 Kč carries 69.42 Kč. So the real band is 190.91 (no plátci) → 121.49 (all plátci), not "nothing to reclaim".

**And the finding that matters more than any of the arithmetic: there is no way to turn VAT on.** `CompanyInfo.SetVatPayerStatus` (`CompanyInfo.cs:135`) has exactly two references in the entire repository — its own definition and `VatCalculatorTests.cs:42`. `CompanyInfo.Create` (`:72`) has no `isVatPayer` parameter and its initializer omits the field, so every company row is permanently `false`. Neither `CreateCompanyInfo.Command` nor `UpdateCompanyInfo.Command` carries it. The whole Angular tree has zero references to it. **Registering for VAT today means a hand-written `UPDATE` against the production database — the one operation you have forbidden outright.** That fails your constraint 5 on its face, and it is in Wave B below.

Two things I could not settle and an adviser must: whether a refund reduces obrat in the period of the original supply or of the refund; and **the straddle** — `CreateOrder.cs` dispatches payment at booking, but the supply is `CleaningDateTime`. On registration day there is a live book of orders already paid at a neplátce price whose date of supply falls after registration, and nothing in the platform can identify, re-price or exclude that cohort. Recurring templates compound it. That belongs on the pre-registration checklist, not in this build.

*Not tax advice. Everything above is quoted from ZoÚ, DŘ, ZDPH and Finanční správa, and from your code with file:line, so an adviser can confirm or correct it in one pass.*

### (c) Option B is settled

Confirmed, and your argument beat the recommendation on its own terms. The deferral rested on one premise — that the retrofit cost is flat because there is no production. Your reply denies exactly that premise: the retrofit would land *after* production exists, and a schema change to a table holding live money is not the same change at all. That is correct, and it is the same reasoning CLAUDE.md rule 4 carves out for money paths. Not re-argued below.

One place where "build the per-currency table" does **not** apply, and it is worth naming so the rule stays sharp: `MembershipPlan.MonthlyPriceCzk`. `Service.BasePrice` *is* the price; `MonthlyPriceCzk` is a display mirror of a price that lives in Stripe (`MembershipPlan.cs:44-46`), and `UserMembership` references `MembershipPlanId`, never a price row. Build the price table where the column **is** the price, not where it echoes one Stripe owns.

---

## 2. The extensibility contract

This is what you are buying. After the build in §3:

> **Adding a market is 1 currency row, 1 `Countries.IsServiced` toggle, 1 `CountryConfigurations` row, 1 `CountryInvoiceConfigs` row, N `ServiceCities`, N `PropertySizePresets`, 23 catalogue price rows, 18 `EmployeePayConfigs`, and — only if it is a second legal entity — 1 `CompanyInfo` row. Nothing else in the database, and nothing at all in the backend.**

The 23 is exact, counted from the seed: 10 services (`insert_seed_data.sql:564-638`), 5 extras (`:639-675`), 8 packages (`:677-733`). The 18 is 10 + 8. The "nothing in the backend" holds because currency is derived server-side from `Address.CountryId` → `CountryConfiguration` → `Currency`, and every money reader downstream is denominated by the aggregate's own `CurrencyId`.

### The known exceptions — bounded, additive, and enumerated now so they are never a surprise

| | Change | Category | Notes |
|---|---|---|---|
| **E1** | **Web country control** — the wizard address step, the profile saved-address dialog, a country on the autocomplete suggestion (four DTO hops plus the `short_code` parse), and the whitelist sourced from `getServiced()` | Additive UI + one server parse | **A hard gate on the `IsServiced` flip, not a follow-up.** The flip breaks web booking the same second. Mobile needs nothing. |
| **E2** | `QuoteOrder.Command` gains a country | Additive wire field | The wizard prices on step 0 and collects the address on step 1. Under per-currency tables a quote cannot be priced before the country is known. Costs nothing in CZK-only; mandatory at market two. |
| **E3** | One currency-aware formatter per client | Additive | 12 hardcoded `currency: 'CZK'` sites across 11 Angular files; `OrderFormatters.kt` forces `maximumFractionDigits = 0`, which makes a EUR line of 45.10 render "45 €" so lines stop summing; iOS `OrdersFormat.swift` mirrors it. |
| **E4** | Three Google Pay currency literals | Additive | `BookingBottomSheet.kt`, `SubscribePlusScreen.kt`, `OrderDetailScreen.kt`. The Stripe SDK requires them; nothing can make them data. |
| **E5** | Currency on `RevenueReportDto` + group the query | Additive | The DTO has no currency member at all today, so two markets would silently sum into one number with no hint on screen. |
| **E6** | A currency deactivate command and an `IsActive` predicate | Additive | `Currency.IsActive` is read by **nothing** — no repository predicate, no query filter, no endpoint. "Turn a currency off" is code, not config, and always will be until this is built. |
| **E7** | `CountryConfiguration.VatTreatment` | Additive nullable column | Safe to defer — see §4. |
| **E8** | Plus in a second currency | Additive | `MembershipPlanPrices`, `UserMembership.BillingCurrencyId`, Stripe `currency_options`. The Stripe price is the source of truth, so there is no historical money to restate. |
| **E9** | `agents/tools/check-booking-policy-parity.mjs` | Additive | The only repo checker pinning a currency-bound constant (`NoShowCreditCzk`, read at `:126` with a scalar-const regex). It changes in the same commit as the constant, or the repo-root parity workflow goes red. |

### The honest caveats

- **Four `CountryConfiguration` columns have no reader anywhere: `PhonePrefix`, `ReducedVatRate`, `DefaultPaymentGateway`, `LegalRequirementsJson`.** A market seeded with them is not thereby configured. Do not count them as capability.
- **`CountryConfigurations` for CZE seeds `ReducedVatRate = 0.15` (`insert_seed_data.sql:970`), a rate abolished on 1 January 2024** and replaced by a single 12% reduced rate. It is dormant because nothing reads it — but Option B's authored price tables must not copy it forward, and the SVK `0.20` and POL `0.08` in the same block are equally unchecked. Leave them dormant and wrong rather than half-correcting countries you are not entering; just never treat that column as a source.
- **`SetCountryServiced` validates nothing.** No check for a `CountryConfiguration`, a `CountryInvoiceConfig`, service cities, priced catalogue rows or pay configs. I am building an inline guard in Wave A (§3) — it is one `if` in an existing handler, not a new abstraction, and it is the single cheapest place to make this contract self-enforcing rather than documented.
- The contract covers *adding a market*. It does not cover *Slovak or Polish tax law*, which moves and must be re-checked, nor the fixed-establishment question that fires the first time anyone proposes an office, a depot or a local coordinator.

---

## 3. What gets built now

Dependency-ordered. **All schema work is in one block so the migration regeneration and the DEV drop happen exactly once.**

### Wave A — no schema, no wire, no regeneration

**A1. Adaptive Pricing off.** `AdaptivePricing = new SessionAdaptivePricingOptions { Enabled = false }` in both option blocks in `StripeClient.cs` (order session at `:51`, Plus at `:412`), plus the dashboard toggle. Both properties exist in Stripe.net 50.4.0, already pinned. No test — the options are built inline inside the call, and extracting a seam to assert two literals is not proportionate. **The dashboard toggle is the primary defence, because Payment Links have Adaptive Pricing permanently on and no code can reach them.**
*Proves it:* the sandbox probe only. Nothing in the suite sees a dashboard setting.

**A2. Close the caller-supplied currency route — these three land together or not at all.**
 (a) `CreateOrder` and `QuoteOrder` stop reading `command.CurrencyId`; the server resolves unconditionally. **Keep the field on the wire** (dropping it costs an NSwag run on three clients and a mobile spec re-dump). Drop the two "currency merely exists" validators at `CreateOrder.cs:118-123` and `QuoteOrder.cs:151-156`.
 (b) Stop reading `Currency.ExchangeRate`: delete `OrderPricingCalculator.cs:64` and the thirteen `* exchangeRate` sites at `:78, 106, 109, 114, 115, 132, 135, 143, 144, 145, 148`. **Leave the column and the wire field** — `OrderApi.kt:340, :349` and `BookingApi.kt:126` treat it as required and refuse the page rather than assume parity, deliberately and with tests. Add a one-line comment: display-only, never a price input.
 (c) Delete the ten seeded currencies that are neither CZK nor EUR (`insert_seed_data.sql:526-536`).
Any one alone leaves a worse state: (a) without (c) leaves a **~24× underpayment reachable by any authenticated caller** — post the EUR id against a Prague address and the CZK catalogue is multiplied by the hand-typed `0.041`.

**A3. `VatCalculator` fails closed, and stops writing an ambiguous null.** `VatCalculator.cs:14` collapses two different causes into one silent zero — not a VAT payer, **and** a VAT payer with no `CountryConfiguration` row, which is precisely the second-country case. Split them: throw on a missing config; for the genuine neplátce case write a **non-null zero rate** rather than null, so `Order.AppliedVatRate IS NULL` stops meaning three different things. Invert the test that currently pins the fail-open behaviour.

**A4. The receipt reads the order's own snapshot, not live company state.** `ReceiptService.cs:385-389` lets the live `IsVatPayer` alone decide `IsVatPayer`, `NetAmount`, `VatAmount` and the "Nejsme plátci DPH" notice; `:227` and `:234` AND it with the snapshot. It fails in both directions. **After you register, a pre-registration order re-rendered through `RetryFiscalRegistrationAsync` — which re-resolves company info live at `:279-283` and re-uploads the stored blob at `:312-320` — produces a receipt with neither a VAT line (`DefaultReceiptLayoutBuilder.cs:317` needs `VatAmount > 0`) nor the statutory neplátce notice (`:360` needs `!IsVatPayer`). A tax document that asserts neither posture.** Dormant while EET is off; live at EET 2.0. Four lines, no schema. **Must ship before the flag is ever set.**

**A5. Two one-liners.** `CurrencyRepository.cs:12-13` — the `??` binds to the `Task`, which is never null, so the `EntityNotFoundException` is dead code and a missing default returns a null `Currency` behind a `!` into an NRE on the most-travelled path in the platform. And `SetCountryServiced.cs:43` gains an inline guard refusing the flip unless the country has a `CountryConfiguration`, priced catalogue rows and at least one `ServiceCity`.

**A6. `cs.json:1753`** — the VAT statement in the customer terms.

*Proves Wave A:* `Cleansia.Tests` (pricing, order creation, `VatCalculatorTests`, a receipt-builder test for both postures), `Cleansia.HostTests` (the endpoints still accept `currencyId` and ignore it), `Cleansia.IntegrationTests`.

### Wave B — every schema change, then ONE regeneration and ONE DEV drop

**B1. Order-line snapshots.** `OrderServices += UnitBasePrice, UnitPerRoomPrice, LineTotal`; `OrderPackages += LineTotal`; new `OrderExtras(OrderId, ExtraId, Slug, UnitPrice)` replacing the `Orders.Extras` JSON column (`OrderEntityConfiguration.cs:197-200`). **Snapshots must land with or before the price tables, not after.** If prices move first, `IssuePartialRefund` has to resolve a historical order's price by `(itemId, CurrencyId)` — fail-closed throws on the refund path, fail-open reinstates the `?? 0m` the design forbids. Snapshots delete the question.

**B2. The three price tables.** `ServicePrices(ServiceId, CurrencyId, BasePrice, PerRoomPrice)` UQ`(ServiceId, CurrencyId)`; `PackagePrices(PackageId, CurrencyId, Price)`; `ExtraPrices(ExtraId, CurrencyId, Price)`. All `numeric(18,2)` — this also fixes `Extras.Price`, currently `(10,2)` at `ExtraEntityConfiguration.cs:32-34`. `Service.BasePrice`, `Service.PerRoomPrice`, `Package.Price` and `Extra.Price` are deleted.

Three mapping rules, one of which is a trap:
- **No `TenantId` in any of these unique indexes.** Copy `PropertySizePresetEntityConfiguration.cs:33-40` verbatim including the `HasDatabaseName` and the comment — that is the exact-shape precedent for a composite unique index on catalogue data, and it names the nullable-`TenantId` landmine.
- **Parent → price is `Cascade`. Currency → price is `Restrict`.** Get this backwards and admin service deletion becomes permanently impossible: `DeleteService.cs:59-68` flushes early and maps any FK violation to `service.in_use`, so with `Restrict` every service would have a price row and every delete would refuse.
- **`CurrencyRepository.IsInUseAsync` gains the three tables in the same commit.** It checks only Orders, EmployeePayConfigs and EmployeeInvoices today, and `DeleteCurrency` has no FK-violation mapping and no early flush — so without this you get a raw 500 at pipeline commit instead of `currency.in_use`.
- **No `Prices` collection on the parents.** `HasOne(p => p.Service).WithMany()` with no inverse, DB-level cascade for cleanup — an unnamed `.WithMany()` onto a read-only projection invents a duplicate shadow FK, which is your own recorded landmine.

**B3. `Currencies` gets the indexes it has never had.** `CurrencyEntityConfiguration.cs` contains no `HasIndex` at all: two rows with `Code = 'EUR'` or two rows with `IsDefault = true` are storable right now. Unique on `Code`, filtered unique on `IsDefault`. **Adding either to a live table fails on pre-existing duplicates** — this is exactly the class of change that must not wait.

**B4. `CreditAccounts` becomes UQ`(UserId, CurrencyId)`** and `User` → many accounts (`CreditAccountEntityConfiguration.cs:39-45` is unique on `UserId` with a 1:1 nav today). `GetSpendableAsync` takes a currency — today it is `Where(a => a.UserId == userId).FirstOrDefault()` (`CreditAccountRepository.cs:139-149`), which with N rows becomes an arbitrary pick, i.e. **spending the wrong balance**. This is a 1:1→1:many conversion on a table holding customer money; it is free today and is not free later.

**B5. Cleaner pay gets a unit.** `EmployeePayConfigs`' unique index (`EmployeePayConfigEntityConfiguration.cs:97-99`) gains `CurrencyId`, keeping `.AreNullsDistinct(false)` — **a second per-currency rate for the same cleaner and service literally cannot be stored today.** `OrderEmployeePay` gains `CurrencyId`; it currently records `BasePay`, `TotalPay`, `MinPay`, `MaxPay` and four more with nothing recording their unit. The payout-invoice currency is derived from the `OrderEmployeePay` rows being invoiced and fails if they disagree; **both existing derivations are deleted** — `GenerateInvoice.cs:83` (work country → `CountryConfiguration.DefaultCurrencyCode`) and `PayPeriodBackgroundService.cs:332` (`Employee.PreferredCurrencyCode`), which disagree with each other and neither of which reads the rows invoiced. That fires the day a second country exists, before a single foreign order, on a real tax document.

**B6. `CompanyInfo` — the fork, plus the VAT lever.** `CompanyInfoEntityConfiguration.cs:38` makes `RegistrationNumber` globally unique, so one legal entity cannot hold two country rows, while the read path is already per-country (`OrderFactory.cs:204-205`) and silently borrows another country's posture when a row is missing. **I need your ruling — see §5.** Regardless of the answer: add `VatRegisteredFrom` (one nullable date), thread the VAT-payer flag through the two existing command records so `SetVatPayerStatus` finally has a caller, and add the admin control. **That converts a forbidden production `UPDATE` into configuration, and the registration date is genuinely unrecoverable after the fact** — this is the constraint-5 side of rule 4, not the ordinary side.

**B7. Fail-closed pricing, with no new error keys.** The codebase already implements this exact three-layer shape for missing pay configs, so copy it: (1) `GetServiceOverview.cs:30-45` and `GetPackageOverview.cs:31-44` already withhold an `unquotable` set from the customer catalogue — the unpriced set joins it; (2) `CreateOrder.Validator` and `QuoteOrder.Validator` chain a `MustAsync` after the existing pay term and **reuse `BusinessErrorMessage.InvalidSelectedServices` / `InvalidSelectedPackage`**, exactly as `CreateOrder.cs:130-140` does and for the reason written there; (3) `OrderFactory.CreateAsync` throws `InvalidOperationException` like `OrderFactory.cs:52-60`, because the recurring materializer reaches the factory without the validator. The shared join lives once in a static `CataloguePriceLookup` modelled on `PayCoverageLookup` — one new type mirroring an existing one, not a new pattern. **Zero new i18n keys, therefore zero five-locale sweeps and no error-contract-parity changes in any app.** Admin paged lists must **not** filter — an admin has to see an unpriced service in order to price it.

**B8. Every consumer of a historical order stops reading the live catalogue.** `IssuePartialRefund.cs:255-256, 271, 276, 313`; `ReceiptService.cs:246-247, 260` (fiscal lines) and `:362-363, 368` (PDF lines); and — **the one nobody enumerated** — the order-list projection at `OrderMappers.cs:69, 75-76` and its entity twin at `:134, 159-160`, plus order detail at `:281`. `OrderListProjectionEquivalenceTests` pins the projection and entity paths together, so both move in one commit. B8 also closes a live divergence: `OrderPricingCalculator.cs:49` filters `e.IsActive` and `IssuePartialRefund.cs:313` does not, so an extra deactivated after ordering is dropped from `TotalPrice` but counted in the refund denominator.

**B9. The receipt gains the lines it lacks.** `ReceiptPdfData.Extras` is `List<string>` with no prices (`ReceiptPdfData.cs:14`) and there is no field for the express surcharge or any discount — **the lines cannot sum to the total in pure crowns today, regardless of any currency question.** Same pass as B8, which supplies the numbers.

**B10. Admin per-currency price editors.** `service-form.component.ts` already builds a `translations` FormGroup dynamically per active language (`:160-176`) and patches it (`:189-205`); the price editor is a line-for-line sibling in the same file. **With only CZK active the form renders one price block and looks exactly like today.** The validator is "must cover every active currency", written against the existing `ValidationExtensions`.

**B11. Two compile breaks to resolve deliberately.** `ServiceSort.cs:16-19` and `PackageSort.cs:16-17` sort by price. That is ambiguous with N currencies. Delete the sort terms rather than silently picking a currency.

**B12. `BulkCreateEmployeePayConfigs` joins the price row for its own `CurrencyId`.** It multiplies `service.BasePrice`/`package.Price` by a grade multiplier (`:106-107, 137`) while passing `command.CurrencyId` straight through — **so a EUR pay config would today be generated from CZK catalogue numbers, a ~24× error in what a cleaner is paid, from an admin bulk action.** The price-table join closes it by construction.

**B13. Seed rewrite.** 23 CZK price rows, authored as derived `INSERT … SELECT` joined by name — the shape the pay configs already use — so a new catalogue row cannot leave a price hole. **The pay-config seed reads `s."BasePrice"` at `:753` and `p."Price"` at `:768` and must join the price tables instead, and `PayConfigSeedTests` asserts those exact substrings — same commit, or CI goes red and a fresh DEV boot produces an unbookable catalogue.** Keep **CZK and EUR** in the currency seed: EUR then exists, is active, has no price rows, and is correctly unofferable. That is the machinery demonstrated with only CZK reachable, and it makes the fail-closed rule provable by an integration test rather than a fixture.

**B14. `numeric(18,2)` on the six bare `numeric` columns** on `Orders` (`NetAmount`, `VatAmount`, `AppliedVatRate`, `TravelDistance`, `CancellationRefundAmount`, `CancellationFeeRate`). Doesn't vary by market; rides the regeneration free.

> **B15 — THE ONE REGENERATION. This is where DEV is dropped.** Mine to run, named in the report.
> ```
> export PATH="$HOME/.dotnet/tools:$PATH"
> cd src
> dotnet ef migrations remove --force --project Cleansia.Infra.Database --startup-project Cleansia.Web.Partner
> dotnet ef migrations add   Initial --project Cleansia.Infra.Database --startup-project Cleansia.Web.Partner
> ```
> then drop the DEV database — the migration id changes, and a DEV database recording the old one replays the create script against existing tables. **Everything in B1–B14 lands before this line or waits for a second one.**

**B16. NSwag: the admin client only.** The admin detail DTOs and create/update commands change, so `npm run generate-admin-client` and commit the regenerated client in the same change. **The customer and partner clients do not change, and neither mobile app changes at all** — because the rule is *do not change the property SET*. `ServiceListItem.BasePrice/PerRoomPrice`, `PackageListItem.Price` and `ExtraListItem.Price` were never mirrors of column names; they mean "the price in the currency you are being quoted in", which is what a price table produces. I verified the mobile side directly: zero of the 168 schemas in `customer-mobile-api.json` carries a `required` array (the four hits are path parameters), the Android assertions compare element *names* only, and both platforms build from the committed spec. **Nullability is free; adding, removing or renaming a property is not.** One correction to carry: `npm run generate-customer-client` reads port 5003 and writes only the Angular client — it cannot tell you anything about the mobile spec, which comes from `./gradlew :customer-app:dumpOpenApiSpec` on port 5004. Do not cite it as evidence.

*Proves Wave B:* `Cleansia.Tests` — pricing snapshots, `CataloguePriceLookup` gaps, `RefundAllocator`, `IssuePartialRefundHandlerTests`, `PartialRefundFeeRoundingTests`, `PayConfigSeedTests`, a receipt-builder test asserting lines sum to total in both VAT postures, `FrozenPermissionMapTests` if any permission row moves. **`Cleansia.IntegrationTests` after B15 — Testcontainers builds a real Postgres from the migration and is the only thing that proves the model and the schema agree**; it is the gate on `OrderExtras`, the three price tables and every new unique index, and it is where the EUR fail-closed proof lives. `Cleansia.HostTests` for the admin surfaces. Frontend: the admin `service-management` and `package-management` Jest suites. Repo checkers: `check-booking-policy-parity.mjs` is untouched (the no-show credit stays a scalar); run the full `agents/tools/check-*.mjs` set before the PR.

### One non-schema addition: the turnover number

**Nothing anywhere tracks obrat**, and the admin revenue report is the right shape with the wrong number: `GetRevenueReport.cs:26` sums `TotalPrice` over every order in the window and **never excludes cancelled ones** (`:34` counts them and moves on), the window filters on `CleaningDateTime` — the scheduled date, not the date of supply — and refunds are never subtracted.

The minimum useful thing is one number, not a subsystem: sum `TotalPrice` for orders with `CompletedAt` in the current calendar year and `CancelledAt` null, minus confirmed refunds, shown on the existing report beside the two statutory thresholds and the remaining headroom. One repository query, one tile. **No new table, no config row, no job, no alert** — the thresholds are fixed by statute and 2,536,500 is pinned to an ECB rate from 2018, so a config table for two constants guards nothing.

Proportionality, on the record: this is the **ordinary** side of rule 4, not the constraint-5 side — it prevents no rework of a money path. It earns its place only because it tracks a statutory 10-working-day deadline that nobody and nothing else is watching. It needs no schema, so it blocks nothing and is not part of the regeneration block. Say the word and it comes out.

---

## 4. What is deliberately deferred

| Deferred | The future change | Category | Safe because |
|---|---|---|---|
| EUR prices, cities, and a second `CompanyInfo` | 23 + N data rows | **Config** | That is the whole point of §2. |
| `CountryConfiguration.VatTreatment` (4 states) | Nullable column + a signature | **Additive** | `Order.AppliedVatRate` is genuinely write-once: `SetVatBreakdown` has exactly two call sites, both inside one if/else in `OrderFactory.CreateAsync` (`:210`, `:214`), `Order.TotalPrice` is written once at `Order.cs:505`, and nothing in `src/`, `sql-scripts/` or any `ExecuteUpdateAsync` writes the VAT columns. No backfill. **Wave A3 is what makes this true — without the non-null-zero fix, a null rate means three things and the backfill has no source.** |
| The market selector (`User.PreferredMarketCountryId`) and the home/catalogue/Plus surfaces | Additive UI | **Additive** | With one market those pages have exactly one honest answer. |
| Loyalty divisor + per-currency `LoyaltyTierConfig` floor | One column + one index term | **Additive** | Four config rows, no customer money. Cheap whenever. |
| Per-currency no-show credit | Constant → lookup, + `check-booking-policy-parity.mjs` | **Additive** | Inert with one currency. |
| Extras admin CRUD | Controller, commands, validators, six permission sites | **Additive** | **Its forcing reason is gone.** Option B's "you cannot create an item without pricing it" never fires, because nothing creates an `Extra` — `Features/Extras/` holds only `GetExtraOverview.cs` and a DTO, and there is no `AdminExtraController`. Extra prices are authored by SQL, which is what they are today. It also *activates* the extras third of the snapshot defect, so it must never precede B8 — under this plan it cannot. |
| `Currency.ExchangeRate` column and wire field | Remove from two response contracts | **Additive today, rework after mobile ships** | Wave A2(b) removes every *read*. Delete the column in the next pass that touches the mobile wire for another reason. |
| Slovak / Polish specifics — rates, PKWiU, `PayoutScheme` | Data | **Config, but re-verify** | The seeded SVK `0.20` has been 23% since 2025 and CZE's `ReducedVatRate 0.15` was abolished in 2024. Dormant and wrong is safer than half-corrected. |
| Plus in a second currency | `MembershipPlanPrices`, `UserMembership.BillingCurrencyId`, Stripe `currency_options` | **Additive** | Stripe holds the price; `UserMembership` references a plan, never a price row. |

### The one I am flagging loudly, because it is your stated nightmare and it is not a currency question

**Cleansia Plus revenue has no financial record anywhere in the platform.** `UserMembership` stores a Stripe subscription id, period dates and a status — **no amount, no currency, no net, no VAT**. `Features/Memberships/` contains zero VAT references. No `Payment` or `MembershipPayment` entity exists. No receipt or invoice is generated for a membership charge. So for subscription revenue there is no snapshot to reconstruct a posture from — the only record is Stripe's.

That is not automatically wrong: Stripe's invoices carry amount, currency and tax, and for a Czech neplátce selling to Czech consumers that may be an acceptable book of record. **But it is a decision, and right now it is an unmade one.** If you want Plus revenue in the platform's own accounts, the snapshot must exist before the first live subscription, because a subscription charged without one is unreconstructable afterwards — the exact shape you said you would not accept. Your accountant will have a view. It is not in this build, and I have not sized it.

---

## 5. What I need from you

1. **`CompanyInfo`: one legal entity or two?** `RegistrationNumber` is globally unique (`CompanyInfoEntityConfiguration.cs:38`), so under a one-entity OSS model a second country cannot get its own row — while the read path is already per-country. **This is a schema fork and it must be settled before the regeneration.** If the answer is "one entity, and it can hold a row per country", the unique index moves to `(RegistrationNumber, CountryId)`.
2. **Stripe, three checks only you can do:** turn Adaptive Pricing off in the sandbox dashboard; confirm whether CZK is a settlement currency on the account (if it is not, the whole Adaptive Pricing finding is theoretical); and check whether any Payment Links exist, because those are a hole no code change closes.
3. **EUR seeded active with zero price rows, or CZK alone?** I recommend seeding EUR — it makes "the machinery is built, only CZK is reachable" provable by an integration test rather than a fixture. One word.
4. **Appoint the účetní and send the drafted letter.** It blocks nothing in this build; it sets your VAT registration date, and the obligation is already running.

If there is a named B2B or EUR-invoicing prospect, tell me — that is the single fact that would change the shape of §3, and absent a name I have assumed there is none.

---

## What is still unknown

- Whether `adaptive_pricing[enabled] = false` actually disables it. Stripe documents only the affirmative branch. The three-curl sandbox probe in the CZ-only plan §1 settles it; nobody has executed anything against Stripe.
- Gross vs commission turnover. The code says principal in eight independent places and I believe it is right, but §4a turns on the contracts and the cleaner agreement does not exist yet.
- Whether a refund reduces obrat in the period of the original supply or of the refund. I have assumed net-of-refunds for the turnover tile; confirm before anyone acts on that number.
- The straddle cohort at registration — orders paid at a neplátce price whose supply date falls after registration day. Nothing in the platform can identify it, and I have not designed a fix.
- Whether your blob lifecycle policy retains receipts and invoices for the statutory 5/10 years. I checked that GDPR erasure anonymises rather than deletes them; I did not check the Azure storage configuration.