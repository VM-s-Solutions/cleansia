import { CommonModule } from '@angular/common';
import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { Router } from '@angular/router';
import { CleansiaButtonComponent, CleansiaTitleComponent } from '@cleansia/components';
import {
  loadCustomerOrders,
  selectCustomerOrders,
  selectCustomerOrdersTotal,
  selectCustomerOrderLoading,
} from '@cleansia/customer-stores';
import { OrderListItem, OrderStatus, PaymentStatus } from '@cleansia/partner-services';
import { CleansiaCustomerRoute } from '@cleansia/services';
import { Store } from '@ngrx/store';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { TableLazyLoadEvent, TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { SkeletonModule } from 'primeng/skeleton';
import { PaginatorModule, PaginatorState } from 'primeng/paginator';

@Component({
  selector: 'cleansia-customer-orders',
  standalone: true,
  imports: [
    CommonModule,
    TranslateModule,
    TableModule,
    TagModule,
    SkeletonModule,
    CleansiaButtonComponent,
    CleansiaTitleComponent,
    PaginatorModule
  ],
  templateUrl: './orders.component.html',
  styleUrls: ['./orders.component.scss']
})
export class OrdersComponent implements OnInit {
  private readonly store = inject(Store);
  readonly router = inject(Router);
  private readonly translate = inject(TranslateService);

  orders = toSignal(this.store.select(selectCustomerOrders), { initialValue: [] });
  upcomingOrders = computed(() => {
    const rawOrders = this.orders();
    if (!rawOrders) return [];
    const now = new Date();
    return rawOrders.filter(o => new Date(o.cleaningDateTime) >= now);
  });
  pastOrders = computed(() => {
    const rawOrders = this.orders();
    if (!rawOrders) return [];
    const now = new Date();
    return rawOrders.filter(o => new Date(o.cleaningDateTime) < now);
  });
  
  totalRecords = toSignal(this.store.select(selectCustomerOrdersTotal), { initialValue: 0 });
  loading = toSignal(this.store.select(selectCustomerOrderLoading('paged')), { initialValue: false });

  rows = 10;
  first = 0;

  ngOnInit(): void {
    this.loadOrders();
  }

  loadOrders(): void {
    this.store.dispatch(
      loadCustomerOrders({
        offset: this.first,
        limit: this.rows,
      })
    );
  }

  onPageChange(event: TableLazyLoadEvent | PaginatorState): void {
    this.first = event.first ?? 0;
    this.rows = event.rows ?? 10;
    this.loadOrders();
  }

  viewOrder(order: OrderListItem): void {
    this.router.navigate([CleansiaCustomerRoute.ORDERS, order.id]);
  }

  getOrderStatusSeverity(status: { value?: number }): string {
    switch (status?.value) {
      case OrderStatus.Pending: return 'warn';
      case OrderStatus.Confirmed: return 'info';
      case OrderStatus.InProgress: return 'info';
      case OrderStatus.Completed: return 'success';
      case OrderStatus.Cancelled: return 'danger';
      default: return 'info';
    }
  }

  getPaymentStatusSeverity(status: { value?: number }): string {
    switch (status?.value) {
      case PaymentStatus.Pending: return 'warn';
      case PaymentStatus.Paid: return 'success';
      case PaymentStatus.Failed: return 'danger';
      case PaymentStatus.Refunded: return 'info';
      case PaymentStatus.Disputed: return 'danger';
      default: return 'info';
    }
  }

  formatDate(date: Date): string {
    return new Date(date).toLocaleDateString('cs-CZ', {
      day: '2-digit',
      month: '2-digit',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
    });
  }

  formatPrice(price: number, currency?: { code?: string }): string {
    return new Intl.NumberFormat('cs-CZ', {
      style: 'currency',
      currency: currency?.code || 'CZK',
      minimumFractionDigits: 0,
    }).format(price);
  }
}
