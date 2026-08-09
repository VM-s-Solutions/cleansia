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

**Live input normalization lives in the facade, on `valueChanges`** — `control.setValue(normalized,
{ emitEvent: false })`, the `promo-code-form` uppercase idiom, extended by the partner bank-details
form (digits-only account fields, uppercase IBAN/SWIFT). Two things to know before you write one:

- Keep the rule **presentational** (strip separators, uppercase). A checksum or a length cap here is a
  second copy of a server rule, and where the server gates money a client that disagrees is the bug
  (ADR-0034 D4 — the ADR's own draft had the CZ mod-11 direction backwards and would have rejected 91%
  of real accounts).
- `cleansia-text-input` renders through `[value]="innerValue"`, and a property binding only touches the
  DOM when the expression differs from **what Angular last wrote**, not from what the element holds. So
  a normalizer that *rejects the character just typed* produces a model and a screen that disagree.
  `writeValue` therefore pushes the value onto the native input when they differ, the way
  `DefaultValueAccessor` does; pinned by `cleansia-text-input.component.spec.ts`. Don't reintroduce a
  binding-only write, and remember the trap if you add the same normalization to another CVA.

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

### …and when the mechanism ships, INVERT that spec — a scan that permits any wording is not a guard

The claim comes back the day the code makes it true, and the absence spec goes red. That is the
tripwire working, and the fix is **not** to delete it: point the same file at the claim's *content*.
Three properties survive the inversion, and each is a real defect the old copy shipped:

1. **The false sentence stays banned, by regex, in every locale** (the express perk's was
   *"same-day"*, which describes a window the code does not implement). A locale-by-locale ban is
   the only form that catches a translator putting it back in one file.
2. **A per-plan configurable number stays out of the copy.** Assert it by **shape, not by value**:
   erase `{{placeholders}}` and the constants the copy legitimately names (the rate `20 %`, the
   window `2…4`), then require **no digit to remain**. A bare allow-list of "the digits 2, 4 and 20
   are fine" reads as a quota check and is not one — it happily passes *"2 free express bookings a
   month"*, which is exactly the hardcoded quota you are guarding.
3. **The claim is gated on a server field on every screen that renders it**, and any count in it is
   the server's, rendered verbatim — assert both by reading the `.html` (`allowsExpressUpgrade`,
   `expressSurchargeWaived()`, `count: facade.expressUpgradesRemaining()`), plus a negative
   assertion that no template does arithmetic on the count.

Keep the five-locale key-set parity assertion from the absence version; add a non-empty check on
each new key, so "the perk is advertised again" is pinned rather than assumed. Mutation-prove the
inverted spec with one mutation per property — the copy going false, the number going hard, the gate
going away — not just by deleting a key.

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

**Hard parity rule** (tier and enforcer declared at the foot of this list):
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
  translation reads as "An error occurred. Please try again." and the cleaner just retries
  (`order.weekly_limit_reached` was missing from all five partner bundles for as long as the code
  existed, with only the customer guard in place).
- **The contract is derived, and the derivation is the test — not a procedure anyone runs.** Each spec
  walks its own host's `Controllers/*.cs`, resolves every `Mediator.Send` argument back to a feature
  class, and reads that class's `BusinessErrorMessage` constants (partner spec `:165-232`). A derived
  key must land on the app's roster or on an exclusion list, or *"leaves no derived key unclassified"*
  fails (`:471-479`) — so a key added to a new endpoint arrives on its own. **An unresolved dispatch
  site is itself a failure** (`expect(surface.unresolved).toEqual([])`, `:458-460`): a controller idiom
  the resolver cannot read reddens the build instead of silently shrinking the derived set. Floors on
  controllers / sites / feature classes / keys (`:451-456`) stop an empty walk from agreeing with an
  empty roster.
- **Three named lists, three different jobs — do not merge them.** The **roster** is the contract: every
  entry non-empty in all five locales. **`DELIBERATELY_NOT_TRANSLATED`** is the *only* escape from the
  coverage test and each entry carries its own reason — today, two Stripe-webhook guards on the customer
  host whose refusals are returned to a Stripe **server** and never rendered (customer spec `:456-470`).
  It cannot silence a real gap: the same assertion rejects an entry that is not actually reachable, is
  also on the roster, is in fact translated, or carries an empty reason (partner spec `:481-503`).
  **`DECLARED_BUT_NEVER_EMITTED`** is an exact two-way set of roster keys no handler names — 20 on
  customer (`:483-504`, of which the seven `promo.*` are alive but travel a different road: the enum
  name rides the `ValidatePromoCode` **response** and the facade maps it), empty on partner (`:431`) and
  admin (`:451`) — so a constant that dies must be listed and one that revives must leave.
  **`PENDING_TRANSLATION`** (partner only, `:438`) is the shrink-only ratchet for a key genuinely not
  translated yet: the spec fails if a listed key turns out to be translated, or is not a real roster key.
