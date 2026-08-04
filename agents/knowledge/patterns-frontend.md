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

Per-action `loading` is always a **facade-local signal** like this. Never bind a specific button or
form to the store's global `selectLoading` — that flag is flipped by `LoadingInterceptorFn` for
*every* HTTP request in the app, so any slow unrelated call (boot-time code loads, translation
fetches) freezes the control (the admin login button stuck-loading bug).

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

## Customer SSR (`cleansia.app` only) — two traps that both render the wrong page with a 200

`apps/cleansia.app/server.ts` is a hand-written Express host, and the render catch-all is mounted
**path-lessly** (`app.use((req, res, next) => …)`). A path pattern — `app.use('{*path}', …)` — matches
the same requests but makes Express strip the matched segment from `req.url`, so the engine renders
`/` for every deep link and the landing micro-cache serves that home page to every cookie-less GET of
any URL. It answers 200, and a browser hides it by re-routing during hydration, so only a `curl` of a
deep link shows it. Guarded by `apps/cleansia.app/src/app/ssr/server-request-path.spec.ts`.

`RenderMode.Prerender` in `app.routes.server.ts` needs the builder's `outputMode` option, which
`project.json` does not set — without it `prerendered-routes.json` is emitted empty, the engine finds
no document for the route and returns nothing, and Express answers `Cannot GET /<route>`. Use
`RenderMode.Server` for any route that must be server-rendered until `outputMode` is deliberately
adopted. Verify a new server-rendered route by building and curling it, never by opening a browser:
`npm run build:cleansia-customer && (cd dist/apps/cleansia.app && PORT=4400 node server/server.mjs)`,
then check the `<title>` and body text of the response.

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

### Owner-blankable copy — an empty value hides its own block

For a line only the owner can supply or retire (a publication date, a "pending review" banner), give
it its own key with `""` as the shipped value and render it under `@if ('page.key' | translate; as
value)`. ngx-translate only falls back for an **undefined** value, so `""` passes through as falsy and
the block disappears — the owner turns the line on or off by editing five JSON values, with no code
change and no boolean flag in the component. Used by `legal-pages` for `last_updated_date` and
`review_notice`.

### Retiring a claim the product does not deliver — pin the absence, don't just delete it

When copy promises something no code enforces, **remove the claim rather than soften it**, then add a
spec that asserts it is gone. Deleting the render site alone is not enough: the same claim usually
lives on several surfaces plus five locale bundles, and the next person re-wires one of them from the
key that was left behind. The Plus express perk shipped on the subscribe screen, the success screen
and the management card; removing one pill left the other four live for a full release.

The spec asserts three things, on the whole feature rather than the one component you edited:
1. **No value** under the feature's i18n namespace matches the claim's stems **in any of the five
   locales** (not just the retired key names — a re-introduction under a new key must fail too).
2. **The retired keys are absent**, and the five locales still carry identical key sets.
3. **No template renders the claim or branches on the unenforced flag** — read the `.html` files and
   assert on their text.

Reference: `apps/cleansia.app/src/app/i18n/membership-express-claim.spec.ts`, mirroring the mobile
equivalents (`MembershipExpressClaimTest.kt`, `MembershipExpressClaimTests.swift`). Mutation-check it
by re-adding the string before you call it done.

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
- **The guard is per app, because the surface is per app.** The interceptor is shared, so the same
  generic-fallback swallow happens in partner and admin — a partner-only code with no partner
  translation reads as "An error occurred. Please try again." and the cleaner just retries.
  `apps/cleansia-partner.app/src/app/i18n/error-contract-parity.spec.ts` is the partner twin
  (`order.weekly_limit_reached` was missing from all five partner bundles for as long as the code
  existed, with only the customer guard in place). Derive an app's contract mechanically — the
  `BusinessErrorMessage.*` constants referenced by the feature classes **that app's host controllers
  dispatch** — so it can be re-derived rather than remembered. A key that is genuinely not translated
  yet goes on the spec's short `PENDING_TRANSLATION` list, which is a **ratchet**: the spec fails if a
  listed key turns out to be translated (delete the line) or is not a real contract key.
