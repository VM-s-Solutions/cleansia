import { CommonModule } from '@angular/common';
import {
  AfterViewInit,
  ChangeDetectorRef,
  Component,
  computed,
  inject,
  OnDestroy,
  signal,
  TemplateRef,
  viewChild,
} from '@angular/core';
import { FormBuilder, FormsModule, ReactiveFormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import {
  OrderListItem,
  OrderStatus,
  PaymentStatus,
  SortDefinition,
  SortDirection,
} from '@cleansia/admin-services';
import {
  CleansiaButtonComponent,
  CleansiaCalendarComponent,
  CleansiaCheckboxComponent,
  CleansiaLanguageSwitcherComponent,
  CleansiaLoaderComponent,
  CleansiaSectionComponent,
  CleansiaTableComponent,
  CleansiaTextInputComponent,
  CleansiaTitleComponent,
  TableColumn,
  TableAction,
  PaginationState,
} from '@cleansia/components';
import { CleansiaAdminRoute } from '@cleansia/services';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { ToastModule } from 'primeng/toast';
import { TooltipModule } from 'primeng/tooltip';
import { debounceTime, distinctUntilChanged, Subject, takeUntil } from 'rxjs';
import { OrderManagementFacade } from './order-management.facade';
import {
  getOrderStatusClass,
  getOrderTableDefinition,
  getPaymentStatusClass,
} from './order-management.models';

@Component({
  selector: 'cleansia-admin-order-management',
  standalone: true,
  imports: [
    CommonModule,
    CleansiaButtonComponent,
    CleansiaCalendarComponent,
    CleansiaCheckboxComponent,
    CleansiaTextInputComponent,
    TranslatePipe,
    CleansiaTableComponent,
    CleansiaTitleComponent,
    CleansiaLoaderComponent,
    CleansiaSectionComponent,
    CleansiaLanguageSwitcherComponent,
    FormsModule,
    ReactiveFormsModule,
    ToastModule,
    TooltipModule,
  ],
  templateUrl: './order-management.component.html',
  providers: [OrderManagementFacade],
})
export class OrderManagementComponent implements AfterViewInit, OnDestroy {
  private readonly cd = inject(ChangeDetectorRef);
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);
  protected readonly facade = inject(OrderManagementFacade);
  private readonly translate = inject(TranslateService);

  orderStatusTemplate = viewChild<TemplateRef<any>>('orderStatusTemplate');
  paymentStatusTemplate = viewChild<TemplateRef<any>>('paymentStatusTemplate');

  orderColumns!: TableColumn<OrderListItem>[];
  orderActions!: TableAction<OrderListItem>[];

  readonly OrderStatus = OrderStatus;
  readonly PaymentStatus = PaymentStatus;

  private lastSortField: string | null = null;
  private lastSortOrder: number | null = null;
  private destroy$ = new Subject<void>();

  filterForm = this.fb.group({
    orderStatus: [[] as OrderStatus[]],
    paymentStatus: [[] as PaymentStatus[]],
    searchTerm: [''],
    cleaningDateFrom: [null as Date | null],
    cleaningDateTo: [null as Date | null],
  });

  orderStatusMultiOptions: { label: string; value: OrderStatus }[] = [];
  paymentStatusMultiOptions: { label: string; value: PaymentStatus }[] = [];

  // Filter drawer state
  isFilterDrawerOpen = signal(false);
  // Signal to trigger recalculation of filter chips when form changes
  private filterFormVersion = signal(0);
  activeFilterChips = computed(() => {
    // Access the signal to create dependency
    this.filterFormVersion();
    return this.getActiveFilterChips();
  });
  hasActiveFilters = computed(() => this.activeFilterChips().length > 0);
  activeFilterCount = computed(() => this.activeFilterChips().length);

  ngAfterViewInit(): void {
    this.rebuildTableDefinitions();
    this.rebuildFilterOptions();
    this.cd.detectChanges();

    this.filterForm.valueChanges
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => {
        // Update version to trigger computed recalculation
        this.filterFormVersion.update(v => v + 1);
      });

    this.filterForm.valueChanges
      .pipe(debounceTime(500), distinctUntilChanged(), takeUntil(this.destroy$))
      .subscribe(() => {
        this.applyFilters();
      });

    // Rebuild tables and filter options when language changes
    this.translate.onLangChange
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => {
        this.rebuildTableDefinitions();
        this.rebuildFilterOptions();
        this.cd.detectChanges();
      });

    this.facade.loadOrders();
  }

  private rebuildTableDefinitions(): void {
    const tableDef = getOrderTableDefinition(
      {
        onViewDetails: this.viewOrderDetails.bind(this),
      },
      this.translate,
      this.orderStatusTemplate(),
      this.paymentStatusTemplate()
    );
    this.orderColumns = tableDef.columns;
    this.orderActions = tableDef.actions;
  }

  private rebuildFilterOptions(): void {
    this.orderStatusMultiOptions = [
      {
        label: this.translate.instant('pages.order_management.order_status.pending'),
        value: OrderStatus.Pending,
      },
      {
        label: this.translate.instant('pages.order_management.order_status.confirmed'),
        value: OrderStatus.Confirmed,
      },
      {
        label: this.translate.instant('pages.order_management.order_status.in_progress'),
        value: OrderStatus.InProgress,
      },
      {
        label: this.translate.instant('pages.order_management.order_status.completed'),
        value: OrderStatus.Completed,
      },
      {
        label: this.translate.instant('pages.order_management.order_status.cancelled'),
        value: OrderStatus.Cancelled,
      },
    ];

    this.paymentStatusMultiOptions = [
      {
        label: this.translate.instant('pages.order_management.payment_status.pending'),
        value: PaymentStatus.Pending,
      },
      {
        label: this.translate.instant('pages.order_management.payment_status.paid'),
        value: PaymentStatus.Paid,
      },
      {
        label: this.translate.instant('pages.order_management.payment_status.failed'),
        value: PaymentStatus.Failed,
      },
      {
        label: this.translate.instant('pages.order_management.payment_status.refunded'),
        value: PaymentStatus.Refunded,
      },
      {
        label: this.translate.instant('pages.order_management.payment_status.disputed'),
        value: PaymentStatus.Disputed,
      },
    ];
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  viewOrderDetails(order: OrderListItem): void {
    this.router.navigate([CleansiaAdminRoute.ORDER_MANAGEMENT, order.id]);
  }

  getOrderStatusClass(order: OrderListItem): string {
    return getOrderStatusClass(order);
  }

  getPaymentStatusClass(order: OrderListItem): string {
    return getPaymentStatusClass(order);
  }

  getOrderStatusLabel(order: OrderListItem): string {
    if (!order.orderStatus?.value) return '';
    switch (order.orderStatus.value) {
      case OrderStatus.Pending:
        return this.translate.instant('pages.order_management.order_status.pending');
      case OrderStatus.Confirmed:
        return this.translate.instant('pages.order_management.order_status.confirmed');
      case OrderStatus.InProgress:
        return this.translate.instant('pages.order_management.order_status.in_progress');
      case OrderStatus.Completed:
        return this.translate.instant('pages.order_management.order_status.completed');
      case OrderStatus.Cancelled:
        return this.translate.instant('pages.order_management.order_status.cancelled');
      default:
        return order.orderStatus?.name || '';
    }
  }

  getPaymentStatusLabel(order: OrderListItem): string {
    if (!order.paymentStatus?.value) return '';
    switch (order.paymentStatus.value) {
      case PaymentStatus.Pending:
        return this.translate.instant('pages.order_management.payment_status.pending');
      case PaymentStatus.Paid:
        return this.translate.instant('pages.order_management.payment_status.paid');
      case PaymentStatus.Failed:
        return this.translate.instant('pages.order_management.payment_status.failed');
      case PaymentStatus.Refunded:
        return this.translate.instant('pages.order_management.payment_status.refunded');
      case PaymentStatus.Disputed:
        return this.translate.instant('pages.order_management.payment_status.disputed');
      default:
        return order.paymentStatus?.name || '';
    }
  }

  applyFilters(): void {
    const formValues = this.filterForm.value;

    this.facade.applyFilter({
      orderStatuses:
        formValues.orderStatus && formValues.orderStatus.length > 0
          ? formValues.orderStatus
          : undefined,
      paymentStatuses:
        formValues.paymentStatus && formValues.paymentStatus.length > 0
          ? formValues.paymentStatus
          : undefined,
      searchTerm: formValues.searchTerm?.trim() || undefined,
      cleaningDateFrom: formValues.cleaningDateFrom ?? undefined,
      cleaningDateTo: formValues.cleaningDateTo ?? undefined,
    });
  }

  resetFilters(): void {
    this.filterForm.reset({
      orderStatus: [],
      paymentStatus: [],
      searchTerm: '',
      cleaningDateFrom: null,
      cleaningDateTo: null,
    });
    this.facade.resetFilter();
  }

  onSortChange(event: { field: string; order: number }): void {
    if (
      event.field === this.lastSortField &&
      event.order === this.lastSortOrder
    ) {
      return;
    }

    this.lastSortField = event.field;
    this.lastSortOrder = event.order;

    const sortDirection =
      event.order === 1 ? SortDirection.Ascending : SortDirection.Descending;
    const sort = [
      new SortDefinition({
        field: event.field,
        direction: sortDirection,
      }),
    ];
    this.facade.onSortChange(sort);
  }

  onPageChange(event: PaginationState): void {
    const offset = event.first;
    const limit = event.rows;
    this.facade.onPageChange(offset, limit);
  }

  // Filter drawer methods
  openFilterDrawer(): void {
    this.isFilterDrawerOpen.set(true);
  }

  closeFilterDrawer(): void {
    this.isFilterDrawerOpen.set(false);
  }

  getActiveFilterChips(): { key: string; label: string; value: string }[] {
    const chips: { key: string; label: string; value: string }[] = [];
    const values = this.filterForm.value;

    if (values.searchTerm) {
      chips.push({
        key: 'searchTerm',
        label: this.translate.instant('pages.order_management.filters.search'),
        value: values.searchTerm,
      });
    }

    if (values.orderStatus && values.orderStatus.length > 0) {
      const statusLabels = values.orderStatus
        .map((s) => this.orderStatusMultiOptions.find((o) => o.value === s)?.label)
        .filter(Boolean)
        .join(', ');
      chips.push({
        key: 'orderStatus',
        label: this.translate.instant('pages.order_management.filters.order_status'),
        value: statusLabels,
      });
    }

    if (values.paymentStatus && values.paymentStatus.length > 0) {
      const statusLabels = values.paymentStatus
        .map((s) => this.paymentStatusMultiOptions.find((o) => o.value === s)?.label)
        .filter(Boolean)
        .join(', ');
      chips.push({
        key: 'paymentStatus',
        label: this.translate.instant('pages.order_management.filters.payment_status'),
        value: statusLabels,
      });
    }

    if (values.cleaningDateFrom) {
      chips.push({
        key: 'cleaningDateFrom',
        label: this.translate.instant('pages.order_management.filters.date_from'),
        value: values.cleaningDateFrom.toLocaleDateString(),
      });
    }

    if (values.cleaningDateTo) {
      chips.push({
        key: 'cleaningDateTo',
        label: this.translate.instant('pages.order_management.filters.date_to'),
        value: values.cleaningDateTo.toLocaleDateString(),
      });
    }

    return chips;
  }

  removeFilterChip(key: string): void {
    if (key === 'orderStatus') {
      this.filterForm.patchValue({ orderStatus: [] });
    } else if (key === 'paymentStatus') {
      this.filterForm.patchValue({ paymentStatus: [] });
    } else if (key === 'cleaningDateFrom') {
      this.filterForm.patchValue({ cleaningDateFrom: null });
    } else if (key === 'cleaningDateTo') {
      this.filterForm.patchValue({ cleaningDateTo: null });
    } else {
      this.filterForm.patchValue({ [key]: '' });
    }
    this.applyFilters();
  }

  clearAllFilters(): void {
    this.resetFilters();
  }

  // Checkbox helper methods for order status
  isOrderStatusChecked(status: OrderStatus): boolean {
    return this.filterForm.value.orderStatus?.includes(status) ?? false;
  }

  toggleOrderStatus(status: OrderStatus): void {
    const isChecked = this.isOrderStatusChecked(status);
    this.onOrderStatusChange(status, !isChecked);
  }

  onOrderStatusChange(status: OrderStatus, checked: boolean): void {
    const currentStatuses = [...(this.filterForm.value.orderStatus || [])];

    if (checked) {
      if (!currentStatuses.includes(status)) {
        currentStatuses.push(status);
      }
    } else {
      const index = currentStatuses.indexOf(status);
      if (index > -1) {
        currentStatuses.splice(index, 1);
      }
    }

    this.filterForm.patchValue({ orderStatus: currentStatuses });
  }

  // Checkbox helper methods for payment status
  isPaymentStatusChecked(status: PaymentStatus): boolean {
    return this.filterForm.value.paymentStatus?.includes(status) ?? false;
  }

  togglePaymentStatus(status: PaymentStatus): void {
    const isChecked = this.isPaymentStatusChecked(status);
    this.onPaymentStatusChange(status, !isChecked);
  }

  onPaymentStatusChange(status: PaymentStatus, checked: boolean): void {
    const currentStatuses = [...(this.filterForm.value.paymentStatus || [])];

    if (checked) {
      if (!currentStatuses.includes(status)) {
        currentStatuses.push(status);
      }
    } else {
      const index = currentStatuses.indexOf(status);
      if (index > -1) {
        currentStatuses.splice(index, 1);
      }
    }

    this.filterForm.patchValue({ paymentStatus: currentStatuses });
  }
}
