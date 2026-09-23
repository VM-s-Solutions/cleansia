# Obchodní podklady kolegů proti kódu

**Tři podklady k obchodnímu modelu proti zdrojovému kódu · commit `9e970917e1c7270a0baa0ea1ab07693990340914` (`fix/audit-findings-2026-09-22`, 23. 9. 2026)**

Slovník: úklidník = „uklízečka" v podkladech = `Employee` (kód); zákazník = `User`; host = zákazník bez účtu (`Order.UserId = null`); provozní společnost = `Tenant`; administrátor = `Administrator / Manager / Support / Accountant`. Částky v Kč.

Odkazy „schéma s. 3“, „strom s. 4“ a „sešit, list Balíčky“ míří do `colleague-documents/`. R22–R27 doplňují registr rozhodnutí; R2, rámec R11 a R16 jsou rozhodnuté.

## Shrnutí

Podklady navrhují **model A**, zprostředkování s úklidníky na IČO, a ceník v3. Proti kódu se rozcházejí v cenotvorbě, odměně, DPH a hotovostním vypořádání.

**Cena.** Sešit odvozuje ceny z m², minut na m² a hodinové sazby. Kód oceňuje katalogové položky; dobu odhaduje ze služeb včetně balíčků. Služba stojí `BasePrice + PerRoomPrice × (Rooms + Bathrooms)`, express přidává 20 %. Deset velikostních balíčků ESSENTIAL/DEEP nemá přímý katalogový protějšek.

**Odměna.** Podklady kotví 300 Kč/h. Kód platí sazbu za položku (seed 0,5 × cena) a **každému přiřazenému úklidníkovi celou**: balíček `Deep Clean Premium` za 1 799 Kč má odhad 315 minut, potřebuje `ceil(315/120)` = 3 úklidníky a vyplatí 3 × 899,50 = 2 698,50 Kč.

**DPH.** Sešit předpokládá plátcovství a 21 %. Seed vede společnost jako neplátce; doklad má lokalizované oznámení. DPH se z ceny vyjímá, nepřičítá; samotná změna plátcovství katalogové ceny nezvedne.

**Hotovost a karta.** Schéma navrhuje čtyřkrokové vypořádání (částka, potvrzení zákazníkem, týdenní saldo, limit 5 000 Kč) a uloženou kartu jako záruku. Kód eviduje jen fakt „hotovost vybrána, kým, kdy" bez částky; z uložené karty strhává jediná věc — obnova členství Plus, nikdy zakázka.

