import { computed, Injectable, inject, signal } from '@angular/core';
import { FormBuilder, Validators } from '@angular/forms';
import { FilterChip, FilterDrawerState, PaginationState, SortEvent } from '@cleansia/components';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import { OrderFilter } from '@cleansia/models';
import {
  OrderListItem,
  OrderStatus,
  PartnerClient,
  SortDefinition,
  SortDirection,
  StartOrderCommand,
} from '@cleansia/partner-services';
import * as OrderActions from '@cleansia/partner-stores';
import {
  selectOrderItems,
  selectOrderLoading,
  selectOrderTotal,
} from '@cleansia/partner-stores';
import { SnackbarService } from '@cleansia/services';
import { currentLanguage } from '@cleansia/utils';
import { Actions, ofType } from '@ngrx/effects';
import { Store } from '@ngrx/store';
import { TranslateService } from '@ngx-translate/core';
import { DialogService, DynamicDialogRef } from 'primeng/dynamicdialog';
import { catchError, of, takeUntil } from 'rxjs';
import {
  CompleteOrderDialogComponent,
  CompleteOrderDialogData,
  CompleteOrderDialogResult,
} from '../components/complete-order-dialog';
import {
  WorkContractDialogComponent,
  WorkContractDialogData,
  WorkContractDialogMode,
  WorkContractDialogOutcome,
  WorkContractDialogResult,
} from '../components/work-contract-dialog';
import {
  buildActiveFilterChips,
  buildOrderFilter,
  buildOrderStatusOptions,
  buildPaymentStatusOptions,
} from './orders.helpers';

@Injectable()
export class OrdersFacade extends UnsubscribeControlDirective {
  private readonly store = inject(Store);
  private readonly partnerClient = inject(PartnerClient);
  private readonly snackbarService = inject(SnackbarService);
  private readonly dialogService = inject(DialogService);
  private readonly actions$ = inject(Actions);
  private readonly translate = inject(TranslateService);

  // Per-list store streams. The page renders Available + My side-by-side
  // so they MUST come from independent slices — a single shared `paged`
  // slice was being clobbered by whichever load fired second, putting
  // mine into available and hiding express orders.
  private readonly availableOrders$ = this.store.select(selectOrderItems('available'));
  private readonly availableTotal$ = this.store.select(selectOrderTotal('available'));
  private readonly availableLoading$ = this.store.select(selectOrderLoading('paged:available'));
  private readonly myOrders$ = this.store.select(selectOrderItems('my'));
  private readonly myTotal$ = this.store.select(selectOrderTotal('my'));
  private readonly myLoading$ = this.store.select(selectOrderLoading('paged:my'));

  availableOrders = signal<OrderListItem[]>([]);
  myOrders = signal<OrderListItem[]>([]);
  availableTotalRecords = signal<number>(0);
  myTotalRecords = signal<number>(0);
  availableLoading = signal<boolean>(false);
  myLoading = signal<boolean>(false);
  takeInFlightOrderId = signal<string | null>(null);

  // Aggregate signals kept for any consumer that asks "is anything loading"
  // or "what's the combined total" without caring which list. Derived from
  // both per-list streams so they stay accurate as either side changes.
  totalRecords = signal<number>(0);
  loading = signal<boolean>(false);

  private currentEmployeeId = signal<string | null>(null);
  private currentSort = signal<SortDefinition[]>([]);
  private activeTab = signal<'available' | 'my'>('available');
  private currentFilter = signal<OrderFilter | null>(null);

  readonly lang = currentLanguage(this.translate);
  readonly orderStatusOptions = computed(() => {
    this.lang();
    return buildOrderStatusOptions(this.translate);
  });
  readonly paymentStatusOptions = computed(() => {
    this.lang();
    return buildPaymentStatusOptions(this.translate);
  });
  // One boolean control per status feeds the arrays the query sends, so a new enum value needs
  // no matching FormControl declared by hand.
  readonly filterForm = inject(FormBuilder).group({
    customerName: [''],
    customerEmail: ['', [Validators.email]],
    displayOrderNumber: [''],
    orderStatuses: [[] as number[]],
    paymentStatuses: [[] as number[]],
    cleaningDateFrom: [null as Date | null],
    cleaningDateTo: [null as Date | null],
    ...Object.fromEntries(
      buildOrderStatusOptions(this.translate).map((o) => [`orderStatus_${o.value}`, [false]])
    ),
    ...Object.fromEntries(
      buildPaymentStatusOptions(this.translate).map((o) => [`paymentStatus_${o.value}`, [false]])
    ),
  });
  readonly filters = new FilterDrawerState({
    form: this.filterForm,
    lang: this.lang,
    chips: (value): FilterChip[] =>
      buildActiveFilterChips(value, this.orderStatusOptions(), this.paymentStatusOptions(), this.translate),
    apply: (value) => this.applyFilters(buildOrderFilter(value)),
  });

