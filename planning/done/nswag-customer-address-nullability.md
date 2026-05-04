# NSwag customerAddress Nullability Fix — drop the `as unknown as AddressDto` cast

**Status:** Completed (Plan B — targeted SchemaFilter on Customer + Mobile APIs, wraps `$ref` in `allOf` + `nullable: true`)
**Depends on:** None (self-contained; touches backend Swagger config + NSwag-regenerated TypeScript clients)

## Why this exists

The backend `CreateOrder.Command` at
`src/Cleansia.Core.AppServices/Features/Orders/CreateOrder.cs:154`
declares:

```csharp
public record Command(
    ...
    AddressDto? CustomerAddress,  // NULLABLE
    ...
)
```

but the NSwag-generated TypeScript client at
`src/Cleansia.App/libs/core/customer-services/src/lib/client/customer-client.ts:4462`
declares:

```typescript
customerAddress!: AddressDto;  // NON-NULL ASSERTION
```

with the matching interface at line 4564:

```typescript
export interface ICreateOrderCommand {
    ...
    customerAddress: AddressDto;  // NON-NULLABLE
    ...
}
```

To work around this, `order-wizard.facade.ts` currently uses:

```typescript
customerAddress: undefined as unknown as AddressDto,
```

which is a lie to the type system — the field is actually `undefined` at
runtime when the user picked a saved address and no inline address is being
submitted. This was documented as a TODO in
`planning/active/address-unification-phase-c-web.md`.

Mobile is **unaffected** — the Android client is hand-written
(`CreateOrderAddressDto? = null`) and already handles nullability correctly.
Only the NSwag-generated web clients are wrong.

## Root cause hypothesis

Swashbuckle (the ASP.NET OpenAPI generator that feeds NSwag) defaults to
`SupportNonNullableReferenceTypes = false`. In that mode, it does **not**
inspect C# nullable-reference-type annotations (`string?`, `AddressDto?`)
when building the schema — it treats all reference-type properties as
non-nullable at the JSON-schema level. NSwag then generates TypeScript that
reflects that schema: non-optional, non-null-assertion fields.

The fix is a one-line config call: `c.SupportNonNullableReferenceTypes()` in
every Swashbuckle setup. That flips the global default so Swashbuckle emits
`"nullable": true` in the schema for every nullable reference-type property,
and NSwag generates `foo?: T` in TypeScript.

The blast radius is potentially large — every nullable reference-type
property on every DTO across 4 APIs ripples through. Existing web code may
silently depend on the current wrong types. This spec plans for that.

## What this spec does NOT do

- Rewrite how nullable reference types are used in the C# backend. The
  annotations are already correct — we're just teaching Swashbuckle to
  respect them.
- Switch to a different API client generator (kiota, openapi-typescript-codegen,
  orval). This is an incremental fix; a generator migration is a separate spec.
- Fix nullability issues on the mobile OpenAPI spec dump. The mobile client
  is hand-written and doesn't consume the generated JSON — any ripple in the
  mobile API's OpenAPI schema is cosmetic for now.
