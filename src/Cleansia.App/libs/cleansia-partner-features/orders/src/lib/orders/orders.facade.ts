import { Injectable, inject, signal } from '@angular/core';
import { AbstractControl } from '@angular/forms';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import { OrderFilter } from '@cleansia/models';
import {
  OrderListItem,
  OrderStatus,
  PartnerClient,
  SortDefinition,
  SortDirection,
  StartOrderCommand,
  TakeOrderCommand,
} from '@cleansia/partner-services';
import * as OrderActions from '@cleansia/partner-stores';
import {
  selectOrderItems,
  selectOrderLoading,
  selectOrderTotal,
} from '@cleansia/partner-stores';
import { SnackbarService } from '@cleansia/services';
import { Actions, ofType } from '@ngrx/effects';
import { Store } from '@ngrx/store';
import { TranslateService } from '@ngx-translate/core';
import { DialogService, DynamicDialogRef } from 'primeng/dynamicdialog';
import { catchError, debounceTime, distinctUntilChanged, of, takeUntil } from 'rxjs';
import {
  CompleteOrderDialogComponent,
  CompleteOrderDialogData,
  CompleteOrderDialogResult,
} from '../components/complete-order-dialog';

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

  // Aggregate signals kept for any consumer that asks "is anything loading"
  // or "what's the combined total" without caring which list. Derived from
  // both per-list streams so they stay accurate as either side changes.
  totalRecords = signal<number>(0);
  loading = signal<boolean>(false);

  private currentEmployeeId = signal<string | null>(null);
  private currentSort = signal<SortDefinition[]>([]);
  private activeTab = signal<'available' | 'my'>('available');
  private currentFilter = signal<OrderFilter | null>(null);

  constructor() {
    super();

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
      orderStatuses: additionalFilters?.orderStatuses || [
        OrderStatus.New,
        OrderStatus.Pending,
        OrderStatus.Confirmed,
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

  takeOrder(orderId: string): void {
    const employeeId = this.currentEmployeeId();

    if (!employeeId) {
      this.snackbarService.showErrorTranslated(
        'pages.orders.employee_not_found'
      );
      return;
    }

    this.partnerClient.orderClient
      .takeOrder(new TakeOrderCommand({ orderId }))
      .subscribe((response) => {
        if (response) {
          this.snackbarService.showSuccessTranslated(
            'pages.orders.order_taken_success'
          );
          this.loadAvailableOrders();
          this.loadMyOrders();
        }
      });
  }

  startOrder(orderId: string): void {
    this.partnerClient.orderClient
      .startOrder(new StartOrderCommand({ orderId }))
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

  updateSort(sort: SortDefinition[]): void {
    this.currentSort.set(sort);
    const tab = this.activeTab();
    // Reset to first page when sorting changes
    if (tab === 'available') {
      this.loadAvailableOrders(0, 10);
      return;
    }
    this.loadMyOrders(0, 10);
  }

  openCompleteOrderDialog(order: OrderListItem): void {
    const employeeId = this.currentEmployeeId();

    if (!employeeId) {
      this.snackbarService.showErrorTranslated(
        'pages.orders.employee_not_found'
      );
      return;
    }

    const dialogData: CompleteOrderDialogData = {
      orderId: order.id!,
      orderNumber: order.displayOrderNumber!,
      estimatedTime: order.estimatedTime || 0,
    };

    const ref: DynamicDialogRef = this.dialogService.open(
      CompleteOrderDialogComponent,
      {
        header: undefined,
        data: dialogData,
        width: '600px',
        modal: true,
        dismissableMask: false,
      }
    );

    ref.onClose
      .pipe(takeUntil(this.destroyed$))
      .subscribe((result: CompleteOrderDialogResult) => {
        if (result) {
          this.store.dispatch(
            OrderActions.completeOrder({
              orderId: order.id!,
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
    this.loadAvailableOrders(0, 10);
    setTimeout(() => this.loadMyOrders(0, 10), 100);
  }

  resetFilters(): void {
    this.currentFilter.set(null);
    // Load both sections when filters are cleared
    this.loadAvailableOrders(0, 10);
    setTimeout(() => this.loadMyOrders(0, 10), 100);
  }

  /**
   * Wire form valueChanges and language change subscriptions.
   * Component lifecycle invokes this once; cleanup is handled by the
   * facade's destroyed$ Subject (UnsubscribeControlDirective).
   */
  bindFormChanges(
    formCtrl: AbstractControl,
    onFormChangeImmediate: () => void,
    onFormChangeDebounced: () => void,
    onLangChange: () => void
  ): void {
    formCtrl.valueChanges
      .pipe(takeUntil(this.destroyed$))
      .subscribe(() => onFormChangeImmediate());

    formCtrl.valueChanges
      .pipe(debounceTime(500), distinctUntilChanged(), takeUntil(this.destroyed$))
      .subscribe(() => onFormChangeDebounced());

    this.translate.onLangChange
      .pipe(takeUntil(this.destroyed$))
      .subscribe(() => onLangChange());
  }
}
