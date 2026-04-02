import { Component, input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TranslatePipe } from '@ngx-translate/core';

@Component({
  selector: 'order-packages',
  standalone: true,
  imports: [CommonModule, TranslatePipe],
  template: `
    @if (packages() && packages()!.length > 0) {
    <div class="cleansia-order-details__packages-section">
      <h4>{{ 'pages.order_details.selected_packages' | translate }}</h4>

      @for (pkg of packages(); track pkg.id) {
      <div class="cleansia-order-details__package-item">
        <div class="cleansia-order-details__package-header">
          <h5>{{ pkg.name }}</h5>
          <span class="package-price">{{ pkg.price | currency:currencyCode():'symbol':'1.2-2' }}</span>
        </div>

        @if (pkg.description) {
        <p class="cleansia-order-details__package-description">{{ pkg.description }}</p>
        }

        @if (pkg.includedServices && pkg.includedServices.length > 0) {
        <div class="cleansia-order-details__package-services">
          <h6 class="cleansia-order-details__package-services-title">
            <i class="pi pi-check-circle"></i>
            {{ 'pages.order_details.included_services' | translate }}
          </h6>
          <div class="cleansia-order-details__package-services-grid">
            @for (service of pkg.includedServices; track service) {
            <div class="service-badge">
              <i class="pi pi-check"></i>
              <span>{{ service }}</span>
            </div>
            }
          </div>
        </div>
        }
      </div>
      }
    </div>
    }
  `,
})
export class OrderPackagesComponent {
  packages = input<any[]>();
  currencyCode = input<string>('CZK');
}
