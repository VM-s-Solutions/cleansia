import { Injectable, inject, signal } from '@angular/core';
import {
  OrderItem,
  PartnerClient,
  StartOrderCommand,
} from '@cleansia/partner-services';
import * as OrderActions from '@cleansia/partner-stores';
import { SnackbarService } from '@cleansia/services';
import { Store } from '@ngrx/store';
import { DialogService, DynamicDialogRef } from 'primeng/dynamicdialog';
import { catchError, finalize, of, tap } from 'rxjs';
import {
  CompleteOrderDialogComponent,
  CompleteOrderDialogData,
  CompleteOrderDialogResult,
} from '../components/complete-order-dialog';

@Injectable()
export class OrderDetailsFacade {
  private readonly partnerClient = inject(PartnerClient);
  private readonly snackbarService = inject(SnackbarService);
  private readonly dialogService = inject(DialogService);
  private readonly store = inject(Store);

  // Signals for reactive state management
  readonly orderDetails = signal<OrderItem | null>(null);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly currentEmployeeId = signal<string | null>(null);

  loadOrderDetails(orderId: string): void {
    if (!orderId?.trim()) {
      this.error.set('Invalid order ID');
      return;
    }

    this.loading.set(true);
    this.error.set(null);

    this.partnerClient.orderClient
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
          const errorMessage =
            error?.status === 404
              ? 'Order not found'
              : 'Failed to load order details';
          this.error.set(errorMessage);
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

    if (!order.receiptNumber) {
      this.snackbarService.showErrorTranslated(
        'global.messages.orders.receipt_not_available'
      );
      return;
    }

    this.loading.set(true);

    this.partnerClient.orderClient
      .downloadReceipt(order.id)
      .pipe(
        tap((response) => {
          // Create a blob URL and trigger download
          const blob = response.data;
          const url = window.URL.createObjectURL(blob);
          const link = document.createElement('a');
          link.href = url;
          link.download =
            response.fileName || `receipt_${order.displayOrderNumber}.pdf`;
          document.body.appendChild(link);
          link.click();
          document.body.removeChild(link);
          window.URL.revokeObjectURL(url);

          this.snackbarService.showSuccessTranslated(
            'global.messages.orders.receipt_downloaded'
          );
        }),
        catchError(() => of(null)),
        finalize(() => this.loading.set(false))
      )
      .subscribe();
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

  loadCurrentEmployee(): void {
    this.partnerClient.employeeClient
      .getCurrentEmployee()
      .pipe(catchError(() => of(null)))
      .subscribe((employee) => {
        if (employee?.id) {
          this.currentEmployeeId.set(employee.id);
        }
      });
  }

  startOrder(orderId: string, employeeId: string): void {
    if (!orderId || !employeeId) {
      this.snackbarService.showErrorTranslated(
        'global.messages.orders.invalid_request'
      );
      return;
    }

    this.loading.set(true);

    this.partnerClient.orderClient
      .startOrder(new StartOrderCommand({ orderId, employeeId }))
      .pipe(
        tap(() => {
          this.snackbarService.showSuccessTranslated(
            'global.messages.orders.order_started'
          );
          // Reload order details to reflect new status
          this.loadOrderDetails(orderId);
        }),
        catchError(() => of(null)),
        finalize(() => this.loading.set(false))
      )
      .subscribe();
  }

  reset(): void {
    this.orderDetails.set(null);
    this.loading.set(false);
    this.error.set(null);
  }

  openCompleteOrderDialog(): void {
    const order = this.orderDetails();
    const employeeId = this.currentEmployeeId();

    if (!order || !employeeId) {
      this.snackbarService.showErrorTranslated(
        'global.messages.orders.invalid_request'
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

    ref.onClose.subscribe((result: CompleteOrderDialogResult) => {
      if (result) {
        this.store.dispatch(
          OrderActions.completeOrder({
            orderId: order.id!,
            employeeId,
            actualCompletionTimeMinutes: result.actualCompletionTimeMinutes,
            completionNotes: result.completionNotes,
          })
        );
        // Reload order details after completing
        setTimeout(() => this.loadOrderDetails(order.id!), 500);
      }
    });
  }
}
