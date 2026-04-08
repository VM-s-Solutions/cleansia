import { Injectable, inject, signal } from '@angular/core';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import { OrderFilter } from '@cleansia/models';
import {
  OrderListItem,
  OrderStatus,
  PartnerClient,
  SortDefinition,
  SortDirection,
  TakeOrderCommand,
} from '@cleansia/partner-services';
import * as OrderActions from '@cleansia/partner-stores';
import { selectOrderItems, selectOrderLoading, selectOrderTotal } from '@cleansia/partner-stores';
import { SnackbarService } from '@cleansia/services';
import { Actions, ofType } from '@ngrx/effects';
import { Store } from '@ngrx/store';
import { DialogService, DynamicDialogRef } from 'primeng/dynamicdialog';
import { catchError, of, takeUntil } from 'rxjs';
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

  private readonly orders$ = this.store.select(selectOrderItems);
  private readonly loading$ = this.store.select(selectOrderLoading('paged'));
  private readonly total$ = this.store.select(selectOrderTotal);

  availableOrders = signal<OrderListItem[]>([]);
  myOrders = signal<OrderListItem[]>([]);
  totalRecords = signal<number>(0);
  availableTotalRecords = signal<number>(0);
  myTotalRecords = signal<number>(0);
  loading = signal<boolean>(false);
  availableLoading = signal<boolean>(false);
  myLoading = signal<boolean>(false);

  private currentEmployeeId = signal<string | null>(null);
  private currentSort = signal<SortDefinition[]>([]);
  private activeTab = signal<'available' | 'my'>('available');
  private currentFilter = signal<OrderFilter | null>(null);

  constructor() {
    super();
    this.orders$.pipe(takeUntil(this.destroyed$)).subscribe((orders) => {
      const tab = this.activeTab();
      if (tab === 'available') {
        this.availableOrders.set([...(orders || [])]);
      } else {
        this.myOrders.set([...(orders || [])]);
      }
    });

    this.total$.pipe(takeUntil(this.destroyed$)).subscribe((total) => {
      this.totalRecords.set(total);
      const tab = this.activeTab();
      if (tab === 'available') {
        this.availableTotalRecords.set(total);
      } else {
        this.myTotalRecords.set(total);
      }
    });

    this.loading$.pipe(takeUntil(this.destroyed$)).subscribe((loading) => {
      this.loading.set(loading);
      const tab = this.activeTab();
      if (tab === 'available') {
        this.availableLoading.set(loading);
      } else {
        this.myLoading.set(loading);
      }
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
      ],
      hasAvailableSpots: true,
      excludeEmployeeId: employeeId || undefined,
      cleaningDateFrom: additionalFilters?.cleaningDateFrom ?? new Date(),
    });

    this.store.dispatch(
      OrderActions.loadOrderPaged({
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
      .takeOrder(new TakeOrderCommand({ orderId, employeeId }))
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
              employeeId,
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
}
