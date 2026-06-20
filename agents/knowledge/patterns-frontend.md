# Frontend Patterns (Angular 19 / Nx / NgRx / PrimeNG) — REAL TYPES

The concrete "how we write frontend code" catalog, bound to the **actual shared types and components
in this repo** (verified from source). Read this + [`conventions.md`](./conventions.md) before
touching `.ts`/`.html`. **Reuse these exact components and the exact facade idiom — never invent
parallel ones.** Authoritative prose:
[`../../docs/architecture/frontend.md`](../../docs/architecture/frontend.md).

> **Binding rule for every frontend agent:** before writing a feature, open the nearest existing
> feature of the same kind (a list or a form) in the same `libs/cleansia-*-features/` area and mirror
> it exactly. The samples below are copied from live code (admin `company-management`).

---

## The exact shared types (import these aliases)

| Concept | Exact symbol | Import alias |
|---|---|---|
| Facade base | `UnsubscribeControlDirective` (provides `destroyed$: Subject<void>`) | `@cleansia/directives` |
| Permission gate | `*cleansiaPermission="Policy.CanXxx"` (`CleansiaPermissionDirective`) | `@cleansia/directives` |
| Snackbar/toasts | `SnackbarService` (`showSuccess`, `showError`, `showApiError`) | `@cleansia/services` |
| Route enums | `CleansiaAdminRoute` / partner / customer route consts | `@cleansia/services` |
| Policy names | `Policy.CanXxx` (mirrors backend) | `@cleansia/services` |
| Generated API client | `AdminClient` / `PartnerClient` / `CustomerClient` (wrapper of sub-clients) | `@cleansia/admin-services` / `…/partner-services` / `…/customer-services` |
| Generated DTOs | `*ListItem`, `*DetailDto`, `Create*Command`, `Update*Command`, `*Response`, `PagedData<T>`, `SortDefinition`, `SortDirection` | same generated lib |
| Table | `cleansia-table` + `TableColumn<T>`, `TableAction<T>`, `TableConfig`, `PaginationState`, `SortEvent` | `@cleansia/components` |
| Form/UI primitives | `cleansia-button`, `cleansia-text-input`, `cleansia-select`, `cleansia-section`, `cleansia-title`, `cleansia-loader`, `cleansia-calendar`, `cleansia-multiselect`, `cleansia-checkbox`, `cleansia-telephone`, `cleansia-file`, … (31 total) | `@cleansia/components` |
| Error pipe | `ErrorPipe` | `@cleansia/pipes` |
| Translate | `TranslatePipe` (template), `TranslateService` (`.instant(...)`) | `@ngx-translate/core` |

Generated clients are called via the wrapper, e.g. `adminClient.adminCompanyClient.getPaged(...)`.
**Never** hand-roll `HttpClient` URLs and never edit generated files under `libs/core/services`.

---

## The facade — exact idiom (from `company-info-list.facade.ts`)

State is **signals**. The facade **extends `UnsubscribeControlDirective`** and every client call uses
`takeUntil(this.destroyed$)` + `catchError(() => of(null))` + `finalize(...)`:

```ts
@Injectable()
export class CompanyInfoListFacade extends UnsubscribeControlDirective {
  private readonly adminClient = inject(AdminClient);
  private readonly snackbarService = inject(SnackbarService);
  private readonly translate = inject(TranslateService);
  private readonly router = inject(Router);

  readonly companyInfos = signal<CompanyInfoListItem[]>([]);
  readonly loading = signal(false);
  readonly initialLoading = signal(true);
  readonly totalRecords = signal(0);

  loadCompanyInfos(): void {
    this.loading.set(true);
    this.adminClient.adminCompanyClient
      .getPaged(/* filter */ undefined, undefined, this.currentSort(), this.currentOffset(), this.currentLimit())
      .pipe(takeUntil(this.destroyed$), catchError(() => of(null)), finalize(() => this.loading.set(false)))
      .subscribe((response) => {
        if (response) { this.companyInfos.set(response.data ?? []); this.totalRecords.set(response.total ?? 0); }
        if (this.initialLoading()) this.initialLoading.set(false);
      });
  }

  deleteCompanyInfo(row: CompanyInfoListItem): void {
    if (!row.id) return;
    this.adminClient.adminCompanyClient.delete(row.id)
      .pipe(takeUntil(this.destroyed$), catchError(() => of(null)))
      .subscribe((res) => {
        if (res) {
          this.snackbarService.showSuccess(this.translate.instant('pages.company_management.messages.delete_success'));
          this.loadCompanyInfos();
        }
      });
  }
}
```

