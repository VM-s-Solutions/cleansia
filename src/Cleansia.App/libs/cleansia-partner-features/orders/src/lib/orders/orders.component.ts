import { CommonModule } from '@angular/common';
import {
  AfterViewInit,
  ChangeDetectorRef,
  Component,
  inject,
  OnDestroy,
  TemplateRef,
  viewChild,
} from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { debounceTime, distinctUntilChanged, Subject, takeUntil } from 'rxjs';
import {
  CleansiaCalendarComponent,
  CleansiaLanguageSwitcherComponent,
  CleansiaLoaderComponent,
  CleansiaSectionComponent,
  CleansiaSelectComponent,
  CleansiaTableComponent,
  CleansiaTextInputComponent,
  CleansiaTitleComponent,
  ICleansiaSelectOption,
  TableDefinition,
} from '@cleansia/components';
import { OrderFilter } from '@cleansia/models';
import {
  OrderListItem,
  OrderStatus,
  PaymentStatus,
  PaymentType,
  SortDefinition,
  SortDirection,
} from '@cleansia/services';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { ButtonModule } from 'primeng/button';
import { MultiSelectModule } from 'primeng/multiselect';
import { TableModule } from 'primeng/table';
import { TabsModule } from 'primeng/tabs';
import { ToastModule } from 'primeng/toast';
import { DialogService } from 'primeng/dynamicdialog';
import { OrdersFacade } from './orders.facade';
import {
  getAvailableOrdersTableDefinition,
  getMyOrdersTableDefinition,
} from './orders.models';

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
    CleansiaSelectComponent,
    CleansiaCalendarComponent,
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

  availableOrdersTableDefinition!: TableDefinition<OrderListItem>;
  myOrdersTableDefinition!: TableDefinition<OrderListItem>;

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
  });

  // Filter options
  orderStatusOptions: ICleansiaSelectOption[] = [
    { label: this.translate.instant('enums.order_status.pending'), value: OrderStatus.Pending },
    { label: this.translate.instant('enums.order_status.confirmed'), value: OrderStatus.Confirmed },
    { label: this.translate.instant('enums.order_status.in_progress'), value: OrderStatus.InProgress },
    { label: this.translate.instant('enums.order_status.completed'), value: OrderStatus.Completed },
    { label: this.translate.instant('enums.order_status.cancelled'), value: OrderStatus.Cancelled },
  ];

  paymentStatusOptions: ICleansiaSelectOption[] = [
    { label: this.translate.instant('enums.payment_status.pending'), value: PaymentStatus.Pending },
    { label: this.translate.instant('enums.payment_status.paid'), value: PaymentStatus.Paid },
    { label: this.translate.instant('enums.payment_status.failed'), value: PaymentStatus.Failed },
    { label: this.translate.instant('enums.payment_status.refunded'), value: PaymentStatus.Refunded },
  ];

  // Multiselect options for PrimeNG
  orderStatusMultiOptions = this.orderStatusOptions;
  paymentStatusMultiOptions = this.paymentStatusOptions;

  ngAfterViewInit(): void {
    this.availableOrdersTableDefinition = getAvailableOrdersTableDefinition(
      {
        onViewDetails: this.viewOrderDetails.bind(this),
        onTakeOrder: this.takeOrder.bind(this),
      },
      this.translate,
      this.statusTemplate(),
      this.orderStatusTemplate()
    );

    this.myOrdersTableDefinition = getMyOrdersTableDefinition(
      {
        onViewDetails: this.viewOrderDetails.bind(this),
        onCompleteOrder: this.completeOrder.bind(this),
      },
      this.translate,
      this.statusTemplate(),
      this.orderStatusTemplate()
    );

    this.cd.detectChanges();

    // Setup automatic filtering with debounce
    this.searchForm.valueChanges
      .pipe(
        debounceTime(500),
        distinctUntilChanged(),
        takeUntil(this.destroy$)
      )
      .subscribe(() => {
        this.applyFilters();
      });
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
    this.router.navigate(['/orders', order.id]);
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

  applyFilters(): void {
    const formValues = this.searchForm.value;

    const filter = new OrderFilter({
      customerName: formValues.customerName || undefined,
      customerEmail: formValues.customerEmail || undefined,
      displayOrderNumber: formValues.displayOrderNumber || undefined,
      orderStatuses: formValues.orderStatuses && formValues.orderStatuses.length > 0
        ? formValues.orderStatuses
        : undefined,
      paymentStatuses: formValues.paymentStatuses && formValues.paymentStatuses.length > 0
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
}
