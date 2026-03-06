import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { CleansiaCustomerRoute } from '@cleansia/services';
import { TranslateModule } from '@ngx-translate/core';

@Component({
  selector: 'cleansia-customer-checkout-cancel',
  standalone: true,
  imports: [TranslateModule, RouterLink],
  template: `
    <div class="checkout-result checkout-result--cancel">
      <i class="pi pi-times-circle"></i>
      <h1>{{ 'pages.checkout.cancel.title' | translate }}</h1>
      <p>{{ 'pages.checkout.cancel.description' | translate }}</p>
      <div class="checkout-result__actions">
        <a [routerLink]="'/' + routes.ORDER" class="checkout-result__link checkout-result__link--primary">
          {{ 'pages.checkout.cancel.try_again' | translate }}
        </a>
        <a [routerLink]="'/' + routes.ORDERS" class="checkout-result__link checkout-result__link--secondary">
          {{ 'pages.checkout.cancel.view_orders' | translate }}
        </a>
      </div>
    </div>
  `,
  styles: [`
    .checkout-result {
      max-width: 600px;
      margin: 4rem auto;
      text-align: center;
      padding: 3rem;
      border-radius: 12px;

      &--cancel {
        i { font-size: 4rem; color: var(--red-500); margin-bottom: 1rem; }
      }

      h1 { margin-bottom: 0.5rem; }
      p { color: var(--text-color-secondary); margin-bottom: 2rem; }

      &__actions { display: flex; gap: 1rem; justify-content: center; }

      &__link {
        display: inline-block;
        padding: 0.75rem 2rem;
        border-radius: 8px;
        text-decoration: none;
        font-weight: 600;

        &--primary {
          background: var(--primary-color);
          color: white;
          &:hover { opacity: 0.9; }
        }

        &--secondary {
          border: 1px solid var(--surface-border);
          color: var(--text-color);
          &:hover { background: var(--surface-hover); }
        }
      }
    }
  `],
})
export class CheckoutCancelComponent {
  routes = CleansiaCustomerRoute;
}
