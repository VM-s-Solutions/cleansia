import { Injectable, inject, signal } from '@angular/core';
import {
  AdminClient,
  Code,
  OrderItem,
  OrderStatus,
  PaymentStatus,
} from '@cleansia/admin-services';
import { SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { Subject, catchError, finalize, of, takeUntil } from 'rxjs';

@Injectable()
export class OrderDetailFacade {
  private readonly adminClient = inject(AdminClient);
  private readonly snackbarService = inject(SnackbarService);
  private readonly translate = inject(TranslateService);

  private destroy$ = new Subject<void>();

  readonly order = signal<OrderItem | null>(null);
  readonly loading = signal<boolean>(false);

  loadOrderDetail(orderId: string): void {
    this.loading.set(true);

    this.adminClient.adminOrderClient
      .details(orderId)
      .pipe(
        takeUntil(this.destroy$),
        catchError(() => of(null)),
        finalize(() => this.loading.set(false))
      )
      .subscribe((response) => {
        if (response) {
          this.order.set(response);
        }
      });
  }

  formatDate(date: string | Date | null | undefined): string {
    if (!date) return '-';
    const dateObj = date instanceof Date ? date : new Date(date);
    return dateObj.toLocaleDateString('cs-CZ');
  }

  formatDateTime(date: string | Date | null | undefined): string {
    if (!date) return '-';
    const dateObj = date instanceof Date ? date : new Date(date);
    return dateObj.toLocaleString('cs-CZ');
  }

  formatTime(date: string | Date | null | undefined): string {
    if (!date) return '-';
    const dateObj = date instanceof Date ? date : new Date(date);
    return dateObj.toLocaleTimeString('cs-CZ', {
      hour: '2-digit',
      minute: '2-digit',
    });
  }

  formatPrice(price: number | null | undefined): string {
    if (price === null || price === undefined) return '-';
    const currency = this.order()?.currency?.symbol || 'CZK';
    return `${price.toFixed(2)} ${currency}`;
  }

  formatDuration(minutes: number | null | undefined): string {
    if (!minutes) return '-';
    const hours = Math.floor(minutes / 60);
    const mins = minutes % 60;
    if (hours > 0) {
      return mins > 0 ? `${hours}h ${mins}m` : `${hours}h`;
    }
    return `${mins}m`;
  }

  getOrderStatusClass(status: Code | undefined): string {
    if (!status) return 'order-status-badge status-pending';
    switch (status.value) {
      case OrderStatus.Pending:
        return 'order-status-badge status-pending';
      case OrderStatus.Confirmed:
        return 'order-status-badge status-confirmed';
      case OrderStatus.InProgress:
        return 'order-status-badge status-inprogress';
      case OrderStatus.Completed:
        return 'order-status-badge status-completed';
      case OrderStatus.Cancelled:
        return 'order-status-badge status-cancelled';
      default:
        return 'order-status-badge status-pending';
    }
  }

  getPaymentStatusClass(status: Code | undefined): string {
    if (!status) return 'payment-status-badge status-pending';
    switch (status.value) {
      case PaymentStatus.Pending:
        return 'payment-status-badge status-pending';
      case PaymentStatus.Paid:
        return 'payment-status-badge status-paid';
      case PaymentStatus.Failed:
        return 'payment-status-badge status-failed';
      case PaymentStatus.Refunded:
        return 'payment-status-badge status-refunded';
      case PaymentStatus.Disputed:
        return 'payment-status-badge status-disputed';
      default:
        return 'payment-status-badge status-pending';
    }
  }

  getOrderStatusIcon(status: Code | undefined): string {
    if (!status) return 'pi pi-circle';
    switch (status.value) {
      case OrderStatus.Pending:
        return 'pi pi-clock';
      case OrderStatus.Confirmed:
        return 'pi pi-check';
      case OrderStatus.InProgress:
        return 'pi pi-spinner';
      case OrderStatus.Completed:
        return 'pi pi-check-circle';
      case OrderStatus.Cancelled:
        return 'pi pi-times-circle';
      default:
        return 'pi pi-circle';
    }
  }

  getExtrasArray(): { key: string; value: boolean }[] {
    const extras = this.order()?.extras;
    if (!extras) return [];
    return Object.entries(extras).map(([key, value]) => ({ key, value }));
  }

  getActiveExtras(): string[] {
    const extras = this.order()?.extras;
    if (!extras) return [];
    return Object.entries(extras)
      .filter(([, value]) => value)
      .map(([key]) => key);
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }
}
