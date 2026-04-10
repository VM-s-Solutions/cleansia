# Pay Config Management — Rebuild Plan

> Status: Planning | Target: Complete rewrite of pay-config-management feature

## Why a Rebuild

The current `pay-config-management` feature has accumulated architectural debt:

1. **Wrong CSS conventions** — form uses `info-grid`/`info-item` instead of the standard `form-grid`/`form-field` that every other admin form uses
2. **Conceptual confusion** — a "grade multiplier" field on a generic config form that doesn't belong there (grades are for bulk employee setup, not individual rate editing)
3. **Duplicated purpose** — this page creates configs that can target employees, but per-employee bulk setup already lives on the employee detail page
4. **Broken layout** — container padding/margins don't match other admin pages visually
5. **Dead code paths** — form has create/edit modes with grade logic that's never the right answer for either global OR employee-specific use cases

Instead of patching, rebuild with a clear purpose and clean code.

---

## The New Feature: "Global Rates"

### Purpose (narrowed)

This page manages **global rate configurations** only — the platform-wide defaults that apply to every employee unless they have a per-employee override.

Per-employee rates are created ONLY via the employee detail page using the bulk grade-apply feature. They are NEVER edited from this page.

### What the page shows

A **list of all global `EmployeePayConfig` records** (where `EmployeeId IS NULL`), grouped by type (Services / Packages), with the ability to:

- View all global rates at a glance
- Create a new global rate for a service or package that doesn't have one
- Edit an existing global rate (all fields except the service/package binding)
- Delete a global rate

### What the page does NOT do

