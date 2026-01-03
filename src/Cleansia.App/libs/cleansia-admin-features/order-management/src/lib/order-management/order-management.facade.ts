import { Injectable, inject, signal } from '@angular/core';
import {
  AdminClient,
  GetPagedOrdersRequest,
  OrderFilter,
  OrderListItem,
  OrderStatus,
  PaymentStatus,
  SortDefinition,
} from '@cleansia/admin-services';
import { SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { Subject, catchError, finalize, of, takeUntil } from 'rxjs';

export interface OrderFilterParams {
  orderStatuses?: OrderStatus[];
  paymentStatuses?: PaymentStatus[];
  searchTerm?: string;
  cleaningDateFrom?: Date;
  cleaningDateTo?: Date;
  hasAvailableSpots?: boolean;
  isUnassigned?: boolean;
}

@Injectable()
export class OrderManagementFacade {
  private readonly adminClient = inject(AdminClient);
  private readonly snackbarService = inject(SnackbarService);
  private readonly translate = inject(TranslateService);

  private destroy$ = new Subject<void>();

  readonly orders = signal<OrderListItem[]>([]);
  readonly loading = signal<boolean>(false);
  readonly totalRecords = signal<number>(0);

  private currentFilter = signal<OrderFilterParams | null>(null);
  private currentOffset = signal<number>(0);
  private currentLimit = signal<number>(20);
  private currentSort = signal<SortDefinition[] | undefined>(undefined);

  readonly orderStatusOptions = [
    {
      label: this.translate.instant(
        'pages.order_management.order_status.pending'
      ),
      value: OrderStatus.Pending,
    },
    {
      label: this.translate.instant(
        'pages.order_management.order_status.confirmed'
      ),
      value: OrderStatus.Confirmed,
    },
    {
      label: this.translate.instant(
        'pages.order_management.order_status.in_progress'
      ),
      value: OrderStatus.InProgress,
    },
    {
      label: this.translate.instant(
        'pages.order_management.order_status.completed'
      ),
      value: OrderStatus.Completed,
    },
    {
      label: this.translate.instant(
        'pages.order_management.order_status.cancelled'
      ),
      value: OrderStatus.Cancelled,
    },
  ];

  readonly paymentStatusOptions = [
    {
      label: this.translate.instant(
        'pages.order_management.payment_status.pending'
      ),
      value: PaymentStatus.Pending,
    },
    {
      label: this.translate.instant(
        'pages.order_management.payment_status.paid'
      ),
      value: PaymentStatus.Paid,
    },
    {
      label: this.translate.instant(
        'pages.order_management.payment_status.failed'
      ),
      value: PaymentStatus.Failed,
    },
    {
      label: this.translate.instant(
        'pages.order_management.payment_status.refunded'
      ),
      value: PaymentStatus.Refunded,
    },
    {
      label: this.translate.instant(
        'pages.order_management.payment_status.disputed'
      ),
      value: PaymentStatus.Disputed,
    },
  ];

  loadOrders(): void {
    this.loading.set(true);
    const filterParams = this.currentFilter();

    const orderFilter = new OrderFilter();
    if (filterParams?.orderStatuses && filterParams.orderStatuses.length > 0) {
      orderFilter.orderStatuses = filterParams.orderStatuses;
    }
    if (
      filterParams?.paymentStatuses &&
      filterParams.paymentStatuses.length > 0
    ) {
      orderFilter.paymentStatuses = filterParams.paymentStatuses;
    }
    if (filterParams?.searchTerm) {
      orderFilter.customerName = filterParams.searchTerm;
      orderFilter.displayOrderNumber = filterParams.searchTerm;
    }
    if (filterParams?.cleaningDateFrom) {
      orderFilter.cleaningDateFrom = filterParams.cleaningDateFrom;
    }
    if (filterParams?.cleaningDateTo) {
      orderFilter.cleaningDateTo = filterParams.cleaningDateTo;
    }
    if (filterParams?.hasAvailableSpots !== undefined) {
      orderFilter.hasAvailableSpots = filterParams.hasAvailableSpots;
    }
    if (filterParams?.isUnassigned !== undefined) {
      orderFilter.isUnassigned = filterParams.isUnassigned;
    }

    const request = new GetPagedOrdersRequest({
      offset: this.currentOffset(),
      limit: this.currentLimit(),
      filter: orderFilter,
      sort: this.currentSort(),
    });

    this.adminClient.adminOrderClient
      .getPaged(request)
      .pipe(
        takeUntil(this.destroy$),
        catchError((error) => {
          this.snackbarService.showError(
            this.translate.instant('pages.order_management.messages.load_error')
          );
          console.error('Error loading orders:', error);
          return of(null);
        }),
        finalize(() => this.loading.set(false))
      )
      .subscribe((response) => {
        if (response) {
          this.orders.set(response.data || []);
          this.totalRecords.set(response.total || 0);
        }
      });
  }

  onPageChange(offset: number, limit: number): void {
    this.currentOffset.set(offset);
    this.currentLimit.set(limit);
    this.loadOrders();
  }

  onSortChange(sort: SortDefinition[] | undefined): void {
    this.currentSort.set(sort);
    this.loadOrders();
  }

  applyFilter(filter: OrderFilterParams): void {
    this.currentFilter.set(filter);
    this.currentOffset.set(0);
    this.loadOrders();
  }

  resetFilter(): void {
    this.currentFilter.set(null);
    this.currentOffset.set(0);
    this.loadOrders();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }
}