`UnsubscribeControlDirective` is literally:

```ts
@Directive()
export abstract class UnsubscribeControlDirective implements OnDestroy {
  destroyed$ = new Subject<void>();
  ngOnDestroy() { this.destroyed$.next(); this.destroyed$.complete(); }
}
```

**Rules confirmed:** state is `signal<T>()` (never `BehaviorSubject`); the facade is `@Injectable()`
and **provided on the component** (`providers: [XxxFacade]`); every stream is `takeUntil(this.destroyed$)`;
API errors surface through `SnackbarService`.

## The component — exact idiom (from `company-info-list.component.ts`)

Standalone, **OnPush**, facade provided locally, table columns/actions built by a
`get<X>TableDefinition()` function, `Policy` exposed for the permission directive:

```ts
@Component({
  selector: 'cleansia-admin-company-info-list',
  standalone: true,
  imports: [CommonModule, CleansiaButtonComponent, CleansiaTextInputComponent, TranslatePipe,
            CleansiaTableComponent, CleansiaTitleComponent, CleansiaLoaderComponent,
            CleansiaSectionComponent, ReactiveFormsModule, CleansiaPermissionDirective],
  templateUrl: './company-info-list.component.html',
  providers: [CompanyInfoListFacade, ConfirmationService],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CompanyInfoListComponent {
  protected readonly facade = inject(CompanyInfoListFacade);
  protected readonly Policy = Policy;                       // for *cleansiaPermission
  companyColumns!: TableColumn<CompanyInfoListItem>[];
  companyActions!: TableAction<CompanyInfoListItem>[];
  // builds columns/actions via getCompanyInfoTableDefinition({ onEdit, onDelete }, translate, statusTemplate)
}
```

Template uses `cleansia-*` + `cleansia-table` (lazy/server paging) + `*cleansiaPermission` + `TranslatePipe`:

```html
<cleansia-title [title]="'pages.company_management.title' | translate" />
<cleansia-button *cleansiaPermission="Policy.CanCreateCompanyInfo"
  [label]="'pages.company_management.create_company' | translate" icon="pi pi-plus" (onClick)="create()" />

@if (facade.initialLoading()) { <cleansia-loader /> } @else {
  <cleansia-table
    [data]="facade.companyInfos()" [columns]="companyColumns" [actions]="companyActions"
    [config]="{ paginator: true, rows: 20, lazy: true, totalRecords: facade.totalRecords(), emptyMessage: 'pages.company_management.no_companies' }"
    [loading]="facade.loading()" (pageChange)="onPageChange($event)" (sortChange)="onSortChange($event)" />
}
```

