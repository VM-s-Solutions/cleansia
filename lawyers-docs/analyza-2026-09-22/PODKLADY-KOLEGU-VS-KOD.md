# Obchodní podklady kolegů proti kódu

**Tři podklady k obchodnímu modelu proti zdrojovému kódu · commit `fdae9dfba72614e42db0faf1e7c267f79f269ae8` (`fix/audit-findings-2026-09-22`, 26. 9. 2026)**

Slovník: úklidník = „uklízečka" v podkladech = `Employee` (kód); zákazník = `User`; host = zákazník bez účtu (`Order.UserId = null`); provozní společnost = `Tenant`; administrátor = `Administrator / Manager / Support / Accountant`.

Odkazy „schéma s. 3“, „strom s. 4“ a „sešit, list Balíčky“ míří do `colleague-documents/`. Otevřená R22–R27 doplňují registr; rozhodnuta R2, rámec R11 a R16, zčásti R4 a R6.

## Shrnutí

Podklady navrhují **model A** (zprostředkování, úklidníci na IČO) a ceník v3.

**Cena.** Sešit počítá z m² a hodinové sazby; kód z katalogu: služba stojí `BasePrice + PerRoomPrice × (Rooms + Bathrooms)`, express +20 %. Velikostní balíčky ESSENTIAL/DEEP v katalogu nejsou.

**Odměna.** Podklady kotví 300 Kč/h. Kód platí sazbu za položku bez složky za vzdálenost, **každému přiřazenému úklidníkovi celou**: `Deep Clean Premium` (1 799 Kč, 315 minut, tedy 3 úklidníci) vyplatí 3 × 899,50 = 2 698,50 Kč.

**DPH.** Sešit předpokládá plátcovství a 21 %; seed vede společnost jako neplátce. DPH se z ceny vyjímá, registrace ceny nezvedne.

**Hotovost a karta.** Schéma navrhuje vypořádání (částka, potvrzení zákazníkem, týdenní saldo, limit 5 000 Kč) a kartu jako záruku. Kód nabízí hotovost jen přihlášenému zákazníkovi se zakázkou pro jednoho úklidníka; výběr (`MarkCashCollected`) to nečte a eviduje jen „vybráno, kým, kdy" bez částky. Z uložené karty strhává jen obnovu Plus.

**Storno a kredit.** 15 minut zdarma po objednání a pásma 25 % / 50 % odpovídají podkladům; 60 minut placeného člena Plus podklady neznají; poplatek u hotovosti se nevybírá. Kredit propadá po 12 měsících bez pohybu a dokončeným výmazem účtu.

## 1. Co jsou ty tři podklady

| podklad | datum | z čeho vychází | co určuje |
|---|---|---|---|
| `Cleansia_cenik_a_kalkulace_v3.xlsx` | v obsahu neuvedeno; metadata 18. 9. 2026 | odměna 300 Kč/h, cílový příspěvek 18 % | ceny balíčků, doplňků, příplatků a bod zvratu |
| `Cleansia_obchodni_model_A_strom.pdf` | 18. 9. 2026 | provozní popis ke commitu `9ad0b0ed`, obchodní mezery, ceník v3, porada 16. 9. | strukturu modelu A, strany, toky, právní hranice |
| `Cleansia_obchodni_model_schema.pdf` | 21. 9. 2026 | totéž plus ceník v3 | vypořádání hotovosti, nový ceník, měsíční ekonomiku |

PDF uvádějí referenční commit `9ad0b0ed`; následující srovnání ověřuje jejich tvrzení proti auditovanému stromu.

## 2. Premisy modelu A proti kódu

