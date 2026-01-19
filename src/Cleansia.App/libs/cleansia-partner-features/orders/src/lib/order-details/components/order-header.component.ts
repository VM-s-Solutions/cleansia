import { Component, input, output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { CleansiaButtonComponent } from '@cleansia/components';
import { TranslatePipe } from '@ngx-translate/core';

interface StatusInfo {
  name?: string;
  value: number;
}

@Component({
  selector: 'order-header',
  standalone: true,
  imports: [CommonModule, CleansiaButtonComponent, TranslatePipe],
  template: `
    <!-- Status Banner -->
    <div class="cleansia-order-details__status-banner">
      <div class="order-header-info">
        <div class="order-number">
          <span class="label">{{ 'pages.order_details.order_number' | translate }}:</span>
          <span class="value">{{ orderNumber() }}</span>
        </div>
        <div class="order-statuses">
          <span [class]="getOrderStatusClass(orderStatus())">
            {{ orderStatusLabel() }}
          </span>
          <span [class]="getPaymentStatusClass(paymentStatus())">
            {{ paymentStatusLabel() }}
          </span>
        </div>
      </div>
      <div class="order-meta">
        <span class="meta-item">
          <i class="pi pi-calendar"></i>
          {{ createdOn() }}
        </span>
        @if (confirmationCode()) {
        <span class="meta-item">
          <i class="pi pi-key"></i>
          {{ confirmationCode() }}
        </span>
        }
      </div>
    </div>

    <div class="cleansia-order-details__header">
      <div class="cleansia-order-details__header-actions">
        <cleansia-button
          [buttonType]="'button'"
          [style]="'raised-button'"
          [title]="'pages.order_details.print' | translate"
          [icon]="'pi pi-print'"
          (clickFn)="onPrint.emit()"
        />
        @if (hasInvoice()) {
        <cleansia-button
          [buttonType]="'button'"
          [style]="'raised-button'"
          [title]="'pages.order_details.download_invoice' | translate"
          [icon]="'pi pi-download'"
          (clickFn)="onDownloadInvoice.emit()"
        />
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
