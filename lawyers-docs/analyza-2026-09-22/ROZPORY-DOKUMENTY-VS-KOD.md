# Rozpory mezi právními dokumenty a aplikací
**Návrhy z 11. 9. 2026 proti zdrojovému kódu · commit 6792f0c256e81e1473908308bd73881004853be4 (master, 22. 9. 2026)**

Slovník: úklidník = „Zhotovitel" (VOP, RS, RŘ, Kodex) = `Employee` (kód) = „uklízeč" (UI); zákazník = „Zákazník / Spotřebitel / Uživatel" = `User`; host = zákazník bez účtu (`Order.UserId = null`); provozní společnost = „Provozovatel / Operátor / Správce / Cleansia s.r.o." = `Tenant`; administrátor = jeden z `Administrator / Manager / Support / Accountant`, jmenován, kde na roli záleží.

Označení `Pxxx` je číslo odstavce extraktu v `../analyza-2026-09-11/podklady/`, ne číslo článku; článek je ve druhém sloupci.

## Shrnutí

109 odstavců deseti návrhů stojí proti řádku kódu: 78 rozporů a 31 částečných pokrytí (VOP 15/6, RŘ 9/3, RS 17/6, Kodex 4/2, BOZP 5/0, PP 9/3, CP 6/1, GDPR 7/4, DPA 6/4, SS 0/2). 18 funkcí platformy žádný návrh nejmenuje (§2). Rozhodnutí (§3): rozhodnuta jsou R2 (holding a společnost na region, ADR-0061), R11 (smlouva o dílo ke každé zakázce, ADR-0068) a R16 (úklidník není plátce DPH). Otevřených je osmnáct: R1 obchodní model a vystavitel dokladu; R3 znění, které zákazník a úklidník přijímají; R4 storno 25 %/50 % proti 50 %/100 % a kredit bez výplaty; R5 host bez účtu; R6 sazbová odměna, hotovost bez započtení, spropitné; R7 fotografie („po" ≥ 1, „před" nikdy); R8 přístup úklidníka k údajům bez časového limitu a DPA; R9 cookies bez skriptů, marketing bez čtenáře, GPS, věk, retence; R10 lhůty 30 min / 3 dny / 30 dnů bez vlastníka; R12 příplatek na místě; R13 posádka a cena díla na místo; R14 čisticí prostředky; R15 omluvný kredit 250 Kč; R17 věrnostní stupeň klesá; R18 deset událostí bez oznámení; R19 objednávka mimo trh účtu, dokud druhý trh nemá operátora; R20 řídicí prvky proti znakům nezávislé spolupráce; R21 fiskalizace. K R11 je otevřeno šest „jak" (R11.1, R11.3–R11.7).

## 1. Dokument po dokumentu

### 1.1 VOP

Shoda: [VOP P020](../analyza-2026-09-11/podklady/Cleansia_VOP.txt), [VOP P024](../analyza-2026-09-11/podklady/Cleansia_VOP.txt), [VOP P085](../analyza-2026-09-11/podklady/Cleansia_VOP.txt), [VOP P091](../analyza-2026-09-11/podklady/Cleansia_VOP.txt), [VOP P109](../analyza-2026-09-11/podklady/Cleansia_VOP.txt), [VOP P111](../analyza-2026-09-11/podklady/Cleansia_VOP.txt)

Mimo aplikaci (nic v kódu): P059, P087, P089, P093, P115, P127, P129, P135