  constructor() {
    super();
    this.filters.connect(this.destroyed$);

    this.availableOrders$.pipe(takeUntil(this.destroyed$)).subscribe((orders) =>
      this.availableOrders.set([...(orders || [])]),
    );
    this.availableTotal$.pipe(takeUntil(this.destroyed$)).subscribe((total) => {
      this.availableTotalRecords.set(total);
      this.totalRecords.set(total + this.myTotalRecords());
    });
    this.availableLoading$.pipe(takeUntil(this.destroyed$)).subscribe((loading) => {
      this.availableLoading.set(loading);
      this.loading.set(loading || this.myLoading());
    });

    this.myOrders$.pipe(takeUntil(this.destroyed$)).subscribe((orders) =>
      this.myOrders.set([...(orders || [])]),
    );
    this.myTotal$.pipe(takeUntil(this.destroyed$)).subscribe((total) => {
      this.myTotalRecords.set(total);
      this.totalRecords.set(total + this.availableTotalRecords());
    });
    this.myLoading$.pipe(takeUntil(this.destroyed$)).subscribe((loading) => {
      this.myLoading.set(loading);
      this.loading.set(loading || this.availableLoading());
    });

    this.loadCurrentEmployee();
    this.subscribeToCompleteOrderSuccess();
  }

  private subscribeToCompleteOrderSuccess(): void {
    this.actions$
      .pipe(
        ofType(OrderActions.completeOrderSuccess),
        takeUntil(this.destroyed$)
      )
      .subscribe(() => {
        // Reload the current tab's orders after completing an order
        const tab = this.activeTab();
        if (tab === 'available') {
          this.loadAvailableOrders();
        } else {
          this.loadMyOrders();
        }
      });
  }

