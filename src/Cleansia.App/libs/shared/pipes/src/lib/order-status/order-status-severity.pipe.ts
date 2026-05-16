import { Pipe, PipeTransform } from '@angular/core';
import { OrderStatus } from '@cleansia/partner-services';

/**
 * Maps an `OrderStatus` (or wrapper `{ value?: number }`) to the PrimeNG
 * severity token used by `<p-tag>`.
 *
 * Why a pipe and not a function helper:
 *  - Templates that previously called `getOrderStatusSeverity(status)` inside
 *    `@for` blocks re-ran the helper on every change-detection pass — once
 *    per row. With this pipe (pure by default), Angular memoizes by input
 *    identity so the lookup runs at most once per status reference change.
 *  - Single source of truth shared across customer/partner/admin instead of
 *    four byte-identical switch statements.
 *
 * Severities chosen to match PrimeNG's `Tag` token vocabulary:
 *  - `warn`    → "needs attention" (Pending)
 *  - `info`    → neutral progress (Confirmed, InProgress, default)
 *  - `success` → terminal happy path (Completed)
 *  - `danger`  → terminal sad path (Cancelled)
 */
@Pipe({
  name: 'orderStatusSeverity',
  standalone: true,
})
export class OrderStatusSeverityPipe implements PipeTransform {
  transform(status: OrderStatus | { value?: number } | number | null | undefined): string {
    const value = typeof status === 'number' ? status : status?.value;
    switch (value) {
      case OrderStatus.Pending:
        return 'warn';
      case OrderStatus.Confirmed:
      case OrderStatus.OnTheWay:
      case OrderStatus.InProgress:
        return 'info';
      case OrderStatus.Completed:
        return 'success';
      case OrderStatus.Cancelled:
        return 'danger';
      default:
        return 'info';
    }
  }
}
