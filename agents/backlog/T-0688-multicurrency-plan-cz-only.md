# Multicurrency, Czechia only — the plan that replaces Part 3

**2026-09-08. This is the live plan.** It supersedes Part 3 of
[`T-0688-multicurrency-design.md`](T-0688-multicurrency-design.md) — the seven-wave plan, Steps 0–18.
Parts 1 and 2 of that document stand as written except where corrected below.

**What changed to cause this.** The owner settled decision 2 as recommended (price list pinned to the
service address, market selector on address-less surfaces, no settlement picker), confirmed Stripe
Adaptive Pricing is **on**, and pulled Slovakia and Poland out of scope: *"it's still a question if we
ever will… don't be stuck implementing solution for them as well."*

Four research lanes fed this, three of them adversarially verified. **Two of the corrections land on my
own earlier work and are marked in the text**: the prescribed Adaptive Pricing fix was the wrong lever,
and the gross-vs-margin turnover gap was overstated by roughly 3×.

Every code claim was re-verified against the tree after commit `56ad4d45`, which shipped the VAT
convention fix — so **Wave 1 Step 1 of the old plan is done and must not be re-dispatched.**

**Eighteen numbered steps become nine. Seven waves become three. No Mac session, no NSwag
regeneration, and no mobile spec re-dump appear anywhere in this plan.**


> **§§3–5 superseded 2026-09-08 — see
> [`T-0688-multicurrency-plan-final.md`](T-0688-multicurrency-plan-final.md).** The owner ordered
> Option B (per-currency price tables built now, before production), overruling this document's
> recommendation to defer — and his argument was right: the deferral case assumed the retrofit cost is
> flat pre-production, but the retrofit would land *after* production exists.
> **§§1–2 below stand unchanged** — Adaptive Pricing, the drafted accountant letters and the EU SME
> answer are all still current.

---

## 1. Adaptive Pricing is on — what that means and what to do

### Is anyone being charged euros today?

**No — because nobody is being charged anything.** There is no production (`deploy-pro.yml`: one run, cancelled). Stripe is sandbox-only. Not one real customer has ever been charged in any currency, so there is no accounting incident, no refund to issue, and no disclosure to make. The repo cannot tell you what Stripe *presented* on any session — `grep -rn "presentment\|PresentmentDetails\|AmountTotal"` across `src/` returns **zero hits**, so nothing reads it and nothing stores it — but that question is moot while the answer to the prior question is "nobody".

What this actually is: **a hard gate on first live traffic that no code change can undo afterwards.** That is the reason to fix it now, and it is a better reason than "incident".

### What is exposed

Both web Checkout Sessions, neither of which sets the flag:

- **Order charge** — `StripeClient.cs:44`, options at `:51`, `PaymentMethodTypes = ["card"]` at `:53`, inline `PriceData` with `Currency = order.Currency.Code.ToLower()` at `:60`, `Mode = "payment"` at `:70`. Callers: `OrderPaymentDispatcher.cs:60`, `ResumeOrderCheckout.cs:141`.
- **Cleansia Plus** — `StripeClient.cs:390`, options at `:412`, `Mode = "subscription"` at `:414`, `Price = stripePriceId` at `:420`, against a CZK-only seeded Price (`insert_seed_data.sql:1752`, `:1768`). Caller: `CreateMembershipCheckoutSession.cs:114`. **The Plus path is exposed, and it is the worse of the two** — a converted subscription's currency locks until cancellation and re-fetches real-time FX every billing cycle. Disabling Adaptive Pricing does not unwind one.

`grep -n "Currency\|AdaptivePricing"` over that entire file returns exactly two hits: `:60` and `:170`. There is no top-level `SessionCreateOptions.Currency` and no `AdaptivePricing` anywhere.

Not exposed: the mobile `PaymentIntent` path (`StripeClient.cs:158-182`, `Currency` at `:170`) — Adaptive Pricing does not reach the Payment Intents API. The customer web app holds no Stripe.js at all (`grep -rn "stripe-js"` across the Angular tree returns nothing), so web checkout is a plain redirect to Stripe-hosted Checkout, which is precisely what the feature applies to.

### The fix

