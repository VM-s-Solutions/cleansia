import {
  AfterViewInit,
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  computed,
  inject,
  signal,
  TemplateRef,
  viewChild,
} from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import {
  CleansiaCalendarComponent,
  CleansiaSectionComponent,
  CleansiaTableComponent,
  CleansiaTextInputComponent,
  CleansiaTitleComponent,
  CleansiaCheckboxComponent,
  CleansiaButtonComponent,
  CleansiaHelpCardComponent,
  ICleansiaSelectOption,
  TableColumn,
  TableAction,
  PaginationState,
} from '@cleansia/components';
import {
  OrderListItem,
  SortDefinition,
  SortDirection,
} from '@cleansia/partner-services';
import { CleansiaPartnerRoute } from '@cleansia/services';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { DialogService } from 'primeng/dynamicdialog';
import { TabsModule } from 'primeng/tabs';
import { OrdersFacade } from './orders.facade';
import {
  getAvailableOrdersTableDefinition,
  getMyOrdersTableDefinition,
  ORDERS_HELP_STEPS,
  ORDER_STATUS_FLOW,
  PAYMENT_STATUS_FLOW,
} from './orders.models';
import {
  getStatusClass,
  getOrderStatusClass,
  getTranslatedPaymentStatus,
  getTranslatedOrderStatus,
  buildOrderStatusOptions,
  buildPaymentStatusOptions,
  buildActiveFilterChips,
  buildOrderFilter,
} from './orders.helpers';

