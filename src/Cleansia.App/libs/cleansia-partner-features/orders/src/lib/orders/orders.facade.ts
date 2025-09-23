import { Injectable, inject, signal, computed, OnDestroy } from '@angular/core';
import { SnackbarService, OrderListItem, SortDefinition } from '@cleansia/services';
import { OrderFilter, Page } from '@cleansia/models';
import { Store } from '@ngrx/store';
import { Observable, Subject, takeUntil } from 'rxjs';
import * as OrderActions from '@cleansia/stores';
import {
  selectOrderItems,
  selectOrderPage,
  selectOrderLoading,
  selectOrderError
} from '@cleansia/stores';


@Injectable()
export class OrdersFacade implements OnDestroy {
  private readonly snackbarService = inject(SnackbarService);
  private readonly store = inject(Store);
  private readonly destroy$ = new Subject<void>();

  // NgRx selectors
  readonly orders$ = this.store.select(selectOrderItems);
  readonly orderPage$ = this.store.select(selectOrderPage);
  readonly loading$ = this.store.select(selectOrderLoading('paged'));
  readonly error$ = this.store.select(selectOrderError('paged'));

  // Signals for reactive data
  orders = signal<OrderListItem[]>([]);
  currentFilter = signal<OrderFilter>(new OrderFilter({}));
  currentSort = signal<SortDefinition[]>([]);

  constructor() {
    // Subscribe to orders data and update signal
    this.orders$.pipe(takeUntil(this.destroy$)).subscribe(orders => {
      // Create a mutable copy to prevent sorting errors with read-only arrays
      this.orders.set([...(orders || [])]);
    });

    // Load initial data
    this.loadOrders();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }


  // Load orders with current filter and sort
  loadOrders(offset = 0, limit = 20): void {
    this.store.dispatch(OrderActions.loadOrderPaged({
      filter: this.currentFilter(),
      sort: this.currentSort(),
      offset,
      limit
    }));
  }

  // Update filter and reload data
  updateFilter(filter: Partial<OrderFilter>): void {
    const currentFilter = this.currentFilter();
    const newFilter = new OrderFilter({ ...currentFilter, ...filter });
    this.currentFilter.set(newFilter);
    this.loadOrders();
  }

  // Update sort and reload data
  updateSort(sort: SortDefinition[]): void {
    this.currentSort.set(sort);
    this.loadOrders();
  }

}
