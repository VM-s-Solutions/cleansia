import { computed, Injectable, inject, signal } from '@angular/core';
import { FormBuilder } from '@angular/forms';
import {
  AdminClient,
  OrderListItem,
  OrderStatus,
  PaymentStatus,
  SortDefinition,
  SortDirection,
} from '@cleansia/admin-services';
import { FilterChip, FilterDrawerState, PaginationState, SortEvent } from '@cleansia/components';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import { SnackbarService } from '@cleansia/services';
import { currentLanguage } from '@cleansia/utils';
import { TranslateService } from '@ngx-translate/core';
import { catchError, finalize, of, takeUntil } from 'rxjs';
import {
  buildFilterChips,
  buildFilterPayload,
  buildOrderStatusOptions,
  buildPaymentStatusOptions,
  toggleStatusInArray,
} from './order-management.helpers';

export interface OrderFilterParams {
  orderStatuses?: OrderStatus[];
  paymentStatuses?: PaymentStatus[];
  searchTerm?: string;
  cleaningDateFrom?: Date;
  cleaningDateTo?: Date;
  hasAvailableSpots?: boolean;
  isUnassigned?: boolean;
  currencyId?: string;
}

@Injectable()
export class OrderManagementFacade extends UnsubscribeControlDirective {
  private readonly adminClient = inject(AdminClient);
  private readonly snackbarService = inject(SnackbarService);
  private readonly translate = inject(TranslateService);

  readonly orders = signal<OrderListItem[]>([]);
  readonly loading = signal<boolean>(false);
  readonly initialLoading = signal<boolean>(true);
  readonly totalRecords = signal<number>(0);

  readonly lang = currentLanguage(this.translate);
  readonly orderStatusOptions = computed(() => {
    this.lang();
    return buildOrderStatusOptions(this.translate);
  });
  readonly paymentStatusOptions = computed(() => {
    this.lang();
    return buildPaymentStatusOptions(this.translate);
  });
  readonly filterForm = inject(FormBuilder).group({
    orderStatus: [[] as OrderStatus[]],
    paymentStatus: [[] as PaymentStatus[]],
    searchTerm: [''],
    cleaningDateFrom: [null as Date | null],
    cleaningDateTo: [null as Date | null],
    currencyId: [null as string | null],
  });
  readonly filters = new FilterDrawerState({
    form: this.filterForm,
    lang: this.lang,
    chips: (value): FilterChip[] =>
      buildFilterChips(
        value,
        this.orderStatusOptions(),
        this.paymentStatusOptions(),
        this.currencies(),
        this.translate
      ),
    apply: (value) => this.applyFilter(buildFilterPayload(value)),
  });

  private currentFilter = signal<OrderFilterParams | null>(null);
  private currentOffset = signal<number>(0);
  private currentLimit = signal<number>(20);
  private currentSort = signal<SortDefinition[] | undefined>(undefined);

  readonly currencies = signal<{ id: string; code: string; isDefault: boolean }[]>([]);

  constructor() {
    super();
    this.filters.connect(this.destroyed$);
  }

  loadCurrencies(): void {
    this.adminClient.adminCurrencyClient
      .getOverview()
      .pipe(takeUntil(this.destroyed$), catchError(() => of([])))
      .subscribe((currencies) => {
        this.currencies.set(
          (currencies ?? []).flatMap((c) =>
            c.id && c.code ? [{ id: c.id, code: c.code, isDefault: !!c.isDefault }] : []
          )
        );
      });
  }

  loadOrders(): void {
    this.loading.set(true);
    const filterParams = this.currentFilter();

    // Parameters order: id, isActive, customerName, customerEmail, customerPhone,
    // displayOrderNumber, employeeId, cleaningDateFrom, cleaningDateTo,
    // paymentStatuses, paymentTypes, minTotalPrice, maxTotalPrice, orderStatuses,
    // hasAvailableSpots, isUnassigned, excludeEmployeeId, currencyId, userId, sort, offset, limit
    this.adminClient.adminOrderClient
      .getPaged(
        undefined, // id
        undefined, // isActive
        filterParams?.searchTerm, // customerName
        undefined, // customerEmail
        undefined, // customerPhone
        filterParams?.searchTerm, // displayOrderNumber
        undefined, // employeeId
        filterParams?.cleaningDateFrom, // cleaningDateFrom
        filterParams?.cleaningDateTo, // cleaningDateTo
        filterParams?.paymentStatuses, // paymentStatuses
        undefined, // paymentTypes
        undefined, // minTotalPrice
        undefined, // maxTotalPrice
        filterParams?.orderStatuses, // orderStatuses
        filterParams?.hasAvailableSpots, // hasAvailableSpots
        filterParams?.isUnassigned, // isUnassigned
        undefined, // excludeEmployeeId
        filterParams?.currencyId, // currencyId
        undefined, // userId
        this.currentSort(),
        this.currentOffset(),
        this.currentLimit()
      )
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of(null)),
        finalize(() => this.loading.set(false))
      )
      .subscribe((response) => {
        if (response) {
          this.orders.set(response.data || []);
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
    this.loadOrders();
  }

  onSortChange(event: SortEvent): void {
    this.currentSort.set([
      new SortDefinition({
        field: event.field,
        direction: event.order === 1 ? SortDirection.Ascending : SortDirection.Descending,
      }),
    ]);
    this.loadOrders();
  }

  applyFilter(filter: OrderFilterParams): void {
    this.currentFilter.set(filter);
    this.currentOffset.set(0);
    this.loadOrders();
  }

  isOrderStatusChecked(status: OrderStatus): boolean {
    return this.filterForm.value.orderStatus?.includes(status) ?? false;
  }

  setOrderStatus(status: OrderStatus, checked: boolean): void {
    this.filterForm.patchValue({
      orderStatus: toggleStatusInArray(this.filterForm.value.orderStatus || [], status, checked),
    });
  }

  isPaymentStatusChecked(status: PaymentStatus): boolean {
    return this.filterForm.value.paymentStatus?.includes(status) ?? false;
  }

  setPaymentStatus(status: PaymentStatus, checked: boolean): void {
    this.filterForm.patchValue({
      paymentStatus: toggleStatusInArray(this.filterForm.value.paymentStatus || [], status, checked),
    });
  }

}
