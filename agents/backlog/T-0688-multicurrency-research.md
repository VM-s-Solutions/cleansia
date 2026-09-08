# Multi-currency at launch (CZK + EUR) — what it actually costs

**Research pass, 2026-09-08. Read-only — no code, schema or data was changed by this work.**

Produced by a four-way survey of the tree (money math · fiscal and legal · data model · surfaces and
clients), each finding then put to an independent adversarial pass that was asked to refute it. Several
first-pass findings were downgraded or deleted that way, and section 6 lists what came out and why —
being able to *remove* work matters as much as adding it.

Every claim carries a `file:line`. The claims this document leans hardest on were re-checked by hand
afterwards: the VAT convention mismatch (§3.1), the dead null-guard in `CurrencyRepository`, the
caller-supplied `CurrencyId` rule in `CreateOrder`, and the exchange-rate scaling in
`OrderPricingCalculator`. All four hold as written.

---

## 1. The one-paragraph answer

**No — the platform cannot take a euro today, and the reason is not that the machinery is missing. It is that the machinery was built, wired up, and then never once executed.** Every catalogue price is authored in Czech crowns, and a euro price is produced by multiplying the whole basket by a single hand-typed number (`0.041`) that someone entered into a seed file and that nothing anywhere refreshes — there is no rate feed of any kind in this repository. All three client apps send `currencyId: null` on every quote, so the server always resolves the default, the multiplier is always 1.0, and the conversion path has therefore never run in production. That is genuinely good news — nothing is broken for a current customer, and nothing here is a regression. It is also why none of it has been caught: **the first euro order will be the first execution of about fifteen code paths at once.** If you switched euros on this afternoon, the customer would agree to a price labelled in crowns and be charged in euros; the receipt they received would list items that do not sum to its own total, off by roughly 24×; the cleaner who did the job would be issued a tax document in the wrong currency; the admin revenue report would add crowns to euros and stamp "Kč" on the answer; and the customer would earn 4 loyalty points where a Czech customer earns 110 for the identical clean. Separately and more seriously, there is a VAT bug sitting underneath all of this that has nothing to do with currency and would under-declare output VAT by 98.8% on every single sale the day you register for VAT. **Realistic scope: 2–3 weeks of focused work, and it cannot usefully start until you answer one product question (section 4).**

---

## 2. What a customer, an accountant and a cleaner would each see

*Plain language. Reference job throughout: a standard clean of a 3-bedroom, 1-bathroom flat — 1,100 Kč today, which the seeded rate turns into €45.10.*

### The customer

She opens the website, browses, and sees "from 1,200 Kč" on the services page — because that page has its own price formatter that says "Kč" and cannot be told otherwise. She books. The booking wizard also has its own formatter, also hardcoded to crowns, and it never reads the currency the server sends back with the quote. So every number she sees — each line item, the discount, the credit applied, and the final "amount due on your card" — is printed as **"45,10 Kč"**. She agrees to that. Her card is then charged **€45.10**. She has consented to a price stated in a currency she was not charged in, on the one screen where consent is given, and the gap is about twenty-four fold.

On Android it is worse in a different way. The Google Pay sheet — the last thing she taps before the money moves — is hardcoded to say **CZK** and to say the merchant is in Czechia, regardless of what the order actually is. And every euro price on Android loses its cents: €45.10 renders as **"45 €"**, because the formatter is set to zero decimal places, which is a sensible convention for crowns and simply wrong for euros. Her line items will not add up to her total for that reason alone.

Her receipt arrives as a PDF. The total reads **€45.10**. The line item above it reads **€1,100.00** — the crown number with a euro symbol in front of it. Nothing on the document explains the discrepancy, states an exchange rate, gives a date, or shows a crown equivalent.

Then the small print bites. She books a lot, so she is watching her loyalty points: the site promises "1 point per 10 Kč, silver at 500 points, 5% off orders over 1,000." She earns **4 points** for this clean. A Czech customer paying the same real money earns **110**. She would need roughly €5,000 of cleaning — about 122,000 Kč — to reach silver, and when she got there the "5% off orders over 1,000" would never once apply to her, because the threshold is a bare number that means crowns and no residential clean is ever €1,000. Her tier badge would say Silver and her discount would silently be zero, forever, with no error and nothing on screen to explain it.

If she has a bad clean and support compensates her with 500 in credit, that credit opens in crowns — there is no other option in the system — and it can only be spent on a crown order. Her balance shows as **0** at every euro checkout, with no message. Twelve months later it expires. She was compensated with money she could never spend.

