# Rozpory mezi právními dokumenty a aplikací
**Návrhy z 11. 9. 2026 proti zdrojovému kódu · commit 865549b026b02602d11648a3a7e9a12aa2878198 (fix/audit-findings-2026-09-22, 23. 9. 2026)**

Slovník: úklidník = „Zhotovitel" (VOP, RS, RŘ, Kodex) = `Employee` (kód) = „uklízeč" (UI); zákazník = „Zákazník / Spotřebitel / Uživatel" = `User`; host = zákazník bez účtu (`Order.UserId = null`); provozní společnost = „Provozovatel / Operátor / Správce / Cleansia s.r.o." = `Tenant`; administrátor = jeden z `Administrator / Manager / Support / Accountant`, jmenován, kde na roli záleží.

Označení `Pxxx` je číslo odstavce extraktu v `../analyza-2026-09-11/podklady/`, ne číslo článku; článek je ve druhém sloupci.

## Shrnutí

108 odstavců deseti návrhů má srovnávací řádek: 73 rozporů a 35 částečných pokrytí (VOP 15/6, RŘ 9/3, RS 14/6, Kodex 4/2, BOZP 5/0, PP 8/6, CP 6/1, GDPR 7/4, DPA 5/5, SS 0/2). §2 zachycuje 18 funkcí bez odpovídajícího popisu v návrzích. Rozhodnuta jsou R2 (holding a společnost na region), R11 (smlouva o dílo ke každé zakázce) a R16 (úklidník není plátce DPH). Otevřených je osmnáct: R1 obchodní model; R3 přijímané znění; R4 storno a kredit; R5 host; R6 odměna, hotovost a spropitné; R7 fotografie a jejich retence; R8 přístup po zakázce a DPA; R9 cookies, marketing, GPS, věk a retence; R10 provozní lhůty; R12 příplatek na místě; R13 posádka a cena díla na místo; R14 prostředky; R15 omluvný kredit; R17 pokles věrnostního stupně; R18 události a kanály oznámení; R19 objednávka mimo trh účtu; R20 nezávislá spolupráce; R21 fiskalizace. R11 má šest otevřených podotázek (R11.1, R11.3–R11.7).

## 1. Dokument po dokumentu

### 1.1 VOP

Shoda: [VOP P020](../analyza-2026-09-11/podklady/Cleansia_VOP.txt), [VOP P024](../analyza-2026-09-11/podklady/Cleansia_VOP.txt), [VOP P085](../analyza-2026-09-11/podklady/Cleansia_VOP.txt), [VOP P091](../analyza-2026-09-11/podklady/Cleansia_VOP.txt), [VOP P109](../analyza-2026-09-11/podklady/Cleansia_VOP.txt), [VOP P111](../analyza-2026-09-11/podklady/Cleansia_VOP.txt)

Mimo aplikaci (provozní závazek, nic v kódu): P059, P087, P089, P093, P115, P127, P129, P135

