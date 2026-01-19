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
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import {
  CleansiaCalendarComponent,
  CleansiaLanguageSwitcherComponent,
  CleansiaLoaderComponent,
  CleansiaSectionComponent,
  CleansiaTableComponent,
  CleansiaTextInputComponent,
  CleansiaTitleComponent,
  CleansiaCheckboxComponent,
  CleansiaButtonComponent,
  CleansiaHelpCardComponent,
  HelpStep,
  StatusFlowItem,
  ICleansiaSelectOption,
  TableColumn,
  TableAction,
  PaginationState,
} from '@cleansia/components';
import { OrderFilter } from '@cleansia/models';
import {
  OrderListItem,
  OrderStatus,
  PaymentStatus,
  SortDefinition,
  SortDirection,
} from '@cleansia/partner-services';
import { CleansiaPartnerRoute } from '@cleansia/services';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { ButtonModule } from 'primeng/button';
import { DialogService } from 'primeng/dynamicdialog';
import { MultiSelectModule } from 'primeng/multiselect';
import { TableModule } from 'primeng/table';
import { TabsModule } from 'primeng/tabs';
import { ToastModule } from 'primeng/toast';
import { debounceTime, distinctUntilChanged, Subject, takeUntil } from 'rxjs';
import { OrdersFacade } from './orders.facade';
import {
  getAvailableOrdersTableDefinition,
  getMyOrdersTableDefinition,
} from './orders.models';

interface FilterChip {
  key: string;
  label: string;
  value: string;
}

@Component({
  selector: 'cleansia-partner-orders',
  standalone: true,
  imports: [
    TableModule,
    ToastModule,
    CommonModule,
    ButtonModule,
    TranslatePipe,
    TabsModule,
    CleansiaTableComponent,
    CleansiaTitleComponent,
    CleansiaLoaderComponent,
    CleansiaSectionComponent,
    CleansiaLanguageSwitcherComponent,
    CleansiaTextInputComponent,
    CleansiaCalendarComponent,
    CleansiaCheckboxComponent,
    CleansiaButtonComponent,
    CleansiaHelpCardComponent,
    ReactiveFormsModule,
    MultiSelectModule,
  ],
  templateUrl: './orders.component.html',
  providers: [OrdersFacade, DialogService],
})
export class OrdersComponent implements AfterViewInit, OnDestroy {
  private readonly router = inject(Router);
  private readonly cd = inject(ChangeDetectorRef);
  private readonly fb = inject(FormBuilder);
  protected readonly facade = inject(OrdersFacade);
  private readonly translate = inject(TranslateService);

  statusTemplate = viewChild<TemplateRef<any>>('statusTemplate');
  orderStatusTemplate = viewChild<TemplateRef<any>>('orderStatusTemplate');
  ordersHelpCard = viewChild<CleansiaHelpCardComponent>('ordersHelpCard');
  paymentHelpCard = viewChild<CleansiaHelpCardComponent>('paymentHelpCard');

  availableOrdersColumns!: TableColumn<OrderListItem>[];
  availableOrdersActions!: TableAction<OrderListItem>[];
  myOrdersColumns!: TableColumn<OrderListItem>[];
  myOrdersActions!: TableAction<OrderListItem>[];

  private lastSortField: string | null = null;
  private lastSortOrder: number | null = null;
  private destroy$ = new Subject<void>();

  // Search form
  searchForm = this.fb.group({
    customerName: [''],
    customerEmail: ['', [Validators.email]],
    displayOrderNumber: [''],
    orderStatuses: [[] as number[]],
    paymentStatuses: [[] as number[]],
    cleaningDateFrom: [null as Date | null],
    cleaningDateTo: [null as Date | null],
    // Individual checkbox controls for Order Status
    orderStatus_0: [false],
    orderStatus_1: [false],
    orderStatus_2: [false],
    orderStatus_3: [false],
    orderStatus_4: [false],
    orderStatus_5: [false],
    // Individual checkbox controls for Payment Status
    paymentStatus_0: [false],
    paymentStatus_1: [false],
    paymentStatus_2: [false],
    paymentStatus_3: [false],
    paymentStatus_4: [false],
  });