@Component({
  selector: 'cleansia-partner-orders',
  standalone: true,
  imports: [
    TranslatePipe,
    TabsModule,
    CleansiaTableComponent,
    CleansiaTitleComponent,
    CleansiaSectionComponent,
    CleansiaTextInputComponent,
    CleansiaCalendarComponent,
    CleansiaCheckboxComponent,
    CleansiaButtonComponent,
    CleansiaHelpCardComponent,
    ReactiveFormsModule,
  ],
  templateUrl: './orders.component.html',
  providers: [OrdersFacade, DialogService],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class OrdersComponent implements AfterViewInit {
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

  // Filter options — derived first so the form below can build a control per option.
  orderStatusOptions: ICleansiaSelectOption[] = buildOrderStatusOptions(this.translate);
  paymentStatusOptions: ICleansiaSelectOption[] = buildPaymentStatusOptions(this.translate);

  // Search form. Status checkboxes are built from the option lists so adding
  // an enum value doesn't require also declaring a matching FormControl.
  searchForm = this.fb.group({
    customerName: [''],
    customerEmail: ['', [Validators.email]],
    displayOrderNumber: [''],
    orderStatuses: [[] as number[]],
    paymentStatuses: [[] as number[]],
    cleaningDateFrom: [null as Date | null],
    cleaningDateTo: [null as Date | null],
    ...Object.fromEntries(
      this.orderStatusOptions.map((o) => [`orderStatus_${o.value}`, [false]]),
    ),
    ...Object.fromEntries(
      this.paymentStatusOptions.map((o) => [`paymentStatus_${o.value}`, [false]]),
    ),
  });
  orderStatusMultiOptions = this.orderStatusOptions;
  paymentStatusMultiOptions = this.paymentStatusOptions;

  // Help card / status flow constants
  ordersHelpSteps = ORDERS_HELP_STEPS;
  orderStatusFlow = ORDER_STATUS_FLOW;
  paymentStatusFlow = PAYMENT_STATUS_FLOW;

  // Filter drawer state
  isFilterDrawerOpen = signal<boolean>(false);

  // Help card dismissal state
  private helpDismissedVersion = signal(0);
  isOrdersHelpDismissed = computed(() => {
    this.helpDismissedVersion();
    return CleansiaHelpCardComponent.isHelpDismissed('cleansia-orders-help-dismissed');
  });
  isPaymentHelpDismissed = computed(() => {
    this.helpDismissedVersion();
    return CleansiaHelpCardComponent.isHelpDismissed('cleansia-orders-payment-help-dismissed');
  });

  // Filter reactivity
  private filterFormVersion = signal(0);
  activeFilterChips = computed(() => {
    this.filterFormVersion();
    return buildActiveFilterChips(
      this.searchForm.value,
      this.orderStatusMultiOptions,
      this.paymentStatusMultiOptions,
      this.translate
    );
  });
  activeFilterCount = computed(() => this.activeFilterChips().length);
  hasActiveFilters = computed(() => this.activeFilterCount() > 0);

  ngAfterViewInit(): void {
    this.rebuildTableDefinitions();
    this.rebuildFilterOptions();
    this.cd.detectChanges();

    this.facade.bindFormChanges(
      this.searchForm,
      () => this.filterFormVersion.update((v) => v + 1),
      () => this.applyFilters(),
      () => {
        this.rebuildTableDefinitions();
        this.rebuildFilterOptions();
        this.cd.detectChanges();
      }
    );
  }

  private rebuildTableDefinitions(): void {
    const availableDef = getAvailableOrdersTableDefinition(
      { onTakeOrder: this.takeOrder.bind(this) },
      this.statusTemplate(),
      this.orderStatusTemplate()
    );
    this.availableOrdersColumns = availableDef.columns;
    this.availableOrdersActions = availableDef.actions;

    const myOrdersDef = getMyOrdersTableDefinition(
      {
        onStartOrder: (row) => this.facade.startOrder(row.id!),
        onCompleteOrder: this.completeOrder.bind(this),
      },
      this.statusTemplate(),
      this.orderStatusTemplate()
    );
    this.myOrdersColumns = myOrdersDef.columns;
    this.myOrdersActions = myOrdersDef.actions;
  }

  private rebuildFilterOptions(): void {
    this.orderStatusOptions = buildOrderStatusOptions(this.translate);
    this.paymentStatusOptions = buildPaymentStatusOptions(this.translate);
    this.orderStatusMultiOptions = this.orderStatusOptions;
    this.paymentStatusMultiOptions = this.paymentStatusOptions;
  }

  onAvailableSortChange(event: { field: string; order: number }): void {
    this.facade.setActiveTab('available');
    this.onSortChange(event);
  }

  onMySortChange(event: { field: string; order: number }): void {
    this.facade.setActiveTab('my');
    this.onSortChange(event);
  }

  onAvailableOrdersPageChange(event: PaginationState): void {
    this.facade.loadAvailableOrders(event.first, event.rows);
  }

  onMyOrdersPageChange(event: PaginationState): void {
    this.facade.loadMyOrders(event.first, event.rows);
  }

  onSortChange(event: { field: string; order: number }): void {
    if (event.field === this.lastSortField && event.order === this.lastSortOrder) {
      return;
    }
    this.lastSortField = event.field;
    this.lastSortOrder = event.order;

    this.facade.updateSort([
      new SortDefinition({
        field: event.field,
        direction: event.order === 1 ? SortDirection.Ascending : SortDirection.Descending,
      }),
    ]);
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

  // Delegate to extracted helpers — keep callable from template
  getStatusClass(order: OrderListItem): string {
    return getStatusClass(order);
  }

  getOrderStatusClass(order: OrderListItem): string {
    return getOrderStatusClass(order);
  }

  getTranslatedPaymentStatus(paymentStatus: { name?: string } | null | undefined): string {
    return getTranslatedPaymentStatus(paymentStatus, this.translate);
  }

  getTranslatedOrderStatus(orderStatus: { name?: string } | null | undefined): string {
    return getTranslatedOrderStatus(orderStatus, this.translate);
  }

  applyFilters(): void {
    this.facade.applyFilters(buildOrderFilter(this.searchForm.value));
  }

  resetFilters(): void {
    this.searchForm.reset();
    this.facade.resetFilters();
  }

  openFilterDrawer(): void {
    this.isFilterDrawerOpen.set(true);
  }

  closeFilterDrawer(): void {
    this.isFilterDrawerOpen.set(false);
  }

  removeFilterChip(chipKey: string): void {
    if (chipKey === 'orderStatuses') {
      const resetValues: Record<string, any> = { orderStatuses: [] };
      this.orderStatusOptions.forEach((opt) => {
        resetValues[`orderStatus_${opt.value}`] = false;
      });
      this.searchForm.patchValue(resetValues);
    } else if (chipKey === 'paymentStatuses') {
      const resetValues: Record<string, any> = { paymentStatuses: [] };
      this.paymentStatusOptions.forEach((opt) => {
        resetValues[`paymentStatus_${opt.value}`] = false;
      });
      this.searchForm.patchValue(resetValues);
    } else {
      this.searchForm.patchValue({ [chipKey]: null });
    }
  }

  clearAllFilters(): void {
    this.resetFilters();
  }

  onOrderStatusChange(checked: boolean, statusValue: number): void {
    const currentStatuses = this.searchForm.get('orderStatuses')?.value || [];
    this.searchForm.patchValue({
      orderStatuses: checked
        ? [...currentStatuses, statusValue]
        : currentStatuses.filter((s: number) => s !== statusValue),
    });
  }

  onPaymentStatusChange(checked: boolean, statusValue: number): void {
    const currentStatuses = this.searchForm.get('paymentStatuses')?.value || [];
    this.searchForm.patchValue({
      paymentStatuses: checked
        ? [...currentStatuses, statusValue]
        : currentStatuses.filter((s: number) => s !== statusValue),
    });
  }

  onHelpDismissedChange(): void {
    this.helpDismissedVersion.update(v => v + 1);
  }

  restoreAllHelp(): void {
    this.ordersHelpCard()?.restore();
    this.paymentHelpCard()?.restore();
    this.helpDismissedVersion.update(v => v + 1);
  }
}