- ❌ No employee-specific rate management (that's on employee detail)
- ❌ No grade multipliers (grades are for bulk apply only)
- ❌ No mixing of services and packages in a single confusing table

---

## Current Reference: Standard Admin Page Patterns

Based on analyzing `service-management`, `employee-detail`, `pay-periods`, and 12+ other admin forms, here is the confirmed standard:

### List Page Pattern (used in 12+ admin features)

```html
<div class="cleansia-[feature]">
  <div class="cleansia-[feature]__container page-wrapper">
    <!-- Header: title + description -->
    <div class="cleansia-[feature]__header">
      <div class="header-content">
        <cleansia-title [title]="'pages.[feature].title' | translate" />
        <p class="cleansia-[feature]__description">
          {{ 'pages.[feature].description' | translate }}
        </p>
      </div>
    </div>

    <!-- Filter bar with action buttons on right -->
    <div class="filter-header">
      <div class="filter-chips-inline">
        <!-- Active filter chips go here -->
      </div>
      <div class="header-actions">
        <div class="filter-actions">
          <cleansia-button
            [label]="'filters.title' | translate"
            icon="pi pi-filter"
            (onClick)="openFilterDrawer()"
          />
        </div>
        <cleansia-button
          [label]="'create' | translate"
          icon="pi pi-plus"
          (onClick)="onCreate()"
        />
      </div>
    </div>

    <!-- Data section wrapped in cleansia-section -->
    <cleansia-section>
      @if (facade.initialLoading()) {
        <cleansia-loader />
      } @else {
        <cleansia-table
          [data]="facade.items()"
          [columns]="columns"
          [actions]="actions"
          [config]="{ paginator: true, rows: 20, lazy: true, totalRecords: facade.totalRecords() }"
          [loading]="facade.loading()"
          (pageChange)="onPageChange($event)"
        />
      }
    </cleansia-section>
  </div>
</div>
```

### Form Page Pattern (used in 12+ admin forms)

```html
<div class="cleansia-[feature]-form">
  <div class="cleansia-[feature]-form__container page-wrapper">
    <!-- Header: back button + title -->
    <div class="cleansia-[feature]-form__header">
      <cleansia-button
        [label]="'global.actions.back' | translate"
        icon="pi pi-arrow-left"
        (onClick)="onCancel()"
        [outlined]="true"
      />
      <cleansia-title [title]="pageTitle()" />
    </div>

    @if (facade.loading()) {
      <cleansia-loader />
    } @else {
      <form
        [formGroup]="form"
        (ngSubmit)="onSave()"
        class="cleansia-[feature]-form__content"
      >
        <!-- Group related fields in cleansia-section with a title -->
        <cleansia-section [title]="'basic_info' | translate">
          <div class="form-grid">
            <div class="form-field">
              <cleansia-text-input formControlName="field1" />
            </div>
            <div class="form-field">
              <cleansia-text-input formControlName="field2" />
            </div>
            <div class="form-field full-width">
              <cleansia-textarea formControlName="description" [rows]="3" />
            </div>
          </div>
        </cleansia-section>

        <cleansia-section [title]="'pricing' | translate">
          <div class="form-grid">
            <div class="form-field"><!-- ... --></div>
          </div>
        </cleansia-section>

        <!-- Form actions: cancel + submit -->
        <div class="form-actions">
          <cleansia-button
            [label]="'global.actions.cancel' | translate"
            [outlined]="true"
            (onClick)="onCancel()"
          />
          <cleansia-button
            [label]="'global.actions.save' | translate"
            type="submit"
            [disabled]="facade.saving()"
            [loading]="facade.saving()"
            (onClick)="onSave()"
          />
        </div>
      </form>
    }
  </div>
</div>
```

### Key Rules

| Rule | Why |
|---|---|
| **`form-grid` + `form-field`** for forms | Matches 12+ other admin forms |
| **`info-grid` + `info-item`** ONLY for read-only display or inline edit toggles | This pattern is for `employee-detail` view/edit sections, NOT standard forms |
| Wrap content in `.page-wrapper` on the second-level div | Standard padding/max-width |
| Group related fields in `<cleansia-section [title]="...">` | Visual grouping with consistent styling |
| `.form-field.full-width` for textareas and single-row fields | Grid span override |
| `.form-actions` div at the bottom with cancel (outlined) + save (primary) | Consistent button placement |
| Use `CleansiaSelectComponent`, not native `<select>` | Consistent select styling |
| Use `cleansia-text-input`, `cleansia-textarea`, `cleansia-calendar` | Consistent input styling |

---

## Rebuild Implementation Plan

### Phase 1: Delete & Start Fresh

**Delete** (or archive to a backup branch first):
- `pay-config-form/pay-config-form.component.ts/html/scss`
- `pay-config-form/pay-config-form.facade.ts`
- Keep: `pay-config-management.component.ts/html` (list page is mostly fine, needs minor tweaks)
- Keep: `pay-config-management.facade.ts` (needs to filter to global-only)
- Keep: `pay-config-management.models.ts` (grade template logic to be removed)
- Keep: `admin-pay-config.service.ts`
- Keep: `lib.routes.ts` (routes are fine)

### Phase 2: Backend Adjustments

Check whether `GetPagedPayConfigs` already filters to `EmployeeId IS NULL` by default:
- Yes — already done (set default `globalOnly: true` when no `employeeId` filter)
- No action needed on backend

**Optional**: Add a dedicated endpoint `GET /api/AdminPayConfig/global-rates` for clarity.
Or simpler: ensure the frontend always passes `employeeId: undefined` to get global-only.

### Phase 3: List Page (Minor Tweaks)

File: `pay-config-management.component.html`

Current state: Mostly correct structure, but:
- No filter drawer (optional — can add later if needed)
- `filter-chips-inline` div is empty — fine, it's a placeholder

Changes needed:
1. Update the title translation to "Global Rates" instead of "Pay Configuration"
2. Update description: "Manage platform-wide rate defaults applied to all employees. For per-employee overrides, visit the employee detail page."
3. Add a note/banner explaining: "These are global rates. Per-employee overrides are managed on the employee detail page."
4. Maybe split the table into two sections (Services / Packages) via two `cleansia-section` blocks OR add a type column + filter chip.

### Phase 4: Form Page (Full Rebuild)

File: `pay-config-form/pay-config-form.component.ts` + `.html`

**New TS structure**:
```ts
@Component({
  selector: 'cleansia-admin-pay-config-form',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    TranslatePipe,
    CleansiaButtonComponent,
    CleansiaTextInputComponent,
    CleansiaTextareaComponent,
    CleansiaSelectComponent,
    CleansiaLoaderComponent,
    CleansiaSectionComponent,
    CleansiaTitleComponent,
  ],
  templateUrl: './pay-config-form.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [PayConfigFormFacade],
})
export class PayConfigFormComponent implements OnInit, OnDestroy {
  // mode: 'create' | 'edit'
  // form fields: serviceId, packageId, basePay, extraPerRoom, extraPerBathroom, distanceRatePerKm, minimumPay, maximumPay, currencyId, description
  // NO gradeLevel field
  // NO applyGrade method
}
```

**New HTML structure** (follows the standard form blueprint):
```html
<div class="cleansia-pay-config-form">
  <div class="cleansia-pay-config-form__container page-wrapper">
    <!-- Header -->
    <div class="cleansia-pay-config-form__header">
      <cleansia-button
        [label]="'global.actions.back' | translate"
        icon="pi pi-arrow-left"
        (onClick)="onCancel()"
        [outlined]="true"
      />
      <cleansia-title [title]="pageTitle()" />
    </div>

    @if (facade.loading()) {
      <cleansia-loader />
    } @else {
      <form
        [formGroup]="form"
        (ngSubmit)="onSave()"
        class="cleansia-pay-config-form__content"
      >
        <!-- Target (create only): service OR package -->
        @if (!isEditMode()) {
          <cleansia-section [title]="'pages.pay_config_form.target' | translate">
            <div class="form-grid">
              <div class="form-field">
                <cleansia-select
                  [options]="serviceOptions()"
                  [label]="'pages.pay_config_form.service' | translate"
                  [floatVariant]="'on'"
                  [filter]="true"
                  formControlName="serviceId"
                />
              </div>
              <div class="form-field">
                <cleansia-select
                  [options]="packageOptions()"
                  [label]="'pages.pay_config_form.package' | translate"
                  [floatVariant]="'on'"
                  [filter]="true"
                  formControlName="packageId"
                />
              </div>
              <div class="form-field">
                <cleansia-select
                  [options]="currencyOptions()"
                  [label]="'pages.pay_config_form.currency' | translate"
                  [floatVariant]="'on'"
                  formControlName="currencyId"
                />
              </div>
            </div>
            <p class="form-hint">
              {{ 'pages.pay_config_form.target_hint' | translate }}
            </p>
          </cleansia-section>
        }

        <!-- Pay Rates -->
        <cleansia-section [title]="'pages.pay_config_form.pay_rates' | translate">
          <div class="form-grid">
            <div class="form-field">
              <cleansia-text-input
                [label]="'pages.pay_config_form.base_pay' | translate"
                formControlName="basePay"
                dataType="number"
                [required]="true"
              />
            </div>
            <div class="form-field">
              <cleansia-text-input
                [label]="'pages.pay_config_form.extra_per_room' | translate"
                formControlName="extraPerRoom"
                dataType="number"
              />
            </div>
            <div class="form-field">
              <cleansia-text-input
                [label]="'pages.pay_config_form.extra_per_bathroom' | translate"
                formControlName="extraPerBathroom"
                dataType="number"
              />
            </div>
            <div class="form-field">
              <cleansia-text-input
                [label]="'pages.pay_config_form.distance_rate' | translate"
                formControlName="distanceRatePerKm"
                dataType="number"
              />
            </div>
          </div>
        </cleansia-section>

        <!-- Pay Limits -->
        <cleansia-section [title]="'pages.pay_config_form.pay_limits' | translate">
          <div class="form-grid">
            <div class="form-field">
              <cleansia-text-input
                [label]="'pages.pay_config_form.minimum_pay' | translate"
                formControlName="minimumPay"
                dataType="number"
              />
            </div>
            <div class="form-field">
              <cleansia-text-input
                [label]="'pages.pay_config_form.maximum_pay' | translate"
                formControlName="maximumPay"
                dataType="number"
              />
            </div>
          </div>
          <p class="form-hint">
            {{ 'pages.pay_config_form.pay_limits_hint' | translate }}
          </p>
        </cleansia-section>

        <!-- Description -->
        <cleansia-section [title]="'pages.pay_config_form.description_section' | translate">
          <div class="form-grid">
            <div class="form-field full-width">
              <cleansia-textarea
                [label]="'pages.pay_config_form.description' | translate"
                formControlName="description"
                [rows]="3"
              />
            </div>
          </div>
        </cleansia-section>

        <!-- Form actions -->
        <div class="form-actions">
          <cleansia-button
            [label]="'global.actions.cancel' | translate"
            [outlined]="true"
            (onClick)="onCancel()"
          />
          <cleansia-button
            [label]="
              isEditMode()
                ? ('global.actions.save' | translate)
                : ('global.actions.create' | translate)
            "
            type="submit"
            [disabled]="facade.saving()"
            [loading]="facade.saving()"
          />
        </div>
      </form>
    }
  </div>
</div>
```

**What's removed from the form**:
- ❌ Grade Template section
- ❌ `gradeLevel` form control
- ❌ `applyGrade()` method
- ❌ `GRADE_MULTIPLIERS` import

### Phase 5: Models Cleanup

File: `pay-config-management.models.ts`

Remove:
- `GradeLevel` type
- `GRADE_MULTIPLIERS` constant
- Any grade-related utility functions

Keep:
- `PayConfigListItem` interface
- Table column/action definitions (update column definitions to reflect "global rates" context)

### Phase 6: Facade Cleanup

File: `pay-config-management.facade.ts`

Ensure the list loading always fetches global-only:
```ts
loadPayConfigs(): void {
  this.adminClient.adminPayConfigClient
    .getPaged(
      undefined,  // employeeId — always undefined for global-only
      undefined,  // serviceId filter
      undefined,  // packageId filter
      undefined,  // currencyId filter
      undefined,  // sort
      this.offset(),
      this.limit()
    )
    .subscribe(/* ... */);
}
```

File: `pay-config-form/pay-config-form.facade.ts` — full rewrite with no grade logic.

### Phase 7: i18n Updates

Update keys in all 5 admin i18n files:

```json
{
  "pages": {
    "pay_config_management": {
      "title": "Global Rates",
      "description": "Manage platform-wide rate defaults. Per-employee overrides are managed on the employee detail page.",
      "info_banner": "These are global rates applied to all employees by default. To set per-employee rates, open an employee's detail page and use the Apply Grade Template feature.",
      "create": "Create Global Rate",
      "no_pay_configs": "No global rates configured yet."
    },
    "pay_config_form": {
      "create_title": "Create Global Rate",
      "edit_title": "Edit Global Rate",
      "target": "What this rate applies to",
      "target_hint": "Select a service OR a package, not both. This rate will apply to all employees unless they have a per-employee override.",
      "service": "Service",
      "package": "Package",
      "currency": "Currency",
      "pay_rates": "Pay Rates",
      "base_pay": "Base Pay",
      "extra_per_room": "Extra Per Room",
      "extra_per_bathroom": "Extra Per Bathroom",
      "distance_rate": "Distance Rate (per km)",
      "pay_limits": "Pay Limits",
      "pay_limits_hint": "Optional. Leave as 0 for no limit. If set, the calculated pay will be clamped to this range.",
      "minimum_pay": "Minimum Pay",
      "maximum_pay": "Maximum Pay",
      "description_section": "Notes",
      "description": "Internal notes (optional)"
    }
  }
}
```

Remove any grade-related i18n keys.

### Phase 8: Sidebar Label Update

File: `apps/cleansia-admin.app/src/app/app.component.ts`

Change:
```ts
{ label: 'sidebar.pay_configs', icon: 'pi pi-money-bill', route: '/pay-config-management' }
```

To:
```ts
{ label: 'sidebar.global_rates', icon: 'pi pi-money-bill', route: '/pay-config-management' }
```

And update i18n:
```json
"sidebar": {
  "global_rates": "Global Rates"
}
```

### Phase 9: Verification

- `npx nx build cleansia-admin.app` — must pass clean
- Visual check: list page looks like service-management list page
- Visual check: form page looks like service-form page
- Functional check: create a global rate for a service → verify it appears in the list
- Functional check: edit a global rate → verify changes persist
- Functional check: delete a global rate → verify removal

---

## File Change Summary

| File | Action |
|---|---|
| `pay-config-management.component.html` | Minor tweaks (title, description, info banner) |
| `pay-config-management.component.ts` | Update column/action labels |
| `pay-config-management.facade.ts` | Ensure global-only filter |
| `pay-config-management.models.ts` | Remove `GradeLevel`, `GRADE_MULTIPLIERS` |
| `pay-config-form.component.html` | **Full rewrite** with standard `form-grid`/`form-field` pattern, no grade section |
| `pay-config-form.component.ts` | **Full rewrite** — remove grade logic, import `CleansiaSelectComponent` correctly |
| `pay-config-form.facade.ts` | Simplify — remove grade handling |
| `admin-pay-config.service.ts` | No change needed |
| `lib.routes.ts` | No change needed |
| `app.component.ts` (sidebar) | Change label key to `sidebar.global_rates` |
| Admin i18n files × 5 | Rename `pay_configs` → `global_rates`, rewrite form strings, remove grade keys |

---

## Risks & Notes

1. **Breaking change for anyone already using the feature**: existing pay config records are untouched in DB. UI is new.
2. **"Grade multiplier" confusion**: the existing users (if any) will notice it's gone. Document this in release notes.
3. **Backward compatibility**: backend still supports `employeeId` on `CreatePayConfigCommand` — the rebuilt form simply doesn't use it. Per-employee path is unaffected.
4. **Testing scope**: manual verification is enough since there are no unit tests for this feature today.

---

## Execution Order

1. Rewrite form TS + HTML (Phase 4) — biggest change
2. Update list page text (Phase 3)
3. Clean up models (Phase 5)
4. Clean up facade (Phase 6)
5. Update i18n (Phase 7) — all 5 languages
6. Update sidebar (Phase 8)
7. Build verification (Phase 9)

**Estimated tokens**: ~40-50k (medium-complexity refactor, but scope is narrow — only one feature folder)
**Recommended model**: Sonnet (no novel architecture, just applying known patterns)
