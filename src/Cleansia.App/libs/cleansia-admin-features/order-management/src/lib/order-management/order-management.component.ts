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
  buildFilterChips,
  buildFilterPayload,
  buildOrderStatusOptions,
  buildPaymentStatusOptions,
  FILTER_FORM_DEFAULTS,
  getFilterPatchForChipRemoval,
  getOrderStatusLabel,
  getPaymentStatusLabel,
  toggleStatusInArray,
} from './order-management.helpers';
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
  private filterFormVersion = signal(0);
  activeFilterChips = computed(() => {
    this.filterFormVersion();
    return buildFilterChips(
      this.filterForm.value,
      this.orderStatusMultiOptions,
      this.paymentStatusMultiOptions,
      this.translate
    );
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
        this.filterFormVersion.update((v) => v + 1);
      });

    this.filterForm.valueChanges
      .pipe(debounceTime(500), distinctUntilChanged(), takeUntil(this.destroy$))
      .subscribe(() => {
        this.applyFilters();
      });

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
      { onViewDetails: this.viewOrderDetails.bind(this) },
      this.translate,
      this.orderStatusTemplate(),
      this.paymentStatusTemplate()
    );
    this.orderColumns = tableDef.columns;
    this.orderActions = tableDef.actions;
  }

  private rebuildFilterOptions(): void {
    this.orderStatusMultiOptions = buildOrderStatusOptions(this.translate);
    this.paymentStatusMultiOptions = buildPaymentStatusOptions(this.translate);
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
    return getOrderStatusLabel(order, this.translate);
  }

  getPaymentStatusLabel(order: OrderListItem): string {
    return getPaymentStatusLabel(order, this.translate);
  }

  applyFilters(): void {
    this.facade.applyFilter(buildFilterPayload(this.filterForm.value));
  }

  resetFilters(): void {
    this.filterForm.reset(FILTER_FORM_DEFAULTS);
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
    this.facade.onSortChange([
      new SortDefinition({ field: event.field, direction: sortDirection }),
    ]);
  }

  onPageChange(event: PaginationState): void {
    this.facade.onPageChange(event.first, event.rows);
  }

  openFilterDrawer(): void {
    this.isFilterDrawerOpen.set(true);
  }

  closeFilterDrawer(): void {
    this.isFilterDrawerOpen.set(false);
  }

  removeFilterChip(key: string): void {
    this.filterForm.patchValue(getFilterPatchForChipRemoval(key));
    this.applyFilters();
  }

  clearAllFilters(): void {
    this.resetFilters();
  }

  isOrderStatusChecked(status: OrderStatus): boolean {
    return this.filterForm.value.orderStatus?.includes(status) ?? false;
  }

  toggleOrderStatus(status: OrderStatus): void {
    this.onOrderStatusChange(status, !this.isOrderStatusChecked(status));
  }

  onOrderStatusChange(status: OrderStatus, checked: boolean): void {
    this.filterForm.patchValue({
      orderStatus: toggleStatusInArray(
        this.filterForm.value.orderStatus || [],
        status,
        checked
      ),
    });
  }

  isPaymentStatusChecked(status: PaymentStatus): boolean {
    return this.filterForm.value.paymentStatus?.includes(status) ?? false;
  }

  togglePaymentStatus(status: PaymentStatus): void {
    this.onPaymentStatusChange(status, !this.isPaymentStatusChecked(status));
  }

  onPaymentStatusChange(status: PaymentStatus, checked: boolean): void {
    this.filterForm.patchValue({
      paymentStatus: toggleStatusInArray(
        this.filterForm.value.paymentStatus || [],
        status,
        checked
      ),
    });
  }
}
