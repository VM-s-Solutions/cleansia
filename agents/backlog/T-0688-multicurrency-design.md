# Multicurrency: the answers, the system, and the plan

**Research consolidation, 2026-09-08.** Builds on
[`T-0688-multicurrency-research.md`](T-0688-multicurrency-research.md). Read-only — no code, schema or
data was changed by this work.

Seven research lanes fed this — market practice, VAT place-of-supply, VAT thresholds, the cost of the
currency-selection choice, the target data model, Plus/Stripe, and credit/loyalty/extras — and the
riskiest claim from each of five of them was put to an independent pass instructed to **refute** it,
two of them from two different angles. **Three of those verdicts came back REFUTED or materially
corrected, and in every case the correction wins and is marked in the text.** Where this document
contradicts the earlier research pass, it says so in bold.

**Re-verified by hand after synthesis, from the tree:** the Slovak VAT seed (0.20, in *two* places —
`insert_seed_data.sql:894` and `:979`); the three VAT-convention sites (`VatCalculator.cs:24`,
`IssuePartialRefund.cs:234`, `RefundAllocator.cs:93`); the two loyalty divisors
(`LoyaltyService.cs:44` earn and `:163` clawback); Poland's NULL `PayoutScheme`
(`insert_seed_data.sql:991`); and that no `SessionCreateOptions` or `SubscriptionCreateOptions` in
`StripeClient.cs` sets a top-level `Currency`. All hold.

**This is a plan, not a mandate.** Part 1 answers what was asked; Part 2 is the target system; Part 3
is the order of work. Nothing here has been built.

---

# PART 1 — THE ANSWERS

## 1. Decision 2 — who decides the currency

This is the linchpin, so it gets the length. You asked four things: how do Wolt and Bolt do it, which is better, why, and what are the edge cases. Then two facts you wanted checked first.

### 1.1 What Wolt and Bolt actually do

