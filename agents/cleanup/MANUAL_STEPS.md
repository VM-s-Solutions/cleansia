# Manual steps — owner-only

Steps this cleanup needs that Claude does not run (see `CLAUDE.md` § *Manual Steps*). One row per
step, cleared when done.

## Open

### MS-2 — Drop the DEV database before the next deploy — **owner, deferred by decision**

> **Owner, 2026-08-14:** *"I'll drop the db and reseed the data after all of the Phases are done."*
> Deferred deliberately — not overlooked. It stays open until the drop happens.


`MS-1` regenerated the single `Initial` migration, `MS-6` regenerated it again, and the partner
document-lifecycle work regenerated it a third time for the two new tables — so its id has moved from
`20260811192214` to `20260813085249` to `20260815094107` to **`20260825114012`**.

Regenerating is no longer a manual step of any kind (owner ruling 2026-08-25): it is ordinary work and
is done in the branch that needs it. **This drop is the part that stayed the owner's**, and every
regeneration renews it rather than adding a new obligation. `MigrationService/Program.cs` runs `MigrateAsync()` on every deploy, and a database
whose `__EFMigrationsHistory` records the **old** id will try to replay the whole create script against
tables that already exist — failing the `migrate-database` job every other deploy job depends on.

**Action:** drop the DEV database, then deploy. Pre-production, so there is no data to preserve; the
seed repopulates it (`sql-scripts/insert_seed_data.sql`).

This obligation was recorded only inside `MS-1`'s **Cleared** row, where a reader looking at *"what do I
owe?"* would not find it. That is what `CL-043` is.

### MS-9 — Invite yourself to the admin console — **owner, BEFORE the next admin deploy**

`apps/cleansia-admin.app/src/staticwebapp.config.json` now requires the role `admin_console` on
every route and redirects 401/403 to `/.auth/login/aad`, the same shape the docs site has used
since it was gated. Owner ruling 2026-08-27: docs + admin, not the two self-service funnels.

**Action, before the next admin deploy:** Portal → `swa-cleansia-admin-*` → *Role management* →
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

### MS-4 — Re-copy the docs SWA deployment token, and invite yourself — **owner**

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
### MS-3 — Rotate the exposed Mapbox token — **owner**

Four environment files and two runbook rows still carry `MANUAL_STEP (rotate-mapbox-token)`; the exposed
token remains recoverable from git history, so rotation is the only thing that retires it. Its original
tracker row is now inside the archived backlog, which is why it is re-filed here.

**Action:** rotate the token at Mapbox, provision the new value as `Mapbox--GeocodingAccessToken` in Key
Vault (`deploy/AZURE-DEV-RUNBOOK.md:281`), then delete the four `MANUAL_STEP` comments.

## Cleared

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

**Run by Claude, and the rule changed with it.** The owner ruled that regenerating `Initial` is no
longer a manual step: *"Regenerate the migration on your own and also mark this step as non MS. It can
be done by you as well."*

`20260813085249_Initial` → **`20260815094107_Initial`**, carrying `RefreshToken.RememberMe` (G-03) and
`IX_Orders_RecurringTemplateId_CleaningDateTime` (G-18), with the P2 seat index and all six
`NULLS NOT DISTINCT` options intact. Verified by the integration suite — **197 tests against real
Postgres**, which is the only thing that proves the model and the schema agree.

The commands, and the trap that the startup project must be a web host rather than
`Cleansia.MigrationService`, are now in `CLAUDE.md` § *Manual steps*.


### MS-4 — Payroll currency: DTO + regeneration — **DONE 2026-08-14**

Backend by Claude on the owner's instruction (*"MS-4 you can add on your own"*), regeneration by the
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

Run by Claude on the owner's explicit instruction ("you can regenerate Initial migration on your
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

**Discharged by Claude on the owner's explicit instruction** — *"Regenerate all of the clients on your
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