  private loadCurrentEmployee(): void {
    this.partnerClient.employeeClient
      .getCurrentEmployee()
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of(null))
      )
      .subscribe((employee) => {
        if (employee?.id) {
          this.currentEmployeeId.set(employee.id);
          this.loadAvailableOrders();
          // Also load my orders so both sections have data
          setTimeout(() => this.loadMyOrders(), 100);
        }
      });
  }

  loadAvailableOrders(offset = 0, limit = 20): void {
    const employeeId = this.currentEmployeeId();
    const additionalFilters = this.currentFilter();

    const filter = new OrderFilter({
      ...additionalFilters,
      employeeId: undefined,
      // Mirrors OrderAvailability.OfferableStatuses. Started is not over: a half-crewed job stays
      // fillable after the first cleaner taps "on my way" (owner ruling 2026-09-06), and the
      // hasAvailableSpots term below is what keeps a FULL job off the list.
      orderStatuses: additionalFilters?.orderStatuses || [
        OrderStatus.New,
        OrderStatus.Confirmed,
        OrderStatus.OnTheWay,
        OrderStatus.InProgress,
      ],
      hasAvailableSpots: true,
      excludeEmployeeId: employeeId || undefined,
      cleaningDateFrom: additionalFilters?.cleaningDateFrom ?? new Date(),
    });

    this.store.dispatch(
      OrderActions.loadOrderPaged({
        listKey: 'available',
        filter,
        sort: this.currentSort(),
        offset,
        limit,
      })
    );
  }

  loadMyOrders(offset = 0, limit = 20): void {
    const employeeId = this.currentEmployeeId();

    if (!employeeId) {
      this.snackbarService.showErrorTranslated(
        'pages.orders.employee_not_found'
      );
      return;
    }

    const additionalFilters = this.currentFilter();

    const filter = new OrderFilter({
      ...additionalFilters,
      employeeId: employeeId,
    });

    const currentSort = this.currentSort();
    const sort: SortDefinition[] =
      currentSort.length > 0
        ? currentSort
        : [new SortDefinition({ field: 'cleaningDateTime', direction: SortDirection.Descending })];

    this.store.dispatch(
      OrderActions.loadOrderPaged({
        listKey: 'my',
        filter,
        sort,
        offset,
        limit,
      })
    );
  }

  isTakeInFlight(orderId: string | undefined): boolean {
    return !!orderId && this.takeInFlightOrderId() === orderId;
  }

  // Every take passes through the contract dialog: the preview is read there, the tick is
  // given there, and the take carries the text id the cleaner was shown. The row stays
  // in flight while the dialog is open so the board offers no second entry.
  takeOrder(orderId: string): void {
    const employeeId = this.currentEmployeeId();

    if (!employeeId) {
      this.snackbarService.showErrorTranslated(
        'pages.orders.employee_not_found'
      );
      return;
    }

    if (this.takeInFlightOrderId()) {
      return;
    }

    this.takeInFlightOrderId.set(orderId);

    const dialogData: WorkContractDialogData = {
      mode: WorkContractDialogMode.Take,
      orderId,
    };

    const ref: DynamicDialogRef | null = this.dialogService.open(
      WorkContractDialogComponent,
      {
        data: dialogData,
        modal: true,
        dismissableMask: false,
        showHeader: false,
        styleClass: 'cleansia-dialog dialog-panel dialog-panel--reading',
      }
    );

    ref?.onClose
      .pipe(takeUntil(this.destroyed$))
      .subscribe((result: WorkContractDialogResult | undefined) => {
        this.takeInFlightOrderId.set(null);
        if (!result) {
          return;
        }
        if (result.outcome === WorkContractDialogOutcome.Accepted) {
          this.snackbarService.showSuccessTranslated(
            'pages.orders.order_taken_success'
          );
        }
        // Reconcile on refusal exactly as on success: a refused take usually
        // means another cleaner filled the last seat, so the row must stop
        // offering an action the server has already turned down.
        this.loadAvailableOrders();
        this.loadMyOrders();
      });
  }

  startOrder(orderId: string): void {
    const command = new StartOrderCommand();
    command.orderId = orderId;

    this.partnerClient.orderClient
      .startOrder(command)
      .pipe(takeUntil(this.destroyed$))
      .subscribe((response) => {
        if (response) {
          this.snackbarService.showSuccessTranslated(
            'global.messages.orders.order_started'
          );
          this.loadMyOrders();
        }
      });
  }

  setActiveTab(tab: 'available' | 'my'): void {
    this.activeTab.set(tab);
  }

  onAvailableOrdersPageChange(event: PaginationState): void {
    this.loadAvailableOrders(event.first, event.rows);
  }

  onMyOrdersPageChange(event: PaginationState): void {
    this.loadMyOrders(event.first, event.rows);
  }

  onAvailableSortChange(event: SortEvent): void {
    this.activeTab.set('available');
    this.updateSort(event);
  }

  onMySortChange(event: SortEvent): void {
    this.activeTab.set('my');
    this.updateSort(event);
  }

  private updateSort(event: SortEvent): void {
    this.currentSort.set([
      new SortDefinition({
        field: event.field,
        direction: event.order === 1 ? SortDirection.Ascending : SortDirection.Descending,
      }),
    ]);
    // Reset to first page when sorting changes
    if (this.activeTab() === 'available') {
      this.loadAvailableOrders();
      return;
    }
    this.loadMyOrders();
  }

  setOrderStatus(status: number, checked: boolean): void {
    const current = this.filterForm.controls.orderStatuses.value || [];
    this.filterForm.patchValue({
      orderStatuses: checked ? [...new Set([...current, status])] : current.filter((s) => s !== status),
    });
  }

  setPaymentStatus(status: number, checked: boolean): void {
    const current = this.filterForm.controls.paymentStatuses.value || [];
    this.filterForm.patchValue({
      paymentStatuses: checked ? [...new Set([...current, status])] : current.filter((s) => s !== status),
    });
  }

  openCompleteOrderDialog(order: OrderListItem): void {
    const employeeId = this.currentEmployeeId();
    const orderId = order.id;

    if (!employeeId || !orderId) {
      this.snackbarService.showErrorTranslated(
        'pages.orders.employee_not_found'
      );
      return;
    }

    const dialogData: CompleteOrderDialogData = {
      orderId,
      orderNumber: order.displayOrderNumber ?? '',
      estimatedTime: order.estimatedTime || 0,
    };

    const ref: DynamicDialogRef | null = this.dialogService.open(
      CompleteOrderDialogComponent,
      {
        data: dialogData,
        header: this.translate.instant('pages.orders.complete_order.title'),
        modal: true,
        closable: true,
        draggable: false,
        resizable: false,
        dismissableMask: false,
        styleClass: 'cleansia-dialog dialog-panel dialog-panel--wide',
      }
    );

    ref?.onClose
      .pipe(takeUntil(this.destroyed$))
      .subscribe((result: CompleteOrderDialogResult) => {
        if (result) {
          this.store.dispatch(
            OrderActions.completeOrder({
              orderId,
              actualCompletionTimeMinutes: result.actualCompletionTimeMinutes,
              completionNotes: result.completionNotes,
            })
          );
        }
      });
  }

  canCompleteOrder(order: OrderListItem): boolean {
    return order.orderStatus?.value === OrderStatus.InProgress;
  }

  applyFilters(filter: OrderFilter): void {
    this.currentFilter.set(filter);
    // Load both sections when filters change
    this.loadAvailableOrders();
    setTimeout(() => this.loadMyOrders(), 100);
  }
}
