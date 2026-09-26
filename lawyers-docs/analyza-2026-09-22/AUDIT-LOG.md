# Záznam auditu - 26. 9. 2026

**Auditovaný commit `fdae9dfba72614e42db0faf1e7c267f79f269ae8`, 26. 9. 2026, větev `fix/audit-findings-2026-09-22` (PR #265 do `master`).** Pět věcných rozborů a obchodní shrnutí popisují tento strom. Pozdější commit `23d0cc656` (jen iOS test) auditován není. `master` je při připnutí `6792f0c256e81e1473908308bd73881004853be4`.

## Inventář

| co | počet / metoda | doklad |
|---|---|---|
| API servery | 5; Partner :5000, Admin :5001, Partner Mobile :5002, Customer :5003, Customer Mobile :5004 | [Program.cs:58-86](../../src/Cleansia.AppHost/Program.cs#L58-L86) |
| kontroléry / trasy | 115 / 526; Partner 14/70, Admin 37/191, Partner Mobile 13/81, Customer 25/92, Customer Mobile 26/92 | soubory `*Controller.cs` a atributy `[HttpGet/Post/Put/Patch/Delete]` v jednotlivých hostech |
| migrace / tabulky | 1 `Initial`, 85 `CreateTable`, 49 vazeb `FK_*_Tenants_TenantId`, 280 indexů, 69 unikátních | [20260923071814_Initial.cs:18](../../src/Cleansia.Infra.Database/Migrations/20260923071814_Initial.cs#L18) |
| entity | 85 = 47 `TenantAuditable` + 21 `Auditable` + 17 `BaseEntity`; bez samotných základních tříd | přímé deklarace dědičnosti; [TenantAuditable.cs:8](../../src/Cleansia.Core.Domain/Common/TenantAuditable.cs#L8) |
| příkazy / dotazy | 214 `ICommand`, 78 `IQuery`, 17 prostých `IRequest`, 47 složek `Features` | deklarace rozhraní; [ICommand.cs:6-8](../../src/Cleansia.Core.AppServices/Abstractions/ICommand.cs#L6-L8) |
| Azure Functions | 41: 22 timer, 18 fronta (9 pracovních + 9 poison), 1 HTTP; 8 konfigurovaných CRONů | atributy triggerů; [appsettings.json:2-9](../../src/Cleansia.Functions/appsettings.json#L2-L9) |
| hostované služby | 5 registrací: tři databázové, dvě obnovující revokace | `AddHostedService` v `Cleansia.Config`, včetně dvou registrací přes factory |
| politiky | 178: 171 mapovaných + 7 anonymních; 11 fyzických; 4 administrátorské role | sčítání `Map` a `AnonymousAllowList`; [PolicyBuilder.cs:298-307](../../src/Cleansia.Core.AppServices/Authentication/PolicyBuilder.cs#L298-L307) |
| právní texty | 15 souborů: VOP, ochrana údajů, smlouva o dílo, každý v pěti jazycích; publikum `employee` bez souboru | [LegalSeedResource.cs:25-36](../../src/Cleansia.Infra.Database/Seed/Legal/LegalSeedResource.cs#L25-L36) |
| webové trasy | zákazník 25+31, partner 13+12, admin 37+70; vrcholové + feature trasy | vlastnosti `path:` včetně inline zápisů v `app.routes.ts` a feature routing souborech |
| mobilní prezentační soubory | Android zákazník/partner 29/27; iOS zákazník/partner 31/32 | `git ls-files`: Android `src/main/*Screen.kt`; iOS `Sources/*View.swift` a `*Screen.swift` |
| ADR | 68, z toho 63 `accepted`, 5 `proposed` | frontmatter; [index.md:5](../../docs/decisions/index.md#L5) |
| chyby / události | 331 chybových klíčů, 28 zákaznických/partnerských událostí (8 bez vypnutelné kategorie), 9 admin událostí | deklarované konstanty; [NotificationEventCatalog.cs:8](../../src/Cleansia.Core.Domain/Notifications/NotificationEventCatalog.cs#L8) |
| definice backend testů | unit 4 736, integrační 604, host 289; nejde o počet spuštěných parametrických případů | řádky začínající `[Fact` nebo `[Theory` v příslušných projektech |
| testovací soubory klientů | Angular 337 `*.spec.ts`, Android 238 `src/test/*.kt`, iOS 321 `Tests/*.swift` | sledované soubory podle cesty a přípony |

### Ověřené mobilní plochy

| plocha | zjištění | doklad |
|---|---|---|
| výběr adresy | Obě zákaznické aplikace nabízejí uložené adresy i mapu. Android otevírá správce adres; iOS skládá chooser a picker. | [BookingBottomSheet.kt:667-677](../../src/cleansia_android/customer-app/src/main/java/cz/cleansia/customer/features/booking/BookingBottomSheet.kt#L667-L677), [BookingSavedAddressChooser.swift:130-146](../../src/cleansia_ios/CleansiaCustomer/Sources/Features/Booking/WhenWhere/AddressPicker/BookingSavedAddressChooser.swift#L130-L146) |
| počítání iOS | Zákazník má 25 souborů `View.swift` a šest `Screen.swift`; partner 32 a nula. | soupis sledovaných souborů podle přípon výše |
| pomocné placeholdery | Hledání `PlaceholderTabView` a `PlaceholderDestination` v obou iOS cílech: volání pouze v DEBUG preview, mimo deklarace. | [PlaceholderTabView.swift:23-30](../../src/cleansia_ios/CleansiaCustomer/Sources/Features/Shell/PlaceholderTabView.swift#L23-L30), [PartnerShellView.swift:167-183](../../src/cleansia_ios/CleansiaPartner/Sources/Features/Shell/PartnerShellView.swift#L167-L183) |

Počty souborů ani tyto dvě ověřené plochy neprokazují úplnou funkční paritu.

## Podklady a rozsah srovnání

Deset DOCX znovu ověřeno 26. 9. 2026: všechny SHA-256 souhlasí s 11. 9. 2026, viz [inventář](podklady/INVENTAR.md). Extrakce a Pxxx zůstávají stejné.

| návrh | odstavců | výslovně porovnáno | řádků ROZPORY §1 |
|---|---:|---:|---:|
| VOP | 145 | 27 | 21 |
| RŘ | 140 | 18 | 12 |
| RS | 233 | 30 | 20 |
| Kodex | 218 | 8 | 6 |
| BOZP | 181 | 5 | 5 |
| PP | 166 | 22 | 14 |
| CP | 241 | 10 | 7 |
| DPA | 215 | 14 | 10 |
| GDPR | 191 | 16 | 11 |
| SS | 154 | 4 | 2 |

„Výslovně porovnáno“ počítá odlišná Pxxx v seznamu Shoda a řádcích §1; nezahrnuje Mimo aplikaci.

## Metoda a kontroly

Čtecí průchody pokryly identitu, smlouvy, objednávky, ceny, platby, doklady, vratky, spory, úklidníky, členství, věrnost, komunikaci, společnosti a retenci. Připnutí na `fdae9dfb` přidalo průchod čtyřmi pravidly vlastníka z 24. 9. 2026 (hotovost, storno po objednání, kredit při výmazu, odměna bez vzdálenosti) ve všech rozborech. Inventář je přepočten na novém stromu stejnými metodami.

Kontrola: `python lawyers-docs/check-citations.py analyza-2026-09-22 fdae9dfba72614e42db0faf1e7c267f79f269ae8`.

Výstup skriptu: **1777 citací · 194 bez identifikátoru · 0 nevyřešených**.

Příkaz pro rozsah: `wc -w lawyers-docs/analyza-2026-09-22/*.md`.

| dokument | slov | limit |
|---|---:|---:|
| PROVOZ-PLATFORMY | 8 999 | 9 000 |
| ROZPORY-DOKUMENTY-VS-KOD | 7 933 | 8 000 |
| TECHNICKE-MEZERY | 4 040 | 5 000 |
| OBCHODNI-MEZERY-A-HRANICNI-PRIPADY | 4 661 | 5 000 |
| AUDIT-LOG | 1 495 | 1 500 |
| PODKLADY-KOLEGU-VS-KOD | 3 292 | 3 500 |
| SHRNUTI | 3 256 | — |

## Rozdíl 7acea58b → 6792f0c2

63 commitů; 1 061 souborů, +40 994 / -43 671 řádků.

| změna | PR | commit | rozbor |
|---|---|---|---|
| Odstranění dostupnosti úklidníků v backendu, webu i mobilních aplikacích | #260 | `38312c8d9`, `5ef4c292f`, `c76523efa` | PROVOZ §8 |
| Regenerace Initial na 84 tabulek; odstranění košíků, EmailTranslations a vybraných sloupců; TrialPeriodDays zůstává s nulovým seedem | #260 | `38312c8d9` | PROVOZ §2, §9, §11 |
| Odstranění nepoužívaných tras, politik a chybových klíčů | #260 | `f307e835f`, `cf4b1f981` | TECHNICKE §1, inventář |
| Mobilní přikládání důkazů při založení sporu | #260 | `f4f598ed8`, `291731c03` | PROVOZ §7 |
| Sjednocení admin/partner webu a odstranění mrtvých souborů | #260 | `9010033e3`, `bd00ff694`, `7da4c1665` | bez změny obchodních pravidel |
| Právní podklady, zadání auditu, renderer a poznámky k neprodukovaným stavům | #260, #262 | `b79435573`, `3d77bc9b2`, `fd77e08e4` | podklady, TECHNICKE §7 |
| Aktualizace závislostí qs a @humanfs/node | #263, #251 | `452d1e517`, `140690822` | bez vlivu na dokumenty |
| Záznam sloučení předchozích PR | #261 | `3f694a861` | bez vlivu na dokumenty |

## Rozdíl 6792f0c2 → 865549b0

23 commitů včetně dokumentační větve a jejího sloučení; 433 souborů, +21 416 / -3 263 řádků.

| změna | commit | dotčený rozbor |
|---|---|---|
| Náhodný hashovaný hostovský token, nové odkazy, odstranění přístupového tajemství z partnerských odpovědí a klientů | `82aaadfda` až `802da26bf` | PROVOZ §4, TECHNICKE §1, ROZPORY |
| Odměna za balíčky, vratka před uzavřením sporu, hostovský chargeback bez povinného účtu, převod kódů zemí; odstranění order.confirmed | `799b2c635`, `2e1aefe31` | PROVOZ §5-§8, TECHNICKE |
| Součet a jazyk účtenky, údaje neplátce, hotovostní přepis na Paid; jazyk z mobilní objednávky | `e8331245d`, `135a804af` | PROVOZ §5, OBCHODNI |
| Ochrana sdílených adres, audit prvního vydání instrukcí, fotografie sedm dní, audity tři roky, úklid tokenů | `33d10fca5` | PROVOZ §11, ROZPORY, TECHNICKE |
| Pravdivější webová nabídka Plus a věrnosti; odstranění nefunkčních seedovaných benefitů | `08581591c` | PROVOZ §9, TECHNICKE, PODKLADY |
| Limity velikosti, doplňky ve vratce, místní čas účtenky, povinné DIČ plátce, odstranění mrtvých členů, klientská oprava seat-open | `fcd0751d5`, `1858d77ef` | PROVOZ, TECHNICKE, OBCHODNI |
| Dokumentace oprav, místní ověření a sloučení původního auditu | `83c5319fa`, `7a3a9cd21`, `d467f72f5`, `9e970917e` | celý soubor rozborů |
| Nové připnutí, vizuální obchodní shrnutí a formátování iOS testu | `fd45d65f0`, `a4e09c1d3`, `865549b02` | celý soubor rozborů; obchodní pravidla beze změny |

## Rozdíl 865549b0 → fdae9dfb

10 commitů; 322 souborů, +11 187 / -1 073 řádků. Migrace `20260923071814_Initial` beze změny.

| změna | commit | dotčený rozbor |
|---|---|---|
| Hotovost (`AllowsCash`) jen přihlášenému zákazníkovi, když `RequiredEmployees` je 1; host a posádka platí kartou; klíč `order.cash_not_available` [BookingPolicy.cs:147-148](../../src/Cleansia.Core.AppServices/Features/Orders/BookingPolicy.cs#L147-L148) | `a261cc226` | PROVOZ §4.5, §5.1, A1; ROZPORY R6; OBCHODNI §1; PODKLADY §6 |
| Opakovaný úklid: hotovostní šablonu mimo `AllowsCash` materializace přeskočí a seznam označí `RequiresPaymentMethodChange`; `ConfirmRecurringOrder` takovou hotovost odmítne [MaterializeRecurringBookingTemplate.cs:261-263](../../src/Cleansia.Core.AppServices/Features/Bookings/MaterializeRecurringBookingTemplate.cs#L261-L263), [ConfirmRecurringOrder.cs:115-119](../../src/Cleansia.Core.AppServices/Features/Orders/ConfirmRecurringOrder.cs#L115-L119) | `a261cc226` | PROVOZ §4.5; OBCHODNI §1 |
| Storno zdarma do 15 min od objednání všem včetně hostů, 60 min placenému členovi Plus; neúčinné okno první objednávky (`OopsWindowMinutesFirstTime`) odstraněno; pásma storna beze změny [BookingPolicy.cs:81-87](../../src/Cleansia.Core.AppServices/Features/Orders/BookingPolicy.cs#L81-L87) | `0ed55136f` | PROVOZ §6.1, A1; ROZPORY VOP P073, R4; OBCHODNI §1 |
| Dokončený výmaz účtu odepíše kredit všech měn jako `Expired` pod `account-deletion:`; kladný zůstatek výmaz neblokuje (klíč `gdpr.deletion_blocked_by_credit_balance` odstraněn); vymazanému účtu (`AnonymisedEmailSuffix`) se kredit nepřipíše [GdprDeletionService.cs:247-259](../../src/Cleansia.Core.AppServices/Services/GdprDeletionService.cs#L247-L259), [CreditAccountRepository.cs:100-105](../../src/Cleansia.Infra.Database/Repositories/CreditAccountRepository.cs#L100-L105) | `d56b5b6b0` | PROVOZ §6.4, §11.2; ROZPORY VOP P034, R4; OBCHODNI §1, §3 |
| Odměna úklidníka bez vzdálenosti: `BasePay` + pokoje + koupelny, pak meze; sloupec `DistanceRatePerKm` zůstává (hledání v `Cleansia.Core.*`: jen deklarace); klíč `pay_config.distance_rate_negative` odstraněn [PayCalculatorExtensions.cs:36-52](../../src/Cleansia.Core.Domain/Extensions/PayCalculatorExtensions.cs#L36-L52), [EmployeePayConfig.cs:28-29](../../src/Cleansia.Core.Domain/EmployeePayroll/EmployeePayConfig.cs#L28-L29) | `fdc41c97a` | PROVOZ §8.6, A1; ROZPORY R6; PODKLADY §5; OBCHODNI §2; TECHNICKE §7 |
| Web vypíná hotovost s důvodem podle `requiredEmployees` z nabídky; texty 15/60 min; zákaznický i admin výmaz varuje před odepsáním kreditu [cash-eligibility.models.ts:13-22](../../src/Cleansia.App/libs/shared/models/src/lib/models/cash-eligibility.models.ts#L13-L22) | `3e6de6f2a` | SHRNUTI |
| iOS a Android: totéž pravidlo (`requiredEmployees`), texty a varování; partnerské aplikace neslibují odměnu za cestu (`git grep distance_pay` → jen testy) [CashEligibility.swift:14-19](../../src/cleansia_ios/CleansiaCustomer/Sources/Features/Booking/CashEligibility.swift#L14-L19), [CashEligibility.kt:22-27](../../src/cleansia_android/customer-app/src/main/java/cz/cleansia/customer/core/booking/CashEligibility.kt#L22-L27) | `a7c25405a`, `202093723` | SHRNUTI |
| Kontrola parity storna 15/60 min; popis pravidel v `docs/` | `1877915e6`, `fdae9dfba` | bez vlivu na chování |
| Připnutí rozborů po formátování CI | `e4b46a486` | celý soubor rozborů; kód beze změny |

Rozhodovací varianty mimo čtyři pravidla implementovány nejsou: model posádky, hotovostní vyrovnání, serverová časová mřížka, sjednocení živých sazeb DPH a význam měsíčního opakování.