| premisa podkladu | co říká kód | stav | → |
|---|---|---|---|
| Zákazník má jen registrovaný účet (schéma s. 1) | `CreateOrder` nese `AllowsAnonymousActor = true`, host objedná bez účtu — [CreateOrder.cs:27](../../src/Cleansia.Core.AppServices/Features/Orders/CreateOrder.cs#L27) | neplatí | → R5 |
| Uložená karta je podmínka objednávky a záruka (schéma s. 2) | `SetupFutureUsage = "off_session"` jen na mobilní platbě; webová pokladna ji nenastavuje — [StripeClient.cs:189](../../src/Cleansia.Infra.Clients/Stripe/StripeClient.cs#L189) | zčásti | → R27 |
| Platforma si z uložené karty dorovná storno a neotevření (schéma s. 4) | z uložené karty strhává jen `CreateSubscriptionAsync` pro členství; pro zakázku žádná metoda — [IStripeClient.cs:119](../../src/Cleansia.Core.Clients.Abstractions/Stripe/IStripeClient.cs#L119) | návrh | → R27 |
| Úklidník je vždy neplátce DPH (strom s. 2) | `cleanersAreVatPayers` je konstanta `false` — [FileExtensions.cs:119](../../src/Cleansia.Core.AppServices/Extensions/FileExtensions.cs#L119) | platí | → R16 |
| Holding drží IP, provozní společnost je smluvní strana (schéma s. 1) | registr `Tenants` má jeden řádek `cleansia-cz`; holdingová entita v kódu není — [insert_seed_data.sql:63-65](../../sql-scripts/insert_seed_data.sql#L63-L65) | návrh | → R2 |
| Provozní společnost vystavuje doklad zákazníkovi i samofakturu (schéma s. 1) | doklad nese `CompanyInfo`; `InvoiceSupplierData` označuje úklidníka — [ReceiptService.cs:556-563](../../src/Cleansia.Core.AppServices/Services/ReceiptService.cs#L556-L563), [FileExtensions.cs:122-134](../../src/Cleansia.Core.AppServices/Extensions/FileExtensions.cs#L122-L134) | platí | → R1 |
| Platforma neposkytuje úklid vlastním jménem (strom s. 2) | seedované podmínky říkají „Cleansia poskytuje profesionální úklidové služby" — [cs.md:15](../../src/Cleansia.Infra.Database/Seed/Legal/customer/terms-of-service/any/2026-09-14/cs.md#L15) | neplatí v textu | → R1 |
| Jedna uklízečka na 120 minut práce (schéma s. 2) | `RequiredEmployees = ceil(EstimatedTime / 120)`, `MinutesPerEmployee` — [OrderDuration.cs:27](../../src/Cleansia.Core.Domain/Orders/OrderDuration.cs#L27), [OrderDuration.cs:37-38](../../src/Cleansia.Core.Domain/Orders/OrderDuration.cs#L37-L38) | platí | — |
| Preferovaná uklízečka drží zakázku 10 % předstihu, max. 12 h (strom s. 3) | `PreferredHoldFraction`, `PreferredHoldCeilingHours` — [BookingPolicy.cs:229-230](../../src/Cleansia.Core.AppServices/Features/Orders/BookingPolicy.cs#L229-L230) | platí | — |
| Vícečlenná posádka = samostatné účty a odměny (strom s. 3) | každý přiřazený úklidník dostane vlastní úplný výpočet — [CompleteOrder.cs:346-354](../../src/Cleansia.Core.AppServices/Features/Orders/CompleteOrder.cs#L346-L354) | platí, s důsledkem v §5 | → R13 |
| Pojištění odpovědnosti má mít úklidník (strom s. 2) | seed `EmployeeDocumentRequirements` zná `IdentityCard` a `WorkPermit`; pojistku nevyžaduje nikdo (rámcová smlouva žádá 3 000 000 Kč) — [insert_seed_data.sql:1134-1138](../../sql-scripts/insert_seed_data.sql#L1134-L1138) | návrh | → R7 |
| Zákazníkovi se ukazuje pojištění 1 000 000 Kč (strom s. 2) | `InsuranceCoverageAmount` je obsah trhu, seed CZE 1 000 000 — [insert_seed_data.sql:1014](../../sql-scripts/insert_seed_data.sql#L1014) | platí | → R7 |
| Smlouva o dílo vzniká při převzetí, podpis přejetím prstu, např. Signi (strom s. 5) | `StageAsync` uloží přijetí textu při převzetí; poskytovatel podpisu v kódu není — [TakeOrder.cs:357](../../src/Cleansia.Core.AppServices/Features/Orders/TakeOrder.cs#L357) | postaveno bez Signi | → R11 |

## 3. Ceník: struktura

| dimenze nového ceníku | co má kód | → |
|---|---|---|
| Plocha v m² (25–180) jako vstup | zakázka má `Rooms` a `Bathrooms`, nejvýše 8/4; hledání `SquareMeters`/`AreaM2` v produkčním zdroji: žádný vstup — [BookingPolicy.cs:37-38](../../src/Cleansia.Core.AppServices/Features/Orders/BookingPolicy.cs#L37-L38) | → R25 |
| Pracnost 3 / 5 / 7 min na m² | doba je součet `EstimatedTime` samostatných služeb i služeb uvnitř balíčků — [OrderDuration.cs:29-31](../../src/Cleansia.Core.Domain/Orders/OrderDuration.cs#L29-L31) | → R25 |
| Balíček nese velikost, dispozici a hodiny | `Package` nese `Name`, `Description`, `Tagline`, `IsPopular`, `Translations` — [Package.cs:8-37](../../src/Cleansia.Core.Domain/Packages/Package.cs#L8-L37) | → R25 |
| Dispozice 1+kk … dům 5+ | `PropertySizePreset` je štítek trhu mapovaný na `Rooms` a `Bathrooms`; zakázka jeho id neukládá — [PropertySizePreset.cs:49-53](../../src/Cleansia.Core.Domain/Configuration/PropertySizePreset.cs#L49-L53) | → R25 |
| Deset balíčků (5 velikostí × ESSENTIAL/DEEP) | osm balíčků s jednou cenou: `Essential Clean` 799 Kč … `Luxury Full Service` 3 499 Kč — [insert_seed_data.sql:729-736](../../sql-scripts/insert_seed_data.sql#L729-L736) | → R25 |
| Cena se zaokrouhluje nahoru na 10 Kč (sešit, list Vstupy) | cena se nezaokrouhluje; zaokrouhlují se jen slevy a kredit — [OrderPricingCalculator.cs:96](../../src/Cleansia.Core.AppServices/Services/OrderPricingCalculator.cs#L96) | → R25 |
| Minimální cena zakázky 690 Kč (sešit, list Vstupy) | `baseSubtotal` žádnou dolní mez nezná; `MinimumOrder` je práh promo a věrnosti — [OrderPricingCalculator.cs:96](../../src/Cleansia.Core.AppServices/Services/OrderPricingCalculator.cs#L96) | → R25 |

## 4. Příplatky a doplňky

| položka ceníku | sazba v ceníku | co má kód | → |
|---|---|---|---|
| Expres 2–4 h předem | +20 % | `ExpressSurchargeRate` = 0,20, jediný příplatek v ceně — [BookingPolicy.cs:32](../../src/Cleansia.Core.AppServices/Features/Orders/BookingPolicy.cs#L32) | — |
| Premium tier | +25 %, dělit s úklidníkem | `grep -rnE "PremiumTier|PremiumSurcharge" src/` → 0; `baseSubtotal` další člen nemá — [OrderPricingCalculator.cs:96](../../src/Cleansia.Core.AppServices/Services/OrderPricingCalculator.cs#L96) | → R25 |
| Silné znečištění | +30 % | `grep -rnE "HeavySoil|Soiling|DirtLevel" src/Cleansia.Core.AppServices/` → 0; `baseSubtotal` je jen součet tří položek — [OrderPricingCalculator.cs:96](../../src/Cleansia.Core.AppServices/Services/OrderPricingCalculator.cs#L96) | → R12 |
| Noční a víkendový úklid | +25 % | hledání `NightSurcharge`/`WeekendSurcharge` v AppServices: žádná sazba; server kontroluje `IsBelowMinimumLeadTime`, nikoli denní okno — [CreateOrder.cs:162-167](../../src/Cleansia.Core.AppServices/Features/Orders/CreateOrder.cs#L162-L167) | → R25 |
| Mazlíček | +10 % | sazba není; existuje položka `pet-hair-supplement` 150 Kč — [insert_seed_data.sql:753](../../sql-scripts/insert_seed_data.sql#L753) | → R25 |
| Pokoj navíc | +330 / +570 Kč | samostatná položka není; cena roste členem `PerRoomPrice × (Rooms + Bathrooms)` — [OrderPricingCalculator.cs:59](../../src/Cleansia.Core.AppServices/Services/OrderPricingCalculator.cs#L59) | → R25 |
| Koupelna navíc | +430 / +770 Kč | koupelna se násobí toutéž sazbou jako pokoj — [OrderPricingCalculator.cs:59](../../src/Cleansia.Core.AppServices/Services/OrderPricingCalculator.cs#L59) | → R25 |
| 5. patro bez výtahu, balkon, samostatné WC | +190 / +240 / +200 Kč | `grep -rnE "Elevator|Balcony|SeparateWc" src/` → 0 věcných; `CustomerFloor` je text, cena ho nečte — [Order.cs:178](../../src/Cleansia.Core.Domain/Orders/Order.cs#L178) | → R25 |
| Devět doplňků (lednice, trouba, žehlení, ozón…) | 330–1 690 Kč | pět položek `Extras`: 100–250 Kč, v seedu označené jako zástupné — [insert_seed_data.sql:749-753](../../sql-scripts/insert_seed_data.sql#L749-L753) | → R25 |

## 5. Odměna úklidníka

| tvrzení podkladu | co říká kód | → |
|---|---|---|
| Úklidník dostane 300 Kč za odpracovanou hodinu (schéma s. 1) | konfigurace nese `BasePay`, `ExtraPerRoom`, `ExtraPerBathroom`, `MinimumPay`, `MaximumPay`; hodinová sazba nikde — [EmployeePayConfig.cs:22-40](../../src/Cleansia.Core.Domain/EmployeePayroll/EmployeePayConfig.cs#L22-L40) | → R23 |
| Odměna = hodiny × sazba (sešit, list Metodika) | součet přes vybrané služby a balíčky: `BasePay + max(0, Rooms − 1) × ExtraPerRoom + Bathrooms × ExtraPerBathroom` — [PayCalculatorExtensions.cs:39-48](../../src/Cleansia.Core.Domain/Extensions/PayCalculatorExtensions.cs#L39-L48) | → R23 |
| — | součet zvedne nejvyšší kladné `MinimumPay` a ořízne nejnižší kladné `MaximumPay` (`ApplyMinMaxClamp`); seedované meze jsou 0, tedy žádné — [PayCalculatorExtensions.cs:50-52](../../src/Cleansia.Core.Domain/Extensions/PayCalculatorExtensions.cs#L50-L52), [insert_seed_data.sql:777-784](../../sql-scripts/insert_seed_data.sql#L777-L784) | → R23 |
| Podíl úklidníka z ceny 42–53 % (sešit, list Balíčky) | seed počítá `ROUND(BasePrice * 0.5, 2)`, šablona stupně „junior" — [insert_seed_data.sql:781](../../sql-scripts/insert_seed_data.sql#L781) | → R23 |
| Část příplatku za znečištění a noc patří úklidníkovi (sešit, list Příplatky) | odměna nečte cenu, expresní příplatek ani doplňky — [PayCalculatorExtensions.cs:39-48](../../src/Cleansia.Core.Domain/Extensions/PayCalculatorExtensions.cs#L39-L48) | → R23 |
| Provize platformy = 1 − podíl úklidníka (sešit, list Balíčky) | `totalPay` procentní provizi nezná — `grep -rnE "Commission|Provize" src/` → 0 — [PayCalculatorExtensions.cs:48](../../src/Cleansia.Core.Domain/Extensions/PayCalculatorExtensions.cs#L48) | → R1 |
| Cena i odměna jsou na jednu zakázku (sešit, list Ekonomika) | každý přiřazený dostane úplný výpočet včetně balíčků; zákaznická cena se posádkou nenásobí — [CompleteOrder.cs:346-354](../../src/Cleansia.Core.AppServices/Features/Orders/CompleteOrder.cs#L346-L354), [CalculateOrderPay.cs:118-157](../../src/Cleansia.Core.AppServices/Features/EmployeePayroll/CalculateOrderPay.cs#L118-L157) | → R26 |
| Příklad DEEP L: cena 3 540 Kč, odměna 1 813 Kč (schéma s. 5) | sazba balíčku je `ROUND(Price * 0.5, 2)`, tedy 899,50 Kč, a platí se třikrát — [insert_seed_data.sql:797](../../sql-scripts/insert_seed_data.sql#L797) | → R26 |
| Doprava a chemie jsou vlastní náklad v sazbě úklidníka (sešit, list Vstupy; schéma s. 8) | platí: `CalculateAggregatedPay` vrací `expensesPay` vždy 0; `grep -rnE "DistanceRatePerKm|TravelDistance" src/` mimo testy a migrace → 2 deklarace a 2 mapování EF, 0 čtenářů — [PayCalculatorExtensions.cs:29-56](../../src/Cleansia.Core.Domain/Extensions/PayCalculatorExtensions.cs#L29-L56), [EmployeePayConfig.cs:28-29](../../src/Cleansia.Core.Domain/EmployeePayroll/EmployeePayConfig.cs#L28-L29) | → R6 |
| Splatnost samofaktury 14 dní (strom s. 4) | platí: `PaymentTermsDays` = 14 — [Constants.cs:78](../../src/Cleansia.Core.AppServices/Common/Constants.cs#L78) | — |

## 6. Hotovost: návrh vypořádání proti evidenci faktu

| krok návrhu (schéma s. 4) | co má kód | → |
|---|---|---|
| Platbu volí zákazník: karta, nebo hotovost | `CreateOrder` odmítne hotovost hostovi a zakázce s `RequiredEmployees` ≥ 2 (`order.cash_not_available`); továrna totéž hlídá znovu — [CreateOrder.cs:663-667](../../src/Cleansia.Core.AppServices/Features/Orders/CreateOrder.cs#L663-L667), [OrderFactory.cs:290-295](../../src/Cleansia.Core.AppServices/Features/Orders/OrderFactory.cs#L290-L295) | → R6 |
| — | hotovostní šablona opakovaného úklidu pro posádku ≥ 2, vzniklá před pravidlem, výskyt nevytvoří; potvrzení staršího hotovostního výskytu pro posádku `ConfirmRecurringOrder` odmítne (`AllowsCash`) — [MaterializeRecurringBookingTemplate.cs:257-269](../../src/Cleansia.Core.AppServices/Features/Bookings/MaterializeRecurringBookingTemplate.cs#L257-L269), [ConfirmRecurringOrder.cs:112-119](../../src/Cleansia.Core.AppServices/Features/Orders/ConfirmRecurringOrder.cs#L112-L119) | → R6 |
| ① uložená karta jako záruka, přesná částka v aplikaci | `SetupFutureUsage` je jen na mobilní platbě; `Usage = "off_session"` slouží členství — [StripeClient.cs:189](../../src/Cleansia.Infra.Clients/Stripe/StripeClient.cs#L189), [StripeClient.cs:297](../../src/Cleansia.Infra.Clients/Stripe/StripeClient.cs#L297) | → R27 |
| ① kredit sníží částku k úhradě | kredit se čerpá jen u `PaymentType.Card`; výběr hotovosti na zakázce s `CreditAppliedAmount` > 0 `MarkCashCollected` odmítne — [CreateOrder.cs:1145](../../src/Cleansia.Core.AppServices/Features/Orders/CreateOrder.cs#L1145), [MarkCashCollected.cs:147-155](../../src/Cleansia.Core.AppServices/Features/Orders/MarkCashCollected.cs#L147-L155) | → R6 |
| ① zakázku vidí jen úklidník pod limitem hotovostního dluhu | nabídka filtruje platební typ objednávky, nikdy dluh úklidníka — [OrderAvailability.cs:61-67](../../src/Cleansia.Core.Domain/Orders/OrderAvailability.cs#L61-L67) | → R6 |
| ② úklidník přijme hotovost jménem Cleansia | `MarkCashCollected` zapíše výběr přiřazenému schválenému úklidníkovi při `InProgress` na nezaplacené zakázce; `AllowsCash` nečte (`grep -c "AllowsCash" MarkCashCollected.cs` → 0), kartovou zakázku přijme, když ji Stripe nevede jako provedenou ani zpracovávanou — [MarkCashCollected.cs:57-72](../../src/Cleansia.Core.AppServices/Features/Orders/MarkCashCollected.cs#L57-L72), [MarkCashCollected.cs:211-230](../../src/Cleansia.Core.AppServices/Features/Orders/MarkCashCollected.cs#L211-L230) | → R6 |
| — | kartovou zakázku nabídka pustí jen s `PaymentStatus.Paid`; `grep -rn "AddAssignedEmployee(" src/` mimo testy → `TakeOrder` a `AdminReassignOrder`, jehož `Validator` platbu nečte; tak i host či posádka ≥ 2 zaplatí hotově — [OrderAvailability.cs:66-67](../../src/Cleansia.Core.Domain/Orders/OrderAvailability.cs#L66-L67), [AdminReassignOrder.cs:31-45](../../src/Cleansia.Core.AppServices/Features/Orders/AdminReassignOrder.cs#L31-L45) | → R6 |
| ② úklidník zadá přijatou částku | `MarkCashCollected` zapisuje `CashCollectedAt` a `CollectedByEmployeeId`, částku ne — [Order.cs:50-52](../../src/Cleansia.Core.Domain/Orders/Order.cs#L50-L52) | → R6 |
| ② zákazník platbu potvrdí, teprve pak doklad | `GenerateReceipt` vzniká při objednání; `MarkCashCollected` jej přegeneruje jako zaplacený, bez potvrzení zákazníkem — [OrderPaymentDispatcher.cs:82-91](../../src/Cleansia.Core.AppServices/Features/Orders/OrderPaymentDispatcher.cs#L82-L91), [MarkCashCollected.cs:168-181](../../src/Cleansia.Core.AppServices/Features/Orders/MarkCashCollected.cs#L168-L181) | → R6 |
| — | opakovaná hotovost dostane `PaymentStatus.Paid` a doklad už potvrzením výskytu zákazníkem, před výběrem; `MarkCashCollected` pak zakázku odmítne jako zaplacenou — [ConfirmRecurringOrder.cs:169-177](../../src/Cleansia.Core.AppServices/Features/Orders/ConfirmRecurringOrder.cs#L169-L177), [MarkCashCollected.cs:61-62](../../src/Cleansia.Core.AppServices/Features/Orders/MarkCashCollected.cs#L61-L62) | → R6 |
| ③ týdenní saldo = odměny − vybraná hotovost | `grep -rnE "Saldo|CashBalance|CashOwed" src/` → 0; `PayPeriod` nese jen datum, stav a vazby — [PayPeriod.cs:9-31](../../src/Cleansia.Core.Domain/EmployeePayroll/PayPeriod.cs#L9-L31) | → R6 |
| ③ řádek hotovosti v samofaktuře | řádek odměny nese `BasePay`, `ExtrasPay`, `ExpensesPay`, `BonusPay`; hotovost žádnou — [OrderEmployeePay.cs:38-58](../../src/Cleansia.Core.Domain/EmployeePayroll/OrderEmployeePay.cs#L38-L58) | → R6 |
| ③ úklidník odvede do 7 dnů pod variabilním symbolem | `VariableSymbol` patří výplatní faktuře, tedy směru firma → úklidník — [EmployeeInvoice.cs:74](../../src/Cleansia.Core.Domain/EmployeePayroll/EmployeeInvoice.cs#L74) | → R6 |
| ④ dluh nad 5 000 Kč blokuje hotovostní zakázky | `grep -rnE "CashLimit|DebtCeiling|CashDebt" src/` → 0; jediný strop je `WeeklyOrderLimit` — [Employee.cs:148](../../src/Cleansia.Core.Domain/Users/Employee.cs#L148) | → R6 |
| ④ nezaplacení se dorovná z uložené karty | nezaplacenou hotovost kód z karty nedorovná: mimo relaci zákazníka strhává jen `CreateSubscriptionAsync` pro členství (→ §2) — [IStripeClient.cs:119](../../src/Cleansia.Core.Clients.Abstractions/Stripe/IStripeClient.cs#L119) | → R27 |
| Inkasní zmocnění úklidníka v rámcové smlouvě | hledání `DirectDebit`/`Mandate` v produkčním C#: žádné inkasní zmocnění; karta používá `CreateCheckoutSessionAsync` nebo `CreatePaymentIntentAsync` — [IStripeClient.cs:16](../../src/Cleansia.Core.Clients.Abstractions/Stripe/IStripeClient.cs#L16), [CreatePaymentIntent.cs:133-141](../../src/Cleansia.Core.AppServices/Features/Orders/CreatePaymentIntent.cs#L133-L141) | → R6 |

## 7. DPH, doklady a daňová plocha

| tvrzení podkladu | co říká kód | → |
|---|---|---|
| Cleansia je plátce DPH, sazba 21 % (sešit, list Vstupy) | seedovaná společnost má `IsVatPayer = false` — [insert_seed_data.sql:1163](../../sql-scripts/insert_seed_data.sql#L1163) | → R24 |
| Odvod DPH 17,4 % z ceny (sešit, list Ekonomika) | DPH se z hrubé ceny vyjímá: `gross × rate / (1 + rate)` — [VatCalculator.cs:45-49](../../src/Cleansia.Core.AppServices/Services/VatCalculator.cs#L45-L49) | → R24 |
| Základ DPH při zprostředkování (strom s. 6) | `Calculate` dostává celou `TotalPrice` zákazníka, ne provizi — [OrderFactory.cs:329](../../src/Cleansia.Core.AppServices/Features/Orders/OrderFactory.cs#L329) | → R1 |
| Plátcovství zdražuje DEEP L o 850 Kč (sešit, list Citlivost) | registrace katalogovou cenu nemění; `baseSubtotal` člen DPH nemá — [OrderPricingCalculator.cs:96](../../src/Cleansia.Core.AppServices/Services/OrderPricingCalculator.cs#L96) | → R24 |
| Doklad zákazníkovi RCP-RRRR-NNNN od provozní společnosti (strom s. 4) | řada odpovídá; neplátce má lokalizované oznámení a netiskne DIČ — [Constants.cs:83-85](../../src/Cleansia.Core.AppServices/Common/Constants.cs#L83-L85), [DefaultReceiptLayoutBuilder.cs:155-173](../../src/Cleansia.Infra.Services/Pdf/Layouts/DefaultReceiptLayoutBuilder.cs#L155-L173) | → R24 |
| Faktura postrádá prohlášení „vystaveno odběratelem" (strom s. 4) | popisky nesou jen `Supplier` a `Customer`; `grep -rn "self-billing" src/Cleansia.Infra.Services` → 0 — [InvoiceLabels.cs:77-78](../../src/Cleansia.Infra.Services/Pdf/Models/InvoiceLabels.cs#L77-L78) | → R1 |
| Řádky dokladu se nesčítají na uvedený součet (strom s. 4) | Pro PDF neplatí: `InCents` vyrovná uložené slevy; `ItemLines` tiskne položky, express a záporné slevy — [OrderFactory.cs:436-461](../../src/Cleansia.Core.AppServices/Features/Orders/OrderFactory.cs#L436-L461), [DefaultReceiptLayoutBuilder.cs:300-340](../../src/Cleansia.Infra.Services/Pdf/Layouts/DefaultReceiptLayoutBuilder.cs#L300-L340) | — |
| Fiskální podklady | `BuildFiscalLineItems` obsahuje služby a balíčky, nikoli doplňky, slevy a express — [ReceiptService.cs:368-393](../../src/Cleansia.Core.AppServices/Services/ReceiptService.cs#L368-L393) | → R21 |
| DAC7 a evidence tržeb nese provozní společnost (schéma s. 8) | `grep -rn "DAC7" src/` → 0; fiskální poskytovatel vrací `NOT_IMPLEMENTED` — [CzechEet2FiscalService.cs:53-55](../../src/Cleansia.Infra.Fiscal/Countries/Czechia/CzechEet2FiscalService.cs#L53-L55) | → R21 |
| IČO a DIČ provozní společnosti | seed je `REPLACE WITH ACTUAL`; plátce musí dodat `VatNumber` — [insert_seed_data.sql:1161-1162](../../sql-scripts/insert_seed_data.sql#L1161-L1162), [UpdateCompanyInfo.cs:74-79](../../src/Cleansia.Core.AppServices/Features/Company/UpdateCompanyInfo.cs#L74-L79) | → R2 |

## 8. Tvrzení a značky podkladů — stav na auditovaném commitu

| tvrzení podkladu | stav | doklad |
|---|---|---|
| Registr zná „Cleansia CZ s.r.o.", doklady tisknou „Cleansia s.r.o." (strom s. 1) | platí | [insert_seed_data.sql:1158](../../sql-scripts/insert_seed_data.sql#L1158) |
| Smlouvu o dílo teprve generovat při přijetí zakázky (strom s. 5) | postaveno, bez PDF a bez kvalifikovaného podpisu | [TakeOrder.cs:357](../../src/Cleansia.Core.AppServices/Features/Orders/TakeOrder.cs#L357) |
| Rozhodnout „jedna s.r.o. pro všechny" (strom s. 1) | rozhodnuto ADR-0061: holding a společnost na region | [adr-0061.md:39-40](../../docs/decisions/adr-0061.md#L39-L40) |
| Rozhodnout: ponechat hotovost vůbec (strom s. 3) | rozhodnuto: hotovost jen přihlášenému zákazníkovi, jehož zakázka potřebuje jednoho úklidníka (`AllowsCash`) → R6 | [BookingPolicy.cs:143-148](../../src/Cleansia.Core.AppServices/Features/Orders/BookingPolicy.cs#L143-L148) |
| K dostavbě: zrušení hostovských objednávek (schéma s. 2) | host ruší platným `AccessToken`; `RevokeAsync` při stornu tokeny odvolá | [CancelGuestOrder.cs:18](../../src/Cleansia.Core.AppServices/Features/Orders/CancelGuestOrder.cs#L18), [CancelGuestOrder.cs:61](../../src/Cleansia.Core.AppServices/Features/Orders/CancelGuestOrder.cs#L61) |
| K dostavbě: tlačítka Odstoupit a Zástup (schéma s. 2) | platí — server nese `RequestCover` i `DropOrder`; `grep -rniE "RequestCover|DropOrder" src/Cleansia.App src/cleansia_android src/cleansia_ios` → jen generovaný klient a OpenAPI, 0 volání | [OrderController.cs:240](../../src/Cleansia.Web.Partner/Controllers/OrderController.cs#L240), [OrderController.cs:260](../../src/Cleansia.Web.Partner/Controllers/OrderController.cs#L260) |
| K dostavbě: detekce nedostavení úklidníka (schéma s. 2) | platí — sweep bere jen zakázky bez posádky | [CancelUnfilledOrders.cs:112-118](../../src/Cleansia.Core.AppServices/Features/Orders/CancelUnfilledOrders.cs#L112-L118) |
| K dostavbě: stav „zákazník neotevřel" (schéma s. 2) | platí — takový stav ani poplatek v kódu není | [OrderStatus.cs:8](../../src/Cleansia.Core.Domain/Enums/OrderStatus.cs#L8) |
| Členství není ve VOP; benefity končí první neúspěšnou platbou (strom s. 5) | platí; `InvoicePaymentFailed` → `past_due`, nárok čte jen `MembershipStatus.Active` s běžícím obdobím → R18 | [StripeSubscriptionWebhookHandler.cs:77-85](../../src/Cleansia.Core.AppServices/Services/StripeSubscriptionWebhookHandler.cs#L77-L85), [UserMembershipRepository.cs:56-58](../../src/Cleansia.Infra.Database/Repositories/UserMembershipRepository.cs#L56-L58) |
| Sleva Plus a věrnostní stupeň se sčítají se stropem 12 % (strom s. 5) | platí | [OrderFactory.cs:361-371](../../src/Cleansia.Core.AppServices/Features/Orders/OrderFactory.cs#L361-L371) |
| Storno zdarma, dokud nikdo nepřijal, a do 15 min od objednání (strom s. 3; schéma s. 2) | platí pro 15 min (host i nový zákazník, `OopsWindowMinutesStandard`); člen Plus s placeným nárokem má `OopsWindowMinutesPlus` = 60 min, podklady to neznají → R4 | [BookingPolicy.cs:81-87](../../src/Cleansia.Core.AppServices/Features/Orders/BookingPolicy.cs#L81-L87), [CancellationPolicyResolver.cs:31-43](../../src/Cleansia.Core.AppServices/Services/CancellationPolicyResolver.cs#L31-L43) |
| Storno zdarma ≥ 24 h předem (Plus ≥ 4 h), 4–24 h 25 %, pod 4 h 50 % (strom s. 3) | platí; oba plány Plus mají v seedu `FreeCancellationWindowHours` = 4 | [BookingPolicy.cs:65-74](../../src/Cleansia.Core.AppServices/Features/Orders/BookingPolicy.cs#L65-L74), [insert_seed_data.sql:1804-1822](../../sql-scripts/insert_seed_data.sql#L1804-L1822) |
| Storno 25 % a 50 %, u hotovosti nevymahatelné (strom s. 5) | platí — sazba se zapíše jako `FeeRate`; uplatní se jen krácením refundace zaplacené `PaymentType.Card` | [CustomerOrderCancellation.cs:50](../../src/Cleansia.Core.AppServices/Features/Orders/CustomerOrderCancellation.cs#L50), [CustomerOrderCancellation.cs:102-104](../../src/Cleansia.Core.AppServices/Features/Orders/CustomerOrderCancellation.cs#L102-L104) |
| Nikdo nevzal zakázku: 30 min, plná refundace, kredit 250 Kč (strom s. 3) | platí — `TryIssueApologyCreditAsync` píše `CleanerNoShow` jen registrovanému | [CancelUnfilledOrders.cs:274-301](../../src/Cleansia.Core.AppServices/Features/Orders/CancelUnfilledOrders.cs#L274-L301) |
| — (týž běh, zaplacená karta s kreditem) | mezera: refundace vrátí kreditní podíl (`ReturnCreditShareAsync`) a `ReturnUnpaidOrderCreditAsync` bez podmínky `Paid` vrátí celý kredit podruhé | [RefundService.cs:172-173](../../src/Cleansia.Core.AppServices/Services/RefundService.cs#L172-L173), [CancelUnfilledOrders.cs:195-196](../../src/Cleansia.Core.AppServices/Features/Orders/CancelUnfilledOrders.cs#L195-L196) |
| Kredit se nevyplácí v penězích a po 12 měsících bez pohybu propadá (strom s. 5) | platí: `ExpiryMonths` = 12 od posledního pohybu; dokončený výmaz účtu odepíše zůstatek ve všech měnách (`RecordExpiry`, klíč `account-deletion:`) → R4 | [CreditAccount.cs:69](../../src/Cleansia.Core.Domain/Credit/CreditAccount.cs#L69), [GdprDeletionService.cs:253-258](../../src/Cleansia.Core.AppServices/Services/GdprDeletionService.cs#L253-L258) |
| Týdenní limity zakázek nastavené platformou (schéma s. 9) | platí — strop nastavuje administrátor jednotlivci | [Employee.cs:148](../../src/Cleansia.Core.Domain/Users/Employee.cs#L148) |
| Vynucené tričko, jednotná chemie a vůně (schéma s. 9) | v kódu nic takového není; `Employee` nese jen `WeeklyOrderLimit` a hodnocení | [Employee.cs:148](../../src/Cleansia.Core.Domain/Users/Employee.cs#L148) |

## 9. Nová rozhodnutí

| id | otázka | varianty |
|---|---|---|
| R22 | Kdo určuje cenu zakázky a sazbu úklidníka | platforma určuje obojí (návrh podkladů) · úklidník si sazbu nastaví a zákazník si ho vybere (návrh ze schématu s. 9) · platforma určuje cenu, sazba je pásmo |
| R23 | Model odměny — rozšiřuje R6; rozhodnuto 2026-09-24 (commit fdc41c97a): odměna bez kilometrové složky | hodinová sazba 300 Kč/h z odhadované doby · dnešní sazby za položku se stropem a minimem · sazba za hodinu s dopočtem z `EstimatedTime` |
| R24 | Plátcovství DPH provozní společnosti | ponechat neplátcovství po právním ověření podmínek · registrovat bez zdražení (DPH z hrubé ceny `gross × rate / (1 + rate)`) · registrovat a ceny upravit podle sešitu |
| R25 | Struktura ceníku | balíčky podle velikosti a m² s dopočtem z pracnosti (podklady) · dnešní katalog služeb a balíčků s `Rooms` a `Bathrooms` · hybrid: velikostní balíčky jako `Packages`, m² jen jako vstup odhadu |
| R26 | Cena a posádka | cena zůstává za zakázku a odměna se násobí posádkou (dnešní stav) · cena roste s počtem úklidníků · odměna se mezi posádku dělí |
| R27 | Uložená karta jako záruka | karta bez záruky (dnešní stav) · karta uložená při objednání a strhávaná za storno, neotevření a nezaplacenou hotovost · záruka jen u hotovostních objednávek |

## 10. Čísla podkladů proti kódu

| veličina | podklad | kód |
|---|---|---|
| Balíček pro byt 3+kk, DEEP | 3 540 Kč (sešit, list Balíčky) | `Deep Clean Premium` 1 799 Kč — [insert_seed_data.sql:731](../../sql-scripts/insert_seed_data.sql#L731) |
| Čas balíčku a posádka | DEEP L: 6,04 h (sešit, Balíčky) | `Deep Clean Premium`: 180 + 90 + 45 = 315 min, tedy 3 osoby — [insert_seed_data.sql:537-550](../../sql-scripts/insert_seed_data.sql#L537-L550), [insert_seed_data.sql:829-838](../../sql-scripts/insert_seed_data.sql#L829-L838) |
| Odměna za tutéž zakázku | 1 813 Kč | `ROUND(Price * 0.5, 2)` = 899,50 Kč na úklidníka, třikrát — [insert_seed_data.sql:797](../../sql-scripts/insert_seed_data.sql#L797) |
| Doplněk „mytí trouby" | 520 Kč | `inside-oven` 200 Kč — [insert_seed_data.sql:749](../../sql-scripts/insert_seed_data.sql#L749) |
| Doplněk „mytí lednice" | 330 Kč | `inside-fridge` 150 Kč — [insert_seed_data.sql:750](../../sql-scripts/insert_seed_data.sql#L750) |
| Doplněk „žehlení" | 560 Kč | `laundry-ironing` 250 Kč — [insert_seed_data.sql:752](../../sql-scripts/insert_seed_data.sql#L752) |
| Minimální cena zakázky | 690 Kč | žádná — [OrderPricingCalculator.cs:96](../../src/Cleansia.Core.AppServices/Services/OrderPricingCalculator.cs#L96) |
| Garanční kredit za nedostavení | 250 Kč, návrh až 500 Kč | `NoShowCredit` 250 Kč — [insert_seed_data.sql:490](../../sql-scripts/insert_seed_data.sql#L490) |
| Sazba DPH použitá na zakázce | 21 % odváděných | `StandardVatRate` 0,21 u CZE, u neplátce nula — [insert_seed_data.sql:1010](../../sql-scripts/insert_seed_data.sql#L1010) |
| Cleansia Plus | 199 Kč měsíčně, 2 030 Kč ročně | totéž — [insert_seed_data.sql:1836-1837](../../sql-scripts/insert_seed_data.sql#L1836-L1837) |
| Kredit na objednávku | čerpá se automaticky, max. 70 %, jen u karty (strom s. 3) | `MaxCreditShareOfOrder` 0,70, jen u karty (→ §6) — [BookingPolicy.cs:102](../../src/Cleansia.Core.AppServices/Features/Orders/BookingPolicy.cs#L102) |