**The design document's prescribed fix is the wrong lever, and this correction stands after two adversarial passes.** Invariant 17 (`T-0688-multicurrency-design.md:551`) says "Every Stripe Checkout Session and Subscription sets `Currency` explicitly." Setting `SessionCreateOptions.Currency` does **not** disable Adaptive Pricing: Stripe's Restrictions section is a closed two-item list (a price whose `currency_options` already covers the buyer's local currency, and `capture_method: manual`) and `currency` is not on it; Stripe separately states Adaptive Pricing *requires* the session currency to be a settlement currency — it is the **from** currency, not an off-switch. The "specify a currency to override" text belongs to the manual multi-currency-prices mechanism, which neither Cleansia session uses.

The correct lever, in both option blocks:

```csharp
AdaptivePricing = new SessionAdaptivePricingOptions { Enabled = false },
```

`SessionCreateOptions.AdaptivePricing` and `SessionAdaptivePricingOptions.Enabled` both exist in **Stripe.net 50.4.0**, the version pinned at `src/Directory.Packages.props:69` — verified in the packaged XML docs at `Stripe.net.xml:12852` and `:13036`. No SDK upgrade, no migration, no wire change.

One honest caveat: `adaptive_pricing.enabled` is documented only in the affirmative ("If set to `true`, Adaptive Pricing is available on eligible sessions. Defaults to your dashboard setting"). The `false` branch is nowhere described. Near-certainly the complement, but assert it in sandbox rather than assume it.

### Dashboard as well?

**Yes — dashboard first, then the code, and keep both.** The toggle covers what code cannot: **Payment Links, where Adaptive Pricing is always on and cannot be disabled**, plus Hosted Invoices and any Session created outside `StripeClient`. Code-only leaves those open. Dashboard-only is invisible to CI and one click from being flipped back.

**Do not add a test for the two properties.** The options are built inline inside an async method that calls Stripe; asserting them means extracting an options-builder seam, which is a new abstraction paid by every future reader to guard two literals in one file with two call sites. Proportionality says no. The dashboard toggle is the primary defence; the two properties are defence in depth; a two-line comment naming both is the record. (This corrects the lane that recommended a test.)

### Owner actions, in order

1. **Turn Adaptive Pricing off in the Stripe dashboard**, sandbox now and live the day it exists.
2. **Check Dashboard → Balances: is CZK a settlement currency?** If it is not, no session could ever have been converted and this whole finding is theoretical. Thirty seconds, and it should be checked first.
3. **Check whether any Payment Links exist on the account.** If any do, they are a permanent hole neither fix closes.
4. **Supply a sandbox key via user secrets** (every `Stripe:SecretKey` in the tree is a placeholder — `Cleansia.IntegrationTests/appsettings.IntegrationTests.json:24` is `"sk_test_your_secret_key"`), or run three curls yourself. Create the order session three ways with `customer_email = "test+location_DE@example.com"` — bare, with `currency=czk`, and with `adaptive_pricing[enabled]=false` — and open each URL. Stripe's location-formatted email shows you what a German customer sees. If the second presents CZK, the correction above is wrong and the design document's lever was fine. If the second presents EUR and the third presents CZK, ship as written. **Neither research pass executed anything against Stripe; the whole question rests on documentation until this runs.**

---

## 2. The two questions you asked me to explain

### 2.1 Gross vs commission turnover

**The question in one sentence.** You register for Czech VAT when your *obrat* crosses CZK 2,000,000 in a calendar year (payer from 1 January following) or CZK 2,536,500 (payer the next day). §4a ZDPH defines obrat as the consideration due to you *for the supplies you make*. So: what supply do *you* make? If you sell a clean and buy the labour, your obrat is the whole customer price. If you broker a clean between customer and cleaner, it is only what you keep.

**Worked, from your actual seeded numbers.** General Cleaning, 3 rooms + 1 bathroom:

- Customer pays `BasePrice 500 + PerRoomPrice 150 × (rooms + bathrooms)` = **1,100 Kč**. Prices at `insert_seed_data.sql:573-574`; formula at `OrderPricingCalculator.cs:33`.
- Cleaner is paid `BasePay 250 + max(0, rooms − 1) × ExtraPerRoom 75` = **400 Kč**. Config seeded at 50% of catalogue (`insert_seed_data.sql:753`); formula at `PayCalculatorExtensions.cs:12-16`.
- You keep **700 Kč**.

| | Gross reading | Margin reading |
|---|---|---|
| Cleans to reach 2,000,000 | **1,819** | **2,858** |
| Cleans to reach 2,536,500 | **2,306** | **3,624** |
| Customer money before the 2m line | 2,000,000 Kč | ~3,143,000 Kč |

**Correction to the design document**, which said the gap is "roughly 4–5×" (`T-0688-multicurrency-design.md:209`). On your real rate card it is **1.6× to 2×**. Still 1,000–1,500 cleans of headroom, still worth settling — but quote the right number, because the accountant will check the arithmetic.

**What the code says.** Eight indicators, all pointing to principal: the cleaner's pay never reads `order.TotalPrice` and the two formulas have different shapes (price counts `rooms + bathrooms`, pay counts `rooms − 1`); no commission concept exists anywhere (`grep platformFee|serviceFee|bookingFee|commission|takeRate` over `src/` → zero); the self-billed invoice names the cleaner *Dodavatel* and Cleansia *Odběratel* (`DefaultInvoiceLayoutBuilder.cs:12-14`); cleaners carry their own IČO, DIČ and IBAN (`Employee.cs:16,19,24`); you set the customer price and it does not vary by cleaner (`OrderPricingCalculator.CalculateAsync` takes no employee argument); the receipt is yours for the full amount (`ReceiptService.cs:374`); the whole payment lands on your Stripe balance (no `TransferData`, no `ApplicationFee`, no Connect anywhere in the 506-line `StripeClient.cs`); and refunds never reduce cleaner pay (`grep OrderEmployeePay` over `Features/Refunds/` → nothing).

**But the code is not the legal test.** §4a turns on the written contracts, and I could not find a cleaner contract in the repository at all. Your customer terms carry an explicit draft warning at `cs.json:1747` ("Návrh — toto znění zatím neprošlo právní kontrolou"). Give the accountant the signed cleaner agreement, the insurance policy, and confirmation that no cleaner is legally an employee — he will ask.

**One thing to hand him at the same time:** `cs.json:1753` tells customers "Ceny jsou uvedeny v CZK a zahrnují DPH" while the seeded company is `IsVatPayer = false` (`insert_seed_data.sql:1110`). A neplátce must not say prices include VAT.

---

**EN — Subject: VAT turnover — do we count gross bookings or only our margin?**

> Hello,
>
> We run Cleansia, a cleaning-services platform in Czechia. One thing needs settling because it decides when we must register for VAT.
>
> **The question:** For §4a ZDPH (obrat) and the §6 registration thresholds of CZK 2,000,000 / 2,536,500, is our turnover (a) the full amount the customer pays us for the clean, or (b) only the part we keep after paying the cleaner?
>
> **The facts, so you can answer without a call:**
> 1. The customer books through our website or app and contracts with Cleansia. Our terms state that Cleansia provides the cleaning service. We issue the customer a receipt in Cleansia's own name for the full amount.
> 2. Cleaners are independent contractors (OSVČ or s.r.o.), each with their own IČO; some are VAT-registered, some not. They are not our employees.
> 3. Cleaners invoice **us**, not the customer. We generate the invoice on their behalf (self-billing) and pay them by bank transfer. The customer never sees the cleaner's price and never pays the cleaner.
> 4. **We set the customer's price** from our own price list. The price is the same whichever cleaner takes the job.
> 5. **We set the cleaner's pay separately**, from our own rate card, in absolute amounts (a base amount per job plus an amount per extra room). It is not a percentage of the customer's price, and the customer's price is not an input to it. On a typical job the customer pays 1,100 CZK and the cleaner is paid 400 CZK.
> 6. **We charge no commission, platform fee or booking fee.** No such charge exists — we buy labour at our price, sell a clean at our price, and keep the difference.
> 7. **The customer's whole payment comes to us**, onto our own Stripe account and our own bank account. Nothing is split or routed to the cleaner at payment time.
> 8. **We carry the commercial risk.** If a customer complains, we refund from our own money and the cleaner is still paid in full. Our terms say Cleansia handles damage claims.
> 9. If a cleaner cancels, we find a replacement; the customer's contract with us is unchanged.
>
> **Why it matters:** at 1,100 CZK per clean we cross CZK 2,000,000 at about 1,819 cleans on reading (a), but only at about 2,858 on reading (b).
>
> **Please confirm in writing:** which of (a) or (b) applies and on what basis; which of facts 1–9 would have to change for the other answer to apply (i.e. what would make us a zprostředkovatel rather than a principal); and whether §4 odst. 1 ZDPH / Art 28 of Directive 2006/112/EC (commissionaire structure) is relevant here.
>
> Thank you.

**CZ — Předmět: Obrat pro DPH — počítá se celá cena objednávky, nebo jen naše marže?**

> Dobrý den,
>
> provozujeme Cleansia, platformu pro úklidové služby v ČR. Potřebujeme vyjasnit jednu věc, protože určuje, kdy se musíme registrovat k DPH.
>
> **Otázka:** Rozumí se pro účely § 4a ZDPH (obrat) a limitů pro registraci podle § 6 ZDPH (2 000 000 Kč / 2 536 500 Kč) naším obratem (a) celá částka, kterou nám zákazník za úklid zaplatí, nebo (b) pouze část, která nám zůstane po vyplacení uklízeče?
>
> **Skutkový stav, abyste mohl(a) odpovědět bez dalšího dotazování:**
> 1. Zákazník objednává přes náš web nebo aplikaci a uzavírá smlouvu s Cleansia. Naše obchodní podmínky uvádějí, že úklidové služby poskytuje Cleansia. Zákazníkovi vystavujeme doklad vlastním jménem na celou částku.
> 2. Uklízeči jsou samostatní dodavatelé (OSVČ nebo s.r.o.), každý s vlastním IČO; někteří jsou plátci DPH, jiní ne. Nejsou našimi zaměstnanci.
> 3. Uklízeči fakturují **nám**, nikoli zákazníkovi. Fakturu vystavujeme jejich jménem (samofakturace) a hradíme bankovním převodem. Zákazník cenu uklízeče nikdy nevidí a uklízeči nic neplatí.
> 4. **Cenu pro zákazníka určujeme my** podle vlastního ceníku. Cena je stejná bez ohledu na to, který uklízeč zakázku převezme.
> 5. **Odměnu uklízeče určujeme samostatně** podle vlastního sazebníku, v absolutních částkách (základní částka za zakázku plus částka za každou další místnost). Není to procento z ceny pro zákazníka a cena pro zákazníka do výpočtu nijak nevstupuje. U typické zakázky platí zákazník 1 100 Kč a uklízeč dostane 400 Kč.
> 6. **Neúčtujeme žádnou provizi, poplatek platformy ani zprostředkovatelskou odměnu.** Nic takového neexistuje — práci nakupujeme za svou cenu, úklid prodáváme za svou cenu a rozdíl si necháváme.
> 7. **Celá platba zákazníka jde nám**, na náš vlastní účet u Stripe a náš bankovní účet. Při platbě se nic nedělí ani neposílá uklízeči.
> 8. **Obchodní riziko neseme my.** Pokud zákazník reklamuje, vracíme peníze z vlastních prostředků a uklízeč dostane zaplaceno v plné výši. Podle našich podmínek řeší škody Cleansia.
> 9. Pokud uklízeč zakázku zruší, hledáme náhradu; smluvní vztah zákazníka s námi se nemění.
>
> **Proč na tom záleží:** při 1 100 Kč za úklid překročíme 2 000 000 Kč přibližně po 1 819 zakázkách při variantě (a), ale až po 2 858 zakázkách při variantě (b).
>
> **Prosím o písemné potvrzení:** která z variant (a) nebo (b) platí a na jakém základě; která ze skutečností 1–9 by se musela změnit, aby platila varianta druhá (tj. co by z nás udělalo zprostředkovatele místo osoby jednající vlastním jménem na vlastní účet); a zda je zde relevantní § 4 odst. 1 ZDPH / čl. 28 směrnice 2006/112/ES (komisionářská struktura).
>
> Děkuji.

### 2.2 The EU SME "EX" number — **it has dropped off your list. Delete it.**

**Plainly: do not spend an accountant call on this.** From 1 January 2025 a small business can be VAT-exempt not only at home but in other EU member states under a Union-wide €100,000 cap, using an identification number suffixed "-EX". Its entire value to you was letting a Slovak launch run with no Slovak VAT and no OSS return. You are not launching Slovakia.

And the one part that *was* open is now closed without an adviser. Finanční správa, verbatim: *"osoba povinná k dani se sídlem v tuzemsku, bez ohledu na to, jestli je v tuzemsku plátcem nebo neplátcem, získává možnost při splnění podmínek využívat přeshraniční režim pro malé podniky ve zvoleném jiném členském státě"* and *"Stát usazení při registraci do režimu přidělí osobě povinné k dani speciální identifikační číslo, tzv. EX ID."* — so yes, a Czech company can get one. The same page also says *"Vnitrostátní uplatňování režimu pro malé podniky v České republice … zůstává u osob povinných k dani zde usazených zachováno bez nutnosti jakékoli registrace."* **The mechanism is purely cross-border and has zero domestic relevance.** (https://financnisprava.gov.cz/cs/mezinarodni-spoluprace/mezinarodni-spoluprace-a-dph/preshranicni-rezim-dph-pro-male-podniky-eu-rezim-sme/zakladni-charakteristiky-preshranicniho-rezimu-male-podniky)

The one line to carry forward, for the day a second market is real:

- **EN:** "Can we supply Slovak customers under the cross-border EU SME scheme with an EX number issued by the Czech tax office, exempt from Slovak VAT and with no OSS return, and what are the current thresholds and conditions?"
- **CZ:** "Můžeme dodávat slovenským zákazníkům v přeshraničním režimu pro malé podniky (režim SME) s EX ID přiděleným českým finančním úřadem, tedy osvobozeně od slovenské DPH a bez podání OSS, a jaké jsou k tomu aktuální limity a podmínky?"

### 2.3 Fixed establishment — recognise it, don't resolve it

A *fixed establishment* is defined by Implementing Regulation 282/2011 Art 11(2): *"any establishment … characterised by a sufficient degree of permanence and a suitable structure in terms of human and technical resources to enable it to provide the services which it supplies."* Art 11(3): *"The fact of having a VAT identification number shall not in itself be sufficient."* In practice you would create one abroad by putting something permanent and structural on the ground — a local coordinator, an office, a depot, stored equipment — not by sending contractors across a border. It matters because a fixed establishment ejects you from OSS into a full local registration, turning a light operation into a heavy one. **You do not need to resolve this now. You need to recognise the moment:** the first time someone proposes "let's rent a small place in Bratislava" or "let's hire a local coordinator", that is a tax decision, not an ops detail. Your uncertainty about it is correctly parked as an accountant question for that day.

*Not tax advice. Everything above is quoted from Finanční správa, EUR-Lex and ZDPH with links, and from your code with file:line, so a daňový poradce can answer quickly.*

---

## 3. CZK and EUR: the thing you have asked for twice that cannot both be true

**The contradiction, without hedging.** You said: "I want to have multiple currencies at launch. Means that we shall have CZK and EURO, so it has to be working." You also settled that the price list is pinned to the service address country, and that Czechia is the only market. Under those two rulings an order's currency is a function of the property's country, exactly one country is serviced (`insert_seed_data.sql:140` is the only `Countries` row with `IsServiced = true`; every `ServiceCities` row is CZE), and `OrderAddressResolver.cs:98-110` resolves that single country silently. **Every order is CZK. EUR is unreachable by construction.** "CZK and EUR both working at launch" and "Czechia only" cannot both hold. Nobody has reconciled them, and the reconciliation is yours.

**One thing must be said before the options.** The *only* route by which a Cleansia customer can currently be charged euros is a defect, and there are two of them. Stripe Adaptive Pricing is one (§1). The other is in your own code: `CreateOrder.cs:118-123` and `QuoteOrder.cs:151-156` accept a caller-supplied `CurrencyId` and validate **only that the currency exists** — no check against the address country, no active check. Post the EUR id against a Prague address and `CreateOrder.cs:420-422` resolves EUR, then `OrderPricingCalculator.cs:64` reads `currency?.ExchangeRate ?? 1m` and thirteen sites (`:78, :106, :109, :114, :115, :132, :135, :143, :144, :145, :148`) multiply the CZK catalogue by the hand-typed **0.041** at `insert_seed_data.sql:526`. That is a ~24× underpayment, reachable today by any authenticated caller. **"EUR works today" is true in the worst possible sense.** Turning both off does not create the "EUR is unreachable" problem; it makes it visible.

### Is there a legitimate EUR-on-a-Czech-property case?

**No B2B customer domain exists.** `UserProfile.cs` has three members — Customer, Employee, Administrator. `Order.cs:18-29` carries `CustomerName/Email/Phone/AddressId` and no payer entity, company name, registration number or VAT id; a grep of the whole Orders domain for `companyname|businessname|vatnumber|b2b|isbusiness|billingaddress` returns nothing real. `ReceiptPdfData` has no *odběratel* block.

**But the cost of saying yes later is lower than the earlier passes claimed, and this correction wins.** The lane asserted "the platform cannot issue a B2B tax document at all today, in any currency". That is false. `InvoicePdfData` is a complete two-party Czech tax document — `Supplier` with `RegistrationNumber`/`VatNumber`/`IsVatPayer` (`:47-49`), a `Company` block documented at `:29-30` as "the CUSTOMER of a payout invoice", `VariableSymbol`/`ConstantSymbol` (`:6-7`), `VatAmount` (`:19`), a Czech layout builder, the full label set (Faktura / Dodavatel / Odběratel / IČ / DIČ / DPH), and — decisively for this question — **`CurrencyCode` and `CurrencySymbol` per invoice** (`:21-22`). It runs cleaner→Cleansia, not customer→Cleansia. So a B2B customer invoice is a re-pointing of an existing machine, not a greenfield build.

Two things still argue against building it on speculation: nobody has named a prospect, and a EUR invoice for a Prague job still owes 21% Czech VAT **restated in CZK** (VAT Directive Art 230; Czech §29(1)(l) ZDPH: *"výši daně; tato daň se uvádí v české měně"*) — which drags back the exchange-rate feed ruling 1 deleted. That cost is dormant today (`IsVatPayer = false`) and arrives at VAT registration.

### The options

| | What you get | What it costs | What it forecloses |
|---|---|---|---|
| **A. CZK only is the truth.** Close both EUR routes, write the target model down, do not build it. | An honest platform. Nothing hardcodes a wrong currency assumption. | Nothing. It is net deletion. | "EUR at launch" as a claim. |
| **B. Build the three price tables now, seed CZK only, prove EUR in an integration test.** | EUR is a data-authoring session away, demonstrably. | `ServicePrices`/`PackagePrices`/`ExtraPrices`, delete four price columns, fail-closed pricing calculator, seed rewrite, admin per-currency price editors. | Nothing, but it is the largest single body of work in the old plan. |
| **C. Seed EUR and flip a second country on in DEV so EUR is clickable.** | A euro you can click. | All of B, plus the web country picker (`OrderAddressResolver.cs:104-108` returns `CountryRequired` the moment a second country is serviced, and the wizard has no country control), plus a second market's cities, plus Slovak/Polish specifics you told us not to build. | Your own scope ruling. |
| **D. EUR override for a B2B/corporate payer.** | A real answer for a named prospect. | A company payer on `Order`, a customer-side tax-document layout, and the Art 230 CZK VAT restatement with a rate feed and rate date. | Ruling 1's deletion of the rate feed. |
| **E. Leave Adaptive Pricing on and call it "multi-currency".** | Nothing. | An undisclosed 2–4% surcharge, a receipt that never matches the card statement, no record of what was paid, and permanently FX-drifting Plus subscriptions. | Your credibility. Rule it out. |

### Recommendation: **A.**

**Defend it on three grounds.**

*The retrofit argument that normally justifies building early does not exist here.* The standard case for B is "a live table cannot be restructured cheaply later". You have no production, one committed migration regenerated rather than stacked, and a DEV drop that is routine. **The schema change costs the same whenever it is paid.** Only ten files read `.BasePrice`/`.PerRoomPrice` outside tests, and several are writers. That is the whole reader set, enumerable today.

*Wave B shrinks it further, and Wave B is justified without EUR.* Once every order line carries its own snapshot price (§4), the live catalogue is read only at quote time. The historical readers — the receipt builder, the fiscal request, the refund allocator — stop reading the catalogue at all. **Snapshots first makes the price tables a smaller change later, not a bigger one.** Dependency order is the same whatever you decide about EUR.

*Proportionality.* What happens if the price tables are not built? EUR is one schema change and 33 seeded rows away. How likely is that to hurt? Not at all, pre-production. That is the "do nothing" tier of CLAUDE.md rule 4, and this is exactly the case it was written for.

**So say the promise honestly, and change it.** "CZK and EUR both working at launch" is not achievable in a one-market platform without contradicting the rule you just settled. What *is* achievable: **"adding a currency is a schema change, a seed file and a country flip — no design work, and here is the written model that proves it."** That is a different promise. It is the one that survives contact with your own scope decision.

**If a named B2B prospect exists, tell me and the answer changes to D** — the document machinery is already two-party and currency-parameterised, so the cost is materially lower than the earlier passes said. Absent a name, D stays closed.

---

## 4. The revised plan

### What was cut, and why

| Dropped or deferred | Why |
|---|---|
| **Wave 7 entirely** — Slovakia (Step 17), Poland (Step 18) | Out of scope by owner ruling. Tax law will move first. |
| **Wave 6 entirely** — Plus multi-currency, `MembershipPlanPrices`, `UserMembership.BillingCurrencyId`, Stripe `currency_options`, minimum-charge validator, cancel-and-resubscribe (Steps 15, 16) | `MonthlyPriceCzk` is honest in a CZK-only platform. Only the Adaptive Pricing flag survives, and it moves to Wave A. |
| **The three price tables** (Step 4.1) and the deletion of `Service.BasePrice` / `Package.Price` / `Extra.Price` | Recommendation A. Retrofit cost is flat pre-production. |
| **Per-currency credit** (Steps 4.3, 7.3), **loyalty divisor + per-currency floor** (4.4, 7.4), **`OrderEmployeePay.CurrencyId` + payroll derivation** (4.5, 7.5), **per-currency no-show credit** (7.9) | All inert with one currency, whether built or not. `check-booking-policy-parity.mjs` stays untouched. |
| **`CountryConfiguration.VatTreatment`** (4.8, 7.8) | Only two of four states are reachable with one country, and `CompanyInfo.IsVatPayer` already expresses those two. Its shape depends on unanswered accountant questions about law that will change. |
| **Currency activate/deactivate machinery** (Step 7.6), `DefaultCurrencyCode → DefaultCurrencyId` (4.8) | Ruling 8's cheap half is the whole answer: delete the eleven non-CZK seed rows. `IsActive` is read by no currency path today. |
| **The market selector**, `User.PreferredMarketCountryId`, **the web country picker** (Step 11) | One market. Nothing to select, and `OrderAddressResolver.cs:98-110` resolves silently. |
| **The wire changes and NSwag regeneration** (Step 8) — `CurrencyId` off `QuoteOrder`/`CreateOrder`, `TierInfo` currency, credit list response | `CurrencyId` stays on the wire and is ignored server-side. **Both Mac sessions are gone. So is the mobile spec re-dump.** |
| **Angular formatter sweep, admin reports by currency, Android/iOS fraction digits, i18n de-currencying** (Steps 11–14) | Render identically with one currency. |
| **`MembershipPlan.DiscountPercentage` rename**, the Slovak 0.23 seed correction, Poland's NULL `PayoutScheme` | Cosmetic or Slovak/Polish-specific. |
| **`OrderFactory`'s `GetActiveCompanyInfoAsync` fallback deletion** (Step 1's tail) | A multi-country hazard. With one company row it is harmless. |
| **`numeric(18,2)` precision normalisation** (Step 4.9) | Free to fold into a regeneration, but regenerations are free pre-production too. Skip it; it costs the same later. |

Eighteen numbered steps become nine. Seven waves become three.

**Ground truth first — the tree moved under the design document.** Commit `56ad4d45` shipped the whole VAT convention fix: all three sites now read `× rate / (1m + rate)` (`VatCalculator.cs:32`, `IssuePartialRefund.cs:238`, `RefundAllocator.cs:95`), the display bug is fixed (`DefaultReceiptLayoutBuilder.cs:324` renders `VatRate * 100m:N0`), and **`Cleansia.Tests/Services/VatCalculatorTests.cs` now exists with the 2,000 @ 0.21 → 347.11 case pinned.** The lane that reported this as uncommitted work-in-progress with a zero-test class was reading a stale tree. **Do not re-dispatch Wave 1 Step 1.**

---

### Wave A — the money paths. No schema, no wire, no regeneration.

**A1 — Adaptive Pricing off.** Two properties in `StripeClient.cs` (inside the options at `:51` and `:412`), plus the dashboard toggle and the account checks in §1. No test (§1, proportionality). Owner actions: dashboard toggle, settlement-currency check, Payment Links check, sandbox key or the three curls.
*Proves it:* the sandbox probe. Nothing in the suite can see a Stripe dashboard setting.

**A2 — Close the caller-supplied currency route. These three land together or not at all.**
 (a) `CreateOrder` and `QuoteOrder` stop reading `command.CurrencyId` — server resolves `GetDefaultAsync` unconditionally. **Keep the field on the wire**; drop the two validators at `CreateOrder.cs:118-123` and `QuoteOrder.cs:151-156`.
 (b) Stop reading `Currency.ExchangeRate` in `OrderPricingCalculator` — delete `:64` and the thirteen multiplication sites. **Leave the column.**
 (c) Delete the eleven non-CZK rows from `insert_seed_data.sql:526-536`.
Any one alone leaves a worse state than today: (a) without (c) leaves a live 24× hole for anyone who can reach the repository; (b) without (a) leaves EUR resolvable at rate 1.0.
*Proves it:* `Cleansia.Tests` (pricing, order creation), `Cleansia.HostTests` (the endpoint still accepts the field and ignores it), `Cleansia.IntegrationTests`.

**A3 — Two one-liners on files the VAT fix just touched.** `CurrencyRepository.cs:12-13` — the `??` binds to the `Task`, which is never null, so the `EntityNotFoundException` is dead and a missing default returns a null `Currency` into an NRE. And `RefundAllocator.cs:28`, whose XML doc still documents `round(lineRefund × rate / (100 + rate), 2)` after the code at `:95` was changed to `/(1m + rate)`.
*Proves it:* `Cleansia.Tests`.

**A4 — `VatCalculator` fails closed.** `VatCalculator.cs:14-17` returns `VatBreakdown.NotApplicable` — zero VAT, silently — when `countryConfig` is null. Throw instead, and invert `VatCalculatorTests.NullCountryConfiguration_CurrentlyYieldsZeroVat_Silently`, which currently pins the fail-open behaviour as documentation.
*Proves it:* `Cleansia.Tests` (`VatCalculatorTests`).

---

### Wave B — order-line snapshots, and the ONE regeneration.

**This is the call the brief asked me to make myself, so here it is.** Three sites read the **live** catalogue for a historical order, and I verified all three:

- `IssuePartialRefund.cs:255-256` (service gross), `:271`/`:276` (package), `:306-308` (extras by slug) — the refund denominator.
- `ReceiptService.cs:362-363`, `:368` — the customer's receipt PDF line items.
- `ReceiptService.cs:246-247`, `:260` — `FiscalLineItem`, the lines registered with the tax authority.

`OrderService.cs` and `OrderPackage.cs` are pure join rows with no money column. `UpdateService.cs:97` and `UpdatePackage.cs:110` mutate catalogue prices in place, unversioned, unguarded, through shipped admin forms. Receipts are generated from a queued message after `CompleteOrder.cs:265` — days or weeks after the price was frozen.

**Is it a defect today, in a single-currency platform? Yes — with the priority set honestly:**

- **The refund path is the only one where money moves.** Order = Service A 1,000 + Service B 200, `TotalPrice` frozen at 1,200. Admin later raises A to 2,000. Refunding B pays `200/2200 × 1200 = 109.09` instead of 200, and the admin picker shows no amount before it goes to Stripe (`admin-order-refund.component.ts` builds every line option with `price: null`). Uniform price sweeps cancel through the `totalGross` normalisation at `RefundAllocator.cs:68,74` — **but extras cannot be re-priced at all** (no admin surface exists), so on any order carrying an extra, no sweep is uniform and the cancellation is unreachable.
- **The receipt PDF path is live but is a document defect, not a money movement** — and the document is *already* wrong for a different reason: `ReceiptPdfData` has `Extras` as `List<string>` with no prices, and **no field at all for a discount or the express surcharge**. The lines cannot sum to the total in pure crowns regardless of drift.
- **The fiscal path is dormant, and I am correcting the adversarial pass that called it decisive.** `Fiscal:CzechEet2:Enabled` is `false` in all five hosts' `appsettings.json`, so `FiscalServiceResolver` returns `NoOpFiscalService`, which returns `FiscalResult.NotRequired()` and never sends the request. Czech EET 2.0 lands January 2027 per the comment in that file. **Nothing is declared to any tax authority today.** It is a gate before EET 2.0, not a live defect.

**Verdict:** currency-independent, real, cheap, and a hard prerequisite for Wave C. It stays the largest item in the plan. It is not "drop everything" — there is no production, so nothing has actually been mis-refunded — and it ranks *after* Wave A only because Wave A needs no schema and unblocks the sandbox probe.

**B1 — Schema edits.** Entity + EF-configuration only, no regeneration between them. `OrderServices += UnitBasePrice, UnitPerRoomPrice, LineTotal`; `OrderPackages += LineTotal`; new `OrderExtras(OrderId, ExtraId, Slug, UnitPrice)` replacing the `Orders.Extras` JSON column.

**B2 — Pricing calculator writes the snapshots; the three consumers read them.** `IssuePartialRefund` and both `ReceiptService` builders stop touching `Service`/`Package`/`Extra` entirely. This also closes a divergence: `OrderPricingCalculator.cs:49` filters `e.IsActive` and `IssuePartialRefund.cs:307` does not, so an extra deactivated after ordering is dropped from `TotalPrice` but counted in the refund denominator.

**B3 — The receipt gains the lines it lacks.** `ReceiptPdfData` needs extras-with-prices, an express-surcharge line and a discount line before its lines can sum to `Total`. Same pass as B2, because B2 supplies the numbers.

> **B4 — THE ONE REGENERATION. This is where DEV is dropped. Agent's job, not the owner's** (rulings 2026-08-15, 2026-08-25, 2026-09-07), and every step taken gets named in the report.
> ```
> export PATH="$HOME/.dotnet/tools:$PATH"
> cd src
> dotnet ef migrations remove --force --project Cleansia.Infra.Database --startup-project Cleansia.Web.Partner
> dotnet ef migrations add   Initial --project Cleansia.Infra.Database --startup-project Cleansia.Web.Partner
> ```
> then drop the DEV database — the migration id changes, and a DEV database recording the old one replays the create script against existing tables.
> **B1 is the only schema change in this plan.** If anything else needs schema, it lands before B4 or it waits for a second regeneration, which is free but should still be deliberate.

*Proves Wave B:* `Cleansia.Tests` (pricing snapshots, `RefundAllocator`, `IssuePartialRefundHandlerTests`, `PartialRefundFeeRoundingTests`, a receipt-builder test asserting lines sum to total); **`Cleansia.IntegrationTests` after B4 — Testcontainers builds a real Postgres from the migration and is the only thing that proves the model and the schema agree**, and it is the gate on the new `OrderExtras` table and its unique index; `Cleansia.HostTests` if any order DTO surface moves.

---

### Wave C — Extras admin CRUD.

You ruled this gets built. **Two things have changed and you should re-rule.**

Its original forcing reason is gone: it was forced by "you cannot create a catalogue item without pricing it *per currency*", and there are no per-currency price rows now. With the single `Extra.Price` column it has today, it is a pure admin convenience.

And it is the thing that *activates* the extras third of the Wave B defect. `Extra.Price` is seed-only today — `Features/Extras/` holds only `GetExtraOverview.cs` and a DTO, `IExtraRepository` is read-only at all three AppServices call sites, and there is no `AdminExtraController`. **Making extra prices editable is exactly what turns `IssuePartialRefund.cs:306-308` from an inert live-read into a live one.** It must not ship before B2, and that is a hard ordering constraint, not a preference.

**My recommendation: defer it.** If you keep it: controller, create/update/activate commands, validators, permissions.
*Proves it:* `Cleansia.Tests` — **`FrozenPermissionMapTests`** (an additive permission row updates the snapshot in the same PR) and **`CatalogLifecycleEndpointPermissionTests`** (activation reuses `CanUpdateExtra`, as every other catalogue entity does). Six registration sites: `Policy.cs`, `PolicyBuilder.cs`, those two tests, and `policy.ts` twice. Plus `EveryRouteCarriesAnAuthorizationDecisionTests`.

---

### Owner actions, all of them

| Action | Where | Blocks |
|---|---|---|
| Turn Adaptive Pricing off in the Stripe dashboard (sandbox now, live later) | Stripe | A1 |
| Confirm CZK is a settlement currency; check for Payment Links | Stripe → Balances | A1 (check first — it may make the whole finding theoretical) |
| Supply a sandbox key via user secrets, or run the three location-email curls | Stripe | The probe that settles §1 |
| Send question 1 (gross vs margin) to the accountant | §2.1 | Nothing in this plan. It sets your VAT registration date. |
| Supply the signed cleaner contract, the insurance policy, and confirmation no cleaner is legally an employee | — | The accountant's answer |
| Rule on §3 (recommendation A) | — | Whether Wave B is the end of the plan |
| Re-rule on Wave C | — | Wave C |

**No Mac session is needed anywhere in this plan. No NSwag regeneration. No mobile spec re-dump. No iOS or Android change.** That is the largest single saving from the scope cut, and it is a direct consequence of keeping `CurrencyId` on the wire.

---

## 5. What we are deliberately NOT building, and where it is written down

**The Slovak and Polish material is knowledge, not work.** It lives in `agents/backlog/T-0688-multicurrency-design.md` §§2.3–2.6 and in the ADR-level sourcing there: place of supply (Art 47 + Implementing Reg 282/2011 Art 31a(2)(k), which names cleaning explicitly), the OSS Union-scheme mechanism (Art 369a(3)(a) and Art 369b, plus Slovakia's own methodological guideline naming *služby vzťahujúce sa na nehnuteľnosť* under the Union heading), the SME cross-border scheme, the rate tables, and the fixed-establishment test. Part 2's target model (§§2.4–2.13) is the multi-currency design itself — **keep it as the written proof that expansion is data plus a bounded schema change, since under recommendation A that document is the promise standing in for the code.**

**Re-check every one of these before a second market is considered.** Rates move, thresholds move, and the SME scheme is a year old:

- The Slovak standard rate and reduced-rate structure. The seed still says `0.20` in **two** places (`insert_seed_data.sql:894` and `:979`) and the real rate has been 23% since 1 January 2025. It is dormant and wrong; leave it dormant and wrong rather than half-correcting a country you are not entering.
- Poland's PKWiU classification for a residential flat clean (81.21/81.22 at 23% vs 81.29.12.0 at 8%), and its `CountryConfiguration.PayoutScheme`, seeded NULL.
- Whether Czechia still issues the EX number and what the caps are then.
- Whether OSS registration as an *identifikovaná osoba* is still available below the plátce threshold.
- Fixed establishment — the moment anyone proposes a local coordinator, an office or a depot.

**Two things to know about the tree, reported not fixed** (CLAUDE.md rule 2): `mapbox-autocomplete.service.ts:83` defaults the address-autocomplete country whitelist to `['cz', 'sk']` with a comment claiming it "matches the platform's current launch markets", so a customer can pick a Bratislava address the server will refuse with `country.not_serviced`. And `SetCountryServiced.cs` exists — **one admin click flipping a second country on breaks every inline-address web booking with a 400**, because `OrderAddressResolver.cs:104-108` returns `CountryRequired` the moment more than one country is serviced and the wizard has no country control. That is a gate to record against the flip, not UI to build now.

---

## What is still unknown

- **Whether `adaptive_pricing[enabled] = false` actually disables it**, and whether a session carrying an explicit `currency` does too. Documentation only, on both sides. The three-curl probe settles it in ten minutes.
- **Whether CZK is a settlement currency on the account.** If not, §1 is theoretical.
- **Gross vs commission turnover.** The code says principal in eight places; the contracts decide, and one of them is not in the repository.
- **Whether a B2B/EUR prospect exists.** Only you can answer it, and it is the one fact that changes §3's recommendation.
- **Whether any catalogue price will ever be edited after orders exist.** Nobody has looked at how often `UpdateService`/`UpdatePackage` run. If the honest answer is "never", Wave B drops a tier — but the admin forms are shipped, extras cannot be re-priced at all, and there is no preview before a refund goes to Stripe, so I would not bet on it.