| Pxxx | čl. | citát | kód | citace | třída | řešení |
|---|---|---|---|---|---|---|
| [VOP P014](../analyza-2026-09-11/podklady/Cleansia_VOP.txt) | úvod | „Registrací v Aplikaci a/nebo podáním první objednávky zákazník potvrzuje, že se s VOP seznámil" | Seedované podmínky nesou banner `Návrh`; zákazník přijímá jiný text než návrh | [cs.md:5](../../src/Cleansia.Infra.Database/Seed/Legal/customer/terms-of-service/any/2026-09-14/cs.md#L5) | rozpor | → R3 |
| [VOP P022](../analyza-2026-09-11/podklady/Cleansia_VOP.txt) | 1.3 | „smlouva o dílo na konkrétní úklid vzniká přímo mezi Zákazníkem a Zhotovitelem okamžikem potvrzení zakázky" | Řádek přijetí zapisuje převzetí (`StageAsync`); seedované podmínky: `Cleansia` „poskytuje profesionální úklidové služby" | [TakeOrder.cs:356](../../src/Cleansia.Core.AppServices/Features/Orders/TakeOrder.cs#L356); [cs.md:15](../../src/Cleansia.Infra.Database/Seed/Legal/customer/terms-of-service/any/2026-09-14/cs.md#L15) | částečně | → R1 |
| [VOP P028](../analyza-2026-09-11/podklady/Cleansia_VOP.txt) | 2.1 | „jméno a příjmení, e-mailovou adresu, telefonní číslo a fakturační adresu" | `Register.Command` bez adresy; host objednává bez účtu, `Order.UserId` nullable | [Register.cs:73-88](../../src/Cleansia.Core.AppServices/Features/Auth/Register.cs#L73-L88); [Order.cs:222](../../src/Cleansia.Core.Domain/Orders/Order.cs#L222) | rozpor | → R5 |
| [VOP P030](../analyza-2026-09-11/podklady/Cleansia_VOP.txt) | 2.2 | „Služby Cleansia mohou využívat pouze osoby starší 18 let" | `Register.Command` nese `TermsAccepted`, ne datum narození; věk zákazníka je bez dolní hranice (`BeReasonableAge` jen do 120 let) | [Register.cs:73-89](../../src/Cleansia.Core.AppServices/Features/Auth/Register.cs#L73-L89); [UpdateCurrentUser.cs:108-112](../../src/Cleansia.Core.AppServices/Features/Users/UpdateCurrentUser.cs#L108-L112) | rozpor | → R9 |
| [VOP P034](../analyza-2026-09-11/podklady/Cleansia_VOP.txt) | 2.4 | „V případě zrušení účtu jsou Zákazníkovi vráceny nevyužité kredity v peněžní hodnotě" | Kladný zůstatek výmaz blokuje (`GdprDeletionBlockedByCreditBalance`); výplata neexistuje, zůstatek propadá (`ExpiryMonths` 12 měsíců) | [GdprDeletionService.cs:157-160](../../src/Cleansia.Core.AppServices/Services/GdprDeletionService.cs#L157-L160); [CreditAccount.cs:69](../../src/Cleansia.Core.Domain/Credit/CreditAccount.cs#L69) | rozpor | → R4 |
| [VOP P038](../analyza-2026-09-11/podklady/Cleansia_VOP.txt) | 3.1 | „Objednávka je závazná okamžikem potvrzení platby" | Webhook zapisuje jen `PaymentStatus.Paid`; hotovostní objednávka (`PaymentType.Cash`) je závazná bez jakékoli platby | [HandlePaymentNotification.cs:297-309](../../src/Cleansia.Core.AppServices/Features/Payments/HandlePaymentNotification.cs#L297-L309); [PaymentType.cs:8-9](../../src/Cleansia.Core.Domain/Enums/PaymentType.cs#L8-L9) | rozpor | → R11.5 |
| [VOP P040](../analyza-2026-09-11/podklady/Cleansia_VOP.txt) | 3.2 | „Potvrzení obsahuje shrnutí objednávky, předpokládaný termín a informace o přiděleném Zhotoviteli" | Při vzniku objednávky úklidník neexistuje; po převzetí push `order.cleaner_assigned` bez jména | [NotificationEventCatalog.cs:26-31](../../src/Cleansia.Core.Domain/Notifications/NotificationEventCatalog.cs#L26-L31); [TakeOrder.cs:420-421](../../src/Cleansia.Core.AppServices/Features/Orders/TakeOrder.cs#L420-L421) | rozpor | → R11.5 |
| [VOP P042](../analyza-2026-09-11/podklady/Cleansia_VOP.txt) | 3.3 | „nejpozději [doplnit, např. 24 hodin] před sjednaným termínem zahájení úklidu" | Změna termínu ani rozsahu neexistuje; `TotalPrice` má jediný zápis při založení | [Order.cs:68](../../src/Cleansia.Core.Domain/Orders/Order.cs#L68) | rozpor | → R12 |
| [VOP P044](../analyza-2026-09-11/podklady/Cleansia_VOP.txt) | 3.4 | „zákazník neodpovídá na hovory po dobu více než 15 minut" | Stav „zákazník nedostupný" neexistuje; nejvyšší sazba `LastMinuteCancellationFeeRate` je 0,50 | [BookingPolicy.cs:70](../../src/Cleansia.Core.AppServices/Features/Orders/BookingPolicy.cs#L70) | rozpor | → R4 |
| [VOP P050](../analyza-2026-09-11/podklady/Cleansia_VOP.txt) | 4.2 | „platební karty (Visa, Mastercard, Amex) … bankovního převodu" | `PaymentType` má jen `Cash = 1`, `Card = 2`; bankovní převod zákazníka neexistuje | [PaymentType.cs:8-9](../../src/Cleansia.Core.Domain/Enums/PaymentType.cs#L8-L9) | rozpor | → R6 |
| [VOP P056](../analyza-2026-09-11/podklady/Cleansia_VOP.txt) | 4.5 | „Spropitné je dobrovolné … v plné výši předáno Zhotoviteli" | Spropitné doména nezná; odměna vzniká jen z `CalculatePay` nad `EmployeePayConfig` | [PayCalculatorExtensions.cs:8](../../src/Cleansia.Core.Domain/Extensions/PayCalculatorExtensions.cs#L8) | rozpor | → R6 |
| [VOP P064](../analyza-2026-09-11/podklady/Cleansia_VOP.txt) | V ř. 1 | „Více než 24 hodin předem" | `FreeCancellationHours = 24`; bez převzetí úklidníkem vždy zdarma; člen Plus má `FreeCancellationWindowHours` 4 h | [BookingPolicy.cs:64](../../src/Cleansia.Core.AppServices/Features/Orders/BookingPolicy.cs#L64); [insert_seed_data.sql:1799-1805](../../sql-scripts/insert_seed_data.sql#L1799-L1805) | částečně | → R4 |
| [VOP P069](../analyza-2026-09-11/podklady/Cleansia_VOP.txt) | V ř. 2 | „50 % ceny zakázky" | 4–24 h předem 25 % (`PartialCancellationFeeRate`); seedované podmínky říkají 25 %/50 % | [BookingPolicy.cs:67](../../src/Cleansia.Core.AppServices/Features/Orders/BookingPolicy.cs#L67); [cs.md:23](../../src/Cleansia.Infra.Database/Seed/Legal/customer/terms-of-service/any/2026-09-14/cs.md#L23) | rozpor | → R4 |
| [VOP P073](../analyza-2026-09-11/podklady/Cleansia_VOP.txt) | V ř. 3 | „100 % ceny zakázky" | Sazba 100 % neexistuje; pod `PartialCancellationHours` platí 0,50; `Cancel` sazbu jen zapíše do `CancellationFeeRate`, u hotovosti ji nikdo nevybere | [BookingPolicy.cs:70-73](../../src/Cleansia.Core.AppServices/Features/Orders/BookingPolicy.cs#L70-L73); [Order.cs:885](../../src/Cleansia.Core.Domain/Orders/Order.cs#L885) | rozpor | → R4 |
| [VOP P081](../analyza-2026-09-11/podklady/Cleansia_VOP.txt) | 5.4 | „plnou náhradu zaplacené částky do 5 pracovních dnů nebo možnost přesunout objednávku na jiný termín" | Neobsazená zakázka: sweep 30 min po začátku (`GraceMinutes`), vratka a kredit `NoShowCredit` 250 Kč; přesun neexistuje | [CancelUnfilledOrders.cs:63](../../src/Cleansia.Core.AppServices/Features/Orders/CancelUnfilledOrders.cs#L63); [insert_seed_data.sql:490](../../sql-scripts/insert_seed_data.sql#L490) | částečně | → R15 |
| [VOP P097](../analyza-2026-09-11/podklady/Cleansia_VOP.txt) | 7.1 | „minimální limit pojistného plnění činí 3 000 000 Kč na pojistnou událost" | Seed CZE `InsuranceCoverageAmount` 1 000 000 Kč; `Employee : TenantAuditable` pole pojištění nemá | [insert_seed_data.sql:1014](../../sql-scripts/insert_seed_data.sql#L1014); [Employee.cs:11](../../src/Cleansia.Core.Domain/Users/Employee.cs#L11) | rozpor | text |
| [VOP P101](../analyza-2026-09-11/podklady/Cleansia_VOP.txt) | 7.3 | „(nejpozději do 24 hodin od dokončení zakázky) nahlásit škodu v Aplikaci nebo na e-mail" | Okno `FilingWindowHours` 24 h jen označí `FiledWithinWindow`; host spor nepodá (`CustomerOnly`) | [DisputeLimits.cs:18-29](../../src/Cleansia.Core.Domain/Disputes/DisputeLimits.cs#L18-L29); [PolicyBuilder.cs:130](../../src/Cleansia.Core.AppServices/Authentication/PolicyBuilder.cs#L130) | částečně | → R5 |
| [VOP P105](../analyza-2026-09-11/podklady/Cleansia_VOP.txt) | 7.5 | „Reklamačním řádu Cleansia, který je dostupný na webu a v Aplikaci" | `LegalDocumentType` zná jen `TermsOfService`, `PrivacyPolicy`, `WorkContract` | [LegalDocumentType.cs:8-16](../../src/Cleansia.Core.Domain/Legal/LegalDocumentType.cs#L8-L16) | rozpor | → R3 |
| [VOP P121](../analyza-2026-09-11/podklady/Cleansia_VOP.txt) | 10.1 | „informován e-mailem nebo oznámením v Aplikaci nejpozději 14 dní před jejich účinností" | Nová verze = `LegalDocument.Create` s jiným `EffectiveFrom`, bez oznámení; objednávka projde s jakýmkoli `IsGranted` souhlasem bez ohledu na verzi | [LegalDocumentSeeder.cs:51-62](../../src/Cleansia.Infra.Database/Seed/Legal/LegalDocumentSeeder.cs#L51-L62); [CreateOrder.cs:325-329](../../src/Cleansia.Core.AppServices/Features/Orders/CreateOrder.cs#L325-L329) | částečně | → R3 |
| [VOP P123](../analyza-2026-09-11/podklady/Cleansia_VOP.txt) | 10.2 | „budou vráceny kredity a přeplatky za již zaplacené, ale neprovedené zakázky" | Útlum společnosti kredit odepisuje (`DischargeCreditAsync` důvodem `Expired`); objednávky refunduje `ServiceNotRendered` | [CompanyWindDownService.cs:449-468](../../src/Cleansia.Core.AppServices/Services/CompanyWindDownService.cs#L449-L468); [CompanyWindDownService.cs:304-325](../../src/Cleansia.Core.AppServices/Services/CompanyWindDownService.cs#L304-L325) | rozpor | → R4 |
| [VOP P142](../analyza-2026-09-11/podklady/Cleansia_VOP.txt) | UMÍSTĚNÍ | „Bez zaregistrovaného souhlasu nelze zákazníka zaregistrovat ani mu umožnit objednání služby" | Registrace souhlas zapisuje; objednávku pustí tvrzení klienta (`AssertedOrAlreadyConsentedAsync`), host řádek souhlasu nemá | [CreateOrder.cs:312-317](../../src/Cleansia.Core.AppServices/Features/Orders/CreateOrder.cs#L312-L317) | částečně | → R5 |

### 1.2 RŘ

Shoda: [RŘ P030](../analyza-2026-09-11/podklady/cleansia_reklamacni_rad.txt), [RŘ P031](../analyza-2026-09-11/podklady/cleansia_reklamacni_rad.txt), [RŘ P034](../analyza-2026-09-11/podklady/cleansia_reklamacni_rad.txt), [RŘ P059](../analyza-2026-09-11/podklady/cleansia_reklamacni_rad.txt), [RŘ P061](../analyza-2026-09-11/podklady/cleansia_reklamacni_rad.txt), [RŘ P110](../analyza-2026-09-11/podklady/cleansia_reklamacni_rad.txt)

Mimo aplikaci (nic v kódu): P052, P114–P126, P134

| Pxxx | čl. | citát | kód | citace | třída | řešení |
|---|---|---|---|---|---|---|
| [RŘ P018](../analyza-2026-09-11/podklady/cleansia_reklamacni_rad.txt) | 1.1 | „Přímým poskytovatelem úklidových služeb je Zhotovitel jakožto samostatná OSVČ" | Doklad zákazníkovi vystavuje provozní společnost (`CompanyInfo` jako vystavitel) | [ReceiptService.cs:423-429](../../src/Cleansia.Core.AppServices/Services/ReceiptService.cs#L423-L429) | rozpor | → R1 |
| [RŘ P020](../analyza-2026-09-11/podklady/cleansia_reklamacni_rad.txt) | 1.2 | „každý Zákazník, který si prostřednictvím Aplikace objednal a zaplatil úklidovou službu" | Spor podá jen přihlášený (`CanCreateDispute` = `CustomerOnly`); host nemá endpoint | [PolicyBuilder.cs:130](../../src/Cleansia.Core.AppServices/Authentication/PolicyBuilder.cs#L130); [CreateDispute.cs:112-126](../../src/Cleansia.Core.AppServices/Features/Disputes/CreateDispute.cs#L112-L126) | částečně | → R5 |
| [RŘ P046](../analyza-2026-09-11/podklady/cleansia_reklamacni_rad.txt) | 3.1 | „nejpozději do 24 hodin od dokončení zakázky potvrzeného v Aplikaci" | Jediné okno `FilingWindowHours` 24 h, jen příznak; 48 h pro krádež neexistuje | [DisputeLimits.cs:29](../../src/Cleansia.Core.Domain/Disputes/DisputeLimits.cs#L29); [DisputeLimits.cs:35-37](../../src/Cleansia.Core.Domain/Disputes/DisputeLimits.cs#L35-L37) | částečně | text |
| [RŘ P053](../analyza-2026-09-11/podklady/cleansia_reklamacni_rad.txt) | III | „ověřit stav před úklidem z fotodokumentace Zhotovitele" | `PhotoType.Before` nikde vyžadován; úklidník spor nevidí (partnerské hosty bez `Dispute`) | [PhotoType.cs:8](../../src/Cleansia.Core.Domain/Enums/PhotoType.cs#L8); [PolicyBuilder.cs:130-139](../../src/Cleansia.Core.AppServices/Authentication/PolicyBuilder.cs#L130-L139) | rozpor | → R7 |
| [RŘ P062](../analyza-2026-09-11/podklady/cleansia_reklamacni_rad.txt) | 4.1 | „Fotodokumentaci — min. 2 fotografie dokumentující reklamovanou skutečnost" | Založení nese `Reason`, `Description` a řádky, žádnou přílohu; evidence až poté (`FileData`), bez minima | [CreateDispute.cs:73-82](../../src/Cleansia.Core.AppServices/Features/Disputes/CreateDispute.cs#L73-L82); [UploadDisputeEvidence.cs:30-34](../../src/Cleansia.Core.AppServices/Features/Disputes/UploadDisputeEvidence.cs#L30-L34) | rozpor | text |
| [RŘ P067](../analyza-2026-09-11/podklady/cleansia_reklamacni_rad.txt) | 4.3 | „obdrží e-mailem nebo push oznámením v Aplikaci do 30 minut od podání reklamace" | Podání zákazníkovi nic neposílá; jen administrátoři `admin.dispute.filed` | [CreateDispute.cs:186-201](../../src/Cleansia.Core.AppServices/Features/Disputes/CreateDispute.cs#L186-L201) | rozpor | → R10 |
| [RŘ P081](../analyza-2026-09-11/podklady/cleansia_reklamacni_rad.txt) | V k. 2 | „Zákazník obdrží automatické potvrzení o registraci reklamace s referenčním číslem" | Jediná zákaznická sporová událost `dispute.reply` (odpověď podpory) | [NotificationEventCatalog.cs:22](../../src/Cleansia.Core.Domain/Notifications/NotificationEventCatalog.cs#L22) | rozpor | → R18 |
| [RŘ P085](../analyza-2026-09-11/podklady/cleansia_reklamacni_rad.txt) | V k. 3 | „Cleansia zkontaktuje Zhotovitele, vyžádá jeho vyjádření a posoudí fotodokumentaci" | Úklidník spor nevidí ani neodpovídá (`Dispute` 0 výskytů v partnerských kontrolerech) | [PolicyBuilder.cs:130-139](../../src/Cleansia.Core.AppServices/Authentication/PolicyBuilder.cs#L130-L139) | rozpor | → R18 |
| [RŘ P090](../analyza-2026-09-11/podklady/cleansia_reklamacni_rad.txt) | V k. 4 | „Do 30 dnů od podání" | Lhůta rozhodnutí na sporu není; `DisputeDetails` nese jen `FiledWithinWindow` | [DisputeDetails.cs:43](../../src/Cleansia.Core.AppServices/Features/Disputes/DTOs/DisputeDetails.cs#L43) | rozpor | → R10 |
| [RŘ P093](../analyza-2026-09-11/podklady/cleansia_reklamacni_rad.txt) | V k. 5 | „opravný úklid, sleva, vrácení platby nebo kredit" | `ResolveDispute` zná jen `RefundAmount`; kredit ručně `IssueCustomerCredit`; opravný úklid neexistuje | [ResolveDispute.cs:25-39](../../src/Cleansia.Core.AppServices/Features/Disputes/ResolveDispute.cs#L25-L39); [IssueCustomerCredit.cs:14-17](../../src/Cleansia.Core.AppServices/Features/Credit/Admin/IssueCustomerCredit.cs#L14-L17) | částečně | → R10 |
| [RŘ P112](../analyza-2026-09-11/podklady/cleansia_reklamacni_rad.txt) | 5.4 | „povinna Zákazníkovi sdělit důvody zamítnutí" | Zamítnutí bez refundace zákazníka nenotifikuje; `ResolveDispute` posílá jen `order.refunded` | [ResolveDispute.cs:89-126](../../src/Cleansia.Core.AppServices/Features/Disputes/ResolveDispute.cs#L89-L126) | rozpor | → R18 |
| [RŘ P137](../analyza-2026-09-11/podklady/cleansia_reklamacni_rad.txt) | UMÍSTĚNÍ | „(2) V Aplikaci pod Nastavení > Právní dokumenty" | Reklamační řád jako dokument neexistuje (`LegalDocumentType` tři typy) | [LegalDocumentType.cs:8-16](../../src/Cleansia.Core.Domain/Legal/LegalDocumentType.cs#L8-L16) | rozpor | → R3 |

### 1.3 RS

Shoda: [RS P026](../analyza-2026-09-11/podklady/cleansia_ramcova_smlouva.txt), [RS P027](../analyza-2026-09-11/podklady/cleansia_ramcova_smlouva.txt), [RS P028](../analyza-2026-09-11/podklady/cleansia_ramcova_smlouva.txt), [RS P030](../analyza-2026-09-11/podklady/cleansia_ramcova_smlouva.txt), [RS P031](../analyza-2026-09-11/podklady/cleansia_ramcova_smlouva.txt), [RS P032](../analyza-2026-09-11/podklady/cleansia_ramcova_smlouva.txt), [RS P033](../analyza-2026-09-11/podklady/cleansia_ramcova_smlouva.txt), [RS P057](../analyza-2026-09-11/podklady/cleansia_ramcova_smlouva.txt), [RS P063](../analyza-2026-09-11/podklady/cleansia_ramcova_smlouva.txt), [RS P162](../analyza-2026-09-11/podklady/cleansia_ramcova_smlouva.txt)

Mimo aplikaci (nic v kódu): P045, P061, P071–P077, P085–P087, P095–P101, P110, P136, P174

| Pxxx | čl. | citát | kód | citace | třída | řešení |
|---|---|---|---|---|---|---|
| [RS P017](../analyza-2026-09-11/podklady/cleansia_ramcova_smlouva.txt) | I | „Obchodní firma: Cleansia s.r.o." | Registr `Tenants` „Cleansia CZ s.r.o."; `CompanyInfo` „Cleansia s.r.o." s IČO `REPLACE WITH ACTUAL` | [insert_seed_data.sql:63-64](../../sql-scripts/insert_seed_data.sql#L63-L64); [insert_seed_data.sql:1158-1162](../../sql-scripts/insert_seed_data.sql#L1158-L1162) | částečně | → R2 |
| [RS P041](../analyza-2026-09-11/podklady/cleansia_ramcova_smlouva.txt) | 2.2 | „Operátor je výhradně provozovatelem technologické platformy a zprostředkovatelem zakázek" | `Tenant.cs`: společnost „contracts the customer, employs the cleaner"; doklad vystavuje `CompanyInfo` | [Tenant.cs:7-8](../../src/Cleansia.Core.Domain/Tenancy/Tenant.cs#L7-L8); [ReceiptService.cs:423-429](../../src/Cleansia.Core.AppServices/Services/ReceiptService.cs#L423-L429) | rozpor | → R1 |
| [RS P043](../analyza-2026-09-11/podklady/cleansia_ramcova_smlouva.txt) | 2.3 | „v okamžiku, kdy Zhotovitel prostřednictvím Aplikace potvrdí akceptaci dané zakázky" | Přijetí zapisuje převzetí (`StageAsync`); `AdminReassignOrder` přidá úklidníka (`AddAssignedEmployee`) bez řádku přijetí | [TakeOrder.cs:356](../../src/Cleansia.Core.AppServices/Features/Orders/TakeOrder.cs#L356); [AdminReassignOrder.cs:123](../../src/Cleansia.Core.AppServices/Features/Orders/AdminReassignOrder.cs#L123) | částečně | → R11.6 |
| [RS P059](../analyza-2026-09-11/podklady/cleansia_ramcova_smlouva.txt) | 3.4 | „plnit zakázku osobně nebo prostřednictvím svých vlastních zaměstnanců, spolupracovníků či subdodavatelů" | Každé místo v posádce je vlastní účet a vlastní řádek přijetí (`OrderEmployeeId`); subdodavatele data neznají | [WorkContractAcceptance.cs:31](../../src/Cleansia.Core.Domain/Contracts/WorkContractAcceptance.cs#L31) | rozpor | → R13 |
| [RS P065](../analyza-2026-09-11/podklady/cleansia_ramcova_smlouva.txt) | 3.7 | „Zhotovitel si svobodně volí, které zakázky přijme a které odmítne — bez jakékoliv sankce" | Administrátor smí úklidníkovi nasadit týdenní strop zakázek (`WeeklyOrderLimit`, odmítnutí `order.weekly_limit_reached`); dokončení váže fotografie `After` | [Employee.cs:148](../../src/Cleansia.Core.Domain/Users/Employee.cs#L148); [CompleteOrder.cs:183-188](../../src/Cleansia.Core.AppServices/Features/Orders/CompleteOrder.cs#L183-L188) | rozpor | → R20 |
| [RS P080](../analyza-2026-09-11/podklady/cleansia_ramcova_smlouva.txt) | 4.6 | „Dosažení průměrného hodnocení pod [doplnit, např. 4,0 hvězdičky z 5,0] po dobu delší než [doplnit, např. 30 … dní] … může vést k dočasnému pozastavení" | Nástěnku otevírá `ContractStatus.Approved`, hodnocení ji neuzavírá; `AverageRating` jde jen do výpisů a exportu | [TakeOrder.cs:214](../../src/Cleansia.Core.AppServices/Features/Orders/TakeOrder.cs#L214); [EmployeeMappers.cs:69](../../src/Cleansia.Core.AppServices/Mappers/EmployeeMappers.cs#L69) | rozpor | → R20 |
| [RS P089](../analyza-2026-09-11/podklady/cleansia_ramcova_smlouva.txt) | 5.3 | „zavazuje se Zhotovitel takto vyplacenou částku Operátorovi v plné výši refundovat" | Refundace ani chargeback odměnu nemění; `DeductionPay` zvyšuje jen ruční srážka, po ní `RecomputeTotalPay` | [OrderEmployeePay.cs:183-205](../../src/Cleansia.Core.Domain/EmployeePayroll/OrderEmployeePay.cs#L183-L205) | rozpor | → R6 |
| [RS P091](../analyza-2026-09-11/podklady/cleansia_ramcova_smlouva.txt) | 5.4 | „zdokumentovat fotografiemi s časovým razítkem a informovat Zákazníka i Operátora prostřednictvím Aplikace" | Fotografie `Before` nevyžadována; `ReportOrderIssue.Handler` nikoho neupozorní | [PhotoType.cs:8](../../src/Cleansia.Core.Domain/Enums/PhotoType.cs#L8); [ReportOrderIssue.cs:42-44](../../src/Cleansia.Core.AppServices/Features/Orders/ReportOrderIssue.cs#L42-L44) | rozpor | → R7 |
| [RS P114](../analyza-2026-09-11/podklady/cleansia_ramcova_smlouva.txt) | 7.1 | „Cena se skládá ze složky odměny Zhotovitele a provize Operátora" | Odměna = `BasePay` + (pokoje − 1)·`ExtraPerRoom` + koupelny·`ExtraPerBathroom` + km·`DistanceRatePerKm`, výsledek ořízne `ApplyMinMaxClamp`; cena objednávky ani provize v ní nejsou | [PayCalculatorExtensions.cs:12-18](../../src/Cleansia.Core.Domain/Extensions/PayCalculatorExtensions.cs#L12-L18) | rozpor | → R6 |
| [RS P116](../analyza-2026-09-11/podklady/cleansia_ramcova_smlouva.txt) | 7.2 | „provize ve výši [doplnit, např. 20 %] z celkové ceny zakázky zaplacené Zákazníkem" | Provize neexistuje; seed sazby 0,5 × `BasePrice` (`junior template`); faktura = `SubTotal + BonusAmount − DeductionAmount` | [insert_seed_data.sql:781](../../sql-scripts/insert_seed_data.sql#L781); [EmployeeInvoice.cs:200-205](../../src/Cleansia.Core.Domain/EmployeePayroll/EmployeeInvoice.cs#L200-L205) | rozpor | → R6 |
| [RS P120](../analyza-2026-09-11/podklady/cleansia_ramcova_smlouva.txt) | 7.4 | „Vyúčtování probíhá vždy k [doplnit, např. 15. a k poslednímu] dni … Čistá odměna je odeslána … do [doplnit, např. 3] pracovních dnů" | Období jsou 1.–14. a 15.–konec měsíce (`CreateMonthlyFirstHalf`, `CreateMonthlySecondHalf`); den výplaty kód nezná, splatnost faktury počítá `CalculateDueDate` z konfigurace | [PayPeriod.cs:61-74](../../src/Cleansia.Core.Domain/EmployeePayroll/PayPeriod.cs#L61-L74); [EmployeeInvoice.cs:372-376](../../src/Cleansia.Core.Domain/EmployeePayroll/EmployeeInvoice.cs#L372-L376) | částečně | text |
| [RS P122](../analyza-2026-09-11/podklady/cleansia_ramcova_smlouva.txt) | 7.5 | „(a) Zhotovitel je povinen tuto skutečnost neprodleně potvrdit v Aplikaci" | `MarkCashCollected` zapíše `CashCollectedAt` bez částky; `OrderEmployeePay` má `BasePay`, `BonusPay` a `DeductionPay`, žádný hotovostní člen | [Order.cs:50-52](../../src/Cleansia.Core.Domain/Orders/Order.cs#L50-L52); [OrderEmployeePay.cs:38-58](../../src/Cleansia.Core.Domain/EmployeePayroll/OrderEmployeePay.cs#L38-L58) | částečně | → R6 |
| [RS P124](../analyza-2026-09-11/podklady/cleansia_ramcova_smlouva.txt) | 7.6 | „Je-li Zhotovitel nebo se teprve stane plátcem DPH, je povinen tuto skutečnost neprodleně oznámit" | `cleanersAreVatPayers = false` natvrdo; DIČ úklidníka se na faktuře nepoužije | [FileExtensions.cs:119](../../src/Cleansia.Core.AppServices/Extensions/FileExtensions.cs#L119); [FileExtensions.cs:132](../../src/Cleansia.Core.AppServices/Extensions/FileExtensions.cs#L132) | rozpor | → R16 |
| [RS P132](../analyza-2026-09-11/podklady/cleansia_ramcova_smlouva.txt) | 8.2 | „Veškerá komunikace … musí probíhat výhradně prostřednictvím rozhraní Aplikace" | Kanál úklidník–zákazník neexistuje; každý z `AssignedEmployees` vidí telefon zákazníka, zákazník jen `FirstName` | [OrderAccessService.cs:83-94](../../src/Cleansia.Core.AppServices/Authentication/OrderAccessService.cs#L83-L94); [OrderMappers.cs:379-380](../../src/Cleansia.Core.AppServices/Mappers/OrderMappers.cs#L379-L380) | rozpor | text |
| [RS P144](../analyza-2026-09-11/podklady/cleansia_ramcova_smlouva.txt) | 9.2 | „Povinnost mlčenlivosti trvá … po dobu deseti (10) let od ukončení této Smlouvy" | Návrhy mají tři délky: DPA na dobu neurčitou, Kodex dva roky po ukončení; v kódu žádná lhůta ani kontrola | [DPA P105](../analyza-2026-09-11/podklady/cleansia_dpa.txt); [Kodex P130](../analyza-2026-09-11/podklady/cleansia_kodex_chovani.txt) | rozpor | text |
| [RS P146](../analyza-2026-09-11/podklady/cleansia_ramcova_smlouva.txt) | 9.3 | „(minimálně jméno, adresa výkonu zakázky a kontaktní telefon)" | Každý z `AssignedEmployees` dostává i e-mail a `AccessInstructions` bez odhalovacího kroku | [OrderAccessService.cs:83-94](../../src/Cleansia.Core.AppServices/Authentication/OrderAccessService.cs#L83-L94); [GetOrderDetails.cs:155-160](../../src/Cleansia.Core.AppServices/Features/Orders/GetOrderDetails.cs#L155-L160) | částečně | → R8 |
| [RS P148](../analyza-2026-09-11/podklady/cleansia_ramcova_smlouva.txt) | 9.4 | „Bez platně podepsané DPA není Operátor oprávněn zpřístupnit Zhotoviteli osobní údaje Zákazníků" | Dokument publika `employee` není seedován; `ApproveEmployee` souhlasy nekontroluje | [LegalSeedResource.cs:28](../../src/Cleansia.Infra.Database/Seed/Legal/LegalSeedResource.cs#L28); [ApproveEmployee.cs:19-31](../../src/Cleansia.Core.AppServices/Features/Employees/ApproveEmployee.cs#L19-L31) | rozpor | → R8 |
| [RS P152](../analyza-2026-09-11/podklady/cleansia_ramcova_smlouva.txt) | 9.6 | „smluvní pokutu ve výši 50 000 Kč za každý jednotlivý případ porušení" | Týž skutek nese pokutu dvakrát: DPA ukládá za porušení mlčenlivosti dalších 50 000 Kč; v kódu pokuta není | [DPA P178](../analyza-2026-09-11/podklady/cleansia_dpa.txt) | rozpor | text |
| [RS P160](../analyza-2026-09-11/podklady/cleansia_ramcova_smlouva.txt) | 10.3 | „opakující se (minimálně dvakrát v období 90 dnů) oprávněné reklamace kvality" | `ComplaintsCount` nemá zvyšující zapisovač; `SubmitOrderReview` předává stávající hodnotu | [SubmitOrderReview.cs:227](../../src/Cleansia.Core.AppServices/Features/Orders/SubmitOrderReview.cs#L227); [Employee.cs:59](../../src/Cleansia.Core.Domain/Users/Employee.cs#L59) | rozpor | provoz |
| [RS P164](../analyza-2026-09-11/podklady/cleansia_ramcova_smlouva.txt) | 10.5 | „omezit nebo trvale zablokovat přístup Zhotovitele k novým zakázkám" | Jen `Approve`/`Reject`; `ContractStatus.Terminated` bez zapisovače; zamítnutí uvolní sedadla jen u budoucích `Confirmed` | [Employee.cs:324-350](../../src/Cleansia.Core.Domain/Users/Employee.cs#L324-L350); [RejectEmployee.cs:113-116](../../src/Cleansia.Core.AppServices/Features/Employees/RejectEmployee.cs#L113-L116) | částečně | provoz |
| [RS P178](../analyza-2026-09-11/podklady/cleansia_ramcova_smlouva.txt) | 12.1 | „Po dobu [doplnit, doporučujeme 12 měsíců] od ukončení spolupráce … nebude … zakládat, provozovat … konkurenční platformy" | Konkurenční doložku ani zákaz obcházení kód nezná; pokutu 150 000 Kč z P184 nic nevede | [RS P184](../analyza-2026-09-11/podklady/cleansia_ramcova_smlouva.txt) | rozpor | text |
| [RS P200](../analyza-2026-09-11/podklady/cleansia_ramcova_smlouva.txt) | 13.7 | „Zhotovitel v rámci registračního procesu v Aplikaci elektronicky potvrdí souhlas s jejím zněním" | `RegisterEmployee` zapisuje souhlasy bez dokumentu (`null`); `TermsAccepted` se nevyžaduje | [RegisterEmployee.cs:121-127](../../src/Cleansia.Core.AppServices/Features/Auth/RegisterEmployee.cs#L121-L127); [RegisterEmployee.cs:68-70](../../src/Cleansia.Core.AppServices/Features/Auth/RegisterEmployee.cs#L68-L70) | rozpor | → R3 |
| [RS P209](../analyza-2026-09-11/podklady/cleansia_ramcova_smlouva.txt) | Přílohy | „NUTNO DODAT před první zakázkou" | `LegalDocumentAudience.Employee` deklarován; žádný text pro úklidníka ve stromu | [LegalDocumentAudience.cs:13](../../src/Cleansia.Core.Domain/Legal/LegalDocumentAudience.cs#L13) | rozpor | → R8 |

### 1.4 Kodex

Shoda: [Kodex P071](../analyza-2026-09-11/podklady/cleansia_kodex_chovani.txt), [Kodex P115](../analyza-2026-09-11/podklady/cleansia_kodex_chovani.txt)

Mimo aplikaci (nic v kódu): P016–P023, P029–P032, P038–P058, P060, P063–P076, P082–P091, P104–P114, P118–P128, P132–P134, P143–P148, P158–P202

| Pxxx | čl. | citát | kód | citace | třída | řešení |
|---|---|---|---|---|---|---|
| [Kodex P034](../analyza-2026-09-11/podklady/cleansia_kodex_chovani.txt) | 2.3 | „oznámit Operátorovi prostřednictvím Aplikace a vyžádat doplnění" | Vůně ani prostředky nemají pole na objednávce a doplnění ceny nemá zápis; `ReportOrderIssue.Handler` nikoho neupozorní | [ReportOrderIssue.cs:42-44](../../src/Cleansia.Core.AppServices/Features/Orders/ReportOrderIssue.cs#L42-L44) | rozpor | → R14 |
| [Kodex P059](../analyza-2026-09-11/podklady/cleansia_kodex_chovani.txt) | 3.4 | „Potvrdí ukončení zakázky v Aplikaci — s fotografiemi výsledku, pokud to zakázka vyžaduje" | Dokončení vyžaduje ≥ 1 fotografii `After` vždy (`HasAfterPhotosAsync`) | [CompleteOrder.cs:183-188](../../src/Cleansia.Core.AppServices/Features/Orders/CompleteOrder.cs#L183-L188) | částečně | → R7 |
| [Kodex P064](../analyza-2026-09-11/podklady/cleansia_kodex_chovani.txt) | III | „Nepřijímejte spropitné ani platby mimo Aplikaci" | Hotovost přebírá přiřazený úklidník ve stavu `InProgress` (`OrderIsInProgressAsync`); `CashCollectedAt` je bez částky a bez započtení proti odměně | [MarkCashCollected.cs:55-70](../../src/Cleansia.Core.AppServices/Features/Orders/MarkCashCollected.cs#L55-L70); [Order.cs:50-52](../../src/Cleansia.Core.Domain/Orders/Order.cs#L50-L52) | rozpor | → R6 |
| [Kodex P100](../analyza-2026-09-11/podklady/cleansia_kodex_chovani.txt) | 5.1 | „nad 30 minut je Operátor oprávněn zakázku přesunout na jiného Zhotovitele" | Zpoždění přiřazeného se nedetekuje; sweep ruší 30 min (`GraceMinutes`) po začátku jen zakázku bez posádky ve stavech `NeverStarted` | [CancelUnfilledOrders.cs:63](../../src/Cleansia.Core.AppServices/Features/Orders/CancelUnfilledOrders.cs#L63); [CancelUnfilledOrders.cs:85](../../src/Cleansia.Core.AppServices/Features/Orders/CancelUnfilledOrders.cs#L85) | částečně | → R10 |
| [Kodex P140](../analyza-2026-09-11/podklady/cleansia_kodex_chovani.txt) | 7.2 | „dočasně pozastavit přístup Zhotovitele k novým zakázkám až na 14 dnů" | `ContractStatus` má `Pending`, `Active`, `Terminated`, `Approved`, `Rejected`; pozastavení neexistuje | [ContractStatus.cs:8-12](../../src/Cleansia.Core.Domain/Enums/ContractStatus.cs#L8-L12) | rozpor | provoz |
| [Kodex P205](../analyza-2026-09-11/podklady/cleansia_kodex_chovani.txt) | VIII | „Tento Kodex je platný po dobu 12 měsíců od data podpisu" | Dokument publika `Employee` deklarován, žádná složka seedu | [LegalDocumentAudience.cs:13](../../src/Cleansia.Core.Domain/Legal/LegalDocumentAudience.cs#L13) | rozpor | → R3 |

### 1.5 BOZP

Shoda: žádný odstavec nemá nosič v kódu

Mimo aplikaci (nic v kódu): P013–P030, P035–P045, P050–P074, P076–P098, P101–P109, P113–P124, P131–P169

| Pxxx | čl. | citát | kód | citace | třída | řešení |
|---|---|---|---|---|---|---|
| [BOZP P032](../analyza-2026-09-11/podklady/cleansia_bozp.txt) | 1.4 | „O každém incidentu neprodleně informujte Operátora" | `ReportOrderIssue.Handler` má jen `IOrderRepository` a `IOrderAccessService`, žádného notifikátora | [ReportOrderIssue.cs:42-44](../../src/Cleansia.Core.AppServices/Features/Orders/ReportOrderIssue.cs#L42-L44) | rozpor | → R10 |
| [BOZP P047](../analyza-2026-09-11/podklady/cleansia_bozp.txt) | 2.3 | „odmítnout jeho použití, informovat Zákazníka a tuto skutečnost zaznamenat" | Odmítnutí spotřebiče se nezaznamenává; jediný volný text je `Content` poznámky a `Description` hlášení, obojí bez adresáta | [AddOrderNote.cs:37](../../src/Cleansia.Core.AppServices/Features/Orders/AddOrderNote.cs#L37); [ReportOrderIssue.cs:34-38](../../src/Cleansia.Core.AppServices/Features/Orders/ReportOrderIssue.cs#L34-L38) | rozpor | → R10 |
| [BOZP P099](../analyza-2026-09-11/podklady/cleansia_bozp.txt) | 5.3 | „informovat Operátora neprodleně prostřednictvím Aplikace nebo telefonu" | Devět administrátorských událostí od `OrderNew` po `CompanyArchived`; hlášený incident mezi nimi není | [AdminEventCatalog.cs:19-39](../../src/Cleansia.Core.AppServices/Features/AdminNotifications/AdminEventCatalog.cs#L19-L39) | rozpor | → R10 |
| [BOZP P110](../analyza-2026-09-11/podklady/cleansia_bozp.txt) | 5.5 | „Operátor je povinen uchovávat záznamy o úrazech osob pracujících prostřednictvím platformy" | Záznam o úrazu jako entita neexistuje; `EmployeeDocument` nemá platnost a typy jdou od `IdentityCard` po `Other` | [EmployeeDocument.cs:10-40](../../src/Cleansia.Core.Domain/Documents/EmployeeDocument.cs#L10-L40); [DocumentType.cs:8-17](../../src/Cleansia.Core.Domain/Enums/DocumentType.cs#L8-L17) | rozpor | → R10 |
| [BOZP P117](../analyza-2026-09-11/podklady/cleansia_bozp.txt) | 6.3 | „zdokumentovat … preexistující poškození viditelná pouhým okem" | `PhotoType.Before` deklarován, nevyžadován nikde; `StartOrder` fotografie nekontroluje | [PhotoType.cs:8](../../src/Cleansia.Core.Domain/Enums/PhotoType.cs#L8); [StartOrder.cs:43-63](../../src/Cleansia.Core.AppServices/Features/Orders/StartOrder.cs#L43-L63) | rozpor | → R7 |

### 1.6 PP

Shoda: [PP P038](../analyza-2026-09-11/podklady/cleansia_privacy_policy.txt), [PP P043](../analyza-2026-09-11/podklady/cleansia_privacy_policy.txt), [PP P048](../analyza-2026-09-11/podklady/cleansia_privacy_policy.txt), [PP P053](../analyza-2026-09-11/podklady/cleansia_privacy_policy.txt), [PP P086](../analyza-2026-09-11/podklady/cleansia_privacy_policy.txt), [PP P088](../analyza-2026-09-11/podklady/cleansia_privacy_policy.txt), [PP P107](../analyza-2026-09-11/podklady/cleansia_privacy_policy.txt), [PP P112](../analyza-2026-09-11/podklady/cleansia_privacy_policy.txt), [PP P121](../analyza-2026-09-11/podklady/cleansia_privacy_policy.txt), [PP P129](../analyza-2026-09-11/podklady/cleansia_privacy_policy.txt)

Mimo aplikaci (nic v kódu): P026, P072, P097–P099, P110–P117, P123–P135, P154

| Pxxx | čl. | citát | kód | citace | třída | řešení |
|---|---|---|---|---|---|---|
| [PP P020](../analyza-2026-09-11/podklady/cleansia_privacy_policy.txt) | 1.1 | „Správcem vašich osobních údajů je společnost Cleansia s.r.o." | Správcem je provozní společnost trhu (`Tenants` jediný řádek `cleansia-cz`, jméno „Cleansia CZ s.r.o.") | [insert_seed_data.sql:63-64](../../sql-scripts/insert_seed_data.sql#L63-L64) | částečně | → R2 |
| [PP P041](../analyza-2026-09-11/podklady/cleansia_privacy_policy.txt) | II | „Po dobu trvání účtu + 3 roky po jeho zrušení (účetní povinnost)" | `RetentionDefaults` zná jiná okna: PII objednávky 2 roky (`DefaultOrderPiiYears`), audit 3 roky; `Anonymize()` přepíše jméno i e-mail ihned při výmazu | [RetentionDefaults.cs:16-25](../../src/Cleansia.Core.AppServices/Features/DataRetention/RetentionDefaults.cs#L16-L25); [User.cs:427-449](../../src/Cleansia.Core.Domain/Users/User.cs#L427-L449) | rozpor | → R9 |
| [PP P046](../analyza-2026-09-11/podklady/cleansia_privacy_policy.txt) | II | „Po dobu zakázky; fakturační adresa 10 let (zákon č. 563/1991 Sb.)" | Sweep anonymizuje adresu objednávky po 2 letech (`DefaultOrderPiiYears`); `Anonymize()` přepíše sdílený řádek adresy i sousedním objednávkám | [RetentionDefaults.cs:19](../../src/Cleansia.Core.AppServices/Features/DataRetention/RetentionDefaults.cs#L19); [Address.cs:55-64](../../src/Cleansia.Core.Domain/Users/Address.cs#L55-L64) | rozpor | → R9 |
| [PP P056](../analyza-2026-09-11/podklady/cleansia_privacy_policy.txt) | II | „3 roky od poslední aktivity" | Audit zákazníka se maže per řádek (`CustomerAuditRetentionYears`), ne od poslední aktivity; sweep bere jen objednávky s historií `OrderStatus.Completed` | [DataRetentionBackgroundService.cs:319-325](../../src/Cleansia.Core.AppServices/Features/DataRetention/DataRetentionBackgroundService.cs#L319-L325); [DataRetentionBackgroundService.cs:172-174](../../src/Cleansia.Core.AppServices/Features/DataRetention/DataRetentionBackgroundService.cs#L172-L174) | částečně | → R9 |
| [PP P061](../analyza-2026-09-11/podklady/cleansia_privacy_policy.txt) | II | „12 měsíců" | Telemetrie `retentionInDays` 90 dní v PRO a 30 v DEV; dvanáctiměsíční okno technických dat nikde | [appInsights.bicep:85](../../deploy/bicep/modules/appInsights.bicep#L85) | rozpor | → R9 |
| [PP P063](../analyza-2026-09-11/podklady/cleansia_privacy_policy.txt) | II | „poloha zařízení (pouze při aktivním používání Aplikace)" | Zákaznická aplikace posílá souřadnice zařízení v `AddSavedAddressCommand`; `Latitude` a `Longitude` jsou sloupce adresy a zůstanou na ní | [Address.cs:24-25](../../src/Cleansia.Core.Domain/Users/Address.cs#L24-L25); [UserAddress.kt:82-90](../../src/cleansia_android/customer-app/src/main/java/cz/cleansia/customer/core/data/UserAddress.kt#L82-L90) | rozpor | → R9 |
| [PP P068](../analyza-2026-09-11/podklady/cleansia_privacy_policy.txt) | II | „e-mail pro zasílání novinek a nabídek (jen s explicitním souhlasem)" | `ConsentType.MarketingEmails` nemá v backendu čtenáře; deset typů e-mailu jde od `ConfirmationEmail` po `AdminNotification`, marketingový mezi nimi není | [ConsentType.cs:10](../../src/Cleansia.Core.Domain/Enums/ConsentType.cs#L10); [EmailType.cs:8-28](../../src/Cleansia.Core.Domain/Enums/EmailType.cs#L8-L28) | rozpor | → R9 |
| [PP P076](../analyza-2026-09-11/podklady/cleansia_privacy_policy.txt) | 3.1 | „Cookie Policy dostupné na [doplnit URL]" | Web nemá stránku cookie policy; lišta ukládá volbu do `localStorage` klíče `cleansia-customer-cookie-consent` | [app.html:16](../../src/Cleansia.App/apps/cleansia.app/src/app/app.html#L16) | rozpor | → R9 |
| [PP P079](../analyza-2026-09-11/podklady/cleansia_privacy_policy.txt) | 3.2 | „(např. Google Analytics). Aktivují se až po vašem souhlasu" | Google Analytics ani gtag na zákaznickém webu nejsou; Google Fonts se načítají před volbou | [index.html:50](../../src/Cleansia.App/apps/cleansia.app/src/index.html#L50); [index.html:43-49](../../src/Cleansia.App/apps/cleansia.app/src/index.html#L43-L49) | rozpor | → R9 |
| [PP P082](../analyza-2026-09-11/podklady/cleansia_privacy_policy.txt) | 3.3 | „nastavení v Aplikaci (Nastavení → Soukromí → Cookies)" | Uloženou volbu nic nečte (`hasAcceptedCookies`, `getPreferences` bez volajících) | [cleansia-cookie-consent.component.ts:184-198](../../src/Cleansia.App/libs/shared/components/src/lib/cleansia-cookie-consent/cleansia-cookie-consent.component.ts#L184-L198) | rozpor | → R9 |
| [PP P140](../analyza-2026-09-11/podklady/cleansia_privacy_policy.txt) | 7.1 | „Při registraci ověřujeme věk zákazníka" | Registrace věk nesbírá; `Register.Command` bez `BirthDate` | [Register.cs:73-89](../../src/Cleansia.Core.AppServices/Features/Auth/Register.cs#L73-L89) | rozpor | → R9 |
| [PP P146](../analyza-2026-09-11/podklady/cleansia_privacy_policy.txt) | 8.1 | „Cleansia nevyužívá plně automatizované rozhodování" | Rezervaci pro oblíbeného úklidníka (`PreferredHoldFraction`) i filtr nástěnky (`PayableTo`) rozhoduje kód bez člověka | [BookingPolicy.cs:216-217](../../src/Cleansia.Core.AppServices/Features/Orders/BookingPolicy.cs#L216-L217); [OrderVisibility.cs:53-59](../../src/Cleansia.Core.Domain/Orders/OrderVisibility.cs#L53-L59) | částečně | → R9 |

### 1.7 CP

Shoda: [CP P021](../analyza-2026-09-11/podklady/cleansia_cookie_policy.txt), [CP P023](../analyza-2026-09-11/podklady/cleansia_cookie_policy.txt), [CP P150](../analyza-2026-09-11/podklady/cleansia_cookie_policy.txt)

Mimo aplikaci (nic v kódu): P113–P118, P126–P135, P238–P240

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

Shoda: [DPA P066](../analyza-2026-09-11/podklady/cleansia_dpa.txt), [DPA P084](../analyza-2026-09-11/podklady/cleansia_dpa.txt), [DPA P088](../analyza-2026-09-11/podklady/cleansia_dpa.txt), [DPA P092](../analyza-2026-09-11/podklady/cleansia_dpa.txt), [DPA P144](../analyza-2026-09-11/podklady/cleansia_dpa.txt), [DPA P146](../analyza-2026-09-11/podklady/cleansia_dpa.txt)

Mimo aplikaci (nic v kódu): P071–P073, P105–P113, P123–P127, P136–P138, P150–P154, P164–P168, P178–P180, P198

| Pxxx | čl. | citát | kód | citace | třída | řešení |
|---|---|---|---|---|---|---|
| [DPA P017](../analyza-2026-09-11/podklady/cleansia_dpa.txt) | úvod | „Zpracování osobních údajů bez platné DPA je porušením GDPR" | Podepsaná DPA není podmínkou schválení úklidníka ani přístupu k objednávce | [ApproveEmployee.cs:19-31](../../src/Cleansia.Core.AppServices/Features/Employees/ApproveEmployee.cs#L19-L31); [OrderAccessService.cs:88-94](../../src/Cleansia.Core.AppServices/Authentication/OrderAccessService.cs#L88-L94) | rozpor | → R8 |
| [DPA P070](../analyza-2026-09-11/podklady/cleansia_dpa.txt) | 2.3 | „výhradně za účelem splnění konkrétní úklidové zakázky, která mu byla přidělena prostřednictvím Aplikace" | Přístup drží, dokud existuje sedadlo (`AssignedEmployees.Any`), i po dokončení; token partnerského webu 1 440 min | [OrderVisibility.cs:53-55](../../src/Cleansia.Core.Domain/Orders/OrderVisibility.cs#L53-L55); [appsettings.json:20](../../src/Cleansia.Web.Partner/appsettings.json#L20) | rozpor | → R8 |
| [DPA P086](../analyza-2026-09-11/podklady/cleansia_dpa.txt) | III | „Po dobu zakázky + 24 h" | Žádný časový člen v přístupu; čte se jen sedadlo v `AssignedEmployees` | [OrderAccessService.cs:94](../../src/Cleansia.Core.AppServices/Authentication/OrderAccessService.cs#L94) | rozpor | → R8 |
| [DPA P094](../analyza-2026-09-11/podklady/cleansia_dpa.txt) | III | „Po dobu zakázky — po skončení neprodleně vymazat" | `AccessInstructions` vidí přiřazený bez odhalovacího kroku a bez konce; administrátor je odhaluje auditovaně | [GetOrderDetails.cs:155-160](../../src/Cleansia.Core.AppServices/Features/Orders/GetOrderDetails.cs#L155-L160); [RevealOrderAccessInstructions.cs:26](../../src/Cleansia.Core.AppServices/Features/Orders/RevealOrderAccessInstructions.cs#L26) | rozpor | → R8 |
| [DPA P098](../analyza-2026-09-11/podklady/cleansia_dpa.txt) | III | „Max. 30 dní od ukončení zakázky" | Deset úloh sweepu jde od `ExpiredUserCodes` po `WorkContractAcceptanceMetadata`, fotografie mezi nimi nejsou; blob `OrderPhotos` mizí jen při výmazu účtu | [DataRetentionBackgroundService.cs:64-73](../../src/Cleansia.Core.AppServices/Features/DataRetention/DataRetentionBackgroundService.cs#L64-L73); [GdprDeletionService.cs:362-371](../../src/Cleansia.Core.AppServices/Services/GdprDeletionService.cs#L362-L371) | rozpor | → R7 |
| [DPA P115](../analyza-2026-09-11/podklady/cleansia_dpa.txt) | 4.3 | „Správce není oprávněn prostřednictvím Aplikace sledovat polohu Zhotovitele mimo dobu aktivní zakázky" | Poloha úklidníka se na server neposílá; geokodér volá Mapbox ze zařízení | [ReverseGeocodingService.kt:28](../../src/cleansia_android/core/src/main/java/cz/cleansia/core/location/ReverseGeocodingService.kt#L28) | částečně | → R9 |
| [DPA P117](../analyza-2026-09-11/podklady/cleansia_dpa.txt) | 4.4 | „nesmí bez předchozího písemného souhlasu Správce pověřit zpracováním osobních údajů Zákazníků dalšího zpracovatele" | Každé místo v posádce je samostatný účet s vlastním přístupem; substituce v datech neexistuje | [WorkContractAcceptance.cs:31](../../src/Cleansia.Core.Domain/Contracts/WorkContractAcceptance.cs#L31) | částečně | → R13 |
| [DPA P134](../analyza-2026-09-11/podklady/cleansia_dpa.txt) | 4.7 | „doručí ho Správci nejpozději do 14 dnů od ukončení spolupráce" | Potvrzení výmazu ze zařízení úklidníka aplikace nesbírá; mezi typy dokladů od `IdentityCard` po `Other` takový není | [DocumentType.cs:8-17](../../src/Cleansia.Core.Domain/Enums/DocumentType.cs#L8-L17) | rozpor | provoz |
| [DPA P140](../analyza-2026-09-11/podklady/cleansia_dpa.txt) | 4.10 | „zajistí lidský dohled nad automatizovanými rozhodnutími … na přístup Zpracovatele k zakázkám" | Nástěnka filtruje měnou úklidníka bez člověka; osm push klíčů je nevypnutelných (`GetCategoryFor` → `null`) | [OrderVisibility.cs:53-59](../../src/Cleansia.Core.Domain/Orders/OrderVisibility.cs#L53-L59); [NotificationEventCatalog.cs:228-253](../../src/Cleansia.Core.Domain/Notifications/NotificationEventCatalog.cs#L228-L253) | částečně | → R8 |
| [DPA P166](../analyza-2026-09-11/podklady/cleansia_dpa.txt) | 7.2 | „Správce je oprávněn v takovém případě vzdáleně odhlásit zařízení z Aplikace" | Zařízení se `Deactivate` a `RevokeByDeviceAsync` odvolá jeho tokeny; úkon zadává sám úklidník, administrátorský povrch chybí | [RevokeDevice.cs:44-52](../../src/Cleansia.Core.AppServices/Features/Devices/RevokeDevice.cs#L44-L52) | částečně | provoz |

### 1.9 GDPR

Shoda: [GDPR P020](../analyza-2026-09-11/podklady/cleansia_gdpr_souhlasy.txt), [GDPR P061](../analyza-2026-09-11/podklady/cleansia_gdpr_souhlasy.txt), [GDPR P069](../analyza-2026-09-11/podklady/cleansia_gdpr_souhlasy.txt), [GDPR P106](../analyza-2026-09-11/podklady/cleansia_gdpr_souhlasy.txt), [GDPR P107](../analyza-2026-09-11/podklady/cleansia_gdpr_souhlasy.txt)

Mimo aplikaci (nic v kódu): P188

| Pxxx | čl. | citát | kód | citace | třída | řešení |
|---|---|---|---|---|---|---|
| [GDPR P036](../analyza-2026-09-11/podklady/cleansia_gdpr_souhlasy.txt) | I | „Po dobu zakázky + 24 h (adresa)" | Adresa objednávky se anonymizuje po 2 letech (`DefaultOrderPiiYears`), jen u historie `OrderStatus.Completed` | [RetentionDefaults.cs:19](../../src/Cleansia.Core.AppServices/Features/DataRetention/RetentionDefaults.cs#L19); [DataRetentionBackgroundService.cs:172-174](../../src/Cleansia.Core.AppServices/Features/DataRetention/DataRetentionBackgroundService.cs#L172-L174) | rozpor | → R8 |
| [GDPR P050](../analyza-2026-09-11/podklady/cleansia_gdpr_souhlasy.txt) | I | „Souhlas čl. 6(1)(a) GDPR" | Push řídí `UserNotificationPreferences` (12 kategorií, vše `true` kromě `Promo`), ne souhlas | [UserNotificationPreferences.cs:18-34](../../src/Cleansia.Core.Domain/Notifications/UserNotificationPreferences.cs#L18-L34) | rozpor | → R9 |
| [GDPR P063](../analyza-2026-09-11/podklady/cleansia_gdpr_souhlasy.txt) | II | „Souhlasím se zpracováním svých osobních údajů za účelem realizace úklidových zakázek" | Registrace zapisuje dva souhlasy (`TermsOfService`, `PrivacyPolicy`); třetí checkbox neexistuje | [Register.cs:146-155](../../src/Cleansia.Core.AppServices/Features/Auth/Register.cs#L146-L155) | rozpor | text |
| [GDPR P086](../analyza-2026-09-11/podklady/cleansia_gdpr_souhlasy.txt) | 5.1 | „Cleansia musí implementovat double opt-in" | Marketingový e-mail neexistuje; `MarketingEmails` vzniká jen přes `TryGrantAsync` a nikdo ho nečte | [GrantConsent.cs:50](../../src/Cleansia.Core.AppServices/Features/Gdpr/GrantConsent.cs#L50); [ConsentType.cs:10](../../src/Cleansia.Core.Domain/Enums/ConsentType.cs#L10) | rozpor | → R9 |
| [GDPR P090](../analyza-2026-09-11/podklady/cleansia_gdpr_souhlasy.txt) | 5.3 | „V sekci Nastavení → Oznámení a soukromí musí být zákazníkovi dostupné přepínače (toggles)" | Zákazník mění 11 přepínačů od `OrderUpdates` po `RecurringScheduled`; partnerští hostitelé takový příkaz ani obrazovku nemají | [UpdateNotificationPreferences.cs:21-32](../../src/Cleansia.Core.AppServices/Features/Notifications/UpdateNotificationPreferences.cs#L21-L32) | částečně | → R18 |
| [GDPR P096](../analyza-2026-09-11/podklady/cleansia_gdpr_souhlasy.txt) | 5.4 | „Pro zákazníky mladší 16 let je vyžadován souhlas zákonného zástupce" | Věk zákazníka nemá dolní hranici, jen strop 120 let; povinných 18 let (`BeReasonableAge`) má úklidník i administrátor | [UpdateCurrentUser.cs:108-112](../../src/Cleansia.Core.AppServices/Features/Users/UpdateCurrentUser.cs#L108-L112); [CreateAdminUser.cs:72](../../src/Cleansia.Core.AppServices/Features/AdminUsers/CreateAdminUser.cs#L72) | rozpor | → R9 |
| [GDPR P108](../analyza-2026-09-11/podklady/cleansia_gdpr_souhlasy.txt) | 6.2 | „způsob udělení (checkbox, API call, OS dialog)" | `UserConsent` nese jen `IpAddress` a `UserAgent`, bez klienta a id zařízení | [UserConsent.cs:23-27](../../src/Cleansia.Core.Domain/Users/UserConsent.cs#L23-L27) | částečně | → R3 |
| [GDPR P110](../analyza-2026-09-11/podklady/cleansia_gdpr_souhlasy.txt) | 6.2 | „hash nebo plný text souhlasu platný v okamžiku udělení (SHA-256)" | Řádek souhlasu odkazuje `LegalDocumentId` a `DocumentVersion`; hash je na `LegalDocumentText.ContentHash` | [UserConsent.cs:15-38](../../src/Cleansia.Core.Domain/Users/UserConsent.cs#L15-L38); [LegalDocumentText.cs:20-33](../../src/Cleansia.Core.Domain/Legal/LegalDocumentText.cs#L20-L33) | částečně | text |
| [GDPR P112](../analyza-2026-09-11/podklady/cleansia_gdpr_souhlasy.txt) | 6.3 | „immutable záznamy — žádné UPDATE operace, pouze INSERT" | Jeden řádek na (uživatel, typ), unikátní index `UserId + ConsentType`; `AcceptVersion` řádek přepíše | [UserConsentEntityConfiguration.cs:42-43](../../src/Cleansia.Infra.Database/EntityConfigurations/UserConsentEntityConfiguration.cs#L42-L43); [UserConsent.cs:80-85](../../src/Cleansia.Core.Domain/Users/UserConsent.cs#L80-L85) | rozpor | → R3 |
| [GDPR P140](../analyza-2026-09-11/podklady/cleansia_gdpr_souhlasy.txt) | 7.4 | „Záznamy v Consent Logu musí být anonymizovány (nikoliv smazány" | Odvolané souhlasy se po třech letech mažou: `CleanWithdrawnConsentsAsync` je odstraní (`RemoveRange`), neanonymizuje | [DataRetentionBackgroundService.cs:200-214](../../src/Cleansia.Core.AppServices/Features/DataRetention/DataRetentionBackgroundService.cs#L200-L214) | rozpor | → R9 |
| [GDPR P150](../analyza-2026-09-11/podklady/cleansia_gdpr_souhlasy.txt) | VIII | „Registrační formulář má 2 povinné + 2 volitelné checkboxy (žádný předvyplněný)" | Registrace nese jediné `TermsAccepted`; volitelné souhlasy se udělují až v aplikaci (`GrantConsent`) | [Register.cs:55-58](../../src/Cleansia.Core.AppServices/Features/Auth/Register.cs#L55-L58); [GdprController.cs:45-46](../../src/Cleansia.Web.Customer/Controllers/GdprController.cs#L45-L46) | částečně | → R3 |

### 1.10 SS

Shoda: [SS P020](../analyza-2026-09-11/podklady/cleansia_spolecenska_smlouva.txt), [SS P024](../analyza-2026-09-11/podklady/cleansia_spolecenska_smlouva.txt)

Mimo aplikaci (nic v kódu): P005–P017, P022–P023, P028–P055, P059–P091, P095–P113, P117–P131, P151

| Pxxx | čl. | citát | kód | citace | třída | řešení |
|---|---|---|---|---|---|---|
| [SS P015](../analyza-2026-09-11/podklady/cleansia_spolecenska_smlouva.txt) | 1.1 | „Firma společnosti je: Cleansia s.r.o." | Tři názvy: `Tenants` „Cleansia CZ s.r.o.", `CompanyInfo` „Cleansia s.r.o.", `FooterText` e-mailů „© Cleansia s.r.o." | [insert_seed_data.sql:64](../../sql-scripts/insert_seed_data.sql#L64); [EmailService.cs:691](../../src/Cleansia.Core.AppServices/Services/EmailService.cs#L691) | částečně | → R2 |
| [SS P021](../analyza-2026-09-11/podklady/cleansia_spolecenska_smlouva.txt) | 1.3 | „Poskytování úklidových služeb" | Seedovaná smlouva o dílo: `Cleansia` „aplikaci provozuje a není smluvní stranou"; seedované podmínky říkají opak | [cs.md:7](../../src/Cleansia.Infra.Database/Seed/Legal/customer/work-contract/any/2026-09-20/cs.md#L7); [cs.md:15](../../src/Cleansia.Infra.Database/Seed/Legal/customer/terms-of-service/any/2026-09-14/cs.md#L15) | částečně | → R1 |

## 2. Co v dokumentech chybí

| co kód dělá | citace | který dokument to má nést | → Rn |
|---|---|---|---|
| Ke každé zakázce vzniká záznam přijetí smlouvy o dílo na každé místo v posádce (`OrderEmployeeId`) | [WorkContractAcceptance.cs:25-45](../../src/Cleansia.Core.Domain/Contracts/WorkContractAcceptance.cs#L25-L45) | VOP čl. III, RS čl. II | → R11 |
| Kredit je platidlo per měna, `TotalPrice` nesnižuje, hradí nejvýše 70 % objednávky (`MaxCreditShareOfOrder`) | [BookingPolicy.cs:97](../../src/Cleansia.Core.AppServices/Features/Orders/BookingPolicy.cs#L97) | VOP čl. IV | → R4 |
| Posádka = ⌈odhad minut / 120⌉ (`MinutesPerEmployee`), rezervní místa `SpareSeatsPerOrder` 0 | [OrderDuration.cs:27](../../src/Cleansia.Core.Domain/Orders/OrderDuration.cs#L27); [BookingPolicy.cs:135](../../src/Cleansia.Core.AppServices/Features/Orders/BookingPolicy.cs#L135) | VOP čl. III | → R13 |
| Čtyři role administrátora `Administrator / Manager / Support / Accountant` s oddělenými oprávněními | [AdminRole.cs:14-17](../../src/Cleansia.Core.Domain/Enums/AdminRole.cs#L14-L17) | PP čl. V, DPA čl. IV | → R10 |
| Životní cyklus společnosti: útlum, deaktivace, zmrazení, archiv (`CompanyLifecycleState`) | [CompanyLifecycleState.cs:10-17](../../src/Cleansia.Core.Domain/Tenancy/CompanyLifecycleState.cs#L10-L17) | VOP čl. X, RS čl. X | → R2 |
| Devět retenčních oken per společnost (90 d / 3 r / 2 r / 365 d …) v `RetentionDefaults` | [RetentionDefaults.cs:16-25](../../src/Cleansia.Core.AppServices/Features/DataRetention/RetentionDefaults.cs#L16-L25) | PP čl. II | → R9 |
| Chargeback zakládá spor `Escalated` s důvodem `Chargeback`; zákazník se nedozví nic | [HandlePaymentNotification.cs:475-505](../../src/Cleansia.Core.AppServices/Features/Payments/HandlePaymentNotification.cs#L475-L505) | VOP čl. IV, RŘ čl. VI | → R18 |
| Host se k objednávce dostane trojicí číslo + e-mail + `ConfirmationCode` (6 hex znaků, čitelně) | [GuestOrderAccess.cs:12-24](../../src/Cleansia.Core.AppServices/Features/Orders/GuestOrderAccess.cs#L12-L24) | VOP čl. II | → R5 |
| Opakované úklidy: šablona (týdně/dvoutýdně/měsíčně), materializace `HorizonDays` 7 dní dopředu; bez Plus `RecurringTemplateMembershipRequired` | [CreateRecurringBooking.cs:184-194](../../src/Cleansia.Core.AppServices/Features/Bookings/CreateRecurringBooking.cs#L184-L194); [MaterializeRecurringBookings.cs:27](../../src/Cleansia.Core.AppServices/Features/Bookings/MaterializeRecurringBookings.cs#L27) | VOP čl. III | → R4 |
| Cleansia Plus: sleva 5 %, `FreeCancellationWindowHours` 4 h, 1 express měsíčně, bez zkušební doby (`TrialPeriodDays` 0) | [insert_seed_data.sql:1799-1807](../../sql-scripts/insert_seed_data.sql#L1799-L1807) | VOP čl. IV | → R4 |
| Doporučení: 150 bodů oběma stranám (`PointsPerSide`), okno 90 dní (`QualifyingWindowDays`) | [ReferralPolicy.cs:14-22](../../src/Cleansia.Core.AppServices/Features/Orders/ReferralPolicy.cs#L14-L22) | VOP čl. IV | → R17 |
| Promokódy: procentní i pevné, limit per uživatel a globální, vázané na měnu | [PromoCodeService.cs:242-262](../../src/Cleansia.Core.AppServices/Services/PromoCodeService.cs#L242-L262) | VOP čl. IV | → R4 |
| Věrnostní stupně Bronze/Silver/Gold/Platinum, prahy `LifetimePointsThreshold` 0/500/2000/5000 bodů, slevy 0–12 % | [insert_seed_data.sql:1670-1707](../../sql-scripts/insert_seed_data.sql#L1670-L1707) | VOP čl. IV | → R17 |
| Fotografii objednávky smaže kdokoli z `AssignedEmployees`, v jakémkoli stavu | [DeleteOrderPhoto.cs:56-71](../../src/Cleansia.Core.AppServices/Features/Orders/DeleteOrderPhoto.cs#L56-L71) | VOP čl. VI, RS čl. IX | → R7 |
| Export dat je JSON s 12 sekcemi (`GdprExportDto`), včetně objednávek hosta pod e-mailem účtu | [GdprExportDto.cs:5-18](../../src/Cleansia.Core.AppServices/Features/Gdpr/DTOs/GdprExportDto.cs#L5-L18) | PP čl. VI | → R9 |
| Výmaz blokuje živá objednávka (`GdprDeletionBlockedByOrder`) a u úklidníka otevřená faktura či nevyrovnaná odměna | [GdprDeletionService.cs:144-178](../../src/Cleansia.Core.AppServices/Services/GdprDeletionService.cs#L144-L178) | PP čl. VI | → R9 |
| Mobilní push: 12 kategorií, osm klíčů úklidníka nevypnutelných (`GetCategoryFor` → `null`) | [NotificationEventCatalog.cs:228-253](../../src/Cleansia.Core.Domain/Notifications/NotificationEventCatalog.cs#L228-L253) | GDPR čl. V, RS čl. XIII | → R18 |
| Objednávka mimo trh účtu patří společnosti trhu adresy (`OperatorTenantId`); operátora (`cleansia-cz`) má v seedu jen CZE, do jiného trhu ji kód odmítne | [CreateOrder.cs:933-935](../../src/Cleansia.Core.AppServices/Features/Orders/CreateOrder.cs#L933-L935); [insert_seed_data.sql:1017-1024](../../sql-scripts/insert_seed_data.sql#L1017-L1024) | VOP čl. I, PP čl. I | → R19 |

## 3. Rozhodnutí pro schůzku

### R1 — Obchodní model: kdo zákazníkovi poskytuje úklid a kdo komu fakturuje

Dodavatelský model, zprostředkovatelský (společnost fakturuje provizi), nebo hybrid. Doklad zákazníka nese `CompanyInfo` společnosti — [ReceiptService.cs:423-429](../../src/Cleansia.Core.AppServices/Services/ReceiptService.cs#L423-L429); výplatní faktura je samofakturace, dodavatel `CreateSupplierData` — [FileExtensions.cs:40-69](../../src/Cleansia.Core.AppServices/Extensions/FileExtensions.cs#L40-L69). Návrhy: [VOP P022](../analyza-2026-09-11/podklady/Cleansia_VOP.txt), [RS P114](../analyza-2026-09-11/podklady/cleansia_ramcova_smlouva.txt).

| varianta | odstavce návrhů | v kódu |
|---|---|---|
| dodavatelský model | VOP P022, RS P041, RS P114, RŘ P018 | nic |
| zprostředkovatelský model | RS P116 doplnit sazbu | `ReceiptService`, `CompanyInfo`, provizní člen v `PayCalculatorExtensions` |
| hybrid (příkazní smlouva) | VOP P022, RS P039, RS P114 | `ReceiptService`, nový typ dokladu |

**R2 — Kdo je „společnost": jedna s.r.o., nebo jedna na zemi** · rozhodnuto 2026-09-13 (ADR-0061): holding a provozní společnost na každý region; zbývá jeden název místo tří a skutečné IČO/DIČ místo `REPLACE WITH ACTUAL` — [insert_seed_data.sql:1158-1162](../../sql-scripts/insert_seed_data.sql#L1158-L1162).

### R3 — Jaké znění zákazník a úklidník přijímají a jak se prokáže

Finální znění jako datované složky; k tomu: vyžaduje nová verze VOP opětovné přijetí, je RŘ čtvrtým typem, co přijímá úklidník. Kód zná tři typy — [LegalDocumentType.cs:8-16](../../src/Cleansia.Core.Domain/Legal/LegalDocumentType.cs#L8-L16); objednávka projde s jakýmkoli neodvolaným souhlasem bez ohledu na verzi — [CreateOrder.cs:325-329](../../src/Cleansia.Core.AppServices/Features/Orders/CreateOrder.cs#L325-L329). Brána `AssertedOrAlreadyConsentedAsync` se spokojí s tvrzením klienta a uložený souhlas nečte; host žádný řádek souhlasu nemá — [CreateOrder.cs:312-317](../../src/Cleansia.Core.AppServices/Features/Orders/CreateOrder.cs#L312-L317). Návrhy: [VOP P014](../analyza-2026-09-11/podklady/Cleansia_VOP.txt), [RS P200](../analyza-2026-09-11/podklady/cleansia_ramcova_smlouva.txt).

| varianta | odstavce návrhů | v kódu |
|---|---|---|
| finální znění jako nové datované složky | VOP P014, PP P020 | `Seed/Legal/customer/…` |
| RŘ jako čtvrtý typ dokumentu | VOP P105, RŘ P137 | `LegalDocumentType`, seed, routa |
| partnerský dokument s verzí, přijímaný při schválení | RS P200, RS P209, Kodex P205 | `LegalDocumentAudience.Employee`, `RegisterEmployee`, `ApproveEmployee` |

### R4 — Jedna storno, cenová a kreditní politika

VOP převezmou žebříček kódu, nebo kód převezme VOP (50 %/100 %). Kód: `FreeCancellationHours = 24`, `PartialCancellationFeeRate = 0,25`, `LastMinuteCancellationFeeRate = 0,50` — [BookingPolicy.cs:64-79](../../src/Cleansia.Core.AppServices/Features/Orders/BookingPolicy.cs#L64-L79); okno 60 min je nedosažitelné (`IsFirstTimeCustomer`) — [CancellationAssessor.cs:32](../../src/Cleansia.Core.AppServices/Features/Orders/CancellationAssessor.cs#L32). Návrhy: [VOP P069](../analyza-2026-09-11/podklady/Cleansia_VOP.txt), [VOP P034](../analyza-2026-09-11/podklady/Cleansia_VOP.txt).

| varianta | odstavce návrhů | v kódu |
|---|---|---|
| VOP převezmou žebříček kódu, kredit jako bonus bez nároku | VOP P064–P078, P034, P123 | nic |
| kód převezme VOP včetně stavu „zákazník nedostupný" | žádný | `BookingPolicy`, `CancellationAssessor`, nový důvod storna |
| výplata kreditu při zrušení účtu | VOP P034 | `CreditAccount`, nový tok výplaty |

### R5 — Host bez účtu

Host zůstane a dokumenty se přizpůsobí, nebo se zruší a registrace bude povinná. `Order.UserId` je nullable — [Order.cs:222](../../src/Cleansia.Core.Domain/Orders/Order.cs#L222); host ruší trojicí číslo + e-mail + `ConfirmationCode` — [CancelGuestOrder.cs:18](../../src/Cleansia.Core.AppServices/Features/Orders/CancelGuestOrder.cs#L18) — spor nepodá, kredit ani body nedostane. Návrhy: [VOP P028](../analyza-2026-09-11/podklady/Cleansia_VOP.txt), [RŘ P020](../analyza-2026-09-11/podklady/cleansia_reklamacni_rad.txt).

| varianta | odstavce návrhů | v kódu |
|---|---|---|
| host zůstane, dokumenty se přizpůsobí | VOP P028, RŘ P020, PP P038 | `CreateDispute` (kanál hosta), retence hostovských objednávek |
| registrace povinná | žádný | `CreateOrder` (`AllowsAnonymousActor`), `GuestOrderAccess`, klienti |

### R6 — Odměna úklidníka, hotovost a spropitné

RS přejde na sazbový model kódu s ceníkem v příloze, nebo se odměna přestaví na provizi z ceny. Kód platí `BasePay` + (pokoje − 1)·`ExtraPerRoom` + koupelny·`ExtraPerBathroom` + km·`DistanceRatePerKm` a výsledek ořízne `ApplyMinMaxClamp` — [PayCalculatorExtensions.cs:12-18](../../src/Cleansia.Core.Domain/Extensions/PayCalculatorExtensions.cs#L12-L18); `CalculateAggregatedPay` je jen odhad pro nástěnku — [PayCalculatorExtensions.cs:30-34](../../src/Cleansia.Core.Domain/Extensions/PayCalculatorExtensions.cs#L30-L34). Hotovost zapisuje `CashCollectedAt` bez částky — [Order.cs:50-52](../../src/Cleansia.Core.Domain/Orders/Order.cs#L50-L52). Návrhy: [RS P114](../analyza-2026-09-11/podklady/cleansia_ramcova_smlouva.txt), [VOP P056](../analyza-2026-09-11/podklady/Cleansia_VOP.txt).

| varianta | odstavce návrhů | v kódu |
|---|---|---|
| RS na sazbový model, hotovost provozně | RS P114, P116, P122, příloha 4 | nic |
| odměna jako provize ze zaplacené ceny | RS P116 doplnit sazbu | `PayCalculatorExtensions`, `EmployeePayConfig`, `CalculateOrderPay` |
| spropitné postavit / škrtnout z VOP | VOP P056, Kodex P064 | `Order`, `OrderEmployeePay` nebo `nic` |

### R7 — Fotografie v domácnosti

Fotografie jako smluvní podmínka, nebo volitelná se zaznamenaným souhlasem? Kód vyžaduje ≥ 1 fotografii `After` — [CompleteOrder.cs:183-188](../../src/Cleansia.Core.AppServices/Features/Orders/CompleteOrder.cs#L183-L188) — `Before` nikde, souhlas nezaznamenává, a smaže ji kdokoli z `AssignedEmployees` bez brány na stav — [DeleteOrderPhoto.cs:56-71](../../src/Cleansia.Core.AppServices/Features/Orders/DeleteOrderPhoto.cs#L56-L71). Návrhy: [RS P091](../analyza-2026-09-11/podklady/cleansia_ramcova_smlouva.txt), [DPA P098](../analyza-2026-09-11/podklady/cleansia_dpa.txt).

| varianta | odstavce návrhů | v kódu |
|---|---|---|
| fotografie jako smluvní podmínka, „před" i „po" | VOP čl. VI doplnit, Kodex P059, RS P091 | `StartOrder` (brána), `DeleteOrderPhoto` (zámek) |
| fotografie volitelné se souhlasem na objednávce | RS P091, Kodex P067 | `CompleteOrder` (brána zrušena), nové pole souhlasu |
| v obou: retence fotografií | DPA P098 | `DataRetentionBackgroundService`, blob lifecycle |

### R8 — Přístup úklidníka k údajům po zakázce a DPA

DPA popíše skutečnost, nebo kód dostane odstřižení po dokončení. Kód pouští přiřazeného přes `AssignedEmployees.Any(...)` bez časového členu — [OrderVisibility.cs:53-55](../../src/Cleansia.Core.Domain/Orders/OrderVisibility.cs#L53-L55); token partnerského webu `AccessTokenExpMinutes` 1 440 — [appsettings.json:20](../../src/Cleansia.Web.Partner/appsettings.json#L20). Návrhy: [DPA P086](../analyza-2026-09-11/podklady/cleansia_dpa.txt), [RS P148](../analyza-2026-09-11/podklady/cleansia_ramcova_smlouva.txt).

| varianta | odstavce návrhů | v kódu |
|---|---|---|
| DPA popíše skutečný přístup, lhůty jen na kopie | DPA P086, P090, P094 | nic |
| odstřižení detailu X dnů po dokončení | DPA P070 | `OrderAccessService`, `OrderPiiRedaction` |
| podpis DPA jako podmínka schválení | RS P148, RS P209 | `ApproveEmployee`, seed `Legal/employee/` |

### R9 — Cookies, marketing, GPS, věk a retence

Cookie policy jmenuje skripty, které web nemá; lišta mapuje přihlášenému `analytics → DataProcessing`, `marketing → MarketingEmails` — [consent-sync.service.ts:38-41](../../src/Cleansia.App/libs/core/customer-services/src/lib/services/consent-sync.service.ts#L38-L41) — a `MarketingEmails` nikdo nečte. K tomu věk (18/16) a devět retenčních lhůt, mezi nimi `DefaultOrderPiiYears` a `DefaultStaleDevicesDays` — [RetentionDefaults.cs:16-25](../../src/Cleansia.Core.AppServices/Features/DataRetention/RetentionDefaults.cs#L16-L25). Návrhy: [PP P140](../analyza-2026-09-11/podklady/cleansia_privacy_policy.txt), [GDPR P096](../analyza-2026-09-11/podklady/cleansia_gdpr_souhlasy.txt).

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

Vyžaduje spotřebitelská smlouva s podnikatelem identifikaci zhotovitele (jméno, příjmení, IČO), nebo postačí křestní jméno? Zákazník vidí jen `FirstName` — [OrderMappers.cs:379-380](../../src/Cleansia.Core.AppServices/Mappers/OrderMappers.cs#L379-L380); `RegistrationNumber` a `LegalEntityName` jsou na profilu, na smlouvu nejdou — [Employee.cs:16-19](../../src/Cleansia.Core.Domain/Users/Employee.cs#L16-L19). Návrhy: [VOP P022](../analyza-2026-09-11/podklady/Cleansia_VOP.txt).

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

Jaké věty VOP napíše právník: okamžik vzniku, inkorporace znění odkazem ve verzi platné při objednání, co je zákazníkovi doloženo. Kód: smlouva vzniká přijetím v převzetí — [TakeOrder.cs:356](../../src/Cleansia.Core.AppServices/Features/Orders/TakeOrder.cs#L356); text je otisknut při založení — [OrderFactory.cs:178](../../src/Cleansia.Core.AppServices/Features/Orders/OrderFactory.cs#L178). Návrhy: [VOP P038](../analyza-2026-09-11/podklady/Cleansia_VOP.txt), [VOP P040](../analyza-2026-09-11/podklady/Cleansia_VOP.txt).

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

Volí zákazník „jeden úklidník / posádka", nebo počítá platforma? Nese každé místo celou cenu díla, nebo podíl? Kód: `RequiredEmployees = ⌈EstimatedTime / 120⌉` — [Order.cs:847-850](../../src/Cleansia.Core.Domain/Orders/Order.cs#L847-L850); jeden řádek `OrderEmployeeId` na místo, každý s celou `TotalPrice` — [WorkContractAcceptance.cs:31](../../src/Cleansia.Core.Domain/Contracts/WorkContractAcceptance.cs#L31). Návrhy: [RS P059](../analyza-2026-09-11/podklady/cleansia_ramcova_smlouva.txt), [VOP P022](../analyza-2026-09-11/podklady/Cleansia_VOP.txt).

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

Kód stupeň po odebrání bodů přepočítá a snížení dovolí (`RecomputeTier`) — [LoyaltyAccount.cs:114-120](../../src/Cleansia.Core.Domain/Loyalty/LoyaltyAccount.cs#L114-L120); body se odebírají při stornu i částečné refundaci. Zákaznický web tvrdí, že stupeň nikdy neklesne — [cs.json:1344](../../src/Cleansia.App/apps/cleansia.app/src/assets/i18n/cs.json#L1344). Žádný návrh věrnost nejmenuje.

| varianta | odstavce návrhů | v kódu |
|---|---|---|
| stupeň smí klesnout, text webu opravit | VOP čl. IV doplnit | `cs.json` a čtyři další jazyky |
| stupeň nikdy neklesá | VOP čl. IV doplnit | `LoyaltyAccount.RecomputeTier` |

### R18 — Která oznámení zákazník a úklidník dostanou

Deset okamžiků nemá událost: odchod úklidníka, storno administrátorem, částečná refundace, kredit, zamítnutá reklamace, chargeback, doklad, schválení účtu úklidníka, upravená faktura, ukončení společnosti. Seznam `Customer` má 15 klíčů, `Partner` šest — [NotificationFeedEventKeys.cs:29-59](../../src/Cleansia.Core.Domain/Notifications/NotificationFeedEventKeys.cs#L29-L59). Návrhy: [RŘ P081](../analyza-2026-09-11/podklady/cleansia_reklamacni_rad.txt), [RŘ P112](../analyza-2026-09-11/podklady/cleansia_reklamacni_rad.txt).

| varianta | odstavce návrhů | v kódu |
|---|---|---|
| postavit všech deset událostí | žádný | `NotificationEventCatalog`, deset producentů |
| postavit jen právně nesené (zamítnutá reklamace, kredit, chargeback) | RŘ P081, P112 | `NotificationEventCatalog`, tři producenti |
| popsat lhůty ručně mimo aplikaci | RŘ P067, P081, P112 | nic |

### R19 — Objednávka mimo trh účtu

Operátora má v seedu jen CZE (`cleansia-cz`, `IsDefaultMarket`), u SVK, POL, DEU, AUT a GBR je `NULL` — [insert_seed_data.sql:1014](../../sql-scripts/insert_seed_data.sql#L1014), [insert_seed_data.sql:1017-1024](../../sql-scripts/insert_seed_data.sql#L1017-L1024). EUR, PLN, GBP a USD jsou seedovány jako neaktivní a bez dělitele bodů — [insert_seed_data.sql:491-494](../../sql-scripts/insert_seed_data.sql#L491-L494). Objednávku do takového trhu kód odmítne: přihlášenému `TenantNotFound`, hostu `OrderCountryOperatorMismatch` — [CreateOrder.cs:176-190](../../src/Cleansia.Core.AppServices/Features/Orders/CreateOrder.cs#L176-L190). Je to tedy mechanika čekající na druhý trh: objednávka, doklad, refundace i reklamace patří společnosti trhu adresy (`OperatorTenantId`), účet a věrnost své společnosti — [CreateOrder.cs:933-935](../../src/Cleansia.Core.AppServices/Features/Orders/CreateOrder.cs#L933-L935). Body se v měně bez `LoyaltyPointsDivisor` nepřipíší — [Currency.cs:22-30](../../src/Cleansia.Core.Domain/Internationalization/Currency.cs#L22-L30). Žádný návrh situaci nejmenuje.

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

### R21 — Fiskalizace

Modul `Fiscal` má pro Česko poskytovatele, který doklad nezaregistruje: vrací `NOT_IMPLEMENTED` — [CzechEet2FiscalService.cs:53-55](../../src/Cleansia.Infra.Fiscal/Countries/Czechia/CzechEet2FiscalService.cs#L53-L55). V konfiguraci je `Fiscal:CzechEet2:Enabled` false — [appsettings.json:56](../../src/Cleansia.Web.Admin/appsettings.json#L56). Žádný návrh fiskalizaci nejmenuje.

| varianta | odstavce návrhů | v kódu |
|---|---|---|
| dokončit, než ji trh vyžádá | VOP čl. IV doplnit | `CzechEet2FiscalService`, konfigurace |
| modul odebrat | žádný | `Cleansia.Infra.Fiscal`, registrace služby |
| ponechat nečinný a napsat to do návrhů | VOP čl. IV doplnit | nic |