- **Two readings of a controller that are wrong, both already paid for.** `ProducesResponseType` is
  **not** a dispatch — reading it as one pulled `CreateOrder` onto the *partner* surface through a
  single stale attribute and injected fifteen false keys. Only `[From…] X.Command` binding and
  `HandleResult<X.Response>` are read alongside `Mediator.Send` (partner spec `:197-200`), because both
  are tied to the message the action actually sends. *(The stale attribute was a real shipped defect —
  `PUT /api/Employee/UpdateEmployee` declared `CreateOrder.Response` while returning
  `UpdateEmployee.Response`, and NSwag typed the client from it. Fixed at
  `Cleansia.Web.Partner/Controllers/EmployeeController.cs:43` and now guarded across all five hosts by
  `DeclaredSuccessResponseMatchesTheReturnedTypeTests` — `:36-43`.)* And do **not** segment a controller
  per `[HttpX]` action: resolution belongs at the `Mediator.Send` site, bound to the nearest declaration
  above it (`:108-145`), because action bodies routinely delegate elsewhere
  (`Cleansia.Web.Customer/Controllers/SavedAddressController.cs:19-52` — five actions, every one a
  one-line call into a helper).
- **Admin ships two error namespaces and only one of them is canonical — write `api.*`.** Its bundle
  carries a legacy `errors.*` block (~169 keys) that mirrors `api.*`, read by the per-feature
  `XXX_ERROR_KEY_MAP` resolvers a few admin features still carry (orders, disputes, refunds,
  referrals). Admin also registers `COMMON_INTERCEPTORS_FN`, so the shared interceptor fires on every
  admin error and resolves `api.${code}` — a new key written only under `errors.*` is therefore
  invisible unless you also hand-write a resolver, which is the thing you are not supposed to add.
  `apps/cleansia-admin.app/src/app/i18n/error-contract-parity.spec.ts` is the admin twin; it guards
  five-locale key-set parity and non-emptiness over **both** namespaces, and its roster is derived from
  every `Cleansia.Web.Admin/Controllers/*.cs` (31 files). **A partial admin contract list is no longer
  permitted** — the coverage assertion above forbids it.

**Enforced by:** `apps/*/src/app/i18n/error-contract-parity.spec.ts` — **T1-CI**, `frontend-ci.yml`'s
*"Unit tests (affected)"* step (`:85-87`), which unlike that workflow's lint step is not
`continue-on-error`. **Two boundaries the green does not cover, and both are by design.** (1) The walk
reads `<host>/Controllers/*.cs` only (partner spec `:33-36`), so a dispatch that lives on a shared base
controller under `Cleansia.Config/Abstractions/` is never a *site* at all; those keys stay on the
hand-kept roster, which is a superset of the derived set on purpose — the roster carries the
reachability judgement the walk cannot make. (2) That workflow's push trigger is paths-scoped to
`src/Cleansia.App/**` (`frontend-ci.yml:12-17`) and the test step is `nx affected`, so a **backend-only**
change that adds a constant is caught by the next frontend-touching PR rather than by the one that
opened the gap.

The three specs **duplicate** the ~150-line derivation instead of sharing it, deliberately: each app's
`tsconfig.spec.json` `include` is limited to its own `src/**/*.spec.ts`
(`apps/cleansia-partner.app/tsconfig.spec.json:11-16`), and a `libs/shared/*` home would put `fs`/`path`
tree-walking into a browser-targeted library. Same per-app placement as
`apps/*/src/app/theme/font-stack.spec.ts` below, for a second and independent reason.

### "This optional resource does not exist yet" arrives as a failure — normalize it once, at the client lib

Some reads answer **400 with a business code** for the ordinary first-visit case
(`Employee/GetMyPayoutDetails` → `payout.not_found` for a cleaner who has never saved payout details,
ADR-0034 D8.2). Left alone that is two bugs at once: the facade cannot tell "nothing saved yet" from
"the network is broken", and the shared interceptor toasts a red error on a screen that should simply
open empty. Both halves are fixed **once**, never per component — this is the same normalization the
mobile repositories do at their data boundary (`ProfileRepositoryImpl.getPayoutDetails`,
`PartnerProfileClient.getMyPayoutDetails`):

1. **A hand-written service in the app's own `libs/core/<app>-services/src/lib/services/`** wraps the
   generated call and maps that one code to `of(null)`, rethrowing everything else
   (`PartnerPayoutDetailsService.getMine`; the admin twin over the masked read is
   `AdminPayoutDetailsService.getForEmployee`). It reads the code through the shared
   `extractApiErrorCode` — never a hand-rolled body walk. Facades then branch on `null` vs. the
   `catchError(() => of(null))` arm of the C3 pipe, so **empty and error stay distinct states**.
2. **The code is listed in `ABSENT_RESOURCE_ERROR_CODES`** (`libs/core/services/.../api-error.ts`) and
   `HttpErrorInterceptorFn` stays silent for it **on a GET only** — the same reason it is already
   silent on a 404. On a mutation the identical code is a genuine refusal and still toasts. Keep the
   set tiny: a code belongs there only when *every* reader renders it as an empty state.

