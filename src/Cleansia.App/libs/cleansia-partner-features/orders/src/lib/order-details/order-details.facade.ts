import { Injectable, inject, signal } from '@angular/core';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import {
  OrderItem,
  PartnerClient,
  StartOrderCommand,
} from '@cleansia/partner-services';
import * as OrderActions from '@cleansia/partner-stores';
import { SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { Actions, ofType } from '@ngrx/effects';
import { Store } from '@ngrx/store';
import { DialogService, DynamicDialogRef } from 'primeng/dynamicdialog';
import { catchError, finalize, of, take, takeUntil, tap } from 'rxjs';
import {
  CompleteOrderDialogComponent,
  CompleteOrderDialogData,
  CompleteOrderDialogResult,
} from '../components/complete-order-dialog';

@Injectable()
export class OrderDetailsFacade extends UnsubscribeControlDirective {
  private readonly partnerClient = inject(PartnerClient);
  private readonly snackbarService = inject(SnackbarService);
  private readonly translateService = inject(TranslateService);
  private readonly dialogService = inject(DialogService);
  private readonly store = inject(Store);
  private readonly actions$ = inject(Actions);

  // Signals for reactive state management
  readonly orderDetails = signal<OrderItem | null>(null);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly currentEmployeeId = signal<string | null>(null);

  loadOrderDetails(orderId: string): void {
    if (!orderId?.trim()) {
      this.error.set(this.translateService.instant('pages.order_details.not_found_message'));
      return;
    }

    this.loading.set(true);
    this.error.set(null);

    this.partnerClient.orderClient
      .getById(orderId)
      .pipe(
        takeUntil(this.destroyed$),
        tap((orderDetails) => {
          if (orderDetails) {
            this.orderDetails.set(orderDetails);
          } else {
            this.error.set(this.translateService.instant('pages.order_details.not_found_message'));
          }
        }),
        catchError((error) => {
          const errorMessage = error?.status === 404
            ? this.translateService.instant('pages.order_details.not_found_message')
            : this.translateService.instant('pages.order_details.load_failed');
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
        takeUntil(this.destroyed$),
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
        takeUntil(this.destroyed$),
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

    ref.onClose.pipe(takeUntil(this.destroyed$)).subscribe((result: CompleteOrderDialogResult) => {
      if (result) {
        // Subscribe to action result to reload only on success
        this.actions$
          .pipe(
            ofType(
              OrderActions.completeOrderSuccess,
              OrderActions.completeOrderFailure
            ),
            take(1),
            takeUntil(this.destroyed$)
          )
          .subscribe((action) => {
            if (action.type === OrderActions.completeOrderSuccess.type) {
              // Only reload order details on success
              this.loadOrderDetails(order.id!);
            }
            // On failure, do not reload - the error message is already shown by the effect
          });

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
}
