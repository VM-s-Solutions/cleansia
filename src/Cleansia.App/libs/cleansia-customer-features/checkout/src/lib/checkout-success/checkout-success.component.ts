import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { CustomerAuthService } from '@cleansia/customer-services';
import { CleansiaCustomerRoute } from '@cleansia/services';
import { CleansiaDynamicBackgroundComponent } from '@cleansia/components';
import { TranslatePipe } from '@ngx-translate/core';

@Component({
  selector: 'cleansia-customer-checkout-success',
  standalone: true,
  imports: [TranslatePipe, RouterLink, CleansiaDynamicBackgroundComponent],
  templateUrl: './checkout-success.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CheckoutSuccessComponent {
  private readonly authService = inject(CustomerAuthService);
  routes = CleansiaCustomerRoute;
  ordersRoute = this.authService.isLoggedIn()
    ? '/' + CleansiaCustomerRoute.ORDERS
    : '/' + CleansiaCustomerRoute.TRACK_ORDER;
}
