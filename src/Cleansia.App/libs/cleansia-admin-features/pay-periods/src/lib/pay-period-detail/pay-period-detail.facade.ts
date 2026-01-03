import { Injectable, inject, signal } from '@angular/core';
import {
  AdminClient,
  ClosePayPeriodCommand,
  PayPeriodDto,
} from '@cleansia/admin-services';
import { SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { Subject, catchError, finalize, of, takeUntil } from 'rxjs';

@Injectable()
export class PayPeriodDetailFacade {
  private readonly adminClient = inject(AdminClient);
  private readonly snackbarService = inject(SnackbarService);
  private readonly translate = inject(TranslateService);

  private destroy$ = new Subject<void>();

  readonly payPeriod = signal<PayPeriodDto | null>(null);
  readonly loading = signal<boolean>(false);

  loadPayPeriodDetail(payPeriodId: string): void {
    this.loading.set(true);

    this.adminClient.adminPayPeriodClient
      .details(payPeriodId)
      .pipe(
        takeUntil(this.destroy$),
        catchError((error) => {
          this.snackbarService.showError(
            this.translate.instant('payPeriods.messages.loadDetailError')
          );
          console.error('Error loading pay period details:', error);
          return of(null);
        }),
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
        takeUntil(this.destroy$),
        catchError((error) => {
          this.snackbarService.showError(
            this.translate.instant('payPeriods.messages.closeError')
          );
          console.error('Error closing pay period:', error);
          return of(null);
        })
      )
      .subscribe((response) => {
        if (response) {
          this.snackbarService.showSuccess(
            this.translate.instant('payPeriods.messages.closeSuccess')
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
    return dateObj.toLocaleDateString('cs-CZ');
  }

  formatDateTime(date: string | Date | null | undefined): string {
    if (!date) return '-';
    const dateObj = date instanceof Date ? date : new Date(date);
    return dateObj.toLocaleString('cs-CZ');
  }

  // Get status badge class
  getStatusClass(status: string | null | undefined): string {
    if (!status) return 'status-badge status-unknown';

    const statusLower = status.toLowerCase();
    return `status-badge status-${statusLower}`;
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }
}