| Pxxx | čl. | citát | kód | citace | třída | řešení |
|---|---|---|---|---|---|---|
| [VOP P014](../analyza-2026-09-11/podklady/Cleansia_VOP.txt) | úvod | „Registrací v Aplikaci a/nebo podáním první objednávky zákazník potvrzuje, že se s VOP seznámil" | Seedované podmínky nesou banner `Návrh`; zákazník přijímá jiný text než návrh | [cs.md:5](../../src/Cleansia.Infra.Database/Seed/Legal/customer/terms-of-service/any/2026-09-14/cs.md#L5) | rozpor | → R3 |
| [VOP P022](../analyza-2026-09-11/podklady/Cleansia_VOP.txt) | 1.3 | „smlouva o dílo na konkrétní úklid vzniká přímo mezi Zákazníkem a Zhotovitelem okamžikem potvrzení zakázky" | Řádek přijetí zapisuje převzetí (`StageAsync`); seedované podmínky: `Cleansia` „poskytuje profesionální úklidové služby" | [TakeOrder.cs:357](../../src/Cleansia.Core.AppServices/Features/Orders/TakeOrder.cs#L357); [cs.md:15](../../src/Cleansia.Infra.Database/Seed/Legal/customer/terms-of-service/any/2026-09-14/cs.md#L15) | částečně | → R1 |
| [VOP P028](../analyza-2026-09-11/podklady/Cleansia_VOP.txt) | 2.1 | „jméno a příjmení, e-mailovou adresu, telefonní číslo a fakturační adresu" | `Register.Command` bez adresy; host objednává bez účtu, `Order.UserId` nullable | [Register.cs:73-88](../../src/Cleansia.Core.AppServices/Features/Auth/Register.cs#L73-L88); [Order.cs:222](../../src/Cleansia.Core.Domain/Orders/Order.cs#L222) | rozpor | → R5 |
| [VOP P030](../analyza-2026-09-11/podklady/Cleansia_VOP.txt) | 2.2 | „Služby Cleansia mohou využívat pouze osoby starší 18 let" | `Register.Command` nese `TermsAccepted`, ne datum narození; věk zákazníka je bez dolní hranice (`BeReasonableAge` jen do 120 let) | [Register.cs:73-89](../../src/Cleansia.Core.AppServices/Features/Auth/Register.cs#L73-L89); [UpdateCurrentUser.cs:108-112](../../src/Cleansia.Core.AppServices/Features/Users/UpdateCurrentUser.cs#L108-L112) | rozpor | → R9 |
| [VOP P034](../analyza-2026-09-11/podklady/Cleansia_VOP.txt) | 2.4 | „V případě zrušení účtu jsou Zákazníkovi vráceny nevyužité kredity v peněžní hodnotě" | Kladný zůstatek výmaz blokuje (`GdprDeletionBlockedByCreditBalance`); výplata neexistuje, zůstatek propadá (`ExpiryMonths` 12 měsíců) | [GdprDeletionService.cs:160-163](../../src/Cleansia.Core.AppServices/Services/GdprDeletionService.cs#L160-L163); [CreditAccount.cs:69](../../src/Cleansia.Core.Domain/Credit/CreditAccount.cs#L69) | rozpor | → R4 |
| [VOP P038](../analyza-2026-09-11/podklady/Cleansia_VOP.txt) | 3.1 | „Objednávka je závazná okamžikem potvrzení platby" | Webhook zapisuje jen `PaymentStatus.Paid`; hotovostní objednávka (`PaymentType.Cash`) je závazná bez jakékoli platby | [HandlePaymentNotification.cs:297-309](../../src/Cleansia.Core.AppServices/Features/Payments/HandlePaymentNotification.cs#L297-L309); [PaymentType.cs:8-9](../../src/Cleansia.Core.Domain/Enums/PaymentType.cs#L8-L9) | rozpor | → R11.5 |
| [VOP P040](../analyza-2026-09-11/podklady/Cleansia_VOP.txt) | 3.2 | „Potvrzení obsahuje shrnutí objednávky, předpokládaný termín a informace o přiděleném Zhotoviteli" | Při vzniku objednávky úklidník neexistuje; po převzetí push `order.cleaner_assigned` bez jména | [NotificationEventCatalog.cs:24-29](../../src/Cleansia.Core.Domain/Notifications/NotificationEventCatalog.cs#L24-L29); [TakeOrder.cs:421-422](../../src/Cleansia.Core.AppServices/Features/Orders/TakeOrder.cs#L421-L422) | rozpor | → R11.5 |
| [VOP P042](../analyza-2026-09-11/podklady/Cleansia_VOP.txt) | 3.3 | „nejpozději [doplnit, např. 24 hodin] před sjednaným termínem zahájení úklidu" | Změna termínu ani rozsahu neexistuje; `TotalPrice` má jediný zápis při založení | [Order.cs:68](../../src/Cleansia.Core.Domain/Orders/Order.cs#L68) | rozpor | → R12 |
| [VOP P044](../analyza-2026-09-11/podklady/Cleansia_VOP.txt) | 3.4 | „zákazník neodpovídá na hovory po dobu více než 15 minut" | Stav „zákazník nedostupný" neexistuje; nejvyšší sazba `LastMinuteCancellationFeeRate` je 0,50 | [BookingPolicy.cs:71](../../src/Cleansia.Core.AppServices/Features/Orders/BookingPolicy.cs#L71) | rozpor | → R4 |
| [VOP P050](../analyza-2026-09-11/podklady/Cleansia_VOP.txt) | 4.2 | „platební karty (Visa, Mastercard, Amex) … bankovního převodu" | `PaymentType` má jen `Cash = 1`, `Card = 2`; bankovní převod zákazníka neexistuje | [PaymentType.cs:8-9](../../src/Cleansia.Core.Domain/Enums/PaymentType.cs#L8-L9) | rozpor | → R6 |
| [VOP P056](../analyza-2026-09-11/podklady/Cleansia_VOP.txt) | 4.5 | „Spropitné je dobrovolné … v plné výši předáno Zhotoviteli" | Výpočet odměny spropitné nezahrnuje; ukládá součet sazeb přes `CalculateAggregatedPay` | [CalculateOrderPay.cs:142-180](../../src/Cleansia.Core.AppServices/Features/EmployeePayroll/CalculateOrderPay.cs#L142-L180) | rozpor | → R6 |
| [VOP P064](../analyza-2026-09-11/podklady/Cleansia_VOP.txt) | V ř. 1 | „Více než 24 hodin předem" | Plus používá konfigurované `FreeCancellationWindowHours`; DEV seed nastavuje 4 h | [CancellationPolicyResolver.cs:35-44](../../src/Cleansia.Core.AppServices/Services/CancellationPolicyResolver.cs#L35-L44); [insert_seed_data.sql:1800-1806](../../sql-scripts/insert_seed_data.sql#L1800-L1806) | částečně | → R4 |
| [VOP P069](../analyza-2026-09-11/podklady/Cleansia_VOP.txt) | V ř. 2 | „50 % ceny zakázky" | 4–24 h předem 25 % (`PartialCancellationFeeRate`); seedované podmínky říkají 25 %/50 % | [BookingPolicy.cs:68](../../src/Cleansia.Core.AppServices/Features/Orders/BookingPolicy.cs#L68); [cs.md:23](../../src/Cleansia.Infra.Database/Seed/Legal/customer/terms-of-service/any/2026-09-14/cs.md#L23) | rozpor | → R4 |
| [VOP P073](../analyza-2026-09-11/podklady/Cleansia_VOP.txt) | V ř. 3 | „100 % ceny zakázky" | Sazba 100 % neexistuje; pod `PartialCancellationHours` platí 0,50; `Cancel` sazbu jen zapíše do `CancellationFeeRate`, u hotovosti ji nikdo nevybere | [BookingPolicy.cs:71-74](../../src/Cleansia.Core.AppServices/Features/Orders/BookingPolicy.cs#L71-L74); [Order.cs:910](../../src/Cleansia.Core.Domain/Orders/Order.cs#L910) | rozpor | → R4 |
| [VOP P081](../analyza-2026-09-11/podklady/Cleansia_VOP.txt) | 5.4 | „plnou náhradu zaplacené částky do 5 pracovních dnů nebo možnost přesunout objednávku na jiný termín" | Neobsazená zakázka: sweep po 30 min (`GraceMinutes`), vratka; DEV seed `NoShowCredit` 250 Kč; přesun neexistuje | [CancelUnfilledOrders.cs:63](../../src/Cleansia.Core.AppServices/Features/Orders/CancelUnfilledOrders.cs#L63); [insert_seed_data.sql:490](../../sql-scripts/insert_seed_data.sql#L490) | částečně | → R15 |
| [VOP P097](../analyza-2026-09-11/podklady/Cleansia_VOP.txt) | 7.1 | „minimální limit pojistného plnění činí 3 000 000 Kč na pojistnou událost" | DEV seed CZE `InsuranceCoverageAmount` 1 000 000 Kč; `Employee : TenantAuditable` pole pojištění nemá | [insert_seed_data.sql:1014](../../sql-scripts/insert_seed_data.sql#L1014); [Employee.cs:11](../../src/Cleansia.Core.Domain/Users/Employee.cs#L11) | rozpor | text |
| [VOP P101](../analyza-2026-09-11/podklady/Cleansia_VOP.txt) | 7.3 | „(nejpozději do 24 hodin od dokončení zakázky) nahlásit škodu v Aplikaci nebo na e-mail" | Okno `FilingWindowHours` 24 h jen označí `FiledWithinWindow`; host spor nepodá (`CustomerOnly`) | [DisputeLimits.cs:18-29](../../src/Cleansia.Core.Domain/Disputes/DisputeLimits.cs#L18-L29); [PolicyBuilder.cs:130](../../src/Cleansia.Core.AppServices/Authentication/PolicyBuilder.cs#L130) | částečně | → R5 |
| [VOP P105](../analyza-2026-09-11/podklady/Cleansia_VOP.txt) | 7.5 | „Reklamačním řádu Cleansia, který je dostupný na webu a v Aplikaci" | `LegalDocumentType` zná jen `TermsOfService`, `PrivacyPolicy`, `WorkContract` | [LegalDocumentType.cs:8-16](../../src/Cleansia.Core.Domain/Legal/LegalDocumentType.cs#L8-L16) | rozpor | → R3 |
| [VOP P121](../analyza-2026-09-11/podklady/Cleansia_VOP.txt) | 10.1 | „informován e-mailem nebo oznámením v Aplikaci nejpozději 14 dní před jejich účinností" | Nová verze = `LegalDocument.Create` s jiným `EffectiveFrom`, bez oznámení; objednávka projde s jakýmkoli `IsGranted` souhlasem bez ohledu na verzi | [LegalDocumentSeeder.cs:51-62](../../src/Cleansia.Infra.Database/Seed/Legal/LegalDocumentSeeder.cs#L51-L62); [CreateOrder.cs:330-334](../../src/Cleansia.Core.AppServices/Features/Orders/CreateOrder.cs#L330-L334) | částečně | → R3 |
| [VOP P123](../analyza-2026-09-11/podklady/Cleansia_VOP.txt) | 10.2 | „budou vráceny kredity a přeplatky za již zaplacené, ale neprovedené zakázky" | Při útlumu deaktivované společnosti `DischargeCreditAsync` kredit odepisuje jako `Expired`; peněžní výplata kreditu neexistuje | [CompanyWindDownService.cs:82-84](../../src/Cleansia.Core.AppServices/Services/CompanyWindDownService.cs#L82-L84); [CompanyWindDownService.cs:449-468](../../src/Cleansia.Core.AppServices/Services/CompanyWindDownService.cs#L449-L468) | rozpor | → R4 |
| [VOP P142](../analyza-2026-09-11/podklady/Cleansia_VOP.txt) | UMÍSTĚNÍ | „Bez zaregistrovaného souhlasu nelze zákazníka zaregistrovat ani mu umožnit objednání služby" | Registrace souhlas zapisuje; objednávku pustí tvrzení klienta (`AssertedOrAlreadyConsentedAsync`), host řádek souhlasu nemá | [CreateOrder.cs:317-322](../../src/Cleansia.Core.AppServices/Features/Orders/CreateOrder.cs#L317-L322) | částečně | → R5 |

### 1.2 RŘ

Shoda: [RŘ P030](../analyza-2026-09-11/podklady/cleansia_reklamacni_rad.txt), [RŘ P031](../analyza-2026-09-11/podklady/cleansia_reklamacni_rad.txt), [RŘ P034](../analyza-2026-09-11/podklady/cleansia_reklamacni_rad.txt), [RŘ P059](../analyza-2026-09-11/podklady/cleansia_reklamacni_rad.txt), [RŘ P061](../analyza-2026-09-11/podklady/cleansia_reklamacni_rad.txt), [RŘ P110](../analyza-2026-09-11/podklady/cleansia_reklamacni_rad.txt)

Mimo aplikaci (provozní závazek, nic v kódu): P052, P114–P126, P134

| Pxxx | čl. | citát | kód | citace | třída | řešení |
|---|---|---|---|---|---|---|
| [RŘ P018](../analyza-2026-09-11/podklady/cleansia_reklamacni_rad.txt) | 1.1 | „Přímým poskytovatelem úklidových služeb je Zhotovitel jakožto samostatná OSVČ" | Doklad zákazníkovi vystavuje provozní společnost (`CompanyInfo` jako vystavitel) | [ReceiptService.cs:555-561](../../src/Cleansia.Core.AppServices/Services/ReceiptService.cs#L555-L561) | rozpor | → R1 |
| [RŘ P020](../analyza-2026-09-11/podklady/cleansia_reklamacni_rad.txt) | 1.2 | „každý Zákazník, který si prostřednictvím Aplikace objednal a zaplatil úklidovou službu" | Spor podá jen přihlášený (`CanCreateDispute` = `CustomerOnly`); host nemá endpoint | [PolicyBuilder.cs:130](../../src/Cleansia.Core.AppServices/Authentication/PolicyBuilder.cs#L130); [CreateDispute.cs:112-126](../../src/Cleansia.Core.AppServices/Features/Disputes/CreateDispute.cs#L112-L126) | částečně | → R5 |
| [RŘ P046](../analyza-2026-09-11/podklady/cleansia_reklamacni_rad.txt) | 3.1 | „nejpozději do 24 hodin od dokončení zakázky potvrzeného v Aplikaci" | Jediné okno `FilingWindowHours` 24 h, jen příznak; 48 h pro krádež neexistuje | [DisputeLimits.cs:29](../../src/Cleansia.Core.Domain/Disputes/DisputeLimits.cs#L29); [DisputeLimits.cs:35-37](../../src/Cleansia.Core.Domain/Disputes/DisputeLimits.cs#L35-L37) | částečně | text |
| [RŘ P053](../analyza-2026-09-11/podklady/cleansia_reklamacni_rad.txt) | III | „ověřit stav před úklidem z fotodokumentace Zhotovitele" | `PhotoType.Before` nikde vyžadován; úklidník spor nevidí (partnerské hosty bez `Dispute`) | [PhotoType.cs:8](../../src/Cleansia.Core.Domain/Enums/PhotoType.cs#L8); [PolicyBuilder.cs:130-139](../../src/Cleansia.Core.AppServices/Authentication/PolicyBuilder.cs#L130-L139) | rozpor | → R7 |
| [RŘ P062](../analyza-2026-09-11/podklady/cleansia_reklamacni_rad.txt) | 4.1 | „Fotodokumentaci — min. 2 fotografie dokumentující reklamovanou skutečnost" | Založení nese `Reason`, `Description` a řádky, žádnou přílohu; evidence až poté (`FileData`), bez minima | [CreateDispute.cs:73-82](../../src/Cleansia.Core.AppServices/Features/Disputes/CreateDispute.cs#L73-L82); [UploadDisputeEvidence.cs:30-34](../../src/Cleansia.Core.AppServices/Features/Disputes/UploadDisputeEvidence.cs#L30-L34) | rozpor | text |
| [RŘ P067](../analyza-2026-09-11/podklady/cleansia_reklamacni_rad.txt) | 4.3 | „obdrží e-mailem nebo push oznámením v Aplikaci do 30 minut od podání reklamace" | Podání zákazníkovi nic neposílá; jen administrátoři `admin.dispute.filed` | [CreateDispute.cs:186-201](../../src/Cleansia.Core.AppServices/Features/Disputes/CreateDispute.cs#L186-L201) | rozpor | → R10 |
| [RŘ P081](../analyza-2026-09-11/podklady/cleansia_reklamacni_rad.txt) | V k. 2 | „Zákazník obdrží automatické potvrzení o registraci reklamace s referenčním číslem" | Jediná zákaznická sporová událost `dispute.reply` (odpověď podpory) | [NotificationEventCatalog.cs:20](../../src/Cleansia.Core.Domain/Notifications/NotificationEventCatalog.cs#L20) | rozpor | → R18 |
| [RŘ P085](../analyza-2026-09-11/podklady/cleansia_reklamacni_rad.txt) | V k. 3 | „Cleansia zkontaktuje Zhotovitele, vyžádá jeho vyjádření a posoudí fotodokumentaci" | Úklidník spor nevidí ani neodpovídá (`Dispute` 0 výskytů v partnerských kontrolerech) | [PolicyBuilder.cs:130-139](../../src/Cleansia.Core.AppServices/Authentication/PolicyBuilder.cs#L130-L139) | rozpor | → R18 |
| [RŘ P090](../analyza-2026-09-11/podklady/cleansia_reklamacni_rad.txt) | V k. 4 | „Do 30 dnů od podání" | Lhůta rozhodnutí na sporu není; `DisputeDetails` nese jen `FiledWithinWindow` | [DisputeDetails.cs:43](../../src/Cleansia.Core.AppServices/Features/Disputes/DTOs/DisputeDetails.cs#L43) | rozpor | → R10 |
| [RŘ P093](../analyza-2026-09-11/podklady/cleansia_reklamacni_rad.txt) | V k. 5 | „opravný úklid, sleva, vrácení platby nebo kredit" | `ResolveDispute` zná jen `RefundAmount`; kredit ručně `IssueCustomerCredit`; opravný úklid neexistuje | [ResolveDispute.cs:25-39](../../src/Cleansia.Core.AppServices/Features/Disputes/ResolveDispute.cs#L25-L39); [IssueCustomerCredit.cs:14-17](../../src/Cleansia.Core.AppServices/Features/Credit/Admin/IssueCustomerCredit.cs#L14-L17) | částečně | → R10 |
| [RŘ P112](../analyza-2026-09-11/podklady/cleansia_reklamacni_rad.txt) | 5.4 | „povinna Zákazníkovi sdělit důvody zamítnutí" | Zamítnutí bez refundace zákazníka nenotifikuje; `ResolveDispute` posílá jen `order.refunded` | [ResolveDispute.cs:83-139](../../src/Cleansia.Core.AppServices/Features/Disputes/ResolveDispute.cs#L83-L139) | rozpor | → R18 |
| [RŘ P137](../analyza-2026-09-11/podklady/cleansia_reklamacni_rad.txt) | UMÍSTĚNÍ | „(2) V Aplikaci pod Nastavení > Právní dokumenty" | Reklamační řád jako dokument neexistuje (`LegalDocumentType` tři typy) | [LegalDocumentType.cs:8-16](../../src/Cleansia.Core.Domain/Legal/LegalDocumentType.cs#L8-L16) | rozpor | → R3 |

### 1.3 RS

Shoda: [RS P026](../analyza-2026-09-11/podklady/cleansia_ramcova_smlouva.txt), [RS P027](../analyza-2026-09-11/podklady/cleansia_ramcova_smlouva.txt), [RS P028](../analyza-2026-09-11/podklady/cleansia_ramcova_smlouva.txt), [RS P030](../analyza-2026-09-11/podklady/cleansia_ramcova_smlouva.txt), [RS P031](../analyza-2026-09-11/podklady/cleansia_ramcova_smlouva.txt), [RS P032](../analyza-2026-09-11/podklady/cleansia_ramcova_smlouva.txt), [RS P033](../analyza-2026-09-11/podklady/cleansia_ramcova_smlouva.txt), [RS P057](../analyza-2026-09-11/podklady/cleansia_ramcova_smlouva.txt), [RS P063](../analyza-2026-09-11/podklady/cleansia_ramcova_smlouva.txt), [RS P162](../analyza-2026-09-11/podklady/cleansia_ramcova_smlouva.txt)

Mimo aplikaci (provozní závazek, nic v kódu): P045, P061, P071–P077, P085–P087, P095–P101, P110, P136, P144, P152, P174, P178, P184

| Pxxx | čl. | citát | kód | citace | třída | řešení |
|---|---|---|---|---|---|---|
| [RS P017](../analyza-2026-09-11/podklady/cleansia_ramcova_smlouva.txt) | I | „Obchodní firma: Cleansia s.r.o." | Registr `Tenants` „Cleansia CZ s.r.o."; `CompanyInfo` „Cleansia s.r.o." s IČO `REPLACE WITH ACTUAL` | [insert_seed_data.sql:63-64](../../sql-scripts/insert_seed_data.sql#L63-L64); [insert_seed_data.sql:1158-1162](../../sql-scripts/insert_seed_data.sql#L1158-L1162) | částečně | → R2 |
| [RS P041](../analyza-2026-09-11/podklady/cleansia_ramcova_smlouva.txt) | 2.2 | „Operátor je výhradně provozovatelem technologické platformy a zprostředkovatelem zakázek" | `Tenant.cs`: společnost „contracts the customer, employs the cleaner"; doklad vystavuje `CompanyInfo` | [Tenant.cs:7-8](../../src/Cleansia.Core.Domain/Tenancy/Tenant.cs#L7-L8); [ReceiptService.cs:555-561](../../src/Cleansia.Core.AppServices/Services/ReceiptService.cs#L555-L561) | rozpor | → R1 |
| [RS P043](../analyza-2026-09-11/podklady/cleansia_ramcova_smlouva.txt) | 2.3 | „v okamžiku, kdy Zhotovitel prostřednictvím Aplikace potvrdí akceptaci dané zakázky" | Přijetí zapisuje převzetí (`StageAsync`); `AdminReassignOrder` přidá úklidníka (`AddAssignedEmployee`) bez řádku přijetí | [TakeOrder.cs:357](../../src/Cleansia.Core.AppServices/Features/Orders/TakeOrder.cs#L357); [AdminReassignOrder.cs:123](../../src/Cleansia.Core.AppServices/Features/Orders/AdminReassignOrder.cs#L123) | částečně | → R11.6 |
| [RS P059](../analyza-2026-09-11/podklady/cleansia_ramcova_smlouva.txt) | 3.4 | „plnit zakázku osobně nebo prostřednictvím svých vlastních zaměstnanců, spolupracovníků či subdodavatelů" | Každé místo v posádce je vlastní účet a vlastní řádek přijetí (`OrderEmployeeId`); subdodavatele data neznají | [WorkContractAcceptance.cs:31](../../src/Cleansia.Core.Domain/Contracts/WorkContractAcceptance.cs#L31) | rozpor | → R13 |
| [RS P065](../analyza-2026-09-11/podklady/cleansia_ramcova_smlouva.txt) | 3.7 | „Zhotovitel si svobodně volí, které zakázky přijme a které odmítne — bez jakékoliv sankce" | Administrátor smí úklidníkovi nasadit týdenní strop zakázek (`WeeklyOrderLimit`, odmítnutí `order.weekly_limit_reached`); dokončení váže fotografie `After` | [Employee.cs:148](../../src/Cleansia.Core.Domain/Users/Employee.cs#L148); [CompleteOrder.cs:183-188](../../src/Cleansia.Core.AppServices/Features/Orders/CompleteOrder.cs#L183-L188) | rozpor | → R20 |
| [RS P080](../analyza-2026-09-11/podklady/cleansia_ramcova_smlouva.txt) | 4.6 | „může vést k dočasnému pozastavení nebo ukončení spolupráce" | Převzetí vyžaduje `ContractStatus.Approved`; hodnocení není bránou převzetí ani zdrojem týdenního limitu | [TakeOrder.cs:208-225](../../src/Cleansia.Core.AppServices/Features/Orders/TakeOrder.cs#L208-L225) | rozpor | → R20 |
| [RS P089](../analyza-2026-09-11/podklady/cleansia_ramcova_smlouva.txt) | 5.3 | „zavazuje se Zhotovitel takto vyplacenou částku Operátorovi v plné výši refundovat" | Refundace ani chargeback odměnu nemění; `DeductionPay` zvyšuje jen ruční srážka, po ní `RecomputeTotalPay` | [OrderEmployeePay.cs:183-205](../../src/Cleansia.Core.Domain/EmployeePayroll/OrderEmployeePay.cs#L183-L205) | rozpor | → R6 |
| [RS P091](../analyza-2026-09-11/podklady/cleansia_ramcova_smlouva.txt) | 5.4 | „zdokumentovat fotografiemi s časovým razítkem a informovat Zákazníka i Operátora prostřednictvím Aplikace" | Fotografie `Before` nevyžadována; `ReportOrderIssue.Handler` nikoho neupozorní | [PhotoType.cs:8](../../src/Cleansia.Core.Domain/Enums/PhotoType.cs#L8); [ReportOrderIssue.cs:42-44](../../src/Cleansia.Core.AppServices/Features/Orders/ReportOrderIssue.cs#L42-L44) | rozpor | → R7 |
| [RS P114](../analyza-2026-09-11/podklady/cleansia_ramcova_smlouva.txt) | 7.1 | „Cena se skládá ze složky odměny Zhotovitele a provize Operátora" | `CalculateAggregatedPay` sčítá sazby služeb/balíčků, pokojů, koupelen a kilometrů; součet omezuje `ApplyMinMaxClamp`, bez provize z ceny | [PayCalculatorExtensions.cs:30-60](../../src/Cleansia.Core.Domain/Extensions/PayCalculatorExtensions.cs#L30-L60); [CalculateOrderPay.cs:142-157](../../src/Cleansia.Core.AppServices/Features/EmployeePayroll/CalculateOrderPay.cs#L142-L157) | rozpor | → R6 |
| [RS P116](../analyza-2026-09-11/podklady/cleansia_ramcova_smlouva.txt) | 7.2 | „provize ve výši [doplnit, např. 20 %] z celkové ceny zakázky zaplacené Zákazníkem" | Provize neexistuje; seed sazby 0,5 × `BasePrice` (`junior template`); faktura = `SubTotal + BonusAmount − DeductionAmount` | [insert_seed_data.sql:781](../../sql-scripts/insert_seed_data.sql#L781); [EmployeeInvoice.cs:200-205](../../src/Cleansia.Core.Domain/EmployeePayroll/EmployeeInvoice.cs#L200-L205) | rozpor | → R6 |
| [RS P120](../analyza-2026-09-11/podklady/cleansia_ramcova_smlouva.txt) | 7.4 | „Vyúčtování probíhá vždy k [doplnit, např. 15. a k poslednímu] dni příslušného měsíce" | Automatická období běží měsíčně (`AddMonths(1)`); splatnost faktury určuje `CalculateDueDate` z konfigurace, nikoli tři pracovní dny | [PayPeriodBackgroundService.cs:212-215](../../src/Cleansia.Core.AppServices/Services/PayPeriodBackgroundService.cs#L212-L215); [EmployeeInvoice.cs:372-376](../../src/Cleansia.Core.Domain/EmployeePayroll/EmployeeInvoice.cs#L372-L376) | částečně | text |
| [RS P122](../analyza-2026-09-11/podklady/cleansia_ramcova_smlouva.txt) | 7.5 | „(a) Zhotovitel je povinen tuto skutečnost neprodleně potvrdit v Aplikaci" | `MarkCashCollected` zapíše `CashCollectedAt` bez částky; `OrderEmployeePay` má `BasePay`, `BonusPay` a `DeductionPay`, žádný hotovostní člen | [Order.cs:50-52](../../src/Cleansia.Core.Domain/Orders/Order.cs#L50-L52); [OrderEmployeePay.cs:38-58](../../src/Cleansia.Core.Domain/EmployeePayroll/OrderEmployeePay.cs#L38-L58) | částečně | → R6 |
| [RS P124](../analyza-2026-09-11/podklady/cleansia_ramcova_smlouva.txt) | 7.6 | „Je-li Zhotovitel nebo se teprve stane plátcem DPH, je povinen tuto skutečnost neprodleně oznámit" | `cleanersAreVatPayers = false` natvrdo; DIČ úklidníka se na faktuře nepoužije | [FileExtensions.cs:119](../../src/Cleansia.Core.AppServices/Extensions/FileExtensions.cs#L119); [FileExtensions.cs:132](../../src/Cleansia.Core.AppServices/Extensions/FileExtensions.cs#L132) | rozpor | → R16 |
| [RS P132](../analyza-2026-09-11/podklady/cleansia_ramcova_smlouva.txt) | 8.2 | „Veškerá komunikace … musí probíhat výhradně prostřednictvím rozhraní Aplikace" | Kanál úklidník–zákazník neexistuje; každý z `AssignedEmployees` vidí telefon zákazníka, zákazník jen `FirstName` | [OrderAccessService.cs:83-94](../../src/Cleansia.Core.AppServices/Authentication/OrderAccessService.cs#L83-L94); [OrderMappers.cs:375-376](../../src/Cleansia.Core.AppServices/Mappers/OrderMappers.cs#L375-L376) | rozpor | text |
| [RS P146](../analyza-2026-09-11/podklady/cleansia_ramcova_smlouva.txt) | 9.3 | „(minimálně jméno, adresa výkonu zakázky a kontaktní telefon)" | Přiřazený dostává e-mail i `AccessInstructions` bez odhalovacího kroku; první neprázdné čtení instrukcí se audituje | [OrderMappers.cs:248-274](../../src/Cleansia.Core.AppServices/Mappers/OrderMappers.cs#L248-L274); [GetOrderDetails.cs:159-171](../../src/Cleansia.Core.AppServices/Features/Orders/GetOrderDetails.cs#L159-L171) | částečně | → R8 |
| [RS P148](../analyza-2026-09-11/podklady/cleansia_ramcova_smlouva.txt) | 9.4 | „Bez platně podepsané DPA není Operátor oprávněn zpřístupnit Zhotoviteli osobní údaje Zákazníků" | Dokument publika `employee` není seedován; `ApproveEmployee` souhlasy nekontroluje | [LegalSeedResource.cs:28](../../src/Cleansia.Infra.Database/Seed/Legal/LegalSeedResource.cs#L28); [ApproveEmployee.cs:19-31](../../src/Cleansia.Core.AppServices/Features/Employees/ApproveEmployee.cs#L19-L31) | rozpor | → R8 |
| [RS P160](../analyza-2026-09-11/podklady/cleansia_ramcova_smlouva.txt) | 10.3 | „opakující se (minimálně dvakrát v období 90 dnů) oprávněné reklamace kvality" | `ComplaintsCount` nemá zvyšující zapisovač; `SubmitOrderReview` předává stávající hodnotu | [SubmitOrderReview.cs:227](../../src/Cleansia.Core.AppServices/Features/Orders/SubmitOrderReview.cs#L227); [Employee.cs:59](../../src/Cleansia.Core.Domain/Users/Employee.cs#L59) | rozpor | provoz |
| [RS P164](../analyza-2026-09-11/podklady/cleansia_ramcova_smlouva.txt) | 10.5 | „omezit nebo trvale zablokovat přístup Zhotovitele k novým zakázkám" | Jen `Approve`/`Reject`; `ContractStatus.Terminated` bez zapisovače; zamítnutí uvolní sedadla jen u budoucích `Confirmed` | [Employee.cs:324-350](../../src/Cleansia.Core.Domain/Users/Employee.cs#L324-L350); [RejectEmployee.cs:113-116](../../src/Cleansia.Core.AppServices/Features/Employees/RejectEmployee.cs#L113-L116) | částečně | provoz |
| [RS P200](../analyza-2026-09-11/podklady/cleansia_ramcova_smlouva.txt) | 13.7 | „Zhotovitel v rámci registračního procesu v Aplikaci elektronicky potvrdí souhlas s jejím zněním" | `RegisterEmployee` zapisuje souhlasy bez dokumentu (`null`); `TermsAccepted` se nevyžaduje | [RegisterEmployee.cs:121-127](../../src/Cleansia.Core.AppServices/Features/Auth/RegisterEmployee.cs#L121-L127); [RegisterEmployee.cs:68-70](../../src/Cleansia.Core.AppServices/Features/Auth/RegisterEmployee.cs#L68-L70) | rozpor | → R3 |
| [RS P209](../analyza-2026-09-11/podklady/cleansia_ramcova_smlouva.txt) | Přílohy | „NUTNO DODAT před první zakázkou" | `LegalDocumentAudience.Employee` deklarován; žádný text pro úklidníka ve stromu | [LegalDocumentAudience.cs:13](../../src/Cleansia.Core.Domain/Legal/LegalDocumentAudience.cs#L13) | rozpor | → R8 |

### 1.4 Kodex

Shoda: [Kodex P071](../analyza-2026-09-11/podklady/cleansia_kodex_chovani.txt), [Kodex P115](../analyza-2026-09-11/podklady/cleansia_kodex_chovani.txt)

Mimo aplikaci (provozní závazek, nic v kódu): P016–P023, P029–P032, P038–P058, P060, P063, P065–P070, P072–P076, P082–P091, P104–P114, P118–P128, P132–P134, P143–P148, P158–P202

| Pxxx | čl. | citát | kód | citace | třída | řešení |
|---|---|---|---|---|---|---|
| [Kodex P034](../analyza-2026-09-11/podklady/cleansia_kodex_chovani.txt) | 2.3 | „oznámit Operátorovi prostřednictvím Aplikace a vyžádat doplnění" | Vůně ani prostředky nemají pole na objednávce a doplnění ceny nemá zápis; `ReportOrderIssue.Handler` nikoho neupozorní | [ReportOrderIssue.cs:42-44](../../src/Cleansia.Core.AppServices/Features/Orders/ReportOrderIssue.cs#L42-L44) | rozpor | → R14 |
| [Kodex P059](../analyza-2026-09-11/podklady/cleansia_kodex_chovani.txt) | 3.4 | „Potvrdí ukončení zakázky v Aplikaci — s fotografiemi výsledku, pokud to zakázka vyžaduje" | Dokončení vyžaduje ≥ 1 fotografii `After` vždy (`HasAfterPhotosAsync`) | [CompleteOrder.cs:183-188](../../src/Cleansia.Core.AppServices/Features/Orders/CompleteOrder.cs#L183-L188) | částečně | → R7 |
| [Kodex P064](../analyza-2026-09-11/podklady/cleansia_kodex_chovani.txt) | III | „Nepřijímejte spropitné ani platby mimo Aplikaci" | Hotovost přebírá přiřazený úklidník ve stavu `InProgress` (`OrderIsInProgressAsync`); `CashCollectedAt` je bez částky a bez započtení proti odměně | [MarkCashCollected.cs:57-72](../../src/Cleansia.Core.AppServices/Features/Orders/MarkCashCollected.cs#L57-L72); [Order.cs:50-52](../../src/Cleansia.Core.Domain/Orders/Order.cs#L50-L52) | rozpor | → R6 |
| [Kodex P100](../analyza-2026-09-11/podklady/cleansia_kodex_chovani.txt) | 5.1 | „nad 30 minut je Operátor oprávněn zakázku přesunout na jiného Zhotovitele" | Zpoždění přiřazeného se nedetekuje; sweep ruší 30 min (`GraceMinutes`) po začátku jen zakázku bez posádky ve stavech `NeverStarted` | [CancelUnfilledOrders.cs:63](../../src/Cleansia.Core.AppServices/Features/Orders/CancelUnfilledOrders.cs#L63); [CancelUnfilledOrders.cs:85](../../src/Cleansia.Core.AppServices/Features/Orders/CancelUnfilledOrders.cs#L85) | částečně | → R10 |
| [Kodex P140](../analyza-2026-09-11/podklady/cleansia_kodex_chovani.txt) | 7.2 | „dočasně pozastavit přístup Zhotovitele k novým zakázkám až na 14 dnů" | `ContractStatus` má `Pending`, `Active`, `Terminated`, `Approved`, `Rejected`; pozastavení neexistuje | [ContractStatus.cs:8-12](../../src/Cleansia.Core.Domain/Enums/ContractStatus.cs#L8-L12) | rozpor | provoz |
| [Kodex P205](../analyza-2026-09-11/podklady/cleansia_kodex_chovani.txt) | VIII | „Tento Kodex je platný po dobu 12 měsíců od data podpisu" | Dokument publika `Employee` deklarován, žádná složka seedu | [LegalDocumentAudience.cs:13](../../src/Cleansia.Core.Domain/Legal/LegalDocumentAudience.cs#L13) | rozpor | → R3 |

### 1.5 BOZP

Shoda: žádný odstavec nemá nosič v kódu

Mimo aplikaci (provozní závazek, nic v kódu): P013–P030, P035–P045, P050–P074, P076–P098, P101–P109, P113–P116, P118–P124, P131–P169

| Pxxx | čl. | citát | kód | citace | třída | řešení |
|---|---|---|---|---|---|---|
| [BOZP P032](../analyza-2026-09-11/podklady/cleansia_bozp.txt) | 1.4 | „O každém incidentu neprodleně informujte Operátora" | `ReportOrderIssue.Handler` má jen `IOrderRepository` a `IOrderAccessService`, žádného notifikátora | [ReportOrderIssue.cs:42-44](../../src/Cleansia.Core.AppServices/Features/Orders/ReportOrderIssue.cs#L42-L44) | rozpor | → R10 |
| [BOZP P047](../analyza-2026-09-11/podklady/cleansia_bozp.txt) | 2.3 | „odmítnout jeho použití, informovat Zákazníka a tuto skutečnost zaznamenat" | Odmítnutí spotřebiče se nezaznamenává; jediný volný text je `Content` poznámky a `Description` hlášení, obojí bez adresáta | [AddOrderNote.cs:37](../../src/Cleansia.Core.AppServices/Features/Orders/AddOrderNote.cs#L37); [ReportOrderIssue.cs:34-38](../../src/Cleansia.Core.AppServices/Features/Orders/ReportOrderIssue.cs#L34-L38) | rozpor | → R10 |
| [BOZP P099](../analyza-2026-09-11/podklady/cleansia_bozp.txt) | 5.3 | „informovat Operátora neprodleně prostřednictvím Aplikace nebo telefonu" | Devět administrátorských událostí od `OrderNew` po `CompanyArchived`; hlášený incident mezi nimi není | [AdminEventCatalog.cs:19-39](../../src/Cleansia.Core.AppServices/Features/AdminNotifications/AdminEventCatalog.cs#L19-L39) | rozpor | → R10 |
| [BOZP P110](../analyza-2026-09-11/podklady/cleansia_bozp.txt) | 5.5 | „Operátor je povinen uchovávat záznamy o úrazech osob pracujících prostřednictvím platformy" | Záznam o úrazu jako entita neexistuje; `EmployeeDocument` nemá platnost a typy jdou od `IdentityCard` po `Other` | [EmployeeDocument.cs:10-40](../../src/Cleansia.Core.Domain/Documents/EmployeeDocument.cs#L10-L40); [DocumentType.cs:8-17](../../src/Cleansia.Core.Domain/Enums/DocumentType.cs#L8-L17) | rozpor | → R10 |
| [BOZP P117](../analyza-2026-09-11/podklady/cleansia_bozp.txt) | 6.3 | „zdokumentovat … preexistující poškození viditelná pouhým okem" | `PhotoType.Before` deklarován, nevyžadován nikde; `StartOrder` fotografie nekontroluje | [PhotoType.cs:8](../../src/Cleansia.Core.Domain/Enums/PhotoType.cs#L8); [StartOrder.cs:43-63](../../src/Cleansia.Core.AppServices/Features/Orders/StartOrder.cs#L43-L63) | rozpor | → R7 |

### 1.6 PP

Shoda: [PP P038](../analyza-2026-09-11/podklady/cleansia_privacy_policy.txt), [PP P043](../analyza-2026-09-11/podklady/cleansia_privacy_policy.txt), [PP P053](../analyza-2026-09-11/podklady/cleansia_privacy_policy.txt), [PP P088](../analyza-2026-09-11/podklady/cleansia_privacy_policy.txt), [PP P107](../analyza-2026-09-11/podklady/cleansia_privacy_policy.txt), [PP P112](../analyza-2026-09-11/podklady/cleansia_privacy_policy.txt), [PP P121](../analyza-2026-09-11/podklady/cleansia_privacy_policy.txt), [PP P129](../analyza-2026-09-11/podklady/cleansia_privacy_policy.txt)

Mimo aplikaci (provozní závazek, nic v kódu): P026, P072, P097–P099, P110–P111, P113–P117, P123–P128, P130–P135, P154

| Pxxx | čl. | citát | kód | citace | třída | řešení |
|---|---|---|---|---|---|---|
| [PP P020](../analyza-2026-09-11/podklady/cleansia_privacy_policy.txt) | 1.1 | „Správcem vašich osobních údajů je společnost Cleansia s.r.o." | Správcem je provozní společnost trhu (`Tenants` jediný řádek `cleansia-cz`, jméno „Cleansia CZ s.r.o.") | [insert_seed_data.sql:63-64](../../sql-scripts/insert_seed_data.sql#L63-L64) | částečně | → R2 |
| [PP P041](../analyza-2026-09-11/podklady/cleansia_privacy_policy.txt) | II | „Po dobu trvání účtu + 3 roky po jeho zrušení (účetní povinnost)" | `RetentionDefaults` zná jiná okna: PII objednávky 2 roky (`DefaultOrderPiiYears`), audit 3 roky; `Anonymize()` přepíše jméno i e-mail ihned při výmazu | [RetentionDefaults.cs:19-28](../../src/Cleansia.Core.AppServices/Features/DataRetention/RetentionDefaults.cs#L19-L28); [User.cs:427-449](../../src/Cleansia.Core.Domain/Users/User.cs#L427-L449) | rozpor | → R9 |
| [PP P046](../analyza-2026-09-11/podklady/cleansia_privacy_policy.txt) | II | „Po dobu zakázky; fakturační adresa 10 let (zákon č. 563/1991 Sb.)" | `DefaultOrderPiiYears` je 2 roky; `AnonymizeCustomerAddress` vytváří kopii adresy pro objednávku, původní sdílená adresa zůstává | [RetentionDefaults.cs:22](../../src/Cleansia.Core.AppServices/Features/DataRetention/RetentionDefaults.cs#L22); [DataRetentionBackgroundService.cs:193-216](../../src/Cleansia.Core.AppServices/Features/DataRetention/DataRetentionBackgroundService.cs#L193-L216) | rozpor | → R9 |
| [PP P048](../analyza-2026-09-11/podklady/cleansia_privacy_policy.txt) | II | „typ karty, poslední 4 číslice, stav transakce" | Objednávka ukládá stav a Stripe ID; typ karty ani poslední čtyři číslice nemá v doménovém modelu | [Order.cs:44](../../src/Cleansia.Core.Domain/Orders/Order.cs#L44); [Order.cs:153-155](../../src/Cleansia.Core.Domain/Orders/Order.cs#L153-L155); rg -e Last4 -e CardBrand -e CardType -e LastFour src/Cleansia.Core.Domain → 0 | částečně | → R9 |
| [PP P056](../analyza-2026-09-11/podklady/cleansia_privacy_policy.txt) | II | „3 roky od poslední aktivity" | Auditní řádky mizí podle vlastního stáří (`CustomerAuditRetentionYears`), nikoli podle poslední aktivity účtu | [DataRetentionBackgroundService.cs:345-351](../../src/Cleansia.Core.AppServices/Features/DataRetention/DataRetentionBackgroundService.cs#L345-L351) | částečně | → R9 |
| [PP P061](../analyza-2026-09-11/podklady/cleansia_privacy_policy.txt) | II | „12 měsíců" | Telemetrie `retentionInDays` 90 dní v PRO a 30 v DEV; dvanáctiměsíční okno technických dat nikde | [appInsights.bicep:85](../../deploy/bicep/modules/appInsights.bicep#L85) | rozpor | → R9 |
| [PP P063](../analyza-2026-09-11/podklady/cleansia_privacy_policy.txt) | II | „poloha zařízení (pouze při aktivním používání Aplikace)" | Zákaznická aplikace posílá souřadnice zařízení v `AddSavedAddressCommand`; `Latitude` a `Longitude` jsou sloupce adresy a zůstanou na ní | [Address.cs:24-25](../../src/Cleansia.Core.Domain/Users/Address.cs#L24-L25); [UserAddress.kt:82-90](../../src/cleansia_android/customer-app/src/main/java/cz/cleansia/customer/core/data/UserAddress.kt#L82-L90) | rozpor | → R9 |
| [PP P068](../analyza-2026-09-11/podklady/cleansia_privacy_policy.txt) | II | „e-mail pro zasílání novinek a nabídek (jen s explicitním souhlasem)" | `MarketingEmails` neřídí odesílatele; `EmailType.PromoCode` je jednorázový kód, newsletter mezi typy není | [EmailType.cs:8-28](../../src/Cleansia.Core.Domain/Enums/EmailType.cs#L8-L28); rg MarketingEmails src/Cleansia.Core.AppServices → 0 | rozpor | → R9 |
| [PP P076](../analyza-2026-09-11/podklady/cleansia_privacy_policy.txt) | 3.1 | „Cookie Policy dostupné na [doplnit URL]" | Web nemá stránku cookie policy; lišta ukládá volbu do `localStorage` klíče `cleansia-customer-cookie-consent` | [app.html:16](../../src/Cleansia.App/apps/cleansia.app/src/app/app.html#L16) | rozpor | → R9 |
| [PP P079](../analyza-2026-09-11/podklady/cleansia_privacy_policy.txt) | 3.2 | „(např. Google Analytics). Aktivují se až po vašem souhlasu" | Google Analytics ani gtag na zákaznickém webu nejsou; Google Fonts se načítají před volbou | [index.html:50](../../src/Cleansia.App/apps/cleansia.app/src/index.html#L50); [index.html:43-49](../../src/Cleansia.App/apps/cleansia.app/src/index.html#L43-L49) | rozpor | → R9 |
| [PP P082](../analyza-2026-09-11/podklady/cleansia_privacy_policy.txt) | 3.3 | „udělit nebo odvolat kdykoli prostřednictvím cookie lišty na webu" | Patička znovu otevře lištu; ta obnoví uložené preference. Slíbená Cookie Policy v právních typech chybí | [customer-footer.component.ts:74-75](../../src/Cleansia.App/apps/cleansia.app/src/app/components/footer/customer-footer.component.ts#L74-L75); [LegalDocumentType.cs:8-16](../../src/Cleansia.Core.Domain/Legal/LegalDocumentType.cs#L8-L16) | částečně | → R9 |
| [PP P086](../analyza-2026-09-11/podklady/cleansia_privacy_policy.txt) | 4.1 | „Zpracování těchto údajů Zhotovitelem se řídí Zpracovatelskou smlouvou (DPA)" | Kontakty se sdílí; schválení nekontroluje DPA a přístup přiřazeného nemá časový konec | [ApproveEmployee.cs:19-31](../../src/Cleansia.Core.AppServices/Features/Employees/ApproveEmployee.cs#L19-L31); [OrderAccessService.cs:83-94](../../src/Cleansia.Core.AppServices/Authentication/OrderAccessService.cs#L83-L94) | částečně | → R8 |
| [PP P140](../analyza-2026-09-11/podklady/cleansia_privacy_policy.txt) | 7.1 | „Při registraci ověřujeme věk zákazníka" | Registrace věk nesbírá; `Register.Command` bez `BirthDate` | [Register.cs:73-89](../../src/Cleansia.Core.AppServices/Features/Auth/Register.cs#L73-L89) | rozpor | → R9 |
| [PP P146](../analyza-2026-09-11/podklady/cleansia_privacy_policy.txt) | 8.1 | „Cleansia nevyužívá plně automatizované rozhodování" | Rezervaci pro oblíbeného úklidníka (`PreferredHoldFraction`) i filtr nástěnky (`PayableTo`) rozhoduje kód bez člověka | [BookingPolicy.cs:217-218](../../src/Cleansia.Core.AppServices/Features/Orders/BookingPolicy.cs#L217-L218); [OrderVisibility.cs:53-59](../../src/Cleansia.Core.Domain/Orders/OrderVisibility.cs#L53-L59) | částečně | → R9 |

### 1.7 CP

Shoda: [CP P021](../analyza-2026-09-11/podklady/cleansia_cookie_policy.txt), [CP P023](../analyza-2026-09-11/podklady/cleansia_cookie_policy.txt), [CP P150](../analyza-2026-09-11/podklady/cleansia_cookie_policy.txt)

Mimo aplikaci (provozní závazek, nic v kódu): P113–P118, P126–P135, P238–P240

| Pxxx | čl. | citát | kód | citace | třída | řešení |
|---|---|---|---|---|---|---|
| [CP P035](../analyza-2026-09-11/podklady/cleansia_cookie_policy.txt) | 2.1 | „cleansia_session" | Web používá `customer_token`, `customer_refresh_token`, `customer_csrf`; `cleansia_session` neexistuje | [app.config.ts:144-153](../../src/Cleansia.App/apps/cleansia.app/src/app/app.config.ts#L144-L153) | rozpor | text |
| [CP P045](../analyza-2026-09-11/podklady/cleansia_cookie_policy.txt) | 2.1 | „cleansia_consent" | Volba je v `localStorage` (status + preferences), ne v cookie s platností 12 měsíců | [cleansia-cookie-consent.component.ts:167-171](../../src/Cleansia.App/libs/shared/components/src/lib/cleansia-cookie-consent/cleansia-cookie-consent.component.ts#L167-L171) | rozpor | → R9 |
| [CP P062](../analyza-2026-09-11/podklady/cleansia_cookie_policy.txt) | 2.2 | „_ga" | Google Analytics, gtag ani Tag Manager na zákaznickém webu nejsou | [index.html:50](../../src/Cleansia.App/apps/cleansia.app/src/index.html#L50) | rozpor | → R9 |
| [CP P089](../analyza-2026-09-11/podklady/cleansia_cookie_policy.txt) | 2.3 | „_fbp" | Marketingové skripty ve stromu nejsou; lišta přesto nabízí kategorii `marketing` | [cleansia-cookie-consent.component.ts:88-109](../../src/Cleansia.App/libs/shared/components/src/lib/cleansia-cookie-consent/cleansia-cookie-consent.component.ts#L88-L109) | rozpor | → R9 |
| [CP P152](../analyza-2026-09-11/podklady/cleansia_cookie_policy.txt) | 8 | „4) Souhlas zaznamenavan s timestampem a verzi" | Anonymní návštěvník nemá serverový záznam; přihlášenému se mapuje `analytics → DataProcessing`, `marketing → MarketingEmails` | [consent-sync.service.ts:36](../../src/Cleansia.App/libs/core/customer-services/src/lib/services/consent-sync.service.ts#L36); [consent-sync.service.ts:38-41](../../src/Cleansia.App/libs/core/customer-services/src/lib/services/consent-sync.service.ts#L38-L41) | rozpor | → R9 |
| [CP P163](../analyza-2026-09-11/podklady/cleansia_cookie_policy.txt) | 9.3 | „Souhlas musí být uložen jako nezbytná cookie cleansia_consent (viz sekce 2.1)" | Souhlas jde do řádku s `ConsentType`, `IsGranted` a `GrantedAt` (jeden na typ, přepisovaný), nikoli do cookie | [UserConsent.cs:15-38](../../src/Cleansia.Core.Domain/Users/UserConsent.cs#L15-L38) | částečně | → R9 |
| [CP P168](../analyza-2026-09-11/podklady/cleansia_cookie_policy.txt) | 9.4 | „respektovat Global Privacy Control (GPC) signál ze zařízení zákazníka" | GPC signál kód nečte; lišta ukládá jen `pending`/`accepted`/`declined`/`custom` | [cleansia-cookie-consent.component.ts:28](../../src/Cleansia.App/libs/shared/components/src/lib/cleansia-cookie-consent/cleansia-cookie-consent.component.ts#L28) | rozpor | → R9 |

### 1.8 DPA

Shoda: [DPA P066](../analyza-2026-09-11/podklady/cleansia_dpa.txt), [DPA P084](../analyza-2026-09-11/podklady/cleansia_dpa.txt), [DPA P088](../analyza-2026-09-11/podklady/cleansia_dpa.txt), [DPA P092](../analyza-2026-09-11/podklady/cleansia_dpa.txt)

Mimo aplikaci (provozní závazek, nic v kódu): P071–P073, P105–P113, P123–P127, P136–P138, P144, P146, P150–P154, P164–P165, P167–P168, P178–P180, P198

| Pxxx | čl. | citát | kód | citace | třída | řešení |
|---|---|---|---|---|---|---|
| [DPA P017](../analyza-2026-09-11/podklady/cleansia_dpa.txt) | úvod | „Zpracování osobních údajů bez platné DPA je porušením GDPR" | Podepsaná DPA není podmínkou schválení úklidníka ani přístupu k objednávce | [ApproveEmployee.cs:19-31](../../src/Cleansia.Core.AppServices/Features/Employees/ApproveEmployee.cs#L19-L31); [OrderAccessService.cs:88-94](../../src/Cleansia.Core.AppServices/Authentication/OrderAccessService.cs#L88-L94) | rozpor | → R8 |
| [DPA P070](../analyza-2026-09-11/podklady/cleansia_dpa.txt) | 2.3 | „výhradně za účelem splnění konkrétní úklidové zakázky, která mu byla přidělena prostřednictvím Aplikace" | Přístup drží, dokud existuje sedadlo (`AssignedEmployees.Any`), i po dokončení; token partnerského webu 1 440 min | [OrderVisibility.cs:53-55](../../src/Cleansia.Core.Domain/Orders/OrderVisibility.cs#L53-L55); [appsettings.json:20](../../src/Cleansia.Web.Partner/appsettings.json#L20) | rozpor | → R8 |
| [DPA P086](../analyza-2026-09-11/podklady/cleansia_dpa.txt) | III | „Po dobu zakázky + 24 h" | Žádný časový člen v přístupu; čte se jen sedadlo v `AssignedEmployees` | [OrderAccessService.cs:94](../../src/Cleansia.Core.AppServices/Authentication/OrderAccessService.cs#L94) | rozpor | → R8 |
| [DPA P094](../analyza-2026-09-11/podklady/cleansia_dpa.txt) | III | „Po dobu zakázky — po skončení neprodleně vymazat" | První neprázdné `AccessInstructions` se audituje; přístup přes `AssignedEmployees` nemá odhalovací krok ani časový konec | [GetOrderDetails.cs:159-171](../../src/Cleansia.Core.AppServices/Features/Orders/GetOrderDetails.cs#L159-L171); [OrderAccessService.cs:88-94](../../src/Cleansia.Core.AppServices/Authentication/OrderAccessService.cs#L88-L94) | rozpor | → R8 |
| [DPA P098](../analyza-2026-09-11/podklady/cleansia_dpa.txt) | III | „Max. 30 dní od ukončení zakázky" | `DefaultOrderPhotosDays` 7 po `CompletedAt`; spor mimo `Resolved`/`Closed` drží fotografie i déle než 30 dní | [RetentionDefaults.cs:29](../../src/Cleansia.Core.AppServices/Features/DataRetention/RetentionDefaults.cs#L29); [OrderPhotoRepository.cs:37-43](../../src/Cleansia.Infra.Database/Repositories/OrderPhotoRepository.cs#L37-L43) | částečně | → R7 |
| [DPA P115](../analyza-2026-09-11/podklady/cleansia_dpa.txt) | 4.3 | „Správce není oprávněn prostřednictvím Aplikace sledovat polohu Zhotovitele mimo dobu aktivní zakázky" | Poloha úklidníka se na server neposílá; geokodér volá Mapbox ze zařízení | [ReverseGeocodingService.kt:28](../../src/cleansia_android/core/src/main/java/cz/cleansia/core/location/ReverseGeocodingService.kt#L28) | částečně | → R9 |
| [DPA P117](../analyza-2026-09-11/podklady/cleansia_dpa.txt) | 4.4 | „nesmí bez předchozího písemného souhlasu Správce pověřit zpracováním osobních údajů Zákazníků dalšího zpracovatele" | Každé místo v posádce je samostatný účet s vlastním přístupem; substituce v datech neexistuje | [WorkContractAcceptance.cs:31](../../src/Cleansia.Core.Domain/Contracts/WorkContractAcceptance.cs#L31) | částečně | → R13 |
| [DPA P134](../analyza-2026-09-11/podklady/cleansia_dpa.txt) | 4.7 | „doručí ho Správci nejpozději do 14 dnů od ukončení spolupráce" | Potvrzení výmazu ze zařízení úklidníka aplikace nesbírá; mezi typy dokladů od `IdentityCard` po `Other` takový není | [DocumentType.cs:8-17](../../src/Cleansia.Core.Domain/Enums/DocumentType.cs#L8-L17) | rozpor | provoz |
| [DPA P140](../analyza-2026-09-11/podklady/cleansia_dpa.txt) | 4.10 | „zajistí lidský dohled nad automatizovanými rozhodnutími … na přístup Zpracovatele k zakázkám" | Nástěnka filtruje měnou úklidníka bez člověka; osm push klíčů je nevypnutelných (`GetCategoryFor` → `null`) | [OrderVisibility.cs:53-59](../../src/Cleansia.Core.Domain/Orders/OrderVisibility.cs#L53-L59); [NotificationEventCatalog.cs:226-251](../../src/Cleansia.Core.Domain/Notifications/NotificationEventCatalog.cs#L226-L251) | částečně | → R8 |
| [DPA P166](../analyza-2026-09-11/podklady/cleansia_dpa.txt) | 7.2 | „Správce je oprávněn v takovém případě vzdáleně odhlásit zařízení z Aplikace" | Zařízení se `Deactivate` a `RevokeByDeviceAsync` odvolá jeho tokeny; úkon zadává sám úklidník, administrátorský povrch chybí | [RevokeDevice.cs:44-52](../../src/Cleansia.Core.AppServices/Features/Devices/RevokeDevice.cs#L44-L52) | částečně | provoz |

### 1.9 GDPR

Shoda: [GDPR P020](../analyza-2026-09-11/podklady/cleansia_gdpr_souhlasy.txt), [GDPR P061](../analyza-2026-09-11/podklady/cleansia_gdpr_souhlasy.txt), [GDPR P069](../analyza-2026-09-11/podklady/cleansia_gdpr_souhlasy.txt), [GDPR P106](../analyza-2026-09-11/podklady/cleansia_gdpr_souhlasy.txt), [GDPR P107](../analyza-2026-09-11/podklady/cleansia_gdpr_souhlasy.txt)

Mimo aplikaci (provozní závazek, nic v kódu): P188

| Pxxx | čl. | citát | kód | citace | třída | řešení |
|---|---|---|---|---|---|---|
| [GDPR P036](../analyza-2026-09-11/podklady/cleansia_gdpr_souhlasy.txt) | I | „Po dobu zakázky + 24 h (adresa)" | Adresa objednávky se anonymizuje po 2 letech (`DefaultOrderPiiYears`), jen u historie `OrderStatus.Completed` | [RetentionDefaults.cs:22](../../src/Cleansia.Core.AppServices/Features/DataRetention/RetentionDefaults.cs#L22); [DataRetentionBackgroundService.cs:181-183](../../src/Cleansia.Core.AppServices/Features/DataRetention/DataRetentionBackgroundService.cs#L181-L183) | rozpor | → R8 |
| [GDPR P050](../analyza-2026-09-11/podklady/cleansia_gdpr_souhlasy.txt) | I | „Souhlas čl. 6(1)(a) GDPR" | Push řídí `UserNotificationPreferences` (12 kategorií, vše `true` kromě `Promo`), ne souhlas | [UserNotificationPreferences.cs:18-34](../../src/Cleansia.Core.Domain/Notifications/UserNotificationPreferences.cs#L18-L34) | rozpor | → R9 |
| [GDPR P063](../analyza-2026-09-11/podklady/cleansia_gdpr_souhlasy.txt) | II | „Souhlasím se zpracováním svých osobních údajů za účelem realizace úklidových zakázek" | Registrace zapisuje dva souhlasy (`TermsOfService`, `PrivacyPolicy`); třetí checkbox neexistuje | [Register.cs:146-155](../../src/Cleansia.Core.AppServices/Features/Auth/Register.cs#L146-L155) | rozpor | text |
| [GDPR P086](../analyza-2026-09-11/podklady/cleansia_gdpr_souhlasy.txt) | 5.1 | „Cleansia musí implementovat double opt-in" | `TryGrantAsync` zapisuje souhlas přímo; pro `MarketingEmails` neposílá ověřovací e-mail | [GrantConsent.cs:39-54](../../src/Cleansia.Core.AppServices/Features/Gdpr/GrantConsent.cs#L39-L54); [ConsentType.cs:10](../../src/Cleansia.Core.Domain/Enums/ConsentType.cs#L10) | rozpor | → R9 |
| [GDPR P090](../analyza-2026-09-11/podklady/cleansia_gdpr_souhlasy.txt) | 5.3 | „V sekci Nastavení → Oznámení a soukromí musí být zákazníkovi dostupné přepínače (toggles)" | Zákazník mění 11 přepínačů od `OrderUpdates` po `RecurringScheduled`; partnerští hostitelé takový příkaz ani obrazovku nemají | [UpdateNotificationPreferences.cs:21-32](../../src/Cleansia.Core.AppServices/Features/Notifications/UpdateNotificationPreferences.cs#L21-L32) | částečně | → R18 |
| [GDPR P096](../analyza-2026-09-11/podklady/cleansia_gdpr_souhlasy.txt) | 5.4 | „Pro zákazníky mladší 16 let je vyžadován souhlas zákonného zástupce" | Věk zákazníka nemá dolní hranici, jen strop 120 let; povinných 18 let (`BeReasonableAge`) má úklidník i administrátor | [UpdateCurrentUser.cs:108-112](../../src/Cleansia.Core.AppServices/Features/Users/UpdateCurrentUser.cs#L108-L112); [CreateAdminUser.cs:72](../../src/Cleansia.Core.AppServices/Features/AdminUsers/CreateAdminUser.cs#L72) | rozpor | → R9 |
| [GDPR P108](../analyza-2026-09-11/podklady/cleansia_gdpr_souhlasy.txt) | 6.2 | „způsob udělení (checkbox, API call, OS dialog)" | `UserConsent` nese jen `IpAddress` a `UserAgent`, bez klienta a id zařízení | [UserConsent.cs:23-27](../../src/Cleansia.Core.Domain/Users/UserConsent.cs#L23-L27) | částečně | → R3 |
| [GDPR P110](../analyza-2026-09-11/podklady/cleansia_gdpr_souhlasy.txt) | 6.2 | „hash nebo plný text souhlasu platný v okamžiku udělení (SHA-256)" | Řádek souhlasu odkazuje `LegalDocumentId` a `DocumentVersion`; hash je na `LegalDocumentText.ContentHash` | [UserConsent.cs:15-38](../../src/Cleansia.Core.Domain/Users/UserConsent.cs#L15-L38); [LegalDocumentText.cs:20-33](../../src/Cleansia.Core.Domain/Legal/LegalDocumentText.cs#L20-L33) | částečně | text |
| [GDPR P112](../analyza-2026-09-11/podklady/cleansia_gdpr_souhlasy.txt) | 6.3 | „immutable záznamy — žádné UPDATE operace, pouze INSERT" | Jeden řádek na (uživatel, typ), unikátní index `UserId + ConsentType`; `AcceptVersion` řádek přepíše | [UserConsentEntityConfiguration.cs:42-43](../../src/Cleansia.Infra.Database/EntityConfigurations/UserConsentEntityConfiguration.cs#L42-L43); [UserConsent.cs:80-85](../../src/Cleansia.Core.Domain/Users/UserConsent.cs#L80-L85) | rozpor | → R3 |
| [GDPR P140](../analyza-2026-09-11/podklady/cleansia_gdpr_souhlasy.txt) | 7.4 | „Záznamy v Consent Logu musí být anonymizovány (nikoliv smazány" | Odvolané souhlasy se po třech letech mažou: `CleanWithdrawnConsentsAsync` je odstraní (`RemoveRange`), neanonymizuje | [DataRetentionBackgroundService.cs:226-240](../../src/Cleansia.Core.AppServices/Features/DataRetention/DataRetentionBackgroundService.cs#L226-L240) | rozpor | → R9 |
| [GDPR P150](../analyza-2026-09-11/podklady/cleansia_gdpr_souhlasy.txt) | VIII | „Registrační formulář má 2 povinné + 2 volitelné checkboxy (žádný předvyplněný)" | Registrace nese jediné `TermsAccepted`; volitelné souhlasy se udělují až v aplikaci (`GrantConsent`) | [Register.cs:55-58](../../src/Cleansia.Core.AppServices/Features/Auth/Register.cs#L55-L58); [GdprController.cs:45-46](../../src/Cleansia.Web.Customer/Controllers/GdprController.cs#L45-L46) | částečně | → R3 |

### 1.10 SS

Shoda: [SS P020](../analyza-2026-09-11/podklady/cleansia_spolecenska_smlouva.txt), [SS P024](../analyza-2026-09-11/podklady/cleansia_spolecenska_smlouva.txt)

Mimo aplikaci (provozní závazek, nic v kódu): P005–P014, P016–P017, P022–P023, P028–P055, P059–P091, P095–P113, P117–P131, P151

| Pxxx | čl. | citát | kód | citace | třída | řešení |
|---|---|---|---|---|---|---|
| [SS P015](../analyza-2026-09-11/podklady/cleansia_spolecenska_smlouva.txt) | 1.1 | „Firma společnosti je: Cleansia s.r.o." | Tři názvy: `Tenants` „Cleansia CZ s.r.o.", `CompanyInfo` „Cleansia s.r.o.", `FooterText` e-mailů „© Cleansia s.r.o." | [insert_seed_data.sql:64](../../sql-scripts/insert_seed_data.sql#L64); [EmailService.cs:713](../../src/Cleansia.Core.AppServices/Services/EmailService.cs#L713) | částečně | → R2 |
| [SS P021](../analyza-2026-09-11/podklady/cleansia_spolecenska_smlouva.txt) | 1.3 | „Poskytování úklidových služeb" | Seedovaná smlouva o dílo: `Cleansia` „aplikaci provozuje a není smluvní stranou"; seedované podmínky říkají opak | [cs.md:7](../../src/Cleansia.Infra.Database/Seed/Legal/customer/work-contract/any/2026-09-20/cs.md#L7); [cs.md:15](../../src/Cleansia.Infra.Database/Seed/Legal/customer/terms-of-service/any/2026-09-14/cs.md#L15) | částečně | → R1 |

## 2. Co v dokumentech chybí

| co kód dělá | citace | který dokument to má nést | → Rn |
|---|---|---|---|
| Dobrovolné převzetí místa zapisuje přijetí přes `StageAsync`; administrátorské `AddAssignedEmployee` ani založení objednávky přijetí nezapisuje | [TakeOrder.cs:351-357](../../src/Cleansia.Core.AppServices/Features/Orders/TakeOrder.cs#L351-L357); [AdminReassignOrder.cs:123](../../src/Cleansia.Core.AppServices/Features/Orders/AdminReassignOrder.cs#L123) | VOP čl. III, RS čl. II | → R11 |
| Kredit je platidlo per měna, `TotalPrice` nesnižuje, hradí nejvýše 70 % objednávky (`MaxCreditShareOfOrder`) | [BookingPolicy.cs:98](../../src/Cleansia.Core.AppServices/Features/Orders/BookingPolicy.cs#L98) | VOP čl. IV | → R4 |
| Posádka = ⌈odhad minut / 120⌉ (`MinutesPerEmployee`), rezervní místa `SpareSeatsPerOrder` 0 | [OrderDuration.cs:27](../../src/Cleansia.Core.Domain/Orders/OrderDuration.cs#L27); [BookingPolicy.cs:136](../../src/Cleansia.Core.AppServices/Features/Orders/BookingPolicy.cs#L136) | VOP čl. III | → R13 |
| Čtyři role administrátora `Administrator / Manager / Support / Accountant` s oddělenými oprávněními | [AdminRole.cs:14-17](../../src/Cleansia.Core.Domain/Enums/AdminRole.cs#L14-L17) | PP čl. V, DPA čl. IV | → R10 |
| Životní cyklus společnosti: útlum, deaktivace, zmrazení, archiv (`CompanyLifecycleState`) | [CompanyLifecycleState.cs:10-17](../../src/Cleansia.Core.Domain/Tenancy/CompanyLifecycleState.cs#L10-L17) | VOP čl. X, RS čl. X | → R2 |
| 13 nastavení: 12 číselných oken a `DefaultExpiredCodesEnabled`; 14 úloh přes `RunSafeAsync`, včetně mrtvých hostovských tokenů | [RetentionDefaults.cs:19-31](../../src/Cleansia.Core.AppServices/Features/DataRetention/RetentionDefaults.cs#L19-L31); [DataRetentionBackgroundService.cs:69-82](../../src/Cleansia.Core.AppServices/Features/DataRetention/DataRetentionBackgroundService.cs#L69-L82) | PP čl. II | → R9 |
| Dohledané objednávce chargeback založí `Escalated` spor důvodu `Chargeback`, nebo se propojí s otevřeným; zákazník bez oznámení | [HandlePaymentNotification.cs:463-505](../../src/Cleansia.Core.AppServices/Features/Payments/HandlePaymentNotification.cs#L463-L505) | VOP čl. IV, RŘ čl. VI | → R18 |
| Host prokazuje objednávku 256bitovým tokenem; ukládá se hash, přístup vyžaduje neodvolaný a neexpirovaný token k objednávce bez účtu | [GuestOrderAccessToken.cs:55-72](../../src/Cleansia.Core.Domain/Orders/GuestOrderAccessToken.cs#L55-L72); [GuestOrderAccess.cs:36-42](../../src/Cleansia.Core.AppServices/Features/Orders/GuestOrderAccess.cs#L36-L42) | VOP čl. II | → R5 |
| Opakované úklidy: šablona (týdně/dvoutýdně/měsíčně), materializace `HorizonDays` 7 dní dopředu; bez Plus `RecurringTemplateMembershipRequired` | [CreateRecurringBooking.cs:187-197](../../src/Cleansia.Core.AppServices/Features/Bookings/CreateRecurringBooking.cs#L187-L197); [MaterializeRecurringBookings.cs:27](../../src/Cleansia.Core.AppServices/Features/Bookings/MaterializeRecurringBookings.cs#L27) | VOP čl. III | → R4 |
| DEV seed Plus: sleva 5 %, `FreeCancellationWindowHours` 4 h, jeden express měsíčně, `TrialPeriodDays` 0; rozhoduje konfigurace | [insert_seed_data.sql:1800-1808](../../sql-scripts/insert_seed_data.sql#L1800-L1808) | VOP čl. IV | → R4 |
| Doporučení: 150 bodů oběma stranám (`PointsPerSide`), okno 90 dní (`QualifyingWindowDays`) | [ReferralPolicy.cs:14-22](../../src/Cleansia.Core.AppServices/Features/Orders/ReferralPolicy.cs#L14-L22) | VOP čl. IV | → R17 |
| Promokódy: procentní i pevné, limit per uživatel a globální, vázané na měnu | [PromoCodeService.cs:125-134](../../src/Cleansia.Core.AppServices/Services/PromoCodeService.cs#L125-L134); [PromoCodeService.cs:256-275](../../src/Cleansia.Core.AppServices/Services/PromoCodeService.cs#L256-L275) | VOP čl. IV | → R4 |
| DEV seed věrnosti: Bronze/Silver/Gold/Platinum, prahy `LifetimePointsThreshold` 0/500/2000/5000 bodů, slevy 0–12 %; rozhoduje konfigurace | [insert_seed_data.sql:1670-1707](../../sql-scripts/insert_seed_data.sql#L1670-L1707) | VOP čl. IV | → R17 |
| Fotografii objednávky smaže kdokoli z `AssignedEmployees`, v jakémkoli stavu | [DeleteOrderPhoto.cs:56-71](../../src/Cleansia.Core.AppServices/Features/Orders/DeleteOrderPhoto.cs#L56-L71) | VOP čl. VI, RS čl. IX | → R7 |
| Export dat je JSON s 12 sekcemi (`GdprExportDto`), včetně objednávek hosta pod e-mailem účtu | [GdprExportDto.cs:5-18](../../src/Cleansia.Core.AppServices/Features/Gdpr/DTOs/GdprExportDto.cs#L5-L18) | PP čl. VI | → R9 |
| Výmaz blokuje živá objednávka (`GdprDeletionBlockedByOrder`) a u úklidníka otevřená faktura či nevyrovnaná odměna | [GdprDeletionService.cs:147-181](../../src/Cleansia.Core.AppServices/Services/GdprDeletionService.cs#L147-L181) | PP čl. VI | → R9 |
| Mobilní push: 12 kategorií, osm klíčů úklidníka nevypnutelných (`GetCategoryFor` → `null`) | [NotificationEventCatalog.cs:226-251](../../src/Cleansia.Core.Domain/Notifications/NotificationEventCatalog.cs#L226-L251) | GDPR čl. V, RS čl. XIII | → R18 |
| Objednávka patří společnosti trhu adresy (`OperatorTenantId`); operátora má v seedu jen `CZE`, neobsluhovaný trh kód odmítne | [CreateOrder.cs:952-954](../../src/Cleansia.Core.AppServices/Features/Orders/CreateOrder.cs#L952-L954); [insert_seed_data.sql:1003-1064](../../sql-scripts/insert_seed_data.sql#L1003-L1064) | VOP čl. I, PP čl. I | → R19 |

## 3. Rozhodnutí pro schůzku

### R1 — Obchodní model: kdo zákazníkovi poskytuje úklid a kdo komu fakturuje

Dodavatelský model, zprostředkovatelský (společnost fakturuje provizi), nebo hybrid. Doklad zákazníka nese `CompanyInfo` společnosti — [ReceiptService.cs:555-561](../../src/Cleansia.Core.AppServices/Services/ReceiptService.cs#L555-L561); výplatní faktura je samofakturace, dodavatel `CreateSupplierData` — [FileExtensions.cs:40-69](../../src/Cleansia.Core.AppServices/Extensions/FileExtensions.cs#L40-L69). Návrhy: [VOP P022](../analyza-2026-09-11/podklady/Cleansia_VOP.txt), [RS P114](../analyza-2026-09-11/podklady/cleansia_ramcova_smlouva.txt).

| varianta | odstavce návrhů | v kódu |
|---|---|---|
| dodavatelský model | VOP P022, RS P041, RS P114, RŘ P018 | nic |
| zprostředkovatelský model | RS P116 doplnit sazbu | `ReceiptService`, `CompanyInfo`, provizní člen v `PayCalculatorExtensions` |
| hybrid (příkazní smlouva) | VOP P022, RS P039, RS P114 | `ReceiptService`, nový typ dokladu |

**R2 — Kdo je „společnost": jedna s.r.o., nebo jedna na zemi** · rozhodnuto 2026-09-13 (ADR-0061): holding a provozní společnost na každý region; zbývá jeden název místo tří a skutečné IČO/DIČ místo `REPLACE WITH ACTUAL` — [insert_seed_data.sql:1158-1162](../../sql-scripts/insert_seed_data.sql#L1158-L1162).

### R3 — Jaké znění zákazník a úklidník přijímají a jak se prokáže

Finální znění jako datované složky; k tomu: vyžaduje nová verze VOP opětovné přijetí, je RŘ čtvrtým typem, co přijímá úklidník. Kód zná tři typy — [LegalDocumentType.cs:8-16](../../src/Cleansia.Core.Domain/Legal/LegalDocumentType.cs#L8-L16); objednávka projde s jakýmkoli neodvolaným souhlasem bez ohledu na verzi — [CreateOrder.cs:330-334](../../src/Cleansia.Core.AppServices/Features/Orders/CreateOrder.cs#L330-L334). Brána `AssertedOrAlreadyConsentedAsync` se spokojí s tvrzením klienta a uložený souhlas nečte; host žádný řádek souhlasu nemá — [CreateOrder.cs:317-322](../../src/Cleansia.Core.AppServices/Features/Orders/CreateOrder.cs#L317-L322). Návrhy: [VOP P014](../analyza-2026-09-11/podklady/Cleansia_VOP.txt), [RS P200](../analyza-2026-09-11/podklady/cleansia_ramcova_smlouva.txt).

| varianta | odstavce návrhů | v kódu |
|---|---|---|
| finální znění jako nové datované složky | VOP P014, PP P020 | `Seed/Legal/customer/…` |
| RŘ jako čtvrtý typ dokumentu | VOP P105, RŘ P137 | `LegalDocumentType`, seed, routa |
| partnerský dokument s verzí, přijímaný při schválení | RS P200, RS P209, Kodex P205 | `LegalDocumentAudience.Employee`, `RegisterEmployee`, `ApproveEmployee` |

### R4 — Jedna storno, cenová a kreditní politika

VOP převezmou žebříček kódu, nebo kód převezme VOP (50 %/100 %). Kód: `FreeCancellationHours = 24`, `PartialCancellationFeeRate = 0,25`, `LastMinuteCancellationFeeRate = 0,50` — [BookingPolicy.cs:65-80](../../src/Cleansia.Core.AppServices/Features/Orders/BookingPolicy.cs#L65-L80); okno 60 min je nedosažitelné (`IsFirstTimeCustomer`) — [CancellationAssessor.cs:32](../../src/Cleansia.Core.AppServices/Features/Orders/CancellationAssessor.cs#L32). Návrhy: [VOP P069](../analyza-2026-09-11/podklady/Cleansia_VOP.txt), [VOP P034](../analyza-2026-09-11/podklady/Cleansia_VOP.txt).

| varianta | odstavce návrhů | v kódu |
|---|---|---|
| VOP převezmou žebříček kódu, kredit jako bonus bez nároku | VOP P064–P078, P034, P123 | nic |
| kód převezme VOP včetně stavu „zákazník nedostupný" | žádný | `BookingPolicy`, `CancellationAssessor`, nový důvod storna |
| výplata kreditu při zrušení účtu | VOP P034 | `CreditAccount`, nový tok výplaty |

### R5 — Host bez účtu

Host zůstane, nebo bude registrace povinná? Objednávku bez `UserId` zpřístupňuje neexpirovaný, neodvolaný token — [GuestOrderAccess.cs:36-42](../../src/Cleansia.Core.AppServices/Features/Orders/GuestOrderAccess.cs#L36-L42); `CancelGuestOrder` přijímá `AccessToken` — [CancelGuestOrder.cs:18](../../src/Cleansia.Core.AppServices/Features/Orders/CancelGuestOrder.cs#L18). Platnost končí `LifetimeDaysAfterCleaning` 30 dní po termínu úklidu — [GuestOrderAccessToken.cs:67-69](../../src/Cleansia.Core.Domain/Orders/GuestOrderAccessToken.cs#L67-L69). Výmaz odvolává tokeny anonymizovaných hostovských objednávek, živé rezervace ponechává — [GdprDeletionService.cs:406-416](../../src/Cleansia.Core.AppServices/Services/GdprDeletionService.cs#L406-L416). Host spor nepodá, kredit ani body nedostane. Návrhy: [VOP P028](../analyza-2026-09-11/podklady/Cleansia_VOP.txt), [RŘ P020](../analyza-2026-09-11/podklady/cleansia_reklamacni_rad.txt).

| varianta | odstavce návrhů | v kódu |
|---|---|---|
| host zůstane, dokumenty se přizpůsobí | VOP P028, RŘ P020, PP P038 | `CreateDispute` (kanál hosta), retence hostovských objednávek |
| registrace povinná | žádný | `CreateOrder` (`AllowsAnonymousActor`), `GuestOrderAccess`, klienti |

### R6 — Odměna úklidníka, hotovost a spropitné

RS převezme sazby, nebo odměnu nahradí provize? `CalculateAggregatedPay` sčítá `BasePay` + max(0, pokoje−1)·`ExtraPerRoom` + koupelny·`ExtraPerBathroom` + km·`DistanceRatePerKm` a omezuje součet — [PayCalculatorExtensions.cs:30-60](../../src/Cleansia.Core.Domain/Extensions/PayCalculatorExtensions.cs#L30-L60). Výpočet načítá `SelectedServices` i `SelectedPackages` a ukládá odměnu z `CalculateAggregatedPay` — [CalculateOrderPay.cs:119-180](../../src/Cleansia.Core.AppServices/Features/EmployeePayroll/CalculateOrderPay.cs#L119-L180). Hotovost zapisuje `CashCollectedAt` bez částky — [Order.cs:50-52](../../src/Cleansia.Core.Domain/Orders/Order.cs#L50-L52). Návrhy: [RS P114](../analyza-2026-09-11/podklady/cleansia_ramcova_smlouva.txt), [VOP P056](../analyza-2026-09-11/podklady/Cleansia_VOP.txt).

| varianta | odstavce návrhů | v kódu |
|---|---|---|
| RS na sazbový model, hotovost provozně | RS P114, P116, P122, příloha 4 | nic |
| odměna jako provize ze zaplacené ceny | RS P116 doplnit sazbu | `PayCalculatorExtensions`, `EmployeePayConfig`, `CalculateOrderPay` |
| spropitné postavit / škrtnout z VOP | VOP P056, Kodex P064 | `Order`, `OrderEmployeePay` nebo `nic` |

### R7 — Fotografie v domácnosti

Fotografie jako smluvní podmínka, nebo volitelná se souhlasem? Dokončení vyžaduje ≥ 1 `After` — [CompleteOrder.cs:183-188](../../src/Cleansia.Core.AppServices/Features/Orders/CompleteOrder.cs#L183-L188); člen `AssignedEmployees` ji smí smazat bez brány na stav — [DeleteOrderPhoto.cs:56-71](../../src/Cleansia.Core.AppServices/Features/Orders/DeleteOrderPhoto.cs#L56-L71). Výchozích 7 dní od `CompletedAt` prodlužuje neuzavřený spor (§1.8); týdenní `TimerTrigger` není pevný den smazání — [DataRetentionTimerFunction.cs:10](../../src/Cleansia.Functions/Functions/DataRetentionTimerFunction.cs#L10). Selhání blobu ponechá řádek pro další běh — [DataRetentionBackgroundService.cs:426-439](../../src/Cleansia.Core.AppServices/Features/DataRetention/DataRetentionBackgroundService.cs#L426-L439). Výmaz účtu ponechá anonymizované řádky a pokouší se smazat blob — [GdprDeletionService.cs:365-384](../../src/Cleansia.Core.AppServices/Services/GdprDeletionService.cs#L365-L384); pomocník však ponechává kontejner v cestě — [GdprDeletionService.cs:560-564](../../src/Cleansia.Core.AppServices/Services/GdprDeletionService.cs#L560-L564). Návrhy: [RS P091](../analyza-2026-09-11/podklady/cleansia_ramcova_smlouva.txt), [DPA P098](../analyza-2026-09-11/podklady/cleansia_dpa.txt).

| varianta | odstavce návrhů | v kódu |
|---|---|---|
| fotografie jako smluvní podmínka, „před" i „po" | VOP čl. VI doplnit, Kodex P059, RS P091 | `StartOrder` (brána), `DeleteOrderPhoto` (zámek) |
| fotografie volitelné se souhlasem na objednávce | RS P091, Kodex P067 | `CompleteOrder` (brána zrušena), nové pole souhlasu |
| přijmout 7 dní s výjimkou sporu, nebo stanovit jinou lhůtu a výjimky | DPA P098 | podle volby text, `OrderPhotosDays` a podmínka sporu v `OrderPhotoRepository` |

### R8 — Přístup úklidníka k údajům po zakázce a DPA

DPA popíše skutečnost, nebo kód omezí přístup po dokončení? Přiřazeného pouští `AssignedEmployees.Any(...)` bez časového členu — [OrderAccessService.cs:88-94](../../src/Cleansia.Core.AppServices/Authentication/OrderAccessService.cs#L88-L94). První neprázdné čtení instrukcí má audit (§1.8), nikoli gesto odhalení. Token partnerského webu má `AccessTokenExpMinutes` 1 440 — [appsettings.json:20](../../src/Cleansia.Web.Partner/appsettings.json#L20). Návrhy: [DPA P086](../analyza-2026-09-11/podklady/cleansia_dpa.txt), [RS P148](../analyza-2026-09-11/podklady/cleansia_ramcova_smlouva.txt).

| varianta | odstavce návrhů | v kódu |
|---|---|---|
| DPA popíše skutečný přístup, lhůty jen na kopie | DPA P086, P090, P094 | nic |
| odstřižení detailu X dnů po dokončení | DPA P070 | `OrderAccessService`, `OrderPiiRedaction` |
| podpis DPA jako podmínka schválení | RS P148, RS P209 | `ApproveEmployee`, seed `Legal/employee/` |

Právní sjednocení mimo kód: [RS P144](../analyza-2026-09-11/podklady/cleansia_ramcova_smlouva.txt) stanoví mlčenlivost deset let, [DPA P105](../analyza-2026-09-11/podklady/cleansia_dpa.txt) u oprávněných osob neurčitě. Vyjasnit rozsah a souběh pokut 50 000 Kč v [RS P152](../analyza-2026-09-11/podklady/cleansia_ramcova_smlouva.txt) a [DPA P178](../analyza-2026-09-11/podklady/cleansia_dpa.txt); souběh neplyne z kódu.

### R9 — Cookies, marketing, GPS, věk a retence

Cookie policy jmenuje nepřítomné skripty (§1.7). Lišta mapuje `analytics → DataProcessing`, `marketing → MarketingEmails` — [consent-sync.service.ts:38-41](../../src/Cleansia.App/libs/core/customer-services/src/lib/services/consent-sync.service.ts#L38-L41); souhlas neřídí odesílatele (§1.6). Rozhodnout je třeba věk (18/16) a soulad retenčních závazků s 12 číselnými okny a příznakem `DefaultExpiredCodesEnabled` — [RetentionDefaults.cs:19-31](../../src/Cleansia.Core.AppServices/Features/DataRetention/RetentionDefaults.cs#L19-L31). Audit zákazníka, administrátora i úklidníka má výchozí 3 roky. Návrhy: [PP P140](../analyza-2026-09-11/podklady/cleansia_privacy_policy.txt), [GDPR P096](../analyza-2026-09-11/podklady/cleansia_gdpr_souhlasy.txt).

| varianta | odstavce návrhů | v kódu |
|---|---|---|
| cookie policy podle skutečného inventáře, lišta informační | CP P035–P108, PP P076–P082 | `cleansia-cookie-consent`, `consent-sync.service` |
| marketingový e-mail s double opt-in / vypustit | GDPR P086, PP P068 | `EmailType`, `MarketingEmails` čtenář, nebo `nic` |
| věk 18 nebo 16 s prohlášením při registraci | VOP P030, PP P140, GDPR P096 | `Register.Command`, validátor |
| retence PP z `RetentionDefaults` | PP P041–P066 | nic |

### R10 — Kdo obsluhuje ruční závazky a co z nich do aplikace

Lhůty 30 minut, 3 pracovní dny, 30 dnů a 5 pracovních dnů nemají v kódu frontu ani měřidlo; každá potřebuje vlastníka, nebo vypustit. Administrátoři mají devět událostí od `OrderNew` po `CompanyArchived` — [AdminEventCatalog.cs:19-39](../../src/Cleansia.Core.AppServices/Features/AdminNotifications/AdminEventCatalog.cs#L19-L39) — hlášený incident mezi nimi není (`ReportOrderIssue.Handler`) — [ReportOrderIssue.cs:42-44](../../src/Cleansia.Core.AppServices/Features/Orders/ReportOrderIssue.cs#L42-L44). Návrhy: [RŘ P067](../analyza-2026-09-11/podklady/cleansia_reklamacni_rad.txt), [RŘ P090](../analyza-2026-09-11/podklady/cleansia_reklamacni_rad.txt).

| varianta | odstavce návrhů | v kódu |
|---|---|---|
| lhůty s vlastníkem a evidencí mimo aplikaci | žádný | nic |
| lhůty z dokumentů vypustit | RŘ P067, P086, P090, P104 | nic |
| lhůty navázat na feed administrace; incident jako desátá událost | RŘ P067 | `AdminEventCatalog`, `ReportOrderIssue` |

**R11 — Smlouva o dílo** · rozhodnuto 2026-09-20 (ADR-0068): ke každé zakázce vzniká smlouva zákazník ↔ úklidník; text (`LegalDocumentType.WorkContract` v platném znění) je otisknut na objednávku při založení a úklidník ho přijímá při převzetí každého místa v posádce — [OrderFactory.cs:83-86](../../src/Cleansia.Core.AppServices/Features/Orders/OrderFactory.cs#L83-L86). Otevřeno je šest „jak".

### R11.1 — Co je „cena díla" na záznamu

Nese smlouva cenu zákazníka, odměnu úklidníka, nebo obě? Souvisí s R1. Snímek nese `TotalPrice` v měně objednávky, odměna na záznamu není — [WorkContractFactsBuilder.cs:24-25](../../src/Cleansia.Core.AppServices/Services/WorkContractFactsBuilder.cs#L24-L25). Návrhy: [RS P114](../analyza-2026-09-11/podklady/cleansia_ramcova_smlouva.txt).

| varianta | odstavce návrhů | v kódu |
|---|---|---|
| cena zákazníka (dnešní stav) | žádný | nic |
| odměna úklidníka místo ceny | RS P114 | `WorkContractFacts`, `WorkContractFactsBuilder` |
| obě částky ve snímku | RS P114 | `WorkContractFacts` (jedno pole navíc) |

### R11.3 — Jak jsou strany označeny na zobrazení smlouvy

Vyžaduje spotřebitelská smlouva s podnikatelem identifikaci zhotovitele (jméno, příjmení, IČO), nebo postačí křestní jméno? Zákazník vidí jen `FirstName` — [OrderMappers.cs:375-376](../../src/Cleansia.Core.AppServices/Mappers/OrderMappers.cs#L375-L376); `RegistrationNumber` a `LegalEntityName` jsou na profilu, na smlouvu nejdou — [Employee.cs:16-19](../../src/Cleansia.Core.Domain/Users/Employee.cs#L16-L19). Návrhy: [VOP P022](../analyza-2026-09-11/podklady/Cleansia_VOP.txt).

| varianta | odstavce návrhů | v kódu |
|---|---|---|
| křestní jméno (dnešní stav) | žádný | nic |
| jméno, příjmení a IČO zhotovitele, případně obou stran | VOP P022, RS P026–P030 | `WorkContractFacts`, `GetWorkContract` |

### R11.4 — Přejetí prstem, zaškrtnutí, nebo kvalifikovaný podpis

Tvoří přejetí prstem či zaškrtnutí se záznamem B2C smlouvu o dílo podle českého práva, nebo je nutný kvalifikovaný podpis? Řádek přijetí nese `LegalDocumentTextId`, `AcceptedOn`, `ClientAudience`, `IpAddress`, `DeviceId`, `FactsJson` — [WorkContractAcceptance.cs:39-61](../../src/Cleansia.Core.Domain/Contracts/WorkContractAcceptance.cs#L39-L61). Gesto se liší podle klienta: web `cleansia-checkbox` a tlačítko, Android tažený `SlideToCommit` — [WorkContractSheet.kt:185-190](../../src/cleansia_android/partner-app/src/main/java/cz/cleansia/partner/features/orders/WorkContractSheet.kt#L185-L190). Návrhy: [RS P043](../analyza-2026-09-11/podklady/cleansia_ramcova_smlouva.txt).

| varianta | odstavce návrhů | v kódu |
|---|---|---|
| přejetí či zaškrtnutí se záznamem (dnešní stav) | žádný | nic |
| jedno gesto na webu i v mobilu | žádný | `work-contract-dialog.component` |
| kvalifikovaný podpis přes poskytovatele | VOP P022, RS P043 | `WorkContractAcceptance` (id obálky, čas podpisu), webhook |

### R11.5 — Znění VOP: okamžik vzniku smlouvy a inkorporace odkazem

Jaké věty VOP napíše právník: okamžik vzniku, inkorporace znění odkazem ve verzi platné při objednání, co je zákazníkovi doloženo. Kód: smlouva vzniká přijetím v převzetí — [TakeOrder.cs:357](../../src/Cleansia.Core.AppServices/Features/Orders/TakeOrder.cs#L357); text je otisknut při založení — [OrderFactory.cs:185](../../src/Cleansia.Core.AppServices/Features/Orders/OrderFactory.cs#L185). Návrhy: [VOP P038](../analyza-2026-09-11/podklady/Cleansia_VOP.txt), [VOP P040](../analyza-2026-09-11/podklady/Cleansia_VOP.txt).

| varianta | odstavce návrhů | v kódu |
|---|---|---|
| VOP převezmou tvar kódu (vznik přijetím, inkorporace odkazem) | VOP P022, P038, P040 | nic |
| VOP zůstanou u potvrzovacího e-mailu | žádný | `TakeOrder`, `OrderFactory`, nový okamžik vzniku |

### R11.6 — Smí administrátor úklidníka přidělit, nebo je přidělení nabídkou

Smí administrátor úklidníka přidělit, nebo je přidělení nabídkou s rezervací místa? `AdminReassignOrder` přidá úklidníka (`AddAssignedEmployee`) bez řádku přijetí — [AdminReassignOrder.cs:123](../../src/Cleansia.Core.AppServices/Features/Orders/AdminReassignOrder.cs#L123); branky `HasAcceptedWorkContractForSeatAsync` stojí až na zahájení — [StartOrder.cs:58-59](../../src/Cleansia.Core.AppServices/Features/Orders/StartOrder.cs#L58-L59). Návrhy: [RS P043](../analyza-2026-09-11/podklady/cleansia_ramcova_smlouva.txt).

| varianta | odstavce návrhů | v kódu |
|---|---|---|
| administrátor přiděluje, branky a samostatný úkon (dnešní stav) | RS P043 | nic |
| přidělení = nabídka s rezervací místa | žádný | `AdminReassignOrder`, `AcceptWorkContract` |

### R11.7 — Hrubá lokalita na trvalém záznamu

Smí „město · PSČ" zůstat na řádku přijetí navždy jako místo plnění, když výmaz zákazníka tytéž údaje na objednávce anonymizuje? Snímek nese `LocationApproximate` z `BuildApproximateAddress(City, ZipCode)` — [WorkContractFactsBuilder.cs:48](../../src/Cleansia.Core.AppServices/Services/WorkContractFactsBuilder.cs#L48); `Pseudonymise()` nuluje jen IP a zařízení — [WorkContractAcceptance.cs:102-107](../../src/Cleansia.Core.Domain/Contracts/WorkContractAcceptance.cs#L102-L107). Návrhy: [DPA P098](../analyza-2026-09-11/podklady/cleansia_dpa.txt).

| varianta | odstavce návrhů | v kódu |
|---|---|---|
| lokalita zůstane pod titulem řádku (dnešní stav) | DPA P098 | nic |
| lokalita se při výmazu vyprázdní | žádný | `WorkContractAcceptance.Pseudonymise` |

### R12 — Navýšení ceny a příplatek na místě

Víc práce = nová objednávka, nebo příplatek? `Order.TotalPrice` má jediný zápis při založení — [Order.cs:68](../../src/Cleansia.Core.Domain/Orders/Order.cs#L68) — a je zmrazena na řádku přijetí smlouvy — [WorkContractFactsBuilder.cs:46](../../src/Cleansia.Core.AppServices/Services/WorkContractFactsBuilder.cs#L46). Návrhy: [VOP P042](../analyza-2026-09-11/podklady/Cleansia_VOP.txt).

| varianta | odstavce návrhů | v kódu |
|---|---|---|
| víc práce = nová objednávka | VOP P042 doplnit větu | nic |
| příplatek potvrzený zákazníkem v aplikaci | VOP P042 | `Order.TotalPrice`, položka, platba, doklad, snímek smlouvy |
| příplatek zapsaný administrátorem | VOP P042 | `Order.TotalPrice`, admin příkaz, audit |

### R13 — Jeden úklidník, nebo posádka; cena díla na místo

Volí zákazník „jeden úklidník / posádka", nebo počítá platforma? Nese každé místo celou cenu díla, nebo podíl? Kód: `RequiredEmployees = ⌈EstimatedTime / 120⌉` — [Order.cs:872-875](../../src/Cleansia.Core.Domain/Orders/Order.cs#L872-L875); jeden řádek `OrderEmployeeId` na místo, každý s celou `TotalPrice` — [WorkContractAcceptance.cs:31](../../src/Cleansia.Core.Domain/Contracts/WorkContractAcceptance.cs#L31). Návrhy: [RS P059](../analyza-2026-09-11/podklady/cleansia_ramcova_smlouva.txt), [VOP P022](../analyza-2026-09-11/podklady/Cleansia_VOP.txt).

| varianta | odstavce návrhů | v kódu |
|---|---|---|
| výpočet platformě, věta do VOP a smlouvy o více zhotovitelích | VOP P022, RS P059 | `Seed/Legal/customer/work-contract` |
| volba „jeden / posádka" jako parametr objednávky | VOP P022 | `CreateOrder`, `OrderDuration`, `QuoteOrder` |
| podíl ceny na místo místo celé částky | RS P059 | `WorkContractFactsBuilder` |

### R14 — Čisticí prostředky a vybavení

Platí RS (úklidník má vlastní prostředky), volí zákazník, nebo jsou vždy zákazníkovy? Žádné pole objednávky, služby ani adresy neříká, kdo je přinese — [Order.cs:68](../../src/Cleansia.Core.Domain/Orders/Order.cs#L68); seedovaná smlouva říká jen „s použitím vhodného vybavení a prostředků" — [cs.md:23](../../src/Cleansia.Infra.Database/Seed/Legal/customer/work-contract/any/2026-09-20/cs.md#L23). Návrhy: [RS P069](../analyza-2026-09-11/podklady/cleansia_ramcova_smlouva.txt).

| varianta | odstavce návrhů | v kódu |
|---|---|---|
| RS P069 platí, věta do VOP, speciální prostředky jako doplněk | VOP čl. VI doplnit | `Extra` (nový doplněk v ceníku) |
| volba zákazníka při objednání | RS P069, VOP čl. VI | `CreateOrder`, `Order`, `WorkContractFacts` |
| prostředky vždy zákazníkovy | RS P069 přepsat | `Seed/Legal/customer/work-contract` |

### R15 — Omluvný kredit při stornu ze strany platformy

Kredit jen z automatického běhu, i při stornu administrátorem „naše vina", nebo slevový kód? Jediný automatický je `CleanerNoShow` ve výši `Currency.NoShowCredit`, jen registrovanému — [CancelUnfilledOrders.cs:276-300](../../src/Cleansia.Core.AppServices/Features/Orders/CancelUnfilledOrders.cs#L276-L300); storno administrátorem (`CancelledBy.Admin`) refunduje bez kreditu — [AdminCancelOrder.cs:92-98](../../src/Cleansia.Core.AppServices/Features/Orders/AdminCancelOrder.cs#L92-L98). Návrhy: [VOP P081](../analyza-2026-09-11/podklady/Cleansia_VOP.txt).

| varianta | odstavce návrhů | v kódu |
|---|---|---|
| kredit jen z automatického běhu, VOP na refundaci | VOP P081, P070 | nic |
| omluvný kredit i při stornu administrátorem „naše vina" | VOP P081 | `AdminCancelOrder`, nový důvod, oznámení |
| slevový kód místo kreditu | VOP P081 | `PromoCode`, automatické vydání |

**R16 — Úklidník plátce DPH** · rozhodnuto 2026-09-09 (v kódu `cleanersAreVatPayers = false`): úklidník není plátcem DPH; RS P124 slibuje úpravu ceny, která neexistuje — [FileExtensions.cs:102-104](../../src/Cleansia.Core.AppServices/Extensions/FileExtensions.cs#L102-L104).

### R17 — Věrnostní stupeň: klesá, nebo neklesá

Kód stupeň po odebrání bodů přepočítá a snížení dovolí (`RecomputeTier`) — [LoyaltyAccount.cs:114-120](../../src/Cleansia.Core.Domain/Loyalty/LoyaltyAccount.cs#L114-L120); body se odebírají při stornu i částečné refundaci. Web popisuje možnost snížení — [cs.json:1344](../../src/Cleansia.App/apps/cleansia.app/src/assets/i18n/cs.json#L1344). Volbou R17 je klesající, nebo neklesající stupeň; žádný návrh věrnost nejmenuje.

| varianta | odstavce návrhů | v kódu |
|---|---|---|
| stupeň smí klesnout | VOP čl. IV doplnit | nic |
| stupeň nikdy neklesá | VOP čl. IV doplnit | `LoyaltyAccount.RecomputeTier` |

### R18 — Která oznámení zákazník a úklidník dostanou

Feed má 14 klíčů `Customer` a šest `Partner` — [NotificationFeedEventKeys.cs:29-58](../../src/Cleansia.Core.Domain/Notifications/NotificationFeedEventKeys.cs#L29-L58). Úspěšná refundace při administrátorském stornu posílá `order.refunded` — [PlatformOrderCancellation.cs:96-118](../../src/Cleansia.Core.AppServices/Services/PlatformOrderCancellation.cs#L96-L118); totéž refundace sporu, i částečná — [ResolveDispute.cs:118-143](../../src/Cleansia.Core.AppServices/Features/Disputes/ResolveDispute.cs#L118-L143). Doklad a útlum mají e-mail — [EmailType.cs:10-22](../../src/Cleansia.Core.Domain/Enums/EmailType.cs#L10-L22). Katalog nemá samostatné zákaznické události pro odchod úklidníka, kredit, zamítnutí reklamace či chargeback ani partnerské pro schválení účtu a upravenou fakturu — [NotificationEventCatalog.cs:10-224](../../src/Cleansia.Core.Domain/Notifications/NotificationEventCatalog.cs#L10-L224). Rozhodnutí se týká obsahu a kanálu, nikoli paušální absence oznámení. Návrhy: [RŘ P081](../analyza-2026-09-11/podklady/cleansia_reklamacni_rad.txt), [RŘ P112](../analyza-2026-09-11/podklady/cleansia_reklamacni_rad.txt).

| varianta | odstavce návrhů | v kódu |
|---|---|---|
| doplnit samostatné události a jejich kanály podle výčtu | žádný | `NotificationEventCatalog`, odpovídající producenti a klienti |
| postavit jen právně nesené (zamítnutá reklamace, kredit, chargeback) | RŘ P081, P112 | `NotificationEventCatalog`, tři producenti |
| popsat lhůty ručně mimo aplikaci | RŘ P067, P081, P112 | nic |

### R19 — Objednávka mimo trh účtu

Operátora má v seedu jen CZE (`cleansia-cz`, `IsDefaultMarket`), u SVK, POL, DEU, AUT a GBR je `NULL` — [insert_seed_data.sql:1014](../../sql-scripts/insert_seed_data.sql#L1014), [insert_seed_data.sql:1017-1064](../../sql-scripts/insert_seed_data.sql#L1017-L1064). EUR, PLN, GBP a USD jsou seedovány jako neaktivní a bez dělitele bodů — [insert_seed_data.sql:491-494](../../sql-scripts/insert_seed_data.sql#L491-L494). Objednávku do takového trhu kód odmítne: přihlášenému `TenantNotFound`, hostu `OrderCountryOperatorMismatch` — [CreateOrder.cs:181-195](../../src/Cleansia.Core.AppServices/Features/Orders/CreateOrder.cs#L181-L195). Je to tedy mechanika čekající na druhý trh: objednávka, doklad, refundace i reklamace patří společnosti trhu adresy (`OperatorTenantId`), účet a věrnost své společnosti — [CreateOrder.cs:952-954](../../src/Cleansia.Core.AppServices/Features/Orders/CreateOrder.cs#L952-L954). Body se v měně bez `LoyaltyPointsDivisor` nepřipíší — [Currency.cs:22-30](../../src/Cleansia.Core.Domain/Internationalization/Currency.cs#L22-L30). Žádný návrh situaci nejmenuje.

| varianta | odstavce návrhů | v kódu |
|---|---|---|
| ponechat, VOP a PP popíší dvě společnosti | VOP P018, PP P020 | nic |
| omezit objednávku na trh účtu | VOP P018 | `CreateOrder` (nová brána) |
| dořešit body a kredit napříč měnami | VOP čl. IV | `Currency.LoyaltyPointsDivisor`, `CreditAccount` |

### R20 — Řídicí prvky proti znakům nezávislé spolupráce

RS P065 prohlašuje šest znaků nezávislé spolupráce; tři z nich kód váže. Týdenní strop zakázek (`WeeklyOrderLimit`) nasazuje administrátor jednotlivci, výchozí je bez stropu a překročení odmítne `order.weekly_limit_reached` — [Employee.cs:148](../../src/Cleansia.Core.Domain/Users/Employee.cs#L148), [BusinessErrorMessage.cs:112](../../src/Cleansia.Core.AppServices/Common/BusinessErrorMessage.cs#L112). Dokončení bez fotografie `After` neprojde (`HasAfterPhotosAsync`) — [CompleteOrder.cs:183-188](../../src/Cleansia.Core.AppServices/Features/Orders/CompleteOrder.cs#L183-L188). Zahájení stojí na přijaté smlouvě k sedadlu (`HasAcceptedWorkContractForSeatAsync`) — [StartOrder.cs:58-59](../../src/Cleansia.Core.AppServices/Features/Orders/StartOrder.cs#L58-L59). Návrhy: [RS P065](../analyza-2026-09-11/podklady/cleansia_ramcova_smlouva.txt), [RS P080](../analyza-2026-09-11/podklady/cleansia_ramcova_smlouva.txt).

| varianta | odstavce návrhů | v kódu |
|---|---|---|
| prvky jsou podmínky platformy a RS je popíše | RS P065, P080, Kodex P059 | nic |
| řízení zúžit na výsledek díla | RS P080 | `WeeklyOrderLimit`, brána fotografie |
| text ponechat a prvky z kódu odebrat | žádný | `TakeOrder`, `CompleteOrder` |

Právní posouzení mimo kód: [RS P178](../analyza-2026-09-11/podklady/cleansia_ramcova_smlouva.txt) navrhuje dvanáctiměsíční zákaz konkurence, [RS P184](../analyza-2026-09-11/podklady/cleansia_ramcova_smlouva.txt) pokutu 150 000 Kč. Nejde o tvrzení, že je vymáhá aplikace.

### R21 — Fiskalizace

Modul `Fiscal` má pro Česko poskytovatele, který doklad nezaregistruje: vrací `NOT_IMPLEMENTED` — [CzechEet2FiscalService.cs:53-55](../../src/Cleansia.Infra.Fiscal/Countries/Czechia/CzechEet2FiscalService.cs#L53-L55). V konfiguraci je `Fiscal:CzechEet2:Enabled` false — [appsettings.json:56](../../src/Cleansia.Web.Admin/appsettings.json#L56). Žádný návrh fiskalizaci nejmenuje.

| varianta | odstavce návrhů | v kódu |
|---|---|---|
| dokončit, než ji trh vyžádá | VOP čl. IV doplnit | `CzechEet2FiscalService`, konfigurace |
| modul odebrat | žádný | `Cleansia.Infra.Fiscal`, registrace služby |
| ponechat nečinný a napsat to do návrhů | VOP čl. IV doplnit | nic |

