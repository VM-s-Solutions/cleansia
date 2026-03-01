import { Component, input, output } from '@angular/core';
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
  template: `
    <div class="cleansia-order-details__status-banner">
      <!-- Top row: order number + statuses -->
      <div class="order-header-top">
        <span class="order-header-number">{{ orderNumber() }}</span>
        <div class="order-header-badges">
          <span [class]="getOrderStatusClass(orderStatus())">
            {{ orderStatusLabel() }}
          </span>
          <span [class]="getPaymentStatusClass(paymentStatus())">
            {{ paymentStatusLabel() }}
          </span>
        </div>
      </div>
      <!-- Meta row -->
      <div class="order-header-meta">
        <span class="order-header-meta__item">
          <i class="pi pi-calendar"></i>
          {{ createdOn() }}
        </span>
        @if (confirmationCode()) {
        <span class="order-header-meta__item">
          <i class="pi pi-key"></i>
          {{ confirmationCode() }}
        </span>
        }
      </div>
      <!-- Actions row -->
      <div class="order-header-actions">
        <button type="button" class="order-header-action-btn" (click)="onPrint.emit()">
          <i class="pi pi-print"></i>
          <span>{{ 'pages.order_details.print' | translate }}</span>
        </button>
        @if (hasInvoice()) {
        <button type="button" class="order-header-action-btn" (click)="onDownloadInvoice.emit()">
          <i class="pi pi-download"></i>
          <span>{{ 'pages.order_details.download_invoice' | translate }}</span>
        </button>
        }
      </div>
    </div>
  `,
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
