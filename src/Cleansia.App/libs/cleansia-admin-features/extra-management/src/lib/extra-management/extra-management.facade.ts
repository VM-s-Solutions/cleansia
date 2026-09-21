import { Injectable, computed, inject, signal } from '@angular/core';
import { FormBuilder } from '@angular/forms';
import { Router } from '@angular/router';
import {
  AdminClient,
  AdminCurrencyListItem,
  ExtraListItem,
  SortDefinition,
  SortDirection,
} from '@cleansia/admin-services';
import { FilterChip, FilterDrawerState, ICleansiaSelectOption, PaginationState, SortEvent } from '@cleansia/components';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import { CleansiaAdminRoute, SnackbarService } from '@cleansia/services';
import { currentLanguage, formatMoney, localeFor } from '@cleansia/utils';
import { TranslateService } from '@ngx-translate/core';
import { Observable, catchError, finalize, map, of, switchMap, takeUntil, tap } from 'rxjs';
import {
  CatalogStatusFilter,
  mapStatusFilterToIsActive,
  resolveExtraErrorKey,
} from './extra-management.models';

export interface ExtraFilterParams {
  searchTerm?: string;
  isActive?: boolean;
}

@Injectable()
export class ExtraManagementFacade extends UnsubscribeControlDirective {
  private readonly adminClient = inject(AdminClient);
  private readonly snackbarService = inject(SnackbarService);
  private readonly translate = inject(TranslateService);
  private readonly router = inject(Router);

  readonly extras = signal<ExtraListItem[]>([]);
  readonly loading = signal<boolean>(false);
  readonly initialLoading = signal<boolean>(true);
  readonly totalRecords = signal<number>(0);

  readonly lang = currentLanguage(this.translate);
  readonly statusFilterOptions = computed<ICleansiaSelectOption[]>(() => {
    this.lang();
    return [
      { label: this.translate.instant('pages.extra_management.filters.status_all'), value: 'all' },
      { label: this.translate.instant('pages.extra_management.filters.status_active'), value: 'active' },
      { label: this.translate.instant('pages.extra_management.filters.status_inactive'), value: 'inactive' },
    ];
  });
  readonly filterForm = inject(FormBuilder).nonNullable.group({
    searchTerm: [''],
    status: ['all' as CatalogStatusFilter],
  });
  readonly filters = new FilterDrawerState({
    form: this.filterForm,
    lang: this.lang,
    chips: (value): FilterChip[] => [
      ...(value.searchTerm
        ? [
            {
              key: 'searchTerm',
              label: this.translate.instant('pages.extra_management.filters.search'),
              value: value.searchTerm,
            },
          ]
        : []),
      ...(value.status !== 'all'
        ? [
            {
              key: 'status',
              label: this.translate.instant('pages.extra_management.filters.status'),
              value: this.translate.instant(
                value.status === 'active'
                  ? 'pages.extra_management.filters.status_active'
                  : 'pages.extra_management.filters.status_inactive'
              ),
            },
          ]
        : []),
    ],
    apply: (value) =>
      this.applyFilter({
        searchTerm: value.searchTerm.trim() || undefined,
        isActive: mapStatusFilterToIsActive(value.status),
      }),
  });

  private currentFilter = signal<ExtraFilterParams | null>(null);
  private currentOffset = signal<number>(0);
  private currentLimit = signal<number>(20);
  private currentSort = signal<SortDefinition[] | undefined>(undefined);

  readonly isActiveFilter = computed(() => this.currentFilter()?.isActive);

  /**
   * The currency the list's price column is in. The paged list shows the platform default currency's
   * row (ExtraListItem carries no code of its own -- it travels on both mobile specs), so the code is
   * read once from the currency overview rather than assumed. Null until known; the formatter then
   * prints the bare number rather than a currency it cannot name.
   */
  readonly defaultCurrencyCode = signal<string | null>(null);

  constructor() {
    super();
    this.filters.connect(this.destroyed$);
  }