  // Filter options
  orderStatusOptions: ICleansiaSelectOption[] = [
    {
      label: this.translate.instant('enums.order_status.pending'),
      value: OrderStatus.Pending,
    },
    {
      label: this.translate.instant('enums.order_status.confirmed'),
      value: OrderStatus.Confirmed,
    },
    {
      label: this.translate.instant('enums.order_status.in_progress'),
      value: OrderStatus.InProgress,
    },
    {
      label: this.translate.instant('enums.order_status.completed'),
      value: OrderStatus.Completed,
    },
    {
      label: this.translate.instant('enums.order_status.cancelled'),
      value: OrderStatus.Cancelled,
    },
  ];

  paymentStatusOptions: ICleansiaSelectOption[] = [
    {
      label: this.translate.instant('enums.payment_status.pending'),
      value: PaymentStatus.Pending,
    },
    {
      label: this.translate.instant('enums.payment_status.paid'),
      value: PaymentStatus.Paid,
    },
    {
      label: this.translate.instant('enums.payment_status.failed'),
      value: PaymentStatus.Failed,
    },
    {
      label: this.translate.instant('enums.payment_status.refunded'),
      value: PaymentStatus.Refunded,
    },
  ];

  // Multiselect options for PrimeNG
  orderStatusMultiOptions = this.orderStatusOptions;
  paymentStatusMultiOptions = this.paymentStatusOptions;

  // Help card steps for orders workflow
  ordersHelpSteps: HelpStep[] = [
    {
      icon: 'pi pi-search',
      titleKey: 'help.orders.step1_title',
      descriptionKey: 'help.orders.step1_desc',
    },
    {
      icon: 'pi pi-check-circle',
      titleKey: 'help.orders.step2_title',
      descriptionKey: 'help.orders.step2_desc',
    },
    {
      icon: 'pi pi-briefcase',
      titleKey: 'help.orders.step3_title',
      descriptionKey: 'help.orders.step3_desc',
    },
    {
      icon: 'pi pi-wallet',
      titleKey: 'help.orders.step4_title',
      descriptionKey: 'help.orders.step4_desc',
    },
  ];

  // Order status flow explanations
  orderStatusFlow: StatusFlowItem[] = [
    {
      statusKey: 'enums.order_status.pending',
      descriptionKey: 'help.orders.status.pending_desc',
      colorClass: 'status-pending',
    },
    {
      statusKey: 'enums.order_status.confirmed',
      descriptionKey: 'help.orders.status.confirmed_desc',
      colorClass: 'status-confirmed',
    },
    {
      statusKey: 'enums.order_status.in_progress',
      descriptionKey: 'help.orders.status.in_progress_desc',
      colorClass: 'status-in-progress',
    },
    {
      statusKey: 'enums.order_status.completed',
      descriptionKey: 'help.orders.status.completed_desc',
      colorClass: 'status-completed',
    },
    {
      statusKey: 'enums.order_status.cancelled',
      descriptionKey: 'help.orders.status.cancelled_desc',
      colorClass: 'status-cancelled',
    },
  ];

  // Payment status flow explanations
  paymentStatusFlow: StatusFlowItem[] = [
    {
      statusKey: 'enums.payment_status.pending',
      descriptionKey: 'help.orders.payment.pending_desc',
      colorClass: 'status-pending',
    },
    {
      statusKey: 'enums.payment_status.paid',
      descriptionKey: 'help.orders.payment.paid_desc',
      colorClass: 'status-paid',
    },
    {
      statusKey: 'enums.payment_status.failed',
      descriptionKey: 'help.orders.payment.failed_desc',
      colorClass: 'status-failed',
    },
    {
      statusKey: 'enums.payment_status.refunded',
      descriptionKey: 'help.orders.payment.refunded_desc',
      colorClass: 'status-refunded',
    },
  ];

