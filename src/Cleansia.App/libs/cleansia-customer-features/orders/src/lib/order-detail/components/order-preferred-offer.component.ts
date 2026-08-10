import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, Input } from '@angular/core';
import { FormsModule } from '@angular/forms';
import {
  CleansiaButtonComponent,
  CleansiaSelectComponent,
} from '@cleansia/components';
import { PreferredOfferState } from '@cleansia/customer-services';
import { TranslatePipe } from '@ngx-translate/core';
import { SkeletonModule } from 'primeng/skeleton';
import { OrderPreferredOfferFacade } from '../order-preferred-offer.facade';

@Component({
  selector: 'cleansia-customer-order-preferred-offer',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    TranslatePipe,
    SkeletonModule,
    CleansiaButtonComponent,
    CleansiaSelectComponent,
  ],
  templateUrl: './order-preferred-offer.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class OrderPreferredOfferComponent {
  @Input({ required: true }) facade!: OrderPreferredOfferFacade;
  @Input() locale = 'en-US';

  protected readonly PreferredOfferState = PreferredOfferState;

  onSelect(employeeId: string | null): void {
    this.facade.select(employeeId ?? null);
  }

  /**
   * The deadline as an instant, not a countdown: an instant is a fact, and it is the fact the
   * customer needs because the closure message arrives at roughly that time.
   */
  formatRespondBy(respondByUtc: Date | null): string {
    if (!respondByUtc) return '';
    return new Date(respondByUtc).toLocaleString(this.locale, {
      day: '2-digit',
      month: '2-digit',
      hour: '2-digit',
      minute: '2-digit',
    });
  }
}
