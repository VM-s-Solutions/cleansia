import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { CleansiaCustomerRoute } from '@cleansia/services';
import { TranslateModule } from '@ngx-translate/core';

@Component({
  selector: 'cleansia-customer-checkout-success',
  standalone: true,
  imports: [TranslateModule, RouterLink],
  template: `
    <div class="checkout-result checkout-result--success">
      <i class="pi pi-check-circle"></i>
      <h1>{{ 'pages.checkout.success.title' | translate }}</h1>
      <p>{{ 'pages.checkout.success.description' | translate }}</p>
      <a [routerLink]="'/' + routes.ORDERS" class="checkout-result__link">
        {{ 'pages.checkout.success.view_orders' | translate }}
      </a>
    </div>
  `,
  styles: [`
    .checkout-result {
      max-width: 600px;
      margin: 4rem auto;
      text-align: center;
      padding: 3rem;
      border-radius: 12px;

      &--success {
        i { font-size: 4rem; color: var(--green-500); margin-bottom: 1rem; }
      }

      h1 { margin-bottom: 0.5rem; }
      p { color: var(--text-color-secondary); margin-bottom: 2rem; }

      &__link {
        display: inline-block;
        padding: 0.75rem 2rem;
        background: var(--primary-color);
        color: white;
        border-radius: 8px;
        text-decoration: none;
        font-weight: 600;
        &:hover { opacity: 0.9; }
      }
    }
  `],
})
export class CheckoutSuccessComponent {
  routes = CleansiaCustomerRoute;
}
