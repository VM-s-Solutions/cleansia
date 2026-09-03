import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { CustomerAuthService } from '@cleansia/customer-services';
import { CleansiaCustomerRoute } from '@cleansia/services';
import { TranslatePipe } from '@ngx-translate/core';

/**
 * Where Stripe returns a customer who backed out. The board draws this as the
 * confirmation page's second state, and it reads as one: the same shell, an
 * amber panel instead of a headline claiming success.
 *
 * Nothing was charged and the ORDER STILL EXISTS — `CreateOrder` runs before
 * the redirect to Stripe, so backing out of payment leaves a real, unpaid
 * booking rather than nothing. The copy has to say that, or the customer books
 * a second time.
 */
@Component({
  selector: 'cleansia-customer-checkout-cancel',
  standalone: true,
  imports: [TranslatePipe, RouterLink],
  templateUrl: './checkout-cancel.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CheckoutCancelComponent {
  private readonly authService = inject(CustomerAuthService);

  readonly routes = CleansiaCustomerRoute;
  readonly ordersRoute = this.authService.isLoggedIn()
    ? '/' + CleansiaCustomerRoute.ORDERS
    : '/' + CleansiaCustomerRoute.TRACK_ORDER;
}
