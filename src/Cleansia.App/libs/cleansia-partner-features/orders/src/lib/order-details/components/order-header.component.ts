import { Component, input, output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { CleansiaButtonComponent } from '@cleansia/components';
import { TranslatePipe } from '@ngx-translate/core';

@Component({
  selector: 'order-header',
  styleUrls: ['../order-details.component.scss'],
  standalone: true,
  imports: [CommonModule, CleansiaButtonComponent, TranslatePipe],
  template: `
    <div class="cleansia-order-details__header">
      <div class="cleansia-order-details__header-info">
        <div class="cleansia-order-details__order-badge">
          <div class="cleansia-order-details__order-number">
            {{ 'pages.order_details.order_number' | translate }}:
            <span>{{ orderNumber() }}</span>
          </div>
        </div>
      </div>

      <div class="cleansia-order-details__header-actions">
        <cleansia-button
          [buttonType]="'button'"
          [style]="'raised-button'"
          [title]="'pages.order_details.print' | translate"
          [icon]="'pi pi-print'"
          (clickFn)="onPrint.emit()"
        />
        <cleansia-button
          [buttonType]="'button'"
          [style]="'raised-button'"
          [title]="'pages.order_details.download_invoice' | translate"
          [icon]="'pi pi-download'"
          (clickFn)="onDownloadInvoice.emit()"
        />
      </div>
    </div>
  `,
})
export class OrderHeaderComponent {
  orderNumber = input.required<string>();

  onPrint = output<void>();
  onDownloadInvoice = output<void>();
}