If a cleaner no-shows on her booking, she gets her full refund and **no apology credit at all** — the platform deliberately refuses to hand her 250 € where 250 Kč was meant. That refusal is correct and is the one place in the money path that was built properly. But the home page promises her "everything back + 250 Kč credit" in all five languages, including Slovak, so the promise is made and not kept.

### The accountant

Two problems, one of which has nothing to do with euros.

**The euro problem.** Month-end revenue cannot be reconciled against the bank. The revenue report adds every order together with no regard for currency and returns one number — twenty crown orders at 1,100 plus twenty euro orders at 45.10 reports **52,050** and an "average order value" of **1,301.25** — figures denominated in nothing that exists. The admin screen then prints "Kč" next to them. The same is true of the payroll report. There is no currency field on either report at all, so nothing on the screen even hints that currencies were combined. The growth percentage compares the first half of a period against the second half of the same mixed pot, so a week that shifted from Czech to Slovak bookings reads as a revenue collapse.

Worse, the exchange rate a past order was priced at is not recorded anywhere. The order stores which currency it was in, but not the rate. Every screen that reports an order's rate reads the *current* value off the currency table. So the day someone corrects the euro rate from 0.041 to 0.0395 — which they will, because it is already wrong — **every euro order ever placed retroactively starts reporting the new rate**, including ones already invoiced and settled. There is no record anywhere that any other rate ever existed, no record of who changed it or what it was before, and no way to reconstruct the crown value of a euro sale from stored data.

**The problem that is not about euros at all, and is the more urgent of the two.** The VAT calculation is being fed the wrong shape of number. The database stores Czech VAT as `0.21`; the formula that consumes it expects `21`. Nobody has noticed because the seeded company is flagged "not a VAT payer," so the entire VAT block is switched off and returns zero. The day you register for VAT — which you have to before selling in euros at any scale — every receipt and every accounting export will declare **4.19 Kč of VAT on a 2,000 Kč job instead of 347.11 Kč**. That is a 98.8% under-declaration of output VAT on every sale, and the receipt would helpfully print "VAT 0%" next to it. There is not a single test anywhere in the codebase that exercises that calculator.

### The cleaner

He does the 1,100 Kč job. His pay is worked out from rates that are stored in crowns and saved to a row that **has no currency column at all** — the number 400 is written down with nothing recording what 400 means. At the end of the pay period the platform generates an invoice on his behalf: a real tax document, with a variable symbol and a bank account on it, that he files with his return.

The currency printed on that invoice is decided by a completely separate route — it is looked up from **which country he is registered to work in**, not from the job, not from the rates, and not from anything that checks the two agree. A cleaner registered in Slovakia gets an invoice reading **"400 €"** against a job that billed the customer 1,100 Kč. That is roughly a 24× overstatement of his declared income and of the platform's declared cost, on a document he hands to a tax office. He sees the same wrong number in the app, on his "My Pay" screen, before the invoice even exists.

**Correction to the prior pass, which matters for urgency:** this does *not* fire today. Only one country is currently flagged as serviced, so every cleaner resolves to crowns and the document is correct. The defect fires the moment you flip a second country on — which is step one of the euro launch, before a single euro booking exists. There is a window; it is not currently bleeding. There is also no automated payout, so nothing moves unless a human reads that document and wires the money.

---

## 3. The legal blocker

Stated precisely, and kept separate from everything else. **Three things may not ship.** Everything else in this report is money-wrong, expensive or embarrassing; these three are the ones that produce a document or a statement that is legally defective.

### 3.1 The VAT formula — the one that is not about currency, and the one I would fix first

`src/Cleansia.Core.AppServices/Services/VatCalculator.cs:24` computes `totalPrice * rate / (100 + rate)`, and its own docstring at `:22` says the rate is a percent ("500 Kč at 21% → 500 × 21 / 121"). The value it is handed is a **fraction**: `sql-scripts/insert_seed_data.sql:970` seeds CZE at `0.21` (SVK `0.20`, POL `0.23`, DEU `0.19`, AUT `0.20`).

**The seed is not the bug — the formula is.** I verified that the column physically cannot hold a percent: `src/Cleansia.Infra.Database/Migrations/20260908075819_Initial.cs:526` declares `StandardVatRate` as `numeric(5,4)`, maximum 9.9999. Seeding `21` would abort with a Postgres overflow. So the calculator can *never* receive the input its formula requires, and the fix must be in the formula. The other VAT path in the tree already uses the fraction convention correctly — `src/Cleansia.Infra.Services/Pdf/Models/CountryInvoiceContext.cs:37` computes `gross / (1 + rate)`.

