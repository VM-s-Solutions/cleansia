import { Injectable, inject, signal } from '@angular/core';
import { AdminClient, PayPeriodDto } from '@cleansia/admin-services';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import { formatDate } from '@cleansia/utils';
import { TranslateService } from '@ngx-translate/core';
import { catchError, finalize, of, takeUntil } from 'rxjs';
import { formatPayPeriodDuration } from '../pay-period-management/pay-period-management.models';

@Injectable()
export class PayPeriodDetailFacade extends UnsubscribeControlDirective {
  private readonly adminClient = inject(AdminClient);
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

  formatDate(date: string | Date | null | undefined): string {
    return formatDate(date, this.translate.currentLang, 'utcDate') || '-';
  }

  formatDuration(days: number): string {
    return formatPayPeriodDuration(days, this.translate);
  }

  formatDateTime(date: string | Date | null | undefined): string {
    return formatDate(date, this.translate.currentLang, 'dateTime') || '-';
  }
}
