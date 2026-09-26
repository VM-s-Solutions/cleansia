# Role — `TenantConfiguration` + `TenantSettingCatalog` (a company's own settings) (CRC card)

> Introduced by **ADR-0061 O-4 as ruled** (`docs/decisions/adr-0061.md`, **`accepted`** 2026-09-13;
> owner ruling Q-TENANCY-04, 2026-09-15: *"There is a need to introduce reader and writer for it."*).
> The table existed since the first tenancy pass with **no writer, no rows and one reader that read
> nothing** (the retention job, under no claim). What shipped on 2026-09-15 (T-0759) is the three things
> that make a key/value table a setting: a catalogue that says which keys exist, a reader that resolves
> under the right company, and a writer with a screen.
>
> **Five files are the role:** `Cleansia.Core.Domain/Configuration/TenantConfiguration.cs` (the row —
> `TenantAuditable`, `Key` ≤ 100, `Value` ≤ 4000, `Category`) ·
> `Cleansia.Core.AppServices/Features/TenantSettings/TenantSettingCatalog.cs` (the keys) ·
> `…/TenantSettings/TenantSettingReader.cs` (the resolve) · `…/TenantSettings/{GetTenantSettings,
> SetTenantSetting,ResetTenantSetting}.cs` (the admin surface) · `Cleansia.Web.Admin/Controllers/
> AdminTenantSettingsController.cs` (the routes). The admin web page is
> `libs/cleansia-admin-features/company-settings`.

## Responsibility (one sentence)

Let **one operating company** override **one catalogued platform setting** — and let every reader of
that setting, whether an admin's request or a job with no claim, get *that company's* value or the
catalogue default, never another company's and never a value nothing reads.

## Collaborators