I confirmed there is **zero test coverage**: `grep -rn "new VatCalculator("` over `src/` returns nothing outside `obj/`/`bin/`; all nine references are `Mock<IVatCalculator>`.

Dormant today solely because `insert_seed_data.sql:1110` sets `IsVatPayer = false`. **May not ship: VAT registration, in any market, while this formula stands.** Half a day to fix, including the tests it has never had. Independent of every currency decision below — do it regardless of what you decide about euros.

### 3.2 The customer receipt states one sale in two currencies

`src/Cleansia.Core.AppServices/Services/ReceiptService.cs:359-368` builds every line item from the raw catalogue columns — `s.Service?.BasePrice`, `s.Service?.PerRoomPrice`, `p.Package?.Price` — with no exchange-rate scaling, while `:375` sets `Total = order.TotalPrice` (scaled) and `:378` stamps `Currency = order.Currency?.Symbol` over both. `grep "ExchangeRate" ReceiptService.cs` returns nothing.

This is **live and unconditional** — the PDF is generated and uploaded at `ReceiptService.cs:132` regardless of any fiscal setting. On the first euro order it prints a €1,100.00 line under a €45.10 total.

**Two things the prior pass got wrong, in opposite directions.** First, the *fiscal-authority* version of this (`BuildFiscalRequest`, `:222-238`) is dormant three ways over, not one: `HandleFiscalAsync` returns early at `:174-177` when the enforcement mode is `None`, which is the default (`CountryConfiguration.cs:72`) and is never changed outside tests; `Fiscal:CzechEet2:Enabled` is `false` in all five hosts (e.g. `src/Cleansia.Web.Customer/appsettings.json:85`), so `FiscalServiceCollectionExtensions` never registers the real service; and that service is an unimplemented stub anyway. **Nothing is currently filed with any tax authority.** Second, and going the other way: the receipt lines already fail to sum to the total *at rate 1.0, in pure crowns, today*. Extras are rendered as bare names with no price (`ReceiptService.cs:369-373`), and the express surcharge and all three discounts get no line at all. A 950 Kč service plus a 200 Kč extra plus a 20% express surcharge prints one line of 950 against a total of 1,380, with 430 unaccounted for. **That is a defect worth fixing whether or not euros ever ship.**

**May not ship: a euro order that issues a receipt, until the lines and the total are in the same currency.**

### 3.3 The cleaner payout invoice asserts a currency it never verified

`src/Cleansia.Core.Domain/EmployeePayroll/OrderEmployeePay.cs` has no currency field — I grepped; there is nothing. Amounts are summed by `PayCalculatorExtensions.cs:30-61` from `EmployeePayConfig` rows that each *do* carry a required `CurrencyId` (`EmployeePayConfig.cs:34`) which is never read. `GenerateInvoice.cs:82-86` then resolves a currency from the employee's work country via `CurrencyResolutionService.cs:15-24` and stamps it onto the invoice, with nothing checking the two agree.

The unique index at `EmployeePayConfigEntityConfiguration.cs:97-99` is `(EmployeeId, ServiceId, PackageId)` with no currency dimension, so a second per-currency rate **cannot even be stored**.

**May not ship: a second serviced country, until the invoice currency is derived from the rows being invoiced rather than asserted from a person's geography.** This one is worth carving off and shipping ahead of everything else — it is triggered by flipping a country on, not by taking a euro.

### Not a legal blocker, though the prior pass called it one

The koruna baked into translated copy (all five locales at `cleansia.app/src/assets/i18n/*.json:238, 581, 1309`) is real and must be fixed, but there is no live misrepresentation: no customer is currently quoted a currency they are not charged in, and the terms-of-service line "prices are displayed in CZK" is true today. It becomes false the moment a euro order is possible. Treat it as launch scope, not as exposure.

---

## 4. The design decision that halves the cost

**This is the load-bearing decision. Everything else is downstream of it, and the work cannot usefully start before it is made.**

Two options. I recommend the second, and the margin is wider than the prior pass suggested.

### Option A — keep converting: one authored crown price × an exchange rate

**What exists today.** `OrderPricingCalculator.cs:64` reads `currency?.ExchangeRate ?? 1m` and `:76` multiplies the whole basket by it.

**What you would have to build, none of which exists:**

