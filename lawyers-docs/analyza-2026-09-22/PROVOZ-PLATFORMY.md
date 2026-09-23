# Jak platforma CleanSia funguje

**Provozní popis podle zdrojového kódu · commit 9e970917e1c7270a0baa0ea1ab07693990340914 (fix/audit-findings-2026-09-22, 23. 9. 2026)**

Slovník: **úklidník** = „Zhotovitel" (VOP, RS, RŘ, Kodex) = `Employee` (kód) = „uklízeč" (UI); **zákazník** = „Zákazník / Spotřebitel / Uživatel" = `User`; **host** = zákazník bez účtu (`Order.UserId = null`); **provozní společnost** = „Provozovatel / Cleansia s.r.o." = `Tenant`; **administrátor** = jedna z rolí `Administrator / Manager / Support / Accountant`. Částky v Kč, cizí měny ISO kódem.

## Shrnutí

Kartová objednávka:

| Otázka | Odpověď | citace |
|---|---|---|
| Kdo vystavuje doklad | provozní společnost (`CompanyInfo` podle země adresy), číslo `RCP-{rok}-{NNNN}`; úklidník nikdy | [ReceiptService.cs:44-54](../../src/Cleansia.Core.AppServices/Services/ReceiptService.cs#L44-L54), [Constants.cs:83-85](../../src/Cleansia.Core.AppServices/Common/Constants.cs#L83-L85) |
| Kdo komu fakturuje odměnu | samofakturace: `Dodavatel` = úklidník, `Odběratel` = provozní společnost; splatnost `PaymentTermsDays` 14 dní | [InvoiceLabels.cs:77-78](../../src/Cleansia.Infra.Services/Pdf/Models/InvoiceLabels.cs#L77-L78), [Constants.cs:78](../../src/Cleansia.Core.AppServices/Common/Constants.cs#L78) |
| Kam směřují karetní platby | Stripe klient používá jednu konfiguraci `config.SecretKey`; právního vlastníka účtu kód nedokládá | [StripeClient.cs:34](../../src/Cleansia.Infra.Clients/Stripe/StripeClient.cs#L34) |
| Kdo drží hotovost | úklidník; `MarkCashCollected` zapíše `Paid`, `CashCollectedAt`, `CollectedByEmployeeId`; žádné započtení | [Order.cs:713-720](../../src/Cleansia.Core.Domain/Orders/Order.cs#L713-L720) |
| Co si platforma bere | provizi nepočítá; odměna z `BasePay` a příplatků (§8.6), seed `ROUND(BasePrice * 0.5, 2)` | [PayCalculatorExtensions.cs:12-18](../../src/Cleansia.Core.Domain/Extensions/PayCalculatorExtensions.cs#L12-L18), [insert_seed_data.sql:781](../../sql-scripts/insert_seed_data.sql#L781) |

Otevřená rozhodnutí: → R1, R3–R10, R11.1, R11.3–R11.7, R12–R15, R17–R21. R2, rámec R11 a R16 jsou rozhodnuté.

## 1. Strany a role

### 1.1 Zákazník s účtem

- Jedna tabulka `Users`; profil `Customer = 1`, `Employee = 2`, `Administrator = 100` — [UserProfile.cs:8-10](../../src/Cleansia.Core.Domain/Enums/UserProfile.cs#L8-L10)
- Index `Email` unikátní globálně přes společnosti; účet vázán `FK_Users_Tenants_TenantId` (`Restrict`) — [UserEntityConfiguration.cs:107-108](../../src/Cleansia.Infra.Database/EntityConfigurations/UserEntityConfiguration.cs#L107-L108), [20260923071814_Initial.cs:1071-1076](../../src/Cleansia.Infra.Database/Migrations/20260923071814_Initial.cs#L1071-L1076)
- Objednávka v jiném trhu: adresa vybírá operátora (`operatorTenantId`), účet patří dál své společnosti; operátora drží dnes jen CZE (`cleansia-cz`) → R19 — [CreateOrder.cs:952-954](../../src/Cleansia.Core.AppServices/Features/Orders/CreateOrder.cs#L952-L954), [insert_seed_data.sql:1014](../../sql-scripts/insert_seed_data.sql#L1014)
- `User` bez firemních polí; `LegalEntityName`, `RegistrationNumber`, `IBAN` jen na `Employee` — [Employee.cs:16-21](../../src/Cleansia.Core.Domain/Users/Employee.cs#L16-L21)

### 1.2 Host (zákazník bez účtu)

- `CreateOrder`: `AllowsAnonymousActor = true`, `[AllowAnonymous]` na webovém zákaznickém hostu — [CreateOrder.cs:27](../../src/Cleansia.Core.AppServices/Features/Orders/CreateOrder.cs#L27), [OrderController.cs:74-76](../../src/Cleansia.Web.Customer/Controllers/OrderController.cs#L74-L76)
- Mobilní zákaznický host má tytéž operace (`CancelGuest` … `GetWorkContract`) — [OrderController.cs:20-271](../../src/Cleansia.Web.Mobile.Customer/Controllers/OrderController.cs#L20-L271)
- `UserId = null`; přístup přes `AccessToken`, jehož hash odpovídá neodvolanému, neprošlému řádku pro hostovskou objednávku — [GuestOrderAccess.cs:27-42](../../src/Cleansia.Core.AppServices/Features/Orders/GuestOrderAccess.cs#L27-L42)
- `ConfirmationCode` je lidská reference; oprávnění tvoří náhodný token, uložený jako `TokenHash` (SHA-256) — [GuestOrderAccessToken.cs:54-69](../../src/Cleansia.Core.Domain/Orders/GuestOrderAccessToken.cs#L54-L69), [OrderExtensions.cs:7](../../src/Cleansia.Core.Domain/Extensions/OrderExtensions.cs#L7)
- Host nesmí uvést `SavedAddressId`; odpověď `NotFound` — [OrderAddressResolver.cs:94-99](../../src/Cleansia.Core.AppServices/Features/Orders/OrderAddressResolver.cs#L94-L99)
- Jen trh operátora požadavku (`OrderCountryOperatorMismatch`); `TermsAccepted` povinné (`consent.terms_not_accepted`) → R5 — [CreateOrder.cs:181-185](../../src/Cleansia.Core.AppServices/Features/Orders/CreateOrder.cs#L181-L185), [CreateOrder.cs:324-327](../../src/Cleansia.Core.AppServices/Features/Orders/CreateOrder.cs#L324-L327)
- Bez kreditu (`userId` prázdné → 0), bez bodů (`UserId == null`) — [CreateOrder.cs:1128-1131](../../src/Cleansia.Core.AppServices/Features/Orders/CreateOrder.cs#L1128-L1131), [LoyaltyService.cs:40-43](../../src/Cleansia.Core.AppServices/Services/LoyaltyService.cs#L40-L43)
- Bez reklamace (`[Permission]` → 401) a bez stažení dokladu (`DownloadReceipt` = `Authenticated`) — [DisputeController.cs:17-18](../../src/Cleansia.Web.Customer/Controllers/DisputeController.cs#L17-L18), [OrderController.cs:167-168](../../src/Cleansia.Web.Customer/Controllers/OrderController.cs#L167-L168)
- K účtu se nikdy nepřipojí: `UserId` píše jen `Create` a `AnonymizeCustomerData` — grep -rn "UserId = " src/Cleansia.Core.Domain/Orders/Order.cs → 2 zápisy; [Order.cs:985](../../src/Cleansia.Core.Domain/Orders/Order.cs#L985)

### 1.3 Úklidník

- `Employee : TenantAuditable`; `EntityType` výchozí `NaturalPerson`; partner nesmí sám na `LegalEntity` (`employee.legal_entity_not_accepted`) — [Employee.cs:13](../../src/Cleansia.Core.Domain/Users/Employee.cs#L13), [UpdateIdentificationInfo.cs:45-47](../../src/Cleansia.Core.AppServices/Features/Employees/UpdateIdentificationInfo.cs#L45-L47)
- Nikdy plátce DPH: `cleanersAreVatPayers = false` → R16 — [FileExtensions.cs:119](../../src/Cleansia.Core.AppServices/Extensions/FileExtensions.cs#L119)
- Komentář `Tenant.cs`: společnost „contracts the customer, employs the cleaner" → R1 — [Tenant.cs:7-8](../../src/Cleansia.Core.Domain/Tenancy/Tenant.cs#L7-L8)
- `WorkCountryId` řídí měnu a výplatu; `WeeklyOrderLimit` `NULL` = bez limitu — [Employee.cs:188-195](../../src/Cleansia.Core.Domain/Users/Employee.cs#L188-L195), [Employee.cs:127-148](../../src/Cleansia.Core.Domain/Users/Employee.cs#L127-L148)

### 1.4 Administrátor — čtyři role

| role | co smí | citace |
|---|---|---|
| `Administrator = 1` | vše; jen on: admin uživatelé (`CanCreateAdminUser` … `CanSetAdminRole`), životní cyklus (`AdministratorOnly`), `CanViewLegalDocuments` | [AdminRole.cs:14-17](../../src/Cleansia.Core.Domain/Enums/AdminRole.cs#L14-L17), [PolicyBuilder.cs:193-198](../../src/Cleansia.Core.AppServices/Authentication/PolicyBuilder.cs#L193-L198) |
| `Manager = 2` | zápis katalogu (`ManagerOrAbove`), `CanRevealEmployeePayoutDetails`, `CanSendSitewidePromo`, `CanAdminDeleteUserAccount` | [PolicyBuilder.cs:80](../../src/Cleansia.Core.AppServices/Authentication/PolicyBuilder.cs#L80), [PolicyBuilder.cs:236-239](../../src/Cleansia.Core.AppServices/Authentication/PolicyBuilder.cs#L236-L239) |
| `Support = 3` | `CanAdminCancelOrder`, `CanOverrideOrderStatus`, `CanReassignOrder`, `CanRefundOrder`; `CanApproveEmployee`, `CanRevealOrderAccessInstructions`; spory; PII zákazníka | [PolicyBuilder.cs:35-38](../../src/Cleansia.Core.AppServices/Authentication/PolicyBuilder.cs#L35-L38), [PolicyBuilder.cs:73-81](../../src/Cleansia.Core.AppServices/Authentication/PolicyBuilder.cs#L73-L81) |
| `Accountant = 4` | faktury (`CanApproveInvoice` …), období, reporty, `CanManageFiscalFailures`; zákazníka objednávky nevidí (`SupportOrAbove`) | [PolicyBuilder.cs:98-106](../../src/Cleansia.Core.AppServices/Authentication/PolicyBuilder.cs#L98-L106), [PhysicalPolicy.cs:17](../../src/Cleansia.Core.AppServices/Authentication/PhysicalPolicy.cs#L17) |
| sady rolí | `AdministratorOnly` · `ManagerOrAbove` · `SupportOrAbove` · `AccountantOrAbove` {Administrator, Manager, Accountant}; claim `admin_role` fail-closed, nezmapovaná policy → `Deny`; bez dvoufaktorového ověření | grep -rn "TwoFactor\|Totp" src/ → 0; [AdminRoleSets.cs:17-37](../../src/Cleansia.Core.AppServices/Authentication/AdminRoleSets.cs#L17-L37) |

### 1.5 Provozní společnost

`Tenant` = provozní společnost pod holdingem; seed jediná `cleansia-cz` „Cleansia CZ s.r.o." → R2; mechanismus §12 — [insert_seed_data.sql:63-64](../../sql-scripts/insert_seed_data.sql#L63-L64)

## 2. Registrace, souhlasy a právní texty

### 2.1 Registrace a přihlášení

| cesta | co se ověří | lhůty | citace |
|---|---|---|---|
| e-mail + heslo `Register.Command` | `TermsAccepted == true`, jinak `consent.terms_not_accepted` | — | [Register.cs:55-58](../../src/Cleansia.Core.AppServices/Features/Auth/Register.cs#L55-L58) |
| ověření e-mailu | kód `OtpLength` 6 číslic, jen SHA-256 hash; `MaxCodeVerificationAttempts` 5 | 15 minut | [SecurityTokens.cs:33](../../src/Cleansia.Core.Domain/Common/SecurityTokens.cs#L33), [User.cs:15](../../src/Cleansia.Core.Domain/Users/User.cs#L15) |
| Google `GoogleAuth` / Apple `AppleAuth` | zákaznické přihlášení nebo registrace; `TermsAccepted` vyžaduje založení účtu, existující přihlášení nikoli | `IsEmailConfirmed = true` | [GoogleAuth.cs:170-190](../../src/Cleansia.Core.AppServices/Features/Auth/GoogleAuth.cs#L170-L190), [AppleAuth.cs:201-217](../../src/Cleansia.Core.AppServices/Features/Auth/AppleAuth.cs#L201-L217) |
| úklidník `RegisterEmployee` | `TermsAccepted` se nevyžaduje → R3 | — | [RegisterEmployee.cs:68-70](../../src/Cleansia.Core.AppServices/Features/Auth/RegisterEmployee.cs#L68-L70) |
| heslo, zámek, reset | `PasswordPattern` min. 8 znaků; `MaxFailedLoginAttempts` 5; reset jen `AuthenticationType.Internal` | zámek i reset kód 15 min | [ValidationExtensions.cs:13](../../src/Cleansia.Core.AppServices/Common/Validators/ValidationExtensions.cs#L13), [ChangePassword.cs:76-83](../../src/Cleansia.Core.AppServices/Features/Users/ChangePassword.cs#L76-L83) |
| tokeny sezení | `AccessTokenExpMinutes`, `RefreshTokenExpDays` | web 1 440 min; ostatní hodnoty A1 | [appsettings.json:20](../../src/Cleansia.Web.Partner/appsettings.json#L20) |
| věk a telefon | `BirthDate` při registraci nesbírán; úklidník `BeReasonableAge` 18–120; telefon bez SMS ověření → R9 | — | grep -rn "PhoneVerif\|SmsCode" src --include=*.cs → 0; [ValidationExtensions.cs:27-28](../../src/Cleansia.Core.AppServices/Common/Validators/ValidationExtensions.cs#L27-L28) |

### 2.2 Co se zapisuje jako souhlas

| souhlas | kdy | co záznam nese | citace |
|---|---|---|---|
| `ConsentType` | `TermsOfService = 0`, `PrivacyPolicy = 1`, `MarketingEmails = 2`, `DataProcessing = 3` | — | [ConsentType.cs:8-11](../../src/Cleansia.Core.Domain/Enums/ConsentType.cs#L8-L11) |
| řádek `UserConsent` | jeden na (uživatel, typ); `AcceptVersion` přepíše | `IsGranted`, `GrantedAt`, `WithdrawnAt`, `IpAddress`, `UserAgent`, `DocumentVersion`, `LegalDocumentId` | [UserConsent.cs:15-38](../../src/Cleansia.Core.Domain/Users/UserConsent.cs#L15-L38), [UserConsentEntityConfiguration.cs:42-43](../../src/Cleansia.Infra.Database/EntityConfigurations/UserConsentEntityConfiguration.cs#L42-L43) |
| `TermsOfService` + `PrivacyPolicy` | registrace e-mailem, Google, Apple; u úklidníka při `TermsAccepted` | verze textu trhu; u úklidníka bez dokumentu (`null`) → R3 | [Register.cs:146-155](../../src/Cleansia.Core.AppServices/Features/Auth/Register.cs#L146-L155), [RegisterEmployee.cs:121-127](../../src/Cleansia.Core.AppServices/Features/Auth/RegisterEmployee.cs#L121-L127) |
| tick na objednávce | `CreateOrder` | jen audit `OrderBookingEvidence` (`TermsVersionAccepted`); `UserConsents` nevzniká | grep -rn "consentService" CreateOrder.cs → 0; [CreateOrder.cs:1096-1099](../../src/Cleansia.Core.AppServices/Features/Orders/CreateOrder.cs#L1096-L1099) |
| verze při objednávce | `CreateOrder` | projde jakýkoli udělený souhlas (`IsGranted`, `WithdrawnAt is null`); verze přijatého znění se neporovnává → R3 | [CreateOrder.cs:330-334](../../src/Cleansia.Core.AppServices/Features/Orders/CreateOrder.cs#L330-L334) |
| `DataProcessing` | profil úklidníka `UpdateEmployee`, `Consent == true` povinné | — | [UpdateEmployee.cs:216](../../src/Cleansia.Core.AppServices/Features/Employees/UpdateEmployee.cs#L216) |
| `MarketingEmails` | jen `GrantConsent` (cookie lišta / API); žádný čtenář → R9 | — | grep -rn "MarketingEmails" src/Cleansia.Core.AppServices → 0 mimo enum; [GrantConsent.cs:50](../../src/Cleansia.Core.AppServices/Features/Gdpr/GrantConsent.cs#L50) |
| cookie lišta | `analytics → DataProcessing`, `marketing → MarketingEmails`; nepřihlášený bez záznamu | volbu nic nečte; Google Fonts (`fonts.googleapis.com`) před souhlasem → R9 | [consent-sync.service.ts:38-41](../../src/Cleansia.App/libs/core/customer-services/src/lib/services/consent-sync.service.ts#L38-L41), [index.html:43-49](../../src/Cleansia.App/apps/cleansia.app/src/index.html#L43-L49) |
| odvolání; metadata | `Withdraw()` zapíše `IsGranted = false`, `WithdrawnAt`; IP a `UserAgent` ze serveru, nikdy z těla | — | [WithdrawConsent.cs:41-43](../../src/Cleansia.Core.AppServices/Features/Gdpr/WithdrawConsent.cs#L41-L43), [RequestMetadataProvider.cs:8-9](../../src/Cleansia.Infra.Database/RequestMetadataProvider.cs#L8-L9) |

### 2.3 Seedované texty

| dokument | účinnost | jazyky | upozornění | kdo jej čte | citace |
|---|---|---|---|---|---|
| `TermsOfService`, `title: Obchodní podmínky` | 2026-09-14 | cs/en/ru/sk/uk | řádek 5: `Návrh` — „toto znění zatím neprošlo právní kontrolou" | zákazník, `GET api/Legal/GetDocument` anonymně | [cs.md:2](../../src/Cleansia.Infra.Database/Seed/Legal/customer/terms-of-service/any/2026-09-14/cs.md#L2), [cs.md:5](../../src/Cleansia.Infra.Database/Seed/Legal/customer/terms-of-service/any/2026-09-14/cs.md#L5) |
| `PrivacyPolicy`, `title: Ochrana osobních údajů` | 2026-09-14 | cs/en/ru/sk/uk | řádek 5: tentýž `Návrh` | zákazník | [cs.md:2](../../src/Cleansia.Infra.Database/Seed/Legal/customer/privacy-policy/any/2026-09-14/cs.md#L2), [cs.md:5](../../src/Cleansia.Infra.Database/Seed/Legal/customer/privacy-policy/any/2026-09-14/cs.md#L5) |
| `WorkContract`, `title: Smlouva o dílo` | 2026-09-20 | cs/en/ru/sk/uk | řádek 5: tentýž `Návrh` | zákazník objednávky; úklidník při převzetí | [cs.md:2](../../src/Cleansia.Infra.Database/Seed/Legal/customer/work-contract/any/2026-09-20/cs.md#L2), [cs.md:5](../../src/Cleansia.Infra.Database/Seed/Legal/customer/work-contract/any/2026-09-20/cs.md#L5) |
| audience `employee` | — | — | v parseru povolena, žádný soubor → R3 | — | find src/Cleansia.Infra.Database/Seed/Legal/employee → 0; [LegalSeedResource.cs:28](../../src/Cleansia.Infra.Database/Seed/Legal/LegalSeedResource.cs#L28) |
| seeder, verze a správa | `LegalDocumentSeedHostedService` při každém startu hosta bez podmínky prostředí; text v platnosti neměnný; verze = `yyyy-MM-dd` cesty; admin jen čte | — | — | — | grep -rn "ICommand<" src/Cleansia.Core.AppServices/Features/Legal → 0; [LegalDocumentSeeder.cs:72-79](../../src/Cleansia.Infra.Database/Seed/Legal/LegalDocumentSeeder.cs#L72-L79) |
| text smlouvy | objednatel / zhotovitel; „Cleansia … není smluvní stranou"; odměna „na základě samostatné dohody" → R1 | — | — | — | [cs.md:7](../../src/Cleansia.Infra.Database/Seed/Legal/customer/work-contract/any/2026-09-20/cs.md#L7), [cs.md:19](../../src/Cleansia.Infra.Database/Seed/Legal/customer/work-contract/any/2026-09-20/cs.md#L19) |

### 2.4 Smlouva o dílo

- Objednávka nese `WorkContractDocumentId` textu v platnosti (`ResolveInForceAsync`) při objednání; bez něj nevznikne — [OrderFactory.cs:83-86](../../src/Cleansia.Core.AppServices/Features/Orders/OrderFactory.cs#L83-L86)
- `TakeOrder`: `AcceptedWorkContractTextId` musí patřit dokumentu objednávky — [TakeOrder.cs:64-65](../../src/Cleansia.Core.AppServices/Features/Orders/TakeOrder.cs#L64-L65), [TakeOrder.cs:249-266](../../src/Cleansia.Core.AppServices/Features/Orders/TakeOrder.cs#L249-L266)
- Řádek `WorkContractAcceptance`: `OrderId`, `OrderEmployeeId`, `EmployeeId`, `LegalDocumentTextId`, `DocumentVersion`, `AcceptedOn`, `ClientAudience`, `IpAddress`, `DeviceLabel`, `DeviceId`, `FactsJson` — [WorkContractAcceptance.cs:25-45](../../src/Cleansia.Core.Domain/Contracts/WorkContractAcceptance.cs#L25-L45), [WorkContractAcceptance.cs:47-61](../../src/Cleansia.Core.Domain/Contracts/WorkContractAcceptance.cs#L47-L61)
- `FactsJson` (`WorkContractFacts` : `IWorkContractFacts`): číslo, termín, minuty, cena, měna, přibližná lokalita, pokoje, služby; nikdy ulice ani jméno → R11.1, R11.3, R11.7 — [WorkContractFacts.cs:19-31](../../src/Cleansia.Core.AppServices/Features/Orders/WorkContractFacts.cs#L19-L31), [WorkContractFacts.cs:6-9](../../src/Cleansia.Core.AppServices/Features/Orders/WorkContractFacts.cs#L6-L9)
- Přijetí vázané na sedadlo, append-only; `Pseudonymise` nuluje jen IP, label, id zařízení — [WorkContractAcceptance.cs:102-107](../../src/Cleansia.Core.Domain/Contracts/WorkContractAcceptance.cs#L102-L107)
- `DeviceId` je claim ze session; partnerská aplikace `work_contract_swipe_to_accept` „Přejetím přijmete smlouvu o dílo" → R11.4 — [WorkContractAcceptor.cs:37-39](../../src/Cleansia.Core.AppServices/Services/WorkContractAcceptor.cs#L37-L39), [strings.xml:902](../../src/cleansia_android/partner-app/src/main/res/values-cs/strings.xml#L902)
- Čte zákazník objednávky, úklidník svého přijetí (`ExistsForCallerAsync`) a administrátor; host ne (`CanViewOrderDetail`) — [GetWorkContract.cs:48-63](../../src/Cleansia.Core.AppServices/Features/Orders/GetWorkContract.cs#L48-L63), [OrderController.cs:290-291](../../src/Cleansia.Web.Customer/Controllers/OrderController.cs#L290-L291)
- `AdminReassignOrder` přiřadí (`AddAssignedEmployee`) bez přijetí; `AcceptWorkContract` doplní → R11.6 — grep -n "WorkContract" AdminReassignOrder.cs → 0; [AcceptWorkContract.cs:13-19](../../src/Cleansia.Core.AppServices/Features/Orders/AcceptWorkContract.cs#L13-L19)
- `StartOrder` i `CompleteOrder` vyžadují přijetí pro sedadlo (`HasAcceptedWorkContractForSeatAsync`) → R11.5 — [StartOrder.cs:58-59](../../src/Cleansia.Core.AppServices/Features/Orders/StartOrder.cs#L58-L59), [CompleteOrder.cs:90-91](../../src/Cleansia.Core.AppServices/Features/Orders/CompleteOrder.cs#L90-L91)

## 3. Nabídka, ceny a trhy

### 3.1 Katalog

| položka | co nese · cena | citace |
|---|---|---|
| služba `Service` | `EstimatedTime` v minutách; `ServicePrice` per měna `BasePrice` + `PerRoomPrice`; bez řádku = neprodejné | [Service.cs:21](../../src/Cleansia.Core.Domain/Services/Service.cs#L21), [ServicePrice.cs:30-32](../../src/Cleansia.Core.Domain/Services/ServicePrice.cs#L30-L32) |
| cena služby | `BasePrice + PerRoomPrice × (pokoje + koupelny)` | [OrderPricingCalculator.cs:59](../../src/Cleansia.Core.AppServices/Services/OrderPricingCalculator.cs#L59) |
| balíček `Package`; doplněk `Extra` | paušál per měna; rozdělení balíčku podle `PriceWeight`; doplněk bez ceny se tiše vypustí | [PackagePrice.cs:21](../../src/Cleansia.Core.Domain/Packages/PackagePrice.cs#L21), [OrderPricingCalculator.cs:85-88](../../src/Cleansia.Core.AppServices/Services/OrderPricingCalculator.cs#L85-L88) |
| bez ceny v měně | služba/balíček → výjimka (`PriceOf`), nikdy 0; kurz neexistuje (`chargeSubtotal = baseSubtotal`) | [OrderPricingCalculator.cs:197-202](../../src/Cleansia.Core.AppServices/Services/OrderPricingCalculator.cs#L197-L202), [OrderPricingCalculator.cs:103-111](../../src/Cleansia.Core.AppServices/Services/OrderPricingCalculator.cs#L103-L111) |
| velikost bytu, oblast | `PropertySizePreset` per země; adresa musí být v `ServiceCities`, jinak `CityNotServiced` | [PropertySizePreset.cs:33-53](../../src/Cleansia.Core.Domain/Configuration/PropertySizePreset.cs#L33-L53), [OrderAddressResolver.cs:37-42](../../src/Cleansia.Core.AppServices/Features/Orders/OrderAddressResolver.cs#L37-L42) |
| bez platové konfigurace | položka bez sazby v měně nejde objednat (`HavePayCoverageAsync`) | [CreateOrder.cs:214-215](../../src/Cleansia.Core.AppServices/Features/Orders/CreateOrder.cs#L214-L215) |

### 3.2 Trhy a měny

| země | měna | operátor | dělitel bodů | citace |
|---|---|---|---|---|
| `CZE` (`IsServiced` true) | CZK aktivní | `cleansia-cz`, `IsDefaultMarket` | `LoyaltyPointsDivisor`, `NoShowCredit` — hodnoty A1 | [insert_seed_data.sql:148](../../sql-scripts/insert_seed_data.sql#L148), [insert_seed_data.sql:1014](../../sql-scripts/insert_seed_data.sql#L1014) |
| SVK (`IsServiced` false) | EUR neaktivní | `NULL` | `NULL` | [insert_seed_data.sql:298](../../sql-scripts/insert_seed_data.sql#L298), [insert_seed_data.sql:491-494](../../sql-scripts/insert_seed_data.sql#L491-L494) |
| POL, DEU, AUT, FRA, ITA, ESP, GBR, USA | PLN / EUR / GBP / USD | bez operátora | — | [insert_seed_data.sql:1027-1104](../../sql-scripts/insert_seed_data.sql#L1027-L1104) |

- Trh = obsluhovaná země s konfigurací aktivní měny (`DefaultCurrencyCode`, `IsActive`); `OperatorTenantId` NULL = nikdo neobsluhuje — [GetMarkets.cs:40-58](../../src/Cleansia.Core.AppServices/Features/Markets/GetMarkets.cs#L40-L58), [CountryConfiguration.cs:116-123](../../src/Cleansia.Core.Domain/Configuration/CountryConfiguration.cs#L116-L123)
- Měna objednávky = měna země adresy (`ResolveCurrencyForCountryAsync`), jiná odmítnuta; země musí být trh s operátorem (`TenantNotFound`) → R19 — [CreateOrder.cs:945-950](../../src/Cleansia.Core.AppServices/Features/Orders/CreateOrder.cs#L945-L950), [CreateOrder.cs:187-195](../../src/Cleansia.Core.AppServices/Features/Orders/CreateOrder.cs#L187-L195)
- `LoyaltyPointsDivisor`: body = floor(částka / divisor); NULL = bez bodů; kreditní účet per měna (`GetSpendableAsync`) — [Currency.cs:22-30](../../src/Cleansia.Core.Domain/Internationalization/Currency.cs#L22-L30), [CreateOrder.cs:1137-1142](../../src/Cleansia.Core.AppServices/Features/Orders/CreateOrder.cs#L1137-L1142)
- DPH z ceny včetně: `vat = gross × sazba / (1 + sazba)`; neplátce (`IsVatPayer` false) → `VatBreakdown.NotApplicable` — [VatCalculator.cs:45-49](../../src/Cleansia.Core.AppServices/Services/VatCalculator.cs#L45-L49), [VatCalculator.cs:15-18](../../src/Cleansia.Core.AppServices/Services/VatCalculator.cs#L15-L18)

### 3.3 Cenová nabídka a lhůty

- Předstihy: A1, `ExpressLeadTimeHours` — [BookingPolicy.cs:20-26](../../src/Cleansia.Core.AppServices/Features/Orders/BookingPolicy.cs#L20-L26)
- Příplatek `ExpressSurchargeRate` se počítá ze zlevněného mezisoučtu (`ApplyExpressSurcharge`); odpuštěn při `waiverApplies` (volný express Plus) — [BookingPolicy.cs:270-271](../../src/Cleansia.Core.AppServices/Features/Orders/BookingPolicy.cs#L270-L271), [BookingPolicy.cs:177-180](../../src/Cleansia.Core.AppServices/Features/Orders/BookingPolicy.cs#L177-L180)
- Sleva se počítá z hrubého základu — [OrderFactory.cs:129-130](../../src/Cleansia.Core.AppServices/Features/Orders/OrderFactory.cs#L129-L130)
- Posádka = ceil(minut / `MinutesPerEmployee`); `SpareSeatsPerOrder` podle A1 → R13 — [OrderDuration.cs:27](../../src/Cleansia.Core.Domain/Orders/OrderDuration.cs#L27), [BookingPolicy.cs:136](../../src/Cleansia.Core.AppServices/Features/Orders/BookingPolicy.cs#L136)
- Limit `MaxBookableOrderSpanHours`: A1 — [BookingPolicy.cs:146-151](../../src/Cleansia.Core.AppServices/Features/Orders/BookingPolicy.cs#L146-L151)
- Server kontroluje `IsBelowMinimumLeadTime`, nikoli denní okno (A1); rozsah omezuje na `MaxRooms`/`MaxBathrooms` — [CreateOrder.cs:162-167](../../src/Cleansia.Core.AppServices/Features/Orders/CreateOrder.cs#L162-L167), [CreateOrder.cs:95-98](../../src/Cleansia.Core.AppServices/Features/Orders/CreateOrder.cs#L95-L98)
- Server cenu přepočítá; odeslaná hrubá `TotalPrice` musí sedět (`TotalPriceNotMatch`); cena > 0, minimální částka neexistuje — [CreateOrder.cs:646-651](../../src/Cleansia.Core.AppServices/Features/Orders/CreateOrder.cs#L646-L651), [CreateOrder.cs:169-171](../../src/Cleansia.Core.AppServices/Features/Orders/CreateOrder.cs#L169-L171)
- Nabídka vrací cenu a slevy; promo zpracuje pokladna — [QuoteOrder.cs:462-470](../../src/Cleansia.Core.AppServices/Features/Orders/QuoteOrder.cs#L462-L470), [QuoteOrder.cs:405-409](../../src/Cleansia.Core.AppServices/Features/Orders/QuoteOrder.cs#L405-L409)
- Cena položky zmrazena na objednávce (`unitBasePrice`, `lineTotal`) → R12; `PaymentType` jen `Cash = 1`, `Card = 2` — [OrderFactory.cs:188-213](../../src/Cleansia.Core.AppServices/Features/Orders/OrderFactory.cs#L188-L213), [PaymentType.cs:8-9](../../src/Cleansia.Core.Domain/Enums/PaymentType.cs#L8-L9)

### 3.4 Slevy a skládání

| sleva | výpočet · skládání | citace |
|---|---|---|
| Plus | `DiscountPercentage` (seed `PLUS_MONTHLY` 5 %) × hrubý základ, bez minima | [OrderFactory.cs:108-115](../../src/Cleansia.Core.AppServices/Features/Orders/OrderFactory.cs#L108-L115), [insert_seed_data.sql:1806](../../sql-scripts/insert_seed_data.sql#L1806) |
| věrnostní stupeň | `DiscountPercent` × základ; pod `MinimumOrderAmountForDiscount` 0; minimum jen ve výchozí měně | [LoyaltyService.cs:246-259](../../src/Cleansia.Core.AppServices/Services/LoyaltyService.cs#L246-L259), [LoyaltyService.cs:231-244](../../src/Cleansia.Core.AppServices/Services/LoyaltyService.cs#L231-L244) |
| strop | Plus + tier ≤ `MaxCombinedDiscountFraction` 12 %; nad ním kráceny poměrně | [OrderFactory.cs:54](../../src/Cleansia.Core.AppServices/Features/Orders/OrderFactory.cs#L54), [OrderFactory.cs:361-371](../../src/Cleansia.Core.AppServices/Features/Orders/OrderFactory.cs#L361-L371) |
| promo | procentní, nebo pevná min(částka, základ); nahrazuje Plus + tier, je-li větší; nikdy nesčítá | [PromoCodeService.cs:264-277](../../src/Cleansia.Core.AppServices/Services/PromoCodeService.cs#L264-L277), [OrderFactory.cs:373-383](../../src/Cleansia.Core.AppServices/Features/Orders/OrderFactory.cs#L373-L383) |
| pořadí a zaokrouhlení | `ResolveLoy003Discount` → promo → `ApplyExpressSurcharge`; slevy 2 místa `AwayFromZero`, kredit `Math.Floor` | [OrderFactory.cs:121-130](../../src/Cleansia.Core.AppServices/Features/Orders/OrderFactory.cs#L121-L130), [OrderFactory.cs:368-369](../../src/Cleansia.Core.AppServices/Features/Orders/OrderFactory.cs#L368-L369) |
| kredit | platidlo, ne sleva: `TotalPrice`, DPH, body nezměněny; jen karta, stejná měna, max `MaxCreditShareOfOrder` 70 % | [Order.cs:236-239](../../src/Cleansia.Core.Domain/Orders/Order.cs#L236-L239), [BookingPolicy.cs:98](../../src/Cleansia.Core.AppServices/Features/Orders/BookingPolicy.cs#L98) |
| snapshot | `TierDiscountAmount`, `TierAtPurchase`, `PromoDiscountAmount`, `PromoCodeId`, `MembershipDiscountAmount` | [Order.cs:327-362](../../src/Cleansia.Core.Domain/Orders/Order.cs#L327-L362) |

## 4. Objednávka a její stavy

### 4.1 Osa objednávky (`OrderStatus`)

| stav | kdo zapisuje | citace |
|---|---|---|
| `New` 0 | `OrderFactory` při vzniku; `ReturnToBoardIfUnstaffed` při návratu | [OrderFactory.cs:328](../../src/Cleansia.Core.AppServices/Features/Orders/OrderFactory.cs#L328), [Order.cs:858](../../src/Cleansia.Core.Domain/Orders/Order.cs#L858) |
| `Pending` 1 | bez producenta | grep -rn "OrderStatus.Pending" src/ → jen čtení; [OrderStatus.cs:22](../../src/Cleansia.Core.Domain/Enums/OrderStatus.cs#L22) |
| `Confirmed` 2 | `TakeOrder`, `AdminReassignOrder`, jen z `New` | [TakeOrder.cs:361-365](../../src/Cleansia.Core.AppServices/Features/Orders/TakeOrder.cs#L361-L365), [AdminReassignOrder.cs:136-139](../../src/Cleansia.Core.AppServices/Features/Orders/AdminReassignOrder.cs#L136-L139) |
| `OnTheWay` 3 | `NotifyOnTheWay`, jen z `Confirmed` | [NotifyOnTheWay.cs:128-129](../../src/Cleansia.Core.AppServices/Features/Orders/NotifyOnTheWay.cs#L128-L129) |
| `InProgress` 4 | `StartOrder`, z `Confirmed` nebo `OnTheWay` | [StartOrder.cs:186-187](../../src/Cleansia.Core.AppServices/Features/Orders/StartOrder.cs#L186-L187) |
| `Completed` 5 | `CompleteOrder`, jen z `InProgress` | [CompleteOrder.cs:283-284](../../src/Cleansia.Core.AppServices/Features/Orders/CompleteOrder.cs#L283-L284) |
| `Cancelled` 6 | `CustomerOrderCancellation`; `PlatformOrderCancellation` (admin, útlum) | [CustomerOrderCancellation.cs:49-51](../../src/Cleansia.Core.AppServices/Features/Orders/CustomerOrderCancellation.cs#L49-L51), [PlatformOrderCancellation.cs:36-37](../../src/Cleansia.Core.AppServices/Services/PlatformOrderCancellation.cs#L36-L37) |
| libovolný vpřed | `AdminOverrideOrderStatus` (`TargetStatus`); jen vpřed — `targetRank <= currentRank` i `Cancelled` odmítnuty; `Confirmed` bez `AssignedEmployees` odmítnut | [AdminOverrideOrderStatus.cs:116-125](../../src/Cleansia.Core.AppServices/Features/Orders/AdminOverrideOrderStatus.cs#L116-L125), [AdminOverrideOrderStatus.cs:132-137](../../src/Cleansia.Core.AppServices/Features/Orders/AdminOverrideOrderStatus.cs#L132-L137) |
| `CurrentStatus` | poslední řádek historie; jediný zapisovatel `AddOrderStatus`; `Confirmed → New` jen `ReturnToBoardIfUnstaffed` při prázdné posádce | [Order.cs:686-700](../../src/Cleansia.Core.Domain/Orders/Order.cs#L686-L700), [Order.cs:851-860](../../src/Cleansia.Core.Domain/Orders/Order.cs#L851-L860) |

### 4.2 Osa platby (`PaymentStatus`)

| stav | kdo zapisuje | citace |
|---|---|---|
| `Pending` 1 | `OrderFactory` při vzniku | [OrderFactory.cs:166](../../src/Cleansia.Core.AppServices/Features/Orders/OrderFactory.cs#L166) |
| `Paid` 2 | Stripe webhook (checkout dokončen); `OrderStatus` nedotčen, dál `New` | [HandlePaymentNotification.cs:297-309](../../src/Cleansia.Core.AppServices/Features/Payments/HandlePaymentNotification.cs#L297-L309) |
| `Paid` 2 | `MarkCashCollected`; `ConfirmRecurringOrder` u hotovostního výskytu | [Order.cs:713-720](../../src/Cleansia.Core.Domain/Orders/Order.cs#L713-L720), [ConfirmRecurringOrder.cs:158](../../src/Cleansia.Core.AppServices/Features/Orders/ConfirmRecurringOrder.cs#L158) |
| `Failed` 3 | `CleanupStalePendingOrders`; webhook expirované session | [CleanupStalePendingOrders.cs:99](../../src/Cleansia.Core.AppServices/Features/Orders/CleanupStalePendingOrders.cs#L99), [HandlePaymentNotification.cs:360](../../src/Cleansia.Core.AppServices/Features/Payments/HandlePaymentNotification.cs#L360) |
| `Refunded` 4, `PartiallyRefunded` 6 | jen `RefundService` podle součtu vratek proti kartové části | [RefundService.cs:182-185](../../src/Cleansia.Core.AppServices/Services/RefundService.cs#L182-L185) |
| `Disputed` 5 | bez producenta | grep -rn "PaymentStatus.Disputed" src/ → jen čtení; [PaymentStatus.cs:12](../../src/Cleansia.Core.Domain/Enums/PaymentStatus.cs#L12) |
| selhaná platba | `payment_intent.payment_failed` stav nemění; dál `Pending` | [HandlePaymentNotification.cs:240-249](../../src/Cleansia.Core.AppServices/Features/Payments/HandlePaymentNotification.cs#L240-L249) |
| vztah os | `Confirmed` neříká nic o penězích; zaplacená karta odpočívá `New + Paid`; `ActualPaymentType = Cash` při `SettledInCash` | [OrderAvailability.cs:17-23](../../src/Cleansia.Core.Domain/Orders/OrderAvailability.cs#L17-L23), [Order.cs:57-65](../../src/Cleansia.Core.Domain/Orders/Order.cs#L57-L65) |

### 4.3 Co zákazník smí

| úkon | s účtem | host | citace |
|---|---|---|---|
| vytvořit, nabídka `Quote`, seznam, detail | ano | `CreateOrder` anonymně; `Lookup` přes `AccessToken` | [OrderController.cs:76](../../src/Cleansia.Web.Customer/Controllers/OrderController.cs#L76), [GuestOrderAccess.cs:18-42](../../src/Cleansia.Core.AppServices/Features/Orders/GuestOrderAccess.cs#L18-L42) |
| storno a náhled poplatku | `CancelOrder` (`CustomerOnly`), jen vlastní `UserId` | `CancelGuest`, `GuestCancellationPreview` | [CancelOrder.cs:110-122](../../src/Cleansia.Core.AppServices/Features/Orders/CancelOrder.cs#L110-L122), [CancelGuestOrder.cs:18-19](../../src/Cleansia.Core.AppServices/Features/Orders/CancelGuestOrder.cs#L18-L19) |
| doklad, fotky, hodnocení (`Rating` 1–5), reklamace (`CanCreateDispute`), `ResumeCheckout`, `GetWorkContract` | ano | ne | [SubmitOrderReview.cs:48-53](../../src/Cleansia.Core.AppServices/Features/Orders/SubmitOrderReview.cs#L48-L53), [DisputeController.cs:17-55](../../src/Cleansia.Web.Customer/Controllers/DisputeController.cs#L17-L55) |
| promo, kredit, body, Plus, preferovaný úklidník, opakování | ano | ne (`PromoNamesASignedInCustomer` → `promo.requires_account`) | [CreateOrder.cs:672-674](../../src/Cleansia.Core.AppServices/Features/Orders/CreateOrder.cs#L672-L674), [OrderFactory.cs:88-91](../../src/Cleansia.Core.AppServices/Features/Orders/OrderFactory.cs#L88-L91) |
| přeplánování, změna rozsahu, spropitné, bankovní převod, poznámka → R12 | ne (poznámku přidá jen úklidník) | ne | grep -rn "Reschedule\|Tip\b\|BankTransfer\|AddOrderNote" src/Cleansia.Web.Customer/Controllers → 0; [OrderController.cs:20-297](../../src/Cleansia.Web.Customer/Controllers/OrderController.cs#L20-L297) |
| důvod storna | `SystemCancellationReason` ano; text administrátora nikdy | totéž | [OrderMappers.cs:279-284](../../src/Cleansia.Core.AppServices/Mappers/OrderMappers.cs#L279-L284) |

### 4.4 Preferovaný úklidník a obsazení

- Jen člen Plus s dokončenou objednávkou s úklidníkem v měně objednávky — [CreateOrder.cs:297-306](../../src/Cleansia.Core.AppServices/Features/Orders/CreateOrder.cs#L297-L306)
- Hold preferovaného úklidníka: zlomek předstihu, strop a minimum podle A1 — [BookingPolicy.cs:217-218](../../src/Cleansia.Core.AppServices/Features/Orders/BookingPolicy.cs#L217-L218), [BookingPolicy.cs:256-258](../../src/Cleansia.Core.AppServices/Features/Orders/BookingPolicy.cs#L256-L258)
- Zamítnutý hold (bez Plus, neschválený, jiná země, ztlumené push) rezervaci neblokuje — [PreferredCleanerHoldResolver.cs:42-90](../../src/Cleansia.Core.AppServices/Services/PreferredCleanerHoldResolver.cs#L42-L90)
- Během holdu (`NotHeldFrom`) viditelná jen beneficientovi, ostatním `OrderNotFound`; `MaxPreferredOfferRounds` 2, nástěnce ≥ 80 % okna — [OrderVisibility.cs:74-79](../../src/Cleansia.Core.Domain/Orders/OrderVisibility.cs#L74-L79), [BookingPolicy.cs:232](../../src/Cleansia.Core.AppServices/Features/Orders/BookingPolicy.cs#L232)
- Odmítnutí ukončí rezervaci (`EndPreferredHold`) a vyvolá `NotifyPreferredOfferClosedAsync` — [DeclinePreferredOffer.cs:77-79](../../src/Cleansia.Core.AppServices/Features/Orders/DeclinePreferredOffer.cs#L77-L79)
- Sedadla `AvailableSpots = MaxEmployees − přiřazení`; souběh rozhodne unikátní index (`NoAvailableSpots`) — [Order.cs:128-129](../../src/Cleansia.Core.Domain/Orders/Order.cs#L128-L129), [TakeOrder.cs:374-405](../../src/Cleansia.Core.AppServices/Features/Orders/TakeOrder.cs#L374-L405)
- Objednávka v jiné měně než mzda úklidníka se nenabízí; preferovaný úklidník se partnerovi nezobrazuje — [OrderVisibility.cs:53-59](../../src/Cleansia.Core.Domain/Orders/OrderVisibility.cs#L53-L59), [Order.cs:364-374](../../src/Cleansia.Core.Domain/Orders/Order.cs#L364-L374)

### 4.5 Opakované úklidy

- Šablona `RecurringBookingTemplate`: `Weekly = 1`, `Biweekly = 2`, `Monthly = 3`; adresa (`SavedAddressId`), služby, balíčky, `PaymentType` — [RecurringBookingTemplate.cs:23-64](../../src/Cleansia.Core.Domain/Bookings/RecurringBookingTemplate.cs#L23-L64), [RecurrenceFrequency.cs:9-11](../../src/Cleansia.Core.Domain/Bookings/RecurrenceFrequency.cs#L9-L11)
- Vyžaduje aktivní Plus (`RecurringTemplateMembershipRequired`); bez placeného Plus se šablona přeskočí a jde push `NotificationEventCatalog.RecurringPaused` — [CreateRecurringBooking.cs:187-197](../../src/Cleansia.Core.AppServices/Features/Bookings/CreateRecurringBooking.cs#L187-L197), [MaterializeRecurringBookingTemplate.cs:121-143](../../src/Cleansia.Core.AppServices/Features/Bookings/MaterializeRecurringBookingTemplate.cs#L121-L143)
- Materializace podle A1: `Monthly` není kalendářní měsíc; krok a zarovnání na den týdne obvykle dávají 35 dní — [MaterializeRecurringBookingTemplate.cs:324-342](../../src/Cleansia.Core.AppServices/Features/Bookings/MaterializeRecurringBookingTemplate.cs#L324-L342)
- Výskyt nemá doplňky ani promo; tier/Plus sleva platí. Surový výpočet má `cleaningDateUtc: null`, ale `OrderFactory` posoudí express podle termínu výskytu — [MaterializeRecurringBookingTemplate.cs:238-264](../../src/Cleansia.Core.AppServices/Features/Bookings/MaterializeRecurringBookingTemplate.cs#L238-L264), [OrderFactory.cs:124-130](../../src/Cleansia.Core.AppServices/Features/Orders/OrderFactory.cs#L124-L130)
- Nabízitelný až `Paid`; potvrzení vlastníkem (`ConfirmRecurringOrder`): hotovost → `Paid`, karta → PaymentIntent na `AmountDueOnCard` — [OrderAvailability.cs:82-83](../../src/Cleansia.Core.Domain/Orders/OrderAvailability.cs#L82-L83), [ConfirmRecurringOrder.cs:321-329](../../src/Cleansia.Core.AppServices/Features/Orders/ConfirmRecurringOrder.cs#L321-L329)
- Připomínka `recurring.scheduled` 6–26 h před (`LeadHoursLow/High`) — [SendRecurringOrderReminders.cs:22](../../src/Cleansia.Core.AppServices/Features/Bookings/SendRecurringOrderReminders.cs#L22)
- Nepotvrzený výskyt zrušen `MissedConfirmGraceHours` 1 h před začátkem, bez poplatku, kredit vrácen (`ReturnUnpaidOrderCreditAsync`) — [AutoCancelStaleRecurringOrders.cs:38](../../src/Cleansia.Core.AppServices/Features/Bookings/AutoCancelStaleRecurringOrders.cs#L38), [AutoCancelStaleRecurringOrders.cs:112-133](../../src/Cleansia.Core.AppServices/Features/Bookings/AutoCancelStaleRecurringOrders.cs#L112-L133)
- Storno výskytu = běžný `CancelOrder`; výskyty jsou nezávislé řádky `Order` — [RecurringBookingTemplate.cs:11-12](../../src/Cleansia.Core.Domain/Bookings/RecurringBookingTemplate.cs#L11-L12)

### 4.6 Neobsazená a opuštěná zakázka

- Sweep `0 */15 * * * *`: `New`/`Confirmed` bez posádky (`NeverStarted`, `AssignedEmployees`) po `GraceMinutes` od začátku, v okně `LookbackHours` → `Cancelled`; `OnTheWay`/`InProgress` se neruší — [CleanupStalePendingOrdersFunction.cs:10](../../src/Cleansia.Functions/Functions/CleanupStalePendingOrdersFunction.cs#L10), [CancelUnfilledOrders.cs:112-118](../../src/Cleansia.Core.AppServices/Features/Orders/CancelUnfilledOrders.cs#L112-L118)
- `CancelledBy.System`, důvod `no_cleaner_available`, plná vratka `ServiceNotRendered` — [CancelUnfilledOrders.cs:149-155](../../src/Cleansia.Core.AppServices/Features/Orders/CancelUnfilledOrders.cs#L149-L155), [CancelUnfilledOrders.cs:164-175](../../src/Cleansia.Core.AppServices/Features/Orders/CancelUnfilledOrders.cs#L164-L175)
- Omluvný kredit `Currency.NoShowCredit` (CZK 250 Kč) jednou; host nic; měna bez `NoShowCredit` nic → R15 — [CancelUnfilledOrders.cs:281-305](../../src/Cleansia.Core.AppServices/Features/Orders/CancelUnfilledOrders.cs#L281-L305), [insert_seed_data.sql:490](../../sql-scripts/insert_seed_data.sql#L490)
- Opuštěná pokladna: karta `PaymentStatus.Pending` starší `OlderThanHours` 1 h → `Failed` + `Cancelled`, `payment_not_completed`, kredit zpět — [CleanupStalePendingOrdersHandler.cs:28](../../src/Cleansia.Functions.Core/Handlers/CleanupStalePendingOrdersHandler.cs#L28), [CleanupStalePendingOrders.cs:99-111](../../src/Cleansia.Core.AppServices/Features/Orders/CleanupStalePendingOrders.cs#L99-L111)
- Web vytváří objednávku (`CreateOrder`) před přesměrováním na Stripe; `ResumeCheckout` vrací tutéž session (`checkout-{orderId}`) — [ResumeOrderCheckout.cs:18-24](../../src/Cleansia.Core.AppServices/Features/Orders/ResumeOrderCheckout.cs#L18-L24), [ResumeOrderCheckout.cs:26-30](../../src/Cleansia.Core.AppServices/Features/Orders/ResumeOrderCheckout.cs#L26-L30)

## 5. Platby, doklady a fiskalizace

### 5.1 Metody

| metoda | mechanismus | citace |
|---|---|---|
| karta | web: Stripe Checkout Session (`PaymentMethodTypes = ["card"]`); mobil: `CreatePaymentIntent` na `AmountDueOnCard` | [StripeClient.cs:53](../../src/Cleansia.Infra.Clients/Stripe/StripeClient.cs#L53), [CreatePaymentIntent.cs:133-141](../../src/Cleansia.Core.AppServices/Features/Orders/CreatePaymentIntent.cs#L133-L141) |
| hotovost | `MarkCashCollected`: schválený přiřazený úklidník, jen `InProgress`; částka se neukládá; s kreditem odmítnuto (`CashNotCollectableOnCreditOrder`) | [MarkCashCollected.cs:82](../../src/Cleansia.Core.AppServices/Features/Orders/MarkCashCollected.cs#L82), [MarkCashCollected.cs:147-155](../../src/Cleansia.Core.AppServices/Features/Orders/MarkCashCollected.cs#L147-L155) |
| dvojí úhrada | karta po hotovosti: `EscalateDoubleSettlement` → spor `Escalated`, `IncorrectAmount`, bez refundu | [HandlePaymentNotification.cs:420-434](../../src/Cleansia.Core.AppServices/Features/Payments/HandlePaymentNotification.cs#L420-L434) |
| bankovní převod zákazníka; kill switch | převod neexistuje; `Stripe:Enabled` false → `order.payment_gateway_unavailable`, refundy neblokuje | grep -rn "BankTransfer" src/*.cs → jen `EmployeeInvoice`; `StripeConfig.Enabled` [StripeConfig.cs:8](../../src/Cleansia.Infra.Common/Configuration/StripeConfig.cs#L8) |
| webhook | podpis `WebhookSecret`; idempotence `ProcessedStripeEvents`; `[AllowAnonymous]` na Customer, Mobile Customer i Partner hostu | [HandlePaymentNotification.cs:138-148](../../src/Cleansia.Core.AppServices/Features/Payments/HandlePaymentNotification.cs#L138-L148), [HandlePaymentNotification.cs:150-173](../../src/Cleansia.Core.AppServices/Features/Payments/HandlePaymentNotification.cs#L150-L173) |
| zamítnutá platba; výmaz | objednávka se neruší, admin `admin.payment.failed`; Stripe Customer se nemaže | grep -rn "DeleteCustomerAsync" src/*.cs → 0; [HandlePaymentNotification.cs:259-276](../../src/Cleansia.Core.AppServices/Features/Payments/HandlePaymentNotification.cs#L259-L276) |

### 5.2 Doklad

| kdy vzniká · co obsahuje | citace |
|---|---|
| hotovost: `GenerateReceipt` zařazen při vytvoření objednávky (`PaymentStatus` ještě `Pending`) | [OrderPaymentDispatcher.cs:82-92](../../src/Cleansia.Core.AppServices/Features/Orders/OrderPaymentDispatcher.cs#L82-L92) |
| karta: `GenerateReceipt` po webhooku `Paid`; záloha při dokončení bez `Receipt` | [HandlePaymentNotification.cs:315-321](../../src/Cleansia.Core.AppServices/Features/Payments/HandlePaymentNotification.cs#L315-L321), [CompleteOrder.cs:289-298](../../src/Cleansia.Core.AppServices/Features/Orders/CompleteOrder.cs#L289-L298) |
| vystavitel `CompanyInfo` dle země adresy; PDF: LegalName, IČO, DIČ, adresa, IBAN → R1 | [ReceiptService.cs:44-54](../../src/Cleansia.Core.AppServices/Services/ReceiptService.cs#L44-L54), [ReceiptService.cs:555-574](../../src/Cleansia.Core.AppServices/Services/ReceiptService.cs#L555-L574) |
| číslo `RCP-{rok}-{NNNN}` z `FiscalCounters` per společnost; bez fiskálního režimu rok čítače `NoAnnualResetYear` = 0 | [Constants.cs:83-85](../../src/Cleansia.Core.AppServices/Common/Constants.cs#L83-L85), [FiscalSequenceScope.cs:12-26](../../src/Cleansia.Core.Fiscal.Abstractions/FiscalSequenceScope.cs#L12-L26) |
| PDF: služby, balíčky, doplňky, express a záporné slevy; `InCents` při tvorbě objednávky vyrovná haléře, `ItemLines` tiskne uložené složky | [OrderFactory.cs:421-454](../../src/Cleansia.Core.AppServices/Features/Orders/OrderFactory.cs#L421-L454), [DefaultReceiptLayoutBuilder.cs:300-340](../../src/Cleansia.Infra.Services/Pdf/Layouts/DefaultReceiptLayoutBuilder.cs#L300-L340) |
| kredit `CreditApplied` pod Total, celkovou cenu nemění; DPH a označení neplátce vycházejí z uložené daňové pozice objednávky | [DefaultReceiptLayoutBuilder.cs:425-428](../../src/Cleansia.Infra.Services/Pdf/Layouts/DefaultReceiptLayoutBuilder.cs#L425-L428), [ReceiptService.cs:537-554](../../src/Cleansia.Core.AppServices/Services/ReceiptService.cs#L537-L554) |
| `VatNumber` neplátce se netiskne; neplátce má `VatNotice` | [DefaultReceiptLayoutBuilder.cs:155](../../src/Cleansia.Infra.Services/Pdf/Layouts/DefaultReceiptLayoutBuilder.cs#L155), [DefaultReceiptLayoutBuilder.cs:172-173](../../src/Cleansia.Infra.Services/Pdf/Layouts/DefaultReceiptLayoutBuilder.cs#L172-L173) |
| PDF: popisky, stavy a metody v cs/en/sk/uk/ru; jazyk rezervace → preference účtu → zpráva | [ReceiptLabels.cs:422-428](../../src/Cleansia.Infra.Services/Pdf/Models/ReceiptLabels.cs#L422-L428), [GenerateReceiptHandler.cs:295-296](../../src/Cleansia.Functions.Core/Handlers/GenerateReceiptHandler.cs#L295-L296) |
| Existující doklad drží jazyk; data vystavení a úklidu jsou v pásmu trhu, náhradně UTC | [ReceiptService.cs:207-218](../../src/Cleansia.Core.AppServices/Services/ReceiptService.cs#L207-L218), [ReceiptService.cs:498-544](../../src/Cleansia.Core.AppServices/Services/ReceiptService.cs#L498-L544) |
| Potvrzení hotovosti přegeneruje stejný doklad jako zaplacený; žádný další e-mail ani fiskální registrace | [MarkCashCollected.cs:168-181](../../src/Cleansia.Core.AppServices/Features/Orders/MarkCashCollected.cs#L168-L181), [GenerateReceiptHandler.cs:87-106](../../src/Cleansia.Functions.Core/Handlers/GenerateReceiptHandler.cs#L87-L106) |
| e-mail s PDF na `order.CustomerEmail`; stažení `DownloadReceipt` jen `Authenticated`; dobropis ani firemní faktura neexistují | grep -rin "CreditNote\|dobropis" src/*.cs → jen komentáře; [OrderController.cs:167-168](../../src/Cleansia.Web.Customer/Controllers/OrderController.cs#L167-L168) |

### 5.3 Fiskalizace

- Jediná implementace `CzechEet2FiscalService` je stub `NOT_IMPLEMENTED`; `ProviderKey "cz-eet2"`, `CountryCode "CZ"` → R21 — [CzechEet2FiscalService.cs:53-55](../../src/Cleansia.Infra.Fiscal/Countries/Czechia/CzechEet2FiscalService.cs#L53-L55), [CzechEet2FiscalService.cs:21-23](../../src/Cleansia.Infra.Fiscal/Countries/Czechia/CzechEet2FiscalService.cs#L21-L23)
- `Fiscal:CzechEet2:Enabled: false` na všech hostech; fallback `NoOpFiscalService` → `NotRequired` — [appsettings.json:85](../../src/Cleansia.Web.Customer/appsettings.json#L85), [NoOpFiscalService.cs:13-25](../../src/Cleansia.Infra.Fiscal/NoOp/NoOpFiscalService.cs#L13-L25)
- Registrace nese `TotalPrice`, `VatAmount`, měnu, IČO, DIČ, kontakt zákazníka; položky bez extras a slevy — [ReceiptService.cs:335-351](../../src/Cleansia.Core.AppServices/Services/ReceiptService.cs#L335-L351)
- Selhání: retry jen `Transient`/`Unknown`; `MaxFiscalRetries` 10; admin řeší přes `CanManageFiscalFailures` (`AccountantOrAbove`) — [OrderReceipt.cs:183](../../src/Cleansia.Core.Domain/Receipts/OrderReceipt.cs#L183), [PolicyBuilder.cs:150](../../src/Cleansia.Core.AppServices/Authentication/PolicyBuilder.cs#L150)

## 6. Storno, refundace a kredity

### 6.1 Sazebník

| situace | sazba | citace |
|---|---|---|
| bez `AssignedEmployees`; `OopsWindowMinutesStandard` 15 min od rezervace | 0 | [BookingPolicy.cs:334-337](../../src/Cleansia.Core.AppServices/Features/Orders/BookingPolicy.cs#L334-L337), [BookingPolicy.cs:80](../../src/Cleansia.Core.AppServices/Features/Orders/BookingPolicy.cs#L80) |
| `OopsWindowMinutesFirstTime` 60 min | nikdy (`IsFirstTimeCustomer` = false) → R4 | [BookingPolicy.cs:83](../../src/Cleansia.Core.AppServices/Features/Orders/BookingPolicy.cs#L83), [CancellationAssessor.cs:32](../../src/Cleansia.Core.AppServices/Features/Orders/CancellationAssessor.cs#L32) |
| ≥ `FreeCancellationHours` 24 h před začátkem | 0 | [BookingPolicy.cs:65](../../src/Cleansia.Core.AppServices/Features/Orders/BookingPolicy.cs#L65) |
| 4–24 h | `PartialCancellationFeeRate` 25 % | [BookingPolicy.cs:68](../../src/Cleansia.Core.AppServices/Features/Orders/BookingPolicy.cs#L68) |
| < `PartialCancellationHours` 4 h | `LastMinuteCancellationFeeRate` 50 % | [BookingPolicy.cs:74](../../src/Cleansia.Core.AppServices/Features/Orders/BookingPolicy.cs#L74), [BookingPolicy.cs:71](../../src/Cleansia.Core.AppServices/Features/Orders/BookingPolicy.cs#L71) |
| sazba 100 %; člen Plus | neexistuje (`CancellationFeeRateFor`) → R4; `FreeCancellationWindowHours` (seed 4 h) nahrazuje 24 h, pak rovnou 50 % | [BookingPolicy.cs:356-361](../../src/Cleansia.Core.AppServices/Features/Orders/BookingPolicy.cs#L356-L361), [CancellationPolicyResolver.cs:35-44](../../src/Cleansia.Core.AppServices/Services/CancellationPolicyResolver.cs#L35-L44) |
| vratka | round(`TotalPrice` × (1 − sazba), 2); poplatek je zbytek | [CancellationAssessor.cs:69-77](../../src/Cleansia.Core.AppServices/Features/Orders/CancellationAssessor.cs#L69-L77) |
| blokováno | `Cancelled`, `Completed`, `InProgress`; `OnTheWay` stornovat lze | [CancellationAssessor.cs:39-45](../../src/Cleansia.Core.AppServices/Features/Orders/CancellationAssessor.cs#L39-L45) |
| hotovost | `FeeRate` zapsán, nikdy inkasován; vratka jen u `PaymentType.Card`; čtenář jen archiv | [CustomerOrderCancellation.cs:90-95](../../src/Cleansia.Core.AppServices/Features/Orders/CustomerOrderCancellation.cs#L90-L95), [CompanyArchiveService.cs:374](../../src/Cleansia.Core.AppServices/Services/CompanyArchiveService.cs#L374) |

### 6.2 Kdo smí zrušit

| kdo | podmínky · efekt | citace |
|---|---|---|
| zákazník `CancelOrder` (`CustomerOnly`) | jen vlastní; sazebník; body odebrány; úklidníci `order.assignment_cancelled` | [CancelOrder.cs:18-21](../../src/Cleansia.Core.AppServices/Features/Orders/CancelOrder.cs#L18-L21), [CustomerOrderCancellation.cs:65-69](../../src/Cleansia.Core.AppServices/Features/Orders/CustomerOrderCancellation.cs#L65-L69) |
| host `CancelGuestOrder` | `AccessToken`; aktor „System"; `RevokeAsync` odvolá tokeny | [CancelGuestOrder.cs:18-20](../../src/Cleansia.Core.AppServices/Features/Orders/CancelGuestOrder.cs#L18-L20), [CancelGuestOrder.cs:61](../../src/Cleansia.Core.AppServices/Features/Orders/CancelGuestOrder.cs#L61) |
| administrátor `AdminCancelOrder` (`SupportOrAbove`) | `feeRate: 0m`, plná vratka; audit `order.cancel`; důvod zákazníkovi nejde → R15 | [PlatformOrderCancellation.cs:27-35](../../src/Cleansia.Core.AppServices/Services/PlatformOrderCancellation.cs#L27-L35), [AdminCancelOrder.cs:73-90](../../src/Cleansia.Core.AppServices/Features/Orders/AdminCancelOrder.cs#L73-L90) |
| systém; úklidník | sweepy (§4.6), webhook expirace, útlum (`CancelledBy.System`); úklidník rušit nemůže, `DropOrder` jen uvolní sedadlo (`HasTakeableSeat`) | [CompanyWindDownService.cs:304-325](../../src/Cleansia.Core.AppServices/Services/CompanyWindDownService.cs#L304-L325), [DropOrder.cs:14-28](../../src/Cleansia.Core.AppServices/Features/Orders/DropOrder.cs#L14-L28) |

### 6.3 Refundace

| cesta | kdo · podmínka | citace |
|---|---|---|
| jediná cesta zpět | `IssueRefundAsync` → `RefundCheckoutSessionAsync` / `RefundPaymentIntentAsync`; důvod u Stripe vždy `RequestedByCustomer`; při stornu automaticky (karta + `Paid`) | [RefundService.cs:140-153](../../src/Cleansia.Core.AppServices/Services/RefundService.cs#L140-L153), [StripeClient.cs:121](../../src/Cleansia.Infra.Clients/Stripe/StripeClient.cs#L121) |
| plný `AdminRefundOrder` | `CanRefundOrder` = `SupportOrAbove`; bez okna, bez důvodu; stav objednávky se nemění (`AdminCancelOrder` je zvlášť) | [AdminRefundOrder.cs:76-85](../../src/Cleansia.Core.AppServices/Features/Orders/AdminRefundOrder.cs#L76-L85), [PolicyBuilder.cs:38](../../src/Cleansia.Core.AppServices/Authentication/PolicyBuilder.cs#L38) |
| částečný `IssuePartialRefund` | po řádcích; `CanIssueRefund` = `SupportOrAbove`; `RefundWindowDays` 14 dní, mimo okno `OverrideReason` | [IssuePartialRefund.cs:28-34](../../src/Cleansia.Core.AppServices/Features/Refunds/IssuePartialRefund.cs#L28-L34), [RefundPolicy.cs:17](../../src/Cleansia.Core.AppServices/Features/Refunds/RefundPolicy.cs#L17) |
| spor `ResolveDispute` | `RefundAmount` > 0 → `DisputeResolution`; neúspěšná refundace vrátí chybu před `Resolved`; uzavřený spor nelze znovu rozhodnout | [ResolveDispute.cs:68-107](../../src/Cleansia.Core.AppServices/Features/Disputes/ResolveDispute.cs#L68-L107) |
| strop, dělení, hotovost | `CardRefundCeiling` = `CardChargedAmount` − consumed; proporčně karta / kredit; hotovost `RefundOrderNotRefundable` | [RefundService.cs:224-225](../../src/Cleansia.Core.AppServices/Services/RefundService.cs#L224-L225), [RefundService.cs:59-63](../../src/Cleansia.Core.AppServices/Services/RefundService.cs#L59-L63) |
| řádek `Refund` | `Pending` → `Succeeded`; `Failed` nikdy; `StripeRefundId` vždy null; `RefundKey` idempotence | [RefundService.cs:97-112](../../src/Cleansia.Core.AppServices/Services/RefundService.cs#L97-L112), [RefundService.cs:170](../../src/Cleansia.Core.AppServices/Services/RefundService.cs#L170) |
| Stripe poplatek | odečítá se u `AdminDiscretion` (`PlatformAbsorbsStripeFee` false); `RefundStripeFeeRate` nikdo nezapisuje → 0 | [RefundPolicy.cs:49-50](../../src/Cleansia.Core.AppServices/Features/Refunds/RefundPolicy.cs#L49-L50), [CountryConfiguration.cs:89-98](../../src/Cleansia.Core.Domain/Configuration/CountryConfiguration.cs#L89-L98) |
| částečný refund | body odebrány; zákazník bez události → R18 | grep -n "NotificationEventCatalog" IssuePartialRefund.cs → 0; [IssuePartialRefund.cs:167-171](../../src/Cleansia.Core.AppServices/Features/Refunds/IssuePartialRefund.cs#L167-L171) |
| `charge.refunded` | nezpracovává se | grep -rn "charge.refunded" src/ → 0; [Constants.cs:50-55](../../src/Cleansia.Core.AppServices/Common/Constants.cs#L50-L55) |

### 6.4 Kredity

| pravidlo | citace |
|---|---|
| `CreditAccount` per zákazník a měnu; `Balance` nikdy záporný; ledger append-only s `IdempotencyKey` | [CreditAccount.cs:35-53](../../src/Cleansia.Core.Domain/Credit/CreditAccount.cs#L35-L53), [CreditTransaction.cs:45-47](../../src/Cleansia.Core.Domain/Credit/CreditTransaction.cs#L45-L47) |
| důvody `DisputeSettlement`, `CleanerNoShow`, `Goodwill`, `OrderPayment`, `OrderPaymentReturned`, `Expired` | [CreditTransactionReason.cs:14-35](../../src/Cleansia.Core.Domain/Credit/CreditTransactionReason.cs#L14-L35) |
| ruční `IssueCustomerCredit` (`SupportOrAbove`): `SanityCap` 10 000, poznámka povinná; automaticky jen `CleanerNoShow` (`TryIssueApologyCreditAsync`) | [IssueCustomerCredit.cs:67](../../src/Cleansia.Core.AppServices/Features/Credit/Admin/IssueCustomerCredit.cs#L67), [CancelUnfilledOrders.cs:276-289](../../src/Cleansia.Core.AppServices/Features/Orders/CancelUnfilledOrders.cs#L276-L289) |
| čerpání automatické, jen karta, `CapCreditForOrder` max `MaxCreditShareOfOrder` 70 % | [CreateOrder.cs:1128-1144](../../src/Cleansia.Core.AppServices/Features/Orders/CreateOrder.cs#L1128-L1144), [BookingPolicy.cs:98](../../src/Cleansia.Core.AppServices/Features/Orders/BookingPolicy.cs#L98) |
| vrácení `ReturnCreditShareAsync` → `OrderPaymentReturned` při refundu a stornu; expirace `ExpiryMonths` 12 od posledního pohybu | [RefundService.cs:172-174](../../src/Cleansia.Core.AppServices/Services/RefundService.cs#L172-L174), [CreditAccount.cs:69](../../src/Cleansia.Core.Domain/Credit/CreditAccount.cs#L69) |
| nikdy se nevyplácí → R4; kladný zůstatek blokuje výmaz (`GdprDeletionBlockedByCreditBalance`) | grep -rn "Payout" src/Cleansia.Core.AppServices/Features/Credit → 0; [GdprDeletionService.cs:160-163](../../src/Cleansia.Core.AppServices/Services/GdprDeletionService.cs#L160-L163) |
| admin `ExpireCustomerCredit` (`Drain` + `RecordExpiry`); útlum odepíše vše `Expired` | [ExpireCustomerCredit.cs:107-109](../../src/Cleansia.Core.AppServices/Features/Credit/Admin/ExpireCustomerCredit.cs#L107-L109), [CompanyWindDownService.cs:449-468](../../src/Cleansia.Core.AppServices/Services/CompanyWindDownService.cs#L449-L468) |

## 7. Reklamace a spory

### 7.1 Podání

| podmínka | hodnota | citace |
|---|---|---|
| kdo | jen přihlášený zákazník (`CanCreateDispute` = `CustomerOnly`); host ne → R5 | [PolicyBuilder.cs:130](../../src/Cleansia.Core.AppServices/Authentication/PolicyBuilder.cs#L130) |
| kdy | ne před termínem (`DisputeCleaningNotStarted`); jeden otevřený (`DisputeAlreadyExists`) | [CreateDispute.cs:145-149](../../src/Cleansia.Core.AppServices/Features/Disputes/CreateDispute.cs#L145-L149), [CreateDispute.cs:151-157](../../src/Cleansia.Core.AppServices/Features/Disputes/CreateDispute.cs#L151-L157) |
| okno | `FilingWindowHours` 24 h neblokuje; jen flag `FiledWithinWindow` → R10 | [DisputeLimits.cs:29](../../src/Cleansia.Core.Domain/Disputes/DisputeLimits.cs#L29), [DisputeMappers.cs:58-61](../../src/Cleansia.Core.AppServices/Mappers/DisputeMappers.cs#L58-L61) |
| obsah | popis `DescriptionMin/Max` 10–2000; `MaxLines` 50; řádek mimo objednávku odmítnut | [DisputeLimits.cs:5-6](../../src/Cleansia.Core.Domain/Disputes/DisputeLimits.cs#L5-L6), [DisputeLimits.cs:13](../../src/Cleansia.Core.Domain/Disputes/DisputeLimits.cs#L13) |
| důvody | `QualityIssue`, `ServiceNotProvided`, `ServiceIncomplete`, `DamagedProperty`, `UnauthorizedCharge`, `IncorrectAmount`, `Other`, `Chargeback` | [DisputeReason.cs:8-17](../../src/Cleansia.Core.Domain/Enums/DisputeReason.cs#L8-L17) |
| přílohy | zvlášť po založení; `MaxFileSizeBytes` 10 MB; `ImageMetadata.Scrub`; bez limitu počtu; SAS `GenerateSasUri` 1 h jen vlastníkovi | [UploadDisputeEvidence.cs:17](../../src/Cleansia.Core.AppServices/Features/Disputes/UploadDisputeEvidence.cs#L17), [UploadDisputeEvidence.cs:124-129](../../src/Cleansia.Core.AppServices/Features/Disputes/UploadDisputeEvidence.cs#L124-L129) |
| audit | hodiny od dokončení, okno, délka popisu; admin `admin.dispute.filed` | [CreateDispute.cs:203-213](../../src/Cleansia.Core.AppServices/Features/Disputes/CreateDispute.cs#L203-L213) |

### 7.2 Průběh a rozhodnutí

- `DisputeStatus`: `Pending`, `UnderReview`, `WaitingForResponse`, `Resolved`, `Closed`, `Escalated`; `Resolved`/`Closed` terminální — [DisputeStatus.cs:8-13](../../src/Cleansia.Core.Domain/Enums/DisputeStatus.cs#L8-L13), [Dispute.cs:115-124](../../src/Cleansia.Core.Domain/Disputes/Dispute.cs#L115-L124)
- Zprávy ≤ 2000; `IsStaffMessage` jen `UserProfile.Administrator`; push `dispute.reply` jen staff → zákazník — [AddDisputeMessage.cs:35](../../src/Cleansia.Core.AppServices/Features/Disputes/AddDisputeMessage.cs#L35), [AddDisputeMessage.cs:88-106](../../src/Cleansia.Core.AppServices/Features/Disputes/AddDisputeMessage.cs#L88-L106)
- `ResolveDispute` (`SupportOrAbove`): notes povinné ≤ 2000; terminální nelze znovu (`DisputeAlreadyResolved`) — [ResolveDispute.cs:25-39](../../src/Cleansia.Core.AppServices/Features/Disputes/ResolveDispute.cs#L25-L39), [ResolveDispute.cs:71-74](../../src/Cleansia.Core.AppServices/Features/Disputes/ResolveDispute.cs#L71-L74)
- Úklidník spor nevidí ani neodpovídá; SLA na odpověď neexistuje → R10 — grep -rn "Dispute" Web.Partner/Controllers a "ResponseDue" Features/Disputes → 0; [PolicyBuilder.cs:130-139](../../src/Cleansia.Core.AppServices/Authentication/PolicyBuilder.cs#L130-L139)
- Zamítnutí bez události; úspěšná refundace vyvolá `order.refunded`; kredit jen ručně `IssueCustomerCredit` → R18 — [ResolveDispute.cs:113-139](../../src/Cleansia.Core.AppServices/Features/Disputes/ResolveDispute.cs#L113-L139), [IssueCustomerCredit.cs:14-17](../../src/Cleansia.Core.AppServices/Features/Credit/Admin/IssueCustomerCredit.cs#L14-L17)

### 7.3 Chargeback

- Chargeback hledá `StripePaymentIntentId`; web ukládá jen `AssignStripeSessionId`, takže objednávku nemusí najít → R18 — [HandlePaymentNotification.cs:463-467](../../src/Cleansia.Core.AppServices/Features/Payments/HandlePaymentNotification.cs#L463-L467), [OrderPaymentDispatcher.cs:66](../../src/Cleansia.Core.AppServices/Features/Orders/OrderPaymentDispatcher.cs#L66)
- Stripe `won` → `Resolved` bez refundu, `lost` → `Closed`, ostatní `Escalated` — [HandlePaymentNotification.cs:604-611](../../src/Cleansia.Core.AppServices/Features/Payments/HandlePaymentNotification.cs#L604-L611)
- Administrátoři `admin.dispute.chargeback` s částkou; zákazník nic → R18 — [HandlePaymentNotification.cs:518-538](../../src/Cleansia.Core.AppServices/Features/Payments/HandlePaymentNotification.cs#L518-L538)
- Evidence do Stripe se neposílá; výplata úklidníka nedotčena — grep -rn "DisputeService|SubmitEvidence" src/*.cs → 0; [CalculateOrderPay.cs:149-157](../../src/Cleansia.Core.AppServices/Features/EmployeePayroll/CalculateOrderPay.cs#L149-L157)
- Nalezené objednávce vzniká `Dispute`, důvod `Chargeback`, stav `Escalated`; host má `UserId = null`. Podání reklamace hostem nepovoluje → R5 — [HandlePaymentNotification.cs:484-505](../../src/Cleansia.Core.AppServices/Features/Payments/HandlePaymentNotification.cs#L484-L505), [DisputeEntityConfiguration.cs:21-22](../../src/Cleansia.Infra.Database/EntityConfigurations/DisputeEntityConfiguration.cs#L21-L22)

## 8. Úklidník

### 8.1 Kdo je

- `EmployeeEntityType`: `NaturalPerson = 1`, `LegalEntity = 2`; IČO `RegistrationNumber`, firma `LegalEntityName` — [EmployeeEntityType.cs:8-9](../../src/Cleansia.Core.Domain/Enums/EmployeeEntityType.cs#L8-L9), [Employee.cs:16-19](../../src/Cleansia.Core.Domain/Users/Employee.cs#L16-L19)
- Výplatní účet `EmployeePayoutDetails`; schémata `CzskDomesticWithIban`, `SepaIban`, `ProviderPayoutToken` — [PayoutScheme.cs:19-28](../../src/Cleansia.Core.Domain/Enums/PayoutScheme.cs#L19-L28)
- `ContractStatus`: `Pending = 1`, `Active = 2`, `Terminated = 3`, `Approved = 4`, `Rejected = 5`; produkční změny provádějí `Approve`/`Reject` — [ContractStatus.cs:8-12](../../src/Cleansia.Core.Domain/Enums/ContractStatus.cs#L8-L12), [Employee.cs:324-350](../../src/Cleansia.Core.Domain/Users/Employee.cs#L324-L350)
- `Employee` nese `AverageRating` a `ComplaintsCount` — [Employee.cs:57-59](../../src/Cleansia.Core.Domain/Users/Employee.cs#L57-L59)
- Seniorita, dostupnost, pojištění na `Employee` nejsou — grep -rn "Grade\|Seniority\|Availability\|Insurance" Employee.cs → 0; [Employee.cs:11](../../src/Cleansia.Core.Domain/Users/Employee.cs#L11)

### 8.2 Nábor a doklady

| krok | pravidlo | citace |
|---|---|---|
| registrace a profil | `RegisterEmployee` (`CountryId` trhu); `IsProfileComplete` = jméno, telefon, `BirthDate`, adresa, `RegistrationNumber`, účet, `PassportId`, `NationalityId` | [RegisterEmployee.cs:59-71](../../src/Cleansia.Core.AppServices/Features/Auth/RegisterEmployee.cs#L59-L71), [Employee.cs:382-408](../../src/Cleansia.Core.Domain/Users/Employee.cs#L382-L408) |
| dokumenty | `DocumentType` `IdentityCard` … `Other`; stavy `Pending = 1`, `Approved = 2`, `Rejected = 3`; bez data expirace | [DocumentType.cs:8-17](../../src/Cleansia.Core.Domain/Enums/DocumentType.cs#L8-L17), [DocumentStatus.cs:8-10](../../src/Cleansia.Core.Domain/Enums/DocumentStatus.cs#L8-L10) |
| požadavky per země | `EmployeeDocumentRequirement`; seed CZE/SVK `IdentityCard` povinný; přes profil typ `Other` | [EmployeeDocumentRequirement.cs:24-46](../../src/Cleansia.Core.Domain/Documents/EmployeeDocumentRequirement.cs#L24-L46), [insert_seed_data.sql:1134-1139](../../sql-scripts/insert_seed_data.sql#L1134-L1139) |
| posouzení | `CanApproveEmployeeDocument` (`SupportOrAbove`); smazání jen na žádost úklidníka (`ResolveDocumentDeletionRequest`) | [AdminEmployeeDocumentController.cs:177-181](../../src/Cleansia.Web.Admin/Controllers/AdminEmployeeDocumentController.cs#L177-L181) |
| `ApproveEmployee` | úplný profil; povinné doklady `Approved`; pracovní země (`WorkCountryId`) adminovy společnosti; sazby v měně | [ApproveEmployee.cs:47-60](../../src/Cleansia.Core.AppServices/Features/Employees/ApproveEmployee.cs#L47-L60), [ApproveEmployee.cs:123-136](../../src/Cleansia.Core.AppServices/Features/Employees/ApproveEmployee.cs#L123-L136) |
| `RejectEmployee` | `RejectionReason`; uvolní budoucí `Confirmed` sedadla; `NotifyCleanerOfRevocationAsync` oznámí odebrané zakázky, nikoli zamítnutí účtu → R18 | [RejectEmployee.cs:113-116](../../src/Cleansia.Core.AppServices/Features/Employees/RejectEmployee.cs#L113-L116), [RejectEmployee.cs:165-180](../../src/Cleansia.Core.AppServices/Features/Employees/RejectEmployee.cs#L165-L180) |
| brána | neschválený profil 403 (`RequireCompleteProfileAttribute`); souhlasy se nekontrolují; deaktivace jako příkaz neexistuje | grep -rn "class DeactivateEmployee\|class TerminateEmployee" src/ → 0; [RequireCompleteProfileAttribute.cs:32-47](../../src/Cleansia.Config/Filters/RequireCompleteProfileAttribute.cs#L32-L47) |

### 8.3 Nástěnka a převzetí

| brána | podmínka | citace |
|---|---|---|
| nabízitelnost | stav `New/Confirmed/OnTheWay/InProgress` a (`Paid`, nebo hotovost mimo opakování) | [OrderAvailability.cs:61-67](../../src/Cleansia.Core.Domain/Orders/OrderAvailability.cs#L61-L67) |
| filtr nástěnky | měna úklidníka (`cleanerCurrencyId`), ne město; bez `ContractStatus` | [OrderVisibility.cs:53-55](../../src/Cleansia.Core.Domain/Orders/OrderVisibility.cs#L53-L55), [GetPagedOrders.cs:97-100](../../src/Cleansia.Core.AppServices/Features/Orders/GetPagedOrders.cs#L97-L100) |
| po začátku | `BoardBacklogHours` 2 h; bez horního limitu předstihu | [BookingPolicy.cs:130](../../src/Cleansia.Core.AppServices/Features/Orders/BookingPolicy.cs#L130) |
| `TakeOrder` | `AcceptedWorkContractTextId`, nabízitelnost, volné sedadlo, `EmployeeIsApprovedAsync`, adresa, bez kolize, `WeeklyOrderLimit` → R20 | [TakeOrder.cs:60-75](../../src/Cleansia.Core.AppServices/Features/Orders/TakeOrder.cs#L60-L75), [TakeOrder.cs:76-89](../../src/Cleansia.Core.AppServices/Features/Orders/TakeOrder.cs#L76-L89) |
| digest a zástup | `order.new_available` jen `Approved`/`Active`, `JobRadiusKm` 1–500 km; sedadlo se žádostí o zástup lze vzít vytlačením | [NewJobsDigestService.cs:79-94](../../src/Cleansia.Core.AppServices/Services/NewJobsDigestService.cs#L79-L94), [TakeOrder.cs:337-349](../../src/Cleansia.Core.AppServices/Features/Orders/TakeOrder.cs#L337-L349) |

### 8.4 Provedení

| brána | podmínka | citace |
|---|---|---|
| `NotifyOnTheWay` | z `Confirmed`, přiřazený, nejdříve `StartGraceWindowMinutes` 60 min před startem | [NotifyOnTheWay.cs:48-56](../../src/Cleansia.Core.AppServices/Features/Orders/NotifyOnTheWay.cs#L48-L56), [BookingPolicy.cs:62](../../src/Cleansia.Core.AppServices/Features/Orders/BookingPolicy.cs#L62) |
| `StartOrder` | `Confirmed`/`OnTheWay`, schválený, smlouva pro sedadlo, bez jiné `InProgress`; bez fotky a polohy → R20 | grep -n "Photo\|Latitude" StartOrder.cs → 0; [StartOrder.cs:49-63](../../src/Cleansia.Core.AppServices/Features/Orders/StartOrder.cs#L49-L63) |
| `CompleteOrder` | ≥ 1 fotka `After` (`HasAfterPhotosAsync`), `Paid`, smlouva → R7, R20 | [CompleteOrder.cs:183-189](../../src/Cleansia.Core.AppServices/Features/Orders/CompleteOrder.cs#L183-L189) |
| délka a fotky | `actualMinutes` = teď − start, min. 1 min; `Before = 1`, `After = 2`, smazat smí jen `AssignedEmployees`, i po dokončení → R7 | [CompleteOrder.cs:268](../../src/Cleansia.Core.AppServices/Features/Orders/CompleteOrder.cs#L268), [DeleteOrderPhoto.cs:56-71](../../src/Cleansia.Core.AppServices/Features/Orders/DeleteOrderPhoto.cs#L56-L71) |
| přístup k údajům | dokud drží sedadlo (`AssignedEmployees`), bez časového limitu; prohlížející `RedactForBrowsingCleaner` → R8 | [OrderAccessService.cs:88-94](../../src/Cleansia.Core.AppServices/Authentication/OrderAccessService.cs#L88-L94), [OrderPiiRedaction.cs:40-67](../../src/Cleansia.Core.AppServices/Features/Orders/OrderPiiRedaction.cs#L40-L67) |
| po dokončení | účtenka, push, e-mail, body, doporučení, `CalculateOrderPay` per úklidník | [CompleteOrder.cs:289-355](../../src/Cleansia.Core.AppServices/Features/Orders/CompleteOrder.cs#L289-L355) |

### 8.5 Odstoupení a zástup

- `DropOrder`: uvolní sedadlo (`HasTakeableSeat`), objednávka se neruší, peníze se nehýbou; jen `New/Confirmed/OnTheWay/InProgress` — [DropOrder.cs:14-28](../../src/Cleansia.Core.AppServices/Features/Orders/DropOrder.cs#L14-L28), [DropOrder.cs:106-110](../../src/Cleansia.Core.AppServices/Features/Orders/DropOrder.cs#L106-L110)
- Prázdná posádka → `ReturnToBoardIfUnstaffed`, alarm adminům; „The customer is told nothing here" → R18 — [DropOrder.cs:130-148](../../src/Cleansia.Core.AppServices/Features/Orders/DropOrder.cs#L130-L148), [DropOrder.cs:38](../../src/Cleansia.Core.AppServices/Features/Orders/DropOrder.cs#L38)
- `RequestCover`: úklidník je dál přiřazen, sedadlo převzatelné vytlačením (`HasTakeableSeat`); bez důvodu — [RequestCover.cs:17-22](../../src/Cleansia.Core.AppServices/Features/Orders/RequestCover.cs#L17-L22)
- Endpointy `RequestCover`/`DropOrder` existují na obou partnerských hostech, žádný klient je nevolá — grep -rn "dropOrder\|requestCover" partner-app/src/main → 0, src/cleansia_ios → 0, Angular mimo `client/` → 0; [OrderController.cs:285-305](../../src/Cleansia.Web.Mobile.Partner/Controllers/OrderController.cs#L285-L305)
- Sankce za drop neexistuje; no-show přiřazeného se nedetekuje, jen prázdná posádka; `DropOrder` nic nerefunduje → R10 — grep -rn "Penalty\|Sanction\|CleanerFee" Cleansia.Core.Domain → 0; [CancelUnfilledOrders.cs:34-38](../../src/Cleansia.Core.AppServices/Features/Orders/CancelUnfilledOrders.cs#L34-L38)
- Uvolněné sedadlo: push `order.seat_open` až `MaxRecipients` 50 úklidníkům v zemi a rádiusu — [SeatOpenedNotifier.cs:37](../../src/Cleansia.Core.AppServices/Features/Orders/SeatOpenedNotifier.cs#L37), [SeatOpenedNotifier.cs:54-65](../../src/Cleansia.Core.AppServices/Features/Orders/SeatOpenedNotifier.cs#L54-L65)

### 8.6 Odměna

- Odměna: `BasePay + max(0, pokoje−1)·ExtraPerRoom + koupelny·ExtraPerBathroom + km·DistanceRatePerKm`, ořezaná `ApplyMinMaxClamp` — [PayCalculatorExtensions.cs:12-18](../../src/Cleansia.Core.Domain/Extensions/PayCalculatorExtensions.cs#L12-L18)
- `CalculateOrderPay` načte `SelectedServices` i `SelectedPackages` pro `CalculateAggregatedPay` — [CalculateOrderPay.cs:90](../../src/Cleansia.Core.AppServices/Features/EmployeePayroll/CalculateOrderPay.cs#L90), [CalculateOrderPay.cs:118-157](../../src/Cleansia.Core.AppServices/Features/EmployeePayroll/CalculateOrderPay.cs#L118-L157)
- `CalculateAggregatedPay` zahrnuje balíčky, nikoli doplňky, express nebo spropitné. Hledání `TravelDistance =` v produkčním C# nenachází zapisovač → R6 — [OrderPayEstimator.cs:88-107](../../src/Cleansia.Core.AppServices/Features/Orders/OrderPayEstimator.cs#L88-L107), [Order.cs:108](../../src/Cleansia.Core.Domain/Orders/Order.cs#L108)
- Sazba `EmployeePayConfig` jen v měně objednávky, per-úklidník override před celoplatformní; šablony 0,5 / 0,75 / 1,0 × cena — [CalculateOrderPay.cs:146-155](../../src/Cleansia.Core.AppServices/Features/EmployeePayroll/CalculateOrderPay.cs#L146-L155), [BulkCreateEmployeePayConfigs.cs:25-27](../../src/Cleansia.Core.AppServices/Features/PayConfig/BulkCreateEmployeePayConfigs.cs#L25-L27)
- `OrderEmployeePay.UpdatePay` odmítá změnu po schválení (`IsApproved`); hotovost placena stejně; jeden `CalculateOrderPay` na přiřazeného — [OrderEmployeePay.cs:130-148](../../src/Cleansia.Core.Domain/EmployeePayroll/OrderEmployeePay.cs#L130-L148), [CompleteOrder.cs:346-355](../../src/Cleansia.Core.AppServices/Features/Orders/CompleteOrder.cs#L346-L355)
- Přijetí smlouvy odměnu nezmrazuje (`WorkContractFacts` bez `Pay`); admin override na `Completed` odměnu nezaloží — grep -n "Pay" WorkContractFacts.cs → 0; [AdminOverrideOrderStatus.cs:141-147](../../src/Cleansia.Core.AppServices/Features/Orders/AdminOverrideOrderStatus.cs#L141-L147)

### 8.7 Výplatní období a faktura

| prvek | pravidlo | citace |
|---|---|---|
| období | 7–31 dní; automatika otevírá měsíční (`AddMonths`); stavy `Open = 1`, `Closed = 2`, `Paid = 3` | [PayPeriod.cs:40-44](../../src/Cleansia.Core.Domain/EmployeePayroll/PayPeriod.cs#L40-L44), [PayPeriodBackgroundService.cs:99-100](../../src/Cleansia.Core.AppServices/Services/PayPeriodBackgroundService.cs#L99-L100) |
| uzávěrka | `CloseExpiredPayPeriods` denně `0 0 2 * * *`; zavře období a založí faktury; e-mail §10 | [PayPeriodTimerFunction.cs:10](../../src/Cleansia.Functions/Functions/PayPeriodTimerFunction.cs#L10) |
| faktura | jedna na měnu; stavy `Pending`, `Approved`, `Paid`, `Disputed`, `Rejected`, `Cancelled`; číslo `INV-YYYY-NNNNNN`, VS `YYYYNNNNNN` | [EmployeeInvoiceStatus.cs:8-13](../../src/Cleansia.Core.Domain/Enums/EmployeeInvoiceStatus.cs#L8-L13), [PayoutReferenceAllocator.cs:50-52](../../src/Cleansia.Core.AppServices/Services/PayoutReferenceAllocator.cs#L50-L52) |
| částka | `SubTotal + BonusAmount − DeductionAmount = TotalAmount`; splatnost `PaymentTermsDays` 14 dní | [EmployeeInvoice.cs:200-205](../../src/Cleansia.Core.Domain/EmployeePayroll/EmployeeInvoice.cs#L200-L205), [Constants.cs:78](../../src/Cleansia.Core.AppServices/Common/Constants.cs#L78) |
| self-billing | `Supplier` „Dodavatel" = úklidník, `Customer` „Odběratel" = společnost; `NotVatRegistered` „Nejsme plátci DPH" → R1, R6 | [InvoiceLabels.cs:77-78](../../src/Cleansia.Infra.Services/Pdf/Models/InvoiceLabels.cs#L77-L78), [InvoiceLabels.cs:87](../../src/Cleansia.Infra.Services/Pdf/Models/InvoiceLabels.cs#L87) |
| schválení a platba | výplatní účet `Provided`, shodná měna; „zaplaceno" jen z `Approved` s VS (`HasVariableSymbolAsync`), převod mimo platformu | [ApproveInvoice.cs:68-88](../../src/Cleansia.Core.AppServices/Features/EmployeePayroll/ApproveInvoice.cs#L68-L88), [MarkInvoicePaid.cs:56-57](../../src/Cleansia.Core.AppServices/Features/EmployeePayroll/MarkInvoicePaid.cs#L56-L57) |
| výplatní integrace; úprava faktury | neexistuje; `UpdateInvoiceAmounts` neinformuje a PDF nepřegeneruje → R18 | grep -rn "TransferService\|PayoutService" src/ → 0; [UpdateInvoiceAmounts.cs:75](../../src/Cleansia.Core.AppServices/Features/EmployeePayroll/UpdateInvoiceAmounts.cs#L75) |
| role | období i faktury `AccountantOrAbove` | [PolicyBuilder.cs:115-120](../../src/Cleansia.Core.AppServices/Authentication/PolicyBuilder.cs#L115-L120), [PolicyBuilder.cs:99-106](../../src/Cleansia.Core.AppServices/Authentication/PolicyBuilder.cs#L99-L106) |

### 8.8 Hotovost

- Nevratné; bez započtení proti odměně; odvod společnosti se nesleduje → R6 — grep -rn "CashCollected\|SettledInCash" Features/EmployeePayroll → 0; [Order.cs:50-52](../../src/Cleansia.Core.Domain/Orders/Order.cs#L50-L52)
- Hotovostní dokončení vyžaduje `PaymentStatus.Paid`; potvrzení opakované objednávky jej nastavuje před výběrem (§4.5); tržby zahrnují jen `PaidAtSomePoint` — [CompleteOrder.cs:201](../../src/Cleansia.Core.AppServices/Features/Orders/CompleteOrder.cs#L201), [OrderRepository.cs:271-277](../../src/Cleansia.Infra.Database/Repositories/OrderRepository.cs#L271-L277)

## 9. Plus, věrnost, doporučení

### 9.1 Cleansia Plus

| pravidlo | citace |
|---|---|
| plány seed `PLUS_MONTHLY`, `PLUS_YEARLY`; cena jen CZK, jinde `MembershipPlanNotPricedInCurrency`; `DiscountPercentage` 5 %, `FreeCancellationWindowHours` 4 h | [insert_seed_data.sql:1806](../../sql-scripts/insert_seed_data.sql#L1806), [CreateMembershipCheckoutSession.cs:95-99](../../src/Cleansia.Core.AppServices/Features/Memberships/CreateMembershipCheckoutSession.cs#L95-L99) |
| `TrialPeriodDays` 0; jiná hodnota odmítnuta (`MembershipPlanTrialNotPermitted`); `faq_cancel_a` neslibuje trial | [CreateMembershipPlan.cs:86-88](../../src/Cleansia.Core.AppServices/Features/Memberships/Admin/CreateMembershipPlan.cs#L86-L88), [cs.json:1654](../../src/Cleansia.App/apps/cleansia.app/src/assets/i18n/cs.json#L1654) |
| entitled = `Active`, neprošlé období a žádné budoucí `TrialEndsAtUtc`; události vybírá `IsSubscriptionEvent` | [UserMembershipRepository.cs:39-58](../../src/Cleansia.Infra.Database/Repositories/UserMembershipRepository.cs#L39-L58), [Constants.cs:57-61](../../src/Cleansia.Core.AppServices/Common/Constants.cs#L57-L61) |
| selhání platby → `PastDue` = okamžitý konec výhod, bez informování; zrušit nelze (`MembershipNotFound`) → R18 | [MembershipStatus.cs:18-25](../../src/Cleansia.Core.Domain/Memberships/MembershipStatus.cs#L18-L25), [CancelMembershipSubscription.cs:40-45](../../src/Cleansia.Core.AppServices/Features/Memberships/CancelMembershipSubscription.cs#L40-L45) |
| zrušení `CancelAtPeriodEnd = true`, odvolání ani pauza neexistují; swap maže lokální `CancelledAt`, Stripe `cancel_at_period_end` neruší | grep -rn "CancelAtPeriodEnd = false\|ResumeMembership" src/ → 0; [UserMembership.cs:302-304](../../src/Cleansia.Core.Domain/Memberships/UserMembership.cs#L302-L304) |
| vznik bez události; `membership.expiring_soon` 2–4 dny před (`RenewalLeadDaysLow/High`); žádný e-mail (`EmailType`) | [SendMembershipLifecycleNotifications.cs:26-27](../../src/Cleansia.Core.AppServices/Features/Memberships/SendMembershipLifecycleNotifications.cs#L26-L27), [EmailType.cs:6-29](../../src/Cleansia.Core.Domain/Enums/EmailType.cs#L6-L29) |

### 9.2 Věrnost

| stupeň | práh (lifetime body) | sleva | minimum | citace |
|---|---|---|---|---|
| `BronzeCleaner` 1 (`LoyaltyTierConfigs`) | 0 | 0 % | — | [LoyaltyTier.cs:6-12](../../src/Cleansia.Core.Domain/Loyalty/LoyaltyTier.cs#L6-L12), [insert_seed_data.sql:1674](../../sql-scripts/insert_seed_data.sql#L1674) |
| `SilverMopper` 2 | 500 | 5 % | 1000 Kč | [insert_seed_data.sql:1685](../../sql-scripts/insert_seed_data.sql#L1685), [insert_seed_data.sql:1718](../../sql-scripts/insert_seed_data.sql#L1718) |
| `GoldPolisher` 3 | 2000 | 10 % | 1000 Kč | [insert_seed_data.sql:1696](../../sql-scripts/insert_seed_data.sql#L1696), [insert_seed_data.sql:1716-1731](../../sql-scripts/insert_seed_data.sql#L1716-L1731) |
| `PlatinumSparkler` 4 | 5000 | 12 % | 1000 Kč | [insert_seed_data.sql:1707](../../sql-scripts/insert_seed_data.sql#L1707), [insert_seed_data.sql:1716-1731](../../sql-scripts/insert_seed_data.sql#L1716-L1731) |

- Body = floor(`TotalPrice` / `LoyaltyPointsDivisor`) při `CompleteOrder`, jen registrovaným — [LoyaltyService.cs:417-430](../../src/Cleansia.Core.AppServices/Services/LoyaltyService.cs#L417-L430), [LoyaltyService.cs:40-45](../../src/Cleansia.Core.AppServices/Services/LoyaltyService.cs#L40-L45)
- Storno odebírá celý zisk, částečný refund poměrně; `RecomputeTier` dovoluje pokles a webový `spent_note` jej popisuje. Volba trvale dosaženého stupně je otevřená → R17 — [LoyaltyAccount.cs:114-120](../../src/Cleansia.Core.Domain/Loyalty/LoyaltyAccount.cs#L114-L120), [cs.json:1344](../../src/Cleansia.App/apps/cleansia.app/src/assets/i18n/cs.json#L1344)
- Body neexpirují, admin je přidá i odebere; floor slevy jen ve výchozí měně — grep -rin "PointsExpir" src/ → 0; [LoyaltyService.cs:236-244](../../src/Cleansia.Core.AppServices/Services/LoyaltyService.cs#L236-L244)
- Ledger patří firmě účtu, nikoli firmě objednávky → R19 — [LoyaltyAccount.cs:75-78](../../src/Cleansia.Core.Domain/Loyalty/LoyaltyAccount.cs#L75-L78)

### 9.3 Doporučení a promokódy

- `PointsPerSide` 150 oběma stranám po první dokončené objednávce pozvaného do `QualifyingWindowDays` 90 — [ReferralPolicy.cs:14](../../src/Cleansia.Core.AppServices/Features/Orders/ReferralPolicy.cs#L14), [ReferralPolicy.cs:22](../../src/Cleansia.Core.AppServices/Features/Orders/ReferralPolicy.cs#L22)
- Kód 6 znaků; `SelfReferral`, `AlreadyReferred` odmítnuty; stavy `Accepted 1, Qualified 2, Expired 3, Reversed 4`; admin `ReverseReferral` body odebere → R18 — [ReferralStatus.cs:6-23](../../src/Cleansia.Core.Domain/Loyalty/ReferralStatus.cs#L6-L23), [ReverseReferral.cs:65-101](../../src/Cleansia.Core.AppServices/Features/Referrals/Admin/ReverseReferral.cs#L65-L101)
- Promokód `PercentDiscount` / `FixedDiscount`; `MaxRedemptionsPerUser` výchozí 1 a `GlobalMaxRedemptions`; `Code`, `DiscountPercent`, `DiscountAmount` po vytvoření neměnné — [PromoCode.cs:40-52](../../src/Cleansia.Core.Domain/Loyalty/PromoCode.cs#L40-L52), [PromoCode.cs:169-175](../../src/Cleansia.Core.Domain/Loyalty/PromoCode.cs#L169-L175)
- Veřejný `VITEJTE-XXXXXX`: `FirstOrderDiscountPercent` 10 %, `ValidForDays` 30, 1×; první objednávku nekontroluje — [RequestPromoCode.cs:33-36](../../src/Cleansia.Core.AppServices/Features/PromoCodes/RequestPromoCode.cs#L33-L36), [RequestPromoCode.cs:84-91](../../src/Cleansia.Core.AppServices/Features/PromoCodes/RequestPromoCode.cs#L84-L91)
- Kampaň `SendSitewidePromo`: titulek ≤ 120, tělo ≤ 500 v 5 jazycích; jen push; příjemci `Promo = true` bez ohledu na `MarketingEmails` a profil → R9 — [SendSitewidePromo.cs:40-53](../../src/Cleansia.Core.AppServices/Features/Marketing/SendSitewidePromo.cs#L40-L53), [SendSitewidePromoFanoutHandler.cs:103-111](../../src/Cleansia.Functions.Core/Handlers/SendSitewidePromoFanoutHandler.cs#L103-L111)

## 10. Komunikace

| událost | zákazník dostane? (kanál) | úklidník dostane? | administrátor dostane? | citace |
|---|---|---|---|---|
| objednávka vytvořena | bez feed/push; hotovostní účtenka e-mailem | preferovaná nabídka, je-li nabízitelná | `admin.order.new`, jen nabízitelná | [OrderPaymentDispatcher.cs:82-92](../../src/Cleansia.Core.AppServices/Features/Orders/OrderPaymentDispatcher.cs#L82-L92), [NewOrderAdminNotifier.cs:10-16](../../src/Cleansia.Core.AppServices/Features/Orders/NewOrderAdminNotifier.cs#L10-L16) |
| platba potvrzena | `order.payment_confirmed` (feed + push), účtenka e-mailem; host jen e-mail | preferovaná nabídka beneficientovi | `admin.order.new`, je-li nabízitelná | [HandlePaymentNotification.cs:315-345](../../src/Cleansia.Core.AppServices/Features/Payments/HandlePaymentNotification.cs#L315-L345) |
| platba zamítnuta | ne — žádná událost | ne | `admin.payment.failed` (Support+), jednou | [HandlePaymentNotification.cs:245-279](../../src/Cleansia.Core.AppServices/Features/Payments/HandlePaymentNotification.cs#L245-L279) |
| úklidník převzal | `order.cleaner_assigned` (feed + push, bez jména) + e-mail „Confirmed" | ne | ne | [TakeOrder.cs:421-437](../../src/Cleansia.Core.AppServices/Features/Orders/TakeOrder.cs#L421-L437) |
| na cestě / zahájeno / dokončeno | `order.on_the_way`; `order.in_progress` + e-mail „Started"; `order.completed` + e-mail + účtenka | ne | ne | [CompleteOrder.cs:289-326](../../src/Cleansia.Core.AppServices/Features/Orders/CompleteOrder.cs#L289-L326) |
| připomínka před úklidem | `order.starting_soon` (push) 55–70 min před | ne | ne | [SendPreCleaningReminders.cs:41](../../src/Cleansia.Core.AppServices/Features/Orders/SendPreCleaningReminders.cs#L41) |
| úklidník odešel → R18 | ne — žádná událost | ostatní `order.seat_open` | `admin.order.crew_lost` při prázdné posádce | grep -n "order.UserId" DropOrder.cs → 0 NotifyAsync; [DropOrder.cs:153-172](../../src/Cleansia.Core.AppServices/Features/Orders/DropOrder.cs#L153-L172) |
| zástup vyžádán / přijat | `order.cleaner_assigned` až při přijetí | `order.seat_open`; původní `order.assignment_revoked` | ne | [TakeOrder.cs:410-422](../../src/Cleansia.Core.AppServices/Features/Orders/TakeOrder.cs#L410-L422) |
| admin přiřadil / přeřadil | `order.cleaner_assigned` | nový `order.assigned`; odebraný `order.assignment_revoked` | ne | [AdminReassignOrder.cs:141-153](../../src/Cleansia.Core.AppServices/Features/Orders/AdminReassignOrder.cs#L141-L153) |
| zákazník nebo host zrušil | `order.refunded` při refundaci; host e-mail „Cancelled" s částkou | `order.assignment_cancelled` | ne | [CustomerOrderCancellation.cs:100-107](../../src/Cleansia.Core.AppServices/Features/Orders/CustomerOrderCancellation.cs#L100-L107), [CancelGuestOrder.cs:62-66](../../src/Cleansia.Core.AppServices/Features/Orders/CancelGuestOrder.cs#L62-L66) |
| administrátor zrušil → R18 | jen `order.refunded` u karty; hotovost nic; důvod se nepředává | `order.assignment_cancelled` | ne | [AdminCancelOrder.cs:92-98](../../src/Cleansia.Core.AppServices/Features/Orders/AdminCancelOrder.cs#L92-L98), [PlatformOrderCancellation.cs:65-68](../../src/Cleansia.Core.AppServices/Services/PlatformOrderCancellation.cs#L65-L68) |
| neobsazená zakázka; opuštěná pokladna | `order.no_cleaner_refunded` s kreditem, jinak `order.cancelled` | ne | ne — žádná událost | grep -n "adminNotifier" CancelUnfilledOrders.cs → 0; [CancelUnfilledOrders.cs:205-238](../../src/Cleansia.Core.AppServices/Features/Orders/CancelUnfilledOrders.cs#L205-L238) |
| částečná refundace; kredit připsán / propadl → R18 | ne — žádná událost | ne | ne | grep -n "notificationProducer" IssuePartialRefund.cs a Features/Credit → 0; [ExpireStaleCredit.cs:20-21](../../src/Cleansia.Core.AppServices/Features/Credit/ExpireStaleCredit.cs#L20-L21) |
| plný refund adminem | `order.refunded` (jen `OrderNumberArg`, bez částky) | ne | ne | [AdminRefundOrder.cs:121-142](../../src/Cleansia.Core.AppServices/Features/Orders/AdminRefundOrder.cs#L121-L142) |
| reklamace podána / odpověď | `dispute.reply` jen od podpory | ne | `admin.dispute.filed` (Support+) | [CreateDispute.cs:186-201](../../src/Cleansia.Core.AppServices/Features/Disputes/CreateDispute.cs#L186-L201) |
| reklamace rozhodnuta → R18 | `order.refunded` jen při refundaci; zamítnutí nic | ne | ne | [ResolveDispute.cs:83-139](../../src/Cleansia.Core.AppServices/Features/Disputes/ResolveDispute.cs#L83-L139) |
| chargeback → R18 | ne — žádná událost | ne | `admin.dispute.chargeback` (všichni) | grep -in "chargeback" NotificationEventCatalog.cs → 0; [HandlePaymentNotification.cs:518-538](../../src/Cleansia.Core.AppServices/Features/Payments/HandlePaymentNotification.cs#L518-L538) |
| doklad posouzen, účet schválen → R18 | — | ne — žádná událost | ne | grep -rn "Notif" Features/EmployeeDocuments a ApproveEmployee.cs → 0; [ApproveEmployee.cs:205-206](../../src/Cleansia.Core.AppServices/Features/Employees/ApproveEmployee.cs#L205-L206) |
| účet úklidníka zamítnut | — | `order.assignment_revoked` za každou odebranou zakázku | `admin.order.crew_lost` | [RejectEmployee.cs:165-180](../../src/Cleansia.Core.AppServices/Features/Employees/RejectEmployee.cs#L165-L180) |
| týdenní limit nastaven | — | `EmployeeWeeklyLimitSet` (`employee.weekly_limit_set`, push, nevypnutelný) | — | [AdminSetEmployeeWeeklyOrderLimit.cs:130-132](../../src/Cleansia.Core.AppServices/Features/Employees/AdminSetEmployeeWeeklyOrderLimit.cs#L130-L132) |
| nabídka práce | — | `NewJobsAvailable` (`order.new_available`) digest; `order.preferred_offer` beneficientovi | — | [NewJobsDigestService.cs:219-221](../../src/Cleansia.Core.AppServices/Services/NewJobsDigestService.cs#L219-L221), [PreferredOfferNotifier.cs:35](../../src/Cleansia.Core.AppServices/Features/Orders/PreferredOfferNotifier.cs#L35) |
| připomínky úklidníkovi | — | `order.reminder_tomorrow` v `LocalSendHour` 18; `order.reminder_soon` (`SoonLeadMinutesLow`); `order.reminder_not_started` | — | [SendCleanerJobReminders.cs:47-51](../../src/Cleansia.Core.AppServices/Features/Orders/SendCleanerJobReminders.cs#L47-L51) |
| preferovaná nabídka propadla | `order.preferred_offer_closed` (push) | ne | ne | [NotificationEventCatalog.cs:44-55](../../src/Cleansia.Core.Domain/Notifications/NotificationEventCatalog.cs#L44-L55) |
| výplatní období | — | e-mail 3 a 1 den před koncem; „Period Closed" s PDF | — | [PayPeriodBackgroundService.cs:287-307](../../src/Cleansia.Core.AppServices/Services/PayPeriodBackgroundService.cs#L287-L307) |
| faktura zaplacena / upravena → R18 | — | `payroll.invoice_paid`; úprava bez události | — | [MarkInvoicePaid.cs:119-132](../../src/Cleansia.Core.AppServices/Features/EmployeePayroll/MarkInvoicePaid.cs#L119-L132); grep -n "Notif" UpdateInvoiceAmounts.cs → 0 |
| Plus: vznik / selhání / konec → R18 | vznik nic; selhání nic; `membership.cancellation_effective` jen po žádosti | — | ne | [StripeSubscriptionWebhookHandler.cs:19-25](../../src/Cleansia.Core.AppServices/Services/StripeSubscriptionWebhookHandler.cs#L19-L25), [SendMembershipLifecycleNotifications.cs:96-102](../../src/Cleansia.Core.AppServices/Features/Memberships/SendMembershipLifecycleNotifications.cs#L96-L102) |
| opakovaný úklid | `recurring.scheduled`; `order.cancelled`; `recurring.paused` | — | — | [AutoCancelStaleRecurringOrders.cs:125](../../src/Cleansia.Core.AppServices/Features/Bookings/AutoCancelStaleRecurringOrders.cs#L125) |
| věrnost / doporučení | `loyalty.tier_upgrade`; pokles tichý; doporučení nic | — | — | grep -rn "Referral" NotificationEventCatalog.cs → 0; [LoyaltyService.cs:69-86](../../src/Cleansia.Core.AppServices/Services/LoyaltyService.cs#L69-L86) |
| výmaz / export | ne — žádná událost | — | `admin.erasure.failed` (`ManagerOrAbove`) | grep -rn "notificationProducer" src/Cleansia.Core.AppServices/Features/Gdpr → 0; [RetryFailedUserDeletions.cs:138-148](../../src/Cleansia.Core.AppServices/Features/Gdpr/RetryFailedUserDeletions.cs#L138-L148) |
| plošná kampaň | `promo.new_sitewide` (push, opt-in `Promo`) | totéž | — | [SendSitewidePromoFanoutHandler.cs:158-160](../../src/Cleansia.Functions.Core/Handlers/SendSitewidePromoFanoutHandler.cs#L158-L160) |
| útlum a archiv společnosti | e-mail `CompanyWindDownCustomer` | e-mail `CompanyWindDownCleaner` (`Approved`) | `admin.company.wind_down_requested`, `…run`, `…archived` (jen Administrator) | [CompanyWindDownService.cs:182-190](../../src/Cleansia.Core.AppServices/Services/CompanyWindDownService.cs#L182-L190), [AdminNotificationEventCatalog.cs:11-19](../../src/Cleansia.Core.Domain/Notifications/AdminNotificationEventCatalog.cs#L11-L19) |
| kanály, preference, e-mail | 12 kategorií, zákazník 11 přepínačů včetně `OrderUpdates`; 8 klíčů úklidníka nevypnutelných; partner bez UI; stavové e-maily anglicky; `DeadLetters` bez čtenáře | | | [UpdateNotificationPreferences.cs:21-32](../../src/Cleansia.Core.AppServices/Features/Notifications/UpdateNotificationPreferences.cs#L21-L32), [NotificationEventCatalog.cs:226-250](../../src/Cleansia.Core.Domain/Notifications/NotificationEventCatalog.cs#L226-L250) |

## 11. Údaje, retence, výmaz, export

### 11.1 Export

- `POST api/v1/Gdpr/export` (`CanExportOwnData` = `Authenticated`); JSON, synchronní odpověď; admin export `SupportOrAbove` — [GdprController.cs:16-18](../../src/Cleansia.Web.Customer/Controllers/GdprController.cs#L16-L18), [PolicyBuilder.cs:236](../../src/Cleansia.Core.AppServices/Authentication/PolicyBuilder.cs#L236)
- Sekce: Profile, Address, Employee, PayoutDetails, Orders, Disputes, Documents, Invoices, Consents, CustomerActions, Metadata, WorkContractAcceptances; zahrnuje objednávky hosta pod e-mailem účtu — [GdprExportDto.cs:5-18](../../src/Cleansia.Core.AppServices/Features/Gdpr/DTOs/GdprExportDto.cs#L5-L18), [GdprExportDto.cs:77-83](../../src/Cleansia.Core.AppServices/Features/Gdpr/DTOs/GdprExportDto.cs#L77-L83)
- Incidentní spis PDF, ne na administrátora (`CannotTargetAdminViaGdprTool`) — [ExportCustomerIncidentFile.cs:46-49](../../src/Cleansia.Core.AppServices/Features/Gdpr/ExportCustomerIncidentFile.cs#L46-L49)

### 11.2 Výmaz

| podmínka | efekt | citace |
|---|---|---|
| `POST api/v1/Gdpr/delete-account`; admin `ManagerOrAbove`; úklidník jen žádost (`deferEmployeeErasure`) | — | [PolicyBuilder.cs:237](../../src/Cleansia.Core.AppServices/Authentication/PolicyBuilder.cs#L237), [DeleteUserAccount.cs:33-36](../../src/Cleansia.Core.AppServices/Features/Gdpr/DeleteUserAccount.cs#L33-L36) |
| blokuje | čekající žádost; živá objednávka `New/Pending/Confirmed/OnTheWay/InProgress`; kladný kredit (`HasPositiveCreditBalanceAsync`) | [GdprDeletionService.cs:223-234](../../src/Cleansia.Core.AppServices/Services/GdprDeletionService.cs#L223-L234), [GdprDeletionService.cs:160-163](../../src/Cleansia.Core.AppServices/Services/GdprDeletionService.cs#L160-L163) |
| blokuje úklidníka | otevřená faktura, přiřazená živá zakázka, nevyplacená mzda | [GdprDeletionService.cs:168-181](../../src/Cleansia.Core.AppServices/Services/GdprDeletionService.cs#L168-L181) |
| odstraněné vazby a soubory | profilová fotka, dokumenty, zařízení, notifikace, načtené uložené adresy, šablony, výplatní údaje, lokální Stripe id | [GdprDeletionService.cs:311-312](../../src/Cleansia.Core.AppServices/Services/GdprDeletionService.cs#L311-L312), [GdprDeletionService.cs:395-507](../../src/Cleansia.Core.AppServices/Services/GdprDeletionService.cs#L395-L507) |
| fotografie objednávek | `Anonymize` ponechá řádky; smazání blobu se pouze zkusí, `ExtractBlobNameFromUrl` zahrnuje kontejner a může mířit chybně | [GdprDeletionService.cs:369-384](../../src/Cleansia.Core.AppServices/Services/GdprDeletionService.cs#L369-L384), [GdprDeletionService.cs:560-564](../../src/Cleansia.Core.AppServices/Services/GdprDeletionService.cs#L560-L564) |
| anonymizováno | `User`, objednávky a adresa úklidníka; adresy nahrazeny anonymními kopiemi, původní smazány pouze bez zbývajícího vlastníka | [GdprDeletionService.cs:395-420](../../src/Cleansia.Core.AppServices/Services/GdprDeletionService.cs#L395-L420), [GdprDeletionService.cs:527-541](../../src/Cleansia.Core.AppServices/Services/GdprDeletionService.cs#L527-L541) |
| adresní hranice výmazu | census zahrnuje neaktivní odkazy; adresy pouze uložené, bez objednávky/úklidníka, nemají úplný úklid řádků | [AddressRepository.cs:30-50](../../src/Cleansia.Infra.Database/Repositories/AddressRepository.cs#L30-L50), [GdprDeletionService.cs:395-404](../../src/Cleansia.Core.AppServices/Services/GdprDeletionService.cs#L395-L404) |
| anonymizován úklidník | `Employee.Anonymize`: `RegistrationNumber`, `IBAN`, `PassportId` přepsány, nouzový kontakt vymazán; řádek zůstává | [Employee.cs:352-365](../../src/Cleansia.Core.Domain/Users/Employee.cs#L352-L365) |
| zachováno | text sporu do `TextRetainedUntil`; přijetí smlouvy trvale (`Pseudonymise`); souhlasy a tokeny odvolány | [GdprDeletionService.cs:464-465](../../src/Cleansia.Core.AppServices/Services/GdprDeletionService.cs#L464-L465), [WorkContractAcceptance.cs:98-101](../../src/Cleansia.Core.Domain/Contracts/WorkContractAcceptance.cs#L98-L101) |
| selhání | `RetryFailedUserDeletions` denně; `Processing` > 30 min (`StaleProcessingAfter`) = selhání | [RetryFailedUserDeletionsFunction.cs:11](../../src/Cleansia.Functions/Functions/RetryFailedUserDeletionsFunction.cs#L11), [AdminRetryUserDeletion.cs:28](../../src/Cleansia.Core.AppServices/Features/Gdpr/AdminRetryUserDeletion.cs#L28) |

### 11.3 Retence

| okno | dny | co | citace |
|---|---|---|---|
| ExpiredUserCodes; StaleDevices | zapnuto; 90 | nuluje prošlé kódy; maže zařízení podle `LastActiveAt` | [RetentionDefaults.cs:19](../../src/Cleansia.Core.AppServices/Features/DataRetention/RetentionDefaults.cs#L19), [RetentionDefaults.cs:20](../../src/Cleansia.Core.AppServices/Features/DataRetention/RetentionDefaults.cs#L20) |
| OldGdprRequests | 3 roky | nuluje `ProcessedBy` (`DefaultGdprRequestsYears`); řádek se nemaže | [RetentionDefaults.cs:21](../../src/Cleansia.Core.AppServices/Features/DataRetention/RetentionDefaults.cs#L21) |
| OrderCustomerPii | 2 roky | `DefaultOrderPiiYears`: dokončené anonymizuje, `AnonymizeCustomerAddress` kopíruje adresu; původní maže jen nesdílenou | [RetentionDefaults.cs:22](../../src/Cleansia.Core.AppServices/Features/DataRetention/RetentionDefaults.cs#L22), [DataRetentionBackgroundService.cs:197-215](../../src/Cleansia.Core.AppServices/Features/DataRetention/DataRetentionBackgroundService.cs#L197-L215) |
| WithdrawnConsents; SupersededDocuments | 3 roky; 365 dní | maže odvolané souhlasy; maže blob i řádek dokumentu | [RetentionDefaults.cs:23](../../src/Cleansia.Core.AppServices/Features/DataRetention/RetentionDefaults.cs#L23), [RetentionDefaults.cs:24](../../src/Cleansia.Core.AppServices/Features/DataRetention/RetentionDefaults.cs#L24) |
| UserNotifications; RefreshTokenCleanup | 90 dní (strop 500); 90 dní | maže feed; maže odvolané a prošlé tokeny | [RetentionDefaults.cs:25](../../src/Cleansia.Core.AppServices/Features/DataRetention/RetentionDefaults.cs#L25), [RefreshTokenCleanupService.cs:22](../../src/Cleansia.Core.AppServices/Features/DataRetention/RefreshTokenCleanupService.cs#L22) |
| CustomerActionAudits; AdminActionAudits; EmployeeActionAudits; DisputeText; metadata smlouvy | 3 roky | maže audity; pseudonymizuje text sporu a metadata přijetí | [RetentionDefaults.cs:26-31](../../src/Cleansia.Core.AppServices/Features/DataRetention/RetentionDefaults.cs#L26-L31) |
| OrderPhotos → R7 | 7 dní od dokončení | drží je neuzavřený spor; neúspěšné smazání blobu zachová řádek | [OrderPhotoRepository.cs:39-43](../../src/Cleansia.Infra.Database/Repositories/OrderPhotoRepository.cs#L39-L43), [DataRetentionBackgroundService.cs:426-435](../../src/Cleansia.Core.AppServices/Features/DataRetention/DataRetentionBackgroundService.cs#L426-L435) |
| GuestOrderAccessTokens | po expiraci nebo odvolání | maže nepoužitelné tokeny | [DataRetentionBackgroundService.cs:466-475](../../src/Cleansia.Core.AppServices/Features/DataRetention/DataRetentionBackgroundService.cs#L466-L475) |
| sweep | neděle 03:00 UTC | per společnost; vypínač `DataRetention:Enabled` výchozí `true` | [DataRetentionTimerFunction.cs:10](../../src/Cleansia.Functions/Functions/DataRetentionTimerFunction.cs#L10), [DataRetentionConfig.cs:24](../../src/Cleansia.Infra.Common/Configuration/DataRetentionConfig.cs#L24) |

### 11.4 Kdo vidí co

| údaj | kdo | citace |
|---|---|---|
| detail objednávky včetně zákazníka | `SupportOrAbove`; Accountant ne | [PolicyBuilder.cs:42-44](../../src/Cleansia.Core.AppServices/Authentication/PolicyBuilder.cs#L42-L44), [PhysicalPolicy.cs:17](../../src/Cleansia.Core.AppServices/Authentication/PhysicalPolicy.cs#L17) |
| kontakty a adresa zákazníka | přiřazený úklidník (`AssignedEmployees`) bez časového okna → R8 | [OrderAccessService.cs:83-94](../../src/Cleansia.Core.AppServices/Authentication/OrderAccessService.cs#L83-L94) |
| `AccessInstructions` | přiřazený bez odhalovacího kroku; `RecordOnceOutOfBandAsync` audituje první neprázdné čtení za úklidníka/zakázku; admin auditovaný reveal → R8 | [GetOrderDetails.cs:159-171](../../src/Cleansia.Core.AppServices/Features/Orders/GetOrderDetails.cs#L159-L171), [EmployeeActionAuditRepository.cs:13-39](../../src/Cleansia.Infra.Database/Repositories/EmployeeActionAuditRepository.cs#L13-L39) |
| prohlížející úklidník; zákazník | bez jména, kontaktů, adresy, kódu; zákazník jen vlastní objednávku | [OrderPiiRedaction.cs:40-67](../../src/Cleansia.Core.AppServices/Features/Orders/OrderPiiRedaction.cs#L40-L67), [OrderAccessService.cs:78-81](../../src/Cleansia.Core.AppServices/Authentication/OrderAccessService.cs#L78-L81) |
| bankovní účet úklidníka | maskovaný `AccountantOrAbove`; odhalení `ManagerOrAbove`, `RevealCount` | [PolicyBuilder.cs:78-80](../../src/Cleansia.Core.AppServices/Authentication/PolicyBuilder.cs#L78-L80), [RevealEmployeePayoutDetails.cs:57-60](../../src/Cleansia.Core.AppServices/Features/Employees/RevealEmployeePayoutDetails.cs#L57-L60) |
| analytika → R9 | žádný GA/gtag; `sentryDsn: ''`; Mapbox přes Customer API | grep -rni "gtag\|googletagmanager" src/Cleansia.App/apps/cleansia.app → 0; [environment.prod.ts:23](../../src/Cleansia.App/apps/cleansia.app/src/environments/environment.prod.ts#L23) |

## 12. Provozní společnosti a holding

### 12.1 Model

| fakt o `Tenant` | citace |
|---|---|
| `Tenant : Auditable`; `Id` přidělený, max 26 znaků; devět sloupců cyklu (`WindDownFrom` … `ArchiveManifestSha256`) | [Tenant.cs:18-24](../../src/Cleansia.Core.Domain/Tenancy/Tenant.cs#L18-L24), [Tenant.cs:26-45](../../src/Cleansia.Core.Domain/Tenancy/Tenant.cs#L26-L45) |
| registr jen seed; `Tenant.Create` bez produkčního volajícího → R2 | grep -rn "Tenant.Create(" src/ mimo testy → 0; [Tenant.cs:68-73](../../src/Cleansia.Core.Domain/Tenancy/Tenant.cs#L68-L73) |
| trh → provozovatel `OperatorTenantId` zapisuje jen seed; trh nabízen jen s `OperatorTenant.IsActive` | [CountryConfiguration.cs:117-123](../../src/Cleansia.Core.Domain/Configuration/CountryConfiguration.cs#L117-L123), [CountryRepository.cs:40-48](../../src/Cleansia.Infra.Database/Repositories/CountryRepository.cs#L40-L48) |
| `CompanyInfo` = vystavitel dokladů (`LegalName`, `RegistrationNumber`, `VatNumber`, `IsVatPayer`); seed IČO i DIČ `REPLACE WITH ACTUAL` → R2 | [CompanyInfo.cs:7-83](../../src/Cleansia.Core.Domain/Company/CompanyInfo.cs#L7-L83), [insert_seed_data.sql:1161-1162](../../sql-scripts/insert_seed_data.sql#L1161-L1162) |
| admin mutace nesou `AdminActionAudit` bez IP; `DefaultAdminAuditRetentionYears` v §11.3 | [AuditEntryFactory.cs:76-92](../../src/Cleansia.Core.AppServices/Auditing/AuditEntryFactory.cs#L76-L92), [RetentionDefaults.cs:30](../../src/Cleansia.Core.AppServices/Features/DataRetention/RetentionDefaults.cs#L30) |
| pojistné krytí trhu je obsah trhu (`InsuranceCoverageAmount`), ne údaj sledovaný u úklidníka | [CountryMappers.cs:25](../../src/Cleansia.Core.AppServices/Mappers/CountryMappers.cs#L25) |

### 12.2 Životní cyklus

| krok | kdo | efekt | citace |
|---|---|---|---|
| stavy | — | `Operating`, `WindingDown`, `Deactivated`, `Frozen`, `Archived`; platí nejvyšší | [CompanyLifecycleState.cs:10-17](../../src/Cleansia.Core.Domain/Tenancy/CompanyLifecycleState.cs#L10-L17), [Tenant.cs:59-64](../../src/Cleansia.Core.Domain/Tenancy/Tenant.cs#L59-L64) |
| pět politik (`CanViewCompanyLifecycle` … `CanArchiveCompany`) | `AdministratorOnly` | audity: `company.deactivate`, reaktivace, útlum, archiv | [PolicyBuilder.cs:219-223](../../src/Cleansia.Core.AppServices/Authentication/PolicyBuilder.cs#L219-L223), [DeactivateCompany.cs:26](../../src/Cleansia.Core.AppServices/Features/CompanyLifecycle/DeactivateCompany.cs#L26) |
| deaktivace výchozího trhu | — | `CompanyOperatesDefaultMarket` blokuje deaktivaci `cleansia-cz`, a tím archivaci; útlum lze zahájit přes `FromDate` | [DeactivateCompany.cs:48-49](../../src/Cleansia.Core.AppServices/Features/CompanyLifecycle/DeactivateCompany.cs#L48-L49), [WindDownCompany.cs:39-58](../../src/Cleansia.Core.AppServices/Features/CompanyLifecycle/WindDownCompany.cs#L39-L58) |
| deaktivace | Administrator | `IsActive = false`: úklidníkovi odmítne přihlášení i refresh; vydaný JWT platí. Při `IsWindDownRequested` znovu zařadí útlum do fronty | [CompanySignInGate.cs:17-41](../../src/Cleansia.Core.AppServices/Tenancy/CompanySignInGate.cs#L17-L41), [DeactivateCompany.cs:89-92](../../src/Cleansia.Core.AppServices/Features/CompanyLifecycle/DeactivateCompany.cs#L89-L92) |
| reaktivace; útlum | Administrator | reaktivace jen z `Deactivated` a nic nevrací; útlum: `FromDate` jednou, fronta `company-wind-down` | [ReactivateCompany.cs:11-15](../../src/Cleansia.Core.AppServices/Features/CompanyLifecycle/ReactivateCompany.cs#L11-L15), [WindDownCompany.cs:50-57](../../src/Cleansia.Core.AppServices/Features/CompanyLifecycle/WindDownCompany.cs#L50-L57) |
| sweep útlumu | fronta | oznámení, storna `New/Confirmed/OnTheWay`, pauza šablon a zrušení Plus; `DischargeCreditAsync` a `CloseLastPeriodAsync` vyžadují deaktivaci | [CompanyWindDownService.cs:77-85](../../src/Cleansia.Core.AppServices/Services/CompanyWindDownService.cs#L77-L85), [CompanyWindDownService.cs:486-490](../../src/Cleansia.Core.AppServices/Services/CompanyWindDownService.cs#L486-L490) |
| archiv a zmrazení | Administrator | vyžaduje `IsDeactivated`, `IsWindDownRequested`, nulové živé položky, uplynulý chargeback horizont; od požadavku `CommitAsync` vyhodí `CompanyArchivedException` (409) | [ArchiveCompany.cs:47-72](../../src/Cleansia.Core.AppServices/Features/CompanyLifecycle/ArchiveCompany.cs#L47-L72), [CleansiaDbContext.cs:123-149](../../src/Cleansia.Infra.Database/CleansiaDbContext.cs#L123-L149) |
| balík | fronta `company-archive` | JSONL knih, auditů a PDF; manifest SHA-256; řádky se nemažou; nevratné (`IsFrozen` → `CompanyArchived`) | [CompanyArchiveService.cs:172-182](../../src/Cleansia.Core.AppServices/Services/CompanyArchiveService.cs#L172-L182), [ReactivateCompany.cs:32-35](../../src/Cleansia.Core.AppServices/Features/CompanyLifecycle/ReactivateCompany.cs#L32-L35) |

## 13. Co běží bez člověka

| úloha | spouštěč | co dělá | co zapisuje | citace |
|---|---|---|---|---|
| `CleanupStalePendingOrders` | timer `0 */15 * * * *` | stale pending, `ReleaseOrphanedBenefitReservations`, `CancelUnfilledOrders` | `Order`, kredit | [CleanupStalePendingOrdersFunction.cs:10](../../src/Cleansia.Functions/Functions/CleanupStalePendingOrdersFunction.cs#L10) |
| `AutoCancelStaleRecurringOrders` | timer `0 0 * * * *` | ruší nepotvrzené výskyty | `Order`, kredit | [AutoCancelStaleRecurringOrdersFunction.cs:9-11](../../src/Cleansia.Functions/Functions/AutoCancelStaleRecurringOrdersFunction.cs#L9-L11) |
| `MaterializeRecurringBookings` | timer `%MaterializeRecurringBookingsCron%` | tvoří výskyty `HorizonDays` dopředu | `Order` | [MaterializeRecurringBookingsFunction.cs:12-14](../../src/Cleansia.Functions/Functions/MaterializeRecurringBookingsFunction.cs#L12-L14) |
| `SendRecurringOrderReminders` | timer `%SendRecurringOrderRemindersCron%` | push `recurring.scheduled` | `RecurringReminderSentAt` | [SendRecurringOrderRemindersFunction.cs:13-15](../../src/Cleansia.Functions/Functions/SendRecurringOrderRemindersFunction.cs#L13-L15) |
| `SendPreCleaningReminders` | timer `%SendPreCleaningRemindersCron%` | push `order.starting_soon` | `PreCleaningReminderSentAt` | [SendPreCleaningRemindersFunction.cs:15-17](../../src/Cleansia.Functions/Functions/SendPreCleaningRemindersFunction.cs#L15-L17) |
| `SendCleanerJobReminders` | timer `%SendCleanerJobRemindersCron%` | push před zakázkou a nudge | razítka přiřazení | [SendCleanerJobRemindersFunction.cs:16-18](../../src/Cleansia.Functions/Functions/SendCleanerJobRemindersFunction.cs#L16-L18) |
| `SendNewJobsDigest` | timer `%SendNewJobsDigestCron%` | digest `order.new_available` | `LastNewJobsDigestAt` | [SendNewJobsDigestTimerFunction.cs:13-17](../../src/Cleansia.Functions/Functions/SendNewJobsDigestTimerFunction.cs#L13-L17) |
| `SendTomorrowJobDigest` | timer `%SendTomorrowJobDigestCron%` | `order.reminder_tomorrow` v 18:00 místně | `LastTomorrowDigestAt` | [SendTomorrowJobDigestFunction.cs:16-18](../../src/Cleansia.Functions/Functions/SendTomorrowJobDigestFunction.cs#L16-L18) |
| `NotifyLapsedPreferredOffers` | timer `0 */5 * * * *` | `order.preferred_offer_closed`; hold neuvolňuje | `PreferredOfferLapseNotifiedAt` | [NotifyLapsedPreferredOffersFunction.cs:13-15](../../src/Cleansia.Functions/Functions/NotifyLapsedPreferredOffersFunction.cs#L13-L15) |
| `SendMembershipLifecycleNotifications` | timer `%SendMembershipLifecycleNotificationsCron%` | push o obnově a konci členství | razítka členství | [SendMembershipLifecycleNotificationsFunction.cs:13-15](../../src/Cleansia.Functions/Functions/SendMembershipLifecycleNotificationsFunction.cs#L13-L15) |
| `ExpireStaleCredit` | timer `0 30 3 * * *` | odpis kreditu po `ExpiryMonths` | `CreditTransaction` `Expired` | [ExpireStaleCreditFunction.cs:11-13](../../src/Cleansia.Functions/Functions/ExpireStaleCreditFunction.cs#L11-L13) |
| `ExpireStaleReferrals` | timer `%ExpireStaleReferralsCron%` | `Accepted` po 90 dnech → `Expired` | `Referral` | [ExpireStaleReferralsFunction.cs:13-15](../../src/Cleansia.Functions/Functions/ExpireStaleReferralsFunction.cs#L13-L15) |
| `CloseExpiredPayPeriods` | timer `0 0 2 * * *` | zavírá období, otevírá další | `PayPeriod`, faktury | [PayPeriodTimerFunction.cs:9-11](../../src/Cleansia.Functions/Functions/PayPeriodTimerFunction.cs#L9-L11) |
| `SendPeriodEndReminders` | timer `0 0 9 * * *` | e-mail před koncem období; bez dedup razítka | nic | [PeriodReminderTimerFunction.cs:9-11](../../src/Cleansia.Functions/Functions/PeriodReminderTimerFunction.cs#L9-L11) |
| `FiscalReconciliation` | timer `0 */5 * * * *` | znovu zařadí neodeslané fiskální zprávy | outbox | [FiscalReconciliationFunction.cs:15-17](../../src/Cleansia.Functions/Functions/FiscalReconciliationFunction.cs#L15-L17) |
| `RetryFailedFiscalRegistrations` | timer `0 */5 * * * *` | opakuje registraci účtenky | `OrderReceipt` | [RetryFailedFiscalRegistrationsFunction.cs:11-13](../../src/Cleansia.Functions/Functions/RetryFailedFiscalRegistrationsFunction.cs#L11-L13) |
| `DataRetentionCleanup` | timer `0 0 3 * * 0` | 14 úloh přes `RunSafeAsync` | maže a anonymizuje (§11.3) | [DataRetentionTimerFunction.cs:9-11](../../src/Cleansia.Functions/Functions/DataRetentionTimerFunction.cs#L9-L11), [DataRetentionBackgroundService.cs:69-82](../../src/Cleansia.Core.AppServices/Features/DataRetention/DataRetentionBackgroundService.cs#L69-L82) |
| `RetryFailedUserDeletions` | timer `0 0 5 * * *` | opakuje neúspěšné výmazy | `GdprRequest` | [RetryFailedUserDeletionsFunction.cs:10-12](../../src/Cleansia.Functions/Functions/RetryFailedUserDeletionsFunction.cs#L10-L12) |
| `RefreshTokenCleanup` | timer `0 30 3 * * *` | maže odvolané a prošlé tokeny | `RefreshToken` | [RefreshTokenCleanupTimerFunction.cs:9-11](../../src/Cleansia.Functions/Functions/RefreshTokenCleanupTimerFunction.cs#L9-L11) |
| `GenerateReceipt` | fronta `generate-receipt` | účtenka, fiskální registrace, e-mail | `OrderReceipt` | [GenerateReceiptFunction.cs:10-14](../../src/Cleansia.Functions/Functions/GenerateReceiptFunction.cs#L10-L14) |
| `GenerateInvoice` | fronta `generate-invoice` | faktura úklidníka za období | `EmployeeInvoice` | [GenerateInvoiceFunction.cs:9-13](../../src/Cleansia.Functions/Functions/GenerateInvoiceFunction.cs#L9-L13) |
| `CalculateOrderPay` | fronta `calculate-order-pay` | odměna za dokončenou objednávku; archiv → dead-letter | `OrderEmployeePay` | [CalculateOrderPayFunction.cs:11](../../src/Cleansia.Functions/Functions/CalculateOrderPayFunction.cs#L11) |
| `SendPushNotification`, `SendEmail`, `SendLiveActivityUpdate`, `SendSitewidePromoFanout` | fronty | FCM push (at-most-once), SendGrid (at-least-once), APNs, fan-out kampaně | maže odmítnutá `Device`; `CampaignProgress` | [SendPushNotificationHandler.cs:19-37](../../src/Cleansia.Functions.Core/Handlers/SendPushNotificationHandler.cs#L19-L37) |
| `CompanyWindDown` | fronta `company-wind-down` | sweep útlumu společnosti | objednávky, kredit, období | [CompanyWindDownFunction.cs:9-13](../../src/Cleansia.Functions/Functions/CompanyWindDownFunction.cs#L9-L13) |
| `CompanyArchive` | fronta `company-archive` | archivní balík zmrazené společnosti | blob + `ArchivedOn` | [CompanyArchiveFunction.cs:11](../../src/Cleansia.Functions/Functions/CompanyArchiveFunction.cs#L11) |
| `LegalDocumentSeedHostedService` | start hosta | seeduje právní texty | `LegalDocuments` | [DbContextBindingExtensions.cs:81](../../src/Cleansia.Config/Database/DbContextBindingExtensions.cs#L81) |

## A1 Tabulka všech čísel

| co | hodnota | kde (identifikátor) | citace |
|---|---|---|---|
| Standardní / minimální předstih | 4 h / 2 h | `StandardLeadTimeHours`, `ExpressLeadTimeHours` | [BookingPolicy.cs:20](../../src/Cleansia.Core.AppServices/Features/Orders/BookingPolicy.cs#L20), [BookingPolicy.cs:26](../../src/Cleansia.Core.AppServices/Features/Orders/BookingPolicy.cs#L26) |
| Expresní příplatek | 20 % | `ExpressSurchargeRate` | [BookingPolicy.cs:32](../../src/Cleansia.Core.AppServices/Features/Orders/BookingPolicy.cs#L32) |
| Nabídka startů klienta; serverové okno nerozhodnuté | 08:00 včetně–20:00 bez konce | `FIRST_WINDOW_HOUR`, `LAST_WINDOW_HOUR`; server neomezuje | [booking-window.models.ts:44-51](../../src/Cleansia.App/libs/shared/models/src/lib/models/booking-window.models.ts#L44-L51) |
| Maximální rozsah | 8 pokojů / 4 koupelny | `MaxRooms`, `MaxBathrooms` | [BookingPolicy.cs:37-38](../../src/Cleansia.Core.AppServices/Features/Orders/BookingPolicy.cs#L37-L38) |
| Krok startů na webu | 15 min | `BOOKING_SLOT_INTERVAL_MINUTES` | [booking-window.models.ts:13](../../src/Cleansia.App/libs/shared/models/src/lib/models/booking-window.models.ts#L13) |
| Start nejdříve před termínem | 60 min | `StartGraceWindowMinutes` | [BookingPolicy.cs:62](../../src/Cleansia.Core.AppServices/Features/Orders/BookingPolicy.cs#L62) |
| Storno zdarma; storno 4–24 h | ≥ 24 h; 25 % | `FreeCancellationHours`, `PartialCancellationFeeRate` | [BookingPolicy.cs:65](../../src/Cleansia.Core.AppServices/Features/Orders/BookingPolicy.cs#L65), [BookingPolicy.cs:68](../../src/Cleansia.Core.AppServices/Features/Orders/BookingPolicy.cs#L68) |
| Storno < 4 h; hranice | 50 %; 4 h | `LastMinuteCancellationFeeRate`, `PartialCancellationHours` | [BookingPolicy.cs:71](../../src/Cleansia.Core.AppServices/Features/Orders/BookingPolicy.cs#L71), [BookingPolicy.cs:74](../../src/Cleansia.Core.AppServices/Features/Orders/BookingPolicy.cs#L74) |
| „Oops" okno; okno prvního zákazníka | 15 min; 60 min (neaktivní) | `OopsWindowMinutesStandard`, `OopsWindowMinutesFirstTime` | [BookingPolicy.cs:80](../../src/Cleansia.Core.AppServices/Features/Orders/BookingPolicy.cs#L80), [BookingPolicy.cs:83](../../src/Cleansia.Core.AppServices/Features/Orders/BookingPolicy.cs#L83) |
| Max. podíl kreditu; max. délka objednávky | 70 %; 24 h = 1 440 min | `MaxCreditShareOfOrder`, `MaxBookableOrderSpanHours` | [BookingPolicy.cs:98](../../src/Cleansia.Core.AppServices/Features/Orders/BookingPolicy.cs#L98), [BookingPolicy.cs:146-151](../../src/Cleansia.Core.AppServices/Features/Orders/BookingPolicy.cs#L146-L151) |
| Nástěnka po začátku; rezervní sedadla | 2 h; 0 | `BoardBacklogHours`, `SpareSeatsPerOrder` | [BookingPolicy.cs:130](../../src/Cleansia.Core.AppServices/Features/Orders/BookingPolicy.cs#L130), [BookingPolicy.cs:136](../../src/Cleansia.Core.AppServices/Features/Orders/BookingPolicy.cs#L136) |
| Hold preferovaného; min. předstih | 10 % předstihu, strop 12 h; 8 h | `PreferredHoldFraction`, `ComputePreferredHold` | [BookingPolicy.cs:217-218](../../src/Cleansia.Core.AppServices/Features/Orders/BookingPolicy.cs#L217-L218), [BookingPolicy.cs:256-258](../../src/Cleansia.Core.AppServices/Features/Orders/BookingPolicy.cs#L256-L258) |
| Kola nabídky / okno nástěnce | 2 / 80 % | `MaxPreferredOfferRounds`, `MinimumOpenBoardShare` | [BookingPolicy.cs:232](../../src/Cleansia.Core.AppServices/Features/Orders/BookingPolicy.cs#L232), [BookingPolicy.cs:242](../../src/Cleansia.Core.AppServices/Features/Orders/BookingPolicy.cs#L242) |
| Minuty na úklidníka / sken kolizí | 120 / 168 h | `MinutesPerEmployee`, `MaxOrderSpanHours` | [OrderDuration.cs:27](../../src/Cleansia.Core.Domain/Orders/OrderDuration.cs#L27), [Order.cs:126](../../src/Cleansia.Core.Domain/Orders/Order.cs#L126) |
| Strop Plus + tier; zaokrouhlení | 12 %; 2 místa `AwayFromZero`, kredit `Math.Floor` | `MaxCombinedDiscountFraction`, `OrderFactory` | [OrderFactory.cs:54](../../src/Cleansia.Core.AppServices/Features/Orders/OrderFactory.cs#L54), [OrderFactory.cs:368-369](../../src/Cleansia.Core.AppServices/Features/Orders/OrderFactory.cs#L368-L369) |
| Sedadlo / „moji úklidníci" | 50 / 20 | `MaxRecipients`, `MaxCleaners` | [SeatOpenedNotifier.cs:37](../../src/Cleansia.Core.AppServices/Features/Orders/SeatOpenedNotifier.cs#L37), [GetMyServingCleaners.cs:14](../../src/Cleansia.Core.AppServices/Features/Orders/GetMyServingCleaners.cs#L14) |
| Týdenní limit; poloměr digestu | `NULL` = neomezeno, min. 1; 1–500 km | `WeeklyOrderLimit`, `JobProximity` | [AdminSetEmployeeWeeklyOrderLimit.cs:57](../../src/Cleansia.Core.AppServices/Features/Employees/AdminSetEmployeeWeeklyOrderLimit.cs#L57), [JobProximity.cs:43-49](../../src/Cleansia.Core.Domain/Orders/JobProximity.cs#L43-L49) |
| Vratka při stornu | round(`TotalPrice` × (1 − sazba), 2) | `CancellationAssessor` | [CancellationAssessor.cs:69-77](../../src/Cleansia.Core.AppServices/Features/Orders/CancellationAssessor.cs#L69-L77) |
| Storno administrátorem; důvod | sazba 0, plná vratka; ≤ 500 znaků | `feeRate: 0m`, `Reason` | [PlatformOrderCancellation.cs:27-35](../../src/Cleansia.Core.AppServices/Services/PlatformOrderCancellation.cs#L27-L35), [AdminCancelOrder.cs:39-41](../../src/Cleansia.Core.AppServices/Features/Orders/AdminCancelOrder.cs#L39-L41) |
| Refundační okno; Stripe fee | 14 dní; 0 | `RefundWindowDays`, `RefundStripeFeeRate` | [RefundPolicy.cs:17](../../src/Cleansia.Core.AppServices/Features/Refunds/RefundPolicy.cs#L17), [CountryConfiguration.cs:89-98](../../src/Cleansia.Core.Domain/Configuration/CountryConfiguration.cs#L89-L98) |
| Expirace kreditu; ruční grant | 12 měsíců; ≤ 10 000 | `ExpiryMonths`, `SanityCap` | [CreditAccount.cs:69](../../src/Cleansia.Core.Domain/Credit/CreditAccount.cs#L69), [IssueCustomerCredit.cs:67](../../src/Cleansia.Core.AppServices/Features/Credit/Admin/IssueCustomerCredit.cs#L67) |
| Omluvný kredit; bod věrnosti; jiné měny | 250 Kč; 1 bod za 10 Kč; EUR 10, PLN 40, GBP 9, USD 10 | `NoShowCredit`, `LoyaltyPointsDivisor` | [insert_seed_data.sql:490](../../sql-scripts/insert_seed_data.sql#L490), [insert_seed_data.sql:491-494](../../sql-scripts/insert_seed_data.sql#L491-L494) |
| Neuhrazená karta; nepotvrzený opakovaný; neobsazená | po 1 h; 1 h před termínem; 30 min po termínu (6 h zpět) | `OlderThanHours`, `GraceMinutes` | [CleanupStalePendingOrdersHandler.cs:28](../../src/Cleansia.Functions.Core/Handlers/CleanupStalePendingOrdersHandler.cs#L28), [CancelUnfilledOrders.cs:63](../../src/Cleansia.Core.AppServices/Features/Orders/CancelUnfilledOrders.cs#L63) |
| Materializace; krok opakování | 7 dní; 7 / 14 / 30 dní, potom den týdne | `HorizonDays`; kalendářní význam `Monthly` nerozhodnutý | [MaterializeRecurringBookings.cs:27](../../src/Cleansia.Core.AppServices/Features/Bookings/MaterializeRecurringBookings.cs#L27), [MaterializeRecurringBookingTemplate.cs:324-342](../../src/Cleansia.Core.AppServices/Features/Bookings/MaterializeRecurringBookingTemplate.cs#L324-L342) |
| Připomínky zákazníkovi | opakovaný 6–26 h; před úklidem 55–70 min | `LeadHoursLow/High`, `LeadMinutesLow/High` | [SendRecurringOrderReminders.cs:22](../../src/Cleansia.Core.AppServices/Features/Bookings/SendRecurringOrderReminders.cs#L22), [SendPreCleaningReminders.cs:41](../../src/Cleansia.Core.AppServices/Features/Orders/SendPreCleaningReminders.cs#L41) |
| Připomínky úklidníkovi; digest | 110–130 a 25–40 min; 18:00 místně | `SendCleanerJobReminders`, `LocalSendHour` | [SendCleanerJobReminders.cs:47-51](../../src/Cleansia.Core.AppServices/Features/Orders/SendCleanerJobReminders.cs#L47-L51), [SendTomorrowJobDigest.cs:59](../../src/Cleansia.Core.AppServices/Features/Orders/SendTomorrowJobDigest.cs#L59) |
| Okno reklamace; popis; řádky | 24 h; 10–2000 znaků; 50 | `FilingWindowHours`, `MaxLines` | [DisputeLimits.cs:29](../../src/Cleansia.Core.Domain/Disputes/DisputeLimits.cs#L29), [DisputeLimits.cs:13](../../src/Cleansia.Core.Domain/Disputes/DisputeLimits.cs#L13) |
| Příloha / SAS / zpráva sporu | 10 MB / 1 h / ≤ 2000 znaků | `MaxFileSizeBytes`, `AddDisputeMessage` | [UploadDisputeEvidence.cs:17](../../src/Cleansia.Core.AppServices/Features/Disputes/UploadDisputeEvidence.cs#L17), [AddDisputeMessage.cs:35](../../src/Cleansia.Core.AppServices/Features/Disputes/AddDisputeMessage.cs#L35) |
| Číslo dokladu; rok čítače bez režimu | `RCP-{rok}-{NNNN}`; 0 | `ReceiptNumberFormat.Pattern`, `NoAnnualResetYear` | [Constants.cs:83-85](../../src/Cleansia.Core.AppServices/Common/Constants.cs#L83-L85), [FiscalSequenceScope.cs:12-26](../../src/Cleansia.Core.Fiscal.Abstractions/FiscalSequenceScope.cs#L12-L26) |
| Fiskální retry; seznam selhání | max 10 (1 m … 24 h); 200 | `MaxFiscalRetries`, `MaxResults` | [OrderReceipt.cs:183](../../src/Cleansia.Core.Domain/Receipts/OrderReceipt.cs#L183), [GetFiscalFailures.cs:11](../../src/Cleansia.Core.AppServices/Features/FiscalFailures/GetFiscalFailures.cs#L11) |
| DPH CZE; pojistné krytí | 21 %; 1 000 000 Kč | `StandardVatRate`, `InsuranceCoverageAmount`; sjednocení zdrojů DPH nerozhodnuté | [insert_seed_data.sql:1010](../../sql-scripts/insert_seed_data.sql#L1010), [insert_seed_data.sql:1014](../../sql-scripts/insert_seed_data.sql#L1014) |
| Seed sazba úklidníka; násobky | 0,5 × cena; 0,5 / 0,75 / 1,0 | seed `ROUND(BasePrice * 0.5, 2)` | [insert_seed_data.sql:781](../../sql-scripts/insert_seed_data.sql#L781), [BulkCreateEmployeePayConfigs.cs:25-27](../../src/Cleansia.Core.AppServices/Features/PayConfig/BulkCreateEmployeePayConfigs.cs#L25-L27) |
| Sleva Plus; Plus volné storno | 5 %; 4 h | seed `PLUS_MONTHLY`: `DiscountPercentage`, `FreeCancellationWindowHours` | [insert_seed_data.sql:1806](../../sql-scripts/insert_seed_data.sql#L1806) |
| Plus: express upgrady; zkušební doba | 1 měsíčně; 0 dní | seed `PLUS_MONTHLY`: `ExpressUpgradesPerMonth`, `TrialPeriodDays` | [insert_seed_data.sql:1807](../../sql-scripts/insert_seed_data.sql#L1807), [insert_seed_data.sql:1808](../../sql-scripts/insert_seed_data.sql#L1808) |
| Cena Plus; připomínka obnovy | 199 Kč/měsíc, 2030 Kč/rok; 2–4 dny před | seed `MembershipPlanPrices`, `RenewalLeadDaysLow/High` | [insert_seed_data.sql:1836-1837](../../sql-scripts/insert_seed_data.sql#L1836-L1837), [SendMembershipLifecycleNotifications.cs:26-27](../../src/Cleansia.Core.AppServices/Features/Memberships/SendMembershipLifecycleNotifications.cs#L26-L27) |
| Tiery: prahy / slevy / floor; referral | 0, 500, 2000, 5000 / 0, 5, 10, 12 % / 1000 Kč; 150 bodů, 90 dní | seed `LoyaltyTierConfigs`, `PointsPerSide` | [insert_seed_data.sql:1716-1731](../../sql-scripts/insert_seed_data.sql#L1716-L1731), [ReferralPolicy.cs:14](../../src/Cleansia.Core.AppServices/Features/Orders/ReferralPolicy.cs#L14) |
| Veřejný promokód; seed promo | 10 %, 30 dní, 1×; WELCOME15 15 %, SPRING20 20 % od 1500, LOYAL10 10 % od 800 | `FirstOrderDiscountPercent`, seed `PromoCodes` | [RequestPromoCode.cs:33-36](../../src/Cleansia.Core.AppServices/Features/PromoCodes/RequestPromoCode.cs#L33-L36), [insert_seed_data.sql:1750-1782](../../sql-scripts/insert_seed_data.sql#L1750-L1782) |
| Kampaň: titulek / tělo / stránka; jazyky | ≤ 120 / ≤ 500 znaků / 200; 5 | `SendSitewidePromo`, `EmailLocale` | [SendSitewidePromo.cs:40-53](../../src/Cleansia.Core.AppServices/Features/Marketing/SendSitewidePromo.cs#L40-L53), [EmailLocale.cs:10-11](../../src/Cleansia.Core.AppServices/Common/EmailLocale.cs#L10-L11) |
| Dokumentů na požadavek; velikost; poznámka | 10; 10 MB; ≤ 500 znaků | `SaveMyDocuments`, `BlobFileSize` | [SaveMyDocuments.cs:53](../../src/Cleansia.Core.AppServices/Features/EmployeeDocuments/SaveMyDocuments.cs#L53), [BlobFileSize.cs:8-9](../../src/Cleansia.Core.AppServices/Common/Validators/BlobFileSize.cs#L8-L9) |
| Fotek na požadavek; `After`; min. trvání | 30; ≥ 1; 1 min | `SaveOrderPhotos`, `HasAfterPhotosAsync` | [SaveOrderPhotos.cs:47](../../src/Cleansia.Core.AppServices/Features/Orders/SaveOrderPhotos.cs#L47), [CompleteOrder.cs:183-189](../../src/Cleansia.Core.AppServices/Features/Orders/CompleteOrder.cs#L183-L189) |
| Poznámky; hodnocení | dokončení ≤ 1000, problém ≤ 2000 znaků; 1–5 | `CompletionNotes`, `SubmitOrderReview` | [CompleteOrder.cs:100](../../src/Cleansia.Core.AppServices/Features/Orders/CompleteOrder.cs#L100), [SubmitOrderReview.cs:48-53](../../src/Cleansia.Core.AppServices/Features/Orders/SubmitOrderReview.cs#L48-L53) |
| Výplatní období; připomínky konce | 7–31 dní (automaticky 1 měsíc); 3 a 1 den před | `PayPeriod`, `SendRemindersForPeriodsEndingInAsync` | [PayPeriod.cs:40-44](../../src/Cleansia.Core.Domain/EmployeePayroll/PayPeriod.cs#L40-L44), [PeriodReminderBackgroundService.cs:35-36](../../src/Cleansia.Core.AppServices/Services/PeriodReminderBackgroundService.cs#L35-L36) |
| Splatnost faktury | 14 dní | `PaymentTermsDays` | [Constants.cs:78](../../src/Cleansia.Core.AppServices/Common/Constants.cs#L78) |
| Číslo faktury / VS | `INV-YYYY-NNNNNN` / `YYYYNNNNNN` | `PayoutReferenceAllocator` | [PayoutReferenceAllocator.cs:50-52](../../src/Cleansia.Core.AppServices/Services/PayoutReferenceAllocator.cs#L50-L52) |
| Poznámka převodu / admin | 500 / 1000 znaků | `MarkInvoicePaid` | [MarkInvoicePaid.cs:59-65](../../src/Cleansia.Core.AppServices/Features/EmployeePayroll/MarkInvoicePaid.cs#L59-L65) |
| Ověřovací i reset kód; pokusy; zámek | 6 číslic, 15 minut; 5 a 5; zámek 15 minut | `OtpLength`, `MaxCodeVerificationAttempts` | [SecurityTokens.cs:33](../../src/Cleansia.Core.Domain/Common/SecurityTokens.cs#L33), [User.cs:15](../../src/Cleansia.Core.Domain/Users/User.cs#L15) |
| Heslo; hash | min. 8 znaků, písmeno + číslice; PBKDF2-SHA256, 600 000 iterací | `PasswordPattern`, `HashAndSaltPassword` | [ValidationExtensions.cs:13](../../src/Cleansia.Core.AppServices/Common/Validators/ValidationExtensions.cs#L13), [PasswordExtensions.cs:9-11](../../src/Cleansia.Core.Domain/Extensions/PasswordExtensions.cs#L9-L11) |
| Refresh token; access token web | 48 B = 64 znaků; 1 440 min | `TokenByteLength`, `AccessTokenExpMinutes` | [RefreshTokenService.cs:22](../../src/Cleansia.Core.AppServices/Services/RefreshTokenService.cs#L22), [appsettings.json:20](../../src/Cleansia.Web.Partner/appsettings.json#L20) |
| Access token Admin / mobil | 15 / 30 min | `AccessTokenExpMinutes` | [appsettings.json:19](../../src/Cleansia.Web.Admin/appsettings.json#L19), [appsettings.json:23](../../src/Cleansia.Web.Mobile.Partner/appsettings.json#L23) |
| Refresh „zapamatovat"; bez něj | web a partnerský mobil 30 dní, zákaznický mobil 90; 1 den | `RefreshTokenExpDays`, `RefreshTokenShortExpDays` | [appsettings.json:24](../../src/Cleansia.Web.Mobile.Customer/appsettings.json#L24), [appsettings.json:24](../../src/Cleansia.Web.Mobile.Partner/appsettings.json#L24) |
| `NotBefore` zpětně; věk úklidníka | 60 s; 18–120 let (zákazník bez minima) | `ClockDriftBufferSeconds`, `BeReasonableAge` | [TokenService.cs:28](../../src/Cleansia.Core.AppServices/Services/TokenService.cs#L28), [ValidationExtensions.cs:27-28](../../src/Cleansia.Core.AppServices/Common/Validators/ValidationExtensions.cs#L27-L28) |
| Přístup hosta / lidský kód | 256 bitů, 43 znaků; do 30 dní po úklidu / 6 hex znaků | `LifetimeDaysAfterCleaning`; `GenerateConfirmationCode` neopravňuje | [GuestOrderAccessToken.cs:30-69](../../src/Cleansia.Core.Domain/Orders/GuestOrderAccessToken.cs#L30-L69), [OrderExtensions.cs:7](../../src/Cleansia.Core.Domain/Extensions/OrderExtensions.cs#L7) |
| Objednávka: jméno / e-mail / telefon; adresa; instrukce | 2–100 / ≤ 150 / ≤ 20; ulice 5–255, město 2–100, PSČ 3–20; ≤ 2000 znaků | `CustomerName`, `Street`, `SpecialInstructions` | [CreateOrder.cs:107-130](../../src/Cleansia.Core.AppServices/Features/Orders/CreateOrder.cs#L107-L130), [CreateOrder.cs:132-160](../../src/Cleansia.Core.AppServices/Features/Orders/CreateOrder.cs#L132-L160) |
| `UserConsent`; `WorkContractAcceptance` | IP 45, UA 500; audience 40, label 120, id 64 | obě entity | [UserConsent.cs:23-38](../../src/Cleansia.Core.Domain/Users/UserConsent.cs#L23-L38), [WorkContractAcceptance.cs:47-58](../../src/Cleansia.Core.Domain/Contracts/WorkContractAcceptance.cs#L47-L58) |
| Účinnost VOP a zásad; smlouvy o dílo | 2026-09-14; 2026-09-20 | verze = adresář cesty; `title:` seedu | [cs.md:2](../../src/Cleansia.Infra.Database/Seed/Legal/customer/terms-of-service/any/2026-09-14/cs.md#L2), [cs.md:2](../../src/Cleansia.Infra.Database/Seed/Legal/customer/work-contract/any/2026-09-20/cs.md#L2) |
| Seedované VOP: storno; lhůta škody | zdarma před přijetím, 25 %, 50 %; 24 hodin | text VOP | [cs.md:23](../../src/Cleansia.Infra.Database/Seed/Legal/customer/terms-of-service/any/2026-09-14/cs.md#L23), [cs.md:27](../../src/Cleansia.Infra.Database/Seed/Legal/customer/terms-of-service/any/2026-09-14/cs.md#L27) |
| Retence: zařízení / dokumenty / notifikace | 90 / 365 / 90 dní, strop 500 | `RetentionDefaults` | [RetentionDefaults.cs:20](../../src/Cleansia.Core.AppServices/Features/DataRetention/RetentionDefaults.cs#L20), [RetentionDefaults.cs:24](../../src/Cleansia.Core.AppServices/Features/DataRetention/RetentionDefaults.cs#L24) |
| PII dokončených objednávek | 2 roky | `DefaultOrderPiiYears` | [RetentionDefaults.cs:22](../../src/Cleansia.Core.AppServices/Features/DataRetention/RetentionDefaults.cs#L22) |
| Souhlasy, audity všech tří aktérů, spor, metadata smlouvy, GDPR žádosti; fotky | 3 roky; fotky 7 dní, spor drží | `DefaultOrderPhotosDays`, `DefaultAdminAuditRetentionYears`, `DefaultEmployeeAuditRetentionYears` | [RetentionDefaults.cs:21-31](../../src/Cleansia.Core.AppServices/Features/DataRetention/RetentionDefaults.cs#L21-L31) |
| Dávka sweepu; tokeny; retry výmazů | 100 řádků; 90 dní; 30 min | `BatchSize`, `StaleProcessingAfter` | [RetentionDefaults.cs:39](../../src/Cleansia.Core.AppServices/Features/DataRetention/RetentionDefaults.cs#L39), [AdminRetryUserDeletion.cs:28](../../src/Cleansia.Core.AppServices/Features/Gdpr/AdminRetryUserDeletion.cs#L28) |
| `Tenant.Id` / `Name`; mrtvý běh útlumu | 26 / 200 znaků; 1 h | `[MaxLength(26)]`, `WindDownRunStaleness` | [Tenant.cs:18-24](../../src/Cleansia.Core.Domain/Tenancy/Tenant.cs#L18-L24), [Tenant.cs:16](../../src/Cleansia.Core.Domain/Tenancy/Tenant.cs#L16) |
| Účetní povrch / stampované tabulky | 21 typů / 49 | `AccountSurface`, `StampedTables` | [ArchivedCompanyWriteGuard.cs:26-49](../../src/Cleansia.Infra.Database/ArchivedCompanyWriteGuard.cs#L26-L49), [InitialMigrationTenantDdlTests.cs:24](../../src/Cleansia.Tests/Infrastructure/InitialMigrationTenantDdlTests.cs#L24) |
| Chargeback horizont | 180 dní, max. 730 | `lifecycle.chargeback_horizon_days` | [TenantSettingCatalog.cs:27-28](../../src/Cleansia.Core.AppServices/Features/TenantSettings/TenantSettingCatalog.cs#L27-L28) |

## A2 Zástupné hodnoty a DEV čísla

| hodnota | kde | co z ní právník nesmí opsat |
|---|---|---|
| „Návrh — toto znění zatím neprošlo právní kontrolou…" | `Návrh` na řádku 5 všech 15 seedovaných textů; token `{{currency}}` — [cs.md:5](../../src/Cleansia.Infra.Database/Seed/Legal/customer/terms-of-service/any/2026-09-14/cs.md#L5), [cs.md:19](../../src/Cleansia.Infra.Database/Seed/Legal/customer/terms-of-service/any/2026-09-14/cs.md#L19) | žádný seedovaný text jako platné znění; měnu |
| `info@cleansia.cz`, `+420 739 788 108` | seedované VOP — [cs.md:31](../../src/Cleansia.Infra.Database/Seed/Legal/customer/terms-of-service/any/2026-09-14/cs.md#L31) | kontakt společnosti |
| IČO `12345678`, DIČ `CZ12345678` „REPLACE WITH ACTUAL" | seed `CompanyInfo`, `12345678` — [insert_seed_data.sql:1161-1162](../../sql-scripts/insert_seed_data.sql#L1161-L1162) | identifikátory společnosti |
| `Václavské náměstí 1`, `+420 123 456 789`, IBAN `CZ65 0800 …`, SWIFT `GIBACZPX` | seed `CompanyInfo`, `GIBACZPX` — [insert_seed_data.sql:1164-1174](../../sql-scripts/insert_seed_data.sql#L1164-L1174) | sídlo, telefon, bankovní spojení |
| „Cleansia s.r.o." v `CompanyInfo` vs. `Tenant` „Cleansia CZ s.r.o." | seed `cleansia-cz` — [insert_seed_data.sql:1158](../../sql-scripts/insert_seed_data.sql#L1158), [insert_seed_data.sql:63-64](../../sql-scripts/insert_seed_data.sql#L63-L64) | název společnosti (dvě znění) |
| ceny v CZK „placeholder"; `NoShowCredit` mimo CZK „DEV placeholders" | seed — [insert_seed_data.sql:741](../../sql-scripts/insert_seed_data.sql#L741), [insert_seed_data.sql:483](../../sql-scripts/insert_seed_data.sql#L483) | ceník do smluv; výši kreditu mimo CZK |
| jediný trh `CZE`; SVK … USA bez operátora a pojištění | seed — [insert_seed_data.sql:1017-1104](../../sql-scripts/insert_seed_data.sql#L1017-L1104) | seznam trhů; velikosti bytu pro SVK |
| Stripe Price `price_1TSiJ83KjMqxM0RBVaiKAF6r`; promo WELCOME15 / SPRING20 / LOYAL10 „Phase B" | sandbox seed — [insert_seed_data.sql:1836-1837](../../sql-scripts/insert_seed_data.sql#L1836-L1837), [insert_seed_data.sql:1734-1735](../../sql-scripts/insert_seed_data.sql#L1734-L1735) | produkční ceník Plus; existující kampaně |
| veřejný kód 10 % a 30 dnů „placeholder awaiting an owner ruling" | [RequestPromoCode.cs:28-36](../../src/Cleansia.Core.AppServices/Features/PromoCodes/RequestPromoCode.cs#L28-L36) | slevu na první objednávku |
| sazba 0,5 × cena „junior template" jen CZK; požadavky na dokumenty jen CZE/SVK | seed — [insert_seed_data.sql:768-771](../../sql-scripts/insert_seed_data.sql#L768-L771), [insert_seed_data.sql:1134-1139](../../sql-scripts/insert_seed_data.sql#L1134-L1139) | výši odměny; seznam dokladů pro jiné trhy |
| DEV administrátor `admin@cleansia.local` / `Admin123!`; DEV Postgres heslo; DEV crony `0 */2 * * * *` | seed a `local.settings.json` — [insert_seed_data.sql:1891](../../sql-scripts/insert_seed_data.sql#L1891), [local.settings.json:7-11](../../src/Cleansia.Functions/local.settings.json#L7-L11) | cokoli; frekvenci úloh |
| `SET_VIA_USER_SECRETS`; `Sentry:Dsn ""`; `Fiscal:CzechEet2` prázdné; DEV URL; prod `googleClientId: ''` | web hosty — [appsettings.json:76-86](../../src/Cleansia.Web.Customer/appsettings.json#L76-L86), [environment.prod.ts:14](../../src/Cleansia.App/apps/cleansia.app/src/environments/environment.prod.ts#L14) | že klíče, monitoring, fiskalizace nebo přihlášení Google fungují |
| `support@cleansia.com`, `support@cleansia.cz`, `it@cleansia.cz`; `@anonymized.local`; reset hesla mobil na `partner.cleansia.cz` | seed a konfigurace — [insert_seed_data.sql:1199](../../sql-scripts/insert_seed_data.sql#L1199), [appsettings.Production.json:30-31](../../src/Cleansia.Web.Mobile.Customer/appsettings.Production.json#L30-L31) | adresu podpory (tři různé); identitu osoby; URL v textech |

