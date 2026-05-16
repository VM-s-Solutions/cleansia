import { Pipe, PipeTransform } from '@angular/core';
import { OrderStatus } from '@cleansia/partner-services';

/**
 * Maps an `OrderStatus` to a PrimeIcon class. Used in timelines + status
 * pills. See `OrderStatusSeverityPipe` for rationale on pipe-vs-helper.
 */
@Pipe({
  name: 'orderStatusIcon',
  standalone: true,
})
export class OrderStatusIconPipe implements PipeTransform {
  transform(status: OrderStatus | { value?: number } | number | null | undefined): string {
    const value = typeof status === 'number' ? status : status?.value;
    switch (value) {
      case OrderStatus.Pending:
        return 'pi pi-clock';
      case OrderStatus.Confirmed:
        return 'pi pi-check';
      case OrderStatus.OnTheWay:
        return 'pi pi-send';
      case OrderStatus.InProgress:
        return 'pi pi-spin pi-spinner';
      case OrderStatus.Completed:
        return 'pi pi-check-circle';
      case OrderStatus.Cancelled:
        return 'pi pi-times-circle';
      default:
        return 'pi pi-circle';
    }
  }
}