Filter-drawer backdrops must be the lint-clean a11y variant (from `partner-features/orders`):
`role="button" tabindex="0" (click)="closeFilterDrawer()" (keydown.escape)="closeFilterDrawer()"
[attr.aria-label]="'global.close' | translate"` — a bare `(click)` div fails
`click-events-have-key-events` / `interactive-supports-focus`. Lib eslint configs use selector
prefix `cleansia` (not `lib`) to match the `cleansia-*` component selectors above — and the Nx
generator default (`nx.json` `generators` → `@nx/angular:library`/`@nx/angular:component`
`prefix: 'cleansia'`) is set so a freshly scaffolded lib/component is born compliant. Every component
selector is `cleansia-*` (route shells and feature components alike, e.g.
`cleansia-admin-order-detail`, `cleansia-admin-payroll-ops`). The only `app-*` selectors live in the
app shells (`app-root` in each app's `index.html`, the lazy `app-unauthorized` route shell); the admin
app's eslint config therefore allows `prefix: ['app', 'cleansia']` — `app` for the framework shell,
`cleansia` for everything else.

## Table config — exact idiom (`*.models.ts`)

A `models.ts` exports a **function** returning `{ columns, actions }` typed with `TableColumn<T>` /
`TableAction<T>`:

```ts
export function getCompanyInfoTableDefinition(
  defs: { onEdit: (r: CompanyInfoListItem) => void; onDelete: (r: CompanyInfoListItem) => void },
  translate: TranslateService, statusTemplate?: TemplateRef<CompanyInfoListItem>,
): { columns: TableColumn<CompanyInfoListItem>[]; actions: TableAction<CompanyInfoListItem>[] } {
  return {
    columns: [
      { id: 'legalName', field: 'legalName', header: translate.instant('pages.company_management.columns.legal_name'), sortable: true, width: '20%' },
      { id: 'isActive', field: 'isActive', header: translate.instant('pages.company_management.columns.status'), customTemplate: statusTemplate, width: '10%' },
    ],
    actions: [
      { icon: 'pi pi-pencil', tooltip: translate.instant('…edit'), color: 'warning', onClick: (r) => defs.onEdit(r) },
      { icon: 'pi pi-trash',  tooltip: translate.instant('…delete'), color: 'danger',  onClick: (r) => defs.onDelete(r) },
    ],
  };
}
```

When a row action depends on state the list DTO does **not** carry (e.g. service/package
`ListItem`s have no `isActive`), don't guess per row — pass a getter for the list's current filter
into the table definition and drive `visible` off it
(`visible: () => defs.getIsActiveFilter() !== false`), showing both toggle directions when the
filter is "All" (the backend activate/deactivate commands are idempotent, so a redundant click is
harmless). See `service-management.models.ts` / `package-management.models.ts`.

## Forms — exact idiom

Reactive forms via `FormBuilder.nonNullable.group(...)` with `Validators`, rendered with `cleansia-*`
inputs bound by `formControlName`. Submit calls a facade `create`/`update` that builds a
`Create*Command`/`Update*Command` (generated) and navigates via a route enum on success. Field-level
errors come from `ErrorPipe`; API errors from `SnackbarService.showApiError`.

## Routing

`lib.routes.ts` exports a `Route[]`, list + `create` + `:id/edit`, using `data: { mode, title }` read
in the component via `route.snapshot.data`.

## Selector-driven detail (master select → dependent load)

When a screen is "pick X in a `cleansia-select`, load the data for X" (e.g. partner `period-pay`:
pick a pay period → load its pay summary), the component owns a bare `FormControl<string | null>`
and hands it to the facade once (`facade.connectPeriodControl(control)` in `ngOnInit`). The facade
subscribes to `valueChanges` (takeUntil `destroyed$`) to drive the dependent load, and when **it**
auto-selects (e.g. newest item after the list loads) it syncs the control back with
`control.setValue(id, { emitEvent: false })` so the select displays the selection without
re-triggering the load. Same family as the invoices `bindFormChanges` idiom — the control lives in
the component, every subscription lives in the facade.

## i18n binding (verified)

Keys live in `apps/<app>/src/assets/i18n/{en,cs,sk,uk,ru}.json`, deeply namespaced
(`pages.company_management.columns.legal_name`). Use `TranslatePipe` in templates,
`TranslateService.instant` in TS.

### Error-contract → i18n: the one canonical path is the interceptor `api.*` namespace

The single canonical mechanism for surfacing a backend `BusinessErrorMessage` to the user is the
shared `HttpErrorInterceptorFn` (`libs/core/services/.../interceptors/http-error.interceptor.ts`). It
fires for **every** non-404/non-403 error, pulls the first `BusinessErrorMessage` dot-value out of the
response body (`order.cancellation_window_closed`), and resolves it as **`api.${dotValue}`** — i.e. it
looks up `api.order.cancellation_window_closed` against the deeply-nested `api.*` block. So when the
backend adds a customer-reachable `BusinessErrorMessage` constant, add the matching `api.*` key (the
**full dot path**, nested) in **all five** locales.

Note the canonical namespace is **`api.*`**, not `errors.*`. `conventions.md` historically phrased the
rule as `errors.*`; the live customer interceptor uses `api.${code}`. **Follow the code: `api.*`.**

**Hard parity rule (enforced by a CI guard):**
- Every customer-reachable backend error key must have a non-empty translation under `api.*` in all
  five customer locales, with **identical `api.*` key sets** across the five files.
- The guard is `apps/cleansia.app/src/app/i18n/error-contract-parity.spec.ts`. It holds the explicit
  customer-surface key contract (the dot-values a Customer API endpoint can return), asserts each
  resolves under `api.*` in `en` and in all five, cross-checks the five files' `api.*` key sets are
  identical, and asserts every contract key is a real `BusinessErrorMessage` value. Adding a new
  customer-surface error → add its key to the contract + all five locales, or the guard fails.
- **Unknown/unmapped key → generic fallback, never the raw key.** The interceptor never lets a machine
  key reach the snackbar: if `instant('api.<code>')` echoes the key back (no translation), it falls
  back to `api.common.error_occurred`. Pinned by `http-error.interceptor.spec.ts`.

### Other (non-canonical) error-resolution paths — do not add new ones

These predate the canonical path and exist for back-compat; **do not hand-roll new per-feature maps**,
reuse the interceptor `api.*` path instead (EP-3 root cause was the proliferation of bespoke maps):
1. `SnackbarService.showApiError(err, fallbackKey)` normalizes a PascalCase/dot code to a lowercase
   alphanumeric key and looks it up via `DEFAULT_SNACKBAR_ERROR_MAPPINGS`; when no mapping matches it
   tries the raw code **as a translation key** (root-level blocks like `membership.*`/`gdpr.*` mirror
   the code).
2. A few features keep an explicit `XXX_ERROR_KEY_MAP` + `resolveXxxErrorKey(error)` in their
   `*.models.ts` (see `membership-plan-list.models.ts`, `referrals-list.models.ts`, disputes upload).

## What to mirror, not invent

- Extend `UnsubscribeControlDirective`; state in `signal()`; `takeUntil(this.destroyed$)` on every stream.
- Call the generated client wrapper (`adminClient.adminXClient.method()`); never hand-roll HTTP, never
  edit generated files. If a backend DTO changes → ticket carries `manual_step: nswag-regen`; **wait**.
- Use `cleansia-*` components + `cleansia-table` + `getXxxTableDefinition()`. No raw HTML form controls.
- Gate UI with `*cleansiaPermission="Policy.CanXxx"`. Toasts via `SnackbarService`. For data-driven
  menus where a structural directive can't attach (the app-shell sidebar), set
  `SidebarMenuItem.permission: Policy.CanXxx | Policy[]` — same `PermissionService` engine,
  any-of semantics for an array.
- OnPush always; standalone always; facade `providers: [XxxFacade]` on the component.
- Every string via `TranslatePipe`/`TranslateService`, present in all 5 locales. No `any`.
- Cross-app HTTP concerns live as `HttpInterceptorFn`s in `libs/core/services/src/lib/interceptors/`
  and join `COMMON_INTERCEPTORS_FN` — all three apps inherit with zero `app.config.ts` edits. Array
  order = chain order: a later entry is closer to the backend, so its errors are seen first (the 429
  `RetryAfterInterceptorFn` sits after `HttpErrorInterceptorFn` so the snackbar fires only once the
  back-off retry is exhausted). Customer is SSR — guard wait/retry logic with `isPlatformServer`.

## Module boundaries — the per-app client is the only client a feature may import

Each app owns its **own generated client lib**: `@cleansia/customer-services`
(`libs/core/customer-services`), `@cleansia/partner-services` (`libs/core/partner-services`),
`@cleansia/admin-services` (`libs/core/admin-services`). They are generated from **per-host OpenAPI
specs** — the partner spec is regenerated independently of customer, so a customer feature that imports
`@cleansia/partner-services` silently compiles against the wrong contract and a partner-only regen (or
a removed partner endpoint) can break/skew customer flows. **A customer feature imports only
`@cleansia/customer-services`; partner only partner; admin only admin.** The shared `@cleansia/services`
(`libs/core/services`, `scope:shared`) is app-agnostic and fine for everyone.

This is enforced by `@nx/enforce-module-boundaries` (`eslint.config.mjs`) on a **scope tag** scheme
(each `project.json` carries a `scope:*` and a `type:*` tag):

| Tag | Applied to |
|---|---|
| `scope:customer` / `scope:partner` / `scope:admin` | each app's feature libs, its `*-services` client lib, its `*-stores` data lib |
| `scope:shared` | cross-app libs (`components`, `directives`, `pipes`, `services`, `models`, `utils`, `assets`) |
| `type:feature` / `type:ui` / `type:data` / `type:util` | feature / shared-UI / NgRx-store / client-or-helper libs |

The constraints read: `scope:customer → [scope:customer, scope:shared]` (and the same for partner/admin),
plus the orthogonal `type:*` rules. A cross-app client import is therefore a **lint error**
("A project tagged with `scope:customer` can only depend on libs tagged with `scope:customer`,
`scope:shared`"), caught by `nx lint` in CI. When you add a lib, tag it (`scope` + `type`) in its
`project.json` or it falls outside the guard. The `*-services` index barrels
(`libs/core/<app>-services/src/index.ts`) are **hand-maintained** (not generated — NSwag only emits
`client/<app>-client.ts`); re-exporting an already-generated DTO through the barrel is normal frontend
work, **not** a `nswag-regen` step.