1. **A rate feed.** There is none, of any kind. I searched the entire `src/` tree for ČNB, cnb.cz, ECB, europa.eu, fixer, openexchange, and every rate-provider identifier I could think of: zero hits. The only rates in existence are twelve literals at `insert_seed_data.sql:525-536`. You would add an outbound HTTP client, a scheduled job, and an operational dependency you own at 3am.
2. **An effective-dated rate table with a source and a staleness rule.** `Currency.ExchangeRate` (`Currency.cs:19`) is one mutable scalar; `Update()` at `:31-37` overwrites it in place with no history, and `UpdateCurrency.cs:59-62` validates only `> 0`. You would need to decide what a quote does when the last fixing is three days old — refuse, or price on a stale number.
3. **A rate snapshot on the order.** `Order.cs` has no `ExchangeRate` property (grepped; nothing), and the Orders table has no such column. `OrderMappers.cs:63` and `:145` project the *live* rate. Without a snapshot, every historical euro order reports whatever the admin last typed.
4. **A charge-time freeze**, so a rate move between the quote and the Stripe redirect cannot change the price the customer already agreed to.

**And after all four, 1,100 Kč is still €45.10.** There is no place in this design to type a nice number. Your euro price list would read €45.10, €53.26, €32.76, and it would move every time someone touched a rate.

**Expandable to a third currency?** Type one number into an admin form and the entire catalogue appears in Swiss francs, priced at €102.459-style artefacts, with a rate nobody has checked and no local price sense at all. Cheap, instant, and wrong.

### Option B — author a price per currency ✅ **recommended**

**Shape:** three narrow tables, because `Service` has two money columns (`Service.cs:19, 21` — `BasePrice`, `PerRoomPrice`) while `Package.cs:34` and `Extra.cs:31` have one each. A single generic `(ItemType, ItemId, CurrencyId, Amount)` table cannot express base+per-room without a discriminator and loses its foreign key, so: `ServicePrices(ServiceId, CurrencyId, BasePrice, PerRoomPrice)`, `PackagePrices(PackageId, CurrencyId, Price)`, `ExtraPrices(ExtraId, CurrencyId, Price)`, each with a unique index on `(ItemId, CurrencyId)`.

> **Landmine.** `Service`, `Package` and `Extra` all derive from `Auditable`, which carries a nullable `TenantId`. Per `CLAUDE.md`, a unique index containing a null `TenantId` enforces nothing. These indexes must deliberately **exclude** `TenantId` — the catalogue is platform config, not tenant-scoped.

**Data to author — I counted this precisely, and the prior pass had it wrong.** The seed has 10 services, 8 packages, 5 extras. That is 10×2 + 8 + 5 = **33 price values per currency**, not the 23 the prior pass claimed. The 33 crown values already exist and move across unchanged; someone types 33 euro values by hand. 66 rows total.

**What this deletes outright.** `Currency.ExchangeRate` stops being read by the pricing path entirely — `OrderPricingCalculator.cs:64, 76` and the line-scaling below them all go. That removes the need for the rate feed, the effective-dated table, the staleness policy, the snapshot column and the charge-time freeze. Four pieces of machinery you never build, and one 3am dependency you never own.

**Missing price in a currency — the one real design question.** Recommend **fail closed**: filter the catalogue to items that have a price row in the requested currency, so an unpriced item is simply not offerable. This copies the precedent the team already ruled on for credit (`CancelUnfilledOrders.cs:268-276`, owner ruling 2026-09-06) and makes expansion honest — adding CHF shows an empty catalogue until someone types 33 numbers, which is the correct failure. Do **not** fall back to rate conversion; that reintroduces every problem of Option A on the least-tested path in the system.

**Expandable to a third currency?** 33 rows and **zero code**. That is the "easily expandable" you asked for, and it is the strongest single argument for B.

### The honest comparison

| | A — convert by rate | B — author per currency |
|---|---|---|
| Schema work now | 1 column on Order | 3 tables |
| New machinery to build | rate feed, rate history, staleness policy, charge freeze | none |
| Ongoing operational burden | a feed you own forever | none |
| What a euro price looks like | €45.10, moves when a rate is edited | €49, chosen by a human, stable |
| Reconciliation of a past order | needs a snapshot column you must add | trivially exact |
| Third currency | 1 number, prices are artefacts | 33 numbers, prices are prices |
| Rough cost | 4–6 days | 5–8 days |

A is one to two days cheaper at launch and buys a permanent operational liability. **B is the better trade after the first week.** It is also the only one of the two where the price on your website is a number a cleaning company would actually quote.

**Shared cost — identical under either option, and the part most likely to be forgotten.** The currency-blind thresholds and displays do not care which model you pick: the loyalty divisor (`LoyaltyService.cs:44`), the tier floor (`LoyaltyService.cs:232-233`, seeded to `1000.00` for all three tiers by the idempotent UPDATE at `insert_seed_data.sql:1663-1677`), the promo minimum (`PromoCodeService.cs:55, 131`), the refund fixed fee (`IssuePartialRefund.cs:224`), the receipt line items, the mixed-currency reports, and the ~26 hardcoded `'CZK'` formatters across 11 Angular files.

