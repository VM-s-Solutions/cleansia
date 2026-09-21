import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { CleansiaButtonComponent, CleansiaStatusBadgeComponent } from '@cleansia/components';
import { Code } from '@cleansia/partner-services';
import { TranslatePipe } from '@ngx-translate/core';

@Component({
  selector: 'cleansia-partner-order-header',
  standalone: true,
  imports: [TranslatePipe, CleansiaButtonComponent, CleansiaStatusBadgeComponent],
  templateUrl: './order-header.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class OrderHeaderComponent {
  orderNumber = input.required<string>();
  hasInvoice = input<boolean>(false);
  orderStatus = input<Code | null>(null);
  paymentStatus = input<Code | null>(null);
  createdOn = input<string>('');
  confirmationCode = input<string>('');

  print = output<void>();
  downloadInvoice = output<void>();
}