- **Admin ships two error namespaces and only one of them is canonical — write `api.*`.** Its bundle
  carries a legacy `errors.*` block (~169 keys) that mirrors `api.*`, read by the per-feature
  `XXX_ERROR_KEY_MAP` resolvers a few admin features still carry (orders, disputes, refunds,
  referrals). Admin also registers `COMMON_INTERCEPTORS_FN`, so the shared interceptor fires on every
  admin error and resolves `api.${code}` — a new key written only under `errors.*` is therefore
  invisible unless you also hand-write a resolver, which is the thing you are not supposed to add.
  `apps/cleansia-admin.app/src/app/i18n/error-contract-parity.spec.ts` is the admin twin; it guards
  five-locale key-set parity and non-emptiness over **both** namespaces, and holds a contract list
  bounded to the surface derived so far rather than pretending to cover all 31 admin controllers.
  Extend that list when you derive another admin surface.

### A refused row action reconciles the list — a toast alone is a bug

When a row action can be **refused because the world moved on** (two cleaners racing for the same
job), the facade must re-read from the server on the **error branch as well as the success branch**.
Toast-only leaves the row on screen with a live button, so the next click just repeats the toast; both
mobile clients already reconcile (`OrdersListViewModel.kt` / `.swift` invalidate + refetch on
`ApiResult.Error`). The web shape is the C3 pipe with the reload **after** the `if (response)`, not
inside it, plus an in-flight signal so the second click is a no-op:

```ts
this.takeInFlightOrderId.set(orderId);
client.orderClient.takeOrder(new TakeOrderCommand({ orderId }))
  .pipe(takeUntil(this.destroyed$), catchError(() => of(null)),
        finalize(() => this.takeInFlightOrderId.set(null)))
  .subscribe((response) => {
    if (response) this.snackbarService.showSuccessTranslated('…order_taken_success');
    this.loadAvailableOrders();   // both branches
    this.loadMyOrders();
  });
```

