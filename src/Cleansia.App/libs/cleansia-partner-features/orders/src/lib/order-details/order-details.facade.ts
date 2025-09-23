import { Injectable, inject, signal } from '@angular/core';
import { Client, OrderItem, SnackbarService } from '@cleansia/services';
import { catchError, finalize, of, tap } from 'rxjs';

@Injectable()
export class OrderDetailsFacade {
  private readonly client = inject(Client);
  private readonly snackbarService = inject(SnackbarService);

  // Signals for reactive state management
  readonly orderDetails = signal<OrderItem | null>(null);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);

  loadOrderDetails(orderId: string): void {
    if (!orderId?.trim()) {
      this.error.set('Invalid order ID');
      return;
    }

    this.loading.set(true);
    this.error.set(null);

    this.client.orderClient
      .getById(orderId)
      .pipe(
        tap((orderDetails) => {
          if (orderDetails) {
            this.orderDetails.set(orderDetails);
          } else {
            this.error.set('Order not found');
          }
        }),
        catchError((error) => {
          console.error('Error loading order details:', error);
          const errorMessage =
            error?.status === 404
              ? 'Order not found'
              : 'Failed to load order details';
          this.error.set(errorMessage);
          this.snackbarService.showErrorTranslated(
            'global.messages.orders.failed_to_load_details'
          );
          return of(null);
        }),
        finalize(() => this.loading.set(false))
      )
      .subscribe();
  }

  downloadInvoice(): void {
    const order = this.orderDetails();
    if (!order) {
      this.snackbarService.showErrorTranslated(
        'global.messages.orders.no_order_selected'
      );
      return;
    }

    // TODO: Implement actual invoice download
    console.log('Download invoice for order:', order.id);
    this.snackbarService.showSuccessTranslated(
      'global.messages.orders.invoice_download_not_available'
    );
  }

  printOrder(): void {
    try {
      window.print();
    } catch (error) {
      console.error('Print failed:', error);
      this.snackbarService.showErrorTranslated(
        'global.messages.orders.print_failed'
      );
    }
  }

  reset(): void {
    this.orderDetails.set(null);
    this.loading.set(false);
    this.error.set(null);
  }
}
