import { Pipe, PipeTransform } from '@angular/core';
import { OrderStatus } from '@cleansia/models';

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
      // New is EXPLICIT, not the default arm. It is the resting state of every booking that nobody
      // has taken yet — including a card order the customer has already paid for, since T-0691 made
      // Confirmed mean "a cleaner took this job" and nothing else. Falling through to the generic dot
      // is the same shape as the admin blank-pill defect (T-0687): a zero-valued enum member with no
      // case of its own, rendering as an absence.
      case OrderStatus.New:
        return 'pi pi-inbox';
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