**Wolt is a hard market-per-country model.** One legal entity per country — "Wolt Česko s.r.o.", "Wolt Slovensko s. r. o.", "Wolt Polska sp. z o.o." (https://explore.wolt.com/wolt-entities-list). One set of terms per country: "the Wolt Service is legally provided to you by Wolt Slovensko s. r. o. in Slovakia" (https://explore.wolt.com/en/svk/terms). One price list per country, **authored, not converted**:

- https://explore.wolt.com/en/cze/wolt-plus — "The monthly subscription fee in Czechia is 99 CZK."
- https://explore.wolt.com/en/svk/wolt-plus — "The monthly subscription fee in Slovakia is 4,99 Eur."
- https://explore.wolt.com/en/pol/wolt-plus — "The monthly subscription fee in Poland is 12,99 PLN."

Those are not the same money. At the ECB euro reference rates of 8 September 2026 (CZK 24.186, PLN 4.3178), 99 CZK = €4.09 and 12,99 PLN = €3.01, against €4.99 in Slovakia. Pairwise: CZ→SK +22%, PL→CZ +36%, PL→SK +66%. **This is empirical confirmation of your ruling 1 from the company operating in your exact three markets.**

Language is a separate axis from market: the URL is `wolt.com/en/cze/prague` — read the Czech market in English. That is the split you already have (`User.PreferredLanguageCode`, `User.cs:95`) and it is the right one.

Wolt+ is bound to a **Subscription Country**. The first pass cited Romania for this — a market you will never enter. The identical sentences are on the Slovak page, https://explore.wolt.com/en/svk/woltplus-terms: "The Subscription and Wolt+ Benefits are limited to the country in which you purchase the Subscription ('Subscription Country')." and "If you travel outside of your Subscription Country, your Wolt+ Benefits will not apply to any purchases made while outside of your Subscription Country."

**Bolt** pins stored balance to its issuing currency and tells the customer so, plainly: "the Bolt balance can only be used in the currency it was issued in", "if you travel to a country that uses a different currency, you won't be able to use your Bolt balance", "It is not possible to convert the currency." (https://bolt.eu/en/support/articles/53676/). That is your ruling 5, live at a competitor — plus the half you are missing, which is *saying it*.

**Two honesty flags on the comparator evidence:**

- **Neither Wolt nor Bolt was confirmed to lack a currency picker.** No such control appears in their help documentation and Wolt's URL structure has no currency segment. That is evidence of absence, not a statement from either company. If it matters to you, five minutes with the apps installed settles it.
- **The "domestic services industry does X" claim has no source.** The first research lane named Helpling as its closest structural analogue and supplied **no URL for it anywhere**. Helpling operates in Australia, Germany, Italy, France, Ireland, UK, UAE, Netherlands, Singapore and Switzerland — not CZ, SK or PL. Treat that comparator as unevidenced. The food-delivery and travel precedents stand on their own.

### 1.2 Display vs charge vs contract — and a correction the first pass got wrong

The first research pass told you: only the *display* currency ever moves; nobody lets a customer move the charge. **That is wrong, and I have to say so, because it was the strongest-sounding argument against your lean.**

- **Airbnb**, verbatim on its own supported-currencies page: "Even though guests can choose their payment currency, they must select a compatible payment method." And: "If a guest pays in a different currency than the host's listing currency, an additional fee is charged." Airbnb prices it — cross-currency bookings push the guest service fee to "up to 16.5% of the booking subtotal" against a 14.1–16.5% base range.
- **Booking.com**, terms last updated 25 August 2025: "If the currency selected on the Platform isn't the same as the Service Provider's currency, we may: show prices in your own currency; offer you the Pay In Your Own Currency option." Its fee is "expressed as a percentage over European Central Bank rates."
- **Uber**, terms last updated 6 October 2026: "Use of Preferred Currency Pricing will be subject to a currency conversion fee equal to 1.5% of the Local Currency value of the total trip fare", and "Uber does not set the exchange rate."

So **three of six move the settlement currency**, each for a priced fee, each with the local amount still on the receipt.

**The real invariant — and it is a better one — is that nobody moves the price list.** The host's listing currency, the Service Provider's currency, and Uber's "Local Currency ... the currency of the location where you book the ride" all stay put. What moves is *settlement*, and moving it costs an FX rate at charge time, a conversion fee, and a dual-currency receipt.

Three axes, not two:

| Axis | What it is | Who moves it |
|---|---|---|
| **Display** | What I browse in | Airbnb, Booking, Uber let you. Wolt/Bolt don't. |
| **Settlement** | What my card is debited in | Airbnb, Booking, Uber — as a paid add-on. |
| **Price list / contract** | What the invoice, the VAT line and the consumer contract are denominated in | **Nobody.** |

### 1.3 Fact (a) — does VAT follow the property rather than the money?

**Yes, decisively. And it does NOT decide decision 2.**

VAT Directive Art 47: "The place of supply of services connected with immovable property ... shall be the place where the immovable property is located." Implementing Regulation 282/2011 Art 31a(2)(k) names your business explicitly: "the maintenance, renovation and repair of a building or parts of a building, **including work such as cleaning**, tiling, papering and parqueting". Cleaning appears in none of the 31a(3) exclusions.

*(Sourcing note: Art 31a is not in the original 282/2011 — it was inserted by Reg (EU) 1042/2013. Cite the consolidated text, `02011R0282-20250414`, not the base act. The first pass cited a URL that does not contain the article it quotes.)*

Currency is not an input to that rule anywhere. A Slovak paying euros for a Prague clean owes **21% Czech VAT**. A Czech paying crowns for a Bratislava clean owes **23% Slovak VAT**. The two axes are independent in law — and **already independent in your code**, correctly: `OrderFactory.cs:203` reads `var countryId = input.Address.CountryId;` and resolves both the company and the country config from it (`:204-209`). That is Art 47, implemented, today.

**So VAT does not settle decision 2.** But there is a second-order tax fact that does bite, and it is the only real cost of a settlement picker:

**VAT Directive Art 230: "The amounts which appear on the invoice may be expressed in any currency, provided that the amount of VAT payable or to be adjusted is expressed in the national currency of the Member State, using the conversion rate mechanism provided for in Article 91."** Czech §29(1)(l) ZDPH requires *výše daně v české měně* on a Czech tax document.

So a EUR-denominated invoice for a Prague job must **additionally state the VAT in CZK at a ČNB rate**. `ReceiptPdfData` carries one `Currency` string and one `VatAmount`/`VatRate` pair — no second amount, no rate, no rate date. **That reintroduces the exchange rate you just deleted from pricing, onto the tax document.**

It is dormant while `IsVatPayer = false` (seeded false at `insert_seed_data.sql`, and `VatCalculator.cs:14-17` short-circuits on it) and it bites the day you register for Czech VAT.

**Correction to the first pass:** it said Option B "deletes the ČNB/ECB rate feed as a concept" (T-0688:232). That is **true for pricing and false for tax reporting**. You will need a rate source the day you are a VAT payer, because the OSS return converts at the ECB rate on the last day of the quarter regardless of any pricing decision. Just never let it near the price.

### 1.4 Fact (b) — does Stripe pin a Customer to a currency after the first charge?

**No. The folklore is wrong, and this was worth checking because it would have killed your lean outright.**

Stripe does not enforce via the scalar `Customer.currency`. It enforces via a documented **closed list of active locking resources** (https://support.stripe.com/questions/why-are-customers-locked-to-a-specific-currency-and-can-not-be-moved-to-a-price-in-another-currency): "Here is the full list of resources that lock a Customer to their default currency: Active Subscriptions (status other than 'canceled'); Not started or active SubscriptionSchedules; Discounts; Draft or Open Quotes; Pending (not yet attached to an Invoice) InvoiceItems."

PaymentIntents and Charges are on neither that list nor the API error that enforces it. `currency` is not even a settable parameter on Customer create.

I verified the exposure exhaustively in the tree: `StripeClient.cs` is the only file that constructs any Stripe service, and it constructs exactly `CustomerService`, `PaymentIntentService`, `SessionService`, `EphemeralKeyService`, `SetupIntentService` and `SubscriptionService`. **There is no `InvoiceService`, no `InvoiceItemService`, no `CouponService`, no `QuoteService`, no `SubscriptionScheduleService`, and no customer-balance write anywhere.** Cleansia creates exactly one of the five locking resources, and only via Plus.

Narrower still: the **web** payment Checkout Session attaches no Customer at all (`StripeClient.cs:52-73` sets PaymentMethodTypes, LineItems, Mode, SuccessUrl, CancelUrl, Metadata — no `Customer`). Only the mobile PaymentIntent does (`StripeClient.cs:168`), and PaymentIntents don't lock.

**So Stripe does not decide decision 2 either.** What Stripe *does* constrain is subscriptions, and that is decision 6 — see §3.

**But one Stripe thing is urgent and it is not a design question.** `StripeClient.cs:52-72` builds the web Checkout Session with an inline price:

```csharp
PriceData = new SessionLineItemPriceDataOptions
{
    Currency = order.Currency.Code.ToLower(),
    ...
}
```

and **never sets `SessionCreateOptions.Currency`**. Stripe's Adaptive Pricing doc: "This applies to prices you create and reference with a price ID **and prices you create inline with `price_data` when you create a Checkout Session**", and "You pay 0% / Your customers pay 2–4%". Adaptive Pricing "isn't available for businesses using Elements with the Payment Intents API" — which is your mobile path (`StripeClient.cs:158-194`).

**So if that toggle is on, the same order can charge different currencies on web and mobile, today, in CZK-only mode, with the customer paying a 2–4% fee, while the order row and the receipt still say CZK.** The first pass explicitly did not check Stripe (T-0688:319).

> **Owner action, thirty seconds, before any code:** open `dashboard.stripe.com/settings/adaptive-pricing` and tell me whether it is on. Set `Currency` explicitly on every Checkout Session regardless.

### 1.5 The legal position — and a correction that removes an argument I would otherwise be leaning on

The first pass argued that Regulation (EU) 2018/302 (geo-blocking) makes address-pinning legally safer than a picker. **That was wrong, and an adversarial re-read of the primary text plus the Commission's own Q&A refuted it. The correction wins.**

- Art 4(1) prohibits different conditions of access "**for reasons related to** a customer's nationality, place of residence or place of establishment". Pinning to the service address is a **territorial** criterion. A picker offered to all customers on identical terms is a **non-personal** criterion. **Neither engages Art 4.** The Regulation does not distinguish the two designs at all. The legal case for pinning over choosing is **zero, not "safer"**.
- Art 4(1)(c) is a *prohibition on the trader* (treat the visiting foreigner as a local), not a permission to pin.
- The actual safe harbour for per-country price lists is **Art 4(2)**: "The prohibition set out in paragraph 1 shall not prevent traders from offering general conditions of access, including net sale prices, which differ between Member States ... offered to customers on a specific territory ... on a non-discriminatory basis." Reinforced by Commission Q&A 2.3.5: "Does the Regulation regulate prices? No." **This validates ruling 1 squarely.**
- Art 5(1)(c) is a *condition limiting* Article 5's prohibition, not a rule about currency selection.

**What IS legally exposed — and it is the one option to rule out on legal grounds — is deriving currency from the customer's residence, nationality or CARD COUNTRY.** Art 5(1) expressly names "the place of issue of the payment instrument within the Union" as a prohibited basis. **That is precisely the model in your decision-6 note: "if the currency is automatically applied to the customer account then a Slovakian user has to pay in euros."** Your instinct to reject that was right, and it turns out to be the only legally grounded part of this entire question. But the fix is not a picker.

**Two provisions nobody has named, both more likely to bite you than the currency question:**

- **Art 3(2)**: a trader "shall not ... redirect that customer to a version of the trader's online interface that is different from the online interface to which the customer initially sought access ... unless the customer has explicitly consented", and the original "shall remain easily accessible". **If you copy Wolt's per-country-site model with auto-redirect, that is the real compliance risk in the precedent — not the pricing.**
- **Pammer/Alpenhof (C-585/08, C-144/09)**, quoted in the Commission Q&A: "use of a language or a currency other than the language or currency generally used in the Member State of the trader" is evidence of **directing activities**, engaging Rome I Art 6 consumer protection and Brussels I consumer jurisdiction. A currency picker open to everyone is a directing-activities signal toward states you do not operate in. That is a better-founded argument against a free-for-all picker than the arbitrage one.

**Honest caveat.** Whether Art 4(1)(c) even *reaches* a clean performed at the customer's own address is unresolved. Recital 25's examples are all trader-side or third-party venues ("hotel accommodation, sports events, car rental, and entrance tickets for music festivals or leisure parks"), and Commission Q&A 2.3.4 says: "Does the Regulation oblige traders to move physically in order to provide services at the customer's location in Europe? No, the Regulation contains no such obligation for traders to move." Nobody has tested a domestic-services platform against this article. I am not a lawyer. The **commercial** case does not depend on the answer; the legal framing does. The Commission opened an Art 9 review call for evidence on 11 February 2025; no revision proposal published.

### 1.6 Recommendation

**Pin the PRICE LIST to the service address country. Give the customer a free MARKET selector on the surfaces that have no address. Do not build a settlement-currency picker.**

| Pinned to the service address | Free to the customer |
|---|---|
| Price list, contract currency | Language (already is) |
| VAT rate and jurisdiction | **Market selector** on address-less surfaces: home calculator, services catalogue, Plus page |
| The Stripe charge currency | |
| The receipt | |
| Which credit account is spent | |
| Promo minimum, loyalty divisor, loyalty floor | |

Reconciled visibly at the address step: *"This property is in Czechia — you'll be billed in CZK."*

**Why this and not your lean, stated plainly.** Your worry — a Slovak forced into euros — dissolves once the pin is the **flat** rather than the **person**. A Slovak living in Prague books a Prague flat and pays CZK, because the flat is in Prague. He is never stuck in euros. You get exactly the outcome you wanted, with no picker, no FX rate and no Art 5(1) exposure. And the market selector gives you the 80% of "the customer chooses" that costs nothing: he chooses which market's prices he *browses*, which is the only thing a customer without an address can meaningfully choose.

**What you lose either way, stated honestly:**

- *If you take my recommendation:* no "pay in your own currency". A Slovak comparing Prague prices sees CZK. That is what Airbnb, Booking and Uber sell as a paid add-on, and buying it means an FX rate at charge time, a rate provider you operate, a conversion fee you absorb or disclose, dual-currency receipts, and — the one that actually costs — an **Art 230 CZK VAT restatement on every cross-currency invoice** from the day you register for VAT. Ruling 1 deleted exactly that machinery.
- *If you overrule me and build the settlement picker:* you get the comparison affordance and a directing-activities signal toward markets you do not serve, and you own an FX rate forever. **You do not, however, get arbitrage.** The first pass claimed a picker creates an arbitrage machine, citing Wolt's 22–66% spread. **That is wrong for you and I'm correcting it, because it means the picker is cheaper than you were told.** A clean happens at an address, so the address pins the price list whatever currency settles. There is no Prague price available for a Bratislava flat. Wolt's defence against its own spread is the Subscription Country binding, not the absence of a picker.

**If you do want it later**, the safe shape: display-only, labelled approximate, with the actual charge amount restated in the market currency directly next to the confirm button. CRD Art 6(1)(e) requires "the total price of the goods or services inclusive of taxes"; Art 8(2) requires it "in a clear and prominent manner, and directly before the consumer places his order"; Art 6(6) says undisclosed charges "shall not be borne by the consumer". **And never derive the default from the card BIN or the customer's residence** — that is the one thing Art 5(1) actually prohibits.

### 1.7 The edge cases, each with what the platform should do

1. **Slovak citizen, Prague flat.** → CZK, 21% Czech VAT. Pinned to the flat. No prompt, no picker, no exception.
2. **Czech citizen, Bratislava flat.** → EUR, **23% Slovak VAT** (the seed says 20% — see §2.6).
3. **Address changed mid-basket, Prague → Bratislava.** → Hard re-quote with explicit re-consent. The price list changes, applied credit becomes unspendable, an applied promo may become invalid. CRD Art 8(2) makes this a consent question, not a UX one. **Block the address change after the payment step; before it, re-quote and clear credit/promo with a visible message.** Note this forces the address step *before* the price step in all three booking flows — but see §1.8: that is survivable by re-pricing, and the address-less surfaces are the real reason for the market selector.
4. **Recurring template whose address country changes.** → **End the series and start a new one.** Silently switching an off-session recurring charge (`ConfirmRecurringOrder`) from CZK to EUR is a new contract at a new price in a new currency with no fresh consent. Today `MaterializeRecurringBookingTemplate` hardcodes the platform default currency while reading the template's saved address twelve lines away — under the address-pinned model that becomes a one-line fix.
5. **Plus member moves Prague → Bratislava.** → Plus keeps billing in CZK; the 5% still applies to EUR orders; the management screen says "Plus bills in CZK". **Stripe forbids changing a subscription's currency** ("The currency cannot be updated on an ongoing subscription"), and `CancelAtPeriodEnd = true` (`StripeClient.cs:383`) leaves the status `active`, so the currency lock **persists until the paid period actually ends — up to a year on an annual plan.** That needs product copy and probably a scheduled re-subscribe. It is not a button.
6. **Customer holds CZK credit, books a Bratislava flat.** → Show the EUR balance (zero) *and* the CZK balance marked unspendable here, with one sentence saying why. Bolt does exactly this and states it. Today Cleansia shows 0 with no message at all.
7. **Card issued in a third currency.** → Nothing. Cleansia charges one currency; the issuer's conversion is invisible to the platform and unreconcilable. Say so in the terms, as Airbnb does ("Airbnb isn't responsible for these fees"). **Never act as the DCC provider the way Uber does.**
8. **Anonymous home-page calculator, no address.** → Market selector, defaulting to the market of the site the visitor landed on. Never to the card country, never to IP-derived residence.
9. **Guest subscribes to Plus before ever booking.** → The market selector supplies the currency. It is not a separate mechanism; this is why the selector exists.
10. **Admin issues goodwill credit.** → Must pick a currency. Today `IssueCustomerCredit` calls `GetDefaultAsync` unconditionally (verified at `:119`) and the dialog only *displays* the resolved code.
11. **Cleaner works a job outside their registered work country.** → The payout invoice currency must derive from the pay rows being invoiced. Today `CurrencyResolutionService:15-24` derives it from `employee.WorkCountryId`, and **`OrderEmployeePay` has no currency column at all** (grep returns nothing). That fires on the day you flip a second country on, before a single euro booking exists.
12. **A currency with no price row for an item.** → The item is **not offerable** in that market. Fail closed. Not zero, not converted. `OrderPricingCalculator` currently does `s?.BasePrice + s?.PerRoomPrice * ...` with a `?? 0m` — a missing row would silently omit a line and charge a total the lines do not sum to.
13. **Stripe presents a converted currency via Adaptive Pricing.** → Prevented by setting `Currency` explicitly on every Checkout Session. One line, plus the dashboard check.
14. **A market is deactivated while customer credit is outstanding in it.** → Refuse the deactivation. `CurrencyRepository.IsInUseAsync` checks Orders, EmployeePayConfigs and EmployeeInvoices only — **not CreditAccounts, not PromoCodes** (verified). Deactivating today would strand real money silently.
15. **An abandoned mobile Plus checkout.** → `PaymentBehavior = "default_incomplete"` (`StripeClient.cs:307`) creates an `incomplete` subscription, which is "a status other than canceled" and therefore **locks the customer's currency the instant the sheet opens**, for up to 23 hours if abandoned. Whether `incomplete_expired` also locks is genuinely ambiguous from the docs. **This deserves a sandbox test.**

---

## 2. The VAT questions

> **Loud caveat, and it is not boilerplate.** I am not a tax adviser. Everything below is quoted from Finanční správa, Finančná správa, EUR-Lex, Commission guidance and professional publications, cited. The three things that most change the answer — principal vs agent, fixed establishment, and whether Czechia issues the SME "EX" number — are all unresolved and all need a **daňový poradce**. §2.8 separates what you can rely on from what he must confirm.

### 2.1 Is "2kk CZK" right?

**The number is right. The shape of the rule is not, and the difference could cost you.**

Since 1 January 2025 there are **two** thresholds, not one (GFŘ Informace č.j. 11977/25, and the Finanční správa 2025 news page):

- **CZK 2,000,000** in a calendar year → you are a payer **from 1 January of the following year**. (You may also *elect* to become a payer the day after crossing.)
- **CZK 2,536,500** in a calendar year → you are a payer **the next day**, with no grace period.

Two more 2025 changes matter: turnover is now measured over the **calendar year**, not twelve rolling months ("Od ledna 2025 je nově počítán obrat pro zákonnou registraci za kalendářní rok a nikoliv za 12 bezprostředně předcházejících po sobě jdoucích kalendářních měsíců"), and you have **10 working days** to file.

So the comfortable version of your belief — *"I get until next January"* — is true only in the band between 2.0m and 2.54m. **Above 2.54m the software has to be correct overnight, on a day nothing warns you about**, and nothing in this codebase tracks turnover (grep for turnover/obrat/threshold across `src/`: zero hits).

**The 2,536,500 figure is fixed, not indexed.** Directive (EU) 2020/285 pins the national-currency conversion to one historical date: "The corresponding value in national currency ... shall be calculated by applying the exchange rate published by the European Central Bank on 18 January 2018." €100,000 × 25.365 = 2,536,500 exactly. So a hardcoded monitoring figure will not rot with the exchange rate — only if the Czech legislator moves it.

### 2.2 The sting: your obrat is almost certainly GROSS booking value

**This moves the date more than anything else in this document, and it applies today, before any expansion.**

If Cleansia is the **principal** — it sells the clean and buys the labour — then the whole order total is its turnover. If it is an **agent**, only the commission counts. The difference is roughly 4–5×.

The code answers this unambiguously, in five independent places:

- `PayCalculatorExtensions.cs:8-23` — the cleaner's pay is computed **entirely from Cleansia's own rate card** (`config.BasePay`, `config.ExtraPerRoom`, `config.ExtraPerBathroom`, `config.DistanceRatePerKm`), then clamped by Minimum/MaximumPay. **The customer's price appears nowhere in the calculation.** Cleansia buys labour at its own price, sells a clean at its own price, and keeps the spread.
- There is **no commission or platform-fee concept anywhere** — grep for `platformfee|servicefee|bookingfee|commission` over `src/` returns zero hits.
- `DefaultInvoiceLayoutBuilder.cs:11-14`: "the cleaner supplies the work and is paid for it, so the cleaner is the SUPPLIER and Cleansia is the CUSTOMER."
- `ReceiptService.cs:374-378` — the customer receipt carries `order.TotalPrice` under Cleansia's own company identity.
- `cs.json:1751` — "Cleansia poskytuje profesionální úklidové služby pro domácnosti", and `:1757` says Cleansia carries the liability for the clean.

**That is a buy-sell principal on every VAT indicator that matters.** Put it to the accountant as a confirmation, not an open question — but confirm it, because it sets your registration date.

### 2.3 Where VAT is due for a Slovak or Polish job

**Where the flat is.** Art 47 + Art 31a(2)(k), covered in §1.3. Slovak flat → Slovak VAT. Polish flat → Polish VAT. Same company, same customer, three tax jurisdictions.

**Slovak and Polish revenue does NOT count toward the Czech obrat.** §4a ZDPH counts only "uskutečněná plnění **s místem plnění v tuzemsku**", and GFŘ Informace č.j. 82973/24 says it in terms: "Pro účely § 6 ZDPH, tedy pro vznik plátcovství z titulu překročení obratu, je ... i nadále rozhodující pouze obrat v tuzemsku (nikoli obrat v EU)."

*(One tempting escape route is closed: if you thought "we're a platform, so the general B2C rule puts the supply in Czechia" — no. Art 45 is residual and is never reached. If you act in your own name, Art 28 deems you to have received and supplied the same cleaning service (CJEU C-464/10 Henfling ¶35-36), so Art 47 still governs. As a disclosed agent to a consumer, Art 46 places it where the underlying transaction is. As an electronically supplied service, Art 58 places it with the customer — and Reg 282/2011 Art 7(3)(u) excludes services of this family "booked online" from ESS anyway.)*

**But a non-established supplier in Slovakia has a NIL threshold** — §5 of zákon 222/2004 requires registration "pred začatím vykonávania činnosti, ktorá je predmetom dane". Slovakia's own €50,000/62,500 limits are for locally-established businesses only. Without relief, **the first Bratislava booking is a registration event.**

### 2.4 Does OSS apply? Yes — and this is the good news

**Slovakia and Poland are a LIGHT operation, not a heavy one.** A Czech-established company declares Slovak and Polish VAT on **one quarterly Czech OSS return**, with **no Slovak or Polish VAT number, no foreign tax agent**.

This is not an inference from silence — the first lane worried it was, and two independent adversarial passes found the mechanism:

- **Art 369b** (as replaced by Directive (EU) 2019/1995, applying 1 July 2021) admits "a taxable person not established in the Member State of consumption supplying services to a non-taxable person", and closes "This special scheme applies to **all** those goods or services supplied in the Community." No service-type carve-out.
- **Art 369a(3)(a)** defines "Member State of consumption" for services as "the Member State in which the supply is deemed to take place according to **Chapter 3 of Title V**" — the whole chapter, which contains Article 47. **Positive cross-reference, not absence.**
- The Commission's Explanatory Notes §3.2.2, under the heading "3.2 THE UNION SCHEME": a supplier "can also declare all other cross-border supplies of services to non-taxable persons taking place in the EU. Regarding examples of services that can be declared under the Union scheme, please see section 3.1.3" — and §3.1.3's list names "Services connected to immovable property".
- **Slovakia's own tax authority says it**, in its Union-scheme methodological guideline §3.2: the Union scheme covers "všetky služby poskytnuté nezdaniteľným osobám s miestom dodania podľa § 16 zákona o DPH" and lists "**služby vzťahujúce sa na nehnuteľnosť**" among them. §16 is the Slovak immovable-property place-of-supply rule. The authority that would challenge your return names your case under the Union heading.

*(Cite the **July 2026 revisions** of the Commission Explanatory Notes and OSS Guidelines, not the September 2020 / March 2021 editions the first lane used. The substance survives verbatim; the citations are stale.)*

**Four conditions the OSS answer carries, and all four matter:**

1. **You must first hold a Czech VAT registration** — either *plátce* or *identifikovaná osoba*. Finanční správa: "Podmínkou registrace do režimu EU je registrace k DPH jako plátce nebo identifikovaná osoba." **"Identifikovaná osoba" is the light path: it does not make you a Czech plátce and does not force Czech VAT onto your Czech sales, yet it unlocks OSS.** That is very likely the right posture for a Slovak launch below the Czech threshold — confirm it.
2. **No fixed establishment in the country of consumption.** "For a fixed establishment to be considered as such, it should have a sufficient degree of permanence and a suitable structure in terms of human and technical resources." A mere VAT number is not one (Reg 282/2011 Art 11(3)). **This is the live risk for a labour-on-the-ground business: a Bratislava coordinator, a depot, or equipment stored in-country could eject Slovakia from OSS into a full Slovak registration.** Make that a gated business decision, not an ops detail.
3. **OSS is all-or-nothing.** Once enrolled, every eligible cross-border B2C supply must go through it. **There is no "ship Slovakia now, handle Poland by hand later" option.** The day OSS switches on, the system must rate every non-establishment supply correctly per country.
4. **No input-VAT deduction in an OSS return.** Slovak VAT charged by VAT-registered Slovak cleaners is recoverable only via Directive 2008/9/EC refund claims. Accounting cost, not code.

**And Czech jobs can never go through OSS** — they go in the domestic Czech return. **Engineering consequence nobody has stated: the same Orders table feeds two reporting destinations, split by the property's country.**

### 2.5 Does the EU SME scheme apply? Probably, and it may make year one a non-event

From 1 January 2025, if your **Union-wide** turnover stays under **€100,000** and your Slovak turnover stays under Slovakia's limit (€50,000 prior year / 62,500 current), you get an **"EX" number** from the Czech tax office and supply Slovak jobs **exempt** — no Slovak VAT charged, no OSS return, no rate-selection code needed on day one. Poland the same, under PLN 200,000 (240,000 from 1 January 2026); Poland repealed art. 113 ust. 13 pkt 3, which had barred non-residents, in favour of art. 113a.

Critically, the VAT Committee agreed **unanimously** that "the taxable person shall not be required also to apply the domestic exemption" — **you can be a Czech VAT payer and still be SME-exempt in Slovakia.** And SME and OSS can coexist *across* countries but not *within* one: "it is not possible to apply both the SME and OSS Union schemes at the same time in one same jurisdiction." So "VAT payer in CZ, SME-exempt in SK, OSS in PL" is a real configuration a data model must be able to represent. **One global `IsVatPayer` bool cannot.**

**The two triggers collide, and this is the planning insight.** CZK 2,536,500 was set as the equivalent of €100,000 — the SME Union cap. **Your Czech VAT registration and your loss of the EU SME exemption arrive at almost exactly the same moment. Plan them as one prepared event, not three surprises.**

### 2.6 Rates — and one live wrong number in your seed

| | Standard | Cleaning of a private flat |
|---|---|---|
| Czechia | 21% | 21% — moved *off* the reduced rate on 1 Jan 2024 |
| **Slovakia** | **23% since 1 Jan 2025** | 23% — on neither reduced list (19%, 5%) |
| Poland | 23% | 23% for interior; 8% only for exterior/common-area |

**`sql-scripts/insert_seed_data.sql` seeds SVK at `0.20, 0.10`. Both figures are dead — the standard rate is 23% and the 10% reduced rate was abolished. The first Slovak invoice would under-declare Slovak VAT by three points.** CZE `0.21, 0.15` and POL `0.23, 0.08` are correct.

**And the 20% is seeded TWICE, not once** — re-verified by hand: `insert_seed_data.sql:894` (the
per-country fiscal/company row, `true, 0.20, false, 'PDF'`) and `:979` (the `CountryConfiguration`
row, `0.20, 0.10`). Fixing one leaves the other, and they feed different readers. Step 1 must change
both.

Secondary: Slovakia now has **two** reduced rates (19% and 5%) and `CountryConfiguration` holds exactly one nullable `ReducedVatRate`. Neither applies to cleaning, so this does not block launch — but the model cannot represent Slovakia's rate structure, and `ReducedVatRate` is never read anywhere.

Do **not** hardcode "cleaning is always standard-rated". Annex III point (10b) still permits a reduced rate for "window-cleaning and cleaning in private households" and was not deleted by Directive (EU) 2022/542.

### 2.7 The VAT formula bug — worse than the first pass recorded

`VatCalculator.cs:24` computes `totalPrice * rate / (100 + rate)` against a column that is physically a **fraction** (`numeric(5,4)`, seeded `0.21`). A 98.8% under-declaration the day `IsVatPayer` flips.

**The first pass found one site. There are three, and I verified all three by hand today:**

- `src/Cleansia.Core.AppServices/Services/VatCalculator.cs:24`
- `src/Cleansia.Core.AppServices/Features/Refunds/IssuePartialRefund.cs:234`
- `src/Cleansia.Core.AppServices/Features/Refunds/RefundAllocator.cs:93`

Fixing only the first leaves the refund path under-declaring VAT on credit notes by the same margin. The correct convention is already used at `CountryInvoiceContext.cs:37` (`grossTotal / (1m + VatRate)`).

Two more, both real:

- `DefaultReceiptLayoutBuilder.cs:320` formats the fraction with `:N0` — **renders 0.21 as "VAT 0%"** next to a non-zero VAT amount. A defective tax document, visible to the customer.
- `VatCalculator.cs:14-16` returns `VatBreakdown.NotApplicable(totalPrice)` — **zero VAT, silently** — whenever `countryConfig == null`. Under OSS that is a silent under-declaration to a foreign tax authority. And `OrderFactory.cs:204-205` falls back from `GetActiveByCountryAsync(countryId)` to `GetActiveCompanyInfoAsync()`, so a seeded SVK CompanyInfo with `IsVatPayer=false` would zero out Slovak VAT that is genuinely owed.

**With three live jurisdictions, this is an under-declaration to three different tax authorities. Fix it before anything else. It is independent of every currency decision.**

### 2.8 What you can rely on, and what the accountant must confirm

**Rely on (multiple primary sources, adversarially verified):**
- VAT is due where the flat is, for B2C. Art 47 + Art 31a(2)(k).
- Slovak/Polish revenue does not enter the Czech §4a obrat.
- CZK 2,000,000 / 2,536,500, calendar year, 10 working days, and 2,536,500 is fixed.
- Slovakia is 23%; your seed is wrong.
- Currency does not affect VAT.

**Ask the accountant, in this order:**
1. **Is our obrat the gross booking value or only the margin?** (§2.2. Highest-leverage question in this document; moves the registration date by 4–5×.)
2. **Confirm in writing that we may declare Slovak VAT on a Bratislava clean through the Czech Union OSS return, with no Slovak registration.** (Downgraded from blocker to routine confirmation — the evidence is now Directive-level and confirmed by Slovakia's own guideline.)
3. **Can we register for OSS as an *identifikovaná osoba*, below the Czech plátce threshold?** (Decides whether the Slovak launch needs Czech VAT registration first.)
4. **Can a Czech company obtain the SME "EX" number?** I verified Slovakia's and Poland's *inbound* implementations. **I did not verify the Czech outbound half.** If Czechia has not implemented it, the SME route is unavailable and Slovakia goes straight to OSS.
5. **Do cleaners working regularly in Bratislava create a fixed establishment?** Genuinely contested EU VAT law and an unusually live risk here. Ask specifically about a local coordinator, a depot, and stored equipment. Berlin Chemie (C-333/20) helps on the *receiving* side but the OSS test is the *supplying* side (Art 11(2)) — an easier test to trip.
6. **Does the Czech OSS return go in CZK or EUR?** Decides which conversion the platform must compute and store.
7. **Poland's exact rate for a residential flat clean** (PKWiU 81.21/81.22 at 23% vs 81.29.12.0 at 8%). PKWiU classification is the taxpayer's responsibility and a known dispute area. A binding rate ruling (WIS) may be worth obtaining before the Polish launch.
8. **§6h ZDPH** — services *received* from a foreign taxable person. You buy from Stripe (Irish entity). Whether the financial-services exemption saves you is unresolved and **may already be live**, unrelated to Slovakia.
9. **Do Slovakia and Poland require a B2C invoice at all?** The Directive does not, and under OSS the invoicing rules of the Member State of identification (Czechia) apply — "one invoice format for all three countries" is a real simplification. Poland's KSeF machinery makes it worth confirming.

**Bottom line for the expansion plan:** Slovakia is a light operation. It does not need a Slovak entity, a Slovak VAT number or a tax agent. What it needs is correct per-country rates, correct place-of-supply, and a VAT posture the data model can express per country. **The one thing that would make it heavy is a fixed establishment — so keep the Slovak operation contractor-based and remote-coordinated until you have deliberately decided otherwise.**

---

## 3. Decision 6 — Cleansia Plus, and closing the loop with decision 2

**Plus does not block on decision 2, and decision 2 should not be decided by Plus. The dependency you named dissolves.**

**Plus is the cheapest surface to make multi-currency, not the hardest.** The first research lane called it a structural blocker; the adversarial pass refuted that and I verified the code myself.

- **The charged price does not live in Cleansia at all.** `MembershipPlan.cs:44-47` says "Canonical price lives in Stripe (referenced via StripePriceId)"; `MonthlyPriceCzk` is a display mirror. `StripeClient.cs:412-427` sends only `Price = stripePriceId` and no amount.
- **One Stripe Price can carry all three currencies.** "A single Price can support multiple currencies", via `currency_options` — e.g. `currency_options[eur][unit_amount]=799`. Stripe's compatibility table marks "Multi-currency prices" as **"✓ Supported"** for both Checkout **and Subscriptions**. So "one `StripePriceId` per plan" is not evidence of single-currency, and **no second Price object is needed.** This improves on the first pass, which proposed a second Stripe Price per plan.
- **Manually defined multi-currency prices override Adaptive Pricing for those currencies, even if it's enabled** — which structurally immunises Plus against the FX-per-renewal behaviour ruling 1 deleted. Do **not** use Adaptive Pricing for subscriptions: it "will use real-time exchange rates for each billing cycle, resulting in varying local prices based on fluctuations."
- **The Plus discount is a percentage and needs no currency at all.** `OrderFactory.cs:88-89` computes `RawSubtotal * (DiscountPercentage / 100m)`. Free-cancellation is hours; express waiver is a count. **A CZK Plus member booking a EUR clean gets 5% off the EUR total, correctly, with zero code change.**

**So the answer to your note — "a Slovakian user has to pay in euros unless he can select the currency" — is: he pays in whatever his flat's market uses, and his Plus is billed in the market he subscribed from. Those are allowed to differ, and nothing breaks when they do.**

**The design:**

- **`UserMembership.BillingCurrencyId`** — required, set at subscribe, **never mutated**, defaulted from the market selector (or the default saved address's country, whichever the customer's context supplies), and **rendered on the management screen**: "Plus bills in CZK". The mismatch becomes a labelled state instead of a silent one.
- **One Stripe Product "Cleansia Plus", two Prices (monthly, yearly), each carrying `currency_options` for CZK, EUR and PLN.** Stripe's own guidance: "When to add a price to an existing product: Same plan, different billing interval; **Same plan, different currency**."
- **Pass `currency` explicitly** on `SubscriptionCreateOptions` and on the membership `SessionCreateOptions`. Today neither sets it. Stripe: "If you create subscriptions directly, the multi-currency prices don't take effect until you pass the `currency` parameter", and Checkout "automatically determines the customer's local currency from their IP address" if you don't. **If `currency_options` are added without setting `Currency`, Plus starts pricing by buyer IP — a third rule that is neither the address nor the customer's choice, with no Cleansia deploy.** That is a defect-in-waiting neither prior pass named.
- **Per-currency display mirror rows** (`MembershipPlanPrices`), replacing `MonthlyPriceCzk`. This is required, not cosmetic: `GetMembershipPlans.cs:56-66` computes a `monthlyBaseline` as the min across monthly plans and derives a "Save 15%" badge from it. **With CZK and EUR plans as sibling rows the baseline becomes 7.99 and the badge renders nonsense with no error.** Scope the baseline to a currency.
- **`SwapMembershipPlan` cannot cross currencies.** It calls `SubscriptionService.UpdateAsync` on the existing item (`StripeClient.cs:350-367`), changing the Price, not the currency. Constrain swaps to plans priced in the membership's existing billing currency and add a refusal path. Whether Stripe silently keeps the old currency when the new Price carries the target only in `currency_options` is **unverified — test it in sandbox.**

**Product ruling I need from you (I recommend, but it is yours):** should a CZK Plus member get 5% off a EUR order? **I recommend yes** — the code already does it with no change, the discount is a proportion of that market's authored price so it costs the same margin everywhere, and Wolt's Subscription Country binding exists because a Wolt+ benefit is free delivery on a *specific market's* order economics, which is not your shape. Binding Plus benefits to a market would mean a Prague member gets nothing on a Bratislava flat — harsh across three adjacent countries with cross-border movement. **Price pinned, benefit universal.**

**Two Stripe facts that need product flows, not code:** the cancel-at-period-end lock (edge case 5) and the abandoned-checkout 23-hour lock (edge case 15).

**One more:** `hero_trial_price` (web ×5 locales) and `membership_hero_trial_price` (Android ×5) may be **dead copy** — trials are now forbidden on every plan (`CreateMembershipPlan.cs:84-86` requires `TrialPeriodDays == 0`, per the 2026-09-08 ruling). Check before translating them into three currencies; deletion is likely cheaper.

---

## 4. The settled rulings — confirmed, with what the research changed

### Ruling 1 — author a price per currency. **Confirmed, and strengthened.**
Wolt does exactly this in your exact three markets. Two changes to how it should be implemented:

- **Line snapshots become mandatory, not polish.** `OrderService` and `OrderPackage` are pure join rows with no money column (verified: `OrderService.cs` has only OrderId/ServiceId). `Order.Extras` is a `Dictionary<string,bool>` JSON column with no price. **Two live consumers re-read the live catalogue for a historical order**: `ReceiptService.cs:361-368` and — this is a money path, not a display path — `IssuePartialRefund.cs:251-253`, whose own comment says so. Under Option B `Service.BasePrice` **stops existing**, so both readers break. Snapshots are what make Option B safe.
- **Delete `Currency.ExchangeRate` outright**, not "retain for reporting". Nothing snapshots it, so editing it retroactively restates every historical euro order. A present rate column is how someone writes `× ExchangeRate` in a report next year and it compiles. *The risk I'm accepting, stated: "delete now, build the right thing later" can become "delete now, do it in Excel forever". Cross-currency roll-ups group by currency, which is what a per-currency bank account reconciles against anyway.*
- **And the rate comes back on the tax document** (§1.3). Budget for a rate source at VAT registration; just never let it near the price.

### Ruling 3 — Slovakia, then Poland. **Confirmed. Two blockers found.**
- **The Slovak VAT rate in the seed is 20%; it must be 23%** (§2.6).
- **Poland's `CountryConfiguration.PayoutScheme` is seeded NULL** where Czechia's and Slovakia's are `1` (verified in the seed today). **Polish cleaners cannot be paid until that is answered — it is a business question, not a code gap.**
- Slovakia is otherwise fully configured: EUR, `sk`, Europe/Bratislava, +421, IČO/IČ DPH labels, Stripe, payout scheme 1. `IsServiced` is false; service cities are seeded for CZE only.

### Ruling 4 — per-currency loyalty divisor on `LoyaltyTierConfig`. **Confirmed in intent; I recommend a change to where it lives.**
- `LoyaltyTierConfig`'s unique index is `(TenantId, Tier)` — 4 tier rows. **The divisor is not tier-dependent.** Putting it there gives 12 cells holding 3 values with 4 chances to disagree about CZK. **Recommend: the divisor on `Currency`; the currency dimension on `LoyaltyTierConfig` for the FLOOR only** (`MinimumOrderAmountForDiscount`, currently a uniform `1000.00`).
- **There is a SECOND divisor nobody named.** `LoyaltyService.cs:44` is the earn site; **`LoyaltyService.cs:163` is the partial-refund clawback**, also `/ 10m`, verified today. Fixing only :44 makes earn and clawback asymmetric — a refunded euro order returns the wrong number of points, in the customer's favour, with no error.
- **Points stay pooled.** `LoyaltyAccount` is unique on UserId alone and `LifetimePoints` is a bare int; thresholds compare counts. Zero schema work. **The corollary is a hard one: the divisor is the only thing between a euro and a crown earning the same rate. A mis-authored EUR divisor of 10 is a ~24× earning exploit.** It needs a `> 0` validator and a test pinning the three launch values.
- The rule is stated verbatim in more copy than the first pass counted: five web keys × five locales, Android and iOS perk strings, **and one of the key names is seeded into the database** (`loyalty.perks.discount_5_above_1000` inside `PerksJson`). Renaming to a placeholder key edits the seed, not just locale files.

### Ruling 5 — one credit account per customer per currency. **Confirmed. Four implementation notes.**
- The key becomes `(UserId, CurrencyId)`. Both columns are NOT NULL, so the nullable-`TenantId` landmine does not apply — and the existing index already deliberately excludes `TenantId` for exactly that reason (verified in `CreditAccountEntityConfiguration`).
- **The 1:1 nav must become 1:many.** `builder.HasOne(a => a.User).WithOne()` → `.WithMany()`. `User` has no inverse collection, so a bare `.WithMany()` is correct — **but prove it with `Cleansia.IntegrationTests` before believing it** (see the EF `WithMany` shadow-FK landmine in memory).
- **`EnsureForUserAsync` ignores the `currencyId` it is passed** — verified: the predicate is `a.UserId == userId` only. And `GetSpendableAsync(userId)` takes no currency and returns `FirstOrDefault`, which with N rows becomes an arbitrary pick. **This supersedes the first pass's §3.4**, which asked `EnsureForUserAsync` to *refuse* a mismatched currency — under ruling 5 there is nothing to refuse.
- **`ExpireCustomerCredit` uses the client's `RequestId` verbatim as the ledger idempotency key** (verified at `:92`) and `CreditTransactions.IdempotencyKey` is globally unique. If the admin discharge drains N accounts, the key must become `$"{RequestId}:{account.Id}"` or the second row raises 23505 — 500ing the action that exists to unblock GDPR deletion.
- **No transfer between balances.** A transfer is a conversion, needs a rate, and would put that rate on the least-travelled path in the system where a wrong one lives longest. A stranded balance is discharged and re-granted in the spendable currency — two ledger rows a human decided.
- **The customer sees one row per non-zero balance. Never a sum, never a converted total.**

### Ruling 7 — build an admin CRUD for Extras. **Confirmed as net-new scope. Recommend deactivate-only.**
Verified: `Features/Extras/` contains exactly `GetExtraOverview.cs` and one DTO. There is **no `AdminExtraController`** in `src/Cleansia.Web.Admin/Controllers/` (32 controllers, none for Extras). `IExtraRepository` is an empty marker. The domain mutators already exist (`Extra.Create`, `Extra.Update`) and `Update` correctly refuses a slug change.

**Do not build hard delete.** `Order.Extras` is a JSON slug dictionary with no join table, so an `IsInUseAsync` has no cheap form. Deactivate-only matches what `GetExtraOverview` already filters on and removes a command, a validator, a controller action, a `CanDeleteExtra` permission with its six registration sites, and a JSON-containment query for a five-row table.

Build the price as **per-currency rows from day one**, or the form gets written twice. Admin-only, so **no mobile spec re-dump and no Mac needed.**

### Ruling 8 — keep only the operated currencies active. **Confirmed in intent, but the first pass was wrong about the cost.**
The first pass said "Zero code, one data change" (T-0688:311). **It is not.** I verified today: `IsActive` lives on `BaseEntity`; `CurrencyRepository` (all 39 lines) never mentions it; `BaseRepository.ExistsAsync`/`GetByIdAsync` apply no predicate; there is no `HasQueryFilter` outside the tenant filter; `CurrencyListItem` does not carry the flag; and `AdminCurrencyController` has six endpoints (overview, details, create, update, set-default, delete) — **no activate/deactivate**.

**Setting `IsActive = false` on a currency changes no read path anywhere.** Ruling 8 needs code: a deactivate command that refuses the default currency, an `IsActive` predicate in resolution, a filter on the **anonymous** `Partner/CurrencyController.GetOverview` (on the anonymous allow-list, returns all twelve currencies unfiltered), and `IsInUseAsync` extended to CreditAccounts and PromoCodes.

Two adjacent defects found while verifying, both real:
- `CurrencyRepository.GetDefaultAsync` has a **dead null guard** — the `??` binds to the `Task`, not the awaited result, so the throw can never fire and a null escapes behind the `!`, NREing on the most-travelled path in the platform. Verified verbatim.
- `Currencies` has **no unique index on `Code`** and none on `IsDefault`. The column is `citext`, so `UQ(Code)` gives case-insensitive uniqueness free.

---

# PART 2 — THE SYSTEM WE ARE BUILDING

Described as it should be, not as a diff. There is no production, no migration to write and no compatibility to preserve — so this is designed right, not cheap.

## 2.1 The money model

**One currency per aggregate; every decimal on that aggregate is denominated by it.** Not a `Money` value object: EF Core 10 complex types "cannot contain navigation properties" and indexes into them arrive only in EF 11, so a `Money` type would lose the FK to `Currencies` and could not be indexed, while `Orders` would grow eight redundant `CurrencyId` columns. The query enforces the invariant; the type does not.

**Conventions, legislated once:**
- Every money column is `numeric(18,2)`. This fixes `Extras.Price` at `(10,2)` and six bare `numeric` columns on `Orders` (`NetAmount`, `VatAmount`, `AppliedVatRate`, `TravelDistance`, `CancellationRefundAmount`, `CancellationFeeRate`) — the first pass found three.
- **Every rate is a fraction in [0,1] in `numeric(5,4)`. The suffix is always `Rate`. `Percent`/`Percentage` is banned.** The platform currently holds five rate columns in two conventions. `MembershipPlan.DiscountPercentage` becomes `DiscountRate` with its seed divided by 100; `RefundStripeFeeRate` is rebased from 1.4 to 0.014 and its `/100m` deleted.
- `TravelDistance` is not money — `numeric(9,2)`.

**`Currency`**: `Code` (immutable after create, unique, citext), `Symbol`, `Name`, `IsActive` (now actually read), `IsDefault` (filtered unique index, exactly one row), **`LoyaltyPointsDivisor`**. `ExchangeRate` is **deleted**.

## 2.2 Price authoring

Three narrow tables, because `Service` has two money columns while `Package` and `Extra` have one each:

- `ServicePrices(ServiceId → Services, CurrencyId → Currencies, BasePrice, PerRoomPrice)` — UQ`(ServiceId, CurrencyId)`
- `PackagePrices(PackageId, CurrencyId, Price)` — UQ`(PackageId, CurrencyId)`
- `ExtraPrices(ExtraId, CurrencyId, Price)` — UQ`(ExtraId, CurrencyId)`

**None of these unique indexes contains `TenantId`.** The catalogue is platform config, and `ExtraEntityConfiguration` already made and documented that exact decision for the slug index — this follows an existing ruling, not a new judgement, and sidesteps the nullable-`TenantId` landmine by construction.

A single discriminated price table is worse: it cannot FK a polymorphic ItemId, needs a second amount column meaningless for two of three types, and adds a discriminator nothing else uses. JSON-on-the-item is worse still — it cannot carry the unique constraint, and the constraint is the whole arbiter.

**`Service.BasePrice`, `Service.PerRoomPrice`, `Package.Price` and `Extra.Price` are deleted outright.** No "default currency price" beside a price table — two rows that can disagree, and every reader forced to know which wins. **Consequence, and it is the correct rule: you cannot create a catalogue item without pricing it in at least one active currency.** The Extras admin CRUD is built to that shape.

Within-package apportionment keeps reading live `PackageService.PriceWeight` — weights are dimensionless, so currency never touches them. Deliberate, not an omission.

## 2.3 How currency is decided for an order

**From the service address country, exactly once, on the server.**

`Address.CountryId` → `CountryConfiguration.DefaultCurrencyId` → `Currency`. `DefaultCurrencyCode` becomes `DefaultCurrencyId`, a real FK, so deactivating a currency cannot silently orphan a market's default.

- `QuoteOrder.Command` **gains a country** and **loses `CurrencyId`**. `CreateOrder.Command` **loses `CurrencyId`** and derives from the address it already resolved two statements earlier.
- All three clients currently send `null` for currency, so removing it breaks nothing. Adding the country is a real wire change to both committed mobile specs and all three NSwag clients.
- The **market selector** supplies the country for address-less surfaces (home calculator, catalogue, Plus). It is a browse-market choice, not a money axis. Persisted as `User.PreferredMarketCountryId`, modelled on `PreferredLanguageCode`, and defaulted from the site the visitor landed on. **Never from the card BIN, never from IP-derived residence.**
- **A country picker on the web address step is required at launch regardless of any of this.** `OrderAddressResolver` returns `CountryRequired` once more than one country is serviced, and the wizard has no country UI. Do not credit that cost to this design.

## 2.4 Where VAT comes from

**From the same `Address.CountryId`, via `CountryConfiguration.StandardVatRate`.** Already correct at `OrderFactory.cs:203-209`, and it stays. `Order.AppliedVatRate` remains a snapshot.

Currency and VAT now come from the **same row for the same address**, which is a new invariant this design adds. *(Correction to the first pass, which claimed they "agree by construction" today — they do not; they are resolved from two different places and nothing reconciles them.)*

**Fail closed, not open.** `VatCalculator` must **throw** when `countryConfig` is null rather than silently returning zero VAT, and the `GetActiveCompanyInfoAsync()` fallback in `OrderFactory` is deleted — under OSS there is exactly one supplier, the Czech entity.

## 2.5 The multi-jurisdiction model

Three countries, three VAT rates, and **a per-country VAT posture that one boolean cannot express**.

`CountryConfiguration` gains **`VatTreatment ∈ { NotRegistered, DomesticReturn, Oss, SmeExempt }`**:

- **CZE = `DomesticReturn`** once registered — Czech jobs can never go through OSS.
- **SVK = `SmeExempt`** at launch (no Slovak VAT charged), moving to **`Oss`** when SME headroom runs out or a Czech registration lands.
- **POL = `SmeExempt`** then `Oss`.

`CompanyInfo.IsVatPayer` stays as the Czech-entity fact it is. **One legal entity, one CompanyInfo row, three tax postures.** Reporting splits the same `Orders` table into two destinations by the property's country: the Czech domestic return, and the OSS return.

**Under OSS there is one invoicing regime — Czechia's** (Art 219a(2)(b): "the rules applying in the Member State where the supplier making use of one of the special schemes ... is identified"). That is a real simplification and an argument against per-country invoice layouts.

**Because currency is pinned to the place of supply, the Art 230 restatement can never arise.** CZE→CZK, SVK→EUR, POL→PLN. That is the single largest payoff of §1.6 and it should be written down as a design property, not rediscovered.

## 2.6 Subscriptions

- One Stripe Product, two Prices (monthly, yearly), each carrying `currency_options` for CZK/EUR/PLN. `MembershipPlan.StripePriceId` stays a scalar.
- `MembershipPlanPrices(PlanId, CurrencyId, Amount)` — display mirrors only, no per-row Stripe id. **`MonthlyPriceCzk` and `MonthlyEquivalentPriceCzk` are deleted from the entity and off the wire.**
- `UserMembership.BillingCurrencyId` — required, immutable, rendered on the management screen.
- Every Stripe call that can carry a currency **sets it explicitly**.
- The savings-badge baseline is scoped to a currency.
- Swaps are constrained to the membership's billing currency.
- The admin plan editor validates against Stripe's per-currency minimum charge (15.00 CZK / 0.50 EUR / 2.00 PLN) — today it accepts anything ≥ 0, so an unchargeable price is authorable and fails only at the first invoice.

## 2.7 Credit

- `CreditAccounts` UQ`(UserId, CurrencyId)`; `User` → many accounts.
- `GetSpendableAsync(userId, currencyId)`. The three spend sites lose their `spendable.CurrencyId != order.CurrencyId` guards — a simplification.
- `IssueCustomerCredit.Command` carries a required `CurrencyId`; the admin dialog gains a picker.
- Expiry is already per account with its own `ExpiresOn` clock. **A CZK booking must not extend a EUR clock.** Free and correct today.
- `GdprDeletionService.HasPositiveCreditBalanceAsync` must see **any** account; `ExpireCustomerCredit` drains all of them under per-account idempotency keys.
- The customer sees one row per non-zero balance and a plain sentence at a foreign-currency checkout. **No sums, no conversions, no silent zeroes.**
- The no-show apology (`BookingPolicy.NoShowCreditCzk = 250m`) becomes a per-currency lookup. **`agents/tools/check-booking-policy-parity.mjs` reads it with a scalar-const regex and fails the repo-root parity workflow when the read returns null — the checker changes in the same commit.** The push cannot interpolate an amount at all (the lock-screen loc-arg allowlist is a closed `{orderNumber, count}` set, ADR-0025 D3), so the push copy drops the number. The home-page promise keeps its number, as a placeholder, **because the market selector means the anonymous page knows its market.**

## 2.8 Loyalty

- `Currency.LoyaltyPointsDivisor` — one number per currency, validated `> 0`, pinned by a test at the three launch values. Both the earn site and the clawback site read it.
- `LoyaltyTierConfig` UQ`(TenantId, Tier, CurrencyId)`, keeping `.AreNullsDistinct(false)`, for `MinimumOrderAmountForDiscount` only.
- Points stay pooled.
- `GetLoyaltyTiers` takes a currency and `TierInfo` carries one on the wire.

## 2.9 The catalogue and its admin surfaces

- Per-currency price editors for Services and Packages, showing a missing price row as **missing**, never defaulting to zero.
- **A full Extras admin: list, create, update, activate/deactivate, with per-currency prices.** No hard delete. Reuses `CanUpdateExtra` for activation, as `CatalogLifecycleEndpointPermissionTests` asserts for every other catalogue entity. Six permission registration sites, two of which are gate tests that fail on omission.
- **Fail closed:** one repository method returns `(item, price)` pairs joined on the requested currency, and the pricing calculator **fails** on a missing row rather than null-coalescing it.
- A currency activation/deactivation surface that refuses to deactivate the default or a currency holding credit, promo or order rows.

## 2.10 Receipts and invoices

- Every line item is built from the **order's snapshot**, never from the live catalogue.
- The receipt gains the lines it is missing today: extras, the express surcharge, and each discount. **This is broken in pure crowns today** — a 950 service + 200 extra + 20% surcharge prints one line of 950 against a total of 1,380.
- The VAT rate prints as a percentage of a fraction (`:P0`-shaped), not `:N0`.
- The cleaner's payout invoice currency is **derived from the `OrderEmployeePay` rows being invoiced** and fails if they disagree. `OrderEmployeePay` gains `CurrencyId`; `EmployeePayConfig`'s unique index gains it too (keeping `.AreNullsDistinct(false)` — it has nullable `EmployeeId`/`ServiceId`/`PackageId`). `CurrencyResolutionService`'s geography-derived route is deleted from the invoice path.

## 2.11 What Stripe holds

- **One Customer per user.** Verified safe: PaymentIntents do not lock.
- **One Product, two Prices with `currency_options`** for Plus.
- Order charges: inline `price_data` in the order's currency **plus an explicit `SessionCreateOptions.Currency`**; mobile PaymentIntents already carry the currency.
- **Adaptive Pricing off, or overridden everywhere by explicit currency.**
- Sandbox objects recreated fresh rather than edited: the API says only `metadata`, `nickname` and `active` are updatable on a Price, while the Dashboard offers retroactive multi-currency. **There is no production data to protect — create new, archive the two seeded Prices, update the seed.**

## 2.12 What is DELETED

| Deleted | Why |
|---|---|
| `Currency.ExchangeRate` — column, entity property, admin form field, DTO field, `UpdateCurrency` validation | No rate in the pricing path, ever |
| The exchange-rate scaling in `OrderPricingCalculator` (`currency?.ExchangeRate ?? 1m` and the `* exchangeRate` below it) | Same |
| `Service.BasePrice`, `Service.PerRoomPrice`, `Package.Price`, `Extra.Price` | Replaced by price tables; a second source can disagree |
| `Orders.Extras` (the `text` JSON column) | Replaced by `OrderExtras` with a real FK and a snapshot price |
| `CurrencyId` on `QuoteOrder.Command` and `CreateOrder.Command` | Currency is a server fact derived from the address |
| `MembershipPlan.MonthlyPriceCzk`, `MonthlyEquivalentPriceCzk` — entity and wire | Replaced by per-currency mirrors |
| The `GetActiveCompanyInfoAsync()` fallback in `OrderFactory` | Under OSS there is one supplier; the fallback silently borrows another country's VAT posture |
| `VatBreakdown.NotApplicable` on a null `countryConfig` | Fail-open under-declaration to a foreign tax authority |
| `CurrencyResolutionService`'s geography route on the payout-invoice path | The invoice currency comes from the rows invoiced |
| Ten seeded currencies (USD, GBP, CHF, SEK, NOK, DKK, HUF, RON, BGN, and any other unoperated) | Ruling 8 |
| `DeleteExtra` — never built; explicitly out of scope | No cheap `IsInUseAsync` against a JSON slug map |
| `hero_trial_price` / `membership_hero_trial_price` (web ×5, Android ×5) — **pending a check** | Trials are forbidden on every plan |
| `check-booking-policy-parity.mjs`'s scalar `NoShowCreditCzk` read | The constant becomes per-currency |

## 2.13 The invariants

A reviewer can check code against these.

1. **An order's `CurrencyId` equals `CountryConfiguration.DefaultCurrencyId` for the order's `Address.CountryId`.** No other route sets it.
2. **An order's `AppliedVatRate` comes from `CountryConfiguration` for the same `Address.CountryId`** as its currency.
3. **No API command accepts a currency id from the caller** on any order or quote path.
4. **No exchange rate exists anywhere in the pricing path.** `Currency` has no rate column, and no money value is ever multiplied by one.
5. **Every money decimal on an aggregate is denominated by that aggregate's single `CurrencyId`.** No aggregate holds two currencies.
6. **A catalogue item with no price row in a currency is not offerable in that currency.** The pricing calculator fails; it never coalesces to zero and never converts.
7. **Every order line carries its own snapshot price.** No consumer of a historical order ever reads the live catalogue.
8. **A receipt's lines sum to its total**, in every currency, including extras, express surcharge and every discount.
9. **A rate column is a fraction in [0,1] stored as `numeric(5,4)` and named `*Rate`.** No `Percent`/`Percentage` identifier exists.
10. **Every money column is `numeric(18,2)`.**
11. **`VatCalculator` throws when it cannot resolve a country configuration.** It never returns zero VAT for a missing config.
12. **`CountryConfiguration.VatTreatment` is the only source of a country's VAT posture.** No global `IsVatPayer` decides a foreign supply.
13. **A credit account is unique on `(UserId, CurrencyId)`, and credit is only ever spent against an order of the same currency.** Balances are never summed across currencies and never converted.
14. **Loyalty points are earned and clawed back through the same `Currency.LoyaltyPointsDivisor`.** Both sites read it; no literal divisor exists.
15. **A cleaner's payout-invoice currency is derived from the `OrderEmployeePay` rows being invoiced, and generation fails if they disagree.** It is never derived from a person's registered country.
16. **`UserMembership.BillingCurrencyId` is set once at subscribe and never mutated.** A currency change is cancel-and-resubscribe.
17. **Every Stripe Checkout Session and Subscription sets `Currency` explicitly.** No Stripe-side localisation ever chooses a currency.
18. **Only active currencies are resolvable, quotable, chargeable or listable** — including on the anonymous currency overview endpoint.
19. **The default currency cannot be deactivated, and no currency holding orders, credit, promo, pay configs or invoices can be deactivated.**
20. **The market selector chooses a browse price list only.** It is never a settlement currency, and it is never defaulted from a card BIN, a card issuing country, or the customer's residence or nationality.

---

# PART 3 — THE STEP-BY-STEP PLAN

No sizing, no day counts. Dependencies are real. Steps in the same **Wave** can run in parallel; waves cannot.

**The two scarce resources this plan is shaped around:**
- **The `Initial` regeneration + DEV drop happens exactly once** (Step 6). Every schema change lands as entity + EF-config edits *before* it, and the integration suite runs *after* it.
- **The owner's Mac is needed exactly twice** (Steps 8 and 14), because all wire-contract changes are batched into one NSwag regeneration and one mobile-spec re-dump.

---

## Wave 0 — Owner actions, start now, block nothing but the launch

**Step 0.1 — OWNER: Stripe dashboard check.**
Open `dashboard.stripe.com/settings/adaptive-pricing` and report whether it is on. Open Dashboard → Balances and report whether EUR and PLN are available as settlement currencies for the Czech account.
*Why it matters:* if Adaptive Pricing is on, web Checkout may already be converting CZK with a 2–4% customer-paid fee while the order row says CZK, and the mobile path would not — the two channels disagree today.
*Proves it:* the dashboard reading itself.

**Step 0.2 — OWNER: engage the accountant** with the nine questions in §2.8, in that order. **Question 1 (gross vs commission) gates the registration date; question 4 (can Czechia issue the EX number?) gates whether Slovakia launches SME-exempt or straight into OSS.**
*Proves it:* written answers, filed alongside this document.

**Step 0.3 — AGENT: Stripe sandbox probes.** Three things the docs genuinely leave open, all cheap:
(a) does a `mode=subscription` Checkout Session accept an explicit `currency`? (every doc example uses `mode=payment`);
(b) does an `incomplete_expired` subscription still lock the customer's currency?;
(c) does `SubscriptionUpdateOptions` item-price swap silently keep the old currency when the new Price carries the target only in `currency_options`?
*Blocker:* no usable sandbox key is committed — every `Stripe:SecretKey` in the tree is a placeholder. **Owner supplies a sandbox key via user secrets, or runs these himself.**

---

## Wave 1 — VAT correctness. Independent of every currency decision. Ship first.

**Step 1 — Fix the VAT convention.**
Changes: `VatCalculator.cs:24`, `IssuePartialRefund.cs:234`, `RefundAllocator.cs:93` → `× rate / (1 + rate)`. `DefaultReceiptLayoutBuilder.cs:320` → percentage formatting of a fraction. `VatCalculator.cs:14-16` → throw on a null `countryConfig` instead of returning zero. Delete the `GetActiveCompanyInfoAsync()` fallback at `OrderFactory.cs:204-205`. Correct the Slovak seed to `0.23` (and drop the dead 10% reduced rate).
*Proves it:* **`Cleansia.Tests`** — a new `VatCalculatorTests` (the class has **zero** tests today; all nine references are `Mock<IVatCalculator>`), including 2,000 CZK @ 21% → 347.11; plus the existing `IssuePartialRefundHandlerTests` and `PartialRefundFeeRoundingTests`, whose own helpers use the `× 100m / (100m + rate)` convention and must be updated in the same commit.
*Runs in parallel with:* Steps 2, 3.
*No schema change, no wire change, no NSwag, no Mac.*

**Step 2 — Rate/precision conventions (code only, schema lands in Step 6).**
Rename `MembershipPlan.DiscountPercentage` → `DiscountRate` and divide the seed by 100; rebase `RefundStripeFeeRate` to a fraction and delete its `/100m`. Fix `CurrencyRepository.GetDefaultAsync`'s dead `??` guard.
*Proves it:* `Cleansia.Tests` (membership discount, refund fee rounding).

**Step 3 — Design review of the schema** (no code): the three price tables, the order line snapshots, `OrderExtras`, the credit key, the loyalty divisor location, `VatTreatment`, `MembershipPlanPrices`, precision normalisation.
*Proves it:* your sign-off on §2 of this document, specifically the **ruling-4 deviation** (divisor on `Currency`, not `LoyaltyTierConfig`).

---

## Wave 2 — Schema. Everything lands, then ONE regeneration.

Steps 4.1–4.9 are entity + EF-configuration + seed edits only. **Do not regenerate between them.** They can be written in parallel; they must all be complete before Step 6.

**Step 4.1 — Price tables.** `ServicePrices`, `PackagePrices`, `ExtraPrices`, each UQ`(ItemId, CurrencyId)` with **no `TenantId` term**. Delete the four price columns.
**Step 4.2 — Order line snapshots.** `OrderServices += UnitBasePrice, UnitPerRoomPrice, LineTotal` with UQ`(OrderId, ServiceId)`; `OrderPackages += LineTotal` with UQ`(OrderId, PackageId)`; new `OrderExtras(OrderId, ExtraId, Slug, UnitPrice)` UQ`(OrderId, ExtraId)`, replacing the JSON column.
**Step 4.3 — Credit.** UQ`(UserId, CurrencyId)`; `HasOne(a => a.User).WithOne()` → `.WithMany()`. **Name the inverse explicitly if EF invents a shadow FK** — that is a known trap in this codebase.
**Step 4.4 — Loyalty.** `Currency.LoyaltyPointsDivisor`; `LoyaltyTierConfig` UQ`(TenantId, Tier, CurrencyId)` keeping `.AreNullsDistinct(false)`.
**Step 4.5 — Payroll.** `OrderEmployeePay += CurrencyId`; `EmployeePayConfig` unique index gains `CurrencyId`, keeping `.AreNullsDistinct(false)`.
**Step 4.6 — Membership.** `MembershipPlanPrices(PlanId, CurrencyId, Amount)` UQ`(PlanId, CurrencyId)`; drop `MonthlyPriceCzk`.
**Step 4.7 — Currency.** Drop `ExchangeRate`; UQ`(Code)`; filtered UQ on `IsDefault`.
**Step 4.8 — Country.** `DefaultCurrencyCode` → `DefaultCurrencyId` FK; add `VatTreatment`.
**Step 4.9 — Precision.** Every money column to `numeric(18,2)`; every rate to `numeric(5,4)`; `TravelDistance` to `numeric(9,2)`.

**Step 5 — Seed rewrite.** 33 price rows per currency (10 services × 2 + 8 packages + 5 extras) × CZK and EUR; loyalty divisors and per-currency floors; per-currency pay configs; membership mirror prices; `VatTreatment` per country; **the ten unoperated currencies removed or deactivated**; SVK VAT at 0.23.

> **Step 6 — THE ONE REGENERATION. This is where DEV is dropped.**
> ```
> export PATH="$HOME/.dotnet/tools:$PATH"
> cd src
> dotnet ef migrations remove --force --project Cleansia.Infra.Database --startup-project Cleansia.Web.Partner
> dotnet ef migrations add   Initial --project Cleansia.Infra.Database --startup-project Cleansia.Web.Partner
> ```
> then drop the DEV database (the migration id changes; a DEV database recording the old one replays the create script against existing tables).
> **This is the agent's to run, not the owner's** (rulings 2026-08-15, 2026-08-25, 2026-09-07). **Report every step taken.**
> *Proves it:* **`Cleansia.IntegrationTests`** — Testcontainers builds a real Postgres from the migration. **This is the only thing that proves the model and the schema agree**, and it is the gate on the `.WithMany()` change in 4.3 and on every new unique index.

---

## Wave 3 — Backend behaviour. Depends on Step 6.

These can run in parallel with one another **except** 7.2, which depends on 7.1.

**Step 7.1 — Pricing calculator.** Join prices by currency; delete the exchange-rate scaling; **fail** on a missing price row instead of `?? 0m`; write the line snapshots into `OrderServices`/`OrderPackages`/`OrderExtras`.
*Proves it:* `Cleansia.Tests` (pricing), `Cleansia.IntegrationTests` (the joins against real Postgres).

**Step 7.2 — Currency and VAT resolution from the address.** `CreateOrder` derives currency from the resolved address; `QuoteOrder.Command` gains a country; both lose `CurrencyId`. `MaterializeRecurringBookingTemplate` resolves from the template's address (already loaded twelve lines above the hardcoded default).
*Proves it:* `Cleansia.Tests`, `Cleansia.HostTests` (endpoint contract), `Cleansia.IntegrationTests`.

**Step 7.3 — Credit per currency.** `EnsureForUserAsync` and `GetSpendableAsync` take a currency; the three spend-site guards are deleted; `IssueCustomerCredit.Command` carries a required `CurrencyId`; `ExpireCustomerCredit`'s idempotency key becomes per-account; `GdprDeletionService` sees all accounts.
*Proves it:* `Cleansia.Tests` + `Cleansia.IntegrationTests` (the 23505 case is only reachable against real Postgres).

**Step 7.4 — Loyalty per currency.** Both divisor sites (`:44` and `:163`) read `Currency.LoyaltyPointsDivisor`; the floor resolves per `(tier, currency)`; `GetLoyaltyTiers` takes a currency; a `> 0` validator and a test pinning the three launch divisors.
*Proves it:* `Cleansia.Tests`.

**Step 7.5 — Payroll currency.** `GenerateInvoice` derives the invoice currency from the `OrderEmployeePay` rows and fails on disagreement; the geography route leaves that path.
*Proves it:* `Cleansia.Tests`.

**Step 7.6 — Currency activation.** Deactivate command refusing the default; `IsActive` predicate in resolution; the anonymous `Partner/CurrencyController.GetOverview` filtered; `IsInUseAsync` extended to CreditAccounts and PromoCodes.
*Proves it:* `Cleansia.Tests` + `EveryRouteCarriesAnAuthorizationDecisionTests`.

**Step 7.7 — Extras admin CRUD.** Controller, create/update/activate commands, validators, per-currency price rows, admin permissions.
*Proves it:* `Cleansia.Tests` — **`FrozenPermissionMapTests`** (an additive permission row updates the snapshot in the same PR) and **`CatalogLifecycleEndpointPermissionTests`** (activation reuses `CanUpdateExtra`, as for every other catalogue entity). Six registration sites: `Policy.cs`, `PolicyBuilder.cs`, the two tests, and `policy.ts` twice.

**Step 7.8 — VAT posture.** `CountryConfiguration.VatTreatment` read by the VAT path; the CZ-domestic vs OSS reporting split.
*Proves it:* `Cleansia.Tests`.
*Depends on:* **Step 0.2's answers.** Build the mechanism; do not commit a posture value until the accountant rules.

**Step 7.9 — No-show apology per currency.** `BookingPolicy.NoShowCreditCzk` → per-currency lookup, **and `agents/tools/check-booking-policy-parity.mjs` updated in the same commit** or the repo-root parity workflow goes red.
*Proves it:* `node agents/tools/check-booking-policy-parity.mjs` and its self-test.

---

## Wave 4 — The wire. One regeneration, one Mac session.

**Step 8 — THE SINGLE WIRE-CONTRACT CHANGE.**
Batch every contract change discovered above into one pass: `QuoteOrder.Command` country in / `CurrencyId` out; `CreateOrder.Command` `CurrencyId` out; quote response carries `currencyCode`; membership DTOs carry `currencyCode` and drop `*Czk`; `TierInfo` carries a currency; `GetMyCredit.Response` becomes a list; extras admin DTOs.

Then: **NSwag regeneration for all three clients (agent's job, not the owner's — `npm run generate-*-client`, which per ADR-0031 D1 must end in a typecheck), plus a re-dump of `src/cleansia_android/openapi/customer-mobile-api.json`** (shared with iOS via `openapi-generator-config.customer.yaml`). Commit the regenerated clients in the same change as the DTOs.

> **Step 8b — OWNER: MAC SESSION #1.** iOS's generated `CleansiaCustomerApi` changes here, plus `QuoteClient.swift`, `BookingViewModel.swift`, `BookingOrderCommandFactory.swift` and the mock in `Tests/BookingSubmitTests.swift`. **This cannot be compiled on Windows. Compile and run the iOS test suite, and report.**

*Proves it:* `Cleansia.HostTests`; the Android `MembershipWireTest.kt`; the iOS build (owner); Jest for the regenerated Angular clients (**`NX_DAEMON=false`**).

---

## Wave 5 — Surfaces. Parallel after Step 8.

**Step 9 — Receipts and invoices.** Lines from snapshots; add the missing extras / surcharge / discount lines; percentage VAT rate display.
*Proves it:* `Cleansia.Tests` + a PDF golden.

**Step 10 — Admin per-currency price editors** for Services and Packages, and the Extras admin screen.
*Proves it:* Jest (admin lib), `NX_DAEMON=false`.

**Step 11 — Angular money formatter.** One shared currency-aware formatter; convert the ~26 hardcoded `'CZK'` sites across 11 files (the two `order-wizard.models.ts` module constants feed ~30 render sites). Country picker on the web address step. Market selector on the home calculator, catalogue and Plus page. Per-currency credit rows.
*Proves it:* Jest, and each app's `error-contract-parity.spec.ts` if any new error key lands (**keys go under `api.*`, never `errors.*`, in all five locales**).

**Step 12 — Admin reports grouped by currency.** `RevenueReportDto` gains a currency; `reports.facade.ts` stops stamping "Kč"; growth percentages computed within a currency.
*Proves it:* Jest + `Cleansia.Tests`.

**Step 13 — Android.** Per-currency fraction digits in `OrderFormatters.kt`; thread the resolved currency into the Google Pay sheet's `countryCode`/`currencyCode` (`SubscribePlusScreen.kt:247-248` — the Stripe SDK requires a currency there, so it cannot simply be deleted); i18n keys de-currencied. **Escape any apostrophe you add to `strings.xml`** or `mergeDebugResources` fails with an error naming no file.
*Proves it:* Gradle unit tests on Windows; the Google Pay sheet needs a device/emulator.

> **Step 14 — OWNER: MAC SESSION #2.** iOS per-currency fraction digits, catalogue strings off literal "Kč", `MembershipFormat.price`, and the `Localizable.xcstrings` loyalty perk strings. **Compile, run the suite, and eyeball the Plus and booking screens.** Note iOS configures **no Apple Pay at all** (`StripePaymentController.swift:21-42` never sets `applePay`), so there is no wallet currency to fix.

---

## Wave 6 — Plus.

**Step 15 — OWNER (or agent with a sandbox key): recreate the Stripe objects.**
One Product "Cleansia Plus"; two Prices (monthly, yearly) each with `currency_options` for `czk`, `eur`, `pln`; archive `price_1TSiJ83KjMqxM0RBVaiKAF6r` and `price_1TSiJ83KjMqxM0RBrfMWdjrF`; update the seed.
*Why fresh rather than edited:* the API says only `metadata`, `nickname` and `active` are updatable on a Price, while the Dashboard offers retroactive multi-currency. There is no production data, so don't resolve the tension — create new.

**Step 16 — Plus multicurrency code.** `UserMembership.BillingCurrencyId`; explicit `Currency` on all three Stripe calls; per-currency mirror rows; the savings-baseline scoped to a currency; swap constrained to the billing currency with a refusal path; the minimum-charge validator; management-screen copy ("Plus bills in CZK"); the cancel-and-resubscribe flow with its period-end dead window.
*Depends on:* Steps 6, 8, 15, and **Step 0.3's sandbox answers**.
*Proves it:* `Cleansia.Tests`, `Cleansia.HostTests`, Jest.

---

## Wave 7 — Market activation.

**Step 17 — Slovakia.**
Flip `Countries.IsServiced` for SVK; seed Slovak service cities; author the 33 EUR catalogue prices, EUR pay configs, the EUR loyalty divisor and floor, the EUR credit and no-show constants, the EUR membership mirror; set `VatTreatment` per **Step 0.2's answer**.
**Hard prerequisite: the web country picker (Step 11) must ship before this flip, or every inline-address web booking returns a 400** — `OrderAddressResolver` returns `CountryRequired` the moment more than one country is serviced.
*Proves it:* `Cleansia.IntegrationTests` end to end; one manual booking against DEV in each currency; every repo checker.

**Step 18 — Poland.** Same shape, plus 33 PLN prices and `currency_options[pln]`. **Blocked on `CountryConfiguration.PayoutScheme` for POL, which is seeded NULL — a business question about how Polish cleaners are paid, for the owner. And on the Polish PKWiU rate classification (§2.8 question 7).**
**Zero code.** That is the point of ruling 1, and it is the test of whether this design worked.

---

## What is still unknown

- **Whether Adaptive Pricing is on.** One dashboard read (Step 0.1). Until it is done, we do not know what currency the web checkout is actually charging.
- **Gross vs commission turnover.** Moves the Czech VAT date by 4–5×. The code says principal, unambiguously and in five places; a tax authority's view is a different question.
- **Whether Czechia issues the SME "EX" number.** Decides whether Slovakia launches exempt or straight into OSS. I verified Slovakia's and Poland's inbound implementations, not the Czech outbound half.
- **Fixed establishment in Slovakia.** Genuinely contested EU VAT law, and unusually live for a labour-on-the-ground business. It is the one thing that turns Slovakia from light to heavy, and it turns on a business decision (a coordinator, a depot) that would otherwise be made as an ops detail.
- **Whether Reg 2018/302 Art 4(1)(c) reaches a clean at the customer's own address.** No case law found; the Commission's own Q&A points away from it. The commercial case does not depend on this; the legal framing does.
- **Whether Wolt and Bolt truly have no currency picker.** Evidence of absence only.
- **Three Stripe behaviours** (Step 0.3), all cheap to settle in sandbox and all currently resting on documentation rather than execution.
- **Whether an `incomplete_expired` subscription still locks a Stripe customer's currency.** Read literally, "status other than canceled" includes it. That ambiguity is the one that most deserves the sandbox call.