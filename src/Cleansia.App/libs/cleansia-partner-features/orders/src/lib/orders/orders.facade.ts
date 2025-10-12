import { Injectable, inject, signal } from '@angular/core';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import { OrderFilter } from '@cleansia/models';
import {
  Client,
  OrderListItem,
  OrderStatus,
  SnackbarService,
  SortDefinition,
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

    this.loadAvailableOrders();
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
        }
      });
  }

  loadAvailableOrders(offset = 0, limit = 20): void {
    const filter = new OrderFilter({
      employeeId: undefined,
      orderStatuses: [OrderStatus.Pending, OrderStatus.Confirmed],
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
      orderStatuses: [3], // Completed = 3
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

    // TODO: Call backend API to assign employee to order
    // For now, show success message and reload
    this.snackbarService.showSuccessTranslated(
      'pages.orders.order_taken_success'
    );

    // Reload available orders after taking one
    this.loadAvailableOrders();
  }

  // Set active tab
  setActiveTab(tab: 'available' | 'my'): void {
    this.activeTab.set(tab);
  }

  // Update sort and reload data
  updateSort(sort: SortDefinition[]): void {
    this.currentSort.set(sort);
    // Reload data for the current active tab
    const tab = this.activeTab();
    if (tab === 'available') {
      this.loadAvailableOrders();
    } else {
      this.loadMyOrders();
    }
  }
}