---

## 5. Ordered work plan

Sizes are for one developer. Dependencies are real — items in the same phase can run in parallel.

### Phase 0 — ship these now, independent of every decision (1.5 days)

| # | Work | Size | Why now |
|---|---|---|---|
| 0.1 | **Fix the VAT formula** — `VatCalculator.cs:24` to `totalPrice * rate / (1 + rate)`; fix the rate display at `DefaultReceiptLayoutBuilder.cs:320` (`:N0` renders 0.21 as "0%"); write the tests that class has never had, including 2,000 Kč @ CZ → 347.11 | 0.5d | Independent of currency. Turns from dormant into a 98.8% VAT under-declaration the moment `IsVatPayer` flips |
| 0.2 | **Stop reading `CurrencyId` from the caller** — `CreateOrder.cs:118-123` and `QuoteOrder.cs:151-156` | 0.5d | **Zero client changes needed**: web sends `null` (`order-pricing.facade.ts:204`), iOS sends `nil` (`QuoteClient.swift:25`), Android sends `null` (`BookingViewModel.kt:394, 561`); create just echoes the quote's resolved id back. Closes the exploit where any authenticated caller posts the HUF id (rate 16.2) and earns 16× the loyalty points |
| 0.3 | **Default-currency invariants** — enforce exactly one `IsDefault` row (there is no unique index on it today); fix the dead `??` guard at `CurrencyRepository.cs:11-13`, which binds to the `Task` rather than the result, so `GetDefaultAsync` can return a null that NREs at `CreateOrder.cs:478` | 0.5d | Latent 500 on the single most-travelled path |

### Phase 1 — the decision (0 days of code, blocks everything below)

Answer section 7's decisions 1 and 2. **Nothing in phases 2–4 can start before this.**

### Phase 2 — the pricing model (4–5 days, assuming Option B)

| # | Work | Size | Depends on |
|---|---|---|---|
| 2.1 | Three price tables + entities + EF configs + repositories; regenerate `Initial`; drop DEV | 1d | Phase 1 |
| 2.2 | Rewrite `OrderPricingCalculator` to join prices by currency; thread through `QuoteOrder`/`CreateOrder` | 1d | 2.1 |
| 2.3 | **Bind the order's currency to the address country.** The chain already exists and is seeded correctly — `CountryConfiguration.DefaultCurrencyCode`: CZE→CZK (`insert_seed_data.sql:969`), SVK→EUR (`:978`), DEU→EUR (`:996`). `CurrencyResolutionService.cs` already implements exactly this shape for payroll. `CreateOrder.cs:414` already holds the resolved, serviced address two lines before it resolves currency at `:420` | 0.5d | 2.2 |
| 2.4 | **Add a country/city input to `QuoteOrder.Command`** — it has none today, so currency cannot be derived on the quote path. Do *not* quote in the default and re-price at create; that shows one number and charges another, which is the exact defect `PriceMatchesAsync` exists to prevent | 0.5d | 2.3 |
| 2.5 | Admin per-currency price editors for services and packages | 1d | 2.1 |
| 2.6 | NSwag regeneration (all three clients) + re-dump both committed mobile specs | 0.5d | 2.4 |

> **Extras have no admin surface at all.** I checked: the only files under `Features/Extras/` are `GetExtraOverview.cs` and a DTO — no controller, no create/update command. Either extras stay seed-only in both currencies, or building an extras admin is new scope. **Say which.**

### Phase 3 — the currency-blind constants (2 days, parallel with Phase 2 after 2.1)

| # | Work | Size |
|---|---|---|
| 3.1 | Loyalty points normalisation (`LoyaltyService.cs:44`) and tier floor (`:232-233`) | 0.5d |
| 3.2 | Promo minimum (`PromoCodeService.cs:55, 131`) — the fixed-amount currency check at `:62-67` is already correct and stays | 0.25d |
| 3.3 | Refund fixed fee (`IssuePartialRefund.cs:224`) — the percentage half is currency-safe; only the flat `+ fixedFee` is wrong | 0.25d |
| 3.4 | Credit grant currency (`IssueCustomerCredit.cs:119-122`); make `EnsureForUserAsync` (`CreditAccountRepository.cs:17-34`) **refuse** rather than silently ignore a mismatched currency | 0.5d |
| 3.5 | Cleaner pay: add `CurrencyId` to `OrderEmployeePay`, widen the `EmployeePayConfig` unique index to include it, make `GenerateInvoice.cs:82-99` **derive** the invoice currency from the rows it is invoicing and fail if they disagree | 0.5d |

