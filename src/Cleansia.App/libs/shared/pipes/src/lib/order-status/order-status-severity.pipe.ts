import { Pipe, PipeTransform } from '@angular/core';
import { OrderStatus } from '@cleansia/models';
import { TagSeverity } from '@cleansia/types';

/**
 * Maps an order status to the PrimeNG severity token.
 *
 * **A pipe rather than a helper function on purpose**: a helper called inside `@for` re-ran once per
 * row on every change-detection pass. A pure pipe memoizes by input identity, so the lookup runs at
 * most once per status change. → /domain/order-lifecycle
 */
@Pipe({
  name: 'orderStatusSeverity',
  standalone: true,
})
export class OrderStatusSeverityPipe implements PipeTransform {
  transform(status: OrderStatus | { value?: number } | number | null | undefined): TagSeverity {
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
