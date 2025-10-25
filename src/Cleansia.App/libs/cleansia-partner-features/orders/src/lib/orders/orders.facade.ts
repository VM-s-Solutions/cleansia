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
import { Store } from '@ngrx/store';
import { catchError, of, takeUntil } from 'rxjs';

@Injectable()
export class OrdersFacade extends UnsubscribeControlDirective {
  private readonly store = inject(Store);
  private readonly client = inject(Client);
  private readonly snackbarService = inject(SnackbarService);

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
}
