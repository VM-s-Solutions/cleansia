import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';
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
  routes = CleansiaCustomerRoute;
}