> 3.5 folds into the same `Initial` regeneration as 2.1 — do not do two schema regenerations.

### Phase 4 — display and reporting (5–6 days, parallel after 2.6)

| # | Work | Size |
|---|---|---|
| 4.1 | **Receipt line items** — build from the scaled/per-currency price, not the live catalogue (`ReceiptService.cs:246-261` and `:359-368`). Add the missing lines: extras, express surcharge, each discount. **This is the item that is broken in pure crowns today** | 1.5d |
| 4.2 | One shared currency-aware money formatter in Angular + convert 26 hardcoded `'CZK'` sites across 11 files (biggest: the two module constants in `order-wizard.models.ts` feeding ~30 render sites) | 2d |
| 4.3 | **Country picker in the web booking wizard.** The template has zero occurrences of "country"; `order-wizard.facade.ts:338` auto-selects only when exactly one country is served. `OrderAddressResolver.cs:107` returns `CountryRequired` otherwise. **Flipping a second country to serviced stops every inline-address web booking with a 400 until this ships** | 1d |
| 4.4 | Admin reports: group by currency, put currency on the DTO (`RevenueReportDto` has no currency field at all), stop stamping "Kč" in `reports.facade.ts:123-131` | 1d |
| 4.5 | Android: per-currency fraction digits in `OrderFormatters.kt:98-116`; three hardcoded Google Pay `currencyCode = "CZK"` sites | 1d |
| 4.6 | iOS: same fraction-digit rule + catalogue strings off literal "Kč" | 1d |
| 4.7 | i18n de-currencying: 4 keys × 5 locales in the customer web app, plus the iOS string catalogues | 0.5d |

### Phase 5 — launch data and verification (1–2 days)

Author 33 euro prices; author euro pay configs for any euro-country cleaners; flip SVK (or whichever) to `IsServiced` and seed its service cities; run the integration suite.

### What can be verified on this Windows machine

**Yes, fully:**
- `dotnet test` from `src/` against `Cleansia.Api.sln` — the unit suite (`Cleansia.Tests`) is pure and needs nothing external. This covers the VAT fix, the pricing calculator, loyalty, promo, refund and payroll logic.
- The 17 repo checkers under `agents/tools/check-*.mjs` — dependency-free Node, including the booking-policy parity gate that pins CZK-bound constants across three languages.
- Jest via Nx for the Angular work — **use `NX_DAEMON=false`**, since Bash-invoked nx runs poison the daemon with drive-letter casing and kill a running `nx serve`.
- The `Initial` regeneration and the DEV drop, per `CLAUDE.md`.

**Yes, with Docker Desktop running:**
- `Cleansia.IntegrationTests` — `PostgresContainerFixture.cs:15` spins up `postgres:latest` via Testcontainers and builds a real database from the migration. **This is the only thing that proves the model and the schema agree**, and it is what must go green after the regeneration.

**Partially:**
- Android — needs a JDK and Gradle; compiles and unit-tests on Windows, but you cannot exercise the Google Pay sheet without a device or emulator.

**No — and this is the real gap:**
- **iOS cannot be verified on this machine at all.** XcodeGen, SPM and `swift build` require macOS. Items 4.6 and the iOS half of 4.7 ship blind from here. Budget for that: either a Mac in the loop, or accept that the iOS currency formatting is unverified until someone runs it.

---

## 6. What is NOT needed — deletions from the prior pass

Being able to remove work matters as much as adding it. **The prior pass over-scoped in six places.**

**❌ A ČNB or ECB rate feed.** The prior pass listed this as required. Under Option B it does not exist as a concept — the exchange rate leaves the pricing path entirely. Even under Option A it is only required if you keep conversion. **Delete it from the plan the moment you choose B.** That is the single largest deletion available, and it takes the effective-dated rate table, the staleness policy and the "what happens when the feed is down" decision with it.

**❌ An effective-dated `CurrencyRate` history table.** Same reasoning. Under B, there is nothing to version.

**❌ Extending `FiscalReceiptRequest` with base-currency restatement fields, and the whole CZ EET 2.0 payload discussion.** The prior pass rated this "legal" and urgent. It is neither. Nothing is filed with any authority: the enforcement mode defaults to `None` and is never changed outside tests; `CzechEet2:Enabled` is `false` in all five hosts; and the service is an explicit stub that returns `NOT_IMPLEMENTED` without reading the payload. The prior pass also claimed extending the record is "a breaking change to every provider implementation" — **that is false**. `IFiscalService` takes the record as a *parameter*; implementations consume it and compile unchanged. There are exactly three construction sites (one production, two tests). This is roughly an hour of work whenever EET 2.0 actually lands in 2027. **Defer it entirely.**

