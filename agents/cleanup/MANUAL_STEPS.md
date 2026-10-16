# Manual steps — owner-only

Steps this cleanup needs that Claude does not run (see `CLAUDE.md` § *Manual Steps*). One row per
step, cleared when done.

## Open

### MS-13 — Send one of each migrated e-mail before the next deploy — **owner**

T-0677 moved all six live e-mails off hosted SendGrid templates onto
`email-templates/` in this repository. The folder holds **ten templates now**: the original six, the
promo-code e-mail, the two company wind-down notices (T-0762) and the administrator notification
(T-0774). Tests assert on the HTML the SDK actually serializes, so the bodies are proven; what is
**not** proven is what a real mail client does with them, and that is the half a test cannot reach.

**Action:** with a live SendGrid key, trigger one of each and open them — ideally in Gmail and
Outlook, which are the two that rewrite CSS:

1. registration → confirmation e-mail
2. forgot password → reset e-mail
3. an order → receipt (check the PDF is attached)
4. an order status change → status update (check the status badge has its colour — it is driven by
   `{{StatusClass}}` written into a class attribute)
5. close a pay period → period-closed (check the invoice PDF)
6. a period near its end → reminder (check the countdown reads "N days remaining")
7. request a promo code → promo-code e-mail
8. wind down the company from a date → the customer notice **and** the cleaner notice (one each)
9. file a dispute as a customer → the administrator notification (`admin-notification.html`; the body
   is one sentence with the order number substituted — check the substitution and the sign-in hint
   line — and set `notifications.admin_email` on Company settings to see the single-mailbox, English
   variant)

**Why it matters more than usual here.** These bodies were previously rendered by SendGrid, which
inlines CSS on the way out. We now send the HTML as written, so anything relying on that inlining
would look different. The templates use inline-friendly CSS in a `<style>` block, which Gmail
tolerates, but this is exactly the class of thing worth seeing once.

**If one looks wrong:** the hosted templates were not deleted from the SendGrid account, so
reverting the T-0677 commit restores the previous behaviour exactly. Nothing about this is one-way.

### MS-15 — Regenerate the customer client for the quote's crew and duration — **DISCHARGED 2026-09-16 (found in the tree by the sweep: `estimatedDurationMinutes` is on the generated client and the quick quote renders it; the crew count came off on an owner ruling)**

`QuoteOrder.Response` now carries `EstimatedDurationMinutes` and `RequiredEmployees`, computed from
`OrderDuration.EstimateMinutes` and `ceil(minutes / OrderDuration.MinutesPerEmployee)` — the same two
definitions the order itself uses, so a quote cannot promise a crew the booking will not send.