**One code is normalized once per app, not once per platform.** The set and the interceptor are shared,
but the wrapping service is per client lib, so the second app to read the same optional resource writes
its own two-line wrapper — and gets the silence for free. `payout.not_found` now has two readers
(partner self-read, admin masked read) and neither toasts on GET; the admin **reveal** is a POST and
still does. **Three distinct states, not two**: a facade over such a read needs a `loaded` latch as well
as `loading`/`loadFailed`, because "`maskedDetails() === null`" is equally true before the first response
— `isEmpty` that omits the latch renders the empty state during the initial load.

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

### An interceptor that dispatches to the store lives in the STORE lib, and the app composes the chain

Three tiers, and the tier decides the file's home:

| Interceptor | Home | Joins |
|---|---|---|
| cross-app, no store (`HttpErrorInterceptorFn`, `RetryAfterInterceptorFn`, `ContentDisposition…`) | `libs/core/services/src/lib/interceptors/` | `COMMON_INTERCEPTORS_FN` |
| per-app, client concern (auth header, 401 refresh, per-app error map) | `libs/core/<app>-services/src/lib/interceptors/` | `<APP>_INTERCEPTORS_FN` |
| per-app, **dispatches NgRx** (`LoadingInterceptorFn`) | `libs/data-access/<app>-stores/src/lib/<slice>/` | `<APP>_STORE_INTERCEPTORS_FN` |

The third row is the one that is easy to get wrong, because the interceptor *feels* like HTTP wiring.
It is not: `*-stores` already reads `*-services` for the generated client, so an interceptor that
`inject(Store).dispatch(...)` from inside `*-services` is the arrow that closes the loop. All three
apps shipped exactly that, and it cost **47 of the workspace's 66 module-boundary errors** in three
cycles. Put the file beside the actions it dispatches and the arrow simply is not there.

The cost is that `<APP>_INTERCEPTORS_FN` can no longer be the whole chain, because no lib can see
both sides. **The app composes it, in `apps/<app>/src/app/http-interceptors.ts`, and a spec pins the
composed array by identity and in order** — an interceptor that is moved but not re-registered is
silent at runtime and green in every other check, which is precisely the failure this move risks:

```ts
export const APP_INTERCEPTORS_FN: HttpInterceptorFn[] = [
  ...COMMON_INTERCEPTORS_FN,
  ...PARTNER_INTERCEPTORS_FN,
  ...PARTNER_STORE_INTERCEPTORS_FN,
];
```

`app.config.ts` then passes that one symbol to `withInterceptors`. Keep the concatenation order —
common, then client, then store — because array order is chain order (above).

**Enforced by:** `agents/tools/check-module-boundaries.mjs` for the placement (a store-dispatching
interceptor back inside `*-services` re-creates the cycle, and the gate refuses it — measured: one
restored import took the gate from 0 drifts to 18) — **T1-CI**; and
`apps/*/src/app/http-interceptors.spec.ts` for the registration — **T1-CI** (`nx affected -t test`
selects the app whenever the chain file changes, and the step is not `continue-on-error`).

### A lower lib calls a higher one through a token, never by injecting it

`CustomerAuthService` (in `customer-services`) has to warm the saved-address cache on sign-in and
blank it on sign-out, and `SavedAddressStore` lives in `customer-stores` — the lib that reads
`customer-services`. `inject(SavedAddressStore)` there was the second half of the customer cycle.

Do **not** solve it by moving the state down into the client lib: cross-feature state belongs in
`data-access`, and `*-services` is the generated client plus its guards and interceptors. Declare the
seam where the *caller* lives and let the app join the two ends:

```ts
// libs/core/customer-services/src/lib/services/session-lifecycle.ts
export interface SessionLifecycleListener { onSessionStarted(): void; onSessionEnded(): void }
export const SESSION_LIFECYCLE_LISTENERS =
  new InjectionToken<readonly SessionLifecycleListener[]>('SESSION_LIFECYCLE_LISTENERS');

// apps/cleansia.app/src/app/session-listeners.ts
{ provide: SESSION_LIFECYCLE_LISTENERS, useExisting: SavedAddressStore, multi: true }
```

Same shape as `AUTH_COOKIE_KEYS` / `MAPBOX_PROXY_PATH`: shared or lower lib declares the token, the
app config provides the concrete. Two rules that are not optional. **Inject it `{ optional: true }`
and default to `[]`** — every existing spec of the caller would otherwise need the provider, and a
listener really is optional (partner and admin register none). And **pin the wiring with a spec in
the app**, because a `multi` provider that is never registered is a no-op with no error anywhere:
here, dropping it leaves user B looking at user A's addresses after a sign-out on a shared device.

Prefer this to inverting with an `effect()` on the auth signal. That reads cleaner and quietly
changes behaviour twice: a `providedIn: 'root'` store only observes once something injects it, and a
`setSession` on an already-true signal stops re-firing.

