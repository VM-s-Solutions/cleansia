import { Injectable, inject, signal } from '@angular/core';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import { OrderFilter } from '@cleansia/models';
import {
  OrderListItem,
  OrderStatus,
  PartnerClient,
  SortDefinition,
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

  readonly orders$ = this.store.select(selectOrderItems);
  readonly loading$ = this.store.select(selectOrderLoading('paged'));
  readonly total$ = this.store.select(selectOrderTotal);

  availableOrders = signal<OrderListItem[]>([]);
  myOrders = signal<OrderListItem[]>([]);
  totalRecords = signal<number>(0);

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
        OrderStatus.Pending,
        OrderStatus.Confirmed,
      ],
      hasAvailableSpots: true,
      excludeEmployeeId: employeeId || undefined,
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

    this.store.dispatch(
      OrderActions.loadOrderPaged({
        filter,
        sort: this.currentSort(),
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
    const tab = this.activeTab();
    // Reset to first page when filters change
    if (tab === 'available') {
      this.loadAvailableOrders(0, 10);
    } else {
      this.loadMyOrders(0, 10);
    }
  }

  resetFilters(): void {
    this.currentFilter.set(null);
    const tab = this.activeTab();
    // Reset to first page when filters are cleared
    if (tab === 'available') {
      this.loadAvailableOrders(0, 10);
    } else {
      this.loadMyOrders(0, 10);
    }
  }
}
