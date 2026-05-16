import { Injectable, inject, signal } from '@angular/core';
import {
  AdminClient,
  ClosePayPeriodCommand,
  PayPeriodDto,
} from '@cleansia/admin-services';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import { SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { catchError, finalize, of, takeUntil } from 'rxjs';

@Injectable()
export class PayPeriodDetailFacade extends UnsubscribeControlDirective {
  private readonly adminClient = inject(AdminClient);
  private readonly snackbarService = inject(SnackbarService);
  private readonly translate = inject(TranslateService);

  readonly payPeriod = signal<PayPeriodDto | null>(null);
  readonly loading = signal<boolean>(false);

  loadPayPeriodDetail(payPeriodId: string): void {
    this.loading.set(true);

    this.adminClient.adminPayPeriodClient
      .details(payPeriodId)
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of(null)),
        finalize(() => this.loading.set(false))
      )
      .subscribe((response) => {
        if (response) {
          this.payPeriod.set(response);
        }
      });
  }

  closePayPeriod(payPeriodId: string, notes?: string): void {
    const command = new ClosePayPeriodCommand({
      payPeriodId,
      notes,
    });

    this.adminClient.adminPayPeriodClient
      .close(command)
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of(null))
      )
      .subscribe((response) => {
        if (response) {
          this.snackbarService.showSuccess(
            this.translate.instant('pay_periods.messages.close_success')
          );
          // Reload the pay period to reflect the change
          this.loadPayPeriodDetail(payPeriodId);
        }
      });
  }

  // Format date for display
  formatDate(date: string | Date | null | undefined): string {
    if (!date) return '-';
    const dateObj = date instanceof Date ? date : new Date(date);
    return dateObj.toLocaleDateString('en-GB');
  }

  formatDateTime(date: string | Date | null | undefined): string {
    if (!date) return '-';
    const dateObj = date instanceof Date ? date : new Date(date);
    return dateObj.toLocaleString('en-GB');
  }

  // Get status badge class
  getStatusClass(status: string | null | undefined): string {
    if (!status) return 'status-badge status-unknown';

    const statusLower = status.toLowerCase();
    return `status-badge status-${statusLower}`;
  }
}