**Enforced by:** `apps/cleansia.app/src/app/session-listeners.spec.ts` (resolves the store through
the token, and asserts the sign-out path blanks it) — **T1-CI**; the cycle it prevents is
`check-module-boundaries.mjs`'s — **T1-CI**.

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
glob to the call in `src/Cleansia.App/eslint.config.mjs`. **Every scope is now opted in and the count
under the selector is zero across `libs/` and `apps/`** — `libs/core`, `libs/data-access`, all of
`libs/cleansia-customer-features`, all of `libs/cleansia-partner-features`, and all 26
`libs/cleansia-admin-features` (T-0559 closed the last 46 in 9 admin libs). The ratchet is therefore
strict rather than partial: the next literal anyone writes is a violation on the spot. **Never delete a
scope from that list to make a new literal compile** — convert the call site instead.

**The unit of progress is a lint scope, not a file.** Each `libs/cleansia-*-features/<lib>` owns its
`eslint.config.mjs` and is therefore its own scope, so a converted lib opts in on its own; the customer
feature libs have no local configs and are covered by one workspace-relative glob. A half-converted lib
earns nothing, so take libs whole and say which remain.

**Pin the wire body BEFORE you convert, with `.toJSON()`, not property-by-property.** Every generated
member is declared `field!: T`, so a dropped assignment type-checks and no build catches it — the
conversion is only safe if a test already asserts the *serialized* body:

```ts
const command: TakeOrderCommand = orderClient.takeOrder.mock.calls[0][0];
expect(command).toBeInstanceOf(TakeOrderCommand);
expect(command.toJSON()).toEqual({ orderId: ORDER_ID });
```

`toEqual` on the whole object is what makes it a guard: a per-field `expect(command.orderId).toBe(…)`
passes happily when a *different* field is dropped. Write the test first, watch it pass against the
literal, convert, watch it pass again — then mutation-prove by deleting one assignment. Two details
that bite: `toJSON()` emits a `Date` as the serializer's own string (`'1990-05-15'` for a date-only
member, ISO for a date-time), and an unset optional appears as a present key with value `undefined`,
so assert `body.phoneNumber` is `undefined` rather than `not.toHaveProperty`.

Where a command is built from a whole form, put a `buildXxxCommand(data)` factory in the feature's
`*.models.ts` and call it from the component/facade — the assignments become unit-testable without a
TestBed, and the component stops holding construction logic.

**The selector's suffix set is narrower than the hazard.** It matches
`(Command|Request|Dto|Query)$` only, so `new SaveOrderPhotosPhotoToSave({…})`,
`new SaveMyDocumentsDocumentToSave({…})`, `new CreateServiceTranslationInput({…})` and
`new IssuePartialRefundRefundLineSelection({…})` are the *same* regen-fragile literal and the rule is
silent on them. Convert them when you are in the file anyway; widening the selector is an Architect
call (a broader regex risks matching hand-written classes).

Two measurements from T-0559's sweep, recorded as **evidence for that ruling, not as a rule**. The
largest invisible surface is `SortDefinition`: 16 of its 17 object-literal call sites construct the
**generated** class (11 admin + 5 partner — e.g.
`libs/cleansia-admin-features/audit-log/src/lib/audit-log/audit-log.component.ts:209`,
`libs/cleansia-partner-features/orders/src/lib/orders/orders.facade.ts:191`). And the 17th
(`libs/shared/models/src/lib/models/sort.models.ts:244`) constructs a **hand-written** `SortDefinition`
declared in `libs/shared/models/src/lib/models/sort-types.models.ts:6`. So the same identifier names a
generated DTO and a hand-written class in one workspace, which is the concrete reason a name-only
discriminator cannot be made both complete and false-positive-free. `OrderFilter`
(`libs/shared/models/src/lib/models/filter.models.ts:196`, 4 literal call sites) is entirely
hand-written, so a `Filter$` widening would be 100 % false positives there.

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

This is enforced by `@nx/enforce-module-boundaries` on a **scope tag** scheme (each `project.json`
carries a `scope:*` and a `type:*` tag; the apps carry `scope:<app>` + `type:app`):

| Tag | Applied to |
|---|---|
| `scope:customer` / `scope:partner` / `scope:admin` | each app itself, its feature libs, its `*-services` client lib, its `*-stores` data lib |
| `scope:shared` | cross-app libs (`components`, `directives`, `pipes`, `services`, `models`, `utils`, `assets`) |
| `type:feature` / `type:ui` / `type:data` / `type:util` / `type:app` | feature / shared-UI / NgRx-store / client-or-helper lib / application |

