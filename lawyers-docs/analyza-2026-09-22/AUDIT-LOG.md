# Záznam auditu - 23. 9. 2026

**Auditovaný commit `9e970917e1c7270a0baa0ea1ab07693990340914`, 23. 9. 2026, větev `fix/audit-findings-2026-09-22`.** Pět věcných rozborů a obchodní shrnutí popisují tento strom. Následující dokumentační commity nemění auditovaný kód. Cíl PR je `master`; jeho ověřený stav při připnutí je `6792f0c256e81e1473908308bd73881004853be4`. Složka zachovává datum původního auditu.

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
| chyby / události | 332 chybových klíčů, 28 zákaznických/partnerských událostí (8 bez vypnutelné kategorie), 9 admin událostí | deklarované konstanty; [NotificationEventCatalog.cs:8](../../src/Cleansia.Core.Domain/Notifications/NotificationEventCatalog.cs#L8) |
| definice backend testů | unit 4 692, integrační 572, host 289; nejde o počet spuštěných parametrických případů | řádky začínající `[Fact` nebo `[Theory` v příslušných projektech |
| testovací soubory klientů | Angular 331 `*.spec.ts`, Android 233 `src/test/*.kt`, iOS 314 `Tests/*.swift` | sledované soubory podle cesty a přípony |

### Ověřené mobilní plochy

| plocha | zjištění | doklad |
|---|---|---|
| výběr adresy | Obě zákaznické aplikace nabízejí uložené adresy i mapu. Android otevírá správce adres; iOS skládá chooser a picker. | [BookingBottomSheet.kt:666-676](../../src/cleansia_android/customer-app/src/main/java/cz/cleansia/customer/features/booking/BookingBottomSheet.kt#L666-L676), [BookingSavedAddressChooser.swift:130-146](../../src/cleansia_ios/CleansiaCustomer/Sources/Features/Booking/WhenWhere/AddressPicker/BookingSavedAddressChooser.swift#L130-L146) |
| počítání iOS | Zákazník má 25 souborů `View.swift` a šest `Screen.swift`; partner 32 a nula. | soupis sledovaných souborů podle přípon výše |
| pomocné placeholdery | Hledání `PlaceholderTabView` a `PlaceholderDestination` v obou iOS cílech: volání pouze v DEBUG preview, mimo deklarace. | [PlaceholderTabView.swift:23-30](../../src/cleansia_ios/CleansiaCustomer/Sources/Features/Shell/PlaceholderTabView.swift#L23-L30), [PartnerShellView.swift:167-183](../../src/cleansia_ios/CleansiaPartner/Sources/Features/Shell/PartnerShellView.swift#L167-L183) |

Počty souborů ani tyto dvě ověřené plochy neprokazují úplnou funkční paritu.

## Podklady a rozsah srovnání

Deset DOCX znovu ověřeno 23. 9. 2026: všechny SHA-256 souhlasí s 11. 9. 2026, viz [inventář](podklady/INVENTAR.md). Extrakce a Pxxx zůstávají stejné. Pro obchodní podklady bylo přečteno všech 17 stran dvou PDF a deset listů sešitu.

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

„Výslovně porovnáno“ počítá odlišná Pxxx v seznamu Shoda a řádcích §1; nezahrnuje Mimo aplikaci. Nahrazuje nereprodukovatelné souhrnné počty původního logu.

## Metoda a kontroly

Čtecí průchody pokryly identitu, smlouvy, objednávky, ceny, platby, doklady, vratky, spory, úklidníky, členství, věrnost, komunikaci, společnosti a retenci. Znovu byly ověřeny také nezměněné výroky, právní citáty a rozhodnutí R1-R27. Souřadnice citací byly přeneseny porovnáním stromů; tvrzení následně ověřena proti zdrojům. Oponentura odděluje věcnou pravdivost od srozumitelnosti a rozsahu. PDF procházejí vizuální kontrolou.

Kontrola: `python lawyers-docs/check-citations.py analyza-2026-09-22 9e970917e1c7270a0baa0ea1ab07693990340914`.

Výstup skriptu: **1706 citací · 199 bez identifikátoru · 0 nevyřešených**.

Příkaz pro rozsah: `wc -w lawyers-docs/analyza-2026-09-22/*.md`.

| dokument | slov | limit |
|---|---:|---:|
| PROVOZ-PLATFORMY | 8983 | 9 000 |
| ROZPORY-DOKUMENTY-VS-KOD | 7888 | 8 000 |
| TECHNICKE-MEZERY | 3701 | 5 000 |
| OBCHODNI-MEZERY-A-HRANICNI-PRIPADY | 4032 | 5 000 |
| AUDIT-LOG | 1203 | 1 500 |
| PODKLADY-KOLEGU-VS-KOD | 2824 | 3 500 |

## Rozdíl 7acea58b → 6792f0c2

63 commitů; 1 061 souborů, +40 994 / -43 671 řádků. Historický interval je oddělen od oprav níže.

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

## Rozdíl 6792f0c2 → 9e970917

20 commitů včetně dokumentační větve a jejího sloučení; 430 souborů, +20 813 / -3 262 řádků. Poslední aplikační změna je `1858d77ef`; sloučení auditu aplikační strom nemění.

| změna | commit | dotčený rozbor |
|---|---|---|
| Náhodný hashovaný hostovský token, nové odkazy, odstranění přístupového tajemství z partnerských odpovědí a klientů | `82aaadfda` až `802da26bf` | PROVOZ §4, TECHNICKE §1, ROZPORY |
| Odměna za balíčky, vratka před uzavřením sporu, hostovský chargeback bez povinného účtu, převod kódů zemí; odstranění order.confirmed | `799b2c635`, `2e1aefe31` | PROVOZ §5-§8, TECHNICKE |
| Součet a jazyk účtenky, údaje neplátce, hotovostní přepis na Paid; jazyk z mobilní objednávky | `e8331245d`, `135a804af` | PROVOZ §5, OBCHODNI |
| Ochrana sdílených adres, audit prvního vydání instrukcí, fotografie sedm dní, audity tři roky, úklid tokenů | `33d10fca5` | PROVOZ §11, ROZPORY, TECHNICKE |
| Pravdivější webová nabídka Plus a věrnosti; odstranění nefunkčních seedovaných benefitů | `08581591c` | PROVOZ §9, TECHNICKE, PODKLADY |
| Limity velikosti, doplňky ve vratce, místní čas účtenky, povinné DIČ plátce, odstranění mrtvých členů, klientská oprava seat-open | `fcd0751d5`, `1858d77ef` | PROVOZ, TECHNICKE, OBCHODNI |
| Dokumentace oprav, místní ověření a sloučení původního auditu | `83c5319fa`, `7a3a9cd21`, `d467f72f5`, `9e970917e` | celý soubor rozborů |

Rozhodovací varianty nebyly implementovány: mimo jiné model posádky, hotovostní vyrovnání, první storno okno, serverová časová mřížka, sjednocení živých sazeb DPH a význam měsíčního opakování.