  loadExtras(): void {
    this.loading.set(true);
    const filterParams = this.currentFilter();

    // The currency first, once, so the rows never render under a label that arrives later.
    const currency$: Observable<string | null> =
      this.defaultCurrencyCode() !== null
        ? of(this.defaultCurrencyCode())
        : this.adminClient.adminCurrencyClient.getOverview().pipe(
            catchError(() => of([] as AdminCurrencyListItem[])),
            map((currencies) => (currencies ?? []).find((c) => c.isDefault)?.code ?? null),
            tap((code) => this.defaultCurrencyCode.set(code))
          );

    currency$
      .pipe(
        switchMap(() =>
          this.adminClient.adminExtraClient.getPaged(
            filterParams?.searchTerm,
            filterParams?.isActive,
            this.currentSort(),
            this.currentOffset(),
            this.currentLimit()
          )
        ),
        takeUntil(this.destroyed$),
        catchError(() => of(null)),
        finalize(() => this.loading.set(false))
      )
      .subscribe((response) => {
        if (response) {
          this.extras.set(response.data || []);
          this.totalRecords.set(response.total || 0);
        }
        if (this.initialLoading()) {
          this.initialLoading.set(false);
        }
      });
  }

  onPageChange(event: PaginationState): void {
    this.currentOffset.set(event.first);
    this.currentLimit.set(event.rows);
    this.loadExtras();
  }

  onSortChange(event: SortEvent): void {
    this.currentSort.set([
      new SortDefinition({
        field: event.field,
        direction: event.order === 1 ? SortDirection.Ascending : SortDirection.Descending,
      }),
    ]);
    this.loadExtras();
  }

  applyFilter(filter: ExtraFilterParams): void {
    this.currentFilter.set(filter);
    this.currentOffset.set(0);
    this.loadExtras();
  }

  formatCurrency(value: number | undefined): string {
    if (value === undefined || value === null) return '';
    return formatMoney(value, this.defaultCurrencyCode(), localeFor(this.translate.currentLang), {
      fractionDigits: 2,
    });
  }

  navigateToCreateExtra(): void {
    this.router.navigate([CleansiaAdminRoute.EXTRA_MANAGEMENT, 'create']);
  }

  navigateToEditExtra(extra: ExtraListItem): void {
    if (extra.id) {
      this.router.navigate([CleansiaAdminRoute.EXTRA_MANAGEMENT, extra.id, 'edit']);
    }
  }

  deactivateExtra(extra: ExtraListItem): void {
    if (!extra.id) return;

    this.adminClient.adminExtraClient
      .deactivate(extra.id)
      .pipe(
        takeUntil(this.destroyed$),
        catchError((error: unknown) => {
          this.snackbarService.showError(
            this.translate.instant(resolveExtraErrorKey(error))
          );
          return of(null);
        })
      )
      .subscribe((response) => {
        if (response) {
          this.snackbarService.showSuccess(
            this.translate.instant(
              'pages.extra_management.messages.deactivate_success'
            )
          );
          this.loadExtras();
        }
      });
  }

  activateExtra(extra: ExtraListItem): void {
    if (!extra.id) return;

    this.adminClient.adminExtraClient
      .activate(extra.id)
      .pipe(
        takeUntil(this.destroyed$),
        catchError((error: unknown) => {
          this.snackbarService.showError(
            this.translate.instant(resolveExtraErrorKey(error))
          );
          return of(null);
        })
      )
      .subscribe((response) => {
        if (response) {
          this.snackbarService.showSuccess(
            this.translate.instant(
              'pages.extra_management.messages.activate_success'
            )
          );
          this.loadExtras();
        }
      });
  }

  deleteExtra(extra: ExtraListItem): void {
    if (!extra.id) return;

    this.adminClient.adminExtraClient
      .delete(extra.id)
      .pipe(
        takeUntil(this.destroyed$),
        catchError((error: unknown) => {
          this.snackbarService.showError(
            this.translate.instant(resolveExtraErrorKey(error))
          );
          return of(null);
        })
      )
      .subscribe((response) => {
        if (response) {
          this.snackbarService.showSuccess(
            this.translate.instant('pages.extra_management.messages.delete_success')
          );
          this.loadExtras();
        }
      });
  }
}
