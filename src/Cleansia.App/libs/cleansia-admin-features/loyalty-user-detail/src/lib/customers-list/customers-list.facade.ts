import { Injectable, computed, inject, signal } from '@angular/core';
import { FormBuilder } from '@angular/forms';
import { Router } from '@angular/router';
import {
  AdminCustomerClient,
  AdminCustomerListItem,
  SortDefinition,
  SortDirection,
} from '@cleansia/admin-services';
import { FilterChip, FilterDrawerState, ICleansiaSelectOption, PaginationState, SortEvent } from '@cleansia/components';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import { CleansiaAdminRoute } from '@cleansia/services';
import { currentLanguage } from '@cleansia/utils';
import { TranslateService } from '@ngx-translate/core';
import { catchError, finalize, of, takeUntil } from 'rxjs';
import {
  CustomerStatusFilter,
  DEFAULT_CUSTOMER_STATUS,
  mapStatusFilterToIsActive,
} from './customers-list.models';

export interface CustomerFilterParams {
  searchTerm?: string;
  isActive?: boolean;
}

const STATUS_LABEL_KEYS: Readonly<Record<CustomerStatusFilter, string>> = {
  all: 'pages.customers.filters.status_all',
  active: 'pages.customers.filters.status_active',
  inactive: 'pages.customers.filters.status_inactive',
};

@Injectable()
export class CustomersListFacade extends UnsubscribeControlDirective {
  private readonly customerClient = inject(AdminCustomerClient);
  private readonly translate = inject(TranslateService);
  private readonly router = inject(Router);

  readonly customers = signal<AdminCustomerListItem[]>([]);
  readonly loading = signal<boolean>(false);
  readonly initialLoading = signal<boolean>(true);
  readonly totalRecords = signal<number>(0);
  readonly hasError = signal<boolean>(false);

  readonly lang = currentLanguage(this.translate);
  readonly statusFilterOptions = computed<ICleansiaSelectOption[]>(() => {
    this.lang();
    return (['active', 'inactive', 'all'] as const).map((status) => ({
      label: this.translate.instant(STATUS_LABEL_KEYS[status]),
      value: status,
    }));
  });
  readonly filterForm = inject(FormBuilder).nonNullable.group({
    searchTerm: [''],
    status: [DEFAULT_CUSTOMER_STATUS],
  });
  readonly filters = new FilterDrawerState({
    form: this.filterForm,
    lang: this.lang,
    chips: (value): FilterChip[] => [
      ...(value.searchTerm.trim()
        ? [
            {
              key: 'searchTerm',
              label: this.translate.instant('pages.customers.filters.search'),
              value: value.searchTerm.trim(),
            },
          ]
        : []),
      ...(value.status !== DEFAULT_CUSTOMER_STATUS
        ? [
            {
              key: 'status',
              label: this.translate.instant('pages.customers.filters.status'),
              value: this.translate.instant(STATUS_LABEL_KEYS[value.status]),
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

  private readonly currentFilter = signal<CustomerFilterParams>({
    isActive: mapStatusFilterToIsActive(DEFAULT_CUSTOMER_STATUS),
  });
  private readonly currentOffset = signal<number>(0);
  private readonly currentLimit = signal<number>(20);
  private readonly currentSort = signal<SortDefinition[] | undefined>(undefined);

  constructor() {
    super();
    this.filters.connect(this.destroyed$);
  }

  loadCustomers(): void {
    this.loading.set(true);
    this.hasError.set(false);
    const filter = this.currentFilter();

    this.customerClient
      .getPaged(
        filter.searchTerm,
        filter.isActive,
        this.currentSort(),
        this.currentOffset(),
        this.currentLimit()
      )
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => {
          this.hasError.set(true);
          return of(null);
        }),
        finalize(() => this.loading.set(false))
      )
      .subscribe((response) => {
        if (response) {
          this.customers.set(response.data ?? []);
          this.totalRecords.set(response.total ?? 0);
        }
        if (this.initialLoading()) {
          this.initialLoading.set(false);
        }
      });
  }

  onPageChange(event: PaginationState): void {
    this.currentOffset.set(event.first);
    this.currentLimit.set(event.rows);
    this.loadCustomers();
  }

  onSortChange(event: SortEvent): void {
    const sort = new SortDefinition();
    sort.field = event.field;
    sort.direction = event.order === 1 ? SortDirection.Ascending : SortDirection.Descending;
    this.currentSort.set([sort]);
    this.loadCustomers();
  }

  applyFilter(filter: CustomerFilterParams): void {
    this.currentFilter.set(filter);
    this.currentOffset.set(0);
    this.loadCustomers();
  }

  openCustomer(customer: AdminCustomerListItem): void {
    if (!customer.id) return;
    this.router.navigate([CleansiaAdminRoute.CUSTOMERS, customer.id], {
      queryParams: customer.email ? { email: customer.email } : undefined,
    });
  }
}
