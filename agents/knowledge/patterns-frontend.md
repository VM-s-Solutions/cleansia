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
prefix `cleansia` (not `lib`) to match the `cleansia-*` component selectors above.

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

## Forms — exact idiom

Reactive forms via `FormBuilder.nonNullable.group(...)` with `Validators`, rendered with `cleansia-*`
inputs bound by `formControlName`. Submit calls a facade `create`/`update` that builds a
`Create*Command`/`Update*Command` (generated) and navigates via a route enum on success. Field-level
errors come from `ErrorPipe`; API errors from `SnackbarService.showApiError`.

## Routing

`lib.routes.ts` exports a `Route[]`, list + `create` + `:id/edit`, using `data: { mode, title }` read
in the component via `route.snapshot.data`.

## i18n binding (verified)

Keys live in `apps/<app>/src/assets/i18n/{en,cs,sk,uk,ru}.json`, deeply namespaced
(`pages.company_management.columns.legal_name`). Backend error **codes** map to translations under
`api.*`: the snackbar layer normalizes a code (`OrderNotFound` / `order.not_found`) to a lowercase
alphanumeric key and looks it up via a mapping table (`DEFAULT_SNACKBAR_ERROR_MAPPINGS`) into an
`api.order.not_found`-style key. So when backend adds a `BusinessErrorMessage` constant, add the
matching `api.*` key in **all five** locales. Use `TranslatePipe` in templates, `TranslateService.instant`
in TS.

## What to mirror, not invent

- Extend `UnsubscribeControlDirective`; state in `signal()`; `takeUntil(this.destroyed$)` on every stream.
- Call the generated client wrapper (`adminClient.adminXClient.method()`); never hand-roll HTTP, never
  edit generated files. If a backend DTO changes → ticket carries `manual_step: nswag-regen`; **wait**.
- Use `cleansia-*` components + `cleansia-table` + `getXxxTableDefinition()`. No raw HTML form controls.
- Gate UI with `*cleansiaPermission="Policy.CanXxx"`. Toasts via `SnackbarService`.
- OnPush always; standalone always; facade `providers: [XxxFacade]` on the component.
- Every string via `TranslatePipe`/`TranslateService`, present in all 5 locales. No `any`.