Rozhodnutí R22–R27 jsou otevřená.

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
| Jedna uklízečka na 120 minut práce (schéma s. 2) | `RequiredEmployees = ceil(EstimatedTime / 120)`, `MinutesPerEmployee` — [OrderDuration.cs:27](../../src/Cleansia.Core.Domain/Orders/OrderDuration.cs#L27) | platí | — |
| Preferovaná uklízečka drží zakázku 10 % předstihu, max. 12 h (strom s. 3) | `PreferredHoldFraction`, `PreferredHoldCeilingHours` — [BookingPolicy.cs:217-218](../../src/Cleansia.Core.AppServices/Features/Orders/BookingPolicy.cs#L217-L218) | platí | — |
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
| Premium tier | +25 %, dělit s úklidníkem | `grep "PremiumTier|PremiumSurcharge" src/` → 0; `baseSubtotal` další člen nemá — [OrderPricingCalculator.cs:96](../../src/Cleansia.Core.AppServices/Services/OrderPricingCalculator.cs#L96) | → R25 |
| Silné znečištění | +30 % | `grep "HeavySoil|Soiling|DirtLevel" AppServices/` → 0; `baseSubtotal` je jen součet tří položek — [OrderPricingCalculator.cs:96](../../src/Cleansia.Core.AppServices/Services/OrderPricingCalculator.cs#L96) | → R12 |
| Noční a víkendový úklid | +25 % | hledání `NightSurcharge`/`WeekendSurcharge` v AppServices: žádná sazba; server kontroluje `IsBelowMinimumLeadTime`, nikoli denní okno — [CreateOrder.cs:162-167](../../src/Cleansia.Core.AppServices/Features/Orders/CreateOrder.cs#L162-L167) | → R25 |
| Mazlíček | +10 % | sazba není; existuje položka `pet-hair-supplement` 150 Kč — [insert_seed_data.sql:753](../../sql-scripts/insert_seed_data.sql#L753) | → R25 |
| Pokoj navíc | +330 / +570 Kč | samostatná položka není; cena roste členem `PerRoomPrice × (Rooms + Bathrooms)` — [OrderPricingCalculator.cs:59](../../src/Cleansia.Core.AppServices/Services/OrderPricingCalculator.cs#L59) | → R25 |
| Koupelna navíc | +430 / +770 Kč | koupelna se násobí toutéž sazbou jako pokoj — [OrderPricingCalculator.cs:59](../../src/Cleansia.Core.AppServices/Services/OrderPricingCalculator.cs#L59) | → R25 |
| 5. patro bez výtahu, balkon, samostatné WC | +190 / +240 / +200 Kč | `grep "Elevator|Balcony|SeparateWc" src/` → 0 věcných; `CustomerFloor` je text, cena ho nečte — [Order.cs:181-182](../../src/Cleansia.Core.Domain/Orders/Order.cs#L181-L182) | → R25 |
| Devět doplňků (lednice, trouba, žehlení, ozón…) | 330–1 690 Kč | pět položek `Extras`: 100–250 Kč, v seedu označené jako zástupné — [insert_seed_data.sql:749-753](../../sql-scripts/insert_seed_data.sql#L749-L753) | → R25 |

## 5. Odměna úklidníka

| tvrzení podkladu | co říká kód | → |
|---|---|---|
| Úklidník dostane 300 Kč za odpracovanou hodinu (schéma s. 1) | konfigurace nese `BasePay`, `ExtraPerRoom`, `ExtraPerBathroom`, `DistanceRatePerKm`; hodinová sazba nikde — [EmployeePayConfig.cs:22-28](../../src/Cleansia.Core.Domain/EmployeePayroll/EmployeePayConfig.cs#L22-L28) | → R23 |
| Odměna = hodiny × sazba (sešit, list Metodika) | `BasePay + max(0, Rooms − 1) × ExtraPerRoom + Bathrooms × ExtraPerBathroom + km × DistanceRatePerKm` — [PayCalculatorExtensions.cs:12-16](../../src/Cleansia.Core.Domain/Extensions/PayCalculatorExtensions.cs#L12-L16) | → R23 |
| — | výsledek ořízne `ApplyMinMaxClamp`; seedované meze jsou nulové, tedy žádné — [PayCalculatorExtensions.cs:18](../../src/Cleansia.Core.Domain/Extensions/PayCalculatorExtensions.cs#L18) | → R23 |
| Podíl úklidníka z ceny 42–53 % (sešit, list Balíčky) | seed počítá `ROUND(BasePrice * 0.5, 2)`, šablona stupně „junior" — [insert_seed_data.sql:781](../../sql-scripts/insert_seed_data.sql#L781) | → R23 |
| Část příplatku za znečištění a noc patří úklidníkovi (sešit, list Příplatky) | odměna nezávisí na ceně ani na expresním příplatku — [PayCalculatorExtensions.cs:12-16](../../src/Cleansia.Core.Domain/Extensions/PayCalculatorExtensions.cs#L12-L16) | → R23 |
| Provize platformy = 1 − podíl úklidníka (sešit, list Balíčky) | `totalPay` procentní provizi nezná — `grep "Commission|Provize" src/` → 0 — [PayCalculatorExtensions.cs:16](../../src/Cleansia.Core.Domain/Extensions/PayCalculatorExtensions.cs#L16) | → R1 |
| Cena i odměna jsou na jednu zakázku (sešit, list Ekonomika) | každý přiřazený dostane úplný výpočet včetně balíčků; zákaznická cena se posádkou nenásobí — [CompleteOrder.cs:346-354](../../src/Cleansia.Core.AppServices/Features/Orders/CompleteOrder.cs#L346-L354), [CalculateOrderPay.cs:118-157](../../src/Cleansia.Core.AppServices/Features/EmployeePayroll/CalculateOrderPay.cs#L118-L157) | → R26 |
| Příklad DEEP L: cena 3 540 Kč, odměna 1 813 Kč (schéma s. 5) | sazba balíčku je `ROUND(Price * 0.5, 2)`, tedy 899,50 Kč, a platí se třikrát — [insert_seed_data.sql:797](../../sql-scripts/insert_seed_data.sql#L797) | → R26 |
| Doprava a chemie jsou v sazbě úklidníka (sešit, list Vstupy) | seed zapisuje poslední dvě sazby jako `0, 0`, šablona „junior template" — [insert_seed_data.sql:781](../../sql-scripts/insert_seed_data.sql#L781) | → R23 |
| Splatnost samofaktury (strom s. 4) | `PaymentTermsDays` = 14 dnů — [Constants.cs:78](../../src/Cleansia.Core.AppServices/Common/Constants.cs#L78) | platí | 

## 6. Hotovost: návrh vypořádání proti evidenci faktu

| krok návrhu (schéma s. 4) | co má kód | → |
|---|---|---|
| ① uložená karta jako záruka, přesná částka v aplikaci | `SetupFutureUsage` je jen na mobilní platbě; `Usage = "off_session"` slouží členství — [StripeClient.cs:189](../../src/Cleansia.Infra.Clients/Stripe/StripeClient.cs#L189), [StripeClient.cs:297](../../src/Cleansia.Infra.Clients/Stripe/StripeClient.cs#L297) | → R27 |
| ① zakázku vidí jen úklidník pod limitem hotovostního dluhu | nabídka filtruje platební typ objednávky, nikdy dluh úklidníka — [OrderAvailability.cs:61-67](../../src/Cleansia.Core.Domain/Orders/OrderAvailability.cs#L61-L67) | → R6 |
| ② úklidník zadá přijatou částku | `MarkCashCollected` zapisuje `CashCollectedAt` a `CollectedByEmployeeId`, částku ne — [Order.cs:50-52](../../src/Cleansia.Core.Domain/Orders/Order.cs#L50-L52) | → R6 |
| ② zákazník platbu potvrdí, teprve pak doklad | `GenerateReceipt` vzniká při objednání; `MarkCashCollected` jej přegeneruje jako zaplacený, bez potvrzení zákazníkem — [OrderPaymentDispatcher.cs:82-91](../../src/Cleansia.Core.AppServices/Features/Orders/OrderPaymentDispatcher.cs#L82-L91), [MarkCashCollected.cs:168-181](../../src/Cleansia.Core.AppServices/Features/Orders/MarkCashCollected.cs#L168-L181) | → R6 |
| ③ týdenní saldo = odměny − vybraná hotovost | `grep "Saldo|CashBalance|CashOwed" src/` → 0; `PayPeriod` nese jen datum, stav a vazby — [PayPeriod.cs:9-31](../../src/Cleansia.Core.Domain/EmployeePayroll/PayPeriod.cs#L9-L31) | → R6 |
| ③ řádek hotovosti v samofaktuře | řádek odměny nese `BasePay`, `ExtrasPay`, `ExpensesPay`, `BonusPay`; hotovost žádnou — [OrderEmployeePay.cs:38-58](../../src/Cleansia.Core.Domain/EmployeePayroll/OrderEmployeePay.cs#L38-L58) | → R6 |
| ③ úklidník odvede do 7 dnů pod variabilním symbolem | `VariableSymbol` patří výplatní faktuře, tedy směru firma → úklidník — [EmployeeInvoice.cs:74](../../src/Cleansia.Core.Domain/EmployeePayroll/EmployeeInvoice.cs#L74) | → R6 |
| ④ dluh nad 5 000 Kč blokuje hotovostní zakázky | `grep "CashLimit|DebtCeiling|CashDebt" src/` → 0; jediný strop je `WeeklyOrderLimit` — [Employee.cs:148](../../src/Cleansia.Core.Domain/Users/Employee.cs#L148) | → R6 |
| ④ nezaplacení se dorovná z uložené karty | storno poplatek se zapíše jako sazba a u hotovosti se nevybere — [CustomerOrderCancellation.cs:90-95](../../src/Cleansia.Core.AppServices/Features/Orders/CustomerOrderCancellation.cs#L90-L95) | → R27 |
| Inkasní zmocnění úklidníka v rámcové smlouvě | hledání `DirectDebit`/`Mandate` v produkčním C#: žádné inkasní zmocnění; karta používá `CreateCheckoutSessionAsync` nebo `CreatePaymentIntentAsync` — [IStripeClient.cs:16](../../src/Cleansia.Core.Clients.Abstractions/Stripe/IStripeClient.cs#L16), [CreatePaymentIntent.cs:133-141](../../src/Cleansia.Core.AppServices/Features/Orders/CreatePaymentIntent.cs#L133-L141) | → R6 |

## 7. DPH, doklady a daňová plocha

| tvrzení podkladu | co říká kód | → |
|---|---|---|
| Cleansia je plátce DPH, sazba 21 % (sešit, list Vstupy) | seedovaná společnost má `IsVatPayer = false` — [insert_seed_data.sql:1163](../../sql-scripts/insert_seed_data.sql#L1163) | → R24 |
| Odvod DPH 17,4 % z ceny (sešit, list Ekonomika) | DPH se z hrubé ceny vyjímá: `gross × rate / (1 + rate)` — [VatCalculator.cs:45-49](../../src/Cleansia.Core.AppServices/Services/VatCalculator.cs#L45-L49) | → R24 |
| Základ DPH při zprostředkování (strom s. 6) | `Calculate` dostává celou `TotalPrice` zákazníka, ne provizi — [OrderFactory.cs:320-321](../../src/Cleansia.Core.AppServices/Features/Orders/OrderFactory.cs#L320-L321) | → R1 |
| Plátcovství zdražuje DEEP L o 850 Kč (sešit, list Citlivost) | registrace katalogovou cenu nemění; `baseSubtotal` člen DPH nemá — [OrderPricingCalculator.cs:96](../../src/Cleansia.Core.AppServices/Services/OrderPricingCalculator.cs#L96) | → R24 |
| Doklad zákazníkovi RCP-RRRR-NNNN od provozní společnosti (strom s. 4) | řada odpovídá; neplátce má lokalizované oznámení a netiskne DIČ — [Constants.cs:83-85](../../src/Cleansia.Core.AppServices/Common/Constants.cs#L83-L85), [DefaultReceiptLayoutBuilder.cs:155-173](../../src/Cleansia.Infra.Services/Pdf/Layouts/DefaultReceiptLayoutBuilder.cs#L155-L173) | → R24 |
| Faktura postrádá prohlášení „vystaveno odběratelem" (strom s. 4) | popisky nesou jen `Supplier` a `Customer`; `grep "self-billing" Cleansia.Infra.Services` → 0 — [InvoiceLabels.cs:77-78](../../src/Cleansia.Infra.Services/Pdf/Models/InvoiceLabels.cs#L77-L78) | → R1 |
| Řádky dokladu se nesčítají na uvedený součet (strom s. 4) | Pro PDF neplatí: `InCents` vyrovná uložené slevy; `ItemLines` tiskne položky, express a záporné slevy — [OrderFactory.cs:421-454](../../src/Cleansia.Core.AppServices/Features/Orders/OrderFactory.cs#L421-L454), [DefaultReceiptLayoutBuilder.cs:300-340](../../src/Cleansia.Infra.Services/Pdf/Layouts/DefaultReceiptLayoutBuilder.cs#L300-L340) | — |
| Fiskální podklady | `BuildFiscalLineItems` obsahuje služby a balíčky, nikoli doplňky, slevy a express — [ReceiptService.cs:368-393](../../src/Cleansia.Core.AppServices/Services/ReceiptService.cs#L368-L393) | → R21 |
| DAC7 a evidence tržeb nese provozní společnost (schéma s. 8) | `grep "DAC7" src/` → 0; fiskální poskytovatel vrací `NOT_IMPLEMENTED` — [CzechEet2FiscalService.cs:53-55](../../src/Cleansia.Infra.Fiscal/Countries/Czechia/CzechEet2FiscalService.cs#L53-L55) | → R21 |
| IČO a DIČ provozní společnosti | seed je `REPLACE WITH ACTUAL`; plátce musí dodat `VatNumber` — [insert_seed_data.sql:1161-1162](../../sql-scripts/insert_seed_data.sql#L1161-L1162), [UpdateCompanyInfo.cs:74-79](../../src/Cleansia.Core.AppServices/Features/Company/UpdateCompanyInfo.cs#L74-L79) | → R2 |

## 8. Značky „rozpor" a „k dostavbě" z podkladů — stav na auditovaném commitu

| tvrzení podkladu | stav | doklad |
|---|---|---|
| Registr zná „Cleansia CZ s.r.o.", doklady tisknou „Cleansia s.r.o." (strom s. 1) | platí | [insert_seed_data.sql:1158](../../sql-scripts/insert_seed_data.sql#L1158) |
| Smlouvu o dílo teprve generovat při přijetí zakázky (strom s. 5) | postaveno, bez PDF a bez kvalifikovaného podpisu | [TakeOrder.cs:357](../../src/Cleansia.Core.AppServices/Features/Orders/TakeOrder.cs#L357) |
| Rozhodnout „jedna s.r.o. pro všechny" (strom s. 1) | rozhodnuto ADR-0061: holding a společnost na region | [adr-0061.md:39-40](../../docs/decisions/adr-0061.md#L39-L40) |
| K dostavbě: zrušení hostovských objednávek (schéma s. 2) | host ruší platným `AccessToken`; `RevokeAsync` při stornu tokeny odvolá | [CancelGuestOrder.cs:18](../../src/Cleansia.Core.AppServices/Features/Orders/CancelGuestOrder.cs#L18), [CancelGuestOrder.cs:61](../../src/Cleansia.Core.AppServices/Features/Orders/CancelGuestOrder.cs#L61) |
| K dostavbě: tlačítka Odstoupit a Zástup (schéma s. 2) | server obě trasy nese, volající v aplikacích chybí | [OrderController.cs:240](../../src/Cleansia.Web.Partner/Controllers/OrderController.cs#L240) |
| K dostavbě: detekce nedostavení úklidníka (schéma s. 2) | platí — sweep bere jen zakázky bez posádky | [CancelUnfilledOrders.cs:112-118](../../src/Cleansia.Core.AppServices/Features/Orders/CancelUnfilledOrders.cs#L112-L118) |
| K dostavbě: stav „zákazník neotevřel" (schéma s. 2) | platí — takový stav ani poplatek v kódu není | [OrderStatus.cs:8](../../src/Cleansia.Core.Domain/Enums/OrderStatus.cs#L8) |
| Členství není ve VOP; benefity končí první neúspěšnou platbou (strom s. 5) | platí; plány `PLUS_MONTHLY` 199 Kč a `PLUS_YEARLY` 2 030 Kč — [insert_seed_data.sql:1836-1837](../../sql-scripts/insert_seed_data.sql#L1836-L1837) | → R18 |
| Sleva Plus a věrnostní stupeň se sčítají se stropem 12 % (strom s. 5) | platí | [OrderFactory.cs:361-370](../../src/Cleansia.Core.AppServices/Features/Orders/OrderFactory.cs#L361-L370) |
| Storno 25 % a 50 %, u hotovosti nevymahatelné (strom s. 5) | platí — refunduje se jen `PaymentType.Card` s `PaymentStatus.Paid` | [CustomerOrderCancellation.cs:90-95](../../src/Cleansia.Core.AppServices/Features/Orders/CustomerOrderCancellation.cs#L90-L95) |
| Nikdo nevzal zakázku: 30 min, plná refundace, kredit 250 Kč (strom s. 3) | platí — `TryIssueApologyCreditAsync` píše `CleanerNoShow` jen registrovanému | [CancelUnfilledOrders.cs:276-300](../../src/Cleansia.Core.AppServices/Features/Orders/CancelUnfilledOrders.cs#L276-L300) |
| Týdenní limity zakázek nastavené platformou (schéma s. 9) | platí — strop nastavuje administrátor jednotlivci | [Employee.cs:148](../../src/Cleansia.Core.Domain/Users/Employee.cs#L148) |
| Vynucené tričko, jednotná chemie a vůně (schéma s. 9) | v kódu nic takového není; `Employee` nese jen `WeeklyOrderLimit` a hodnocení | [Employee.cs:148](../../src/Cleansia.Core.Domain/Users/Employee.cs#L148) |

## 9. Nová rozhodnutí

| id | otázka | varianty |
|---|---|---|
| R22 | Kdo určuje cenu zakázky a sazbu úklidníka | platforma určuje obojí (návrh podkladů) · úklidník si sazbu nastaví a zákazník si ho vybere (návrh ze schématu s. 9) · platforma určuje cenu, sazba je pásmo |
| R23 | Model odměny — rozšiřuje R6 | hodinová sazba 300 Kč/h z odhadované doby · dnešní sazby za položku se stropem a minimem · sazba za hodinu s dopočtem z `EstimatedTime` |
| R24 | Plátcovství DPH provozní společnosti | ponechat neplátcovství po právním ověření podmínek · registrovat bez zdražení (DPH z hrubé ceny `gross × rate / (1 + rate)`) · registrovat a ceny upravit podle sešitu |
| R25 | Struktura ceníku | balíčky podle velikosti a m² s dopočtem z pracnosti (podklady) · dnešní katalog služeb a balíčků s `Rooms` a `Bathrooms` · hybrid: velikostní balíčky jako `Packages`, m² jen jako vstup odhadu |
| R26 | Cena a posádka | cena zůstává za zakázku a odměna se násobí posádkou (dnešní stav) · cena roste s počtem úklidníků · odměna se mezi posádku dělí |
| R27 | Uložená karta jako záruka | karta bez záruky (dnešní stav) · karta uložená při objednání a strhávaná za storno, neotevření a nezaplacenou hotovost · záruka jen u hotovostních objednávek |

## 10. Čísla, která se rozcházejí

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
| Kredit na objednávku | čerpá se automaticky, max. 70 % | `MaxCreditShareOfOrder` 0,70 — [BookingPolicy.cs:98](../../src/Cleansia.Core.AppServices/Features/Orders/BookingPolicy.cs#L98) |
