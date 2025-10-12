import { Component, input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TranslatePipe } from '@ngx-translate/core';

@Component({
  selector: 'order-additional-services',
  standalone: true,
  imports: [CommonModule, TranslatePipe],
  template: `
    @if (services() && services()!.length > 0) {
    <div class="cleansia-order-details__additional-services">
      <h4>{{ 'pages.order_details.additional_services' | translate }}</h4>
      <div class="cleansia-order-details__services-list">
        @for (service of services(); track service.id) {
        <div class="service-item">
          <div class="service-icon">
            <i class="pi pi-plus"></i>
          </div>
          <div class="service-content">
            <span class="service-name">{{ service.name }}</span>
            <span class="service-description">{{ service.description }}</span>
          </div>
        </div>
        }
      </div>
    </div>
    }
  `,
})
export class OrderAdditionalServicesComponent {
  services = input<any[]>();
}
