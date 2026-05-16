import { Pipe, PipeTransform } from '@angular/core';
import { PaymentStatus } from '@cleansia/partner-services';

/**
 * Maps a `PaymentStatus` to the PrimeNG `<p-tag>` severity token. See
 * `OrderStatusSeverityPipe` for the rationale on pipe-vs-helper.
 */
@Pipe({
  name: 'paymentStatusSeverity',
  standalone: true,
})
export class PaymentStatusSeverityPipe implements PipeTransform {
  transform(status: PaymentStatus | { value?: number } | number | null | undefined): string {
    const value = typeof status === 'number' ? status : status?.value;
    switch (value) {
      case PaymentStatus.Pending:
        return 'warn';
      case PaymentStatus.Paid:
        return 'success';
      case PaymentStatus.Failed:
      case PaymentStatus.Disputed:
        return 'danger';
      case PaymentStatus.Refunded:
        return 'info';
      default:
        return 'info';
    }
  }
}