- Regenerate the clients itself (that's a MANUAL_STEP owned by the repo owner).
- Apply EF Core migrations. No schema changes here.

---

## Phase 1 — Audit current Swashbuckle config

### TASK-NS1: Audit Swashbuckle configuration across all 4 APIs

```yaml
task: Find every AddSwaggerGen / AddOpenApi / SwaggerGenOptions call and document current nullability behavior
id: TASK-NS1
type: investigation
priority: high
specialist: backend
app: backend
estimated_complexity: small
recommended_model: sonnet

context: |
  Before flipping any flag, confirm exactly where Swashbuckle is wired
  for each of the 4 APIs and what config options are currently set. We
  need to know:

    1. Is `c.SupportNonNullableReferenceTypes()` already called anywhere?
       (Almost certainly NO given the bug, but verify.)
    2. Are there any `SchemaFilter` or `OperationFilter` registrations
       that might override nullability for specific types?
    3. Is the Swashbuckle version 6.x+? (`SupportNonNullableReferenceTypes`
       was added in 6.x — older versions need upgrading.)
    4. Are the 4 APIs consistent in their Swagger setup, or does each
       configure it differently?

  Also check `Cleansia.Config` — if there's a shared Swagger extension
  method in `ServiceExtensions.cs` or similar, the fix in NS2 becomes a
  one-location edit. If each API registers its own independently, we
  need to touch 4 files.

files_to_read:
  - src/Cleansia.Web/Program.cs
  - src/Cleansia.Web.Admin/Program.cs
  - src/Cleansia.Web.Mobile/Program.cs
  - src/Cleansia.Web.Customer/Program.cs
  - src/Cleansia.Web/Extensions/**/*.cs
  - src/Cleansia.Web.Admin/Extensions/**/*.cs
  - src/Cleansia.Web.Mobile/Extensions/**/*.cs
  - src/Cleansia.Web.Customer/Extensions/**/*.cs
  - src/Cleansia.Config/Services/ServiceExtensions.cs
  - src/Cleansia.Config/**/*.cs # grep for AddSwaggerGen

investigation_steps:
  - Grep for `AddSwaggerGen` across the whole repo:
      rg -n "AddSwaggerGen|SupportNonNullableReferenceTypes|ISchemaFilter|IOperationFilter"
  - Check Swashbuckle package version in `*.csproj` (look for
    Swashbuckle.AspNetCore). Required: >= 6.4.0 for
    SupportNonNullableReferenceTypes.
  - Identify whether there's a shared `AddCleansiaSwagger(...)` method
    or whether each API configures independently.
  - Record findings in a short audit note appended to this task's
    verification section.

deliverables:
  - A short audit summary (a few bullets) answering:
    - Where is AddSwaggerGen called? (file + line for each API)
    - Current Swashbuckle version?
    - Any existing SchemaFilters/OperationFilters?
    - Is it centralized in Cleansia.Config or per-API?
  - Clear input for TASK-NS2 (one edit location vs four).

dependencies: []
verification:
  - Audit note committed to this spec as a comment OR just reported
    back to the owner inline before starting NS2.
```

---

## Phase 2 — Fix the Swashbuckle config

### TASK-NS2: Enable `SupportNonNullableReferenceTypes` globally

```yaml
task: Add c.SupportNonNullableReferenceTypes() to all 4 Swashbuckle configs
id: TASK-NS2
type: bugfix
priority: high
specialist: backend
app: backend
estimated_complexity: small
recommended_model: sonnet

context: |
  Based on NS1 audit, add `c.SupportNonNullableReferenceTypes()` to
  every AddSwaggerGen call. This is the single flag that tells
  Swashbuckle to inspect C# nullable-reference-type annotations when
  building schema. Without it, `AddressDto?` and `AddressDto` look
  identical to the schema generator.

  Preferred: if Cleansia.Config has a shared `AddCleansiaSwagger`
  extension (NS1 will confirm), edit that ONE file and all 4 APIs
  pick it up automatically. Otherwise, touch all 4 Program.cs /
  Extension files directly.

  Upgrade Swashbuckle if needed: requires >= 6.4.0. If NS1 finds an
  older version, bump to the latest stable 6.x (likely 6.5.x or 6.6.x)
  in every .csproj that references Swashbuckle.AspNetCore. Flag as
  MANUAL_STEP for the owner to verify the lockfile diff.

  **Plan B (fallback) — targeted SchemaFilter:**

  If TASK-NS5 reveals the blast radius is too large (>50 compile
  errors in TypeScript client consumers), revert NS2 and instead add
  a narrow ISchemaFilter that only forces nullability on the known
  problem properties. Minimum set: `CreateOrder.Command.CustomerAddress`.
  Sketch:

    public class NullableAddressSchemaFilter : ISchemaFilter
    {
        public void Apply(OpenApiSchema schema, SchemaFilterContext ctx)
        {
            if (ctx.Type == typeof(CreateOrder.Command) &&
                schema.Properties.TryGetValue("customerAddress", out var prop))
            {
                prop.Nullable = true;
                schema.Required.Remove("customerAddress");
            }
        }
    }

  Register with:
    c.SchemaFilter<NullableAddressSchemaFilter>();

  This approach leaves every OTHER nullable reference type
  incorrectly non-null in the client — a known-bad compromise —
  but ships the wizard fix immediately without the ripple.

files_to_modify:
  # If NS1 finds a shared helper in Cleansia.Config, edit only that one.
  # Otherwise edit each Program.cs / Extensions file individually.
  - path: src/Cleansia.Config/Services/ServiceExtensions.cs # IF shared
    change: |
      Inside the AddCleansiaSwagger(...) extension (or equivalent
      AddSwaggerGen block), add:
        c.SupportNonNullableReferenceTypes();
      alongside the existing options (document comments, OpenApi info,
      security definitions, etc.). Do not reorder existing calls.

  - path: src/Cleansia.Web/Program.cs # IF per-API
    change: |
      Inside the AddSwaggerGen block, add
        c.SupportNonNullableReferenceTypes();
    # Same edit for Cleansia.Web.Admin / Cleansia.Web.Mobile /
    # Cleansia.Web.Customer if they register their own.

  # If Swashbuckle version < 6.4.0 per NS1:
  - path: src/Cleansia.Web/Cleansia.Web.csproj # and siblings
    change: |
      Bump <PackageReference Include="Swashbuckle.AspNetCore"
        Version="6.4.0" /> to the latest stable 6.x.
      Apply to each .csproj that references Swashbuckle.AspNetCore.

manual_step:
  label: MANUAL_STEP — Swashbuckle upgrade (only if NS1 version < 6.4.0)
  who: owner
  what: |
    Review `dotnet restore` output and Directory.Packages.props /
    packages.lock.json diff after the version bump. Commit lockfile
    changes alongside the code change. No migration runs.

dependencies:
  - TASK-NS1
verification:
  - dotnet build Cleansia.Api.sln
  - Run each API locally and hit its /swagger/v1/swagger.json endpoint.
    Confirm the CreateOrder.Command schema now has:
      "customerAddress": { ..., "nullable": true }
    and that "customerAddress" is NOT in the "required" array.
  - Spot-check another known-nullable property (e.g. Order.CancelledAt,
    User.MiddleName if present) — should also show "nullable": true.
```

---

## Phase 3 — Regenerate + audit the diff

### TASK-NS3: Dump fresh OpenAPI specs and compare

```yaml
task: Dump swagger.json from all 4 APIs and diff against checked-in baselines
id: TASK-NS3
type: investigation
priority: high
specialist: backend
app: backend
estimated_complexity: small
recommended_model: sonnet

context: |
  With NS2 applied and each API running locally, pull fresh
  swagger.json and compare against the current baselines. Expect
  dozens of properties across many DTOs to flip from
  (implicitly non-nullable / required) to "nullable": true.

  Mobile OpenAPI dump lives at
  `src/cleansia_customer_android/openapi/customer-api.json` —
  refresh that too (sanity only; mobile is hand-written).

  For admin/partner/customer NSwag clients, find the source of truth
  the NSwag generator reads from. Check
  `src/Cleansia.App/libs/core/customer-services/` (and admin/partner
  siblings) for an `nswag.json` or similar config — it typically
  points at either a running localhost URL or a checked-in JSON.
  If the config uses a checked-in JSON, update that JSON. If it
  uses a live URL, no checked-in file to refresh here.

manual_step:
  label: MANUAL_STEP — OpenAPI dump + NSwag regen
  who: owner
  what: |
    With NS2 deployed to a local dev environment:
      1. Run each API:
         dotnet run --project src/Cleansia.Web
         dotnet run --project src/Cleansia.Web.Admin
         dotnet run --project src/Cleansia.Web.Mobile
         dotnet run --project src/Cleansia.Web.Customer
      2. Dump each swagger.json:
         curl http://localhost:5000/swagger/v1/swagger.json > <checked-in-path-if-any>
         curl http://localhost:5001/swagger/v1/swagger.json > ...
         curl http://localhost:5002/swagger/v1/swagger.json > src/cleansia_customer_android/openapi/customer-api.json  # was mobile
         curl http://localhost:5003/swagger/v1/swagger.json > <customer-dump-location>
      3. Regenerate TypeScript clients:
         cd src/Cleansia.App
         npm run generate-partner-client
         npm run generate-admin-client
         npm run generate-customer-client
      4. `git diff` the client files and the swagger JSON files.
         Expect a large diff. Hand off to NS4.

files_to_inspect_manually:
  - swagger.json diffs (all 4 APIs)
  - src/Cleansia.App/libs/core/customer-services/src/lib/client/customer-client.ts
  - src/Cleansia.App/libs/core/admin-services/src/lib/client/admin-client.ts
  - src/Cleansia.App/libs/core/partner-services/src/lib/client/partner-client.ts

dependencies:
  - TASK-NS2
verification:
  - Customer swagger.json: CreateOrder.Command.customerAddress has
    "nullable": true AND is not listed under "required".
  - Admin/Partner swagger: similar flips wherever DTOs have C# `?` props.
  - NSwag clients regenerated cleanly (no generator errors).
  - The generated ICreateOrderCommand interface now declares:
      customerAddress?: AddressDto | undefined;
    and the class declares:
      customerAddress?: AddressDto;
```

### TASK-NS4: Audit the regenerated TypeScript diff

```yaml
task: Inspect the NSwag client diff and categorize every change
id: TASK-NS4
type: investigation
priority: high
specialist: frontend
app: cleansia-app
estimated_complexity: medium
recommended_model: sonnet

context: |
  The NSwag diff from NS3 will touch many generated files. Go through
  it systematically and categorize each change:

    A. Expected flip — `foo!: T` → `foo?: T` on a property that is
       genuinely nullable in C#. Healthy. Consumers may need updates.

    B. Expected flip — constructor signatures that used to require
       `foo!` now accept undefined. Healthy.

    C. Unexpected flip — a property that was non-null in C# but is
       now nullable in TypeScript. Should not happen if C# annotations
       are correct, but flag any cases for manual backend review.

    D. Delete — the generator may remove class-level casts or
       init! markers. Healthy.

  Produce a short summary (just counts + a handful of notable flips)
  for the owner before proceeding to NS5. This helps decide whether
  the blast radius is manageable or whether we need Plan B from NS2.

  **Size thresholds:**
  - < 50 TypeScript compile errors when building the 3 apps: proceed
    with NS5 as planned (fix each site individually).
  - 50–200 errors: consider staging — ship the customer app fix first
    (NS5 customer wizard only), open a follow-up spec for admin/partner.
  - > 200 errors: fall back to Plan B (targeted SchemaFilter from NS2).
    Revert the global flag; only force nullability on CreateOrder.
    CustomerAddress. Regenerate again. Ship NS5 customer wizard only.
    Open a follow-up spec to progressively widen the filter.

dependencies:
  - TASK-NS3
verification:
  - A count report: N files changed, X properties flipped to optional,
    Y constructors updated, Z unexpected flips (should be 0).
  - An initial pass of `npx nx build` for all 3 apps to measure the
    compile-error count (the key blast-radius metric).
```

---

## Phase 4 — Fix consumers

### TASK-NS5: Fix TypeScript compile errors surfaced by the client regen

```yaml
task: Build all 3 Angular apps, resolve every compile error, drop the wizard cast
id: TASK-NS5
type: bugfix
priority: high
specialist: frontend
app: cleansia-app
estimated_complexity: large
recommended_model: sonnet

context: |
  Each compile error falls into one of three buckets:

    1. LEGITIMATE bug masked by the old wrong types — the consumer
       was passing `undefined` but the type said `T`. Now TypeScript
       correctly flags it. Example: order-wizard.facade.ts and the
       `undefined as unknown as AddressDto` cast. Fix the consumer
       to handle `T | undefined` properly (optional chaining,
       null checks, conditional spreads, etc.).

    2. MISSING non-null annotation on the backend — the property is
       genuinely required but the C# record declared it as nullable.
       Fix the backend: drop the `?`, add a FluentValidation rule if
       one is missing, re-regen. This is rare but possible.

    3. COSMETIC — the generator added `| undefined` to a spot that
       never gets `undefined` in practice. Accept the type, update
       the consumer to handle it (even if it means a fall-through
       `?? throw` or `!` assertion at a single well-commented call site).

  The KEY target: `order-wizard.facade.ts` — remove the cast:

    customerAddress: undefined as unknown as AddressDto

    becomes

    customerAddress: savedAddressId ? undefined : customerAddress

  (exact shape depends on how the facade currently assembles the
  command — just drop the cast and pass a proper `AddressDto | undefined`).

  **Sweep order:**

    1. Customer app (where the bug lives):
       - npx nx build cleansia.app
       - Fix each error.
       - Explicitly remove the `as unknown as AddressDto` cast in
         `libs/cleansia-customer-features/order-wizard/src/lib/order-wizard/order-wizard.facade.ts`.
    2. Partner app:
       - npx nx build cleansia-partner.app
       - Fix each error.
    3. Admin app:
       - npx nx build cleansia-admin.app
       - Fix each error.
    4. Run unit tests for each app that exercises the changed code
       paths (`npx nx test`).

files_to_modify:
  # The primary target — remove the cast
  - path: src/Cleansia.App/libs/cleansia-customer-features/order-wizard/src/lib/order-wizard/order-wizard.facade.ts
    change: |
      Find the `undefined as unknown as AddressDto` cast. Replace with
      a proper conditional that passes either an `AddressDto` (when
      the user entered inline address details) or `undefined` (when
      they picked a saved address by ID). The generated command type
      should now accept `AddressDto | undefined` directly.

      Example shape:
        const command: ICreateOrderCommand = {
          ...
          savedAddressId: pickedSavedAddress?.id,
          customerAddress: pickedSavedAddress
            ? undefined
            : {
                street: formValue.street,
                city: formValue.city,
                zipCode: formValue.zipCode,
                // ...
              } as AddressDto,
          ...
        };

      Delete any related workaround comments in the facade referring
      to the NSwag nullability bug.

  # Then: every other file the build pass flags.
  # Cannot be pre-listed here — depends on NS4's diff audit.
  - path: '<each file with a compile error after regen>'
    change: |
      Apply whichever of the three buckets (legitimate bug fix,
      backend annotation fix, cosmetic type update) is appropriate.

      If any site genuinely expects the property to always be present,
      prefer `value ?? throwExpected('foo')` over `value!` so we
      surface the invariant at runtime. Reserve `!` for places where
      the invariant is truly provable from surrounding code.

files_to_consider:
  # Known consumers to sanity-check — NS4 will surface more.
  - src/Cleansia.App/libs/cleansia-customer-features/order-wizard/src/lib/order-wizard/components/wizard-summary-step.component.ts
  - src/Cleansia.App/libs/cleansia-customer-features/order-wizard/src/lib/order-wizard/order-wizard.models.ts
  - src/Cleansia.App/libs/core/customer-services/src/lib/services/*.ts
  - src/Cleansia.App/libs/core/admin-services/src/lib/services/*.ts
  - src/Cleansia.App/libs/core/partner-services/src/lib/services/*.ts

dependencies:
  - TASK-NS4
verification:
  - `npx nx build cleansia.app` succeeds with 0 errors 0 warnings.
  - `npx nx build cleansia-partner.app` succeeds.
  - `npx nx build cleansia-admin.app` succeeds.
  - `npx nx test cleansia.app` (and siblings) all green.
  - Manual: order wizard — create order with a saved address picked.
    Submit. Verify request body has `savedAddressId` and NO
    `customerAddress` field (or `customerAddress: null`). Previously
    it would send a hollow "AddressDto" because of the cast.
  - Manual: order wizard — create order with an inline address.
    Submit. Verify `customerAddress` is populated and `savedAddressId`
    is absent.
```

---

## Phase 5 — Cleanup

### TASK-NS6: Remove TODO markers and update docs

```yaml
task: Strip the address-unification TODO that flagged this bug; update CLAUDE.md if it mentions the workaround
id: TASK-NS6
type: docs
priority: medium
specialist: docs
app: cleansia-app
estimated_complexity: small
recommended_model: sonnet

context: |
  Once NS5 lands, the workaround comment in
  `planning/active/address-unification-phase-c-web.md` is stale.
  Remove the TODO that described the `as unknown as AddressDto` cast.

  Also grep for any CLAUDE.md entries or inline code comments that
  reference "NSwag nullability workaround", "customerAddress cast",
  or the `as unknown as AddressDto` pattern. Remove them.

files_to_modify:
  - path: planning/active/address-unification-phase-c-web.md
    change: |
      Find the TODO block that references the customerAddress cast /
      NSwag nullability workaround. Replace with a one-line note:

        <!-- NSwag nullability bug resolved — see
             planning/active/nswag-customer-address-nullability.md -->

      Or just delete the TODO entirely if the surrounding spec is
      already considered "done".

  - path: CLAUDE.md
    change: |
      Grep for any reference to the customerAddress workaround,
      "unknown as AddressDto", or SupportNonNullableReferenceTypes.
      If none, skip. If any, update to reflect the new reality:
      "NSwag clients respect C# nullable reference types — do not
      use casts to bypass generated types."

  - path: src/Cleansia.App/CLAUDE.md
    change: |
      Same sweep as above.

grep_targets:
  - "as unknown as AddressDto"
  - "NSwag nullability"
  - "customerAddress cast"
  - "SupportNonNullableReferenceTypes"

dependencies:
  - TASK-NS5
verification:
  - Grep returns zero hits for the above patterns (except inside this
    spec file itself and the follow-up note in phase-c-web.md).
  - `git grep "as unknown as AddressDto"` → empty.
```

---

## Execution order

1. **TASK-NS1** (audit Swashbuckle config across all 4 APIs) — backend, no deps.
2. **TASK-NS2** (add `SupportNonNullableReferenceTypes` flag) — backend, depends on NS1.
3. **→ MANUAL_STEP:** owner dumps fresh swagger.json from each API and
   regenerates the 3 TypeScript clients (`npm run generate-*-client`).
4. **TASK-NS3** (diff inspection of swagger + generated TS) — investigation,
   depends on the manual regen.
5. **TASK-NS4** (categorize the diff, measure blast radius) — frontend
   investigation, depends on NS3.
6. **Decision gate:** if > 200 compile errors, revert NS2 and apply Plan B
   (targeted SchemaFilter) per NS2's fallback plan, then redo the manual
   regen step, then resume at NS4.
7. **TASK-NS5** (fix every compile error, drop the wizard cast) — frontend,
   depends on NS4. This is the biggest task — budget accordingly.
8. **TASK-NS6** (docs + TODO cleanup) — docs, depends on NS5.

Parallelizable: NS1 and early drafting of the NS5 wizard-facade edit can
happen in parallel, but NS5 can't actually *build* until after the manual
regen. NS2 + NS3 are strictly sequential with the manual regen between them.

Estimated tokens: ~35k backend + ~45k frontend = ~80k total, heavily
dependent on how many generated properties flip (NS4 determines that).

---

## Failure modes to plan for

- **Swashbuckle < 6.4.0** — `SupportNonNullableReferenceTypes` doesn't
  exist. NS2 must bump the package version. Flag as a MANUAL_STEP because
  the owner needs to verify the lockfile diff.

- **Blast radius too large** — > 200 TypeScript compile errors across
  the 3 apps. Decision gate after NS4: revert NS2, implement Plan B
  (targeted `ISchemaFilter` on `CreateOrder.Command.CustomerAddress`
  only), reship. Open a follow-up spec to progressively widen the
  filter to cover more DTOs in batches.

- **Inconsistent Swagger config across APIs** — NS1 may find that
  only 3 of 4 APIs share the Cleansia.Config helper, or that one API
  has a legacy ad-hoc Swagger block. Document in NS1 and handle all
  4 locations in NS2.

- **Unexpected backend nullability issues surface** — the regen may
  expose DTOs where the C# `?` annotation is inconsistent with runtime
  behavior (e.g. a property that the handler always populates but is
  declared nullable). Fix those backend DTOs as part of NS5, not a
  separate spec — they're small, one-line changes each.

- **Order wizard submit regression** — after removing the cast, if
  the wizard still silently sends a bad customerAddress (empty-object
  shape), the backend will fail validation differently than before.
  NS5's manual verification (both saved-address and inline-address
  flows) must confirm the submitted JSON shape on the wire.

- **Breaking change to public NSwag client consumers** — if any
  external consumer imports these generated clients directly (no
  known case, but worth checking), every property flip is a breaking
  TypeScript change for them. Not a concern inside this repo.

---

## Out of scope (followup specs)

- **Generator migration** — evaluate kiota or openapi-typescript-codegen
  as a replacement for NSwag. Separate spec.
- **Runtime validation of generated DTOs** — zod schemas auto-derived
  from the OpenAPI JSON, used at the boundary. Separate spec.
- **Mobile OpenAPI client generation** — stop hand-writing
  `CreateOrderCommand` on Android and auto-generate from the same
  OpenAPI source. Separate, bigger spec.
- **Admin/Partner consumer cleanup beyond compile errors** — if NS5
  finds any genuine UX-visible bugs that were masked by the wrong
  types (e.g. a partner screen crashes because a property it assumed
  was populated is actually nullable), open a follow-up issue rather
  than fixing in-spec. Keep NS5's scope to "compiles + the wizard works".
