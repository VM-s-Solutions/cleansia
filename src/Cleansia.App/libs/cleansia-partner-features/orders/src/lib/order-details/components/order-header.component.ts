import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TranslatePipe } from '@ngx-translate/core';

interface StatusInfo {
  name?: string;
  value: number;
}

@Component({
  selector: 'order-header',
  standalone: true,
  imports: [CommonModule, TranslatePipe],
  templateUrl: './order-header.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class OrderHeaderComponent {
  orderNumber = input.required<string>();
  hasInvoice = input<boolean>(false);
  orderStatus = input<StatusInfo | null>(null);
  paymentStatus = input<StatusInfo | null>(null);
  orderStatusLabel = input<string>('');
  paymentStatusLabel = input<string>('');
  createdOn = input<string>('');
  confirmationCode = input<string>('');

  onPrint = output<void>();
  onDownloadInvoice = output<void>();

  getOrderStatusClass(status: StatusInfo | null): string {
    if (!status?.name) return 'order-status-badge';
    const statusKey = status.name.toLowerCase().replace(/\s+/g, '');
    return `order-status-badge status-${statusKey}`;
  }

  getPaymentStatusClass(status: StatusInfo | null): string {
    if (!status?.name) return 'payment-status-badge';
    const statusKey = status.name.toLowerCase().replace(/\s+/g, '');
    return `payment-status-badge status-${statusKey}`;
  }
}
