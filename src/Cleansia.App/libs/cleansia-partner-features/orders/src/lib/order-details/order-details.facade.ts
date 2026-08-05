import { Injectable, inject, signal } from '@angular/core';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import {
  AddOrderNoteCommand,
  CompleteOrderCommand,
  MarkCashCollectedCommand,
  OrderItem,
  OrderStatus,
  PartnerClient,
  ReportOrderIssueCommand,
  StartOrderCommand,
  TakeOrderCommand,
} from '@cleansia/partner-services';
import * as OrderActions from '@cleansia/partner-stores';
import { SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { Actions, ofType } from '@ngrx/effects';
import { Store } from '@ngrx/store';
import { DialogService, DynamicDialogRef } from 'primeng/dynamicdialog';
import { catchError, finalize, of, take, takeUntil, tap } from 'rxjs';
import {
  ReportIssueDialogComponent,
  ReportIssueDialogResult,
} from '../components/report-issue-dialog';
import {
  AddNoteDialogComponent,
  AddNoteDialogResult,
} from '../components/add-note-dialog';
import {
  MarkCashCollectedDialogComponent,
  MarkCashCollectedDialogResult,
} from '../components/mark-cash-collected-dialog';
import { canMarkCashCollected, formatCurrency } from './order-details.helpers';

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
  readonly takeInFlight = signal(false);

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

  startOrder(orderId: string): void {
    if (!orderId) {
      this.snackbarService.showErrorTranslated(
        'global.messages.orders.invalid_request'
      );
      return;
    }

    this.loading.set(true);

    const command = new StartOrderCommand();
    command.orderId = orderId;

    this.partnerClient.orderClient
      .startOrder(command)
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

  takeOrder(orderId: string): void {
    if (!orderId) {
      this.snackbarService.showErrorTranslated(
        'global.messages.orders.invalid_request'
      );
      return;
    }

    if (this.takeInFlight()) {
      return;
    }

    this.takeInFlight.set(true);
    this.loading.set(true);

    const command = new TakeOrderCommand();
    command.orderId = orderId;

    this.partnerClient.orderClient
      .takeOrder(command)
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of(null)),
        finalize(() => this.takeInFlight.set(false))
      )
      .subscribe((response) => {
        if (response) {
          this.snackbarService.showSuccessTranslated(
            'pages.orders.order_taken_success'
          );
        }
        // Re-read on refusal as well as on success so the button reflects the
        // server instead of staying armed for another click. The re-read owns
        // `loading` from here, keeping the spinner unbroken.
        this.loadOrderDetails(orderId);
      });
  }

  reset(): void {
    this.orderDetails.set(null);
    this.loading.set(false);
    this.error.set(null);
  }

  completeOrder(): void {
    const order = this.orderDetails();
    const employeeId = this.currentEmployeeId();

    if (!order || !employeeId) {
      this.snackbarService.showErrorTranslated(
        'global.messages.orders.invalid_request'
      );
      return;
    }

    // Calculate actual minutes from InProgress status start to now
    const inProgressEntry = order.statusHistory?.find(h => h.status.value === OrderStatus.InProgress);
    let actualMinutes = 0;
    if (inProgressEntry) {
      const start = new Date(inProgressEntry.createdOn);
      actualMinutes = Math.max(1, Math.floor((Date.now() - start.getTime()) / 60000));
    }

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
          this.loadOrderDetails(order.id!);
        }
      });

    this.store.dispatch(
      OrderActions.completeOrder({
        orderId: order.id!,
        actualCompletionTimeMinutes: actualMinutes,
        completionNotes: '',
      })
    );
  }

  // Surfaces a translated warning when the partner tries to Complete an order without after photos.
  warnMissingAfterPhotos(): void {
    this.snackbarService.showErrorTranslated(
      'pages.order_details.complete_missing_after_photos'
    );
  }

  // Active statuses where notes / issues may be added: Confirmed, OnTheWay, InProgress.
  private isActiveOrderStatus(orderStatusValue: number): boolean {
    return (
      orderStatusValue === OrderStatus.Confirmed ||
      orderStatusValue === OrderStatus.OnTheWay ||
      orderStatusValue === OrderStatus.InProgress
    );
  }

  openReportIssueDialog(): void {
    const order = this.orderDetails();

    if (!order) {
      this.snackbarService.showErrorTranslated(
        'global.messages.orders.invalid_request'
      );
      return;
    }

    // Pre-flight status gate: refuse to open the dialog outside of active statuses.
    if (!this.isActiveOrderStatus(order.orderStatus.value)) {
      this.snackbarService.showErrorTranslated(
        'pages.order_details.note_issue_gating_error'
      );
      return;
    }

    const ref: DynamicDialogRef = this.dialogService.open(
      ReportIssueDialogComponent,
      {
        header: undefined,
        data: { orderId: order.id },
        width: '500px',
        modal: true,
        dismissableMask: true,
      }
    );

    ref.onClose.pipe(takeUntil(this.destroyed$)).subscribe((result: ReportIssueDialogResult) => {
      if (result) {
        this.loading.set(true);

        const command = new ReportOrderIssueCommand();
        command.orderId = order.id;
        command.description = result.description;

        this.partnerClient.orderClient
          .reportIssue(command)
          .pipe(
            takeUntil(this.destroyed$),
            tap(() => {
              this.snackbarService.showSuccessTranslated(
                'global.messages.orders.issue_reported'
              );
              this.loadOrderDetails(order.id!);
            }),
            catchError(() => of(null)),
            finalize(() => this.loading.set(false))
          )
          .subscribe();
      }
    });
  }

  openAddNoteDialog(): void {
    const order = this.orderDetails();

    if (!order) {
      this.snackbarService.showErrorTranslated(
        'global.messages.orders.invalid_request'
      );
      return;
    }

    // Pre-flight status gate: refuse to open the dialog outside of active statuses.
    if (!this.isActiveOrderStatus(order.orderStatus.value)) {
      this.snackbarService.showErrorTranslated(
        'pages.order_details.note_issue_gating_error'
      );
      return;
    }

    const ref: DynamicDialogRef = this.dialogService.open(
      AddNoteDialogComponent,
      {
        header: undefined,
        data: { orderId: order.id },
        width: '500px',
        modal: true,
        dismissableMask: true,
      }
    );

    ref.onClose.pipe(takeUntil(this.destroyed$)).subscribe((result: AddNoteDialogResult) => {
      if (result) {
        this.loading.set(true);

        const command = new AddOrderNoteCommand();
        command.orderId = order.id;
        command.content = result.content;

        this.partnerClient.orderClient
          .addNote(command)
          .pipe(
            takeUntil(this.destroyed$),
            tap(() => {
              this.snackbarService.showSuccessTranslated(
                'global.messages.orders.note_added'
              );
              this.loadOrderDetails(order.id!);
            }),
            catchError(() => of(null)),
            finalize(() => this.loading.set(false))
          )
          .subscribe();
      }
    });
  }

  /**
   * Opens the custom cash-collection confirmation. Taking cash is irreversible — it flips the
   * order to Paid — so it is never fired straight off the button click.
   */
  openMarkCashCollectedDialog(): void {
    const order = this.orderDetails();
    const employeeId = this.currentEmployeeId();

    if (!order || !employeeId) {
      this.snackbarService.showErrorTranslated(
        'global.messages.orders.invalid_request'
      );
      return;
    }

    // Pre-flight gate mirroring MarkCashCollected.Validator: InProgress, not already Paid,
    // caller assigned. Defence in depth — the trigger is already hidden outside this window.
    if (
      !canMarkCashCollected(
        order.orderStatus.value,
        order.paymentStatus.value,
        order.assignedEmployees,
        employeeId
      )
    ) {
      this.snackbarService.showErrorTranslated(
        'pages.order_details.mark_cash_collected_gating_error'
      );
      return;
    }

    const ref: DynamicDialogRef = this.dialogService.open(
      MarkCashCollectedDialogComponent,
      {
        header: undefined,
        data: {
          orderId: order.id,
          amount: formatCurrency(order.totalPrice, order.currency?.symbol ?? ''),
        },
        width: '500px',
        modal: true,
        dismissableMask: true,
      }
    );

    ref.onClose
      .pipe(takeUntil(this.destroyed$))
      .subscribe((result: MarkCashCollectedDialogResult) => {
        if (result?.confirmed) {
          this.markCashCollected(order.id!);
        }
      });
  }

  markCashCollected(orderId: string): void {
    if (!orderId) {
      this.snackbarService.showErrorTranslated(
        'global.messages.orders.invalid_request'
      );
      return;
    }

    this.loading.set(true);

    const command = new MarkCashCollectedCommand();
    command.orderId = orderId;

    this.partnerClient.orderClient
      .markCashCollected(command)
      .pipe(
        takeUntil(this.destroyed$),
        tap(() => {
          this.snackbarService.showSuccessTranslated(
            'global.messages.orders.cash_collected'
          );
          // Reload so the payment status reflects the collection.
          this.loadOrderDetails(orderId);
        }),
        catchError((error) => {
          this.snackbarService.showApiError(
            error,
            'global.messages.orders.cash_collect_failed'
          );
          return of(null);
        }),
        finalize(() => this.loading.set(false))
      )
      .subscribe();
  }
}