The constraints read: `scope:customer → [scope:customer, scope:shared]` (and the same for partner/admin),
plus the orthogonal `type:*` rules. A cross-app client import is therefore a **lint error**
("A project tagged with `scope:customer` can only depend on libs tagged with `scope:customer`,
`scope:shared`").

**`nx lint` is where you SEE it; it is not what STOPS it — read the next paragraph before you rely on
either.** `frontend-ci.yml`'s only lint step is `continue-on-error: true` (:73), so a boundary
violation reported there sets no exit code, and that step is `nx affected -t lint`, which cannot
select a project the change did not touch — a boundary violation is a statement about a *pair* of
projects and the half that reports it is often not the half that was edited. That combination is how
a customer lib importing the **partner** client shipped and stayed shipped.

**The gate is `agents/tools/check-module-boundaries.mjs`, and it lints from the WORKSPACE ROOT with
the root config rather than through the per-project ones.** That is not a shortcut, it is the point:
the defect this whole scheme was repaired from is that a per-project `eslint.config.mjs` can quietly
opt out, so a gate assembled out of those same per-project configs inherits the hole it exists to
close. ESLint 9 flat config resolves ONE config from the cwd and applies it to every file it walks,
so a single `npx eslint .` at `src/Cleansia.App` measures all 1340 files through the table above.
Verified equal to the 70-project `nx run-many -t lint --all --skip-nx-cache` run: same 19 violations,
same 19 files. It is an **exact-match ratchet in both directions** — a new violation is red, and so is
a recorded one that has been fixed without its entry being deleted.

**`cross-scope`, `untagged-project` and `circular-dependency` are held at ZERO.** The three classes
still recorded are older and each needs its own decision: `buildable-from-non-buildable` ×14
(`libs/shared/components` carries a `package.json`, so Nx refuses its imports of non-buildable shared
libs — a publishable-or-not decision about that one lib), `static-import-of-lazy` ×4 (each app shell
statically imports `@cleansia/components` while its own `app.routes.ts` lazy-loads it), and
`deep-relative-import` ×1 (`invoice-management` reaches into `employee-management`'s source through
`../../../../` for a dialog that lib's barrel does not export).

**A mistyped scope tag cannot hide, and this was measured rather than assumed.** Renaming
`libs/core/customer-services`'s tag to `scope:cusomer` does **not** simply switch that lib's scope
rule off — every consumer's allow-list stops containing the target's tag, so the gate goes from 19
violations to 117 (91 `cross-scope` + 7 `untagged-project`). That is why there is no separate
tag-vocabulary list to maintain: the vocabulary is enforced by the constraint table itself, through
the consumers, and a hand-kept list of legal tags would only be a second thing to keep in sync.

**Enforced by:** `agents/tools/check-module-boundaries.mjs` + its 21-scenario self-test —
**T1-CI** (`.github/workflows/module-boundaries.yml`, its own repo-root workflow with no
`continue-on-error`; an empty or partial eslint walk is a hard failure, not a pass).

**The table lives in exactly one file — `src/Cleansia.App/eslint.module-boundaries.config.mjs` —
because it has to be spread from two.** The root `eslint.config.mjs` lints only the projects that have
**no** local `eslint.config.mjs`; every other project spreads `eslint.base.config.mjs`. Those two
carried separate copies and the base one had decayed to `sourceTag: '*' →
onlyDependOnLibsWithTags: ['*']`, i.e. allow everything — so for months the guard was **off** for the
50 projects that have a local config and on only for the handful that do not. That is why one
customer-lib violation was visible while an identical one inside a feature lib was not. Both configs
now spread `moduleBoundariesRules()`; do not re-inline the table in either.

**An untagged project is now an error, not a silent pass** ("A project without tags matching at least
one constraint cannot depend on any libraries"). Turning the real table on surfaced 515 instances of
it across 28 untagged projects — the 3 apps and 25 of the 26 admin feature libs — all of which were
missing tags rather than violating anything, and all now tagged. So: **when you add a lib or an app,
tag it (`scope` + `type`) in its `project.json`**, or its very first import fails lint.

**…and a lib with no `project.json` at all is not a project, so it is outside test, outside lint AND
outside this constraint simultaneously — with no output from any of the three.** That is how
`cleansia-partner-features/dashboard` sat unregistered for months. Registration alone does not close
it either: an untagged project is unconstrained, i.e. the same hole with a project file on top.
**Enforced by:** `agents/tools/check-nx-project-registration.mjs` + its self-test — it walks `libs/`
for three independent witnesses (`src/index.ts`, `project.json`, the `tsconfig.base.json` alias) and
requires them to agree, and treats **any enumeration coming back empty as a hard failure** rather
than a pass — **T1-CI** (`.github/workflows/nx-project-registration.yml`, its own repo-root workflow:
`frontend-ci`'s lint step is `continue-on-error: true`, and `nx affected` can never select a project
that does not exist). Tags are asserted by **presence** only, and deliberately: the tag *vocabulary*
needs no list of its own, because a mistyped `scope:` is caught by every consumer of the mistyped lib
(measured — see "A mistyped scope tag cannot hide" above).

Both of its recorded sets are **empty** since T-0554/T-0555, so all seven of its rules gate strictly:
the first dangling alias or unregistered source tree you add is red. A recorded set was never a
suppression list — it is exact-match in **both** directions, so closing a recorded gap means deleting
its entry in the **same** change, and the self-test covers that ratchet by injecting entries into a
throwaway copy of the checker rather than by giving the shipped tool a suppression flag.

### A registered lib is not yet a *runnable* one — three ways a green `test` target compiles nothing

Registration, tags and an alias get a lib into `nx run-many -t test --all`. They do not get a single
test compiled, and all three ways of failing that print **success**:

- **The lib's `tsconfig.json` `extends` a path that is not there.** Count the `../` segments against a
  sibling at the same depth — a feature lib under `libs/<area>/<lib>/` is **three** up
  (`../../../tsconfig.base.json`), a `libs/data-access/<lib>/` is three as well. With a spec present the
  suite dies at `TS5083` before a single test runs; with **no** spec — which is the state a fresh lib is
  in — Jest prints `No tests found, exiting with code 0` and Nx prints `Successfully ran target test`.
  Four customer libs and two `data-access` libs shipped that way (T-0546); the wrong depth and the right
  one are one character apart and no build output shows either.
- **The lib has a `jest.config.ts` and no `test` target.** Then it is not in the run at all, and an
  absent project is the one failure no log can print — `legal-pages` had the whole jest shape and no
  target.
- **The lib has NEITHER — no jest config and no `test` target.** `nx test <lib>` answers *"Cannot find
  configuration for task"*, `run-many -t test --all` never lists it, and the project simply does not
  appear in any number you look at. All three `libs/data-access/*-stores` — the NgRx effects behind
  auth, user and the services/packages catalogs — shipped this way with `lint` as their only target
  (T-0463). This is the state NX-7 could not see: NX-7's witness is the jest config, which is exactly
  the half that is missing, so the SOURCE has to be the witness instead.

So the number to look at after adding a lib is **how many projects ran a test**, not whether the run was
green: `run-many` was green over 61 projects while three of them compiled nothing and a fourth was not
listed, and green again over 64 while three more were not projects it could select at all. When you add
a lib, add the spec that proves its target works in the same change — a corrected `extends` with zero
specs is byte-for-byte as green as the broken one, and so is a correct target with no spec beside it.

**Enforced by:** `agents/tools/check-nx-project-registration.mjs` rules **NX-6** (every `tsconfig*.json`
resolves its `extends` and its `references`), **NX-7** (`jest.config.*` ⇔ a `test` target, and the
target's `jestConfig`/`tsConfig` options resolve — they are **workspace**-relative, not project-relative)
and **NX-8** (a registered project holding TypeScript has a `test` target at all) — **T1-CI**, same
repo-root workflow, same anti-vacuity anchor: zero tsconfigs read, zero jest configs, or zero `test`
targets across a non-empty project set is a hard failure. None of the three has a recorded set; all
three baselines are zero and each instance is a one-token fix. NX-8 and NX-7 are disjoint by
construction, so a project is reported once, not twice.

**A dangling `tsconfig.base.json` alias is deleted, not repointed — decide per alias, by grep.** An
alias with live importers and a typo'd path is a *repair*; an alias with **zero** importers whose lib
is gone is a *deletion*, and getting that backwards breaks the build. All three that shipped
(`@cleansia.app/order-details`, `@cleansia/cleansia-services`, `@cleansia/stores`) were predecessors
of aliases later split per app and left behind by the split. Do not repoint a zero-importer alias at
the nearest surviving code even when something plausible is next door: `@cleansia.app/order-details`'s
code is a *folder inside* `libs/cleansia-partner-features/orders`, already reachable through that
lib's barrel, so repointing would have declared a second entry point into one lib's internals — a deep
import that `@nx/enforce-module-boundaries` exists to refuse. Leaving one declared is not free either:
the editor offers the completion, the import resolves in `tsconfig` terms, and the failure surfaces as
a confusing build error attributed to whoever wrote the import.

Two shapes of report the rule folds together, worth knowing before you read a red run: **when an import
is both a cycle and a scope break, only the cycle is printed.** `libs/shared/pipes`' three
`order-status/*.pipe.ts` files imported `@cleansia/partner-services` — a real `scope:shared →
scope:partner` break — and read as *"Circular dependency between pipes and partner-services"* for as long
as they existed. Verified by probe: the same `scope:shared → scope:partner` import placed in a lib the
partner chain does **not** reach (`assets`) prints *"A project tagged with `scope:shared` can only depend
on libs tagged with `scope:shared`"* immediately. So a scope break can hide behind a cycle indefinitely,
and **the scope-violation count staying at zero after you fix a cycle is the expected outcome, not
evidence there was nothing underneath** — one fix retires both.

Which is why a cycle is worth retiring even when it reads as pure tidiness: while one stands, the rule
is **not measuring** the pair it spans. The last three (`partner-services ↔ partner-stores` 18,
`customer-services ↔ customer-stores` 18, `admin-services ↔ admin-stores` 11 — 47 of the workspace's
66 boundary errors) went together; the scope count came back zero, exactly as the paragraph above
predicts, and only then was there a baseline worth ratcheting.

**The fix, and the general rule: a wire enum a shared lib needs is declared in `@cleansia/models`,
pinned to every generated client by an off-disk parity spec, and the clients are declared `inputs` of
that spec's own `test` target — without that last part the spec does not run.** Shared code may not
import any `*-services` client, so the enum has to be re-declared — which makes it a **fourth** copy of
something three clients already generate, and nothing about the language makes the four agree. Declare
it in `libs/shared/models/src/lib/models/` and guard it with a spec that **reads the three generated
clients off disk and parses them** (`order-status-enum-parity.spec.ts`) — an import would be the very
break you are fixing, so the file-reading idiom of the i18n and brand-asset guards is the only
available one.

**A file a spec only `readFileSync`s is a file Nx cannot see, and an unseen input makes a green run
worthless.** Nx derives *both* "is this project affected" *and* the task's cache key from **declared**
inputs: `nx.json` gives `@nx/jest` `["default", "^production", "{workspaceRoot}/jest.preset.js"]`,
`default` is `{projectRoot}/**/*`, `sharedGlobals` is **empty**, and `models` has no dependency edge to
any `*-services` lib — nor may it have one. Undeclared, the three clients are inputs to nothing, which is
two independent holes; the second one is the one nobody expects. Measured by renumbering `OnTheWay = 3`
to `33` in `partner-client.ts` (bytes restored, checksum-verified):

| Invocation | Clients undeclared | Clients declared as `inputs` |
|---|---|---|
| `nx test models`, warm cache | **green — 7 passing, replayed from cache** over the renumbered bytes | **red** — the hash moved, so there is no entry to replay |
| `nx test models --skip-nx-cache` | red | red |
| `nx affected` for a client-only diff | `models` **absent** — 12 test-target projects, all partner-scoped | `models` **present**, and its dependent tree with it — 63 |

So it is not that the guard is merely skipped by `affected` on a regen-only commit: it is
**cache-replayable**, i.e. a run that does select `models` can still serve a pass computed over
different client bytes. The declaration that closes both is one line of
`libs/shared/models/project.json`, on the `test` target — a glob, not three paths, so a fourth client
lib arrives covered:

```jsonc
"inputs": ["default", "^production", "{workspaceRoot}/jest.preset.js",
           "{workspaceRoot}/libs/core/*-services/src/lib/client/*.ts"]
```

**An input is a hashing declaration, not a dependency — and that distinction is the only reason this is
available here.** `nx graph` still reports `models → types` and nothing else. The alternative that looks
equivalent, an `implicitDependencies` entry, **is** a graph edge and yields
`models → partner-services → partner-stores → models`: the same circular-dependency class whose repair
created the `scope:shared` boundary that forces the shared copy to exist. Never reach for it here.

The price is legible and worth paying: because almost everything depends on `models`, a commit that
touches a client now selects nearly the whole workspace, not the partner subtree. Only a regen touches
those files, and a regen is exactly the change whose blast radius *is* the whole workspace — the three
unconditional production builds in `frontend-ci.yml` exist for the same reason.

Write the input with the guard, in the same change, whenever you pin a shared lib to a generated client
this way. When the file a spec reads lives **above** the workspace root (`src/Cleansia.App`) — a C#
domain rule, a Kotlin or Swift literal — an Nx input cannot name it at all, and the house shape is the
one `.github/workflows/offerability-parity.yml` uses and states in its header: a dependency-free Node
checker outside the workspace, its own repo-root workflow, its self-test first. The three
`apps/*/src/app/i18n/error-contract-parity.spec.ts` sit in exactly that un-declarable position — they
walk up to `Cleansia.Api.sln` to read `BusinessErrorMessage.cs`.

**Two other shipped specs read off-project files and declare nothing, and this entry does not cover
them** — `cleansia-brand-name.component.spec.ts` (three apps' `assets/logos`, from `components`) and
partner `forgot-password.models.spec.ts` (the partner i18n bundle, from a feature lib). Do not read
their green as a guarantee about the files they open. Declaring inputs there is not the same cheap call:
it would make `components` — which nearly everything depends on — touched by every asset edit, and that
blast radius wants a ticket, not a drive-by.

**Enforced by:** `libs/shared/models/src/lib/models/order-status-enum-parity.spec.ts` **plus** the
`{workspaceRoot}` client glob on `models`' `test` target — **T1-CI**, `frontend-ci.yml`'s
*"Unit tests (affected)"* step (`nx affected -t test`, which unlike that workflow's lint step is not
`continue-on-error`). Neither half is the enforcer alone: the spec without the input is a spec that does
not run, and the input without the spec hashes nothing. Its anti-vacuity anchor is the spec's third
`it()` — zero client files found, or an enum that parsed to nothing, is a hard failure rather than a
vacuous pass.

**It detects exactly one thing: a generated client that disagrees with the shared table.** Four
artifacts agreeing with each other says nothing about whether any of them matches
`Cleansia.Core.Domain.Enums` — a backend renumbering that has not been regenerated yet leaves all four
in perfect agreement and all four wrong. Which declaration should be **canonical** — whether the clients
should stop emitting their own, and where the shared file's integers come from — is an Architect call on
owner-run generation: `Q-ENUM-01`, still **open**. ADR-0042 answered it and was **returned to its author**
on 2026-08-05 (`proposed`, never accepted); nothing may be built against it, and the §7 catalog
replacement it carries has not landed.

The `*-services` index barrels
(`libs/core/<app>-services/src/index.ts`) are **hand-maintained** (not generated — NSwag only emits
`client/<app>-client.ts`); re-exporting an already-generated DTO through the barrel is normal frontend
work, **not** a `nswag-regen` step.

## Testing an NgRx effect — pin behaviour, and pin that the effect is still alive

The `libs/data-access/*-stores` libs hold the cross-feature state (auth, user, the services/packages
catalogs) and had **no** spec of any kind until T-0463. The harness is `provideMockActions` over a
`Subject`, with the generated client wrapper provided as a plain object of `jest.fn()`s:

```ts
TestBed.configureTestingModule({
  providers: [
    UserEffects,
    provideMockActions(() => actions$),
    { provide: PartnerClient, useValue: { userClient } },
  ],
});
```

**Subscribe to the effect before you push an action.** `actions$` is a `Subject`, so anything dispatched
first is dropped and the test reads as "the effect did nothing" — an assertion that passes for the wrong
reason on a `toHaveLength(0)`.

Pin **three** things per effect, not one:

1. the success action carries what the client returned;
2. the failure branch emits the failure action **carrying the error**, rather than swallowing it;
3. **the effect is still alive afterwards** — push a second action and assert it is served too.

The third is the one worth the keystrokes. `catchError` belongs *inside* the `mergeMap`/`switchMap`;
hoisted to the outer pipe it still compiles, still reports the **first** failure correctly, and then
completes the stream so every later action is dropped in silence for the rest of the session. One
transient failure and the admin app's enum dropdowns are empty until reload. Nothing type-checks it and
tests 1 and 2 both stay green — mutation-proved on all three store libs.

Where the operator choice is load-bearing, pin that too rather than trusting the identifier:
`switchMap` on a catalog read means a slow first response must not land after a newer one and repaint
with stale prices. Two pending `Subject`s, resolve the older one last, assert only the newer action was
emitted.

**The two regen-break pins (ADR-0031's failure mode, seen from the store layer).** A `jest.Mock` has no
signature, so asserting on `mock.calls[0]` cannot catch a regen at all — the mock happily accepts
whatever order the effect passes:

- **A positional query** (`getPaged(id, isActive, firstName, lastName, …)` — eleven optional
  parameters, every one assignable to the next) is pinned **end-to-end** by constructing the *real*
  generated sub-client over a mocked `HttpClient` and asserting the request URL. A regen that inserts or
  reorders a parameter then moves `Filter.Email=` onto a different value and the test goes red;
  the same swap against a mocked client is invisible.
- **A command** is pinned in both directions: `toJSON()` + `toEqual` for the *drop* direction (every
  generated member is `field!: T`, so a deleted assignment type-checks and no build catches it), and
  `Object.keys(toJSON()).sort()` for the *add* direction — the regen tripwire. `toJSON()` emits the
  generated class's declared members, so a member the backend adds turns that list red and forces the
  round-trip decision, instead of a whole-resource update silently sending the type default and
  **overwriting** the stored value.

Reference: `libs/data-access/partner-stores/src/lib/user/user.effects.spec.ts` (all five shapes),
`.../admin-stores/src/lib/code/` and `.../customer-stores/src/lib/catalog/` (effect + reducer pairs).
A reducer spec is worth writing beside the effect spec: it is where the three data states
(empty / loading / error) are actually decided, and `customerCatalogReducer` currently makes a failed
catalog read **byte-identical** to an empty catalog — `CustomerCatalogState` carries no error field —
which that spec pins as-is rather than blesses.

**What is gated:** the *configuration* — that these libs are in the test run at all — is NX-8 above,
**T1-CI**. The *coverage* is not gated and cannot be until it is clean: 7 of 11 `*.effects.ts` and 12 of
14 reducers under `libs/data-access/` still have no spec, so a `*.effects.ts` ⇔ `*.effects.spec.ts`
guard would land on a non-zero baseline (`enforcement.md`: enforcement goes behind the cleanup, never in
front of it). Until that backfill lands this section is **(guidance — no gate)**.

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

## Request only the web font families you actually name

`index.html`'s Google Fonts `<link>` is on the critical path, and the production build **inlines the
whole response into the built `index.html`** — `@font-face` blocks and all — so an unused family is
paid for on every cold visit *and* in the bundle. Admin and partner each requested Kanit at all 18
weights while no `font-family` in the tree named it: 82 inlined `@font-face` blocks and a 31 kB
`index.html`, against 10 and 6.9 kB once dropped (T-0566).

The reverse direction is **not** a rule — a declared family may legitimately go unrequested. System
stacks (`sans-serif`, `Menlo`, `Consolas`), icon fonts (`primeicons`) and CDN faces are all named
without a Google Fonts request, so "every declared family must be requested" would flag correct code.

**Enforced by:** `apps/*/src/app/theme/font-stack.spec.ts` — **T1-CI**. It compiles the app's build
stylesheets and parses its `index.html`, so it holds both sides of the comparison. The guard lives in
the three **app** projects, not the shared library: `nx affected` selects only the owning app for an
`index.html` change (verified —
`nx show projects --affected --files=apps/cleansia-admin.app/src/index.html` returns
`cleansia-admin.app` and its e2e project alone, never `assets`), and the `Unit tests (affected)` step
is not `continue-on-error`.