- **`TenantSettingCatalog`** — the closed list of keys a company may hold. Each entry is a typed
  definition (`IntTenantSetting` with `min`/`max`, `BoolTenantSetting`, and since 2026-09-19
  `EmailTenantSetting`) carrying the key, the category and the default. Today, **fifteen keys in three
  categories**: **thirteen `retention.*` keys**, including order photos (default 7 days) and admin and
  cleaner audit rows (default 3 years each), defaults from `RetentionDefaults`, floor **1** on every
  window (a zero window would empty the customer-audit table on the next tick), ceiling 100 years /
  36 500 days (`DateTimeOffset.AddYears` throws past the calendar's end); `lifecycle.chargeback_horizon_days`
  (180, 0–730 — the archive's wait, ADR-0064); and **`notifications.admin_email`** — an
  `EmailTenantSetting`, value type `Email = 3` (appended, never reordered — the type is on the wire),
  stored trimmed and lower-cased, one `@` with something on both sides, no whitespace, ≤ 150, **the
  empty string invalid**: "unset" is the row's absence and `Resolve(null)` answers `""`, which the admin
  notifier reads as *every administrator* (ADR-0065 D3). A key outside the list is refused by the writer
  (`tenant_setting.unknown_key`) and ignored by the readers; a value the definition rejects is refused
  (`tenant_setting.invalid_value`). A category joins by adding entries here — nowhere else.
- **`TenantConfigurationRepository`** — filtered reads (`GetByKeyAsync`, `GetAllAsync`); the unique
  `(TenantId, Key)` index, `NULLS NOT DISTINCT`, is the arbiter of "one row per company per key".
- **`IAppConfigurationProvider.GetTenantSettingAsync(key)`** + the `TenantSettingReader.GetAsync
  (definition)` extension — the reader. It reads the ambient company's row through the filter and
  hands the stored text to the definition's `Resolve`, which returns the parsed value or the default
  when there is no row **or the row holds something the catalogue no longer accepts**. The reader does
  not take a tenant id: the company is whatever is ambient, which is the caller's job to set.
  **One overload takes one** — `GetTenantSettingAsync(tenantId, key)` + `GetAsync(provider, tenantId,
  definition)`, a tenant-ignoring read with `TenantId == tenantId && Key == key` in the predicate — for
  the one writer that must not trust the ambient tenant: the admin notifier, which is called from
  webhooks and jobs whose override is whatever company was processed last, and would otherwise have
  mailed company A's order numbers to company B's mailbox (ADR-0065 D2, challenge B1). Nothing else
  uses it; a request or a per-company loop keeps the ambient form.
- **`DataRetentionBackgroundService`** — the first per-company reader. It loops
  `ITenantRepository.GetAllIdsAsync`, sets the override per company, runs fourteen tasks under it and
  clears; every `GetAsync(TenantSettingCatalog.X)` inside is that company's. `GdprDeletionService`
  reads `DisputeTextRetentionYears` the same way under the erasing request's own claim. The fourteenth
  task removes guest access tokens from their expiry/revocation timestamps and needs no setting.
- **`SetTenantSetting.Command(Key, Value)`** — upsert for **the admin's own company** (the query filter
  finds its row; the commit stamps a new one). Stores the definition's **canonical** form, never the
  text as typed. **`ResetTenantSetting.Command(Key)`** — a **hard delete** of the row (an override has
  no history of its own, and a deactivated row would block the next `Set` on the unique index);
  idempotent. **`GetTenantSettings.Query`** — every catalogue key with its type, range, default,
  effective value and whether a row exists; **the page is the catalogue, not the table** — a surviving
  row for a retired key is not listed.
- **`AdminTenantSettingsController`** — `GET api/AdminTenantSettings/get-all`
  (`CanViewTenantConfigurations`), `PUT …/set` (`CanUpdateTenantConfiguration`), `DELETE …/reset/{key}`
  (`CanDeleteTenantConfiguration`); the two writes under the `auth` rate window. The policies existed
  before the routes did (`PolicyBuilder`, then `AdminOnly`; `AdministratorOnly` since ADR-0066 — the
  *view* too, because the ruling excludes the whole area from a Manager); the routes are the first thing
  they gate.
- **The admin audit** — both writes carry a marker (`tenant_setting.set`, `tenant_setting.reset`,
  `ResourceType = "TenantSetting"`) and push a `TenantSettingSnapshot(Key, Value)` before/after through
  `IAuditContext.RecordChange` — a number or a switch, never personal data; a null value is "no row".
- **The admin web page** (*Company settings*, `/company-settings`, sidebar entry gated by
  `CanViewTenantConfigurations`) — one row per catalogue key: key, description (from the locale, keyed
  by the catalogue key), category, range, default, the value in force and whether it is an override.
  Edit is inline with the typed input the value type calls for (a number field, a checkbox, an e-mail
  field for `Email` — which refuses a malformed address client-side with the server's own
  `api.tenant_setting.invalid_value` sentence and shows *every administrator* while the row is unset);
  Reset sits behind a confirmation and only on an overridden row; either write re-reads the catalogue.
  A spec ties the five admin locales to the backend catalogue and its three categories, so a new key
  without its copy fails the build.

## Does NOT know

- **A per-country setting.** That is `CountryConfiguration` (tax labels, the market's copy figures).
  This table is per **company**; a company serving two countries holds one value for both.
- **Another company.** The writer names no tenant — there is no way to set company B's window from
  company A's admin, and the ambient reader has no tenant parameter. Both are the ambient tenant, and
  under a job that is the override the loop set. The explicit-tenant overload is a read, never a
  write, and its one caller names the company from the event it is told about.
- **What a key means.** The catalogue knows the type and the range; the sweep knows the semantics.
  `Description` on the row is unused by the page (the copy comes from the locale) and `Category` is a
  grouping label, nothing keys on it.
- **History.** A reset deletes the row; the audit trail (`tenant_setting.set` / `.reset` with the
  before/after snapshot) is the only record of what a company's window used to be.
- **Anything a job needs to loop.** The job loops the registry (`ITenantRepository`), not this table —
  a company with no rows is still a company with settings (the defaults).

## Invariants a reviewer checks

1. **Every read goes through a definition.** `configProvider.GetAsync(TenantSettingCatalog.X, ct)`,
   never a bare `GetTenantSettingAsync("retention.…")` with a hand-parsed result — the definition is
   what makes an unparseable or out-of-range row fall back to the default instead of to a `0`.
2. **The writer validates against the catalogue, and the handler trusts it.** `SetTenantSetting.Validator`
   refuses an unknown key and an invalid value; the handler does `Find(key)!` and `Canonicalize(value)!`
   on the strength of that (a null-forgiving read the validator earned — the review's shape).
3. **A per-company reader runs under a per-company override, and commits inside.** The sweep's loop is
   `ClearTenantOverride → SetTenantOverride(tenantId) → fourteen tasks → …`; a task that commits once at the
   end of the *outer* loop stamps every company's rows with the last company seen (CLAUDE.md landmine
   2). Pinned on Postgres: `CustomerActionAuditRetentionTests
   .A_Companys_Own_Window_Is_Read_Per_Company_So_Its_Rows_Go_And_The_Other_Companys_Stay`.
4. **The floor is the catalogue's, not the sweep's.** No sweep re-checks `years <= 0`; the catalogue's
   `min: 1` is the one place the refusal lives, and the writer is the only path a value takes in.
5. **The five admin locales carry every catalogue key** under `pages.company_settings.descriptions.*`
   and every category under `.categories.*` — `tenant-setting-catalogue.spec.ts` reads the backend
   catalogue file and fails on a gap.
6. **The routes are policy-gated and the page hides what the policy refuses.** `TenantSettingsRouteTests`
   (HostTests) for the three routes; the page's Edit and Reset actions read `CanUpdate` /
   `CanDeleteTenantConfiguration` through `PermissionService`.

## Watch-list

- **The next category.** A non-retention setting (a pay-calculation constant, a booking window a company
  wants to move) joins by adding a definition to the catalogue and a description to five locales — the
  `lifecycle` and `notifications` categories joined exactly that way. It does **not** join by adding a
  second table, a second reader or a per-country arm — if a value must differ per country, it is
  `CountryConfiguration`'s and not this table's.
- **A per-market override of a catalogue table** (ADR-0041 RB-9's routed question) is still not this.
  This is a company's *number*, not a company's *catalogue row*.
- **A holding-level view** (set a window for every company at once) is not built and is not a
  switch — it is the holding-admin question ADR-0061 D5 names.