  // Filter drawer state
  isFilterDrawerOpen = signal<boolean>(false);

  // Help card dismissal state
  private helpDismissedVersion = signal(0);
  isOrdersHelpDismissed = computed(() => {
    this.helpDismissedVersion(); // Track for reactivity
    return CleansiaHelpCardComponent.isHelpDismissed('cleansia-orders-help-dismissed');
  });
  isPaymentHelpDismissed = computed(() => {
    this.helpDismissedVersion(); // Track for reactivity
    return CleansiaHelpCardComponent.isHelpDismissed('cleansia-orders-payment-help-dismissed');
  });

  // Filter reactivity - increment this to trigger computed updates
  private filterFormVersion = signal(0);

  // Active filter chips - depend on filterFormVersion for reactivity
  activeFilterChips = computed(() => {
    this.filterFormVersion(); // Track this signal for reactivity
    return this.getActiveFilterChips();
  });
  activeFilterCount = computed(() => this.activeFilterChips().length);
  hasActiveFilters = computed(() => this.activeFilterCount() > 0);

  ngAfterViewInit(): void {
    this.rebuildTableDefinitions();
    this.rebuildFilterOptions();
    this.cd.detectChanges();

    // Update filter version on every form change for reactive filter chips
    this.searchForm.valueChanges
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => {
        this.filterFormVersion.update(v => v + 1);
      });

    // Setup automatic filtering with debounce
    this.searchForm.valueChanges
      .pipe(debounceTime(500), distinctUntilChanged(), takeUntil(this.destroy$))
      .subscribe(() => {
        this.applyFilters();
      });

