import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { CustomerAuthService } from '@cleansia/customer-services';
import { CleansiaCustomerRoute } from '@cleansia/services';
import { CleansiaDynamicBackgroundComponent } from '@cleansia/components';
import { TranslatePipe } from '@ngx-translate/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { map } from 'rxjs';

@Component({
  selector: 'cleansia-customer-checkout-success',
  standalone: true,
  imports: [TranslatePipe, RouterLink, CleansiaDynamicBackgroundComponent],
  templateUrl: './checkout-success.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CheckoutSuccessComponent {
  private readonly authService = inject(CustomerAuthService);
  private readonly route = inject(ActivatedRoute);

  routes = CleansiaCustomerRoute;
  ordersRoute = this.authService.isLoggedIn()
    ? '/' + CleansiaCustomerRoute.ORDERS
    : '/' + CleansiaCustomerRoute.TRACK_ORDER;

  private readonly paymentType = toSignal(
    this.route.queryParamMap.pipe(map((params) => params.get('type') ?? 'card')),
    { initialValue: 'card' }
  );

  isCash = computed(() => this.paymentType() === 'cash');
}