**❌ A per-currency minor-unit exponent on `Currency`.** `StripeClient.cs:41-42` hardcodes `× 100`. That is **correct for CZK and correct for EUR** — both are two-decimal at Stripe. It is a trap only for zero-decimal currencies (HUF, JPY) or three-decimal ones. **Not launch scope.** The right move is Phase 5's deactivation of the ten currencies nobody has priced, which removes the exposure without writing any code.

**❌ Precision fixes on `NetAmount` / `VatAmount` / `AppliedVatRate`.** Real (they are bare `numeric` while 38 sibling money columns are `numeric(18,2)`), worth a line in a future ticket, and irrelevant to whether you can take a euro. **Not a blocker.**

**❌ "Audit everything" / a repo checker for bare money constants.** The prior pass proposed a new CI gate. Proportionality: the constants are enumerated in this document, there are seven of them, and once they carry a currency the gate guards nothing that a compile error would not. **Don't build it.** Revisit if a second one appears after the fix.

**And one correction that removes urgency rather than work:** the cleaner-invoice defect does **not** fire today. `Employee.WorkCountryId` is only written by `ApproveEmployee.cs:228`, whose validator at `:118-124` requires the country to be serviced, and exactly one country is serviced in the seed (`insert_seed_data.sql:140`, CZE). Every cleaner resolves to crowns today and every payout PDF is currently correct. It fires on the first day of the euro launch, not now. Still ship it in Phase 0/3 — but you are not bleeding.

---

## 7. The open decisions

Each with options, consequences and a recommendation. **Decisions 1 and 2 block all code below Phase 0.**

### Decision 1 — Converted catalogue, or per-currency authored prices?

*The load-bearing one. Section 4 has the full comparison.*

- **A: keep converting.** 4–6 days now; buys a rate feed you operate forever, a staleness policy, a snapshot column, a charge freeze, and prices that read €45.10.
- **B: author per currency.** 5–8 days now; 33 numbers per currency; deletes the exchange rate from the money path.

> **Recommend B.** One to two days more at launch, and it removes four systems you would otherwise own permanently. It is also the only option where "easily expandable to a third currency" means *typing 33 prices*, rather than *typing one rate and hoping*.

### Decision 2 — Who picks the currency: the address country, or the customer?

- **Address country.** Everything needed already exists and is seeded correctly. Currency and VAT then agree *by construction*, because both derive from the same `CountryConfiguration` row. No client needs a currency picker at all.
- **Customer choice.** Requires a served-currency list, a refusal path, new UI in all three booking wizards — and it lets a customer pick whichever currency a stale rate makes cheaper. It is also the field that exists today and is what makes the loyalty exploit reachable.

> **Recommend address country, firmly.** Deriving currency from anything other than the row that also decides VAT lets a euro-priced order carry Czech VAT.
>
> **Consequence you must accept:** the address step has to move *before* the price step in all three booking flows, and `QuoteOrder.Command` gains a country field (item 2.4). That is a wire-contract change to both committed mobile specs and all three generated clients.

### Decision 3 — Which second country goes live?

- **Slovakia** is already fully configured for euros at `insert_seed_data.sql:975-982`: EUR, `sk`, Europe/Bratislava, +421, 20% VAT, IČO/IČ DPH labels, Stripe, payout scheme 1. Slovak is already a selectable UI language and `sk` is already whitelisted in the address autocomplete.

> **Recommend Slovakia.** Cheapest by a wide margin. Still needs `Country.IsServiced` flipped, its service cities seeded, and cleaners with euro pay configs — and item 4.3 (the web country picker) **must ship before that flip**, or every inline-address web booking returns a 400.

### Decision 4 — Loyalty across currencies

- **(i) Normalise to a base unit** before the `/10` — needs a rate, which partly defeats Option B.
- **(ii) Per-currency divisor** on `LoyaltyTierConfig` — 10 for CZK, 0.4 for EUR, authored like a price.
- **(iii) Currency-free unit** — points per completed booking.

> **Recommend (ii).** Consistent with per-currency authored data, needs no feed, and nothing changes for existing Czech customers (rate 1.0 today), so **no restatement of existing balances is needed**. The tier floor (`MinimumOrderAmountForDiscount`, currently `1000.00` uniformly) gets the same per-currency treatment.
>
> Note: whichever way this goes, the customer-facing copy changes too — the rule is stated verbatim in five locales.

### Decision 5 — Credit: per-currency accounts, or converted at spend?

