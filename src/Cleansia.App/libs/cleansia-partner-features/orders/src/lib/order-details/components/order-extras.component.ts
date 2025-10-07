import { Component, input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TranslatePipe } from '@ngx-translate/core';

@Component({
  selector: 'order-extras',
  styleUrls: ['../order-details.component.scss'],
  standalone: true,
  imports: [CommonModule, TranslatePipe],
  template: `
    @if (extrasEntries() && extrasEntries()!.length > 0) {
    <div class="cleansia-order-details__extras">
      <h4>{{ 'pages.order_details.extras' | translate }}</h4>
      <div class="cleansia-order-details__extras-list">
        @for (extra of extrasEntries(); track extra[0]) {
        <div class="extra-item">
          <i class="pi pi-check-circle"></i>
          <span>{{ extra[0] }}</span>
        </div>
        }
      </div>
    </div>
    }
  `,
})
export class OrderExtrasComponent {
  extrasEntries = input<[string, boolean][]>();
}
