import { Injectable, inject, signal } from '@angular/core';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import { OrderFilter } from '@cleansia/models';
import {
  Client,
  OrderListItem,
  OrderStatus,
  SnackbarService,
  SortDefinition,
  TakeOrderCommand,
} from '@cleansia/services';
import * as OrderActions from '@cleansia/stores';
import { selectOrderItems, selectOrderLoading } from '@cleansia/stores';
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
  private readonly client = inject(Client);
  private readonly snackbarService = inject(SnackbarService);
  private readonly dialogService = inject(DialogService);
  private readonly actions$ = inject(Actions);

  readonly orders$ = this.store.select(selectOrderItems);
  readonly loading$ = this.store.select(selectOrderLoading('paged'));

  availableOrders = signal<OrderListItem[]>([]);
  myOrders = signal<OrderListItem[]>([]);

  private currentEmployeeId = signal<string | null>(null);
  private currentSort = signal<SortDefinition[]>([]);
  private activeTab = signal<'available' | 'my'>('available');

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
    this.client.employeeClient
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

    const filter = new OrderFilter({
      employeeId: undefined,
      orderStatuses: [OrderStatus.Pending, OrderStatus.Confirmed],
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

    const filter = new OrderFilter({
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

    this.client.orderClient
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
    if (tab === 'available') {
      this.loadAvailableOrders();
      return;
    }
    this.loadMyOrders();
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

    ref.onClose.pipe(takeUntil(this.destroyed$)).subscribe((result: CompleteOrderDialogResult) => {
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
}