Today it is neither: one account, always opened in the platform default, silently unspendable elsewhere. `CreditAccount.cs:47-50` explicitly deferred this to an owner ruling.

> **Recommend: one account per customer per currency, and make the grant carry a currency.** The admin dialog already displays the account's currency, so the admin is not blind — the surviving harm is purely stranding. Also decide whether the 250 no-show apology gets a euro figure or stays crown-only; today the platform correctly pays nothing on a non-default-currency order, which is safe but breaks a promise made on the home page.

### Decision 6 — Is Plus sold in euros at launch?

`MembershipPlan.MonthlyPriceCzk` (`MembershipPlan.cs:47`) has the currency in its field name, the wire DTO carries no currency at all, and there is exactly one `StripePriceId` per plan. Seven independent formatters across four apps hardcode "Kč" — including one in the **admin** app the prior pass missed.

- **Yes:** a second Stripe Price per plan, a currency on the plan, renaming `MonthlyPriceCzk` off the wire.
- **No:** a Slovak customer is billed in crowns for Plus and euros for everything else — two currencies from one platform on one card statement.

> **Recommend: no for launch, but say it on the page.** Defensible only if stated. The dangerous middle path is shipping euros *and* letting an admin type a euro number into a field called `MonthlyPriceCzk` with seven formatters that will render it "7,99 Kč" against a €7.99 recurring charge, and no validator objecting. **If Plus goes euro, it goes properly or not at all.**

### Decision 7 — Extras: build an admin CRUD, or stay seed-only?

No controller, no create/update command exists. Five extras, seed-only today.

> **Recommend: stay seed-only**, author the euro prices in the seed alongside the crown ones. Building an extras admin is new scope that multi-currency does not require.

### Decision 8 — Do the ten unused seeded currencies stay active?

USD, GBP, PLN, CHF, SEK, NOK, DKK, HUF, RON, BGN are all seeded `IsActive` with hand-typed rates nobody has reviewed.

> **Recommend: deactivate all ten.** Launching CZK+EUR does not require them, and each is an exploit surface today. Zero code, one data change.

---

## What I could not check, and where I am uncertain

- **I did not run the build or any test.** This pass was read-only. Every claim is from source, seed and migration files.
- **I do not know what is in the production database.** `insert_seed_data.sql` is a development fixture — `.github/workflows/execute-sql.yml:53` explicitly blocks it from PRO. So the rates, country configs, membership plans, pay configs, `IsVatPayer` flag and `RefundStripeFixedFee` values in production are **unknown to me**. Every number I quote is a dev-seed number, illustrative of shape rather than of production value. The 0.041 euro rate in particular may or may not exist in production at all. **Someone should read the production `Currencies`, `CountryConfigurations` and `CompanyInfo` rows before this plan is finalised** — the VAT-payer flag especially, since it alone decides whether item 0.1 is urgent or merely important.
- **I did not check Stripe.** What Prices exist, in what currencies, and whether the Czech account can even settle EUR, is outside the repository. **That is a real prerequisite for a euro launch and nobody has verified it.**
- **The iOS count is approximate.** I did not exhaustively enumerate the `.xcstrings` entries; the prior pass's "~70" is plausible but I could not confirm it, and I cannot compile iOS on this machine regardless.
- **Sizing confidence.** Phase 0 and Phase 3 I am confident about — they are small, enumerated changes. Phase 4.2 (the Angular formatter sweep) is the one I would most expect to overrun: 26 call sites across 11 files, and the wizard's two module constants feed roughly 30 render sites in templates I only sampled.

---

## Bottom line for the decision you are making

The euro path has never once run. Nothing here is a regression and no customer is being harmed today — it is a feature that was scaffolded and never finished. **Finishing it is 2–3 weeks across the backend, the database, three client apps and five locales, and it cannot usefully start until you answer decisions 1 and 2.**

Three things I would do this week regardless of what you decide about euros: **the VAT formula** (half a day, and it is a 98.8% under-declaration waiting on a single flag), **stopping the API from accepting a caller-supplied currency** (half a day, zero client changes), and **the receipt lines that already fail to sum to their own total in pure crowns**.

**On [T-0688](tickets/T-0688-multicurrency-is-a-mechanism-that-has-never-run.md).** Its INDEX row already
warns that "that decision changes the size by an order of magnitude", and this pass confirms it: sized `M`,
it is a multi-week body of work. It also does not mention the VAT convention, the cleaner payout invoice,
the receipt line items or the loyalty normalisation at all — four of the findings above are outside it.
**Re-scoping or splitting it is a decision for you, not something this pass took.**