import { Injectable, computed, inject, signal } from '@angular/core';
import { FormBuilder } from '@angular/forms';
import { Router } from '@angular/router';
import {
  AdminClient,
  AdminCurrencyListItem,
  PackageListItem,
  SortDefinition,
  SortDirection,
} from '@cleansia/admin-services';
import { FilterChip, FilterDrawerState, ICleansiaSelectOption, PaginationState, SortEvent } from '@cleansia/components';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import { CleansiaAdminRoute, DialogService, SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { currentLanguage, formatMoney, localeFor } from '@cleansia/utils';
import { catchError, filter, finalize, map, Observable, of, switchMap, takeUntil, tap } from 'rxjs';
import { CatalogStatusFilter, mapStatusFilterToIsActive } from './package-management.models';

export interface PackageFilterParams {
  searchTerm?: string;
  isActive?: boolean;
}

@Injectable()
export class PackageManagementFacade extends UnsubscribeControlDirective {
  private readonly adminClient = inject(AdminClient);
  private readonly dialog = inject(DialogService);
  private readonly snackbarService = inject(SnackbarService);
  private readonly translate = inject(TranslateService);
  private readonly router = inject(Router);

  readonly packages = signal<PackageListItem[]>([]);
  readonly loading = signal<boolean>(false);
  readonly initialLoading = signal<boolean>(true);
  readonly totalRecords = signal<number>(0);

  readonly lang = currentLanguage(this.translate);
  readonly statusFilterOptions = computed<ICleansiaSelectOption[]>(() => {
    this.lang();
    return [
      { label: this.translate.instant('pages.package_management.filters.status_all'), value: 'all' },
      { label: this.translate.instant('pages.package_management.filters.status_active'), value: 'active' },
      { label: this.translate.instant('pages.package_management.filters.status_inactive'), value: 'inactive' },
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
              label: this.translate.instant('pages.package_management.filters.search'),
              value: value.searchTerm,
            },
          ]
        : []),
      ...(value.status !== 'all'
        ? [
            {
              key: 'status',
              label: this.translate.instant('pages.package_management.filters.status'),
              value: this.translate.instant(
                value.status === 'active'
                  ? 'pages.package_management.filters.status_active'
                  : 'pages.package_management.filters.status_inactive'
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

  private currentFilter = signal<PackageFilterParams | null>(null);
  private currentOffset = signal<number>(0);
  private currentLimit = signal<number>(20);
  private currentSort = signal<SortDefinition[] | undefined>(undefined);

  readonly isActiveFilter = computed(() => this.currentFilter()?.isActive);

  /**
   * The currency the list's price columns are in. The paged list shows the platform default currency's
   * row (PackageListItem carries no code of its own), so the code is read once from the currency overview
   * rather than assumed. Null until known; the formatter then prints the bare number rather than a
   * currency it cannot name.
   */
  readonly defaultCurrencyCode = signal<string | null>(null);

  constructor() {
    super();
    this.filters.connect(this.destroyed$);
  }

  loadPackages(): void {
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
          this.adminClient.adminPackageClient.getPaged(
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
          this.packages.set(response.data || []);
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
    this.loadPackages();
  }

  onSortChange(event: SortEvent): void {
    this.currentSort.set([
      new SortDefinition({
        field: event.field,
        direction: event.order === 1 ? SortDirection.Ascending : SortDirection.Descending,
      }),
    ]);
    this.loadPackages();
  }

  applyFilter(filter: PackageFilterParams): void {
    this.currentFilter.set(filter);
    this.currentOffset.set(0);
    this.loadPackages();
  }

  formatCurrency(value: number | undefined): string {
    if (value === undefined || value === null) return '';
    return formatMoney(value, this.defaultCurrencyCode(), localeFor(this.translate.currentLang), {
      fractionDigits: 2,
    });
  }

  navigateToCreatePackage(): void {
    this.router.navigate([CleansiaAdminRoute.PACKAGE_MANAGEMENT, 'create']);
  }

  navigateToEditPackage(pkg: PackageListItem): void {
    if (pkg.id) {
      this.router.navigate([CleansiaAdminRoute.PACKAGE_MANAGEMENT, pkg.id, 'edit']);
    }
  }

  deactivatePackage(pkg: PackageListItem): void {
    const id = pkg.id;
    if (!id) return;

    this.dialog
      .confirmTranslated(
        'pages.package_management.deactivate_confirm',
        'pages.package_management.deactivate_package',
        { name: pkg.name }
      )
      .pipe(takeUntil(this.destroyed$), filter(Boolean))
      .subscribe(() => this.deactivatePackageConfirmed(id));
  }

  private deactivatePackageConfirmed(id: string): void {
    this.adminClient.adminPackageClient
      .deactivate(id)
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of(null))
      )
      .subscribe((response) => {
        if (response) {
          this.snackbarService.showSuccessTranslated(
            'pages.package_management.messages.deactivate_success'
          );
          this.loadPackages();
        }
      });
  }

  activatePackage(pkg: PackageListItem): void {
    if (!pkg.id) return;

    this.adminClient.adminPackageClient
      .activate(pkg.id)
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of(null))
      )
      .subscribe((response) => {
        if (response) {
          this.snackbarService.showSuccessTranslated(
            'pages.package_management.messages.activate_success'
          );
          this.loadPackages();
        }
      });
  }

  deletePackage(pkg: PackageListItem): void {
    const id = pkg.id;
    if (!id) return;

    this.dialog
      .confirmTranslated(
        'pages.package_management.delete_confirm',
        'pages.package_management.delete_package',
        undefined,
        { danger: true, acceptLabelKey: 'global.actions.delete' }
      )
      .pipe(takeUntil(this.destroyed$), filter(Boolean))
      .subscribe(() => this.deletePackageConfirmed(id));
  }

  private deletePackageConfirmed(id: string): void {
    this.adminClient.adminPackageClient
      .delete(id)
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of(null))
      )
      .subscribe((response) => {
        if (response) {
          this.snackbarService.showSuccessTranslated(
            'pages.package_management.messages.delete_success'
          );
          this.loadPackages();
        }
      });
  }
}
