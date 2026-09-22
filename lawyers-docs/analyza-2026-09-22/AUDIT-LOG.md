# Záznam auditu — 22. 9. 2026

**Auditovaný stav zdrojového kódu · commit `6792f0c256e81e1473908308bd73881004853be4` (`master`, 22. 9. 2026).** Všechny čtyři dokumenty ve složce popisují výhradně tento commit; commity, které `master` přijal po něm, auditovány nejsou.

| položka | hodnota |
|---|---|
| commit | `6792f0c256e81e1473908308bd73881004853be4` |
| datum commitu | 2026-09-22 (`git log -1 --format=%ad --date=short 6792f0c2`) |
| větev | `master` |
| předchozí audit | `7acea58b` (20. 9. 2026, složka `analyza-2026-09-16`) |

## Inventář

| co | počet | kde |
|---|---|---|
| API servery | 5 — Partner :5000 · Admin :5001 · Partner Mobile :5002 · Customer :5003 · Customer Mobile :5004 | [Program.cs:58-86](../../src/Cleansia.AppHost/Program.cs#L58-L86) |
| kontroléry / trasy `[Http*]` | 115 / 526 — Partner 14/70 · Admin 37/191 · Partner Mobile 13/81 · Customer 25/92 · Customer Mobile 26/92 | `grep -rhoE '\[Http(Get|Post|Put|Patch|Delete)' src/Cleansia.Web.*/Controllers/ | wc -l` |
| migrace / tabulky | 1 (`Initial`) / 84 `CreateTable`; 48 tabulek s `TenantId` a `FK_*_Tenants_TenantId`; 277 indexů, 68 unikátních | [20260920204705_Initial.cs:18](../../src/Cleansia.Infra.Database/Migrations/20260920204705_Initial.cs#L18) |
| entity | 84 = 46 `TenantAuditable` + 21 `Auditable` + 17 `BaseEntity` (2 z nich `ITenantEntity`) | [TenantAuditable.cs:8](../../src/Cleansia.Core.Domain/Common/TenantAuditable.cs#L8) |
| příkazy / dotazy | 214 `ICommand` / 78 `IQuery` (+ 17 prostých `IRequest`) ve 47 složkách `Features/` | [ICommand.cs:6-8](../../src/Cleansia.Core.AppServices/Abstractions/ICommand.cs#L6-L8) |
| Azure Functions | 41 = 22 timer + 18 fronta (9 pracovních + 9 poison) + 1 http; 8 CRONů `…Cron` z `appsettings.json` (`MaterializeRecurringBookingsCron` …); hostované služby 5 | [appsettings.json:2-9](../../src/Cleansia.Functions/appsettings.json#L2-L9) |
| politiky `Policy` | 178 = 171 v `PolicyBuilder.Map` + 7 v `AnonymousAllowList`; 11 fyzických; 4 role `AdminRole` | [PolicyBuilder.cs:298-307](../../src/Cleansia.Core.AppServices/Authentication/PolicyBuilder.cs#L298-L307) |
| seedované právní texty | 15 souborů = 3 dokumenty (VOP a ochrana údajů 2026-09-14, smlouva o dílo 2026-09-20) × 5 jazyků; publikum `employee` bez souboru | [LegalSeedResource.cs:25-36](../../src/Cleansia.Infra.Database/Seed/Legal/LegalSeedResource.cs#L25-L36) |
| trasy webů | zákaznický 25 + 31 · partnerský 13 + 12 · admin 37 + 69 (vrcholové + feature) | `grep -cE '^\s*path:' src/Cleansia.App/apps/*/src/app/app.routes.ts` |
| obrazovky mobilních cílů | Android zákazník 29 · Android partner 27 · iOS zákazník 31 · iOS partner 32 | `find src/cleansia_android -name '*Screen.kt'`, `find src/cleansia_ios -name '*View.swift'` |
| ADR | 68 (0001–0068), 63 `accepted`, 5 `proposed` | [index.md:5](../../docs/decisions/index.md#L5) |
| chybové klíče / události | 331 `BusinessErrorMessage` · 29 `NotificationEventCatalog` (8 bez vypnutelné kategorie) · 9 admin událostí | [NotificationEventCatalog.cs:8](../../src/Cleansia.Core.Domain/Notifications/NotificationEventCatalog.cs#L8) |
| testy | backend 5 454 (`Cleansia.Tests` 4 632 · `IntegrationTests` 534 · `HostTests` 288) · Angular 329 spec · Android 232 · iOS 314 | `grep -rE '\[(Fact|Theory)\b' src/Cleansia.Tests --include=*.cs | wc -l` |

Parita Android ↔ iOS podle funkcí: žádná obrazovka Androidu bez protějšku na iOS; navíc jen iOS `BookingAddressPickerView` (Android ji vkládá do `WhenWhereStep.kt`) a `PlaceholderTabView` bez čtenáře.

## Podklady

Deset DOCX v `lawyers-docs/SMLOUVY CleanSia/SMLOUVY/` má 22. 9. 2026 stejný SHA-256 jako 11. 9. 2026 → [podklady/INVENTAR.md](podklady/INVENTAR.md) a `../analyza-2026-09-11/podklady/INVENTAR.md`. Extrakce ani číslování Pxxx se nemění.

| návrh | odstavců | zkontrolováno | řádků v ROZPORY §1 |
|---|---:|---:|---:|
| VOP | 145 | 50 | 21 |
| RŘ | 140 | 54 | 12 |
| RS | 233 | 65 | 23 |
| Kodex | 218 | 23 | 6 |
| BOZP | 181 | 19 | 5 |
| PP | 166 | 58 | 12 |
| CP | 241 | 74 | 7 |
| DPA | 215 | 45 | 10 |
| GDPR | 191 | 83 | 11 |
| SS | 154 | 22 | 2 |

„Zkontrolováno" = odstavce s tvrzením, které kód může potvrdit nebo vyvrátit; zbytek je boilerplate nebo provozní závazek mimo aplikaci.

## Metoda

1. Inventář stromu (počty výše, každý s příkazem).
2. Čtecí průchody po doménách: identita a souhlasy · objednávka, ceny a trhy · platby, doklady, storno, refundace, spory · úklidník · členství, věrnost, notifikace · společnosti, role, úlohy na pozadí · deset návrhů · rozdíl `7acea58b..6792f0c2` · přenos rozhodnutí R1–R11 · bezpečnost S1–S12 a technické mezery. Každé tvrzení nese soubor a řádek commitu; záporné tvrzení nese hledaný výraz.
3. Kostra PROVOZ a registr rozhodnutí R1–R19; čtyři dokumenty psané z poznámek; ke každému dva oponentní průchody (tvrzení proti kódu; co není fakt, odstavec, citace ani rozhodnutí) a oprava.
4. Kontroly jménem: `skript citací` (`python lawyers-docs/check-citations.py analyza-2026-09-22 6792f0c2…`: cesta v commitu, mez řádku, identifikátor do ±3 řádků, řádek `[Pxxx]`, buňka ≤ 40 slov, zakázaná slova) a `wc -w`.

## Kontrola citací

Výstup skriptu: `1594 citací · 114 bez identifikátoru · 0 nevyřešených`

## Rozsah dokumentů

Příkaz: `wc -w lawyers-docs/analyza-2026-09-22/*.md`

| dokument | slov | limit |
|---|---:|---:|
| PROVOZ-PLATFORMY.md | 8 851 | 9 000 |
| ROZPORY-DOKUMENTY-VS-KOD.md | 7 844 | 8 000 |
| TECHNICKE-MEZERY.md | 4 130 | 5 000 |
| OBCHODNI-MEZERY-A-HRANICNI-PRIPADY.md | 4 031 | 5 000 |
| AUDIT-LOG.md | 1 187 | 1 500 |

## Rozdíl 7acea58b → 6792f0c2

63 commitů, 1 061 souborů, +40 994 / −43 671 řádků; PR #255 (konec), #260, #261, #251, #262, #263. Ověřeno ve stromu, ne z popisů PR.

| co | PR | commit | dotčený dokument / § |
|---|---|---|---|
| Modul dostupnosti úklidníka odstraněn: `Employee` bez `Availability`, příkazy `UpdateAvailability` / `AdminUpdateEmployeeAvailability` a jejich trasy neexistují, admin editor, partnerské wire členy a obrazovky obou mobilních aplikací pryč | #260 | `38312c8d9` `5ef4c292f` `c76523efa` | PROVOZ §8, TECHNICKE §7 |
| `Initial` přegenerována na `20260920204705`: 84 tabulek (bylo 88), bez `Carts`, `CartPackageItems`, `CartServiceItems`, `EmailTranslations`, bez `Employees.Availability` a `Employees.PreferredCurrencyCode` | #260 | `38312c8d9` | AUDIT-LOG inventář, PROVOZ §11 |
| `MembershipPlans.TrialPeriodDays` ve stromu zůstává (migrace, entita, seed 0); admin formulář plánu pole nenabízí a server jinou hodnotu než 0 odmítá — popis PR jej uvádí mezi odstraněnými, strom ne | #260 | `38312c8d9` `5ef4c292f` | PROVOZ §9 |
| Registrace bez řádku košíku; GDPR export bez `PreferredCurrencyCode`; seed bez `EmailTranslations` | #260 | `38312c8d9` | PROVOZ §2, §11 |
| `Policy` 178 konstant (bylo 188), `BusinessErrorMessage` bez 37 klíčů; partner host bez `PayConfigController`, `GetOverview` tras, prázdného `DisputeController` a tras `CalculateOrderPay` / `RegenerateInvoicePdf` / `GetPayPeriodById`; admin host bez `AdminUser/{userId}`, `AdminCompany/get-current`, `AdminEmailTemplate/get-paged`; testy `PermissionsCarriedByRoutes` a `ReferencedConstants` | #260 | `f307e835f` `cf4b1f981` | AUDIT-LOG inventář, TECHNICKE §1 |
| Důkazy ke sporu přiložitelné už při založení na Androidu i iOS (`addEvidence`, nahrání po potvrzení serveru); serverový validátor beze změny | #260 | `f4f598ed8` `291731c03` | PROVOZ §7, OBCHODNI §1 |
| Admin a partner web sladěny na jeden tvar stránek a formátů; 84 souborů mrtvého webového kódu pryč; počet tras a nabídka funkcí beze změny | #260 | `9010033e3` `bd00ff694` `7da4c1665` a další | žádný |
| Právní analýzy 11. 9. a 16. 9., extrakce a deset DOCX sledovány v repozitáři; zadání auditu a renderer | #260, #262 | `b79435573` `3d77bc9b2` | AUDIT-LOG podklady |
| ADR-0006 a ADR-0034 nesou poznámku: `RefundStatus.Failed` a `PayoutDetailsStatus.NeedsReconfirmation` bez producenta | #260 | `fd77e08e4` | TECHNICKE §7 |
| Beze změny: `BookingPolicy`, `ReferralPolicy`, `DisputeLimits`, `Seed/Legal/`, hodnoty seedu, `.github/`, `deploy/`, Functions, ADR sada (0069 neexistuje) | — | — | — |
| Dependabot `qs` (jen vnořené pod `verdaccio`), `@humanfs/node` | #263, #251 | `452d1e517` `140690822` | bez vlivu na dokumenty |
| Záznam sloučení PR #255 a #260 (jen `agents/`) | #261 | `3f694a861` | bez vlivu na dokumenty |