    // Rebuild tables and filters when language changes
    this.translate.onLangChange
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => {
        this.rebuildTableDefinitions();
        this.rebuildFilterOptions();
        this.cd.detectChanges();
      });
  }

  private rebuildTableDefinitions(): void {
    const availableDef = getAvailableOrdersTableDefinition(
      {
        onViewDetails: this.viewOrderDetails.bind(this),
        onTakeOrder: this.takeOrder.bind(this),
      },
      this.statusTemplate(),
      this.orderStatusTemplate()
    );
    this.availableOrdersColumns = availableDef.columns;
    this.availableOrdersActions = availableDef.actions;

    const myOrdersDef = getMyOrdersTableDefinition(
      {
        onViewDetails: this.viewOrderDetails.bind(this),
        onCompleteOrder: this.completeOrder.bind(this),
      },
      this.statusTemplate(),
      this.orderStatusTemplate()
    );
    this.myOrdersColumns = myOrdersDef.columns;
    this.myOrdersActions = myOrdersDef.actions;
  }

  private rebuildFilterOptions(): void {
    this.orderStatusOptions = [
      {
        label: this.translate.instant('enums.order_status.pending'),
        value: OrderStatus.Pending,
      },
      {
        label: this.translate.instant('enums.order_status.confirmed'),
        value: OrderStatus.Confirmed,
      },
      {
        label: this.translate.instant('enums.order_status.in_progress'),
        value: OrderStatus.InProgress,
      },
      {
        label: this.translate.instant('enums.order_status.completed'),
        value: OrderStatus.Completed,
      },
      {
        label: this.translate.instant('enums.order_status.cancelled'),
        value: OrderStatus.Cancelled,
      },
    ];

    this.paymentStatusOptions = [
      {
        label: this.translate.instant('enums.payment_status.pending'),
        value: PaymentStatus.Pending,
      },
      {
        label: this.translate.instant('enums.payment_status.paid'),
        value: PaymentStatus.Paid,
      },
      {
        label: this.translate.instant('enums.payment_status.failed'),
        value: PaymentStatus.Failed,
      },
      {
        label: this.translate.instant('enums.payment_status.refunded'),
        value: PaymentStatus.Refunded,
      },
    ];

    // Update multi-select options
    this.orderStatusMultiOptions = this.orderStatusOptions;
    this.paymentStatusMultiOptions = this.paymentStatusOptions;
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  onTabChange(tabIndex: string | number): void {
    tabIndex = Number(tabIndex);
    this.lastSortField = null;
    this.lastSortOrder = null;

    if (tabIndex === 0) {
      this.facade.setActiveTab('available');
      this.facade.loadAvailableOrders();
    } else if (tabIndex === 1) {
      this.facade.setActiveTab('my');
      this.facade.loadMyOrders();
    }
  }

  onAvailableOrdersPageChange(event: PaginationState): void {
    const offset = event.first;
    const limit = event.rows;
    this.facade.loadAvailableOrders(offset, limit);
  }

  onMyOrdersPageChange(event: PaginationState): void {
    const offset = event.first;
    const limit = event.rows;
    this.facade.loadMyOrders(offset, limit);
  }

  onSortChange(event: { field: string; order: number }): void {
    // Check if sort actually changed to prevent duplicate requests
    if (
      event.field === this.lastSortField &&
      event.order === this.lastSortOrder
    ) {
      return;
    }

    // Update last sort state
    this.lastSortField = event.field;
    this.lastSortOrder = event.order;

    const sortDef = [
      new SortDefinition({
        field: event.field,
        direction:
          event.order === 1
            ? SortDirection.Ascending
            : SortDirection.Descending,
      }),
    ];
    this.facade.updateSort(sortDef);
  }

  viewOrderDetails(order: OrderListItem): void {
    this.router.navigate([CleansiaPartnerRoute.ORDERS, order.id]);
  }

  takeOrder(order: OrderListItem): void {
    this.facade.takeOrder(order.id!);
  }

  completeOrder(order: OrderListItem): void {
    this.facade.openCompleteOrderDialog(order);
  }

  getStatusClass(order: OrderListItem): string {
    const statusName =
      order.paymentStatus?.name?.toLowerCase().replace(/\s+/g, '-') ||
      'pending';
    return `status-badge status-${statusName}`;
  }

  getOrderStatusClass(order: OrderListItem): string {
    const statusName =
      order.orderStatus?.name?.toLowerCase().replace(/\s+/g, '-') || 'pending';
    return `order-status-badge status-${statusName}`;
  }

  getTranslatedPaymentStatus(paymentStatus: any): string {
    if (!paymentStatus?.name) return '';
    const key = `enums.payment_status.${paymentStatus.name.toLowerCase().replace(/\s+/g, '_')}`;
    return this.translate.instant(key);
  }

  getTranslatedOrderStatus(orderStatus: any): string {
    if (!orderStatus?.name) return '';
    const key = `enums.order_status.${orderStatus.name.toLowerCase().replace(/\s+/g, '_')}`;
    return this.translate.instant(key);
  }

  applyFilters(): void {
    const formValues = this.searchForm.value;

    const filter = new OrderFilter({
      customerName: formValues.customerName || undefined,
      customerEmail: formValues.customerEmail || undefined,
      displayOrderNumber: formValues.displayOrderNumber || undefined,
      orderStatuses:
        formValues.orderStatuses && formValues.orderStatuses.length > 0
          ? formValues.orderStatuses
          : undefined,
      paymentStatuses:
        formValues.paymentStatuses && formValues.paymentStatuses.length > 0
          ? formValues.paymentStatuses
          : undefined,
      cleaningDateFrom: formValues.cleaningDateFrom || undefined,
      cleaningDateTo: formValues.cleaningDateTo || undefined,
    });

    this.facade.applyFilters(filter);
  }

  resetFilters(): void {
    this.searchForm.reset();
    this.facade.resetFilters();
  }

  toggleFilterDrawer(): void {
    this.isFilterDrawerOpen.update((v) => !v);
  }

  openFilterDrawer(): void {
    this.isFilterDrawerOpen.set(true);
  }

  closeFilterDrawer(): void {
    this.isFilterDrawerOpen.set(false);
  }

  getActiveFilterChips(): FilterChip[] {
    const chips: FilterChip[] = [];
    const formValue = this.searchForm.value;

    if (formValue.customerName) {
      chips.push({
        key: 'customerName',
        label: this.translate.instant('pages.orders.filters.customer_name'),
        value: formValue.customerName,
      });
    }

    if (formValue.customerEmail) {
      chips.push({
        key: 'customerEmail',
        label: this.translate.instant('pages.orders.filters.customer_email'),
        value: formValue.customerEmail,
      });
    }

    if (formValue.displayOrderNumber) {
      chips.push({
        key: 'displayOrderNumber',
        label: this.translate.instant('pages.orders.filters.order_number'),
        value: formValue.displayOrderNumber,
      });
    }

    if (formValue.orderStatuses?.length) {
      const statusNames = formValue.orderStatuses
        .map(
          (id) => this.orderStatusMultiOptions.find((o) => o.value === id)?.label
        )
        .filter(Boolean)
        .join(', ');
      chips.push({
        key: 'orderStatuses',
        label: this.translate.instant('pages.orders.filters.order_status'),
        value: statusNames,
      });
    }

    if (formValue.paymentStatuses?.length) {
      const statusNames = formValue.paymentStatuses
        .map(
          (id) =>
            this.paymentStatusMultiOptions.find((o) => o.value === id)?.label
        )
        .filter(Boolean)
        .join(', ');
      chips.push({
        key: 'paymentStatuses',
        label: this.translate.instant('pages.orders.filters.payment_status'),
        value: statusNames,
      });
    }

    if (formValue.cleaningDateFrom) {
      const dateStr = new Date(formValue.cleaningDateFrom).toLocaleDateString();
      chips.push({
        key: 'cleaningDateFrom',
        label: this.translate.instant('pages.orders.filters.cleaning_date_from'),
        value: dateStr,
      });
    }

    if (formValue.cleaningDateTo) {
      const dateStr = new Date(formValue.cleaningDateTo).toLocaleDateString();
      chips.push({
        key: 'cleaningDateTo',
        label: this.translate.instant('pages.orders.filters.cleaning_date_to'),
        value: dateStr,
      });
    }

    return chips;
  }

  removeFilterChip(chipKey: string): void {
    this.searchForm.patchValue({ [chipKey]: null });
    // The form valueChanges subscription will trigger applyFilters automatically
  }

  clearAllFilters(): void {
    this.resetFilters();
  }

  onOrderStatusChange(checked: boolean, statusValue: number): void {
    const currentStatuses = this.searchForm.get('orderStatuses')?.value || [];

    if (checked) {
      this.searchForm.patchValue({
        orderStatuses: [...currentStatuses, statusValue],
      });
    } else {
      this.searchForm.patchValue({
        orderStatuses: currentStatuses.filter((s: number) => s !== statusValue),
      });
    }
  }

  onPaymentStatusChange(checked: boolean, statusValue: number): void {
    const currentStatuses = this.searchForm.get('paymentStatuses')?.value || [];

    if (checked) {
      this.searchForm.patchValue({
        paymentStatuses: [...currentStatuses, statusValue],
      });
    } else {
      this.searchForm.patchValue({
        paymentStatuses: currentStatuses.filter(
          (s: number) => s !== statusValue
        ),
      });
    }
  }

  // Help card methods
  onHelpDismissedChange(): void {
    this.helpDismissedVersion.update(v => v + 1);
  }

  restoreAllHelp(): void {
    this.ordersHelpCard()?.restore();
    this.paymentHelpCard()?.restore();
    this.helpDismissedVersion.update(v => v + 1);
  }

}