The home-page calculator states them under the price in the approved artboard ("2 uklízeči · odhad
4 hodiny"). It cannot until the generated client carries the fields.

**Action:** `npm run generate-customer-client` from `src/Cleansia.App/`, with the customer host
running. Then `QuickQuoteFacade` gains a `crewNote` computed from the two fields and the template
renders it under the price — the facade already carries the comment saying so.

Until then the price shows without that line, which is the honest state: the numbers exist on the
server and not yet on the client.

**Note on the backend build:** `dotnet build` on the full solution fails while the five API hosts are
running — they hold the abstraction DLLs open. `Cleansia.Core.Domain` and `Cleansia.Core.AppServices`
both build clean on their own; the backend test suites need the hosts stopped.

### MS-2 — Drop the DEV database with the next deploy — **deferred by decision**

> **Owner, 2026-08-14:** *"I'll drop the db and reseed the data after all of the Phases are done."*
> Deferred deliberately — not overlooked. It stays open until the drop happens.


`MS-1` regenerated the single `Initial` migration, `MS-6` regenerated it again, and the partner
document-lifecycle work regenerated it a third time for the two new tables — so its id has moved from
`20260811192214` to `20260813085249` to `20260815094107` to `20260825114012` to `20260830221715`
(owner, 2026-08-31, for the `PropertySizePresets` table), then through the multicurrency and market
programme (`20260913080510`, T-0721) to `20260913132759` (T-0722 / `5a5f4681`, 2026-09-13 — the
tenancy activation: `Tenants`, `CountryConfigurations.OperatorTenantId`, `TenantId` NOT NULL on every
stamped table, `IX_Users_Email` global, `IX_OrderReceipts_TenantId_ReceiptNumber`,
`IX_EmployeePayConfigs_Tenant_Scope`, `IX_LoyaltyTierConfigs_Tier`), then through the customer audit
log (T-0730: `CustomerActionAudits` + `UserConsents.DocumentVersion`; T-0733: the
`IX_EmployeeActionAudits_OrderId_CreatedOn` index) to `20260913174821`, then through the 2026-09-14
follow-up batch (T-0742: `LegalDocuments`, `LegalDocumentTexts`, `UserConsents.LegalDocumentId`;
T-0738: `Disputes.TextRetainedUntil` + `IX_Disputes_TextRetainedUntil`) to `20260914115922`, then
through the 2026-09-15 tenancy batch (T-0758: `TenantId` off the 21 tenantless tables with their
`IX_<T>_TenantId`, `FK_<T>_Tenants_TenantId` Restrict on all 48 stamped tables; T-0757:
`PayoutReferenceCounters (TenantId, Year, Scope)`, `IX_EmployeeInvoices_TenantId_InvoiceNumber`,
`IX_EmployeeInvoices_TenantId_VariableSymbol`) to `20260915172310`, then through the 2026-09-16
company-lifecycle batch (T-0760: `Tenants` becomes `Auditable` and gains the nine lifecycle columns —
`WindDownFrom`, `WindDownRequestedOn/By`, `WindDownRunStartedOn`, `WindDownLastRunOn`,
`ArchiveRequestedOn/By`, `ArchivedOn`, `ArchiveManifestSha256`) to **`20260915232921`** — still 87
tables. Each regeneration was proven against a real Postgres by the integration suite; the tenancy ones
also by `SeededDatabaseHasNoOrphanTenantRowsTests`, which applies the seed to the migration-built
database, and the last two by `InitialMigrationTenantDdlTests`, which reads the migration's own
operations. Guest cancellation then regenerated `Initial` as **`20260916102615`** on 2026-09-16:
`Order.CurrentStatus` becomes an EF concurrency token to reject stale status writes. There is no new
SQL column and the create operations remain unchanged; all 501 PostgreSQL integration tests passed
with this metadata change. Recurring pause notification state regenerated Initial again as
**`20260916120038`**: `UserMemberships` gains `PaidPeriodConfirmedAt`,
`RecurringPauseStateObservedAt`, `RecurringPauseNotificationSentAt` and
`RecurringPauseNotificationSequence`, with PostgreSQL `xmin` concurrency metadata. The table count
remains 87; verified on Postgres after the hand-over (backend 5880 / 515 / 318, CI 7/7 on 881d9cc1).
The four administrator roles (T-0748, `56fa5aa2`, 2026-09-19 — ADR-0066) regenerated `Initial` once more as
**`20260919142517`**: `Users.AdminRole` (nullable int) with the check constraint
`CK_Users_AdminRole_Profile` (`("Profile" = 100) = ("AdminRole" IS NOT NULL)`) and
`AdminActionAudits.ActorAdminRole` (nullable int); the body diff against `20260916120038` is exactly those
three operations and the table count is still 87. The seed inserts the administrator with `AdminRole = 1`
and `sql-scripts/set-admin-role.sql` sets both columns. Unit suite 6174 green at the lane; the Postgres
integration suite (which carries the constraint test and the two lock-race tests) and the host suite
compiled but first execute in CI — Docker was down on the box.
The contract for work (T-0777, `a15d3af0`, 2026-09-20 — ADR-0068) regenerated `Initial` once more as
**`20260919231739`**: the `WorkContractAcceptances` table (twelve columns + the audit stamps,
`IX_WorkContractAcceptances_OrderEmployeeId` **unique**, `(OrderId, EmployeeId)`, `(EmployeeId,
AcceptedOn DESC)`, `(TenantId, AcceptedOn)`, FKs `Orders`, `LegalDocumentTexts` and `Tenants`, all
Restrict) and `Orders.WorkContractDocumentId` (nullable, FK `LegalDocuments` Restrict, indexed); the
table count is now **88** and 49 tables carry the `Tenants` FK. The Postgres integration suite
(`TakeOrderWorkContractTests` on the seat-race fixture, `WorkContractAcceptanceRetentionTests`,
`CompanyArchiveBundleTests`) and the host suite (`WorkContractRouteTests`) first execute in CI — Docker
was down on the box again.
The UI-polish removals (T-0791, `38312c8d`, 2026-09-20, branch `chore/ui-polish-and-dead-code` stacked on PR #255)
regenerated `Initial` once more as **`20260920204705`**: `Employees.Availability` dropped, `Employees.PreferredCurrencyCode`
dropped, the three `Cart*` tables and `EmailTranslations` dropped, `MembershipPlans.TrialPeriodDays` dropped (the
owner's Q-UI-01/02 rulings); the table count is now **84** and 48 carry the `Tenants` FK. The integration suite ran
in full locally (Docker up) and green.
The guest access re-key (phase 1 of `agents/FIX-PLAN-2026-09-22.md`, branch `fix/audit-findings-2026-09-22`,
2026-09-22 — finding F69) regenerated `Initial` once more as **`20260922153301`**: one new table,
`GuestOrderAccessTokens` (`OrderId`, `TokenHash`, `ExpiresOn`, `RevokedOn` + the audit stamps and
`TenantId` NOT NULL), with `IX_GuestOrderAccessTokens_TokenHash` **unique**,
`IX_GuestOrderAccessTokens_OrderId_RevokedOn`, `IX_GuestOrderAccessTokens_TenantId` and FKs `Orders`
(Cascade) and `Tenants` (Restrict). The body diff against `20260920204705` is exactly that table, its
two FKs and its three indexes and nothing else; the table count is now **85** and **49** carry the
`Tenants` FK. All three backend suites ran locally and green (6278 / 559 / 347), the integration one
against a real Postgres built from this migration.

The guest chargeback (item 2.3 of phase 2 of `agents/FIX-PLAN-2026-09-22.md`, same branch,
2026-09-22 — finding F71) regenerated `Initial` once more as **`20260922182416`**: `Disputes.UserId`
becomes **nullable** (the FK into `Users` and `IX_Disputes_UserId` both stay). A bank chargeback on a
guest booking has no account to name; the webhook used to substitute the empty string, Postgres raised
`23503`, and Stripe retried a 500 forever while the dispute went unrecorded. The body diff against
`20260922153301` is exactly that one column's nullability — **85 tables, 1339 columns, 169 foreign
keys and 280 indexes are identical**, and **49** still carry the `Tenants` FK.

The receipts (phase 3 of `agents/FIX-PLAN-2026-09-22.md`, same branch, 2026-09-22 — finding F77)
regenerated `Initial` once more as **`20260922220828`**, adding two columns to `Orders`:

- `ExpressSurchargeAmount` (item 3.1), `numeric(18,2)` NOT NULL with a database default of `0` — the
  same shape `CreditAppliedAmount` carries, for the same reason (the integration suite's raw-SQL order
  inserts name their columns explicitly). A receipt's lines are raw catalogue prices and its stored
  discounts are already measured against the charged price, so the surcharge is the term between them.
  It is stored rather than derived as `Total − lines + discounts` because the derivation would print
  any gap — a line a loader missed, a rounding cent — as a surcharge the customer never paid.
- `LanguageCode` (item 3.3), `character varying(5)` NULL — the language the booking request was made
  in. The receipt is written in it before the account's stored preference; null on a recurring
  occurrence, which has no request of its own.

The body diff against `20260922182416` is exactly those two columns (Designer: the migration id and
those two properties) — **85 tables** and **49** carrying the `Tenants` FK, unchanged; `dotnet ef
migrations has-pending-model-changes` reports none. An intermediate regeneration in the same phase
(`20260922200233`, surcharge only) was never committed.

Phase 4 (2026-09-23) regenerates `Initial` as **`20260923064534`**. Its only schema delta is
`Orders.CustomerAddressId` changing from cascading deletion to restrictive deletion. Erasure and
retention now move affected records to blank address copies before deleting an unshared original;
if another booking attaches concurrently, the FK refuses deletion instead of deleting that booking.
The phase-5 loyalty perks arrive with the reseed that follows the drop (below) — the drop alone seeds nothing.

Phase 6A (2026-09-23) regenerates `Initial` as **`20260923071814`**, removing the unread
`CountryConfigurations.ReducedVatRate` column while retaining both live VAT rates.

The 2026-09-27 meeting plan and the fixes after it regenerated it twenty-four times more, each one adding
what is named against the id before it:

- **`20260927231818`** (2026-09-28, `5d7d7b68a`, the chargeback and dispute-amount fixes):
  `Disputes.CardRefundedAmount` and `Disputes.CreditReturnedAmount`, `numeric(18,2)` NULL.
- **`20260928185130`** (2026-09-29, phase 1, `3f8446178` + `70bf76e28`): the
  `CleanerLegalDocumentAcceptances` table (FKs `LegalDocumentTexts` and `Tenants`, four indexes);
  `Orders.CustomerEmail` becomes `citext` with `IX_Orders_CustomerEmail`; `Orders.CashCollectedAmount`
  and `CustomerConfirmedAt`; `Disputes.SettlementPreference`; `Employees.WeeklyOrderLimitReason`;
  `OrderEmployeePays.DeductionDisputeId` and `DeductionReason`; `OrderReceipts.BlobDeletedAt`. **86**
  tables.
- Phase 2, the dirtiness level (2026-09-29): **`20260929022837`** (`Orders.DirtinessLevel`,
  `DirtinessSurchargeAmount`), **`20260929031611`** (`Services.MinutesPerRoom`), **`20260929035817`**
  (`RecurringBookingTemplates.DirtinessLevel`), **`20260929042152`** (`OrderEmployeePays.DirtinessPay`).
- Phase 3, the card guarantee and receivables (2026-09-29): **`20260929070817`** (`SavedCards`),
  **`20260929082511`** (`Receivables`), **`20260929094036`** (`Receivables.StripePaymentIntentId` and
  `PaidOn`; `OrderReceipts.ReceivableId` with its FK and index), **`20260929102644`**
  (`Orders.LockoutReportedAt`, `LockoutReportedByEmployeeId`, `LockoutCallAttempts`),
  **`20260929105442`** (`Receivables.PayLinkSessionId`), **`20260929111628`**
  (`OrderEmployeePays.LineType`, default 0), **`20260929114448`** (`CashLedgerEntries`),
  **`20260929124018`** (`CashLedgerEntries.RemittanceRequestedAt`, `EmployeeInvoices.CashSetOffAmount`).
  **89** tables, **53** carrying the `Tenants` FK.
- **`20260929175413`** (2026-09-29, `00dd84f6f`, P4-D61): the five `EarlyPerformanceConsent*` columns on
  `Orders` and on `RecurringBookingTemplates`.
- **`20260929222804`** (2026-09-30, `07f165f17`, L-E1a): `Users.MustChangePassword`.
- **`20261001174041`** (2026-10-01, `74a854078`, P1-BE-02): `EmployeeDocuments.ContentSha256`, NOT NULL.
- **`20261003084437`** (2026-10-03, `59a87b219`): `Orders.DirtinessRate`, `numeric(5,4)` NOT NULL,
  default 0.
- **`20261003132430`** (2026-10-03, `4d02da9df`, branch `fix/settlement-snapshots-2026-10-03`): `Orders`
  gains `CancellationFreeHours`, `CancellationPartialHours`, `CancellationPartialFeeRate`,
  `CancellationLastMinuteFeeRate` (`numeric(5,4)`) and `CancellationPlusFreeHours`, all NOT NULL with
  **no** default — an order created without its booked terms must fail, not default to a free
  cancellation; `OrderEmployees` gains `JobBasePay`, `JobExtrasPay`, `JobMinPay` and `JobMaxPay`,
  `numeric(18,2)` NULL; `MembershipPlans.FreeCancellationWindowHours` is dropped. Still **89** tables
  and **53** with the `Tenants` FK; `has-pending-model-changes` reports none, and the three backend
  suites ran green at it (7564 / 739 / 417).
- **`20261003220403`** (2026-10-03, `fa24756ea`, branch `fix/settlement-followups-2026-10-03`):
  `LoyaltyAccounts` maps the Postgres `xmin` system column as a concurrency token (`5064b2bdd`, owner
  decision 5 — two loyalty writes for one customer must not lose an update). A system column, so
  Npgsql emits no DDL; the delta against `20261003132430` is that one property in the Designer and
  the snapshot. Still **89** tables and **53** with the `Tenants` FK; `has-pending-model-changes`
  reports none, and the three backend suites ran green at it (7604 / 742 / 417).
- **`20261005010454`** (2026-10-05, branch `feature/meeting-2026-10-04-batch2`, the referral reward as
  credit): `Currencies.ReferralCredit` `numeric(18,2)` NULL; `Referrals` drops `PointsAwardedToReferrer`,
  `PointsAwardedToReferred` and `PointsAwardedOn` and gains `CreditAwardedToReferrer` and
  `CreditAwardedToReferred` (`numeric(18,2)` NULL), `CreditCurrencyId` (FK `Currencies`, Restrict, with
  its index) and `AwardedOn`. Still **89** tables and **53** with the `Tenants` FK;
  `has-pending-model-changes` reports none, and the three backend suites ran green at it
  (7786 / 766 / 418).
- **`20261005082345`** (2026-10-05, branch `fix/meeting-2026-10-05-followups`, each referral side paid in
  its own currency, and the same-person hold): `Referrals` drops `CreditCurrencyId` and its index and gains
  `ReferrerCreditCurrencyId` and `ReferredCreditCurrencyId` (`varchar(26)` NULL, each an FK into
  `Currencies`, Restrict, with its index) and `HoldReasons` (`varchar(32)` NULL), and maps the Postgres
  `xmin` system column as a concurrency token (no DDL; the Designer and the snapshot carry it). Still
  **89** tables and **53** with the `Tenants` FK; `has-pending-model-changes` reports none, and the three
  backend suites ran green at it (8025 / 789 / 428).
- **`20261005201452`** (2026-10-05, branch `fix/findings-2026-10-06`, a cleaner's save and an approval
  that race): `Employees.RegistrationNumber` and `Employees.ContractStatus` become per-property concurrency
  tokens, the `Orders.CurrentStatus` idiom. No DDL: the body is byte-identical to `20261005082345`, and the
  Designer and the snapshot gain the two `IsConcurrencyToken()` lines. Still **89** tables and **53** with
  the `Tenants` FK; `has-pending-model-changes` reports none, and the three backend suites ran green at it
  (8161 / 803 / 428).
- **`20261012190449`** (2026-10-12, branch `fix/post-e-p5-backend-sweep-devices`, ADR-0071 — a payment that
  lands while its order is being cancelled): `Orders.PaymentStatus` becomes the order's second concurrency token
  beside `CurrentStatus`. No DDL: the body is byte-identical to `20261005201452` (sha256 `e4e3866c…f5cc` for both),
  and the Designer and the snapshot each gain the one `IsConcurrencyToken()` line. Still **89** tables and **53**
  with the `Tenants` FK. `has-pending-model-changes` reports none, and reported none before the regeneration too: a
  token on an ordinary column is invisible to it, so the snapshot diff is the check for a change like this one. The
  three backend suites ran green at it (8685 / 860 / 503).
- **`20261013160545`** (2026-10-13, branch `fix/next-wave-p2-backend`, T-0834 follow-up M11): the
  nonunique `Devices(DeviceId, DeviceToken)` index supports the shared-install withdrawal read.
  It is the only migration-body delta from `20261012190449`; tables and tenant foreign keys are
  unchanged. Ran `migrations remove --force`, `migrations add Initial` and
  `migrations has-pending-model-changes` from `src/`, with `Cleansia.Web.Partner` as startup project.
  The model has no pending changes. Command receipts and the normalized schema diff are under
  `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/p2/migration/`.
  Full combined suite evidence is recorded with the backend PR's Work records; no shared DEV reset
  or deploy ran during this regeneration.

**The one owed drop belongs to `20261013160545`** (it renews the one owed for `20261012190449`).
Mike approved exactly the end-of-wave reset and fresh DEV deployment on 2026-10-13:
"You can reset db, but once we're done with everything and we can do a fresh deployment".
It runs after every program PR has merged green, immediately before the fresh deployment from final
master. A shared DEV reset needs explicit approval (`CLAUDE.md`), and code approval alone does not
provide it: a DEV database whose `__EFMigrationsHistory` records any earlier id replays the
whole create script against tables that already exist. The legal texts need no extra step — every host seeds
them at start, and since `b34dff07` a fresh Development database is seeded once more in the boot that
migrates it (the factory now refuses a booking with no contract text in force, so a first boot must not come
up without one).
**Seven** document types are seeded now, in five languages each: for customers the terms of service, the
privacy policy and the complaints procedure; for cleaners the contract for work, the framework
cooperation agreement, the self-billing agreement and the data processing agreement. Every dated version
under `Seed/Legal` comes in with them, so after the drop the seeder also brings in the **2026-10-05**
terms of service, framework agreement, self-billing agreement and contract for work, in force from that
day, the **2026-10-06** terms of service and privacy policy, which carry the per-side referral
currency, the same-person hold and its comparison and are in force from that day, and the **2026-10-07**
terms of service (`d2fbe8363`), which carry the owner's rulings of 2026-10-06 — the cash cap counted as
upcoming bookings, an unpaid door price as an amount owed, and no new booking while anything is owed —
and are in force from that day. The hold and the comparison process data for a purpose only the
2026-10-06 privacy policy discloses, and the booking ban and the door report (`4613974f7`, `8365129a6`)
act on rules only the 2026-10-07 terms state, while the 2026-10-06 terms promise that card bookings stay
open, so this branch's code must not reach **production** before **2026-10-07** (DEV carries test data
only). The integration and host seed counts are 21 documents and 105 texts.

Regenerating is no longer a manual step of any kind (owner ruling 2026-08-25): it is ordinary work and
is done in the branch that needs it. **The drop remains deferred until deployment, never branch
work**, and every regeneration renews it rather than adding a new obligation. It is routine DEV
deployment work; all production operations remain prohibited. `MigrationService/Program.cs` runs `MigrateAsync()` on every deploy, and a database
whose `__EFMigrationsHistory` records the **old** id will try to replay the whole create script against
tables that already exist — failing the `migrate-database` job every other deploy job depends on.

**Action:** drop the DEV database, deploy, then **run the seed yourself** — a deployed host runs as
`Production` (no `ASPNETCORE_ENVIRONMENT` is set anywhere; `deploy/bicep/main.bicep`), the seeder runs only
under `IsDevelopment`, and the deploy applies the migration bundle only. Run
`gh workflow run execute-sql.yml -f environment=DEV -f script_path=prod-bootstrap.sql`, then the same with
`script_path=insert_seed_data.sql` (DEV only — the workflow refuses the fixture against PRO; the fixture
needs the bootstrap first). Pre-production, so there is no data to preserve; the seed repopulates it
(`sql-scripts/prod-bootstrap.sql` inserts the `Tenants` row first, and `insert_seed_data.sql`
stamps every seeded business row `cleansia-cz`; a database seeded by any earlier script has `NULL`
tenants that the NOT NULL columns would refuse, which is one more reason the drop is not optional).

**One script to run by hand alongside it:** `sql-scripts/seed/insert_email_template_translations_promo_code.sql`.
Nothing under `seed/` is run by a workflow — the auto-seed reads only the root
`prod-bootstrap.sql` and `insert_seed_data.sql` (`CleansiaStartupBase.DevelopmentSeedScripts`). Skip it and the promo e-mail still sends, but
with every copy slot blank: the renderer strips placeholders it has no value for, so the failure is a
near-empty e-mail rather than an error anyone sees. The six older e-mails have the same shape via
`seed/insert_email_translations.sql`, so this is the existing convention, not a new one.

This obligation was recorded only inside `MS-1`'s **Cleared** row, where a reader looking at *"what do I
owe?"* would not find it. That is what `CL-043` is.

#### 2026-10-15 — actual reset/migration complete; END blocked, reseed still owed

This dated partial execution supersedes the earlier wholly deferred execution checkpoint without clearing MS-2 or rewriting its historical migration chronology/owner quotes. DEV reset `38660229404` / attempt 1 reported public tables 0; full deploy `38660355359` applied the sole `20261013160545_Initial` (Done `00:44:42.6012853Z`). The three initial web API failures recovered and their three retried jobs passed in the sole failed-jobs rerun, but attempt 2 ended with both customer SSR origins exhausting 30 zero-byte HTTP000/curl28 timeouts each. Later root/process diagnostics also timed out; SSR cause is unestablished and no further mutation or seed-first exception is admitted.

All four seeds remain **unrun**: bootstrap, fixture data, published-admin INSERT and promo translations. Published admin was **not recreated**; company/settings/plans/catalog fixtures were not seeded. Final post-seed DB health and five-API/two-origin readiness were not performed. The missing runtime/worker evidence is recorded in [Q-END-01](../backlog/questions/open.md). Keep MS-2 in this open section, not Cleared. DEV accounts/order/upload rows/push registrations were reset, so testers must wait for completion before signup; SWA roles and storage blobs/queues persist. No PRO operation or graphify rebuild ran.

Shared END actuals and qualified evidence pins: `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/end/final-records-preparation002/end-outcome-facts.json` (SHA256 `8c49656e9f9dfecd936cf0386846fb958cbb64d0eb55f6dc8b67e965de0c3695`).

#### 2026-10-15 — PR #338 merged; startup rollout failed, END remains open

The preceding blocked/all-seeds-unrun subsection is retained as a historical checkpoint. Q-END-01 was subsequently answered; original deploy `38660355359` recovered on Warm-only attempt 3 (14 carried executions, two fresh successes). Bootstrap `38702440461`, fixture seed `38702856219` and published admin `38703413453` then completed. They must not be replayed. The published admin retains `MustChangePassword=false`; no authenticated console exercise is claimed. Promo `38703639106` failed on SQLSTATE 42804 and still awaits its failed-script-only retry after the corrected source can be successfully deployed.

Startup PR [#338](https://github.com/VM-s-Solutions/cleansia/pull/338) merged `c54d3d64e777d5042572231b1ce2db0990e67306` from reviewed head `3a480403e7e48a8d6f8369b5a343f32211a877aa`, retaining tree `12206772eab4070624c197d5bd9af2a0f56facba`. Exact-head PR CI passed six jobs/five workflows; merge-head master CI passed four executed jobs with one Frontend e2e push skip. All were attempt 1/no reruns; PR/master warning annotations were 11/10 with zero failure annotations.

The resulting-master startup DEV rollout `38766841247` / attempt 1 failed (nine successful jobs, six failures, one skip; 18 warning/12 failure annotations). All six SCM package outcomes remain **UNVERIFIED**, with no verified final package JSON or structured activation timings. Warm dev sites was skipped; the corrected skipped-step classification is 18 PRO-only plus five DEV API warm steps. Nine CRC/digest-checked archives were preserved (358,521,552 bytes; six artifact-ID consumer bindings, three qualified by name). The bundle reported no migrations applied/database already up to date, not a fresh DB-history query. The stopped 924-sample observer recorded 588 healthy responses/336 five-second timeouts; its first six-endpoint round was healthy and its last timed out. These are sampled observations, not final seeded health or continuous/worker/cold-start proof.

The failed-rollout actual-evidence verdict is approved with these limits (`startup-2026-10-15/dev-startup-actuals-review001/failed-deploy-actual-review001.json`, SHA256 `e89f3443f8cb6e0bcc88dd4c687af89f922db99d363da5d293872193cb35d458`; seal `47b88c757bf2578cc265ffeb9af5813742c2fdcb3da66921e8d362bc3bd230ad`). [T-0636](../backlog/tickets/T-0636-deployment-startup.md) records the full capture/skip qualification and separate local diagnostic ambiguity; no Azure authentication or startup cause is established. Transport follow-up is being prepared separately, with no corrected deployment or new PR outcome claimed. No promo retry/final DB/HTTP acceptance followed this failed dependency. **MS-2 remains in Open under its existing heading**; no completion, second reset, repeated successful seed, further docs deployment, storage emptying, PRO operation or graphify rebuild is recorded.

#### 2026-10-15 — PR #339 merged; read-only inspection incomplete, END stays open

The prior transport-publication-pending statements are dated checkpoints. [PR #339](https://github.com/VM-s-Solutions/cleansia/pull/339) merged `8403c48c63dceae066f09b64a0ff63dfd0ec183c` from reviewed head `476d0ac6f16f66e517bcd3b9022f80d8834eeae8`, retaining tree `d99a83ef61bdb2cb4f288a34f4d6204b24a2cbb3`. Exact-head PR CI passed six jobs/five workflows; exact-merge master CI passed four jobs (three repository push workflows plus one automatic Dependabot dynamic run). All were attempt 1/no reruns. PR retained 11 warning annotations and one verified Playwright archive; master retained 10 warnings plus one Dependabot Ubuntu-label notice, zero artifacts and no Frontend run/e2e skip. Both retained zero failure annotations. The approved final local seal records 8,746 source inputs unchanged and 17 actual commands: 16 successes plus one preserved Docs harness failure; prior qualified full application acceptance remains applicable.

One GET-only inspection dispatch initially had an unresolved identity; without reposting, GET reconciliation identified `38797931482` / attempt 1 on that master. All six jobs failed with partial JSON; six ZIPs/JSON reports were retained with digest/CRC checks. The comparator blocks all six apps as `BLOCKED_PENDING_OR_UNVERIFIED`: history/active archive/pointer stability remain unverified and no upload is justified. A minimal diagnostic source follow-up is required before another state assessment; no Azure cause, upload retry, recovery or final readiness is established. [T-0636](../backlog/tickets/T-0636-deployment-startup.md) records the actual request outcomes, local/CI qualifications and immutable evidence pins.

**MS-2 remains Open under its existing heading**; applicable END criteria and the four INDEX dispositions remain unchanged. Original reset/docs/deployment attempts and successful bootstrap `38702440461`, fixtures `38702856219` and admin `38703413453` remain history; successful seeds are not replayed. Failed promo `38703639106` still awaits its sole corrected-script retry after the runtime dependency is resolved; final DB health and 13 HTTP observations are unperformed. No second reset, docs redispatch, storage cleanup, PRO or graphify action is added. `MustChangePassword=false`/no authenticated console exercise and the existing provider-warning/no-muting-proof limits remain retained.

#### 2026-10-15 UTC — PR #340 merged; package archives matched, runtime identity unresolved

[PR #340](https://github.com/VM-s-Solutions/cleansia/pull/340) merged `70878a3545da4a6fbc8b541a20916d7d53c86be7` from reviewed H `b6335299314ffc4111ca5c4fda25f35e8c4ebfce`, retaining tree `cf4dcb96901186f269eeeafcdd2b0a6989b896dc`. PR CI passed six jobs/five workflows; master passed three jobs/three repository push workflows, all attempt 1/no reruns. Final local binding retained nine actual commands (seven successes, one consistency non-run, one preserved interrupted Python attempt), the later full 59-test pass and qualified prior application results on 8,746 inputs.

Read-only inspection `38814387736` supported one scoped recovery. Recovery `38815343537` / attempt 1 failed with nine successful jobs, six Location-guard package failures and one warm skip; no final verified SCM JSON or raw upload status/header value was retained. Its nine archive copies and stopped observer remain qualified evidence, not readiness. The single subsequent diagnostic dispatch was GET-reconciled to `38818409957` / attempt 1: all six collection jobs succeeded, the exact new UUIDs were terminal and all six selected archive SHA256/byte counts matched the new intents. **All six SCM `wwwroot` entry hashes still differed from the new entries and equalled older SCM hashes; active worker/mount identity and readiness remain unresolved.** The failed Actions recovery remains failed. [T-0636](../backlog/tickets/T-0636-deployment-startup.md) carries exact evidence/pins and the source-inferred HTTP 202 qualification. These recovery/reconciliation actuals occurred on 2026-10-16 in Europe/Prague; this subsection uses UTC.

**MS-2 remains Open under its existing heading**; all applicable criteria and four INDEX dispositions stay unchanged. Original reset/docs/deployment failures and successful bootstrap `38702440461`, fixtures `38702856219` and admin `38703413453` are retained; they are not replayed. Failed promo `38703639106` still awaits its corrected-script-only retry after runtime acceptance; final DB health/13 HTTP acceptance and final closure remain outstanding. No further upload, reset, docs redispatch, storage cleanup, PRO or graphify action is added. The earlier `MustChangePassword=false`, no authenticated console exercise and unverified alert-muting qualifications remain in force.

#### 2026-10-15 UTC — Linux package source prepared; promo-only retry completed

This checkpoint is 2026-10-16 in Europe/Prague. All preceding MS-2 history, owner quotations and then-pending statements remain retained. The later semantics/security assessments accept exact selected-package archive identity with separate public readiness as the bounded Linux gate; SCM VFS `wwwroot` is not an authoritative executing-worker digest. All six actual SCM entry mismatches remain mismatches, and recovery `38815343537` remains failed. Six later public health requests at 22:46:07–22:46:11 UTC returned HTTP 200/healthy with zero retries; these are sampled health observations, not real SSR rendering, final 13-HTTP acceptance, exact worker bytes, cold startup or continuous uptime.

The sole previously failed promo-script retry `38820436701` / attempt 1 completed on master `70878a3545da4a6fbc8b541a20916d7d53c86be7`, with two successful jobs/one PRO skip and zero annotations/artifacts. DEV job `116715511107` emitted `INSERT 0 60` at `22:56:57.3172060Z` and script success at `22:56:57.3192162Z`. This confirms successful completion of one atomic INSERT, with 60 affected rows; no exact table total or literal BEGIN/COMMIT receipt is asserted. The original promo failure remains history. The original successful bootstrap `38702440461`, fixtures `38702856219` and admin `38703413453`, completed reset and original docs deployment were not replayed.

The first PR #340 closure is local/unpushed C3 `f5e04cbb210afd7b9053ffb34840b98ff4bf3112`; the reviewed helper/test/infrastructure source afterimages are locally applied and uncommitted. Focused regressions/source reviews are recorded, with the full 66-test suite still pending. [T-0636](../backlog/tickets/T-0636-deployment-startup.md#linux-selected-package-verification-source-checkpoint--2026-10-15-utc) carries source/semantics/security/public/promo pins and the stopped-attempt qualifications. Full applicable local/docs/head gates, source commit/binding/publication/CI, resulting-master DEV deployment, fresh final DB health/13 HTTP observations and final closure remain pending. **MS-2 remains Open under its existing heading**; all applicable criteria and four INDEX dispositions remain unchanged. Earlier `MustChangePassword=false`, no authenticated console exercise and unverified alert-muting qualifications remain. No second reset, successful-seed replay, docs redispatch, storage/plan/slot/cost operation, PRO or graphify action is added.

#### 2026-10-16 UTC — PR #341 merged; selected archives reconciled, rollout/readiness still failed

The preceding source/head/CI-pending subsections remain dated history. [PR #341](https://github.com/VM-s-Solutions/cleansia/pull/341) merged `2b83eff633a195c35b4fac332839ff47942638c1` from C4 `4b42cfb4bd543f08b5fa4aa7f782b8226eb594ad`, preserving tree `d8855b6ff21815d605435bed30e2a3d99e5a71a7` and parents prior master `70878a3545da4a6fbc8b541a20916d7d53c86be7` then C4. Exact-head PR six jobs/five workflows and resulting-master three push jobs/three workflows passed on attempt 1/zero reruns; source66/local docs/head and qualified application carry are complete as recorded in [T-0636](../backlog/tickets/T-0636-deployment-startup.md#pr-341-closure-selected-packages-verified-rolloutreadiness-still-failed--2026-10-16-utc). Its warning/cache/fixture/reuse/skip qualifications remain.

Resulting-master DEV `38830931038` / attempt 1 failed: 14 successful jobs, Customer Mobile deployment-list GET TimeoutError 60.519 seconds after its single acknowledged upload/no final JSON, and global warm skip. Nine fresh archive/producer/consumer lineages and six new upload intents/five final selected-package JSONs are verified. The closed observer retained 672 samples: 533 healthy, 139 timeouts (three before TCP completion, 136 after TLS/no first byte), last round 0/6. No real SSR/final 13-HTTP or final DB acceptance followed. Later read-only singleton `38833690130` / attempt 1 passed six collection jobs; the independent comparison verifies all six own UUIDs/terminal states/stable pointers and selected ZIP SHA256 **and bytes**, including Customer Mobile's original acknowledged upload. All six SCM backing-namespace entry mismatches remain; neither collection nor selected-package proof establishes worker bytes/public readiness or changes the failed Actions outcome. Another upload is not justified merely to obtain green history. No confirmed causal source fix exists; bounded hosted-runtime/plan-metrics evidence is the prepared next follow-up.

**MS-2 remains Open under its existing heading**; all applicable criteria/checklists and the four INDEX `in_progress` dispositions stay unchanged. Promo `38820436701` remains completed `INSERT 0 60`/script success, affected rows without literal COMMIT/table-total proof, and is not replayed. The original successful reset/docs/bootstrap `38702440461`/fixtures `38702856219`/admin `38703413453` remain retained without replay. Fresh final DB health/13 anonymous HTTP observations and END closure remain unperformed. Earlier `MustChangePassword=false`, no authenticated console exercise, provider/muting and all historical failure/qualification statements remain in force. No PRO/storage/plan/slot/cost/Graphify action is added. T-0636 pins PR/master CI, failure `0cdcb8d4…`, inspection `6ee3262d…` and source/runtime investigation `cbdc4308…` with their full immutable digests.

#### 2026-10-16 UTC — PRELAUNCH bounded runtime evidence source checkpoint

The one-YAML diagnostic evidence candidate has scoped correctness/security and command-mock QA; [T-0636](../backlog/tickets/T-0636-deployment-startup.md#bounded-runtime-evidence-source-checkpoint--prelaunch-2026-10-16-utc) records its bounds and pins. At this prelaunch checkpoint full66/docs/head gates, fresh app-input carry proof, source binding/publication/CI and hosted execution remain pending. No metrics, cause, public readiness or final acceptance is inferred. **MS-2 remains Open**, with all prior history/quotes/checklists intact, including PR #341 closure. Successful reset/seeds/promo/docs are not replayed; final DB/13 HTTP acceptance remains unperformed.

### MS-9 — Invite yourself to the admin console — **DEV verified 2026-09-11**

The owner confirmed the DEV site's `/.auth/me` reports `aad` with `admin_console`, and the
deployed Cleansia login loads after Microsoft sign-in in a fresh private browser window.
The normal browser's script redirects did not occur in that fresh session. This confirms DEV
access only; each additional admin Static Web App requires its own invitation.

`apps/cleansia-admin.app/src/staticwebapp.config.json` now requires the role `admin_console` on
every route and redirects anonymous visitors (401) to `/.auth/login/aad`. Authenticated visitors
without the role receive 403; redirecting them back to sign-in would loop. See
[`docs/admin-app/overview.md`](../../docs/admin-app/overview.md#access-to-the-deployed-console).
Owner ruling 2026-08-27: docs + admin, not the two self-service funnels.

**For each additional admin site:** Portal → `swa-cleansia-admin-*` → *Role management* →
**Invite** yourself with the role `admin_console`.

**This is the step that makes the console reachable.** The built-in `aad` provider admits any
Microsoft identity, so the ROLE is the access control, not the login — an uninvited visitor with a
perfectly valid Microsoft account signs in and still gets nothing. Skip it and the console refuses
everyone, including the owner, until the invite exists.

`admin_console` is underscore-only on purpose. The SWA invitation form rejects hyphens — that is
what blocked `docs-reader` and is why the docs role is `docs_reader`. If the string here and the
string in the portal ever differ, the console is unreachable and correcting it costs a full DEV
deploy.

**Prod inherits this automatically** on the first prod deploy, because one config file ships to
both environments. Environment-specific gating is not expressible in this file and would need an
asset swap in `project.json`'s configurations block — not built, because nothing asks for it yet.

### MS-4 — Re-copy the docs SWA deployment token, and invite yourself — **DONE 2026-08-29**

`main.bicep` now declares the documentation Static Web App (`swa-cleansia-docs-weu-dev`) under
`if (env == 'dev')`. It was previously created by hand, so the **next DEV infrastructure deploy creates
a NEW resource** — and a Static Web App deployment token belongs to the resource, not to the name.

**Action, after the next DEV deploy:**

1. Portal → `swa-cleansia-docs-weu-dev` → *Manage deployment token* → copy.
2. Replace the repository secret `AZURE_STATIC_WEB_APPS_API_TOKEN_DOCS_DEV`.
3. Portal → same SWA → *Role management* → **Invite** yourself with the role `docs_reader`.

Step 3 is what makes the site reachable at all. `docs/public/staticwebapp.config.json` requires
`docs_reader` on every route and redirects 401/403 to `/.auth/login/aad`, so an uninvited visitor —
including one with a valid Microsoft account — logs in and still gets nothing. That is deliberate: the
built-in `aad` provider admits any Microsoft identity, so the ROLE is the access control, not the login.

**Why not restrict to the tenant instead:** that needs a custom Entra app registration plus a client
secret held as a SWA application setting. Per-person invitation costs one click and no secret, and the
audience is a handful of people. Revisit if the reader list grows.

Until step 3 happens the docs deploy will succeed and the site will refuse everyone, which is the safe
direction to fail in.

**Discharged by the owner on 2026-08-29.** Token re-copied into
`AZURE_STATIC_WEB_APPS_API_TOKEN_DOCS_DEV` and the `docs_reader` invitation accepted, so the docs site
is reachable again.

> **Numbering note:** there are two MS-4 entries in this file — this one, and a payroll-currency item
> further down that was closed on 2026-08-14. The collision is historical and is left alone rather than
> renumbered, because both ids are referenced in commits and PR bodies that cannot be edited. Read the
> title, not the number.
### MS-3 — Rotate the exposed Mapbox token — **owner**

Four environment files and two runbook rows still carry `MANUAL_STEP (rotate-mapbox-token)`; the exposed
token remains recoverable from git history, so rotation is the only thing that retires it. Its original
tracker row is now inside the archived backlog, which is why it is re-filed here.

**Action:** rotate the token at Mapbox, provision the new value as `Mapbox--GeocodingAccessToken` in Key
Vault (`deploy/AZURE-DEV-RUNBOOK.md:281`), then delete the four `MANUAL_STEP` comments.

## Cleared

### MS-14 — Regenerate the customer client for the property-size catalogue — **DISCHARGED 2026-09-15**

Found discharged on ground truth, not by a step taken on the day: the customer client had been
regenerated in the multicurrency programme, `libs/cleansia-customer-features/home/src/lib/home/components/quick-quote/quick-quote.facade.ts:203`
calls `countryClient.getPropertySizes(isoCode, lang)`, and `grep -rn CZ_PROPERTY_SIZE_PRESETS src/Cleansia.App`
returns nothing — the hardcoded list this step existed to replace is gone. The "also run
`seed/insert_property_size_presets.sql`" half is moot as well: the presets are inserted by the root
`sql-scripts/insert_seed_data.sql` (`PropertySizePresets — the per-country size ladder`), which every
host auto-seeds. T-0675 closed with it (T-0756). The row sat `owner` for a fortnight after the thing it
asked for had happened — the reason the backlog README says to grep before dispatching.

### MS-12 — Regenerate the customer web client for the promo-request endpoint — **DONE 2026-08-31**

Owner regenerated `customer-client.ts`. The regen is **purely additive** — 144 lines in, none out —
and adds `PromoCodeClient.request()` plus `RequestPromoCodeCommand` / `RequestPromoCodeResponse`.

`PromoRequestFacade` now calls it, so the footer box sends for real and T-0676 AC6 is closed. The
lib barrel needed the two new DTOs adding by hand: `libs/core/customer-services/src/index.ts`
re-exports the generated client **selectively**, so a regenerated type is invisible to the apps
until it is named there. That is not obvious from a green regen — the client compiles, the app does
not, and the error names the barrel rather than the regen.


### MS-8 — Regenerate the admin client for the country field labels — **DONE 2026-08-27**

`AdminCountryController.GetFieldLabels` now exposes `GET api/AdminCountry/field-labels/{countryId}`,
returning the same `CountryFieldLabelsDto` the partner host has served since #228. The admin Angular
app cannot call it until the generated client carries the method.

**Action:** run `npm run generate-admin-client` from `src/Cleansia.App`, with the Admin API running.

**Why the backend half shipped alone.** The audit that filed T-0615 recorded it as "needs an NSwag
regen". It did not — the admin host had **no field-labels route at all**, so regenerating would have
produced nothing. The route had to exist first, and now it does.

**What stays hardcoded until then.** The Czech "IČO" is baked into the admin translation files in
three key groups, which an admin editing a Polish or Ukrainian company reads today:
`companyInfo.registrationNumber` (`en.json:16`), `pages.companyInfo.tax_id` and
`.registration_number` (`:327`, `:331`), and the employee-detail group at `:1330`. Once the client
method exists those read from the country's own configuration, the way the partner app already
does, and the hardcoded strings become neutral wording rather than a Czech term.

### MS-7 — Regenerate the partner web client — **DONE 2026-08-25**

Owner regenerated the partner and admin NSwag clients. `deleteMyDocument` is gone from
`partner-client.ts` and `requestMyDocumentDeletion` / `replaceMyDocument` /
`getMyDocumentRequirements` / `getFieldLabels` are in; the admin client gained the requirement
CRUD and the deletion queue.

The partner web app was moved onto the new endpoint in the same pass: the delete button is now
a removal REQUEST behind a dialog that collects the reason the server requires, and the list is
deliberately not mutated because nothing was removed. The approved-only guard on the button is
gone with it — it mirrored `DeleteMyDocument`'s validator, and an expired approved ID is exactly
the case worth asking about.

**Still open, and NOT this step:** the admin app has no UI for the requirement CRUD or the
deletion queue. The client methods exist (`requirementsGet` / `requirementsPut` /
`requirementsDelete` / `deletionRequests` / `resolve`), so it is ordinary frontend work rather
than a manual step — an admin answers requests through the API until it is built.


### MS-6 — Regenerate `Initial` for the G-03 column and the G-18 index — **DONE 2026-08-15**

**Run by the agent session, and the rule changed with it.** The owner ruled that regenerating `Initial` is no
longer a manual step: *"Regenerate the migration on your own and also mark this step as non MS. It can
be done by you as well."*

`20260813085249_Initial` → **`20260815094107_Initial`**, carrying `RefreshToken.RememberMe` (G-03) and
`IX_Orders_RecurringTemplateId_CleaningDateTime` (G-18), with the P2 seat index and all six
`NULLS NOT DISTINCT` options intact. Verified by the integration suite — **197 tests against real
Postgres**, which is the only thing that proves the model and the schema agree.

The commands, and the trap that the startup project must be a web host rather than
`Cleansia.MigrationService`, are now in `CLAUDE.md` § *Manual steps*.


### MS-4 — Payroll currency: DTO + regeneration — **DONE 2026-08-14**

Backend by the agent session on the owner's instruction (*"MS-4 you can add on your own"*), regeneration by the
owner (`d10a2cc2`). `PeriodPaySummaryDto.CurrencyCode` is sourced from the **invoice** when the period
has one, so "My Pay" and the cleaner's payout document read the same row and cannot diverge; only an
un-invoiced period resolves, through the same service the partner dashboard uses.

It was one DTO, not the two this step originally named: `OrderEmployeePayDto` is never returned on its
own, and its other parent `EmployeeInvoiceDetailDto` already carried the field.

The partner "My Pay" screen no longer hardcodes `Kč` — six template symbols and the table formatter all
read the server's value, and an absent code renders the amount with no symbol rather than guessing one.


### MS-5 — Regenerate the admin client for the entry-instruction reveal — **DONE 2026-08-14**

Run by the owner (`ac6eebd0`). The admin client carries `AccessInstructionsClient.reveal` and
`OrderItem.hasAccessInstructions`; the reveal control shipped in the same PR, so the interim state where
an admin could not see entry instructions at all lasted only as long as the PR was open.


### MS-1 — Regenerate the `Initial` migration for the order seat ordinal — **DONE 2026-08-13**

Run by the agent session on the owner's explicit instruction ("you can regenerate Initial migration on your
own"), which overrides `CLAUDE.md` § *Manual Steps* for this step only. **The standing rule is
unchanged** unless the owner says otherwise.

```
20260811192214_Initial  ->  20260813085249_Initial
```

Regenerated with `dotnet ef migrations remove --force` then `migrations add Initial` — not hand-folded
into the three files, per the 2026-08-09 ruling. The EF CLI was pinned to **10.0.3** to match
`Directory.Packages.props` (an install had bumped the global tool to 10.0.11; it was rolled back).

Carries what the model gained:

```csharp
SeatOrdinal = table.Column<int>(type: "integer", nullable: false)     // :1779

migrationBuilder.CreateIndex(
    name: "IX_OrderEmployees_OrderId_SeatOrdinal",
    table: "OrderEmployees",
    columns: new[] { "OrderId", "SeatOrdinal" },
    unique: true);                                                    // :2984-2988
```

**⚠️ The migration id changed, so DEV needs a database drop before the next deploy.** That is the
standing consequence of regenerating `Initial` rather than stacking, and it is why this is pre-prod-only.

`TakeOrderConcurrentSeatRaceTests` is no longer skipped and **passes against real Postgres**: two
concurrent commits, exactly one winner, one `DbUpdateException`, one surviving assignment at ordinal 0.

---

### MS-11 — regenerate the API clients before DEV sees T-0665 (DISCHARGED 2026-08-29)

**Two regens, and the order matters: regenerate BEFORE the next deploy, not after.**

T-0665 removed a response body that carried no information. Four commands answered `true` on every
success path — `Logout`, `Register`, `RegisterEmployee`, `ResendConfirmationEmail` — because failures
travel the error channel, so the payload could never be anything else. `HandlePaymentNotification`
answered a Stripe event id that nothing read. All five are now bare `ICommand`, and the endpoints
answer 200 with no body.

The generated clients still expect a `bool` and a `string`. Until they are regenerated they will try
to deserialise a body that is no longer sent, on **logout, registration and resend-confirmation** —
three paths every user meets.

1. **`manual_step: nswag-regen`** — `npm run generate-*-client` for the three web apps. Owner-only
   (`CLAUDE.md` § *Manual steps*).
2. **`manual_step: mobile-spec-regen`** — `src/cleansia_ios/scripts/refresh-mobile-spec.sh` against
   running mobile hosts (:5002, :5004), which rewrites the shared specs under
   `src/cleansia_android/openapi/` that both Android and iOS generate from.

Backend, controllers and tests are done and green — 4077 unit tests pass and the solution builds. What
remains is only the client side of a contract that deliberately changed.

**Discharged by the agent session on the owner's explicit instruction** — *"Regenerate all of the clients on your
own, the API is running"* — which overrides `CLAUDE.md` § *Manual steps* **for this step only**. The
standing rule that NSwag regeneration is owner-run is unchanged unless the owner says otherwise, the
same way MS-8 was handled for the EF migration.

All five hosts were up and serving swagger, and the running build already carried T-0665: the partner
and customer specs reported no 200 schema on Logout/Register, which is how the regeneration was known
to be reading the new contract rather than the old one.

**Web** — `npm run generate-clients` rewrote all three NSwag clients. Its closing typecheck failed, as
that script's own message predicts, with three real call sites: `register` in both partner and customer
auth services and `registerEmployee` in partner still declared `Observable<boolean>` where the client
now returns `Observable<void>`. Each now ends `.pipe(map(() => true))` — the shape `logout()` and
`resendConfirmationEmail()` in the same files already used, so no facade or spec had to move.

**Mobile** — both shared specs refreshed. The downgrade guard refused them first, correctly: the specs
came back ~1.5 KB smaller. Diffing the refused `.fetched` copies proved the shrink was exactly T-0665
and nothing else — no path removed, no schema removed, nothing added, and the only changes were 200
bodies disappearing from the four partner auth endpoints and the three customer ones plus
`/api/Payment/webhook`. The guard gained `--allow-shrink` for that case rather than being worked
around by moving files past it.

Verified: typecheck clean across all three compilation units, all three apps build, and 74 tests pass
across the two service libraries and both register facades. Android and iOS need nothing further — both
generate from the committed specs at build time and no generated client is tracked.