Reconcile by **re-reading**, not by hiding the row optimistically: the server filter already drops a
full or already-mine order, while a refusal that is about *you* (`order.weekly_limit_reached`)
correctly leaves the row in place — which is only usable if that message is translated. Feed the
in-flight signal to `TableAction.disabled` so the row is visibly unavailable, and on a detail page let
the re-read own `loading` so the spinner runs unbroken (`[loading]` on `cleansia-button` disables it —
PrimeNG's `p-button` is `[disabled]="disabled || loading"`).

### Other (non-canonical) error-resolution paths — do not add new ones

These predate the canonical path and exist for back-compat; **do not hand-roll new per-feature maps**,
reuse the interceptor `api.*` path instead (EP-3 root cause was the proliferation of bespoke maps):
1. `SnackbarService.showApiError(err, fallbackKey)` normalizes a PascalCase/dot code to a lowercase
   alphanumeric key and looks it up via `DEFAULT_SNACKBAR_ERROR_MAPPINGS`; when no mapping matches it
   tries the raw code **as a translation key** (root-level blocks like `membership.*`/`gdpr.*` mirror
   the code).
2. A few features keep an explicit `XXX_ERROR_KEY_MAP` + `resolveXxxErrorKey(error)` in their
   `*.models.ts` (see `membership-plan-list.models.ts`, `referrals-list.models.ts`, disputes upload).
   Such a resolver must **delegate the code extraction** to the single shared
   `extractApiErrorCode(error): string | undefined` from `@cleansia/services` (the `result.detail ||
   result.title` → `JSON.parse(response)` walk, typed with the one shared `ApiErrorResult`) and keep
   only its own `code → key` map + fallback — never re-implement the extraction inline. The same helper
   backs `SnackbarService.extractApiErrorMessage`.

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

## Building a generated DTO — construct-then-assign, never an object literal (ADR-0031)

**ADR-0031 is the source of truth** for why: the derivation, the `markOptionalProperties: false`
consequence, the three `master` breaks it is drawn from, and the scope of the rule all live there.
This entry is the call-site form only.

```ts
const photo = new BlobFileDto();
photo.fileName = file.name;
photo.base64Content = base64Content;
photo.contentType = file.type;
```

not `new BlobFileDto({ fileName, base64Content, contentType })`. Only the **literal** is
regen-fragile — the constructor's parameter is typed `IBlobFileDto`, so a literal is checked for
completeness while property assignment is not. Passing an already-built **instance** into an
enclosing DTO (`new SaveOrderPhotosPhotoToSave({ file: blobFile, … })`) is fine.

Two things that bite in practice: you **cannot** pre-add the field ahead of a regen (`blobUrl:
undefined` in a literal fails today's excess-property check, `TS2353`), and a lambda-parameter
default cannot hold the statements — extract a module-level factory (`const createEmptyPhoto =
(): BlobFileDto => { … }`).

The same factory is the answer when **one DTO shape is built at several call sites** (the booking
wizard built `AddressDto` in five places, all coalescing blanks to `''`). Export one
`createXxxDto(fields = {}): XxxDto` beside the feature's models, take an **all-optional plain
object** rather than the generated `IXxxDto`, and assign inside. A regen that adds a member leaves
it compiling and unset; a regen that removes one breaks in **one** place instead of five. Specs
build the DTO through the same factory — `.spec.ts` files are excluded from every app's
`tsconfig.app.json`, so `npm run typecheck` never sees a literal in a test (ADR-0031 residue #5) and
a regen reddens the Jest run instead.

**Enforced by:** `no-restricted-syntax` (selector
`NewExpression[callee.name=/(Command|Request|Dto|Query)$/][arguments.0.type='ObjectExpression']`) in
`src/Cleansia.App/eslint.generated-dto.config.mjs` — **T2-ADVISORY**, because `frontend-ci.yml` runs
lint with `continue-on-error: true`; promotes to `T1-CI` with the rest of the lint baseline. It is an
**opt-in ratchet, and the opt-in list is the progress bar**: a scope may only be added once its own
count is zero, and it is added by spreading `generatedDtoLiteralRules()` into that lib's own
`eslint.config.mjs` (flat-config `files` globs resolve against the loaded config's directory, so a
per-lib config passes no argument) or, for a lib with no local config, by adding a workspace-relative
glob to the call in `src/Cleansia.App/eslint.config.mjs`. Cleared so far: all of `libs/core`,
`libs/data-access`, and `libs/cleansia-customer-features/order-wizard`. **Never delete a scope from
that list to make a new literal compile** — convert the call site instead.

**Removal is the same rule, mirrored.** When the backend *drops* a field, a literal stops compiling
against the still-stale client (`TS2345`, "property X is missing") — construct-then-assign simply
omits it and compiles against both the current and the post-regen client. This is what lets a
contract-narrowing fix land in one change instead of being blocked on the owner's regen.

When a ticket carries `manual_step: nswag-regen`, sweep the call sites into this form **before** the
owner regenerates; that work needs no regenerated client and unblocks the regen.

**Deleting the assignment is only half a field removal — follow the value to its form control.** The
compiler names the read (`employee.iban`) and the write (`command.iban = …`) and nothing else, so a
"green build" is not the finish line. A dropped field usually also has a `FormControl` with
`Validators.required`, and once the server stops sending the value that control can never be
satisfied: the form is permanently `invalid`, `onSubmit`'s `if (!formGroup.valid) return` fires every
time, and the user is silently locked out of saving anything on that page. Nothing type-checks that.
So for each removed field, delete in this order — the mapper read, the command write, **the form
control and its validators**, the input that binds `formControlName`, and any read-only display of the
same value. If that leaves a section component with no controls, delete the component too: an
`@Input`-fed section still bound to `formControlName="<gone>"` throws *"Cannot find control with
name"* the moment anything renders it. Leave the i18n keys — the follow-up feature reuses them.

**Mirrored on the add side: a required field the regen added must round-trip, not default.** The
generated command is a whole-resource PUT, so an update path that omits the new member sends its
type default and **overwrites** the stored value (`expressUpgradesPerMonth` → `0` wipes a plan's
express-waiver quota through `UpdateBenefits`). Assigning the field is what unbreaks the build;
carrying the loaded detail's value through the form is what stops the fix from being a data-loss bug.
When the visible input needs copy you cannot add in that lane, still add the control and populate it
from the detail — an unrendered round-tripping control is honest, a defaulted one is destructive.

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

## Dev API base URL — relative on purpose (one-origin cookie auth)

Auth is an HttpOnly cookie with `SameSite=Strict`, so the browser must see **one origin**. Dev
`environment.ts` sets `apiBaseUrl: ''` and the dev server proxies `/api` server-side
(`apps/<app>/proxy.conf.json` → local API; `--configuration=devremote` →
`proxy.devremote.conf.json` → the deployed dev API). All three auth interceptors treat a relative
URL containing `/api/` as "our API". Never point dev `apiBaseUrl` at an absolute host to fix a 401 —
that reintroduces the cross-site cookie failure — and never weaken the cookie attributes. The
customer app is SSR: its server render resolves the relative base against the incoming request
origin in `app.config.server.ts` (via the `REQUEST` token), so SSR fetches also flow through the
proxy. Full run-mode docs: `src/Cleansia.App/CLAUDE.md`.

## Brand mark — `cleansia-brand-name` is the whole lockup

The mark is the wordmark taken from that app's own iOS art (owner ruling, T-0444). It already contains
the name, so `cleansia-brand-name` renders the image **alone** — never beside a `<h2>Cleansia</h2>` or
a `<span>Cleansia</span>`, which would print the word twice. A suffix the artwork does *not* carry
(admin's "Admin") is fine; the brand word itself is never repeated.

**Customer and admin ship the same "Cleansia" wordmark; partner ships the stacked "Cleansia Partner"
lockup** — because the partner iOS app has its own lockup and the partner web app is the same product.
Do not "fix" partner back to match: `assets/logos/Logo.{webp,png,ico}` is a per-app path resolved by
each app's own assets folder precisely so an app can differ, and the spec pins the three shapes
(`616×112`, `616×112`, `616×172`) rather than a single digest.

Two consequences of the marks differing in *shape*, both handled without per-app markup:

- **Sizing is by width, so the word "Cleansia" is the same size everywhere.** That line spans the full
  width of both lockups (measured aspect 5.4870 customer / 5.4646 partner), so equal width means equal
  wordmark; the partner mark is simply taller. Never size the marks to equal height — that would
  shrink the partner brand.
- **The box's aspect is a CSS variable, `--cleansia-brand-aspect`**, defaulted in the shared stylesheet
  and overridden in `apps/cleansia-partner.app/src/styles.scss`. An `<img>` whose ratio only arrives
  with the bytes is a layout shift, and the `width`/`height` attributes live in a *shared* template
  that cannot know which app it is in.
- **The alt text comes from the app's own i18n bundle** (`components.brand_mark_alt`), for the same
  reason and by the same trick: per-app resolution with no per-call-site plumbing. An input would not
  reach the sidebar, which partner and admin share.

Generally: when a shared component must differ per app, reach for something the app already resolves
for itself — its `assets/` folder, its i18n bundle, a CSS custom property in its `styles.scss`. A DI
token works too but `app.config.ts` statically importing `@cleansia/components` trips
`enforce-module-boundaries` (that lib is lazy-loaded) and pulls the whole barrel in eagerly.

Its only variant input is `compact` — the collapsed sidebar rail, where the mark shrinks rather than
being cropped or swapped.

Two properties to keep when touching any brand asset. Both are **enforced**, not advised, by
`cleansia-brand-name.component.spec.ts` — it reads the shipped files off disk, so regenerating an
asset wrongly goes red in `nx test components`:

- **The bytes must match the extension.** The spec checks magic bytes (PNG signature / `RIFF`+`WEBP` /
  ICO reserved+type), because a PNG served as `.webp` under `<source type="image/webp">` is a false
  MIME claim in markup and it shipped that way for months before T-0444.
- **The mark is drawn in exactly one ink, and that ink is `--cleansia-primary`.** The spec decodes
  `Logo.png` and diffs its single visible colour against `variables.scss`, so a re-theme that forgets
  the logo fails instead of drifting.

And one rule for the shape: **size a non-square logo by width, not height** —
`width: Npx; max-width: 100%; height: auto`. A fixed `height` plus `max-width` squashes a replaced
element horizontally when the container is narrow (CSS 2.1 §10.4); at 1:1 you never notice, at 5.5:1
it is immediate. Note the workspace is **content-box** (no universal `border-box` reset; PrimeFlex only
sets it on `.grid > .col`), so a rail's usable width is its `width` minus its own padding and border.

## Heading rank is not the type scale — `cleansia-title` takes `level`

`cleansia-title`'s `size` picks the visual scale *and*, by default, the heading tag
(`large→h1, big→h2, default→h3, small→h5`). Do not reach for a bigger `size` to get a better outline:
`--large` is `3rem` against `--default`'s `1.5rem`, and page stylesheets key off the
`.cleansia-title--<size>` class, so changing size silently changes both type and styling. Pass
`[level]="1"` instead — a page's own title is the `h1` whatever size it is drawn at. Leave `level` off
and nothing changes. Every auth screen carries `[level]="1"` for exactly this reason (T-0444).
